using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §6.2 — 遺物⑤「浮遊する球体」
/// 物理軸：浮く（反重力）。手を離すとふわっと浮いてどこかに行く。
/// 難易度：★★★  壊れやすさ：低
/// </summary>
public class FloatingSphereRelic : RelicBase
{
    [Header("浮遊設定")]
    [SerializeField] private float _floatHeight     = 2f;       // 地面からの目標浮遊高さ
    [SerializeField] private float _floatForce      = 15f;      // 上昇力
    [SerializeField] private float _windDriftSpeed  = 2f;       // 風に流される速さ
    [SerializeField] private float _driftChangeTime = 3f;       // 漂流方向変化間隔

    [Header("リーシュ（永久喪失防止）")]
    [SerializeField] private float _maxLeashDistance    = 22f;  // 基準点からの最大漂流距離
    [SerializeField] private float _maxFloatAboveGround = 4f;   // 接地点からの最大上昇高度

    // 接地判定で地形以外（プレイヤー/落石/トリガー）を「地面」と誤検出しないためのマスク。
    private static bool s_groundMaskInit;
    private static int  s_groundMask;
    private static int  GroundMask
    {
        get
        {
            if (!s_groundMaskInit)
            {
                s_groundMask = ~LayerMask.GetMask("Player", "Ghost", "RagdollBone");
                s_groundMaskInit = true;
            }
            return s_groundMask;
        }
    }

    private Vector3 _currentDriftDir;
    private float   _driftTimer;
    private bool    _isFloating;   // 空中に浮いているか（地面より _floatHeight 以上上空＝「逃げ中」）
    private Vector3 _leashAnchor;  // 復帰の基準点（接地設置位置 → 下方の地表へは追従するが上方=登坂はしない）
    private float   _homeY;        // 配置高度。浮遊の絶対上限 Y の基準（斜面を漂流で登って凍結帯へ侵入するのを防ぐ）

    /// <summary>
    /// 球体が地面から離れて自由浮遊している（＝プレイヤーが追いかける必要がある）状態か。
    /// HUD やチーム通知、感情表現システムが購読できるように公開する。
    /// </summary>
    public bool IsFloating => _isFloating;

    // 浮遊が本質の遺物。設置後も物理を有効に保つ（kinematic 静止にすると地面に張り付いて浮かない）。
    // 壊れにくい（mult 0.8 / threshold 2）うえリーシュで回収可能なため、静止凍結による保護は不要。
    protected override bool RestsKinematicUntilHandled => false;

    protected override void Awake()
    {
        _relicName        = "浮遊する球体";
        _baseValue        = 1000;   // GDD §9.4 #5
        _maxHp            = 200f;   // GDD §9.4 #5
        _damageMultiplier = 0.8f;   // 壊れにくい(Toughness 500)
        _impactThreshold  = 2f;

        base.Awake();

        _rb.mass       = 0.1f;   // ほぼ無重量
        _rb.useGravity = false;  // 重力無効

        _leashAnchor = transform.position;
        _homeY       = transform.position.y; // 設置前の暫定値。OnSettled で正規配置高度へ更新
        ChangeDriftDirection();
    }

    /// <summary>
    /// 接地設置の瞬間に配置高度を基準として捕捉する。これ以降、浮遊は「配置高度 + _maxFloatAboveGround」を
    /// 絶対上限とし、斜面を漂流で「登って」高所の凍結帯へ侵入し凍傷で破損する不具合を防ぐ（下方へは追従可）。
    /// </summary>
    protected override void OnSettled()
    {
        _homeY       = transform.position.y;
        _leashAnchor = transform.position;
    }

    private void FixedUpdate()
    {
        if (_isDestroyed) return;
        if (_isHeld)
        {
            // 保持中：重力なしで従順に追従（RigidbodyをKinematicに近い状態に）
            _rb.linearDamping = 20f;
            SetFloatingState(false);
            return;
        }

        _rb.linearDamping = 0.5f;

        // 地面チェック（地形以外を誤検出しないようマスク＋トリガー無視）
        bool nearGround = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit,
                                          _floatHeight + 1f, GroundMask, QueryTriggerInteraction.Ignore);
        float groundDist = nearGround ? hit.distance : float.MaxValue;

        // 接地点を基準点として更新するが、配置高度(_homeY)より上には基準を上げない（非対称クランプ）。
        // これで下り斜面へは追従しつつ、上り斜面を伝って凍結帯へ登っていくのを防ぐ。
        if (nearGround)
        {
            _leashAnchor = hit.point;
            if (_leashAnchor.y > _homeY) _leashAnchor.y = _homeY;
        }

        // 配置高度を基準にした絶対上限。これを超えている間は上昇を止め、下方へ引き戻す。
        bool aboveAltitudeCap = transform.position.y > _homeY + _maxFloatAboveGround;

        // 浮遊力：地面より _floatHeight 上を維持。ただし上限を超えている間は上昇させない
        // （斜面で地表が迫り上がっても、配置高度の上限より上へは浮かせない＝凍結帯侵入防止）。
        if (groundDist < _floatHeight && !aboveAltitudeCap)
        {
            float forceMag = (_floatHeight - groundDist) * _floatForce;
            _rb.AddForce(Vector3.up * forceMag, ForceMode.Acceleration);
        }

        // 漂流
        _rb.AddForce(_currentDriftDir * _windDriftSpeed, ForceMode.Acceleration);

        // リーシュ：基準点から水平に離れすぎたら引き戻し、永久喪失（回収不能）を防ぐ。
        Vector3 toAnchor = _leashAnchor - transform.position;
        if (toAnchor.magnitude > _maxLeashDistance)
            _rb.AddForce(toAnchor.normalized * _floatForce, ForceMode.Acceleration);
        // 配置高度の上限を超えたら下方へ引き戻す（登坂・上空逃げ・凍結帯侵入の防止）。
        if (aboveAltitudeCap)
            _rb.AddForce(Vector3.down * _floatForce, ForceMode.Acceleration);

        // 一定間隔で漂流方向を変える
        _driftTimer -= Time.fixedDeltaTime;
        if (_driftTimer <= 0f)
            ChangeDriftDirection();

        // 完全に地面から離れた＝逃走中。エッジトリガーで一度だけハム音を鳴らす。
        bool nowFloating = groundDist >= _floatHeight;
        SetFloatingState(nowFloating);
    }

    private void SetFloatingState(bool value)
    {
        if (_isFloating == value) return;
        _isFloating = value;

        if (value)
        {
            // GDD §15.2 — 逃走開始時のハム音（エッジトリガー）
            GameServices.Audio?.PlaySE(SoundId.RelicSphereHum, transform.position);
            Debug.Log("[FloatingSphere] 逃走開始！");
        }
    }

    private void ChangeDriftDirection()
    {
        _driftTimer = _driftChangeTime + Random.Range(-1f, 1f);
        _currentDriftDir = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-0.2f, 0.5f),
            Random.Range(-1f, 1f)).normalized;
    }

    public override void OnPickedUp(Transform holder)
    {
        base.OnPickedUp(holder);
        // 保持中は漂流を止める
        _rb.linearVelocity = Vector3.zero;
    }

    public override void OnPutDown()
    {
        base.OnPutDown();
        Debug.Log("[FloatingSphere] 「飛んでった！追え！」");
        ChangeDriftDirection();
        // 解放時にふわっと上昇
        _rb.AddForce(Vector3.up * 3f + _currentDriftDir * 2f, ForceMode.Impulse);
        // GDD §15.2 — relic_sphere_hum（解放時の浮遊ハム音）
        GameServices.Audio?.PlaySE(SoundId.RelicSphereHum, transform.position);
    }

    protected override Color GizmoColor => new Color(0.42f, 0.05f, 0.68f);

    protected override void BuildVisual()
    {
        var violet = new Color(0.42f, 0.05f, 0.68f);
        var purple = new Color(0.72f, 0.48f, 1.00f);
        var glow   = new Color(1.00f, 0.85f, 1.00f);

        // コア
        VizChild(PrimitiveType.Sphere, "core",
            Vector3.zero, Vector3.one,
            violet, metallic: 0.2f, smoothness: 0.95f);
        // リング x 3（Cylinder を寝かせて使用）
        VizChildRot(PrimitiveType.Cylinder, "ring1",
            Vector3.zero, Quaternion.Euler(90f, 0f, 0f),
            new Vector3(2.0f, 0.06f, 2.0f), purple, smoothness: 0.8f);
        VizChildRot(PrimitiveType.Cylinder, "ring2",
            Vector3.zero, Quaternion.Euler(90f, 60f, 0f),
            new Vector3(2.0f, 0.06f, 2.0f), purple, smoothness: 0.8f);
        VizChildRot(PrimitiveType.Cylinder, "ring3",
            Vector3.zero, Quaternion.Euler(90f, 120f, 0f),
            new Vector3(2.0f, 0.06f, 2.0f), purple, smoothness: 0.8f);
        // 衛星小球
        VizChild(PrimitiveType.Sphere, "orb1",
            new Vector3(0.75f, 0f, 0f), new Vector3(0.22f, 0.22f, 0.22f),
            glow, smoothness: 1.0f);
        VizChild(PrimitiveType.Sphere, "orb2",
            new Vector3(-0.52f, 0.55f, 0f), new Vector3(0.22f, 0.22f, 0.22f),
            glow, smoothness: 1.0f);
        VizChild(PrimitiveType.Sphere, "orb3",
            new Vector3(0f, -0.75f, 0f), new Vector3(0.22f, 0.22f, 0.22f),
            glow, smoothness: 1.0f);
    }

    protected override void OnBroken()
    {
        base.OnBroken();
        _rb.useGravity = true;
        Debug.Log("[FloatingSphere] 球体が地面に落下した。");
    }
}
