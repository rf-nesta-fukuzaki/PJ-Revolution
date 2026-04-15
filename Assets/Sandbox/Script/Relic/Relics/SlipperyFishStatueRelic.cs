using UnityEngine;

/// <summary>
/// GDD §6.2 — 遺物⑦「ぬるぬる聖なる魚像」
/// 物理軸：滑る。古代オイルでツルッと滑り落ちる。
/// 複数人で下から支えるのが正解。
/// 難易度：★★★  壊れやすさ：高
/// </summary>
public class SlipperyFishStatueRelic : RelicBase
{
    [Header("滑り設定")]
#pragma warning disable CS0414
    [SerializeField] private float _slipTorque       = 8f;    // 保持中に加わるトルク（将来のAddTorque実装用）
#pragma warning restore CS0414
    [SerializeField] private float _slipInterval     = 1.2f;  // 滑り発生間隔
    [SerializeField] private float _slipChance       = 0.4f;  // 毎インターバルの滑り確率
    [SerializeField] private float _dropImpulse      = 4f;    // 滑り落ちたときの初速

    private float _slipTimer;

    protected override void Awake()
    {
        _relicName        = "ぬるぬる聖なる魚像";
        _baseValue        = 140;
        _maxHp            = 60f;
        _damageMultiplier = 4.5f;
        _impactThreshold  = 1f;

        base.Awake();

        // 非常に低い摩擦（コンポーネント取得は base.Awake() 後に行う）
        var col = GetComponent<Collider>();
        if (col != null)
        {
            var mat = new PhysicsMaterial("FishOil")
            {
                staticFriction  = 0.02f,
                dynamicFriction = 0.01f,
                bounciness      = 0.1f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
            col.material = mat;
        }
    }

    private void Update()
    {
        if (_isDestroyed || !_isHeld) return;

        _slipTimer -= Time.deltaTime;
        if (_slipTimer > 0f) return;

        _slipTimer = _slipInterval;
        TrySlip();
    }

    private void TrySlip()
    {
        if (Random.value > _slipChance) return;

        // キャリアーに通知してドロップさせる
        var carrier = GetComponent<RelicCarrier>();
        if (carrier == null || !carrier.IsBeingCarried) return;

        Debug.Log("[FishStatue] 「ツルッ」「なんで魚に油塗るんだこの文明」");

        // ランダムな横方向に飛ぶ
        Vector3 slipDir = new Vector3(
            Random.Range(-1f, 1f),
            0.5f,
            Random.Range(-1f, 1f)).normalized;

        carrier.Drop(slipDir * _dropImpulse);
    }

    protected override Color GizmoColor => new Color(0.49f, 0.78f, 0.83f);

    protected override void BuildVisual()
    {
        var silver = new Color(0.60f, 0.78f, 0.84f);
        var blue   = new Color(0.36f, 0.60f, 0.70f);
        var dark   = new Color(0.06f, 0.06f, 0.06f);

        // 胴体
        VizChild(PrimitiveType.Sphere, "body",
            Vector3.zero, new Vector3(1.6f, 0.75f, 0.75f),
            silver, metallic: 0.4f, smoothness: 0.85f);
        // 尾ひれ
        VizChildRot(PrimitiveType.Cube, "tail",
            new Vector3(-0.92f, 0f, 0f),
            Quaternion.Euler(0f, 0f, 30f),
            new Vector3(0.55f, 0.72f, 0.18f),
            blue, metallic: 0.3f, smoothness: 0.7f);
        // 背びれ
        VizChildRot(PrimitiveType.Cube, "fin_top",
            new Vector3(0.12f, 0.44f, 0f),
            Quaternion.Euler(0f, 0f, -8f),
            new Vector3(0.42f, 0.48f, 0.10f),
            blue, metallic: 0.3f, smoothness: 0.7f);
        // 腹びれ
        VizChildRot(PrimitiveType.Cube, "fin_bot",
            new Vector3(0.12f, -0.44f, 0f),
            Quaternion.Euler(0f, 0f, 8f),
            new Vector3(0.32f, 0.36f, 0.10f),
            blue, metallic: 0.3f, smoothness: 0.7f);
        // 目
        VizChild(PrimitiveType.Sphere, "eye_L",
            new Vector3(0.62f, 0.12f, 0.32f), new Vector3(0.16f, 0.16f, 0.09f),
            dark, smoothness: 0.95f);
        VizChild(PrimitiveType.Sphere, "eye_R",
            new Vector3(0.62f, 0.12f, -0.32f), new Vector3(0.16f, 0.16f, 0.09f),
            dark, smoothness: 0.95f);
    }

    protected override void OnBroken()
    {
        base.OnBroken();
        Debug.Log("[FishStatue] 魚像が地面に叩きつけられた。油が飛び散る。");
    }
}
