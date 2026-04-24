using System.Collections;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

// ── GDD §10.4 神殿トラップ（ゾーン4専用）──────────────────────
//
// 4 種のトラップを 1 ファイルに収録。
// すべて Zone4 のシーン上に配置する独立コンポーネント。
//
// 配置手順:
//   PressurePlateArrow — 床の圧力板 GameObject にアタッチ。_arrowSpawnPoints を設定。
//   PendulumLog        — 丸太 GameObject にアタッチ。ハンドル点(_pivotOffset)を調整。
//   FallingCeiling     — 天井 GameObject にアタッチ。_targetRelic を設定（または自動検索）。
//   FakeFloor          — 偽床 GameObject にアタッチ。Collider は自動で無効化される。

// ─────────────────────────────────────────────────────────────────
// 圧力板＋矢トラップ
// ─────────────────────────────────────────────────────────────────
/// <summary>
/// GDD §10.4 — 圧力板矢トラップ。
/// 床の圧力板を踏むと壁面から矢が射出。1〜3 本同時、各 20 ダメージ。
/// リセット時間 5 秒後に再起動。
/// </summary>
public class PressurePlateArrow : MonoBehaviour
{
    private const float ARROW_DAMAGE     = 20f;
    private const float ARROW_SPEED      = 18f;
    private const float RESET_DELAY      = 5f;
    private const float ARROW_LIFETIME   = 3f;

    [Header("矢の射出口")]
    [SerializeField] private Transform[] _arrowSpawnPoints;  // 壁の矢穴位置(1〜3 個)

    private bool _triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        var target = other.GetComponentInParent<PlayerHealthSystem>();
        Vector3 targetPos = other.transform.position + Vector3.up;

        foreach (var spawnPoint in _arrowSpawnPoints)
        {
            if (spawnPoint == null) continue;
            FireArrow(spawnPoint, targetPos, target);
        }

        StartCoroutine(ResetAfter(RESET_DELAY));
    }

    private void FireArrow(Transform spawn, Vector3 targetPos, PlayerHealthSystem target)
    {
        // 矢を Capsule プリミティブで代替
        var arrow = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        arrow.transform.position   = spawn.position;
        arrow.transform.localScale = new Vector3(0.08f, 0.4f, 0.08f);
        arrow.name = "Arrow_Projectile";

        // 矢のマテリアル（茶色）
        var rend = arrow.GetComponent<Renderer>();
        if (rend != null)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", new Color(0.6f, 0.4f, 0.1f));
            rend.sharedMaterial = mat;
        }

        // プレイヤー方向を向く
        Vector3 dir = (targetPos - spawn.position).normalized;
        arrow.transform.rotation = Quaternion.LookRotation(dir) *
                                   Quaternion.Euler(90f, 0f, 0f);

        arrow.AddComponent<ArrowProjectile>().Init(dir, ARROW_SPEED, ARROW_DAMAGE, ARROW_LIFETIME);

        // GDD §15.2 — trap_arrow（矢の射出音）
        PPAudioManager.Instance?.PlaySE(SoundId.TrapArrow, spawn.position);
    }

    private IEnumerator ResetAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        _triggered = false;
    }
}

/// <summary>矢の飛翔挙動と当たり判定。</summary>
public class ArrowProjectile : MonoBehaviour
{
    private Vector3 _direction;
    private float   _speed;
    private float   _damage;

    public void Init(Vector3 dir, float speed, float damage, float lifetime)
    {
        _direction = dir;
        _speed     = speed;
        _damage    = damage;
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        transform.position += _direction * (_speed * Time.deltaTime);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            other.GetComponentInParent<PlayerHealthSystem>()?.TakeDamage(_damage);
            Destroy(gameObject);
        }
    }
}

// ─────────────────────────────────────────────────────────────────
// 振り子丸太
// ─────────────────────────────────────────────────────────────────
/// <summary>
/// GDD §10.4 — 振り子丸太。
/// 周期 4 秒でサイン波スイング。当たったプレイヤーに 30 ダメージ + 吹き飛ばし。
/// </summary>
public class PendulumLog : MonoBehaviour
{
    private const float DAMAGE          = 30f;
    private const float SWING_PERIOD    = 4f;   // 秒（GDD §10.4）
    private const float SWING_AMPLITUDE = 60f;  // 度
    private const float KNOCKBACK_FORCE = 800f;

    [Header("振り子設定")]
    [SerializeField] private Vector3 _swingAxis    = Vector3.forward;
    [SerializeField] private float   _pivotOffsetY = 2f;   // 吊り下げ原点のローカル Y オフセット

    private float _time;

    private void Update()
    {
        _time += Time.deltaTime;
        float angle = Mathf.Sin((_time / SWING_PERIOD) * Mathf.PI * 2f) * SWING_AMPLITUDE;

        // ピボット点を中心に回転
        Vector3 pivot = transform.parent != null
            ? transform.parent.position + Vector3.up * _pivotOffsetY
            : transform.position        + Vector3.up * _pivotOffsetY;

        transform.RotateAround(pivot, _swingAxis, angle - GetCurrentAngle(pivot));
    }

    private float GetCurrentAngle(Vector3 pivot)
    {
        // 現在のオフセット方向から角度を逆算（ジンバルロック回避のため軽量近似）
        Vector3 dir = transform.position - pivot;
        return Mathf.Atan2(dir.x, -dir.y) * Mathf.Rad2Deg;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        var health = other.GetComponentInParent<PlayerHealthSystem>();
        health?.TakeDamage(DAMAGE);

        // 吹き飛ばし
        var rb = other.GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            Vector3 knockDir = (other.transform.position - transform.position).normalized
                               + Vector3.up * 0.5f;
            rb.AddForce(knockDir.normalized * KNOCKBACK_FORCE, ForceMode.Impulse);
        }

        // GDD §15.2 — trap_pendulum（丸太がプレイヤーに命中した瞬間のウッドヒット音）
        PPAudioManager.Instance?.PlaySE(SoundId.TrapPendulum, transform.position);

        Debug.Log("[PendulumLog] プレイヤーに命中！30 ダメージ + 吹き飛ばし");
    }
}

// ─────────────────────────────────────────────────────────────────
// 落下天井
// ─────────────────────────────────────────────────────────────────
/// <summary>
/// GDD §10.4 — 落下天井トラップ。
/// 特定の遺物を持ち上げると天井がゆっくり下降。プレイヤーが挟まれると即死。
/// </summary>
public class FallingCeiling : MonoBehaviour
{
    private const float DESCENT_SPEED   = 0.3f;   // m/s
    private const float KILL_Y_OFFSET   = 1.0f;   // 天井が地面から何mまで下降したら即死判定

    [Header("設定")]
    [SerializeField] private RelicBase _triggerRelic;  // この遺物を持ち上げるとトリガー

    private bool  _descending;
    private float _startY;
    private float _floorY;

    private void Start()
    {
        _startY = transform.position.y;
        // 真下に Raycast で床を検索
        if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 20f))
            _floorY = hit.point.y;
        else
            _floorY = transform.position.y - 10f;
    }

    private void Update()
    {
        if (!_descending) CheckTrigger();
        else              Descend();
    }

    private void CheckTrigger()
    {
        if (_triggerRelic == null) return;
        // 遺物が誰かに持ち運ばれているか（RelicCarrier.IsHeld）
        var carrier = _triggerRelic.GetComponent<RelicCarrier>();
        if (carrier != null && carrier.IsBeingCarried)
            _descending = true;
    }

    private void Descend()
    {
        transform.position += Vector3.down * (DESCENT_SPEED * Time.deltaTime);

        float killY = _floorY + KILL_Y_OFFSET;
        if (transform.position.y <= killY)
            KillPlayersUnderneath();
    }

    private void KillPlayersUnderneath()
    {
        var hits = Physics.OverlapBox(
            transform.position,
            transform.localScale * 0.5f,
            transform.rotation);

        foreach (var hit in hits)
        {
            var health = hit.GetComponentInParent<PlayerHealthSystem>();
            if (health != null && !health.IsDead)
            {
                health.TakeDamage(9999f);  // 即死
                Debug.Log("[FallingCeiling] プレイヤーが天井に挟まれた！即死");
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.4f);
        Gizmos.DrawCube(transform.position, transform.localScale);
    }
}

// ─────────────────────────────────────────────────────────────────
// 偽の床
// ─────────────────────────────────────────────────────────────────
/// <summary>
/// GDD §10.4 — 偽の床。
/// 見た目は普通の床だが Collider なし。踏んだプレイヤーは落下する。
/// ヒント: 正常な床より色が微妙に異なる。
/// </summary>
public class FakeFloor : MonoBehaviour
{
    [Header("見た目")]
    [SerializeField] private Color _tintColor = new(0.82f, 0.78f, 0.70f);  // 床と微妙に違う色

    private void Awake()
    {
        // Collider を全て無効化（落下させる）
        foreach (var col in GetComponents<Collider>())
            col.enabled = false;

        ApplyTint();
    }

    private void ApplyTint()
    {
        var rend = GetComponent<Renderer>();
        if (rend == null) return;

        var mat = new Material(rend.sharedMaterial != null
            ? rend.sharedMaterial
            : new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")));

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", _tintColor);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     _tintColor);
        rend.sharedMaterial = mat;
    }
}
