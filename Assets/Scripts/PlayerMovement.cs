using UnityEngine;

/// <summary>
/// Rigidbody ベースのプレイヤー移動。慣性・段差・スロープ対応。
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    private const float IdleGroundFriction = 0.8f;
    private const float MovingGroundFriction = 0.1f;

    [Header("Movement")]
    [SerializeField] public float moveSpeed = 5f;
    [SerializeField] public float acceleration = 20f;
    [SerializeField] public float deceleration = 15f;
    [SerializeField] public float airControlFactor = 0.3f;

    [Header("Jump")]
    [SerializeField] public float jumpForce = 6f;
    [SerializeField] public float coyoteTime = 0.12f;
    [SerializeField] public float jumpBufferTime = 0.1f;
    [SerializeField] public float fallMultiplier = 2.5f;
    [SerializeField] public float lowJumpMultiplier = 2f;

    [Header("Ground Check")]
    [SerializeField] public float groundCheckRadius = 0.3f;
    [SerializeField] public LayerMask groundLayer;

    [Header("Slope")]
    [SerializeField] public float maxSlopeAngle = 45f;

    [Header("Step Climb")]
    [SerializeField] public float stepHeight = 0.4f;
    [SerializeField] public float stepSmooth = 0.1f;

    // 公開プロパティ（FirstPersonLook から参照）
    public float SmoothStepOffset { get; private set; }
    public bool IsGrounded { get; private set; }
    public bool IsSwinging { get; set; }

    private Rigidbody _rb;
    private CapsuleCollider _col;
    private Vector2 _moveInput;
    private bool _jumpHeld;
    private float _coyoteTimer;
    private float _jumpBufferTimer;
    private float _stepOffsetTarget;
    private Vector3 _groundNormal = Vector3.up;
    private PhysicsMaterial _runtimePhysicMaterial;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<CapsuleCollider>();
        _rb.freezeRotation = true;

        if (groundLayer == 0)
            groundLayer = ~(1 << gameObject.layer);

        _runtimePhysicMaterial = new PhysicsMaterial($"{nameof(PlayerMovement)}RuntimeFriction")
        {
            bounciness = 0f,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        _col.material = _runtimePhysicMaterial;
    }

    // ─── 公開 API（PlayerInputController から呼ぶ） ───

    public void Move(Vector2 input)
    {
        _moveInput = input;
    }

    public void Jump()
    {
        _jumpBufferTimer = jumpBufferTime;
    }

    public void JumpRelease()
    {
        _jumpHeld = false;
    }

    public void JumpHold()
    {
        _jumpHeld = true;
    }

    // FirstPersonLook の段差補正互換（旧 API 対応）
    public float ConsumeStepOffset()
    {
        float v = SmoothStepOffset;
        return v;
    }

    public float GetCrouchCameraY() => 0f;

    // ─── Unity Lifecycle ───

    private void FixedUpdate()
    {
        CheckGround();
        UpdateFrictionMaterial();
        HandleMovement();
        HandleJump();
        HandleGravityModifier();
        HandleStepClimb();

        // タイマー更新
        if (_coyoteTimer > 0f) _coyoteTimer -= Time.fixedDeltaTime;
        if (_jumpBufferTimer > 0f) _jumpBufferTimer -= Time.fixedDeltaTime;

        // SmoothStepOffset を 0 に戻す
        SmoothStepOffset = Mathf.Lerp(SmoothStepOffset, 0f, Time.fixedDeltaTime * 10f);
    }

    private void CheckGround()
    {
        float halfHeight = _col.height / 2f - _col.radius;
        bool hit = Physics.SphereCast(
            transform.position,
            groundCheckRadius,
            Vector3.down,
            out RaycastHit hitInfo,
            halfHeight + 0.15f,
            groundLayer
        );

        IsGrounded = hit;
        if (hit)
        {
            _groundNormal = hitInfo.normal;
            _coyoteTimer = coyoteTime;
        }
        else
        {
            _groundNormal = Vector3.up;
        }
    }

    private void HandleMovement()
    {
        if (IsSwinging) return;

        // カメラ方向を基準にした移動ベクトル
        Transform cam = Camera.main?.transform;
        Vector3 forward = cam != null
            ? Vector3.ProjectOnPlane(cam.forward, Vector3.up).normalized
            : transform.forward;
        Vector3 right = cam != null
            ? Vector3.ProjectOnPlane(cam.right, Vector3.up).normalized
            : transform.right;

        Vector3 targetVelocity = (forward * _moveInput.y + right * _moveInput.x) * moveSpeed;

        Vector3 currentHoriz = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        float control = IsGrounded ? 1f : airControlFactor;
        float accel = targetVelocity.magnitude > 0.1f ? acceleration : deceleration;

        _rb.AddForce((targetVelocity - currentHoriz) * accel * control, ForceMode.Acceleration);
    }

    private void UpdateFrictionMaterial()
    {
        if (_runtimePhysicMaterial == null)
            return;

        if (!IsGrounded)
        {
            _runtimePhysicMaterial.dynamicFriction = 0f;
            _runtimePhysicMaterial.staticFriction = 0f;
            _runtimePhysicMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
            return;
        }

        bool hasMoveInput = _moveInput.sqrMagnitude > 0.01f;
        float friction = hasMoveInput ? MovingGroundFriction : IdleGroundFriction;

        _runtimePhysicMaterial.dynamicFriction = friction;
        _runtimePhysicMaterial.staticFriction = friction;
        _runtimePhysicMaterial.frictionCombine = hasMoveInput
            ? PhysicsMaterialCombine.Minimum
            : PhysicsMaterialCombine.Maximum;
    }

    private void HandleJump()
    {
        if (_jumpBufferTimer > 0f && _coyoteTimer > 0f)
        {
            Vector3 vel = _rb.linearVelocity;
            vel.y = jumpForce;
            _rb.linearVelocity = vel;
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _jumpHeld = true;
        }
    }

    private void HandleGravityModifier()
    {
        if (_rb.linearVelocity.y < -0.1f)
        {
            _rb.AddForce(Vector3.up * Physics.gravity.y * (fallMultiplier - 1f), ForceMode.Acceleration);
        }
        else if (_rb.linearVelocity.y > 0.1f && !_jumpHeld)
        {
            _rb.AddForce(Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    private void HandleStepClimb()
    {
        if (!IsGrounded || _moveInput.magnitude < 0.1f) return;

        Vector3 moveDir = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        if (moveDir.magnitude < 0.5f) return;
        moveDir.Normalize();

        Vector3 originLow = transform.position + Vector3.up * 0.05f;
        if (!Physics.Raycast(originLow, moveDir, 0.4f, groundLayer)) return;

        Vector3 originHigh = transform.position + Vector3.up * stepHeight;
        if (Physics.Raycast(originHigh, moveDir, 0.4f, groundLayer)) return;

        // 段差を乗り越える
        _rb.position += Vector3.up * stepSmooth;
        SmoothStepOffset -= stepSmooth;
    }

    private void OnDrawGizmosSelected()
    {
        var col = GetComponent<CapsuleCollider>();
        if (col == null) return;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(
            transform.position + Vector3.down * (col.height / 2f - col.radius),
            groundCheckRadius);
    }
}
