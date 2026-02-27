using UnityEngine;

/// <summary>
/// Rigidbody を使った物理ベースのプレイヤー移動を担当する。
/// 入力の受付は PlayerInputController から Move() / Jump() を呼び出すことで行う。
///
/// [改修点 - ステートマシン対応]
///   - PlayerStateManager.CurrentState による CanMove に統合。
///   - ジャンプ後 jumpCooldownDuration 秒間は接地判定をスキップ（崖際でのズレ防止）。
///   - 接地判定の球半径を CapsuleCollider.radius * 0.9f に自動調整。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移動設定")]
    [Tooltip("プレイヤーの最大移動速度 (m/s)")]
    [SerializeField] private float moveSpeed = 5f;

    [Tooltip("ジャンプ時の速度変化量 (VelocityChange モード)")]
    [SerializeField] private float jumpForce = 8f;

    [Header("接地判定")]
    [Tooltip("地面と判定するレイヤーマスク (Layer メニューで 'Ground' を作成して設定)")]
    [SerializeField] private LayerMask groundLayer;

    [Tooltip("足元からの接地判定距離 (小さいほど厳密)")]
    [SerializeField] private float groundCheckDistance = 0.15f;

    [Tooltip("ジャンプ後の接地判定スキップ時間 (秒)。崖際での誤判定を防止")]
    [SerializeField] private float jumpCooldownDuration = 0.1f;

    // ──────── Internal ────────

    private Rigidbody          _rb;
    private CapsuleCollider    _capsule;
    private PlayerStateManager _stateManager;

    private Vector2 _currentInput;
    private bool    _isGrounded;
    private float   _jumpCooldown;

    /// <summary>現在プレイヤーが地面に接しているか</summary>
    public bool IsGrounded => _isGrounded;

    /// <summary>物理移動を実行してよいか。</summary>
    private bool CanMove =>
        _stateManager == null ||
        _stateManager.CurrentState == PlayerState.Normal;

    // ──────── Unity Lifecycle ────────

    private void Awake()
    {
        _rb           = GetComponent<Rigidbody>();
        _capsule      = GetComponent<CapsuleCollider>();
        _stateManager = GetComponent<PlayerStateManager>();

        _rb.constraints = RigidbodyConstraints.FreezeRotationX
                        | RigidbodyConstraints.FreezeRotationZ;
    }

    private void FixedUpdate()
    {
        if (_jumpCooldown > 0f)
            _jumpCooldown -= Time.fixedDeltaTime;

        CheckGround();
        ApplyMovement();
    }

    // ──────── 接地判定 ────────

    private void CheckGround()
    {
        if (_jumpCooldown > 0f)
        {
            _isGrounded = false;
            return;
        }

        float radius = _capsule != null ? _capsule.radius * 0.9f : 0.45f;
        Vector3 origin = transform.position + Vector3.up * radius;
        _isGrounded = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out _,
            groundCheckDistance,
            groundLayer,
            QueryTriggerInteraction.Ignore);
    }

    // ──────── 移動処理 ────────

    private void ApplyMovement()
    {
        if (!CanMove) return;

        if (_currentInput == Vector2.zero)
        {
            Vector3 v = _rb.linearVelocity;
            v.x = 0f;
            v.z = 0f;
            _rb.linearVelocity = v;
            return;
        }

        Vector3 localDir = new Vector3(_currentInput.x, 0f, _currentInput.y).normalized;
        Vector3 worldDir = transform.TransformDirection(localDir);

        Vector3 targetHorizontal  = new Vector3(worldDir.x * moveSpeed, 0f, worldDir.z * moveSpeed);
        Vector3 currentHorizontal = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        Vector3 velocityDelta     = targetHorizontal - currentHorizontal;

        _rb.AddForce(velocityDelta, ForceMode.VelocityChange);
    }

    // ──────── 公開 API ────────

    /// <summary>移動入力を受け取る。</summary>
    public void Move(Vector2 input)
    {
        _currentInput = input;
    }

    /// <summary>ジャンプを実行する。</summary>
    public void Jump()
    {
        if (!_isGrounded) return;
        _jumpCooldown = jumpCooldownDuration;
        _rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
    }

    // ──────── Gizmo ────────

    private void OnDrawGizmosSelected()
    {
        float radius = _capsule != null ? _capsule.radius * 0.9f : 0.45f;
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Vector3 origin = transform.position + Vector3.up * radius;
        Gizmos.DrawWireSphere(origin + Vector3.down * groundCheckDistance, radius);
    }
}
