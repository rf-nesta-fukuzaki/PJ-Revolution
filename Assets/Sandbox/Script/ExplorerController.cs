using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// Sandbox シーン用 Explorer WASD 移動 + Sprint + Jump コントローラー。
/// Animator パラメーター（GDD §16.2 完全準拠）:
///   float   Speed       (0-1) : State Speed Multiplier。0=凍結(Idle再現)、1=通常再生
///   float   SpeedBlend  (0-1) : Blend Tree 選択。0=Walk、1=Run
///   float   MoveSpeed    m/s  : 実測移動速度（Walk/Run 判定用・GDD §16.2）
///   trigger JumpTrigger       : Jump ステートへの遷移
///   bool    IsGrounded        : 接地状態（Jump→MovementBT 戻り条件）
///   bool    IsCrouching       : しゃがみフラグ（GDD §16.2）
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ExplorerController : MonoBehaviour
{
    [Header("移動速度")]
    [SerializeField] private float _walkSpeed   = 5f;
    [SerializeField] private float _sprintSpeed = 10f;

    [Header("ジャンプ")]
    [SerializeField] private float _jumpForce = 5.9f;

    [Header("空中制御 (GDD §5.5)")]
    [Tooltip("滑落/落下中の水平入力で到達可能な最大速度 (m/s)")]
    [SerializeField] private float _airControlMaxSpeed = 1.5f;
    [Tooltip("空中水平制御の加速度 (m/s²)")]
    [SerializeField] private float _airControlAcceleration = 6f;

    [Header("接地判定")]
    [Tooltip("Rigidbody 中心からの下向きオフセット（CapsuleCollider の底面に合わせて調整）")]
    [SerializeField] private float     _groundCheckOffsetY = -0.9f;
    [SerializeField] private float     _groundCheckRadius  = 0.3f;
    [SerializeField] private LayerMask _groundLayer;
    [SerializeField] private float     _groundedGraceTime  = 0.1f;

    [Header("アニメーション補間")]
    [SerializeField] private float _animSmoothTime  = 0.1f;
    [SerializeField] private float _blendSmoothTime = 0.1f;

    [Header("スタミナ (GDD §3.1)")]
    [Tooltip("空なら同 GameObject から自動取得。スプリント中の消費・疲労ゲートに使用。")]
    [SerializeField] private StaminaSystem _stamina;

    // GDD §15.2 — LandHard 判定閾値（落下距離 3m 以上で Hard）
    private const float HARD_LAND_DROP_METERS = 3f;

    // GDD §15.2 — Footstep SE 設定
    private const float FOOTSTEP_INTERVAL_WALK   = 0.50f;   // 歩き
    private const float FOOTSTEP_INTERVAL_RUN    = 0.32f;   // ダッシュ
    private const float FOOTSTEP_INTERVAL_CROUCH = 0.75f;   // しゃがみ
    private const float FOOTSTEP_MIN_SPEED       = 0.5f;    // この実速度未満は鳴らさない

    // GDD §15.2 — SlideFall（滑落 SE）の立ち上がりエッジ用
    private const float SLIDE_FALL_VY_THRESHOLD = -8f;
    private bool  _wasSlideFalling;
    private float _footstepTimer;

    private Rigidbody _rb;
    private Animator  _animator;

    private Vector3 _moveInput;
    private bool    _isSprinting;
    private bool    _jumpRequested;
    private bool    _isGrounded;
    private bool    _isGroundedRaw;
    private bool    _wasGrounded;
    private float   _peakAirY;
    private float   _lastGroundedTime = -999f;

    private float _currentSpeed;
    private float _speedVelocity;
    private float _currentBlend;
    private float _blendVelocity;

    // ── 外部から注入される速度ペナルティ（0 = なし / 0.3 = -30%） ──
    private float _altitudePenalty;    // AltitudeSicknessEffect から設定
    private float _carryPenalty;       // RelicCarrier から設定（運搬重量依存）

    /// <summary>高山病による速度ペナルティを設定する（GDD §3.4）。</summary>
    public void SetAltitudePenalty(float penalty) => _altitudePenalty = Mathf.Clamp01(penalty);

    /// <summary>運搬重量による速度ペナルティを設定する（GDD §3.3）。</summary>
    public void SetCarryPenalty(float penalty)    => _carryPenalty    = Mathf.Clamp01(penalty);

    /// <summary>現在スプリント（ダッシュ）入力中か（GDD §6.2 カメラモード判定用）。</summary>
    public bool IsSprinting => _isSprinting;

    private static readonly int SpeedHash       = Animator.StringToHash("Speed");
    private static readonly int SpeedBlendHash  = Animator.StringToHash("SpeedBlend");
    private static readonly int MoveSpeedHash   = Animator.StringToHash("MoveSpeed");
    private static readonly int JumpTriggerHash = Animator.StringToHash("JumpTrigger");
    private static readonly int IsGroundedHash  = Animator.StringToHash("IsGrounded");
    private static readonly int IsCrouchingHash = Animator.StringToHash("IsCrouching");

    // しゃがみ状態（GDD §16.2 — EA 版は Animator パラメータのみ予約）
    private bool _isCrouching;
    public bool IsCrouching
    {
        get => _isCrouching;
        set
        {
            _isCrouching = value;
            _animator?.SetBool(IsCrouchingHash, value);
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _animator = GetComponentInChildren<Animator>();
        if (_stamina == null) _stamina = GetComponent<StaminaSystem>();

        ResolveGroundMask();
    }

    private void Update()
    {
        // 接地判定
        Vector3 sphereOrigin = _rb.position + Vector3.up * _groundCheckOffsetY;
        _isGroundedRaw = Physics.CheckSphere(
            sphereOrigin,
            _groundCheckRadius,
            _groundLayer,
            QueryTriggerInteraction.Ignore);

        if (!_isGroundedRaw)
        {
            _isGroundedRaw = Physics.Raycast(
                _rb.position + Vector3.up * 0.1f,
                Vector3.down,
                Mathf.Abs(_groundCheckOffsetY) + _groundCheckRadius + 0.35f,
                _groundLayer,
                QueryTriggerInteraction.Ignore);
        }

        if (_isGroundedRaw)
            _lastGroundedTime = Time.time;

        _isGrounded = _isGroundedRaw || (Time.time - _lastGroundedTime) <= _groundedGraceTime;

        // GDD §15.2 — 着地 SE（land_soft / land_hard）
        UpdateLandingSound();

        // GDD §15.2 — 足音 SE（Walk / Run / Crouch）
        UpdateFootstepSound();

        // GDD §15.2 — 滑落 SE（SlideFall）
        UpdateSlideFallSound();

        // 移動入力
        Vector2 moveInput = InputStateReader.ReadMoveVectorRaw();
        float h = moveInput.x;
        float v = moveInput.y;
        _moveInput   = (transform.right * h + transform.forward * v).normalized;
        // GDD §3.1 — 疲労中はスプリント不可。移動入力があり、接地中の時のみスタミナを消費する。
        _isSprinting = _moveInput.sqrMagnitude > 0.01f
                       && InputStateReader.IsSprintPressed()
                       && (_stamina == null || !_stamina.IsExhausted);
        if (_isSprinting && _isGrounded)
            _stamina?.ConsumeSprint();

        // ジャンプ入力（接地時のみ受付）
        if (InputStateReader.JumpPressedThisFrame() && _isGrounded)
            _jumpRequested = true;

        // Animator: Speed（State Speed Multiplier）
        float targetSpeed = _moveInput.sqrMagnitude > 0.01f ? 1f : 0f;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed,
                                          ref _speedVelocity, _animSmoothTime);
        _animator?.SetFloat(SpeedHash, _currentSpeed);

        // Animator: SpeedBlend（Walk / Run ブレンド）
        float targetBlend = _isSprinting ? 1f : 0f;
        _currentBlend = Mathf.SmoothDamp(_currentBlend, targetBlend,
                                          ref _blendVelocity, _blendSmoothTime);
        _animator?.SetFloat(SpeedBlendHash, _currentBlend);

        // Animator: IsGrounded
        _animator?.SetBool(IsGroundedHash, _isGrounded);

        // Animator: MoveSpeed（m/s 実測値・GDD §16.2 Walk/Run BlendTree）
        Vector3 planarVel = _rb.linearVelocity;
        planarVel.y = 0f;
        _animator?.SetFloat(MoveSpeedHash, planarVel.magnitude);
    }

    /// <summary>
    /// GDD §15.2 — 着地時に <see cref="SoundId.LandSoft"/> / <see cref="SoundId.LandHard"/> を再生する。
    /// 空中にいる間のピーク Y を記録し、接地した瞬間の落差で判定する。
    /// </summary>
    private void UpdateLandingSound()
    {
        if (!_isGrounded)
        {
            // 空中中は最高点を記録（ジャンプ上昇後の下降開始点）
            if (_rb.position.y > _peakAirY) _peakAirY = _rb.position.y;
        }
        else if (!_wasGrounded)
        {
            // 着地フレーム
            float drop = Mathf.Max(0f, _peakAirY - _rb.position.y);
            var id    = drop >= HARD_LAND_DROP_METERS ? SoundId.LandHard : SoundId.LandSoft;
            PPAudioManager.Instance?.PlaySE(id, _rb.position);
            _peakAirY = _rb.position.y;
        }
        else
        {
            // 接地継続中はピーク値を現在位置にリセット
            _peakAirY = _rb.position.y;
        }
        _wasGrounded = _isGrounded;
    }

    /// <summary>
    /// GDD §15.2 — 接地して動いている間、状態に応じた足音 SE を一定間隔で鳴らす。
    /// スプリント > しゃがみ > 歩きの優先で間隔を決定する。
    /// </summary>
    private void UpdateFootstepSound()
    {
        if (!_isGrounded)
        {
            _footstepTimer = 0f;
            return;
        }

        Vector3 planarVel = _rb.linearVelocity;
        planarVel.y = 0f;
        if (planarVel.magnitude < FOOTSTEP_MIN_SPEED)
        {
            _footstepTimer = 0f;
            return;
        }

        _footstepTimer -= Time.deltaTime;
        if (_footstepTimer > 0f) return;

        SoundId id;
        if (_isCrouching)
        {
            id              = SoundId.FootstepCrouch;
            _footstepTimer  = FOOTSTEP_INTERVAL_CROUCH;
        }
        else if (_isSprinting)
        {
            id              = SoundId.FootstepRun;
            _footstepTimer  = FOOTSTEP_INTERVAL_RUN;
        }
        else
        {
            id              = SoundId.FootstepWalk;
            _footstepTimer  = FOOTSTEP_INTERVAL_WALK;
        }

        PPAudioManager.Instance?.PlaySE(id, _rb.position);
    }

    /// <summary>
    /// GDD §15.2 — 空中で下方向に一定速度を超えた瞬間（立ち上がりエッジ）に slide_fall を鳴らす。
    /// 接地または上昇に戻った時点でフラグをリセット。
    /// </summary>
    private void UpdateSlideFallSound()
    {
        bool falling = !_isGrounded && _rb.linearVelocity.y < SLIDE_FALL_VY_THRESHOLD;
        if (falling && !_wasSlideFalling)
            PPAudioManager.Instance?.PlaySE(SoundId.SlideFall, _rb.position);
        _wasSlideFalling = falling;
    }

    private void FixedUpdate()
    {
        // ジャンプ（物理フレームで実行）
        if (_jumpRequested)
        {
            _jumpRequested = false;
            // _jumpForce は「力」ではなく上向き初速(m/s)として扱う。
            Vector3 vel = _rb.linearVelocity;
            vel.y = Mathf.Max(0f, vel.y);
            vel.y = _jumpForce;
            _rb.linearVelocity = vel;
            _isGrounded = false;
            _isGroundedRaw = false;
            _lastGroundedTime = -999f;
            _animator?.SetBool(IsGroundedHash, false);
            _animator?.SetTrigger(JumpTriggerHash);
            PPAudioManager.Instance?.PlaySE(SoundId.Jump, _rb.position);
        }

        // 水平移動（速度ペナルティを合算 — GDD §3.3/3.4）
        if (_moveInput.sqrMagnitude < 0.01f) return;

        if (_isGrounded)
        {
            // 接地時: 既存挙動（ペナルティ適用・直接 MovePosition）
            float baseSpeed = _isSprinting ? _sprintSpeed : _walkSpeed;
            float totalPenalty = Mathf.Clamp01(_altitudePenalty + _carryPenalty);
            // 最低速度 1.0m/s を保証（GDD §3.3）
            float speed = Mathf.Max(1.0f, baseSpeed * (1f - totalPenalty));
            _rb.MovePosition(_rb.position + _moveInput * (speed * Time.fixedDeltaTime));
        }
        else
        {
            // GDD §5.5 step 3 — 空中は水平 1.5m/s 制限の緩やかな制御のみ許可。
            // 重力を殺さないよう AddForce + 水平速度 Clamp で実装。
            Vector3 vel      = _rb.linearVelocity;
            Vector3 planar   = new Vector3(vel.x, 0f, vel.z);
            Vector3 desired  = _moveInput * _airControlMaxSpeed;
            Vector3 delta    = desired - planar;
            float   maxDV    = _airControlAcceleration * Time.fixedDeltaTime;
            if (delta.magnitude > maxDV) delta = delta.normalized * maxDV;
            Vector3 newPlanar = planar + delta;

            // 水平速度が上限を超えないよう最終クランプ
            if (newPlanar.magnitude > _airControlMaxSpeed)
                newPlanar = newPlanar.normalized * _airControlMaxSpeed;

            _rb.linearVelocity = new Vector3(newPlanar.x, vel.y, newPlanar.z);
        }
    }

    private void ResolveGroundMask()
    {
        int mask = _groundLayer.value;
        if (mask == 0)
            mask = Physics.DefaultRaycastLayers;

        int defaultLayer = LayerMask.NameToLayer("Default");
        if (defaultLayer >= 0)
            mask |= 1 << defaultLayer;

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            mask |= 1 << groundLayer;

        for (int layer = 0; layer < 32; layer++)
        {
            if (LayerMask.LayerToName(layer) == "Player")
                mask &= ~(1 << layer);
        }

        _groundLayer = mask;
    }
}
