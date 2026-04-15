using System.Collections;
using UnityEngine;

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
#pragma warning disable CS0414
    [SerializeField] private float _visibleRange = 100f;  // フレアの視認距離（将来のLoS判定用）
#pragma warning restore CS0414

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

    /// <summary>フレアを発射する。</summary>
    public bool TryFire(Transform firePoint)
    {
        if (_isBroken || _flaresLeft <= 0) return false;

        FireFlare(firePoint.position, firePoint.forward);
        _flaresLeft--;
        ConsumeDurability(100f / _maxFlares);

        Debug.Log($"[FlareGun] フレア発射！残り {_flaresLeft}/{_maxFlares} 発");
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
        flareComp.Init(_flareBurnTime);

        // コライダー不要（マーカー目的）
        var col = flare.GetComponent<SphereCollider>();
        if (col != null) col.isTrigger = true;
    }

    public override bool TryUse() => _flaresLeft > 0 && !_isBroken;

    protected override float GetUseDurabilityDrain() => 100f / _maxFlares;

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

/// <summary>フレア弾の挙動（着地後に燃え続ける）。</summary>
public class FlareBehavior : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private float _burnTime;
    private bool  _hasLanded;
    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;

    public void Init(float burnTime) => _burnTime = burnTime;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propertyBlock = new MaterialPropertyBlock();
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

        StartCoroutine(BurnRoutine());
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
