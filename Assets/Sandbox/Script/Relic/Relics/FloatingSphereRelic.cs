using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

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

    private Vector3 _currentDriftDir;
    private float   _driftTimer;
    private bool    _isFloating;   // 空中に浮いているか（地面より _floatHeight 以上上空＝「逃げ中」）

    /// <summary>
    /// 球体が地面から離れて自由浮遊している（＝プレイヤーが追いかける必要がある）状態か。
    /// HUD やチーム通知、感情表現システムが購読できるように公開する。
    /// </summary>
    public bool IsFloating => _isFloating;

    protected override void Awake()
    {
        _relicName        = "浮遊する球体";
        _baseValue        = 130;
        _maxHp            = 80f;
        _damageMultiplier = 0.8f;   // 壊れにくい
        _impactThreshold  = 2f;

        base.Awake();

        _rb.mass       = 0.1f;   // ほぼ無重量
        _rb.useGravity = false;  // 重力無効

        ChangeDriftDirection();
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

        // 地面チェック
        bool nearGround = Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, _floatHeight + 1f);
        float groundDist = nearGround ? hit.distance : float.MaxValue;

        // 浮遊力：地面より _floatHeight 上を維持
        if (groundDist < _floatHeight)
        {
            float forceMag = (_floatHeight - groundDist) * _floatForce;
            _rb.AddForce(Vector3.up * forceMag, ForceMode.Acceleration);
        }

        // 漂流
        _rb.AddForce(_currentDriftDir * _windDriftSpeed, ForceMode.Acceleration);

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
            PPAudioManager.Instance?.PlaySE(SoundId.RelicSphereHum, transform.position);
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
        PPAudioManager.Instance?.PlaySE(SoundId.RelicSphereHum, transform.position);
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
