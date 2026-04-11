using UnityEngine;

/// <summary>
/// Rigidbody ベースのプレイヤー移動。慣性・段差・スロープ対応。
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerMovement : MonoBehaviour
{
    private const float IdleGroundFriction = 1.35f;
    private const float MovingGroundFriction = 0.08f;
    private const float GroundSnapForce = 80f;
    private const float AntiSlideDamping = 20f;

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
    private Vector3 _groundDownhill = Vector3.zero;
    private float _groundSlopeAngle;
    private PhysicsMaterial _runtimePhysicMaterial;

    private void Awake()
    {
        EnsureReferences();
    }

    private bool EnsureReferences()
    {
        _rb ??= GetComponent<Rigidbody>();
        _col ??= GetComponent<CapsuleCollider>();
        if (_rb == null || _col == null) return false;

        _rb.freezeRotation = true;
        if (groundLayer == 0)
            groundLayer = Physics.AllLayers;

        if (_runtimePhysicMaterial == null)
        {
            _runtimePhysicMaterial = new PhysicsMaterial($"{nameof(PlayerMovement)}RuntimeFriction")
            {
                bounciness = 0f,
                bounceCombine = PhysicsMaterialCombine.Minimum
            };
            _col.material = _runtimePhysicMaterial;
        }

        return true;
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
        if (!EnsureReferences()) return;

        CheckGround();
        UpdateFrictionMaterial();
        HandleMovement();
        HandleSlopeStickiness();
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
        float halfHeight = Mathf.Max(0f, _col.height * 0.5f - _col.radius);
        float probeRadius = Mathf.Min(groundCheckRadius, _col.radius * 0.95f);
        Vector3 center = transform.position + _col.center;
        Vector3 origin = center + Vector3.up * halfHeight;

        bool hit = Physics.SphereCast(
            origin,
            probeRadius,
            Vector3.down,
            out RaycastHit hitInfo,
            halfHeight + probeRadius + 0.12f,
            groundLayer,
            QueryTriggerInteraction.Ignore
        );

        IsGrounded = hit;
        if (hit)
        {
            _groundNormal = hitInfo.normal;
            _groundSlopeAngle = Vector3.Angle(_groundNormal, Vector3.up);
            Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, _groundNormal);
            _groundDownhill = downhill.sqrMagnitude > 0.0001f ? downhill.normalized : Vector3.zero;
            _coyoteTimer = coyoteTime;
        }
        else
        {
            _groundNormal = Vector3.up;
            _groundSlopeAngle = 0f;
            _groundDownhill = Vector3.zero;
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

        Vector3 desiredMove = forward * _moveInput.y + right * _moveInput.x;
        if (desiredMove.sqrMagnitude > 1f)
            desiredMove.Normalize();

        bool walkableSlope = IsGrounded && _groundSlopeAngle <= maxSlopeAngle;
        if (walkableSlope && desiredMove.sqrMagnitude > 0.0001f)
            desiredMove = Vector3.ProjectOnPlane(desiredMove, _groundNormal).normalized;

        Vector3 targetVelocity = desiredMove * moveSpeed;

        Vector3 currentHoriz = walkableSlope
            ? Vector3.ProjectOnPlane(_rb.linearVelocity, _groundNormal)
            : new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
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

        bool walkableSlope = _groundSlopeAngle <= maxSlopeAngle;
        bool hasMoveInput = _moveInput.sqrMagnitude > 0.01f;

        float friction = walkableSlope
            ? (hasMoveInput ? MovingGroundFriction : IdleGroundFriction)
            : 0.15f;

        _runtimePhysicMaterial.dynamicFriction = friction;
        _runtimePhysicMaterial.staticFriction = friction;
        if (!walkableSlope)
        {
            _runtimePhysicMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
            return;
        }

        _runtimePhysicMaterial.frictionCombine = hasMoveInput
            ? PhysicsMaterialCombine.Minimum
            : PhysicsMaterialCombine.Maximum;
    }

    private void HandleSlopeStickiness()
    {
        if (!IsGrounded) return;
        if (_groundSlopeAngle <= 0.01f || _groundSlopeAngle > maxSlopeAngle) return;

        _rb.AddForce(-_groundNormal * GroundSnapForce, ForceMode.Acceleration);

        if (_moveInput.sqrMagnitude > 0.01f) return;
        if (_groundDownhill.sqrMagnitude < 0.0001f) return;

        float downhillSpeed = Vector3.Dot(_rb.linearVelocity, _groundDownhill);
        if (downhillSpeed <= 0f) return;

        _rb.AddForce(-_groundDownhill * downhillSpeed * AntiSlideDamping, ForceMode.Acceleration);

        // 入力していないときは下り成分を打ち消してズリ落ちを抑える。
        Vector3 velocity = _rb.linearVelocity;
        velocity -= _groundDownhill * downhillSpeed;
        _rb.linearVelocity = velocity;
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
        _rb.MovePosition(_rb.position + Vector3.up * stepSmooth);
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
