using UnityEngine;

/// <summary>
/// Rigidbody を使った物理ベースのプレイヤー移動を担当する。
/// 入力の受付は PlayerInputController から Move() / Jump() を呼び出すことで行う。
///
/// [物理演算改善点]
///   - 空中制御を airControlMultiplier で大幅制限（地上の 15%）。
///   - 落下加速・短ジャンプ補正（fallMultiplier / lowJumpMultiplier）。
///   - コヨーテタイム（地面を離れた直後でもジャンプ可能）。
///   - maxSlopeAngle を超える急斜面では斜面に沿ってスライドフォースを加える。
///   - 地上静止時のみ linearDamping を高くし、移動中・空中は 0 に戻す。
///   - SetClimbingMode() で登攀モードを切り替える（PlayerClimbing から呼ぶ）。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移動")]
    [Tooltip("地上での最大移動速度 (m/s)")]
    [SerializeField] private float _moveSpeed = 6f;

    [Tooltip("空中での移動入力倍率 (0〜1)。1 = 地上と同じ、0.15 で大幅制限")]
    [SerializeField] private float _airControlMultiplier = 0.15f;

    [Header("ジャンプ")]
    [Tooltip("ジャンプの初速度 (VelocityChange)")]
    [SerializeField] private float _jumpForce = 7f;

    [Tooltip("コヨーテタイム: 地面を離れてからジャンプできる猶予時間（秒）")]
    [SerializeField] private float _coyoteTime = 0.1f;

    [Header("重力補正")]
    [Tooltip("落下中に追加する重力倍率（大きいほど速く落ちる）")]
    [SerializeField] private float _fallMultiplier = 2.5f;

    [Tooltip("ジャンプ上昇中にキーを離した場合の重力倍率（短ジャンプ）")]
    [SerializeField] private float _lowJumpMultiplier = 2f;

    [Header("斜面")]
    [Tooltip("この角度を超えると急斜面と判定し、スライドフォースを加える（度）")]
    [SerializeField] private float _maxSlopeAngle = 45f;

    [Tooltip("急斜面で加えるスライドフォース (Acceleration)")]
    [SerializeField] private float _slideForce = 8f;

    [Header("接地判定")]
    [Tooltip("接地チェック用 SphereCast の半径（_capsule がなければこの値を使用）")]
    [SerializeField] private float _groundCheckRadius = 0.3f;

    [Tooltip("足元から伸ばす接地チェック距離")]
    [SerializeField] private float _groundCheckDistance = 0.2f;

    [Tooltip("地面と判定するレイヤーマスク（デフォルト: 全レイヤー）")]
    [SerializeField] private LayerMask _groundLayer = ~0;

    // ──────── Internal ────────

    private Rigidbody          _rb;
    private CapsuleCollider    _capsule;
    private PlayerStateManager _stateManager;

    private Vector2 _currentInput;
    private bool    _jumpRequested;  // Jump() で立て、FixedUpdate で消費
    private bool    _jumpHeld;       // 短ジャンプ判定用キー保持フラグ（Update で取得）
    private float   _coyoteTimer;
    private float   _jumpCooldown;   // ジャンプ直後の再接地誤判定防止
    private Vector3 _slopeNormal = Vector3.up;

    /// <summary>現在プレイヤーが歩行可能な地面に接しているか</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>急斜面上にいるか（maxSlopeAngle 超え）</summary>
    public bool IsOnSteepSlope { get; private set; }

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
        _rb.linearDamping = 0f;
    }

    private void Update()
    {
        // 短ジャンプ判定: ジャンプキーの保持状態を Update で取得し FixedUpdate へ橋渡し
        _jumpHeld = Input.GetButton("Jump");
    }

    private void FixedUpdate()
    {
        if (_jumpCooldown > 0f)
            _jumpCooldown -= Time.fixedDeltaTime;

        CheckGround();
        ManageDrag();
        ApplyGravityModifier();
        ApplyMovement();
        ApplySlopeSlide();
        ProcessJump();
    }

    // ──────── 接地判定 ────────

    private void CheckGround()
    {
        if (_jumpCooldown > 0f)
        {
            // ジャンプ直後: 接地・コヨーテ判定をリセットして誤ジャンプを防止
            IsGrounded    = false;
            IsOnSteepSlope = false;
            _coyoteTimer  = 0f;
            return;
        }

        float radius = _capsule != null ? _capsule.radius * 0.9f : _groundCheckRadius;
        Vector3 origin = transform.position + Vector3.up * radius;

        bool hit = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out RaycastHit hitInfo,
            _groundCheckDistance,
            _groundLayer,
            QueryTriggerInteraction.Ignore);

        if (hit)
        {
            _slopeNormal = hitInfo.normal;
            float angle  = Vector3.Angle(hitInfo.normal, Vector3.up);
            IsOnSteepSlope = angle > _maxSlopeAngle;
            IsGrounded     = !IsOnSteepSlope;
        }
        else
        {
            _slopeNormal   = Vector3.up;
            IsGrounded     = false;
            IsOnSteepSlope = false;
        }

        // コヨーテタイム: 接地中はタイマーをリセット、離れたら減算
        if (IsGrounded)
            _coyoteTimer = _coyoteTime;
        else if (_coyoteTimer > 0f)
            _coyoteTimer -= Time.fixedDeltaTime;
    }

    // ──────── Drag 管理 ────────

    private void ManageDrag()
    {
        // 地上静止時のみ drag を高くして自然に止まる。移動中・空中は 0
        _rb.linearDamping = (IsGrounded && _currentInput == Vector2.zero) ? 8f : 0f;
    }

    // ──────── 重力補正 ────────

    private void ApplyGravityModifier()
    {
        if (_rb.linearVelocity.y < 0f)
        {
            // 落下中: Physics.gravity に追加の倍率をかけて速く落とす
            _rb.AddForce(Physics.gravity * (_fallMultiplier - 1f), ForceMode.Acceleration);
        }
        else if (_rb.linearVelocity.y > 0f && !_jumpHeld)
        {
            // 上昇中にジャンプキーを離した: 低い補正重力で短ジャンプにする
            _rb.AddForce(Physics.gravity * (_lowJumpMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    // ──────── 移動処理 ────────

    private void ApplyMovement()
    {
        if (!CanMove) return;
        if (_currentInput == Vector2.zero) return;

        float control = IsGrounded ? 1f : _airControlMultiplier;

        Vector3 localDir          = new Vector3(_currentInput.x, 0f, _currentInput.y).normalized;
        Vector3 worldDir          = transform.TransformDirection(localDir);
        Vector3 targetHorizontal  = new Vector3(worldDir.x * _moveSpeed, 0f, worldDir.z * _moveSpeed);
        Vector3 currentHorizontal = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        Vector3 velocityDelta     = (targetHorizontal - currentHorizontal) * control;

        _rb.AddForce(velocityDelta, ForceMode.VelocityChange);
    }

    // ──────── 斜面スライド ────────

    private void ApplySlopeSlide()
    {
        if (!IsOnSteepSlope) return;

        // 重力方向を斜面に投影することで「斜面に沿った下方向」を求める
        Vector3 slideDir = Vector3.ProjectOnPlane(Physics.gravity, _slopeNormal).normalized;
        _rb.AddForce(slideDir * _slideForce, ForceMode.Acceleration);
    }

    // ──────── ジャンプ処理 ────────

    private void ProcessJump()
    {
        if (!_jumpRequested) return;
        _jumpRequested = false;

        // コヨーテタイム内のみジャンプ可（地上 or 離れた直後の猶予）
        if (_coyoteTimer <= 0f) return;

        _coyoteTimer  = 0f;
        _jumpCooldown = 0.1f;

        // 上方向の既存速度をリセットしてから Impulse（二段ジャンプ防止）
        Vector3 v = _rb.linearVelocity;
        v.y = 0f;
        _rb.linearVelocity = v;
        _rb.AddForce(Vector3.up * _jumpForce, ForceMode.VelocityChange);
    }

    // ──────── 公開 API ────────

    /// <summary>移動入力を受け取る。PlayerInputController から呼ぶ。</summary>
    public void Move(Vector2 input)
    {
        _currentInput = input;
    }

    /// <summary>
    /// ジャンプリクエストを登録する。PlayerInputController から呼ぶ。
    /// 実際のジャンプは次の FixedUpdate で処理される。
    /// </summary>
    public void Jump()
    {
        _jumpRequested = true;
    }

    /// <summary>
    /// 登攀モードの切替。PlayerClimbing から呼ぶ。
    /// 通常→登攀: velocity=0 → isKinematic=true の順で設定する。
    /// 登攀→通常: isKinematic=false → micro-lift AddForce で壁埋まりを防止する。
    /// </summary>
    public void SetClimbingMode(bool climbing)
    {
        if (climbing)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic    = true;
        }
        else
        {
            _rb.isKinematic = false;
            _rb.AddForce(Vector3.up * 0.5f, ForceMode.VelocityChange);
        }
    }

    // ──────── Gizmo ────────

    private void OnDrawGizmosSelected()
    {
        float radius = _capsule != null ? _capsule.radius * 0.9f : _groundCheckRadius;
        Gizmos.color = IsGrounded ? Color.green : (IsOnSteepSlope ? Color.yellow : Color.red);
        Vector3 origin = transform.position + Vector3.up * radius;
        Gizmos.DrawWireSphere(origin + Vector3.down * _groundCheckDistance, radius);
    }
}
