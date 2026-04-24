using System.Collections;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §5.2 — アイテム「フレアガン」
/// ルートマーキング＆ヘリ信号。3発。
/// コスト 5pt / 重量 1 / スロット 1 / 耐久 100
/// </summary>
public class FlareGunItem : ItemBase
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static Material s_sharedFlareMaterial;

    [Header("フレア設定")]
    [SerializeField] private int   _maxFlares    = 3;
    [SerializeField] private float _flareSpeed   = 15f;
    [SerializeField] private float _flareBurnTime = 8f;   // 地面で燃え続ける時間（秒）
    [Tooltip("着弾後のフレアが視認される最大距離 (m)。この距離を超えた観測者、または LoS が遮られた観測者からは視認不可。")]
    [SerializeField] private float _visibleRange = 100f;

    private int _flaresLeft;

    public int FlaresLeft => _flaresLeft;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "フレアガン";
        _cost              = 5;
        _weight            = 1f;
        _slots             = 1;
        _maxDurability     = 100f;
        _currentDurability = _maxDurability;
        _impactDmgScale    = 0.5f;
        _flaresLeft        = _maxFlares;
    }

    // ── GDD §2.4: 上空発射判定 ────────────────────────────────
    /// <summary>仰角が閾値以上かどうか（ヘリ呼び出し判定用）。</summary>
    private static bool IsPointingUpward(Vector3 direction, float minElevationDeg = 60f)
    {
        // direction と Vector3.up の角度が (90° - minElevationDeg) 以下 = 仰角 minElevationDeg 以上
        float angleBetween = Vector3.Angle(direction, Vector3.up);
        return angleBetween <= (90f - minElevationDeg);
    }

    /// <summary>フレアを発射する。</summary>
    public bool TryFire(Transform firePoint)
    {
        if (_isBroken || _flaresLeft <= 0) return false;

        bool isSkyShot = IsPointingUpward(firePoint.forward);

        // GDD §15.2 — flare_fire
        PPAudioManager.Instance?.PlaySE(SoundId.FlareFire, firePoint.position);

        FireFlare(firePoint.position, firePoint.forward);
        _flaresLeft--;
        ConsumeDurability(100f / _maxFlares);

        if (isSkyShot)
        {
            // 上空発射 → ヘリコプター呼び出し（GameServices 経由で IHelicopterService を使用）
            GameServices.Helicopter?.CallHelicopter(firePoint.position);
            Debug.Log($"[FlareGun] 上空発射！ヘリを呼び出しました。残り {_flaresLeft}/{_maxFlares} 発");
        }
        else
        {
            Debug.Log($"[FlareGun] 水平発射（マーキング）。残り {_flaresLeft}/{_maxFlares} 発");
        }

        return true;
    }

    private void FireFlare(Vector3 origin, Vector3 direction)
    {
        // フレアプロジェクタイル生成（プリミティブ代替）
        var flare = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flare.name = "Flare";
        flare.transform.position   = origin;
        flare.transform.localScale = Vector3.one * 0.1f;

        // 発光色（オレンジ）
        var rend = flare.GetComponent<Renderer>();
        if (rend != null)
        {
            var material = GetSharedFlareMaterial();
            if (material != null)
            {
                rend.sharedMaterial = material;
                var block = new MaterialPropertyBlock();
                var baseColor = new Color(1f, 0.4f, 0f);
                block.SetColor(BaseColorId, baseColor);
                block.SetColor(ColorId, baseColor);
                rend.SetPropertyBlock(block);
            }
        }

        // 物理
        var rb = flare.AddComponent<Rigidbody>();
        rb.linearVelocity = direction * _flareSpeed;

        // 衝突後の処理
        var flareComp = flare.AddComponent<FlareBehavior>();
        flareComp.Init(_flareBurnTime, _visibleRange);

        // コライダー不要（マーカー目的）
        var col = flare.GetComponent<SphereCollider>();
        if (col != null) col.isTrigger = true;
    }

    public override bool TryUse() => _flaresLeft > 0 && !_isBroken;

    protected override void OnItemBroken()
    {
        Debug.Log("[FlareGun] フレアガンを使い切りました");
        base.OnItemBroken();
    }

    private static Material GetSharedFlareMaterial()
    {
        if (s_sharedFlareMaterial != null) return s_sharedFlareMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        s_sharedFlareMaterial = new Material(shader)
        {
            name = "SharedFlareMaterial"
        };
        if (s_sharedFlareMaterial.HasProperty("_Metallic"))
            s_sharedFlareMaterial.SetFloat("_Metallic", 0f);
        return s_sharedFlareMaterial;
    }
}

/// <summary>
/// フレア弾の挙動（着地後に燃え続ける）。
/// 着地後は s_burningFlares に登録され、<see cref="IsVisibleFrom"/> で LoS+距離判定が可能。
/// HUD コンパスやチームメイト向け通知で `GetVisibleFlaresFrom(playerPos)` を呼び出すことを想定。
/// </summary>
public class FlareBehavior : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    // GDD §5.2 — 燃焼中フレアのグローバル登録簿（HUD / ミニマップ用）
    private static readonly System.Collections.Generic.List<FlareBehavior> s_burningFlares = new();
    public static System.Collections.Generic.IReadOnlyList<FlareBehavior> BurningFlares => s_burningFlares;

    private float _burnTime;
    private float _visibleRange = 100f;
    private bool  _hasLanded;
    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;

    public float VisibleRange => _visibleRange;
    public bool  HasLanded    => _hasLanded;

    public void Init(float burnTime, float visibleRange = 100f)
    {
        _burnTime = burnTime;
        _visibleRange = visibleRange;
    }

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>
    /// 指定位置からこのフレアが視認可能か判定。距離 ≤ _visibleRange かつ
    /// 間に遮蔽物が無いこと（Default + Environment 等の既定レイヤー）を条件とする。
    /// </summary>
    public bool IsVisibleFrom(Vector3 observer, int obstacleMask = ~0)
    {
        if (!_hasLanded) return false;
        Vector3 toFlare = transform.position - observer;
        float distSqr = toFlare.sqrMagnitude;
        if (distSqr > _visibleRange * _visibleRange) return false;

        // 視線レイキャスト。自分のコライダーは trigger なので遮らない想定。
        float dist = Mathf.Sqrt(distSqr);
        if (dist < 0.01f) return true;
        Vector3 dir = toFlare / dist;
        return !Physics.Raycast(observer, dir, dist - 0.1f, obstacleMask, QueryTriggerInteraction.Ignore);
    }

    /// <summary>観測者から視認可能な全フレアを返す（HUD コンパス等が利用）。</summary>
    public static System.Collections.Generic.IEnumerable<FlareBehavior> GetVisibleFlaresFrom(
        Vector3 observer, int obstacleMask = ~0)
    {
        foreach (var f in s_burningFlares)
        {
            if (f == null) continue;
            if (f.IsVisibleFrom(observer, obstacleMask)) yield return f;
        }
    }

    private void OnCollisionEnter(Collision col)
    {
        if (_hasLanded) return;
        _hasLanded = true;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity  = Vector3.zero;
            rb.isKinematic = true;
        }

        s_burningFlares.Add(this);
        StartCoroutine(BurnRoutine());
    }

    private void OnDestroy()
    {
        s_burningFlares.Remove(this);
    }

    private IEnumerator BurnRoutine()
    {
        float elapsed = 0f;

        while (elapsed < _burnTime)
        {
            elapsed += Time.deltaTime;

            // 点滅エフェクト
            if (_renderer != null)
            {
                float blink = Mathf.Sin(elapsed * 8f) * 0.3f + 0.7f;
                var color = new Color(1f, 0.4f * blink, 0f);
                _renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, color);
                _propertyBlock.SetColor(ColorId, color);
                _renderer.SetPropertyBlock(_propertyBlock);
            }

            yield return null;
        }

        Debug.Log("[Flare] フレア消灯");
        Destroy(gameObject);
    }
}
