using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §6.2 — 遺物⑥「鎖付き双子像」
/// 物理軸：連結（2人同時運搬）。約3mの鎖で繋がった2体の像。
/// 鎖が岩に引っかかると詰む。
/// 難易度：★★☆  壊れやすさ：高
/// </summary>
public class TwinStatueRelic : RelicBase
{
    public override RelicSizeCategory SizeCategory => RelicSizeCategory.Small;

    [Header("双子像設定")]
    [SerializeField] private TwinStatueRelic _partner;       // もう片方の像
    [SerializeField] private float           _chainLength  = 3f;
    [SerializeField] private float           _chainStiffness = 200f;
    [SerializeField] private float           _chainDamping   = 10f;

    [Header("鎖可視化")]
    [SerializeField] private LineRenderer _chainLineRenderer;

    // 引っかかりチェック用
    private bool  _isChainSnagged;
    private float _snagTimer;
    private const float SNAG_TIMEOUT = 2f;
    // 鎖長のこの倍率を超える距離は「まだ正規位置に置かれていない（co-location 前）」とみなし、
    // 破壊的な張力・ダメージを与えずに待つ。地形整合で双子がゾーン別に離れて配置された直後の
    // 開幕自壊（鎖長3mに対し数十m離れて即時破損）を防ぐ。co-location 後は通常動作に戻る。
    private const float GROSS_STRETCH_FACTOR = 4f;

    protected override void Awake()
    {
        _relicName        = "鎖付き双子像";
        _baseValue        = 900;    // GDD §9.4 #6 — セットで。片方のみは報酬0
        _maxHp            = 90f;    // GDD §9.4 #6 — 共有HP
        _damageMultiplier = 4f;
        _impactThreshold  = 1.5f;

        base.Awake();
    }

    private void FixedUpdate()
    {
        // 相方が破壊済みの場合も停止する。破壊済みリジッドボディへ力を加え続ける不整合を防ぐ。
        if (_partner == null || _isDestroyed || _partner._isDestroyed) return;

        // 双方が設置（接地静止/拾い上げ）されるまで鎖物理を適用しない。設置前は NetworkRigidbody の
        // un-freeze で離れて落下/スタックし、鎖張力で開幕自壊しうる。co-location で正規距離へ寄せ
        // 両者が設置されてから鎖を有効化する。
        if (!IsPlaced || !_partner.IsPlaced) return;

        ApplyChainConstraint();
        UpdateChainVisual();
        CheckChainSnag();
    }

    private void ApplyChainConstraint()
    {
        Vector3 diff   = _partner.transform.position - transform.position;
        float   dist   = diff.magnitude;

        // 鎖長を大きく超える距離 = 未配置（co-location 前）とみなし、張力・ダメージを与えず待機。
        if (dist > _chainLength * GROSS_STRETCH_FACTOR) return;

        if (dist <= _chainLength) return;

        // 鎖が張っている → 弾性力で引き寄せる
        float   excess    = dist - _chainLength;
        Vector3 forceDir  = diff.normalized;
        Vector3 springForce = forceDir * (excess * _chainStiffness);

        // 相対速度による減衰
        Vector3 relVel    = _partner._rb.linearVelocity - _rb.linearVelocity;
        Vector3 dampForce = forceDir * Vector3.Dot(relVel, forceDir) * _chainDamping;

        Vector3 totalForce = springForce + dampForce;
        _rb.AddForce(totalForce,          ForceMode.Force);
        _partner._rb.AddForce(-totalForce, ForceMode.Force);

        // ダメージ：鎖が強く引っ張られると破損リスク。
        // ただし片方を手に持って運んでいる間は Carrier の MovePosition 追従で距離が開きやすく、
        // 開幕から両者が自壊してしまうため、運搬中は張力ダメージを与えない。
        // また excess を _chainLength でクランプし、片側保持時の暴走的な大ダメージを防ぐ。
        if (excess > _chainLength * 0.5f && !_isHeld && !_partner._isHeld)
        {
            float stretchDamage = Mathf.Min(excess, _chainLength) * 2f;
            ApplyDamage(stretchDamage);
            // ApplyDamage(this) で張力破損が発生すると自身もしくは相方が破壊され、_partner が
            // 破壊済み参照になりうる。相方へ与える前に再検証して NullReference を防ぐ。
            if (_partner != null && !_partner._isDestroyed)
                _partner.ApplyDamage(stretchDamage);
        }
    }

    private void UpdateChainVisual()
    {
        if (_chainLineRenderer == null) return;

        _chainLineRenderer.positionCount = 2;
        _chainLineRenderer.SetPosition(0, transform.position);
        _chainLineRenderer.SetPosition(1, _partner.transform.position);
    }

    private void CheckChainSnag()
    {
        float dist = Vector3.Distance(transform.position, _partner.transform.position);

        // 鎖が完全に伸びきっているのに動いていない → 引っかかっている
        bool stretched = dist >= _chainLength * 0.95f;
        bool both = _rb.linearVelocity.sqrMagnitude < 0.01f
                    && _partner._rb.linearVelocity.sqrMagnitude < 0.01f;

        if (stretched && both && !_isChainSnagged)
        {
            _isChainSnagged = true;
            _snagTimer = SNAG_TIMEOUT;
            Debug.Log("[TwinStatue] 「鎖引っかかった！」「3本足レースかよ」");
            // GDD §15.2 — relic_twins_chain（鎖引っかかりのエッジトリガー）
            GameServices.Audio?.PlaySE(SoundId.RelicTwinsChain, transform.position);
        }

        if (_isChainSnagged)
        {
            _snagTimer -= Time.fixedDeltaTime;
            if (_snagTimer <= 0f)
                _isChainSnagged = false;
        }
    }

    public bool IsChainSnagged => _isChainSnagged;

    /// <summary>ペアの相方（未設定時は null）。配置整合（co-location）等の外部処理が参照する。</summary>
    public TwinStatueRelic Partner => _partner;

    /// <summary>鎖の自然長（co-location で相方を鎖が張らない距離に寄せる際の基準）。</summary>
    public float ChainLength => _chainLength;

    /// <summary>
    /// SpawnManager.PairTwinStatues() がランタイムで呼び出すペアリング API。
    /// Inspector で _partner をアサインした場合はそちらが優先される。
    /// </summary>
    public void SetPartner(TwinStatueRelic partner)
    {
        if (_partner != null && _partner != partner)
            Debug.LogWarning("[TwinStatue] 既存パートナーを上書き: " +
                             $"{_partner.name} → {partner?.name}");
        _partner = partner;
    }

    protected override Color GizmoColor => new Color(0.78f, 0.72f, 0.60f);

    protected override void BuildVisual()
    {
        var stone = new Color(0.78f, 0.72f, 0.60f);
        var dark  = new Color(0.50f, 0.46f, 0.38f);

        // 台座
        VizChild(PrimitiveType.Cube, "base",
            new Vector3(0f, -0.72f, 0f), new Vector3(1.6f, 0.22f, 0.7f),
            dark, metallic: 0.05f, smoothness: 0.2f);
        // 左像
        VizChild(PrimitiveType.Cylinder, "body_L",
            new Vector3(-0.42f, -0.22f, 0f), new Vector3(0.35f, 0.70f, 0.35f),
            stone, metallic: 0.05f, smoothness: 0.2f);
        VizChild(PrimitiveType.Sphere, "head_L",
            new Vector3(-0.42f, 0.50f, 0f), new Vector3(0.42f, 0.44f, 0.42f),
            stone, metallic: 0.05f, smoothness: 0.25f);
        // 右像
        VizChild(PrimitiveType.Cylinder, "body_R",
            new Vector3(0.42f, -0.22f, 0f), new Vector3(0.35f, 0.70f, 0.35f),
            stone, metallic: 0.05f, smoothness: 0.2f);
        VizChild(PrimitiveType.Sphere, "head_R",
            new Vector3(0.42f, 0.50f, 0f), new Vector3(0.42f, 0.44f, 0.42f),
            stone, metallic: 0.05f, smoothness: 0.25f);
    }

    protected override void OnBroken()
    {
        base.OnBroken();
        Debug.Log("[TwinStatue] 双子像の片方が壊れた。鎖の意味がなくなった。");
        if (_chainLineRenderer != null)
            _chainLineRenderer.enabled = false;

        // 鎖を双方向に解除する。相方の FixedUpdate が破壊済みの自分へ力を加え続けないように
        // 相互参照を切り、相方側の鎖描画も止める。
        if (_partner != null)
        {
            var partner = _partner;
            _partner = null;
            partner._partner = null;
            if (partner._chainLineRenderer != null)
                partner._chainLineRenderer.enabled = false;
        }
    }
}
