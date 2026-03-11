using System.Collections;
using UnityEngine;

/// <summary>
/// 落石トラップ。天井付近に配置する不可視トリガー。
///
/// [動作フロー]
///   1. Update() で _triggerRadius 内にプレイヤーを検出。
///   2. 検出したら _triggered = true にして RockfallSequence() を起動。
///   3. _warningDuration 秒の警告後、_rockCount 個の岩を天井位置から Instantiate。
///   4. 各岩の FallingRock.OnCollisionEnter で着弾時に範囲ダメージを付与。
///   5. 5 秒後に岩を Destroy。
///
/// [ランダム散らし]
///   UnityEngine.Random は使用禁止。岩ごとに index ベースの決定論的オフセットを使用。
///   横方向力: Mathf.Sin / Mathf.Cos に index * 角度ステップを掛けた放射状配置。
/// </summary>
public class RockfallTrap : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("🪨 検出")]
    [Tooltip("プレイヤー検出半径（m）")]
    [Range(1f, 10f)]
    [SerializeField] private float _triggerRadius = 3f;

    [Header("🪨 落石")]
    [Tooltip("1 回あたりの落石数")]
    [Range(1, 10)]
    [SerializeField] private int _rockCount = 3;

    [Tooltip("落石前の警告時間（秒）")]
    [Range(0.1f, 3f)]
    [SerializeField] private float _warningDuration = 1.0f;

    [Tooltip("岩の Prefab（null の場合は Primitive Sphere を自動生成）")]
    [SerializeField] private GameObject _rockPrefab;

    [Header("🪨 ダメージ")]
    [Tooltip("着弾時の範囲ダメージ量")]
    [Range(1f, 100f)]
    [SerializeField] private float _damage = 20f;

    // ─────────────── 内部状態 ───────────────

    private bool _triggered;

    /// <summary>RockfallPlacer から動的生成時に岩 Prefab を設定する。</summary>
    public void SetRockPrefab(GameObject prefab) => _rockPrefab = prefab;

    // ─────────────── Unity Lifecycle ───────────────

    private void Update()
    {
        if (_triggered) return;

        int playerLayer = LayerMask.GetMask("Player");
        var hits = Physics.OverlapSphere(transform.position, _triggerRadius, playerLayer);
        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Player")) continue;

            _triggered = true;
            StartCoroutine(RockfallSequence());
            break;
        }
    }

    // ─────────────── 落石シーケンス ───────────────

    private IEnumerator RockfallSequence()
    {
        Debug.Log("[RockfallTrap] 落石警告！");

        yield return new WaitForSeconds(_warningDuration);

        // 岩ごとに index ベースの決定論的な放射状オフセットで散らす
        // 各岩を等角度に配置し、UnityEngine.Random を一切使わない
        float angleStep = _rockCount > 1 ? 360f / _rockCount : 0f;

        for (int i = 0; i < _rockCount; i++)
        {
            // 放射方向の決定論的オフセット（index × 等角度ステップ）
            float angle  = i * angleStep * Mathf.Deg2Rad;
            float spread = 0.6f; // 横方向散らし半径（m）
            var   offset = new Vector3(Mathf.Sin(angle) * spread, 0f, Mathf.Cos(angle) * spread);

            Vector3 spawnPos = transform.position + offset;

            // 岩を生成
            GameObject rock = _rockPrefab != null
                ? Instantiate(_rockPrefab, spawnPos, Quaternion.identity)
                : CreatePrimitiveRock(spawnPos);

            // Rigidbody がなければ追加
            var rb = rock.GetComponent<Rigidbody>();
            if (rb == null)
                rb = rock.AddComponent<Rigidbody>();
            rb.useGravity = true;

            // SphereCollider がなければ追加
            if (rock.GetComponent<SphereCollider>() == null)
                rock.AddComponent<SphereCollider>();

            // ダメージ処理コンポーネントを追加
            var fallingRock = rock.AddComponent<FallingRock>();
            fallingRock.Initialize(_damage);

            // 5 秒後に自動消滅
            Destroy(rock, 5f);
        }
    }

    /// <summary>Prefab が未設定のとき Primitive Sphere から岩を生成する。</summary>
    private GameObject CreatePrimitiveRock(Vector3 position)
    {
        var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        rock.transform.position   = position;
        rock.transform.localScale = Vector3.one * 0.4f;
        return rock;
    }

    // ─────────────── Gizmos ───────────────

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, _triggerRadius);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 岩に付与する着弾ダメージコンポーネント
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// 落石岩に AddComponent されるダメージ処理。
/// 最初の着地（OnCollisionEnter）で範囲内の SurvivalStats にダメージを与える。
/// </summary>
public class FallingRock : MonoBehaviour
{
    private const float HitRadius = 1.5f;

    private float _damage;
    private bool  _hasHit;

    /// <summary>RockfallTrap.RockfallSequence() から呼ぶ初期化メソッド。</summary>
    public void Initialize(float damage)
    {
        _damage = damage;
        _hasHit = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasHit) return;
        _hasHit = true;

        // 範囲内の SurvivalStats を持つオブジェクトにダメージ
        int playerLayer = LayerMask.GetMask("Player");
        var hits = Physics.OverlapSphere(transform.position, HitRadius, playerLayer);
        foreach (var hit in hits)
        {
            var stats = hit.GetComponent<SurvivalStats>();
            if (stats == null) continue;
            if (stats.IsDowned)  continue;

            stats.ApplyStatModification(StatType.Health, -_damage);
            Debug.Log($"[FallingRock] 落石ダメージ -{_damage} → {hit.name}");
        }
    }
}
