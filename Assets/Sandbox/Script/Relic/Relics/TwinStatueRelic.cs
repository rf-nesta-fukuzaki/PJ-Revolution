using UnityEngine;

/// <summary>
/// GDD §6.2 — 遺物⑥「鎖付き双子像」
/// 物理軸：連結（2人同時運搬）。約3mの鎖で繋がった2体の像。
/// 鎖が岩に引っかかると詰む。
/// 難易度：★★☆  壊れやすさ：高
/// </summary>
public class TwinStatueRelic : RelicBase
{
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

    protected override void Awake()
    {
        _relicName        = "鎖付き双子像";
        _baseValue        = 110;
        _maxHp            = 70f;
        _damageMultiplier = 4f;
        _impactThreshold  = 1.5f;

        base.Awake();
    }

    private void FixedUpdate()
    {
        if (_partner == null || _isDestroyed) return;

        ApplyChainConstraint();
        UpdateChainVisual();
        CheckChainSnag();
    }

    private void ApplyChainConstraint()
    {
        Vector3 diff   = _partner.transform.position - transform.position;
        float   dist   = diff.magnitude;

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

        // ダメージ：鎖が強く引っ張られると破損リスク
        if (excess > _chainLength * 0.5f)
        {
            float stretchDamage = excess * 2f;
            ApplyDamage(stretchDamage);
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
        }

        if (_isChainSnagged)
        {
            _snagTimer -= Time.fixedDeltaTime;
            if (_snagTimer <= 0f)
                _isChainSnagged = false;
        }
    }

    public bool IsChainSnagged => _isChainSnagged;

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
    }
}
