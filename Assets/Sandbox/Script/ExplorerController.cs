using UnityEngine;

/// <summary>
/// Sandbox シーン用 Explorer WASD 移動 + Sprint + Jump コントローラー。
/// Animator パラメーター:
///   float   Speed       (0-1) : State Speed Multiplier。0=凍結(Idle再現)、1=通常再生
///   float   SpeedBlend  (0-1) : Blend Tree 選択。0=Walk、1=Run
///   trigger JumpTrigger       : Jump ステートへの遷移
///   bool    IsGrounded        : 接地状態（Jump→MovementBT 戻り条件）
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ExplorerController : MonoBehaviour
{
    [Header("移動速度")]
    [SerializeField] private float _walkSpeed   = 5f;
    [SerializeField] private float _sprintSpeed = 10f;

    [Header("ジャンプ")]
    [SerializeField] private float _jumpForce = 6f;

    [Header("接地判定")]
    [Tooltip("Rigidbody 中心からの下向きオフセット（CapsuleCollider の底面に合わせて調整）")]
    [SerializeField] private float     _groundCheckOffsetY = -0.9f;
    [SerializeField] private float     _groundCheckRadius  = 0.3f;
    [SerializeField] private LayerMask _groundLayer;

    [Header("アニメーション補間")]
    [SerializeField] private float _animSmoothTime  = 0.1f;
    [SerializeField] private float _blendSmoothTime = 0.1f;

    private Rigidbody _rb;
    private Animator  _animator;

    private Vector3 _moveInput;
    private bool    _isSprinting;
    private bool    _jumpRequested;
    private bool    _isGrounded;

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

    private static readonly int SpeedHash       = Animator.StringToHash("Speed");
    private static readonly int SpeedBlendHash  = Animator.StringToHash("SpeedBlend");
    private static readonly int JumpTriggerHash = Animator.StringToHash("JumpTrigger");
    private static readonly int IsGroundedHash  = Animator.StringToHash("IsGrounded");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        // 接地判定
        Vector3 origin = _rb.position + Vector3.up * _groundCheckOffsetY;
        _isGrounded = Physics.CheckSphere(origin, _groundCheckRadius,
                                          _groundLayer, QueryTriggerInteraction.Ignore);

        // 移動入力
        Vector2 moveInput = InputStateReader.ReadMoveVectorRaw();
        float h = moveInput.x;
        float v = moveInput.y;
        _moveInput   = (transform.right * h + transform.forward * v).normalized;
        _isSprinting = _moveInput.sqrMagnitude > 0.01f && InputStateReader.IsSprintPressed();

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
    }

    private void FixedUpdate()
    {
        // ジャンプ（物理フレームで実行）
        if (_jumpRequested)
        {
            _jumpRequested = false;
            // Y 速度をリセットしてから AddForce（着地直後の残留速度を除去）
            Vector3 vel = _rb.linearVelocity;
            vel.y = 0f;
            _rb.linearVelocity = vel;
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _animator?.SetTrigger(JumpTriggerHash);
        }

        // 水平移動（速度ペナルティを合算 — GDD §3.3/3.4）
        if (_moveInput.sqrMagnitude < 0.01f) return;
        float baseSpeed = _isSprinting ? _sprintSpeed : _walkSpeed;
        float totalPenalty = Mathf.Clamp01(_altitudePenalty + _carryPenalty);
        // 最低速度 1.0m/s を保証（GDD §3.3）
        float speed = Mathf.Max(1.0f, baseSpeed * (1f - totalPenalty));
        _rb.MovePosition(_rb.position + _moveInput * (speed * Time.fixedDeltaTime));
    }
}
