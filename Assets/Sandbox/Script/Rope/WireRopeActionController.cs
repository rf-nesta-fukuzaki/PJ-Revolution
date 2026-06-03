using System.Collections;
using UnityEngine;
using PeakPlunder.Audio;
using Sandbox.World.Integration;

/// <summary>
/// R キーによるカウボーイ式ワイヤーロープアクション。
/// 溜め: キャラ頭上（水平面から約80°）でラッソをクルクル回す。
/// 回収: 張力で引き寄せたあとロープ離脱でオーバーシュート。終了時にめり込み解消。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class WireRopeActionController : MonoBehaviour
{
    public enum WireRopePhase
    {
        Ready,
        Charging,
        Throwing,
        Attached,
        Retrieving,
    }

    private const float GroundNormalThreshold = WireRopePhysics.GroundNormalThreshold;
    private const float GroundClearance = 0.12f;
    private const float GroundPenetrationCorrectThreshold = 0.2f;
    // 地表より深く潜った（足元サンプルが届かない）ときの救済用：高所から下キャストして直上の面を探す高さ。
    private const float DeepRecoverySampleUp = 200f;
    // この深さ[m]以上潜ったら速度に関係なく即座に地表へスナップ（地面すり抜け落下の防止）。
    private const float HardSnapPenetrationDepth = 0.6f;
    // これを超える速度は物理暴走（NaN/発散）のサイン。通常運用は < 52m/s なので十分な余裕。
    private const float MaxSaneSpeed = 300f;

    // ── 設定 (ScriptableObject) ──────────────────────────────
    // 全チューニング値は WireRopeActionConfigSO へ外部化（Co-op で 1 asset を共有）。
    [Header("設定")]
    [Tooltip("ロープの全チューニング値。未割り当て時は Resources の WireRopeActionConfig を共有ロード。")]
    [SerializeField] private WireRopeActionConfigSO _config;

    [Header("参照 (シーン配線)")]
    [SerializeField] private LayerMask _hookableLayers;
    [SerializeField] private Transform _aimTransform;
    [SerializeField] private Transform _handOrigin;

    // 設定の遅延解決: 実行時 AddComponent や EditMode テスト（Awake 非呼出）でも null にならない。
    private WireRopeActionConfigSO _resolvedConfig;
    private WireRopeActionConfigSO Cfg
    {
        get
        {
            if (_resolvedConfig == null)
                _resolvedConfig = _config != null ? _config : WireRopeActionConfigSO.LoadDefault();
            return _resolvedConfig;
        }
    }

    // ── 設定値 forwarding（呼び出し側は従来のフィールド名 _xxx のまま）──
    private float _minThrowRange => Cfg.MinThrowRange;
    private float _maxThrowRange => Cfg.MaxThrowRange;
    private float _throwSpeed => Cfg.ThrowSpeed;
    private float _gaugeOscillationSpeed => Cfg.GaugeOscillationSpeed;
    private float _spinVisualSpeed => Cfg.SpinVisualSpeed;
    private float _lassoElevationDeg => Cfg.LassoElevationDeg;
    private float _lassoCenterDistance => Cfg.LassoCenterDistance;
    private float _lassoChestHeight => Cfg.LassoChestHeight;
    private float _lassoRadius => Cfg.LassoRadius;
    private float _lassoHandWidthMul => Cfg.LassoHandWidthMul;
    private float _ropeStartWidth => Cfg.RopeStartWidth;
    private float _ropeEndWidth => Cfg.RopeEndWidth;
    private float _ropeSag => Cfg.RopeSag;
    private Color _ropeColor => Cfg.RopeColor;
    private float _pullTensionAccel => Cfg.PullTensionAccel;
    private float _pullMinSpeed => Cfg.PullMinSpeed;
    private float _pullMaxSpeed => Cfg.PullMaxSpeed;
    private float _pullSnapImpulse => Cfg.PullSnapImpulse;
    private float _maxPullElevationDeg => Cfg.MaxPullElevationDeg;
    private float _maxPullElevationDegWallUp => Cfg.MaxPullElevationDegWallUp;
    private float _maxUpwardSpeedFromTension => Cfg.MaxUpwardSpeedFromTension;
    private float _targetPullAccel => Cfg.TargetPullAccel;
    private float _targetPullMaxSpeed => Cfg.TargetPullMaxSpeed;
    private float _targetPullMinSpeed => Cfg.TargetPullMinSpeed;
    private float _targetPullArrivalDistance => Cfg.TargetPullArrivalDistance;
    private float _ropeElasticGain => Cfg.RopeElasticGain;
    private float _ropeElasticPower => Cfg.RopeElasticPower;
    private float _swingCentripetalGain => Cfg.SwingCentripetalGain;
    private float _chargePowerSpread => Cfg.ChargePowerSpread;
    private float _groundSlideAssistAccel => Cfg.GroundSlideAssistAccel;
    private float _groundImpactBleed => Cfg.GroundImpactBleed;
    private float _groundSlideLateralResist => Cfg.GroundSlideLateralResist;
    private float _groundSlideMaxAirGap => Cfg.GroundSlideMaxAirGap;
    private float _pullSnapTensionBoost => Cfg.PullSnapTensionBoost;
    private float _pullPerpendicularDamp => Cfg.PullPerpendicularDamp;
    private float _retrieveSteerAccel => Cfg.RetrieveSteerAccel;
    private float _overshootMomentumScale => Cfg.OvershootMomentumScale;
    private float _anchorArrivalDistance => Cfg.AnchorArrivalDistance;
    private float _releaseInertiaScale => Cfg.ReleaseInertiaScale;
    private float _releaseInertiaMinSpeed => Cfg.ReleaseInertiaMinSpeed;
    private float _releaseInertiaMaxSpeed => Cfg.ReleaseInertiaMaxSpeed;
    private float _releasePerpendicularRetain => Cfg.ReleasePerpendicularRetain;
    private float _maxOvershootDistance => Cfg.MaxOvershootDistance;
    private float _overshootEndSpeed => Cfg.OvershootEndSpeed;
    private float _retrieveStopDistance => Cfg.RetrieveStopDistance;
    private float _maxRetrieveSeconds => Cfg.MaxRetrieveSeconds;
    private float _standOffFromSurface => Cfg.StandOffFromSurface;
    private float _groundSnapUp => Cfg.GroundSnapUp;
    private int _depenetrateIterations => Cfg.DepenetrateIterations;
    private float _gaugeLaunchBoost => Cfg.GaugeLaunchBoost;
    private float _retrieveTensionRampSeconds => Cfg.RetrieveTensionRampSeconds;
    private float _releaseSoftDistance => Cfg.ReleaseSoftDistance;
    private float _retrieveStartVelocityBlend => Cfg.RetrieveStartVelocityBlend;
    private float _releaseVelocityBlend => Cfg.ReleaseVelocityBlend;
    private float _pullAxisTurnSpeed => Cfg.PullAxisTurnSpeed;
    private float _visualBlendSpeed => Cfg.VisualBlendSpeed;
    private float _maxSpeedChangePerSecond => Cfg.MaxSpeedChangePerSecond;
    private float _pullStallReleaseSeconds => Cfg.PullStallReleaseSeconds;
    private float _overshootMaxSeconds => Cfg.OvershootMaxSeconds;
    private float _tensionFalloffFloor => Cfg.TensionFalloffFloor;
    private float _pullSpeedShort => Cfg.PullSpeedShort;
    private float _pullFloorFraction => Cfg.PullFloorFraction;
    private float _pullSpeedChargeSpread => Cfg.PullSpeedChargeSpread;
    private float _overshootDistanceFactor => Cfg.OvershootDistanceFactor;
    private float _overshootMinDistance => Cfg.OvershootMinDistance;
    private float _groundClimbLiftThreshold => Cfg.GroundClimbLiftThreshold;
    private float _impactSlingshotSpeed => Cfg.ImpactSlingshotSpeed;
    private float _impactRestitution => Cfg.ImpactRestitution;
    private float _impactSlingshotFloorPopUp => Cfg.ImpactSlingshotFloorPopUp;
    private float _impactSlingshotCarryFactor => Cfg.ImpactSlingshotCarryFactor;
    private float _impactSlingshotMaxSpeed => Cfg.ImpactSlingshotMaxSpeed;
    private float _impactSlingshotBoostSeconds => Cfg.ImpactSlingshotBoostSeconds;
    private float _impactSlingshotCooldown => Cfg.ImpactSlingshotCooldown;
    private float _impactSlingshotMinIntoSpeed => Cfg.ImpactSlingshotMinIntoSpeed;
    private float _impactSlingshotHardImpactSpeed => Cfg.ImpactSlingshotHardImpactSpeed;
    private float _obstacleNormalMaxY => Cfg.ObstacleNormalMaxY;
    private float _floorSlingshotMinIntoSpeed => Cfg.FloorSlingshotMinIntoSpeed;
    private float _floorSlingshotMinHorizSpeed => Cfg.FloorSlingshotMinHorizSpeed;
    private float _softGroundPenetrationDepth => Cfg.SoftGroundPenetrationDepth;
    private float _throwEasePower => Cfg.ThrowEasePower;
    private float _tensionSoundInterval => Cfg.TensionSoundInterval;
    private float _traumaRetrieveStart => Cfg.TraumaRetrieveStart;
    private float _traumaRopeHit => Cfg.TraumaRopeHit;
    private float _traumaRopeRelease => Cfg.TraumaRopeRelease;
    private float _traumaImpactSlingshot => Cfg.TraumaImpactSlingshot;

    private Rigidbody _rb;
    private ExplorerController _explorer;
    private int _inputSlot;
    private RopeRenderer _renderer;

    private bool _savedUseGravity;
    private bool _savedIsKinematic;
    private bool _savedMotorState;
    private WireRopePhase _phase = WireRopePhase.Ready;
    private float _forceGauge;
    private float _spinAngle;
    private float _lastChargeGauge;
    private Vector3 _anchorPoint;
    private Vector3 _anchorNormal = Vector3.up;
    private bool _anchorIsGround;
    private bool _pullTargetToPlayer;
    private Rigidbody _attachedTargetBody;
    private Transform _attachedTargetTransform;
    private Behaviour _suppressedTargetController;
    private bool _targetSavedKinematic;
    private bool _targetSavedUseGravity;
    private float _chargedThrowRange;
    private float _retrieveTimer;
    private bool _ropeReleased;
    private Vector3 _retrieveStopPoint;
    private Vector3 _retrievePullAxis;
    private float _retrieveTensionBlend;
    private float _pullStallTimer;
    private float _overshootTimer;
    private float _impactBoostTimer;
    private float _lastImpactSlingshotTime = -999f;
    private float _visualSagBlend;
    private float _visualWidthStart;
    private float _attachedRopeSwayPhase;
    private float _tensionSoundTimer;
    private float _retrieveChargeFactor = 1f;
    private float _retrieveStartDistance;
    private float _retrieveEngage01;
    private float _targetPullSpeed;
    private float _effectiveOvershootDistance;
    private float _effectiveOvershootMaxSeconds;
    private float _chargeStartTime;
    private bool _retrieveUsesRaisedGroundPull;
    private float _peakTensionSpeed;
    private Vector3 _overshootOrigin;
    private Vector3 _inertiaSlideDirection = Vector3.forward;
    private Vector3 _retrieveRunDirectionXZ = Vector3.forward;
    private bool _retrieveSlideFrictionActive;
    private float _savedCapsuleDynamicFriction;
    private float _savedCapsuleStaticFriction;
    private Coroutine _throwRoutine;
    private SandboxCameraShake _cameraShake;
    private CapsuleCollider _bodyCapsule;
    private int _collisionMask;
    // RaycastAll の毎回ヒープ確保を避ける共有バッファ（回収中は FixedUpdate ごとに複数回引くため）。
    private readonly RaycastHit[] _rayHits = new RaycastHit[16];

    public WireRopePhase Phase => _phase;
    public float ForceGauge => _forceGauge;
    public bool IsForceGaugeVisible => _phase == WireRopePhase.Charging;
    public bool IsRopeVisible =>
        _phase is WireRopePhase.Throwing or WireRopePhase.Attached
        || (_phase == WireRopePhase.Retrieving && !_ropeReleased);
    /// <summary>張力フェーズ中のみ Explorer の接地 MovePosition を抑止（オーバーシュート・停止時は操作可能）。</summary>
    public bool SuppressExplorerLocomotion =>
        !_pullTargetToPlayer
        && _phase == WireRopePhase.Retrieving
        && !_ropeReleased
        && _retrieveTensionBlend >= 0.12f
        && _pullStallTimer < _pullStallReleaseSeconds * 0.85f;

    private void Awake()
    {
        Cfg.Validate();   // 起動時 fail-fast: 設定値の不正な組み合わせをここで検出する（DbC）

        _rb = GetComponent<Rigidbody>();
        _explorer = GetComponent<ExplorerController>();
        _bodyCapsule = GetComponent<CapsuleCollider>();
        _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);

        ResolveAimReferences();
        if (_handOrigin == null)
            _handOrigin = _aimTransform;

        _renderer = GetComponent<RopeRenderer>();
        if (_renderer == null)
            _renderer = gameObject.AddComponent<RopeRenderer>();
        _renderer.Configure(_ropeStartWidth, _ropeEndWidth, _ropeColor);

        ResolveHookMask();
        ResolveCollisionMask();
    }

    private bool CanDriveRigidbody => _rb != null && !_rb.isKinematic;

    private Vector3 BodyLinearVelocity => CanDriveRigidbody ? _rb.linearVelocity : Vector3.zero;

    private void SaveAndEnableRigidbodyMotor()
    {
        if (_rb == null || _savedMotorState)
            return;

        _savedUseGravity = _rb.useGravity;
        _savedIsKinematic = _rb.isKinematic;
        _savedMotorState = true;

        if (_rb.isKinematic)
            _rb.isKinematic = false;

        if (!_rb.useGravity)
            _rb.useGravity = true;
    }

    private void RestoreRigidbodyMotorState()
    {
        if (_rb == null || !_savedMotorState)
            return;

        _rb.useGravity = _savedUseGravity;
        _rb.isKinematic = _savedIsKinematic;
        _savedMotorState = false;
    }

    // ── 物理不変条件（DbC: 「静かな失敗」を開発ビルドで顕在化）────────
    // UNITY_ASSERTIONS 無効（リリース）時は呼び出しごと除去される。
    [System.Diagnostics.Conditional("UNITY_ASSERTIONS")]
    private void AssertVelocitySane(Vector3 v, string where)
    {
        Contract.Invariant(WireRopePhysics.IsFinite(v), $"WireRope: 速度が NaN/Inf @ {where} (phase={_phase})");
        Contract.Invariant(v.sqrMagnitude <= MaxSaneSpeed * MaxSaneSpeed,
            $"WireRope: 速度暴走 {v.magnitude:F1}m/s @ {where} (phase={_phase})");
    }

    [System.Diagnostics.Conditional("UNITY_ASSERTIONS")]
    private void AssertFinite(Vector3 v, string where)
        => Contract.Invariant(WireRopePhysics.IsFinite(v), $"WireRope: 値が NaN/Inf @ {where} (phase={_phase})");

    private void SetBodyLinearVelocity(Vector3 velocity)
    {
        if (!CanDriveRigidbody)
            return;
        _rb.linearVelocity = velocity;
        AssertVelocitySane(_rb.linearVelocity, nameof(SetBodyLinearVelocity));
    }

    private void AddBodyLinearVelocity(Vector3 delta)
    {
        if (!CanDriveRigidbody)
            return;
        _rb.linearVelocity += delta;
        AssertVelocitySane(_rb.linearVelocity, nameof(AddBodyLinearVelocity));
    }

    private void SetBodyAngularVelocity(Vector3 velocity)
    {
        if (!CanDriveRigidbody)
            return;
        _rb.angularVelocity = velocity;
        AssertFinite(_rb.angularVelocity, nameof(SetBodyAngularVelocity));
    }

    private void AddBodyForce(Vector3 force, ForceMode mode)
    {
        if (!CanDriveRigidbody)
            return;
        AssertFinite(force, nameof(AddBodyForce));
        _rb.AddForce(force, mode);
    }

    private float RetrieveTensionEased =>
        1f - (1f - _retrieveTensionBlend) * (1f - _retrieveTensionBlend);

    private void EnsureCameraShake()
    {
        if (_cameraShake != null)
            return;

        var cam = AimTransform.GetComponentInChildren<Camera>();
        if (cam == null)
            return;

        _cameraShake = cam.GetComponent<SandboxCameraShake>();
        if (_cameraShake == null)
            _cameraShake = cam.gameObject.AddComponent<SandboxCameraShake>();
    }

    private void AddCameraTrauma(float amount)
    {
        if (amount <= 0f)
            return;

        EnsureCameraShake();
        _cameraShake?.AddTrauma(amount);
    }

    private static float EaseOut(float t, float power) =>
        1f - Mathf.Pow(1f - Mathf.Clamp01(t), power);

    private void ResolveCollisionMask()
    {
        _collisionMask = Physics.AllLayers;
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            _collisionMask &= ~(1 << playerLayer);
    }

    private void ResolveAimReferences()
    {
        if (_aimTransform != null) return;
        var childCam = GetComponentInChildren<Camera>();
        _aimTransform = childCam != null ? childCam.transform : transform;
    }

    private Transform AimTransform => _aimTransform != null ? _aimTransform : transform;

    private Vector3 GetFlatForward()
    {
        Vector3 f = transform.forward;
        f.y = 0f;
        if (f.sqrMagnitude < 0.01f) f = Vector3.forward;
        return f.normalized;
    }

    private void Update()
    {
        // 入力スロットは「確定するまで(-1 の間)」だけ再解決する。Awake 時点は roster 未構成で
        // -1 になり得るが、一度有効値を得たら固定し、毎フレームの GetComponent を避ける。
        if (_inputSlot < 0)
            _inputSlot = LocalCoopPartyMember.ResolveInputSlot(this);
        if (_inputSlot < 0) return;

        switch (_phase)
        {
            case WireRopePhase.Ready:
                if (InputStateReader.IsWireRopeHeld(_inputSlot))
                    BeginCharging();
                break;

            case WireRopePhase.Charging:
                // 溜め開始からの経過で振動させる（毎回 0 から立ち上がる）。
                _forceGauge = Mathf.PingPong((Time.time - _chargeStartTime) * _gaugeOscillationSpeed, 100f);
                _spinAngle += _spinVisualSpeed * Time.deltaTime;
                if (InputStateReader.WireRopeReleasedThisFrame(_inputSlot))
                {
                    _lastChargeGauge = _forceGauge;
                    ReleaseThrow();
                }
                break;

            case WireRopePhase.Attached:
                if (InputStateReader.WireRopePressedThisFrame(_inputSlot))
                    BeginRetrieve();
                break;

            case WireRopePhase.Retrieving:
                if (_ropeReleased && HasMoveInput())
                    FinishRetrieveWithMomentum();
                break;
        }
    }

    private void LateUpdate()
    {
        switch (_phase)
        {
            case WireRopePhase.Charging:
                UpdateCowboyLassoVisual();
                break;
            case WireRopePhase.Attached:
            case WireRopePhase.Retrieving:
                UpdateRetrieveRopeVisual();
                break;
        }
    }

    private void UpdateRetrieveRopeVisual()
    {
        // 遺物・キャラに引っかかっている場合はアンカーを対象に追従させ、ロープが離れて見えないようにする。
        if (_attachedTargetTransform != null)
            _anchorPoint = _attachedTargetTransform.position;

        float tensionTight = _phase == WireRopePhase.Retrieving
            ? (_ropeReleased ? 0.35f : 0.4f + RetrieveTensionEased * 0.55f)
            : 0.15f;

        if (_phase == WireRopePhase.Attached)
        {
            _attachedRopeSwayPhase += Time.deltaTime * 2.4f;
            tensionTight = 0.22f + Mathf.Sin(_attachedRopeSwayPhase) * 0.06f;
        }

        float targetSag = _ropeSag * (1f - tensionTight * 0.88f);
        if (_ropeReleased)
            targetSag *= 0.42f;

        float speed = BodyLinearVelocity.magnitude;
        float stretch = _phase == WireRopePhase.Retrieving ? GetRopeStretch01() : 0f;
        float widthMul = 1f + Mathf.Clamp01(speed / 26f) * 0.22f + stretch * 0.14f;
        if (_phase == WireRopePhase.Retrieving && !_ropeReleased)
            widthMul += RetrieveTensionEased * 0.12f;

        _visualSagBlend = Mathf.Lerp(_visualSagBlend, targetSag, Time.deltaTime * _visualBlendSpeed);
        _visualWidthStart = Mathf.Lerp(_visualWidthStart, _ropeStartWidth * widthMul, Time.deltaTime * _visualBlendSpeed);

        _renderer.DrawCurve(GetHandOrigin(), _anchorPoint, _visualSagBlend, _visualWidthStart);
        _renderer.SyncHook(_anchorPoint, Time.deltaTime);

        // 巻き取り中は撚り柄を流して「ケーブルが手繰り寄せられている」感を出す（離脱後・待機中は停止）。
        bool winching = _phase == WireRopePhase.Retrieving && !_ropeReleased;
        _renderer.SetReelScroll(winching ? Mathf.Clamp(speed * 0.14f, 0.8f, 4.5f) : 0f);
    }

    private void FixedUpdate()
    {
        if (_phase != WireRopePhase.Retrieving)
            return;

        if (_pullTargetToPlayer)
            UpdateTargetPullPhysics();
        else
            UpdateRetrievePhysics();
    }

    // ── フェーズ遷移（宣言的 FSM）────────────────────────────
    // 不正な遷移を構造的に禁止する。新しい遷移が必要なときは下表に1行追加するだけ。
    // 設計は PlayerStateMachine.IsValidTransition と同型。
    public static bool IsValidWireRopeTransition(WireRopePhase from, WireRopePhase to) => (from, to) switch
    {
        (WireRopePhase.Ready,    WireRopePhase.Charging)   => true,
        (WireRopePhase.Charging, WireRopePhase.Throwing)   => true,
        (WireRopePhase.Throwing, WireRopePhase.Attached)   => true,
        (WireRopePhase.Attached, WireRopePhase.Retrieving) => true,
        // 任意の状態 → Ready は完了 / 中断 / 空振りとして常に合法（ResetToReady の普遍リセット）。
        (_,                      WireRopePhase.Ready)      => true,
        _ => false,
    };

    /// <summary>
    /// フェーズ遷移を要求する。無効な遷移はエラーログ後に無視される（フェイルファスト）。
    /// 全ての _phase 書き込みはこのメソッド経由で行う。
    /// </summary>
    private void SetPhase(WireRopePhase next)
    {
        if (_phase == next) return;
        if (!IsValidWireRopeTransition(_phase, next))
        {
            Contract.TryRequires(false,
                $"WireRope: 不正なフェーズ遷移 {_phase} → {next} (object: {name})");
            return;
        }
        _phase = next;
    }

    // ── 頭上カウボーイラッソ（水平輪＋手元テザー） ───────────
    private void BeginCharging()
    {
        SetPhase(WireRopePhase.Charging);
        _chargeStartTime = Time.time;
        _forceGauge = 0f;
        _spinAngle = 0f;
        _visualSagBlend = 0f;
        _visualWidthStart = _ropeStartWidth * _lassoHandWidthMul;
        _renderer.SetVisible(true);
        UpdateCowboyLassoVisual();
    }

    private void GetCowboyLassoFrame(out Vector3 center, out Vector3 axisRight, out Vector3 axisForward)
    {
        Vector3 forward = GetFlatForward();
        float elev = _lassoElevationDeg * Mathf.Deg2Rad;
        Vector3 pivot = transform.position + Vector3.up * _lassoChestHeight;
        Vector3 radial = Vector3.up * Mathf.Sin(elev) + forward * Mathf.Cos(elev);
        center = pivot + radial.normalized * _lassoCenterDistance;

        axisRight = transform.right;
        axisForward = forward;
    }

    private void UpdateCowboyLassoVisual()
    {
        GetCowboyLassoFrame(out Vector3 center, out Vector3 axisRight, out Vector3 axisForward);
        Vector3 hand = GetHandOrigin();

        Vector3 toHand = hand - center;
        toHand.y = 0f;
        if (toHand.sqrMagnitude < 0.01f)
            toHand = -axisForward;
        toHand.Normalize();

        float attachAngle = Mathf.Atan2(Vector3.Dot(axisRight, toHand), Vector3.Dot(axisForward, toHand)) * Mathf.Rad2Deg;

        float charge = _forceGauge * 0.01f;
        float pulse = 1f + Mathf.Sin(Time.time * 14f) * 0.04f;
        float widthMul = _lassoHandWidthMul * (1f + charge * 0.28f) * pulse;
        _renderer.DrawLasso(hand, center, axisRight, axisForward, _lassoRadius, _spinAngle, attachAngle,
                            _ropeStartWidth * widthMul, _ropeEndWidth * (0.9f + charge * 0.15f));
    }

    // ── 投擲 ─────────────────────────────────────────────────
    private void ReleaseThrow()
    {
        _chargedThrowRange = Mathf.Lerp(_minThrowRange, _maxThrowRange, _lastChargeGauge / 100f);
        // 物理アンカーは「クロスヘア中心（カメラ）」からキャストする。手元（右に 0.32m オフセット）から
        // 撃つとアンカーが照準より右にずれ、回収が右へ寄って見える原因になるため分離する。
        Vector3 aimOrigin = AimTransform.position;
        Vector3 visualOrigin = GetHandOrigin();
        Vector3 direction = GetAimDirection();

        GameServices.Audio?.PlaySE(SoundId.GrapplingFire, visualOrigin);

        if (_throwRoutine != null)
            StopCoroutine(_throwRoutine);
        _throwRoutine = StartCoroutine(ThrowRoutine(aimOrigin, visualOrigin, direction));
    }

    private IEnumerator ThrowRoutine(Vector3 aimOrigin, Vector3 visualOrigin, Vector3 direction)
    {
        SetPhase(WireRopePhase.Throwing);
        _renderer.SetVisible(true);
        _renderer.SetWidths(_ropeStartWidth, _ropeEndWidth);

        // 命中判定は照準中心ライン（aimOrigin）で行い、アンカーを照準どおりに置く。
        bool hit = TryResolveThrowTarget(aimOrigin, direction, out RaycastHit hitInfo);
        Vector3 targetPoint = hit ? hitInfo.point : aimOrigin + direction * _chargedThrowRange;
        if (hit)
        {
            _anchorNormal = hitInfo.normal.sqrMagnitude > 0.01f ? hitInfo.normal.normalized : Vector3.up;
            _anchorIsGround = _anchorNormal.y >= GroundNormalThreshold;
            ResolvePullTarget(hitInfo.collider);
        }

        // 見た目のフック飛翔・ロープは手元（visualOrigin）から描く。
        _renderer.SpawnHook(visualOrigin);
        float distance = Vector3.Distance(visualOrigin, targetPoint);
        float duration = Mathf.Max(0.08f, distance / _throwSpeed);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOut(elapsed / duration, _throwEasePower);
            Vector3 hookPos = Vector3.Lerp(visualOrigin, targetPoint, t);
            float sag = Mathf.Sin(t * Mathf.PI) * _ropeSag * (1f - t * 0.25f);
            _renderer.DrawCurve(visualOrigin, hookPos, sag, _ropeStartWidth);
            _renderer.SyncHook(hookPos, Time.deltaTime);
            yield return null;
        }

        if (hit)
        {
            _anchorPoint = targetPoint;
            SetPhase(WireRopePhase.Attached);
            _visualSagBlend = _ropeSag * 0.5f;
            UpdateRetrieveRopeVisual();
            GameServices.Audio?.PlaySE(SoundId.GrapplingHit, _anchorPoint);
            AddCameraTrauma(_traumaRopeHit);
            _renderer.PulseHook(1.45f);
            _renderer.AddTwang(0.13f);
            _renderer.HitBurst(_anchorPoint, _pullTargetToPlayer ? 0.7f : 1f, spark: true);
        }
        else
        {
            GameServices.Audio?.PlaySE(SoundId.ItemImpact, targetPoint);
            yield return new WaitForSeconds(0.12f);
            ResetToReady();
        }

        _throwRoutine = null;
    }

    private bool TryResolveThrowTarget(Vector3 origin, Vector3 direction, out RaycastHit hitInfo)
    {
        int count = Physics.RaycastNonAlloc(origin, direction, _rayHits, _chargedThrowRange, _hookableLayers, QueryTriggerInteraction.Ignore);
        float bestDist = float.MaxValue;
        hitInfo = default;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            var h = _rayHits[i];
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.distance < bestDist)
            {
                bestDist = h.distance;
                hitInfo = h;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// ロープ先の命中対象が遺物・キャラなら「対象を自分へ引き寄せる」モードにする。
    /// それ以外（地形・岩など）は従来どおり「自分を対象位置へ移動する」モード。
    /// </summary>
    private void ResolvePullTarget(Collider hitCollider)
    {
        _pullTargetToPlayer = false;
        _attachedTargetBody = null;
        _attachedTargetTransform = null;
        _suppressedTargetController = null;

        if (hitCollider == null)
            return;

        var relic = hitCollider.GetComponentInParent<RelicBase>();
        if (relic != null)
        {
            var relicBody = relic.GetComponent<Rigidbody>();
            if (relicBody != null && relicBody != _rb)
            {
                _attachedTargetBody = relicBody;
                _attachedTargetTransform = relic.transform;
                _pullTargetToPlayer = true;
                return;
            }
        }

        var npc = hitCollider.GetComponentInParent<NPCController>();
        var explorer = hitCollider.GetComponentInParent<ExplorerController>();
        bool isCharacter = npc != null || explorer != null || hitCollider.CompareTag("Player");
        if (!isCharacter)
            return;

        var charBody = hitCollider.GetComponentInParent<Rigidbody>();
        if (charBody == null || charBody == _rb || charBody.transform.IsChildOf(transform))
            return;

        _attachedTargetBody = charBody;
        _attachedTargetTransform = charBody.transform;
        _suppressedTargetController = npc != null ? (Behaviour)npc : explorer;
        _pullTargetToPlayer = true;
    }

    // ── 回収（AddForce 慣性・重力残し） ─────────────────────
    private void BeginRetrieve()
    {
        SetPhase(WireRopePhase.Retrieving);
        _retrieveTimer = 0f;
        _ropeReleased = false;
        _retrieveTensionBlend = 0f;
        _pullStallTimer = 0f;
        _overshootTimer = 0f;

        // 遺物・キャラを引っかけている場合は「対象を自分へ引き寄せる」回収に切り替える。
        if (_pullTargetToPlayer && _attachedTargetBody != null)
        {
            BeginTargetPull();
            return;
        }

        SaveAndEnableRigidbodyMotor();

        _retrieveChargeFactor = 1f + _chargePowerSpread * (_lastChargeGauge / 100f - 0.5f) * 2f;

        // 引き距離とゲージから今回の目標速度・オーバーシュート量を決める（射程連動）。
        _retrieveStartDistance = Vector3.Distance(_rb.position, _anchorPoint);
        _retrieveEngage01 = Mathf.Clamp01(Mathf.InverseLerp(_minThrowRange, _maxThrowRange, _retrieveStartDistance));
        ComputeTargetPullSpeed();
        _effectiveOvershootDistance = Mathf.Clamp(_retrieveStartDistance * _overshootDistanceFactor, _overshootMinDistance, _maxOvershootDistance);
        _effectiveOvershootMaxSeconds = Mathf.Lerp(1.2f, _overshootMaxSeconds, _retrieveEngage01);
        _retrieveUsesRaisedGroundPull = IsGroundAnchor() && GetGroundAnchorLift() > _groundClimbLiftThreshold;

        Vector3 runDir = Flatten(_anchorPoint - _rb.position);
        _retrieveRunDirectionXZ = runDir.sqrMagnitude > 0.01f ? runDir.normalized : GetFlatForward();

        _peakTensionSpeed = 0f;
        _overshootOrigin = _rb.position;
        _retrieveStopPoint = ComputeRetrieveStopPoint();
        _retrievePullAxis = GetPhysicsPullDirection();
        _inertiaSlideDirection = _retrievePullAxis;
        ApplyRetrieveStartVelocity();

        _renderer.SetVisible(true);
        _tensionSoundTimer = 0f;
        if (IsGroundAnchor())
            SetRetrieveSlideFriction(true);

        GameServices.Audio?.PlaySE(SoundId.WinchStart, transform.position);
        GameServices.Audio?.PlaySE2D(SoundId.WinchLoop);
        AddCameraTrauma(_traumaRetrieveStart);
    }

    // ── 回収（対象引き寄せ：遺物 / キャラを自分へ） ─────────
    private void BeginTargetPull()
    {
        _retrieveChargeFactor = 1f + _chargePowerSpread * (_lastChargeGauge / 100f - 0.5f) * 2f;
        _retrieveTensionBlend = 0f;
        _retrieveTimer = 0f;

        _targetSavedKinematic = _attachedTargetBody.isKinematic;
        _targetSavedUseGravity = _attachedTargetBody.useGravity;
        if (_attachedTargetBody.isKinematic)
            _attachedTargetBody.isKinematic = false;
        // 引き寄せ中は重力を切って素直に自分へ飛んでくるようにする。
        _attachedTargetBody.useGravity = false;
        _attachedTargetBody.WakeUp();

        // キャラ自身の移動制御を一時停止し、引き寄せ力が打ち消されないようにする。
        if (_suppressedTargetController != null)
            _suppressedTargetController.enabled = false;

        _renderer.SetVisible(true);
        _tensionSoundTimer = 0f;
        GameServices.Audio?.PlaySE(SoundId.WinchStart, transform.position);
        GameServices.Audio?.PlaySE2D(SoundId.WinchLoop);
        AddCameraTrauma(_traumaRetrieveStart);
    }

    private void UpdateTargetPullPhysics()
    {
        float dt = Time.fixedDeltaTime;
        _retrieveTimer += dt;

        if (_attachedTargetBody == null || _retrieveTimer >= _maxRetrieveSeconds)
        {
            FinishTargetPull();
            return;
        }

        Vector3 handPos = GetHandOrigin();
        Vector3 targetPos = _attachedTargetBody.worldCenterOfMass;
        Vector3 toPlayer = handPos - targetPos;
        float dist = toPlayer.magnitude;

        _anchorPoint = _attachedTargetTransform.position;

        if (dist <= _targetPullArrivalDistance)
        {
            FinishTargetPull();
            return;
        }

        Vector3 pullDir = toPlayer / Mathf.Max(dist, 0.0001f);

        _retrieveTensionBlend = Mathf.MoveTowards(
            _retrieveTensionBlend, 1f, dt / Mathf.Max(0.05f, _retrieveTensionRampSeconds));
        float tensionScale = RetrieveTensionEased;

        _attachedTargetBody.AddForce(
            pullDir * (_targetPullAccel * _retrieveChargeFactor * tensionScale), ForceMode.Acceleration);

        Vector3 vel = _attachedTargetBody.linearVelocity;
        float along = Vector3.Dot(vel, pullDir);
        float minSpeed = _targetPullMinSpeed * tensionScale;
        if (along < minSpeed)
        {
            vel += pullDir * (minSpeed - along);
            _attachedTargetBody.linearVelocity = vel;
        }

        if (vel.magnitude > _targetPullMaxSpeed)
            _attachedTargetBody.linearVelocity = vel.normalized * _targetPullMaxSpeed;

        TryPlayTensionSound(tensionScale, dt);
    }

    private void FinishTargetPull()
    {
        if (_attachedTargetBody != null && !_attachedTargetBody.isKinematic)
            _attachedTargetBody.linearVelocity *= 0.25f;

        GameServices.Audio?.PlaySE(SoundId.RopeCut, transform.position);
        ResetToReady();
    }

    private void RestoreTargetBodyState()
    {
        if (_attachedTargetBody != null)
        {
            _attachedTargetBody.useGravity = _targetSavedUseGravity;
            _attachedTargetBody.isKinematic = _targetSavedKinematic;
            _attachedTargetBody.WakeUp();
        }
    }

    private void RestoreSuppressedTargetController()
    {
        if (_suppressedTargetController == null)
            return;

        _suppressedTargetController.enabled = true;
        _suppressedTargetController = null;
    }

    private void SetRetrieveSlideFriction(bool enabled)
    {
        if (_bodyCapsule == null)
            return;

        PhysicsMaterial mat = _bodyCapsule.material;
        if (mat == null)
            return;

        if (enabled)
        {
            if (_retrieveSlideFrictionActive)
                return;

            _savedCapsuleDynamicFriction = mat.dynamicFriction;
            _savedCapsuleStaticFriction = mat.staticFriction;
            mat.dynamicFriction = 0.06f;
            mat.staticFriction = 0.1f;
            _retrieveSlideFrictionActive = true;
            return;
        }

        if (!_retrieveSlideFrictionActive)
            return;

        mat.dynamicFriction = _savedCapsuleDynamicFriction;
        mat.staticFriction = _savedCapsuleStaticFriction;
        _retrieveSlideFrictionActive = false;
    }

    /// <summary>
    /// 引き距離（射程）とゲージから今回の目標引き速度を決める。
    /// 近距離は穏やかに、遠距離は豪快に。ゲージで±振れる。
    /// </summary>
    private void ComputeTargetPullSpeed()
    {
        float baseSpeed = Mathf.Lerp(_pullSpeedShort, _pullMaxSpeed, _retrieveEngage01);
        float charge01 = Mathf.Clamp01(_lastChargeGauge / 100f);
        float chargeMul = Mathf.Lerp(1f - _pullSpeedChargeSpread, 1f + _pullSpeedChargeSpread, charge01);
        _targetPullSpeed = Mathf.Clamp(baseSpeed * chargeMul, _pullMinSpeed * 0.6f, _pullMaxSpeed);
    }

    /// <summary>目標速度に対する最低維持速度（近づいても止まらない下限）。</summary>
    private float PullFloorSpeed(float tensionScale) =>
        _targetPullSpeed * _pullFloorFraction * Mathf.Max(tensionScale, _tensionFalloffFloor);

    private void ApplyRetrieveStartVelocity()
    {
        float snap = (_pullSnapImpulse + _gaugeLaunchBoost * (_lastChargeGauge / 100f)) * _retrieveChargeFactor;
        Vector3 pullDir = GetPhysicsPullDirection();
        _retrievePullAxis = pullDir;
        _inertiaSlideDirection = GetReleaseSlideDirection();

        Vector3 current = BodyLinearVelocity;
        float along = Vector3.Dot(current, pullDir);
        Vector3 perpendicular = current - pullDir * along;

        float blend = _retrieveStartVelocityBlend;
        // 初速スナップは目標速度を超えない（近距離での過剰な吹っ飛びを防ぐ）。
        float newAlong = Mathf.Min(Mathf.Max(along, 0f) + snap * blend, _targetPullSpeed);
        SetBodyLinearVelocity(pullDir * newAlong + perpendicular * (1f - blend * 0.35f));
        ClampExcessUpwardVelocity();
    }

    private bool IsGroundAnchor() => _anchorIsGround;

    private float GetGroundAnchorLift() => _anchorPoint.y - (_rb != null ? _rb.position.y : transform.position.y);

    private bool ShouldUseRaisedGroundPull() =>
        IsGroundAnchor() && (_retrieveUsesRaisedGroundPull || GetGroundAnchorLift() > _groundClimbLiftThreshold);

    /// <summary>
    /// 地面フック: 空中の停止点にせず、アンカー付近の地表上に停止点を置く（めり込み防止）。
    /// 壁フック: 法線方向にオフセット。
    /// </summary>
    private Vector3 ComputeRetrieveStopPoint()
    {
        if (!IsGroundAnchor())
            return _anchorPoint + _anchorNormal * _retrieveStopDistance;

        float horizDist = Mathf.Min(
            Vector3.Distance(new Vector3(_rb.position.x, 0f, _rb.position.z),
                             new Vector3(_anchorPoint.x, 0f, _anchorPoint.z)),
            _retrieveStopDistance);
        horizDist = Mathf.Max(0.35f, horizDist);

        Vector3 xz = _anchorPoint + _retrieveRunDirectionXZ * horizDist;
        return ProjectBodyCenterOntoGround(xz, extraLift: 0f);
    }

    /// <summary>プレイヤー→アンカー（フック）の引き方向。常にフックへ向けて引く（仰角はクランプ）。</summary>
    private Vector3 GetPhysicsPullDirection()
    {
        Vector3 toAnchor = _anchorPoint - _rb.position;
        if (toAnchor.sqrMagnitude < 0.0001f)
            return GetFlatForward();

        if (IsGroundAnchor())
        {
            if (ShouldUseRaisedGroundPull())
                return ClampPullElevation(toAnchor.normalized);

            Vector3 flat = Flatten(toAnchor);
            return flat.sqrMagnitude < 0.0001f ? GetFlatForward() : flat.normalized;
        }

        // 壁・岩などの非地面アンカー：フック（アンカー）そのものへ向かって引く（仰角クランプのみ）。
        // 旧実装は toAnchor を壁面へ射影していたため、壁に少しでも斜めに当てると射影残差（壁沿いの
        // 横成分）が正規化されてフル強度の「真横へ滑る」引きになり、フックではなく右/左へ移動していた。
        return ClampPullElevation(toAnchor.normalized);
    }

    /// <summary>仰角クランプの上限[°]。アンカー幾何（高さ）に依存するためインスタンス側で計算。</summary>
    private float ComputeClampMaxElevationDeg()
    {
        float maxDeg = _maxPullElevationDeg;
        if (!IsGroundAnchor() || ShouldUseRaisedGroundPull())
        {
            float anchorAbove = _anchorPoint.y - _rb.position.y;
            maxDeg = Mathf.Lerp(_maxPullElevationDeg, _maxPullElevationDegWallUp,
                Mathf.Clamp01(Mathf.InverseLerp(1.5f, 14f, anchorAbove)));
        }
        return maxDeg;
    }

    private Vector3 ClampPullElevation(Vector3 dir)
        => WireRopePhysics.ClampElevation(dir, ComputeClampMaxElevationDeg(), _retrieveRunDirectionXZ, GetFlatForward());

    /// <summary>ロープがどれだけ「伸びている」か 0〜1（弾性張力用）。</summary>
    private float GetRopeStretch01()
    {
        float refLen = Mathf.Max(_chargedThrowRange, _minThrowRange);
        if (IsGroundAnchor() && !ShouldUseRaisedGroundPull())
        {
            float horiz = Vector3.Distance(
                new Vector3(_rb.position.x, 0f, _rb.position.z),
                new Vector3(_anchorPoint.x, 0f, _anchorPoint.z));
            return Mathf.Clamp01(horiz / refLen);
        }

        return Mathf.Clamp01(Vector3.Distance(_rb.position, _anchorPoint) / refLen);
    }

    private float ComputeCableTensionMultiplier(float distToStop, float tensionScale)
    {
        float stretchDist = Mathf.InverseLerp(3f, _retrieveStopDistance + 10f, distToStop);
        float stretch = Mathf.Max(GetRopeStretch01(), stretchDist);
        float elastic = 1f + Mathf.Pow(stretch, _ropeElasticPower) * _ropeElasticGain;
        float snap = 1f + Mathf.InverseLerp(10f, _retrieveStopDistance, distToStop) * _pullSnapTensionBoost;
        return elastic * snap * tensionScale * _retrieveChargeFactor;
    }

    private void ApplyRetrieveMotor(Vector3 pullDir, Vector3 vel, float distToStop, float tensionScale)
    {
        float tensionMul = ComputeCableTensionMultiplier(distToStop, tensionScale);
        AddBodyForce(pullDir * (_pullTensionAccel * tensionMul), ForceMode.Acceleration);

        if (IsGroundAnchor())
        {
            ApplyGroundSlideAlongPull(pullDir, vel, tensionScale);
            return;
        }

        ApplyAirSwingCorrection(pullDir, vel, tensionScale);
    }

    private void ApplyAirSwingCorrection(Vector3 pullDir, Vector3 vel, float tensionScale)
    {
        float speed = vel.magnitude;
        if (speed < 4f || pullDir.sqrMagnitude < 0.01f)
            return;

        float align = Vector3.Dot(vel.normalized, pullDir);
        if (align > 0.88f)
            return;

        Vector3 toAnchor = _anchorPoint - _rb.position;
        if (toAnchor.sqrMagnitude < 0.04f)
            return;

        Vector3 radial = ClampPullElevation(toAnchor.normalized);
        AddBodyForce(radial * (speed * _swingCentripetalGain * tensionScale), ForceMode.Acceleration);
    }

    private void ClampExcessUpwardVelocity()
    {
        if (!CanDriveRigidbody)
            return;

        Vector3 vel = _rb.linearVelocity;
        if (vel.y <= _maxUpwardSpeedFromTension)
            return;

        // 上限超過は即座に抑える（レート制限だと数フレーム突き抜けて吹き上がって見える）。
        vel.y = _maxUpwardSpeedFromTension;
        SetBodyLinearVelocity(vel);
    }

    private float GetDistanceToAnchor()
    {
        // 平らな地面アンカーは水平距離（真下へ吸い込まれない）。
        // 上方の地面アンカー（坂の上）は登攀とみなし 3D 距離で測り、坂を登り切るまで引き続ける。
        if (IsGroundAnchor() && !ShouldUseRaisedGroundPull())
        {
            return Vector3.Distance(
                new Vector3(_rb.position.x, 0f, _rb.position.z),
                new Vector3(_anchorPoint.x, 0f, _anchorPoint.z));
        }

        return Vector3.Distance(_rb.position, _anchorPoint);
    }

    private float GetAnchorArrivalThreshold()
    {
        float extra = IsGroundAnchor() ? GetBodyHalfHeight() * 0.2f : 0f;
        return _anchorArrivalDistance + extra;
    }

    private bool HasReachedRopeTip() => GetDistanceToAnchor() <= GetAnchorArrivalThreshold();

    private bool IsNearRopeTip() => GetDistanceToAnchor() <= GetAnchorArrivalThreshold() + _releaseSoftDistance;

    private static Vector3 Flatten(Vector3 v) => new Vector3(v.x, 0f, v.z);

    private float ComputeTensionFalloff(float _)
    {
        float dist = GetDistanceToAnchor();
        float start = GetAnchorArrivalThreshold() + _releaseSoftDistance;
        if (dist >= start)
            return 1f;

        return Mathf.Lerp(_tensionFalloffFloor, 1f, Mathf.InverseLerp(GetAnchorArrivalThreshold(), start, dist));
    }

    private Vector3 GetReleaseSlideDirection()
    {
        Vector3 dir = GetPhysicsPullDirection();
        if (IsGroundAnchor())
        {
            dir = Flatten(dir);
            if (TryGetGroundContact(out Vector3 groundNormal, out _))
            {
                Vector3 onSurface = Vector3.ProjectOnPlane(dir, groundNormal);
                if (onSurface.sqrMagnitude > 0.01f)
                    dir = onSurface;
            }
        }

        if (dir.sqrMagnitude < 0.01f)
            dir = _retrieveRunDirectionXZ;

        return dir.normalized;
    }

    private float GetSpeedAlongDirection(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f)
            return 0f;

        direction.Normalize();
        Vector3 vel = BodyLinearVelocity;

        if (IsGroundAnchor() && TryGetGroundContact(out Vector3 groundNormal, out _))
        {
            vel = Vector3.ProjectOnPlane(vel, groundNormal);
            direction = Vector3.ProjectOnPlane(direction, groundNormal);
            if (direction.sqrMagnitude < 0.01f)
                return 0f;
            direction.Normalize();
        }

        return Mathf.Max(0f, Vector3.Dot(vel, direction));
    }

    private void UpdatePeakTensionSpeed()
    {
        float speed = GetSpeedAlongDirection(GetReleaseSlideDirection());
        _peakTensionSpeed = Mathf.Max(_peakTensionSpeed, speed);
    }

    private float GetOvershootSlideDistance()
    {
        if (_inertiaSlideDirection.sqrMagnitude < 0.01f)
            return 0f;

        Vector3 delta = _rb.position - _overshootOrigin;
        if (IsGroundAnchor())
            delta.y = 0f;

        return Vector3.Dot(delta, _inertiaSlideDirection.normalized);
    }

    private void UpdateRetrievePhysics()
    {
        float dt = Time.fixedDeltaTime;
        if (_impactBoostTimer > 0f)
            _impactBoostTimer = Mathf.Max(0f, _impactBoostTimer - dt);

        _retrieveTimer += dt;
        if (_retrieveTimer >= _maxRetrieveSeconds)
        {
            FinishRetrieveWithMomentum();
            return;
        }

        if (TryTriggerImpactSlingshotAhead())
            return;

        RefreshRetrievePullAxis(dt);
        RefreshRetrieveStopPoint(dt);
        CorrectGroundPenetrationOnly();
        ApplyGroundRetrieveSlide();
        ApplyRetrievePlayerSteering();

        if (_ropeReleased)
        {
            UpdateOvershootPhase();
            return;
        }

        if (IsBodyBlocked(_rb.position) && IsStuckOnObstacle())
        {
            TriggerImpactSlingshot(null);
            return;
        }

        _retrieveTensionBlend = Mathf.MoveTowards(
            _retrieveTensionBlend, 1f,
            dt / Mathf.Max(0.05f, _retrieveTensionRampSeconds));

        UpdatePeakTensionSpeed();

        float tensionFalloff = ComputeTensionFalloff(0f);
        UpdatePullStallTimer(dt);

        if (HasReachedRopeTip() || (IsNearRopeTip() && _pullStallTimer >= _pullStallReleaseSeconds))
        {
            ReleaseRopeAtTipWithInertia();
            UpdateOvershootPhase();
            return;
        }

        float dist = GetDistanceToAnchor();
        Vector3 vel = BodyLinearVelocity;
        float tensionScale = RetrieveTensionEased * tensionFalloff;

        Vector3 targetPull = GetPhysicsPullDirection();
        _retrievePullAxis = Vector3.Slerp(_retrievePullAxis, targetPull, 0.55f).normalized;
        ApplyRetrieveMotor(_retrievePullAxis, vel, dist, tensionScale);

        ApplyMinPullSpeed(_retrievePullAxis, vel, tensionScale);
        TryPlayTensionSound(tensionScale, dt);

        if (!HasMoveInput())
            ApplyPerpendicularDamp(_retrievePullAxis, vel, tensionScale);

        ClampExcessUpwardVelocity();
        ClampRetrieveSpeed();
    }

    private void RefreshRetrievePullAxis(float dt)
    {
        Vector3 target = GetPhysicsPullDirection();
        if (target.sqrMagnitude < 0.01f)
            return;

        _retrievePullAxis = Vector3.Slerp(_retrievePullAxis, target, _pullAxisTurnSpeed * dt).normalized;
    }

    private void RefreshRetrieveStopPoint(float dt)
    {
        Vector3 target = ComputeRetrieveStopPoint();
        if (IsGroundAnchor())
        {
            _retrieveStopPoint.x = target.x;
            _retrieveStopPoint.z = target.z;
            _retrieveStopPoint.y = Mathf.Lerp(_retrieveStopPoint.y, target.y, Mathf.Clamp01(dt * 6f));
            return;
        }

        _retrieveStopPoint = Vector3.Lerp(_retrieveStopPoint, target, Mathf.Clamp01(dt * 4f));
    }

    private void TryPlayTensionSound(float tensionScale, float dt)
    {
        if (_ropeReleased || tensionScale < 0.35f)
            return;

        _tensionSoundTimer -= dt;
        if (_tensionSoundTimer > 0f)
            return;

        float stretch = GetRopeStretch01();
        _tensionSoundTimer = _tensionSoundInterval * Mathf.Lerp(1f, 0.55f, stretch);
        GameServices.Audio?.PlaySE(SoundId.RopeTension, Vector3.Lerp(transform.position, _anchorPoint, 0.5f));

        float trauma = _traumaRetrieveStart * (0.35f + stretch * 0.45f);
        if (BodyLinearVelocity.magnitude > 18f)
            AddCameraTrauma(trauma);

        // 張力に同期してロープを軽く弾く（負荷で震えて見える）。
        _renderer.AddTwang(0.04f + stretch * 0.05f);
    }

    private void ApplyPerpendicularDamp(Vector3 pullDir, Vector3 vel, float tensionScale)
    {
        float speedAlong = Vector3.Dot(vel, pullDir);
        Vector3 lateral = vel - pullDir * speedAlong;
        if (lateral.sqrMagnitude < 0.01f)
            return;

        float speed = vel.magnitude;
        float damp = _pullPerpendicularDamp * tensionScale;
        damp *= Mathf.Lerp(1f, 0.32f, Mathf.InverseLerp(10f, 28f, speed));
        if (IsGroundAnchor())
            damp *= 0.22f;

        AddBodyForce(-lateral.normalized * (damp * lateral.magnitude), ForceMode.Acceleration);
    }

    private void ApplyMinPullSpeed(Vector3 pullDir, Vector3 vel, float tensionScale)
    {
        pullDir = ClampPullElevation(pullDir.normalized);
        bool use3dPull = !IsGroundAnchor() || ShouldUseRaisedGroundPull();
        Vector3 useDir = use3dPull ? pullDir : Flatten(pullDir);
        Vector3 useVel = use3dPull ? vel : Flatten(vel);
        if (useDir.sqrMagnitude < 0.01f)
            return;
        useDir.Normalize();

        float speedAlong = Vector3.Dot(useVel, useDir);
        float minSpeed = PullFloorSpeed(tensionScale);
        if (speedAlong >= minSpeed)
            return;

        Vector3 delta = useDir * (minSpeed - speedAlong);
        float maxDelta = _maxSpeedChangePerSecond * Time.fixedDeltaTime;
        if (delta.magnitude > maxDelta)
            delta = delta.normalized * maxDelta;

        if (IsGroundAnchor() && !use3dPull)
            SetBodyLinearVelocity(new Vector3(vel.x + delta.x, vel.y, vel.z + delta.z));
        else
            AddBodyLinearVelocity(delta);

        ClampExcessUpwardVelocity();
    }

    private void ApplyGroundSlideAlongPull(Vector3 pullDir, Vector3 vel, float tensionScale)
    {
        if (!TryGetGroundContact(out Vector3 groundNormal, out _))
            return;

        Vector3 baseDir = ShouldUseRaisedGroundPull() ? pullDir : Flatten(pullDir);
        Vector3 slideDir = Vector3.ProjectOnPlane(baseDir, groundNormal);
        if (slideDir.sqrMagnitude < 0.01f)
            slideDir = Vector3.ProjectOnPlane(Flatten(vel), groundNormal);
        if (slideDir.sqrMagnitude < 0.01f)
            return;

        slideDir.Normalize();
        Vector3 surfaceVel = Vector3.ProjectOnPlane(vel, groundNormal);
        float along = Vector3.Dot(surfaceVel, slideDir);

        float minSlide = PullFloorSpeed(tensionScale);
        if (along < minSlide)
            AddBodyForce(slideDir * (_groundSlideAssistAccel * (1f - along / Mathf.Max(minSlide, 0.5f))), ForceMode.Acceleration);
        else
            AddBodyForce(slideDir * (_groundSlideAssistAccel * 0.35f), ForceMode.Acceleration);
    }

    /// <summary>
    /// 地面フック回収中: 床接触で止まらず、法線方向の抵抗＋接線方向のスライドを維持する。
    /// </summary>
    private void ApplyGroundRetrieveSlide()
    {
        if (!IsGroundAnchor() || !CanDriveRigidbody)
            return;

        if (!TryGetGroundContact(out Vector3 groundNormal, out float groundY))
            return;

        float halfH = GetBodyHalfHeight();
        float feetY = groundY + halfH + GroundClearance;
        float airGap = _rb.position.y - feetY;
        if (airGap > _groundSlideMaxAirGap)
            return;

        Vector3 pull = ShouldUseRaisedGroundPull() ? _retrievePullAxis : Flatten(_retrievePullAxis);
        if (pull.sqrMagnitude < 0.01f)
            pull = _retrieveRunDirectionXZ;
        pull.Normalize();

        Vector3 slideDir = Vector3.ProjectOnPlane(pull, groundNormal);
        if (slideDir.sqrMagnitude < 0.01f)
            slideDir = Vector3.ProjectOnPlane(Flatten(BodyLinearVelocity), groundNormal);
        if (slideDir.sqrMagnitude < 0.01f)
            return;
        slideDir.Normalize();

        Vector3 vel = BodyLinearVelocity;
        float intoGround = Vector3.Dot(vel, groundNormal);
        if (intoGround < 0f)
            vel -= groundNormal * (intoGround * _groundImpactBleed);

        Vector3 surfaceVel = Vector3.ProjectOnPlane(vel, groundNormal);
        float along = Vector3.Dot(surfaceVel, slideDir);
        Vector3 lateral = surfaceVel - slideDir * along;

        float tension = _ropeReleased ? 0.45f : Mathf.Max(RetrieveTensionEased, 0.35f);
        float minSlide = _targetPullSpeed * _pullFloorFraction * tension;

        if (along < minSlide)
        {
            float deficit = minSlide - Mathf.Max(along, 0f);
            surfaceVel += slideDir * deficit;
        }

        if (lateral.sqrMagnitude > 0.04f)
            surfaceVel -= lateral * Mathf.Clamp01(_groundSlideLateralResist * Time.fixedDeltaTime);

        if (airGap < -0.02f)
            vel.y = Mathf.Max(vel.y, 0f);

        SetBodyLinearVelocity(new Vector3(surfaceVel.x, vel.y, surfaceVel.z));
    }

    private bool TryGetGroundContact(out Vector3 groundNormal, out float groundY)
    {
        groundNormal = Vector3.up;
        groundY = _rb.position.y;

        if (!TrySampleGroundHeight(_rb.position, out groundY, out RaycastHit hit))
            return false;

        if (hit.normal.sqrMagnitude > 0.01f)
            groundNormal = hit.normal.normalized;

        return true;
    }

    private void ClampRetrieveSpeed()
    {
        float maxSpeed = _impactBoostTimer > 0f ? _impactSlingshotMaxSpeed : _targetPullSpeed;
        Vector3 vel = BodyLinearVelocity;
        if (vel.magnitude <= maxSpeed) return;

        // 張力フェーズ中は即座にキャップ（張力モータ＋スライド補助の合算で
        // レート制限だと目標速度を保持できず、近距離でも最大速度まで吹っ飛ぶため）。
        // 離脱後オーバーシュート／スリングショット中は運動量を残すため緩やかに減速。
        if (_ropeReleased || _impactBoostTimer > 0f)
            SetBodyLinearVelocity(Vector3.MoveTowards(vel, vel.normalized * maxSpeed, _maxSpeedChangePerSecond * Time.fixedDeltaTime));
        else
            SetBodyLinearVelocity(vel.normalized * maxSpeed);
    }

    private void ReleaseRopeAtTipWithInertia()
    {
        if (_ropeReleased)
            return;

        _ropeReleased = true;
        _overshootTimer = 0f;
        _pullStallTimer = 0f;
        _overshootOrigin = _rb.position;

        Vector3 slideDir = GetReleaseSlideDirection();
        _inertiaSlideDirection = slideDir;
        _retrievePullAxis = slideDir;

        float tensionSpeed = Mathf.Max(_peakTensionSpeed, GetSpeedAlongDirection(slideDir));
        tensionSpeed *= _releaseInertiaScale * _overshootMomentumScale * _retrieveChargeFactor;
        // 吹っ飛び速度も目標速度に連動させる（近距離は控えめ、遠距離は豪快）。
        float maxInertia = Mathf.Min(_releaseInertiaMaxSpeed, _targetPullSpeed * _releaseInertiaScale + 2f);
        float minInertia = Mathf.Lerp(1.5f, _releaseInertiaMinSpeed, _retrieveEngage01);
        tensionSpeed = Mathf.Clamp(tensionSpeed, minInertia, maxInertia);

        Vector3 vel = BodyLinearVelocity;
        Vector3 slideVel = slideDir * tensionSpeed;
        Vector3 perpendicular = vel - slideDir * Vector3.Dot(vel, slideDir);
        vel = slideVel + perpendicular * _releasePerpendicularRetain;

        if (IsGroundAnchor() && ShouldUseRaisedGroundPull())
            vel.y = Mathf.Clamp(vel.y, 0f, _maxUpwardSpeedFromTension);
        else if (IsGroundAnchor())
            vel.y = Mathf.Max(BodyLinearVelocity.y * 0.25f, 0f);
        else if (vel.y > _maxUpwardSpeedFromTension)
            vel.y = _maxUpwardSpeedFromTension; // 真上アンカーでの吹き上がりを離脱時に確実に抑える

        SetBodyLinearVelocity(vel);
        ClampExcessUpwardVelocity();

        RetractRopeVisual();
        GameServices.Audio?.PlaySE(SoundId.RopeCut, transform.position);
        AddCameraTrauma(_traumaRopeRelease);
    }

    private void RetractRopeVisual()
    {
        _renderer.ClearHook();
        _renderer.SetVisible(false);
    }

    private void UpdatePullStallTimer(float dt)
    {
        if (!IsNearRopeTip())
        {
            _pullStallTimer = 0f;
            return;
        }

        if (IsGroundAnchor() && Flatten(_rb.linearVelocity).magnitude >= _floorSlingshotMinHorizSpeed)
        {
            _pullStallTimer = 0f;
            return;
        }

        Vector3 slideDir = GetReleaseSlideDirection();
        if (GetSpeedAlongDirection(slideDir) < 2f)
            _pullStallTimer += dt;
        else
            _pullStallTimer = 0f;
    }

    private void UpdateOvershootPhase()
    {
        _overshootTimer += Time.fixedDeltaTime;

        if (TryTriggerImpactSlingshotAhead())
            return;

        CorrectGroundPenetrationOnly();
        ApplyGroundRetrieveSlide();
        ApplyRetrievePlayerSteering();

        Vector3 vel = BodyLinearVelocity;

        if (_impactBoostTimer > 0f)
            ApplyImpactSlingshotSustainForce();

        if (HasMoveInput())
        {
            FinishRetrieveWithMomentum();
            return;
        }

        float speed = GetRetrieveMomentumSpeed(vel);
        float slideDist = GetOvershootSlideDistance();

        if (_impactBoostTimer <= 0f
            && (_overshootTimer >= _effectiveOvershootMaxSeconds
                || slideDist >= _effectiveOvershootDistance
                || speed < _overshootEndSpeed))
        {
            FinishRetrieveWithMomentum();
            return;
        }

        if (_inertiaSlideDirection.sqrMagnitude > 0.01f)
        {
            float speedAlong = Vector3.Dot(vel, _inertiaSlideDirection.normalized);
            if (speedAlong < 0f && _impactBoostTimer <= 0f)
                ApplyVelocityDeltaSmooth(_inertiaSlideDirection.normalized * (-speedAlong * 0.2f));
        }

        ClampExcessUpwardVelocity();
        ClampRetrieveSpeed();
    }

    private void ApplyVelocityDeltaSmooth(Vector3 delta)
    {
        float maxDelta = _maxSpeedChangePerSecond * Time.fixedDeltaTime;
        if (delta.magnitude > maxDelta)
            delta = delta.normalized * maxDelta;
        AddBodyLinearVelocity(delta);
    }

    private void FinishRetrieveWithMomentum()
    {
        if (_phase != WireRopePhase.Retrieving)
            return;

        Vector3 vel = BodyLinearVelocity;
        Vector3 pos = _rb.position;
        float originalY = pos.y;

        if (IsBodyBlocked(pos))
            ResolveEmbeddedPosition(ref pos);
        else
            // 地表より下に潜って宙に浮いている場合も救済（TryLiftFromTerrain は地表上なら何もしない）。
            TryLiftFromTerrain(ref pos);

        if (Vector3.Distance(pos, _rb.position) > 0.05f)
            SnapBodyPositionOnly(pos);

        // 地表下から引き上げた場合は下向き速度を消し、終了直後の自由落下（すり抜け再発）を断つ。
        if (pos.y > originalY + 0.05f && vel.y < 0f)
            vel.y = 0f;

        SetBodyLinearVelocity(vel);
        SetBodyAngularVelocity(Vector3.zero);
        Physics.SyncTransforms();

        CompleteRetrieve();
    }

    /// <summary>障害物衝突時: ロープ先端（アンカー）方向へ引き剥がして吹っ飛ばす。</summary>
    private void TriggerImpactSlingshot(Collision collision)
    {
        if (_phase != WireRopePhase.Retrieving)
            return;
        if (Time.time - _lastImpactSlingshotTime < _impactSlingshotCooldown)
            return;

        _lastImpactSlingshotTime = Time.time;
        _impactBoostTimer = _impactSlingshotBoostSeconds;
        _pullStallTimer = 0f;
        _overshootTimer = 0f;

        Vector3 vel = BodyLinearVelocity;
        if (collision != null && collision.contactCount > 0)
        {
            ContactPoint contact = collision.GetContact(0);
            float into = Vector3.Dot(vel, contact.normal);
            if (into < 0f)
                vel -= contact.normal * into;

            _rb.position += contact.normal * 0.1f;
        }

        Vector3 surfaceNormal = Vector3.up;
        if (collision != null && collision.contactCount > 0)
            surfaceNormal = collision.GetContact(0).normal;

        vel = ComputeImpactLaunchVelocity(vel, surfaceNormal);

        if (!_ropeReleased)
        {
            _ropeReleased = true;
            _overshootOrigin = _rb.position;
            _inertiaSlideDirection = GetReleaseSlideDirection();
            RetractRopeVisual();
            GameServices.Audio?.PlaySE(SoundId.RopeCut, transform.position);
        }

        _retrievePullAxis = IsGroundAnchor() ? Flatten(vel).normalized : ClampPullElevation(vel.normalized);
        if (_retrievePullAxis.sqrMagnitude < 0.01f)
            _retrievePullAxis = GetPhysicsPullDirection();

        SetBodyLinearVelocity(Vector3.ClampMagnitude(vel, _impactSlingshotMaxSpeed));
        SetBodyAngularVelocity(Vector3.zero);
        Physics.SyncTransforms();
        AddCameraTrauma(_traumaImpactSlingshot);
        _renderer.PulseHook(1.25f);
        _renderer.AddTwang(0.12f);

        Vector3 burstAt = collision != null && collision.contactCount > 0
            ? collision.GetContact(0).point
            : _rb.position;
        _renderer.HitBurst(burstAt, 1.2f, spark: true);
    }

    private void ApplyImpactSlingshotSustainForce()
    {
        Vector3 pullDir = GetPhysicsPullDirection();
        if (pullDir.sqrMagnitude < 0.01f)
            return;

        float mul = ComputeCableTensionMultiplier(
            Vector3.Distance(_rb.position, _retrieveStopPoint), 0.75f);
        AddBodyForce(pullDir * (_pullTensionAccel * 0.38f * mul), ForceMode.Acceleration);
    }

    private Vector3 ComputeImpactLaunchVelocity(Vector3 incomingVel, Vector3 surfaceNormal)
        => WireRopePhysics.ComputeImpactLaunch(
            GetPhysicsPullDirection(), incomingVel, surfaceNormal,
            ComputeClampMaxElevationDeg(), _retrieveRunDirectionXZ, GetFlatForward(),
            _retrieveChargeFactor, Cfg);

    private float GetRetrieveMomentumSpeed(Vector3 vel)
    {
        if (!IsGroundAnchor())
            return vel.magnitude;

        float planar = Flatten(vel).magnitude;
        float vertical = Mathf.Max(0f, vel.y);
        return planar + vertical * 0.45f;
    }

    private bool TryTriggerImpactSlingshotAhead()
    {
        if (_phase != WireRopePhase.Retrieving)
            return false;
        if (Time.time - _lastImpactSlingshotTime < _impactSlingshotCooldown)
            return false;

        Vector3 vel = _rb.linearVelocity;
        if (vel.sqrMagnitude < 1f)
        {
            if (!IsStuckOnObstacle())
                return false;

            TriggerImpactSlingshot(null);
            return true;
        }

        Vector3 castDir = vel.normalized;
        if (IsGroundAnchor())
        {
            Vector3 pull = Flatten(_retrievePullAxis);
            if (pull.sqrMagnitude > 0.01f && Vector3.Dot(Flatten(vel).normalized, pull.normalized) > 0.35f)
                castDir = pull.normalized;
        }

        GetCapsuleEnds(_rb.position, out Vector3 p1, out Vector3 p2, out float radius);
        float castDist = vel.magnitude * Time.fixedDeltaTime + 0.45f;
        if (!Physics.CapsuleCast(p1, p2, radius * 0.92f, castDir, out RaycastHit hit, castDist, _collisionMask,
                QueryTriggerInteraction.Ignore))
            return false;

        if (hit.collider != null && hit.collider.transform.IsChildOf(transform))
            return false;

        if (hit.normal.y >= GroundNormalThreshold && IsGroundAnchor())
            return false;

        if (hit.distance > 0.15f && !ShouldTriggerSlingshotFromContact(hit.normal, vel))
            return false;

        TriggerImpactSlingshot(null);
        return true;
    }

    private bool IsStuckOnObstacle()
    {
        if (!IsBodyBlocked(_rb.position))
            return false;

        if (IsGroundAnchor())
        {
            if (Flatten(BodyLinearVelocity).magnitude >= 2.5f)
                return false;

            if (TryGetGroundContact(out Vector3 n, out _) && n.y >= GroundNormalThreshold)
                return false;
        }

        Vector3 lifted = _rb.position;
        if (TryLiftFromTerrain(ref lifted) && lifted.y - _rb.position.y < 0.3f)
            return false;

        return true;
    }

    private bool IsImpactSlingshotCollision(Collision collision, bool allowLowSpeedStay)
    {
        if (collision == null || collision.contactCount == 0)
            return false;

        Vector3 relVel = collision.relativeVelocity;
        if (allowLowSpeedStay && relVel.sqrMagnitude < _impactSlingshotMinIntoSpeed * _impactSlingshotMinIntoSpeed)
            return false;

        int count = collision.contactCount;
        for (int i = 0; i < count; i++)
        {
            ContactPoint contact = collision.GetContact(i);
            if (ShouldTriggerSlingshotFromContact(contact.normal, relVel))
                return true;
        }

        return false;
    }

    // スリングショット接触判定は純関数 WireRopePhysics へ委譲（テスト可能・薄いラッパ）。
    private bool ShouldTriggerSlingshotFromContact(Vector3 normal, Vector3 approachVelocity)
        => WireRopePhysics.ShouldTriggerSlingshot(normal, approachVelocity, IsGroundAnchor(), Cfg);

    private bool IsFloorSlingshotContact(Vector3 normal, Vector3 approachVelocity)
        => WireRopePhysics.IsFloorSlingshotContact(normal, approachVelocity, IsGroundAnchor(), Cfg);

    private bool IsObstacleContactNormal(Vector3 normal, Vector3 approachVelocity)
        => WireRopePhysics.IsObstacleContactNormal(normal, approachVelocity, Cfg);

    private bool HasMoveInput()
    {
        Vector2 move = InputStateReader.ReadMoveVectorRaw(_inputSlot);
        return move.sqrMagnitude > 0.04f;
    }

    private void ApplyRetrievePlayerSteering()
    {
        Vector2 move = InputStateReader.ReadMoveVectorRaw(_inputSlot);
        if (move.sqrMagnitude < 0.04f) return;

        Vector3 wish = transform.right * move.x + transform.forward * move.y;
        if (IsGroundAnchor())
            wish.y = 0f;

        if (wish.sqrMagnitude < 0.01f)
            return;

        if (_retrievePullAxis.sqrMagnitude > 0.01f)
        {
            Vector3 pull = IsGroundAnchor() ? Flatten(_retrievePullAxis) : _retrievePullAxis;
            wish = Vector3.Slerp(wish.normalized, pull.normalized, 0.55f);
        }

        float steerMul = _ropeReleased ? 1f : 0.65f;
        AddBodyForce(wish.normalized * (_retrieveSteerAccel * steerMul), ForceMode.Acceleration);
    }

    private void OnCollisionEnter(Collision collision) =>
        TryTriggerImpactSlingshotFromCollision(collision, allowLowSpeedStay: false);

    private void OnCollisionStay(Collision collision) =>
        TryTriggerImpactSlingshotFromCollision(collision, allowLowSpeedStay: true);

    private void TryTriggerImpactSlingshotFromCollision(Collision collision, bool allowLowSpeedStay)
    {
        if (_phase != WireRopePhase.Retrieving)
            return;
        if (collision == null || collision.transform.IsChildOf(transform))
            return;
        if (collision.collider != null && collision.collider.isTrigger)
            return;

        if (IsGroundAnchor() && collision.contactCount > 0)
        {
            bool onlyFloor = true;
            for (int i = 0; i < collision.contactCount; i++)
            {
                Vector3 n = collision.GetContact(i).normal;
                if (n.y < GroundNormalThreshold)
                {
                    onlyFloor = false;
                    break;
                }
            }

            if (onlyFloor)
                return;
        }

        if (!IsImpactSlingshotCollision(collision, allowLowSpeedStay))
            return;

        TriggerImpactSlingshot(collision);
    }

    private void ResolveEmbeddedPosition(ref Vector3 pos)
    {
        if (TryLiftFromTerrain(ref pos) && !IsBodyBlocked(pos))
            return;

        for (int i = 0; i < _depenetrateIterations; i++)
        {
            if (!IsBodyBlocked(pos))
                return;

            if (TryDepenetrateStep(ref pos))
                continue;

            if (TryLiftFromTerrain(ref pos))
                continue;

            if (TryFindSafeStandPosition(pos, out Vector3 safe))
            {
                pos = safe;
                if (!IsBodyBlocked(pos))
                    return;
            }

            pos += (IsGroundAnchor() ? Vector3.up : _anchorNormal) * 0.25f;
        }

        TryLiftFromTerrain(ref pos);
    }

    /// <summary>
    /// 手続き地形は非凸 MeshCollider のため ComputePenetration が効かないことが多い。
    /// 地表レイでカプセル中心の最低高度を保証する。
    /// </summary>
    private bool TryLiftFromTerrain(ref Vector3 bodyPos)
    {
        if (!TrySampleGroundHeight(bodyPos, out float groundY, out _))
        {
            // 足元サンプル（+_groundSnapUp 起点）が届かない＝地表より深く潜っている可能性。
            // 高所から下キャストして体の直上にある面（地表）を探し、そこへ救済する。
            if (!TryFindSurfaceAboveBody(bodyPos, out groundY))
                return false; // 上にも面が無い＝空中（通常落下中）→触らない
        }

        float minCenterY = groundY + GetBodyHalfHeight() + GroundClearance;
        if (bodyPos.y >= minCenterY - 0.02f)
            return false;

        bodyPos.y = minCenterY;
        return true;
    }

    /// <summary>
    /// 体が地表より深く潜って足元サンプルが届かないときの救済。高所から下キャストし、
    /// 体中心より「上」にある最も低い面（＝直上の地表）の高さを返す。空中なら false。
    /// 構造物の屋根などに吸着しないよう、体より上で最も近い面のみ採用する。
    /// </summary>
    private bool TryFindSurfaceAboveBody(Vector3 bodyPos, out float surfaceY)
    {
        surfaceY = bodyPos.y;
        Vector3 origin = new Vector3(bodyPos.x, bodyPos.y + DeepRecoverySampleUp, bodyPos.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _rayHits,
            DeepRecoverySampleUp + 8f, _collisionMask, QueryTriggerInteraction.Ignore);

        float lowestAbove = float.MaxValue;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            var h = _rayHits[i];
            if (h.collider == null || h.collider.transform.IsChildOf(transform)) continue;
            if (h.point.y <= bodyPos.y + 0.05f) continue; // 体より上の面だけ採用
            if (h.point.y < lowestAbove)
            {
                lowestAbove = h.point.y;
                found = true;
            }
        }

        if (found) surfaceY = lowestAbove;
        return found;
    }

    /// <summary>
    /// 地形へのめり込みを補正する。浅い誤差では速度を弱く押し上げるだけ（重力感を残す）が、
    /// 深いめり込み（または足元サンプルが届かない＝地表下へ潜行）は速度に関係なく即スナップし、
    /// 地面すり抜け→奈落落下を防ぐ。地面アンカーに限らず全モードで毎物理ステップ実行する
    /// （壁・坂フックで地面へ押し込まれるケースも救済するため）。
    /// </summary>
    private void CorrectGroundPenetrationOnly()
    {
        if (!CanDriveRigidbody)
            return;

        Vector3 pos = _rb.position;
        bool nearFound = TrySampleGroundHeight(pos, out float groundY, out _);
        bool deepBelow = false;
        if (!nearFound)
        {
            // 足元（+_groundSnapUp）で見つからない＝地表より深く潜っている可能性。直上の面を探す。
            deepBelow = TryFindSurfaceAboveBody(pos, out groundY);
            if (!deepBelow)
                return; // 上にも下にも面が無い＝空中（通常落下）→触らない
        }

        float minCenterY = groundY + GetBodyHalfHeight() + GroundClearance;
        float penetration = minCenterY - pos.y;
        if (penetration <= GroundPenetrationCorrectThreshold)
            return;

        Vector3 vel = BodyLinearVelocity;
        float planarSpeed = Flatten(vel).magnitude;

        if (vel.y < 0f)
        {
            float bleed = planarSpeed >= 4f ? _groundImpactBleed : 0.45f;
            float vy = vel.y * bleed + penetration * 2.5f;
            SetBodyLinearVelocity(new Vector3(vel.x, vy, vel.z));
        }

        // 深い潜行／地表下は速度に関係なく即スナップ（沈下を毎ステップ打ち切り、復帰不能深度に達するのを防ぐ）。
        // 浅い潜りは低速時のみスナップ（高速スライドのスムーズさを保つ）。
        bool snap = deepBelow
                    || penetration > HardSnapPenetrationDepth
                    || (penetration > _softGroundPenetrationDepth && planarSpeed < 3f);
        if (snap)
        {
            pos.y = minCenterY;
            SnapBodyPositionOnly(pos);
            Vector3 v = BodyLinearVelocity;
            if (v.y < 0f)
            {
                v.y = 0f;
                SetBodyLinearVelocity(v);
            }
        }
    }

    private Vector3 ProjectBodyCenterOntoGround(Vector3 xzHint, float extraLift)
    {
        if (TrySampleGroundHeight(xzHint, out float groundY, out _))
            return new Vector3(xzHint.x, groundY + GetBodyHalfHeight() + GroundClearance + extraLift, xzHint.z);

        return new Vector3(xzHint.x, xzHint.y + GetBodyHalfHeight(), xzHint.z);
    }

    private float GetBodyHalfHeight() =>
        _bodyCapsule != null ? _bodyCapsule.height * 0.5f : 0.9f;

    private bool TrySampleGroundHeight(Vector3 near, out float groundY, out RaycastHit bestHit)
    {
        groundY = near.y;
        bestHit = default;

        Vector3 origin = new Vector3(near.x, near.y + _groundSnapUp, near.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, _rayHits, _groundSnapUp * 3f, _collisionMask, QueryTriggerInteraction.Ignore);
        if (count == 0)
            return false;

        float bestY = float.MinValue;
        bool found = false;

        for (int i = 0; i < count; i++)
        {
            var h = _rayHits[i];
            if (h.collider == null) continue;
            if (h.collider.transform.IsChildOf(transform)) continue;
            if (h.point.y > bestY)
            {
                bestY = h.point.y;
                bestHit = h;
                found = true;
            }
        }

        if (!found) return false;
        groundY = bestY;
        return true;
    }

    private void SnapBodyPositionOnly(Vector3 pos)
    {
        _rb.position = pos;
        transform.position = pos;
        Physics.SyncTransforms();
    }

    private void GetCapsuleEnds(Vector3 bodyCenter, out Vector3 top, out Vector3 bottom, out float radius)
    {
        radius = _bodyCapsule != null ? _bodyCapsule.radius * 0.92f : 0.35f;
        float height = _bodyCapsule != null ? Mathf.Max(_bodyCapsule.height, radius * 2f) : 1.8f;
        Vector3 up = transform.up;
        top = bodyCenter + up * (height * 0.5f - radius);
        bottom = bodyCenter - up * (height * 0.5f - radius);
    }

    private bool TryDepenetrateStep(ref Vector3 bodyPos)
    {
        if (_bodyCapsule == null) return false;

        GetCapsuleEnds(bodyPos, out Vector3 p1, out Vector3 p2, out float radius);
        var overlaps = Physics.OverlapCapsule(p1, p2, radius, _collisionMask, QueryTriggerInteraction.Ignore);
        bool pushed = false;

        foreach (var col in overlaps)
        {
            if (col == null || col.transform.IsChildOf(transform)) continue;

            if (!Physics.ComputePenetration(
                    _bodyCapsule, bodyPos, transform.rotation,
                    col, col.transform.position, col.transform.rotation,
                    out Vector3 dir, out float dist))
                continue;

            bodyPos += dir * (dist + 0.06f);
            pushed = true;
        }

        return pushed;
    }

    private bool TryFindSafeStandPosition(Vector3 near, out Vector3 safePos)
    {
        safePos = near;
        float halfHeight = _bodyCapsule != null ? _bodyCapsule.height * 0.5f : 0.9f;

        Vector3 away = _anchorNormal.sqrMagnitude > 0.01f ? _anchorNormal : Vector3.up;
        Vector3 tangent = Vector3.Cross(away, Vector3.up);
        if (tangent.sqrMagnitude < 0.01f)
            tangent = Vector3.Cross(away, Vector3.forward);
        tangent.Normalize();
        Vector3 bitangent = Vector3.Cross(away, tangent).normalized;

        float[] radii = { 0f, 0.6f, 1.2f, 2f, 3f };
        float[] angles = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        foreach (float r in radii)
        {
            foreach (float deg in angles)
            {
                float rad = deg * Mathf.Deg2Rad;
                Vector3 offset = away * (_standOffFromSurface + r * 0.35f)
                                 + (tangent * Mathf.Cos(rad) + bitangent * Mathf.Sin(rad)) * r;
                Vector3 probe = _anchorPoint + offset;
                if (!TrySampleGroundHeight(probe, out float groundY, out _))
                    continue;

                Vector3 stand = new Vector3(probe.x, groundY + halfHeight + GroundClearance, probe.z);
                if (!IsBodyBlocked(stand))
                {
                    safePos = stand;
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsBodyBlocked(Vector3 center)
    {
        if (_bodyCapsule == null) return false;
        GetCapsuleEnds(center, out Vector3 p1, out Vector3 p2, out float radius);
        return Physics.CheckCapsule(p1, p2, radius, _collisionMask, QueryTriggerInteraction.Ignore);
    }

    private void CompleteRetrieve()
    {
        if (!_ropeReleased)
            GameServices.Audio?.PlaySE(SoundId.RopeCut, transform.position);
        ResetToReady();
    }

    private void ResetToReady()
    {
        SetPhase(WireRopePhase.Ready);
        _forceGauge = 0f;
        _retrieveTimer = 0f;
        _ropeReleased = false;
        _anchorIsGround = false;
        _retrieveTensionBlend = 0f;
        _pullStallTimer = 0f;
        _overshootTimer = 0f;
        _impactBoostTimer = 0f;
        _lastImpactSlingshotTime = -999f;
        _retrieveUsesRaisedGroundPull = false;
        _visualSagBlend = 0f;
        _tensionSoundTimer = 0f;
        SetRetrieveSlideFriction(false);
        GameServices.Audio?.StopLoop(SoundId.WinchLoop);

        RestoreTargetBodyState();
        RestoreSuppressedTargetController();
        _pullTargetToPlayer = false;
        _attachedTargetBody = null;
        _attachedTargetTransform = null;

        RestoreRigidbodyMotorState();
        _rb.WakeUp();

        _renderer.ClearHook();
        _renderer.SetVisible(false);
    }

    private Vector3 GetHandOrigin()
    {
        if (_handOrigin != null && _handOrigin != _aimTransform)
            return _handOrigin.position;

        var aim = AimTransform;
        return aim.position + aim.forward * 0.35f + aim.right * 0.32f + Vector3.down * 0.2f;
    }

    private Vector3 GetAimDirection()
    {
        if (_aimTransform == null) return transform.forward;
        return _aimTransform.forward.normalized;
    }

    private void ResolveHookMask()
    {
        int mask = _hookableLayers.value;
        if (mask != 0) return;

        mask = Physics.DefaultRaycastLayers;
        TryAddLayer(ref mask, "Grappable");
        TryAddLayer(ref mask, "Ground");
        TryAddLayer(ref mask, "Default");

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            mask &= ~(1 << playerLayer);

        _hookableLayers = mask;
    }

    private static void TryAddLayer(ref int mask, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer >= 0)
            mask |= 1 << layer;
    }

    private void OnDisable()
    {
        if (_throwRoutine != null)
        {
            StopCoroutine(_throwRoutine);
            _throwRoutine = null;
        }

        if (_phase != WireRopePhase.Ready)
            ResetToReady();
    }

    private void OnDrawGizmosSelected()
    {
        if (_phase != WireRopePhase.Attached && _phase != WireRopePhase.Retrieving) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(_anchorPoint, 0.25f);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(_retrieveStopPoint.sqrMagnitude > 0.01f ? _retrieveStopPoint : ComputeRetrieveStopPoint(), 0.2f);
    }
}
