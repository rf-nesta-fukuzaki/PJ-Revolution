using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Rigidbody を使った高精度物理ベースのプレイヤー移動。
/// 入力の受付は PlayerInputController から Move() / Jump() を呼び出すことで行う。
///
/// [物理演算仕様]
///   - 接地判定: SphereCast（メイン）＋ 4方向 Raycast（サブ）の複合判定。法線の平均で斜面角度を算出。
///   - 斜面の段階処理: 0-20° 平坦 / 20-35° 緩斜面（速度低下）/ 35-50° 急斜面（スライド）/ 50°+ 壁面（入力無効）
///   - 動的摩擦: PhysicMaterial 不使用。コード内で角度・速度に応じた摩擦係数を計算して減速。
///   - 段差乗り越え: 0.3m 以下の段差を自動で滑らかに乗り越える。
///   - 落下加速・短ジャンプ: fallMultiplier / lowJumpMultiplier による重力補正。
///   - コヨーテタイム: 地面を離れてから 0.1 秒以内ならジャンプ可能。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移動")]
    [Tooltip("地上での最大移動速度 (m/s)")]
    [SerializeField] private float _moveSpeed = 6f;

    [Tooltip("空中での移動入力倍率 (0〜1)")]
    [SerializeField] private float _airControlMultiplier = 0.15f;

    [Header("ジャンプ")]
    [Tooltip("ジャンプの初速度 (VelocityChange)")]
    [SerializeField] private float _jumpForce = 7f;

    [Tooltip("コヨーテタイム: 地面を離れてからジャンプできる猶予時間（秒）")]
    [SerializeField] private float _coyoteTime = 0.1f;

    [Header("重力補正")]
    [Tooltip("落下中の追加重力倍率（大きいほど速く落ちる）")]
    [SerializeField] private float _fallMultiplier = 2.5f;

    [Tooltip("ジャンプ上昇中にキーを離した場合の追加重力倍率（短ジャンプ）")]
    [SerializeField] private float _lowJumpMultiplier = 2f;

    [Header("斜面（詳細）")]
    [Tooltip("平坦扱いの最大角度（度）")]
    [SerializeField] private float _gentleSlopeMax = 20f;

    [Tooltip("緩斜面の上限（これ以下は登れる）")]
    [SerializeField] private float _moderateSlopeMax = 35f;

    [Tooltip("急斜面の上限（これ以上は壁面扱いで入力無効）")]
    [SerializeField] private float _steepSlopeMax = 50f;

    [Tooltip("急斜面でのスライド加速度 (Acceleration)")]
    [SerializeField] private float _slideAcceleration = 12f;

    [Tooltip("スライドの最大速度 (m/s)")]
    [SerializeField] private float _maxSlideSpeed = 15f;

    [Header("段差乗り越え")]
    [Tooltip("自動で乗り越える段差の最大高さ (m)")]
    [SerializeField] private float _stepHeight = 0.3f;

    [Tooltip("段差を上る速さ（滑らかさ）")]
    [SerializeField] private float _stepSmooth = 8f;

    [Header("接地判定")]
    [Tooltip("SphereCast の半径（CapsuleCollider がない場合に使用）")]
    [SerializeField] private float _groundCheckRadius = 0.3f;

    [Tooltip("足元からの接地判定距離")]
    [SerializeField] private float _groundCheckDistance = 0.2f;

    [Tooltip("地面と判定するレイヤーマスク（デフォルト: 全レイヤー）")]
    [SerializeField] private LayerMask _groundLayer = ~0;

    [Header("デバッグ（読み取り専用）")]
    [SerializeField] private float _debugSlopeAngle;
    [SerializeField] private float _debugFriction;

    // ──────── Internal ────────

    private Rigidbody          _rb;
    private CapsuleCollider    _capsule;
    private PlayerStateManager _stateManager;

    private Vector2 _currentInput;
    private bool    _jumpRequested;  // Jump() で立て、FixedUpdate で消費
    private bool    _jumpHeld;       // 短ジャンプ判定用キー保持フラグ（Update で取得）
    private float   _coyoteTimer;
    private float   _jumpCooldown;   // ジャンプ直後の再接地誤判定防止

    private Vector3 _groundNormal    = Vector3.up;
    private float   _currentSlopeAngle;

    /// <summary>歩行可能な地面に接しているか（moderateSlopeMax 以下）</summary>
    public bool IsGrounded { get; private set; }

    /// <summary>急斜面上にいるか（moderateSlopeMax 超え）</summary>
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
        // 短ジャンプ判定: キー保持状態を Update で取得して FixedUpdate に橋渡し
        _jumpHeld = Input.GetButton("Jump");
    }

    private void FixedUpdate()
    {
        if (_jumpCooldown > 0f)
            _jumpCooldown -= Time.fixedDeltaTime;

        CheckGround();
        ApplyGravityModifier();
        ApplyMovement();
        ApplySlopeSlide();
        ApplyFriction();
        HandleStepClimb();
        ProcessJump();
    }

    // ──────── 接地判定（複合） ────────

    private void CheckGround()
    {
        if (_jumpCooldown > 0f)
        {
            IsGrounded     = false;
            IsOnSteepSlope = false;
            _coyoteTimer   = 0f;
            return;
        }

        bool hit = PerformGroundCheck(out Vector3 avgNormal, out float slopeAngle);

        if (hit)
        {
            _groundNormal      = avgNormal;
            _currentSlopeAngle = slopeAngle;
            _debugSlopeAngle   = slopeAngle;

            if (slopeAngle <= _moderateSlopeMax)
            {
                IsGrounded     = true;
                IsOnSteepSlope = false;
            }
            else
            {
                // 急斜面・壁面: 立てない
                IsGrounded     = false;
                IsOnSteepSlope = true;
            }
        }
        else
        {
            _groundNormal      = Vector3.up;
            _currentSlopeAngle = 0f;
            _debugSlopeAngle   = 0f;
            IsGrounded         = false;
            IsOnSteepSlope     = false;
        }

        // コヨーテタイム: 接地中はリセット、離れたら減算
        if (IsGrounded)
            _coyoteTimer = _coyoteTime;
        else if (_coyoteTimer > 0f)
            _coyoteTimer -= Time.fixedDeltaTime;
    }

    /// <summary>
    /// SphereCast（メイン）＋ 4方向 Raycast（サブ）による複合接地判定。
    /// 全ヒット法線の平均から斜面角度を算出する。
    /// </summary>
    private bool PerformGroundCheck(out Vector3 avgNormal, out float slopeAngle)
    {
        float radius   = _capsule != null ? _capsule.radius * 0.85f : _groundCheckRadius;
        Vector3 origin = transform.position + Vector3.up * (radius + 0.05f);

        bool mainHit = Physics.SphereCast(
            origin, radius, Vector3.down, out RaycastHit mainInfo,
            _groundCheckDistance + 0.05f, _groundLayer, QueryTriggerInteraction.Ignore);

        // 4方向サブ Raycast（前後左右にオフセット）
        Vector3[] offsets =
        {
             transform.forward * radius * 0.5f,
            -transform.forward * radius * 0.5f,
             transform.right   * radius * 0.5f,
            -transform.right   * radius * 0.5f,
        };

        var normals = new List<Vector3>();
        if (mainHit) normals.Add(mainInfo.normal);

        foreach (var offset in offsets)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f + offset;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit subHit,
                _groundCheckDistance + 0.3f, _groundLayer, QueryTriggerInteraction.Ignore))
            {
                normals.Add(subHit.normal);
            }
        }

        if (normals.Count == 0)
        {
            avgNormal  = Vector3.up;
            slopeAngle = 0f;
            return false;
        }

        // ヒット法線の平均で斜面角度を算出
        avgNormal = Vector3.zero;
        foreach (var n in normals) avgNormal += n;
        avgNormal  = avgNormal.normalized;
        slopeAngle = Vector3.Angle(avgNormal, Vector3.up);
        return true;
    }

    // ──────── 重力補正 ────────

    private void ApplyGravityModifier()
    {
        if (_rb.linearVelocity.y < 0f)
        {
            // 落下中: 追加重力で素早く落とす
            _rb.AddForce(Physics.gravity * (_fallMultiplier - 1f), ForceMode.Acceleration);
        }
        else if (_rb.linearVelocity.y > 0f && !_jumpHeld)
        {
            // 上昇中にキーを離した: 弱い補正重力で短ジャンプにする
            _rb.AddForce(Physics.gravity * (_lowJumpMultiplier - 1f), ForceMode.Acceleration);
        }
    }

    // ──────── 移動処理 ────────

    private void ApplyMovement()
    {
        if (!CanMove) return;
        if (_currentInput == Vector2.zero) return;

        // 壁面（steepSlopeMax 超え）では入力を完全無効化
        if (_currentSlopeAngle > _steepSlopeMax) return;

        Vector3 localDir = new Vector3(_currentInput.x, 0f, _currentInput.y).normalized;
        Vector3 worldDir = transform.TransformDirection(localDir);

        // 接地中は地面法線に沿った方向に補正（斜面の登り降りが自然になる）
        if (IsGrounded)
            worldDir = Vector3.ProjectOnPlane(worldDir, _groundNormal).normalized;

        // 斜面角度による登り速度の減衰（登り方向のみ低下、下りは影響なし）
        float slopeSpeedMultiplier = 1f;
        if (_currentSlopeAngle > _gentleSlopeMax)
        {
            float dotUphill = Vector3.Dot(worldDir, Vector3.up);
            if (dotUphill > 0.1f)
            {
                slopeSpeedMultiplier = Mathf.Lerp(1f, 0f,
                    Mathf.InverseLerp(_gentleSlopeMax, _moderateSlopeMax, _currentSlopeAngle));
            }
        }

        // 急斜面（35-50°）では空中制御と同程度の弱い抵抗のみ許可
        float control = IsGrounded ? 1f : _airControlMultiplier;
        if (IsOnSteepSlope) control = _airControlMultiplier;

        float   effectiveSpeed    = _moveSpeed * slopeSpeedMultiplier * control;
        Vector3 targetVel         = worldDir * effectiveSpeed;
        Vector3 currentHorizontal = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
        Vector3 delta             = targetVel - currentHorizontal;

        // 加速度クランプ（急激な速度変化を防ぐ）
        float maxAccel = IsGrounded ? 50f : 8f;
        if (delta.magnitude > maxAccel * Time.fixedDeltaTime)
            delta = delta.normalized * maxAccel * Time.fixedDeltaTime;

        _rb.AddForce(delta, ForceMode.VelocityChange);
    }

    // ──────── 急斜面スライド ────────

    private void ApplySlopeSlide()
    {
        if (!IsOnSteepSlope) return;

        // 重力方向を斜面に投影して「斜面に沿った下方向」を求める
        Vector3 slideDir      = Vector3.ProjectOnPlane(Physics.gravity, _groundNormal).normalized;
        Vector3 horizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

        // 最大速度に達していなければスライドフォースを加える
        if (horizontalVel.magnitude < _maxSlideSpeed)
            _rb.AddForce(slideDir * _slideAcceleration, ForceMode.Acceleration);
    }

    // ──────── 動的摩擦 ────────

    /// <summary>
    /// 斜面角度・速度に応じた動的摩擦係数を計算する。
    /// 0.0 = 摩擦なし（氷）、1.0 = 最大摩擦（即停止）
    /// </summary>
    private float CalculateDynamicFriction(float slopeAngle)
    {
        if (!IsGrounded) return 0f;

        float baseFriction = 0.6f;

        // 急斜面ほど摩擦が下がる
        float slopeFactor = 1f - Mathf.InverseLerp(_gentleSlopeMax, _steepSlopeMax, slopeAngle);

        // 速いほど動摩擦が下がる（滑りやすくなる）
        float speed       = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;
        float speedFactor = Mathf.Lerp(1f, 0.7f, Mathf.InverseLerp(0f, _moveSpeed, speed));

        return baseFriction * slopeFactor * speedFactor;
    }

    private void ApplyFriction()
    {
        // デバッグ値は常に更新
        _debugFriction = CalculateDynamicFriction(_currentSlopeAngle);

        // 入力がある間は摩擦を適用しない（移動の邪魔をしない）
        if (!IsGrounded || _currentInput != Vector2.zero) return;

        Vector3 horizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

        if (horizontalVel.magnitude < 0.1f)
        {
            // 低速時: 静摩擦で完全停止
            _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        }
        else
        {
            // 動摩擦: 速度に逆らう減速力
            Vector3 frictionForce = -horizontalVel.normalized * _debugFriction * 20f;
            _rb.AddForce(frictionForce, ForceMode.Acceleration);
        }
    }

    // ──────── 段差乗り越え ────────

    private void HandleStepClimb()
    {
        if (!IsGrounded || _currentInput == Vector2.zero) return;

        Vector3 moveDir = transform.TransformDirection(
            new Vector3(_currentInput.x, 0f, _currentInput.y)).normalized;

        // 足元前方に壁があるか確認
        Vector3 lowerOrigin = transform.position + Vector3.up * 0.1f;
        if (!Physics.Raycast(lowerOrigin, moveDir, 0.6f, _groundLayer, QueryTriggerInteraction.Ignore))
            return;

        // 段差高さの上には障害物がないか確認（あれば登れない壁）
        Vector3 upperOrigin = transform.position + Vector3.up * (_stepHeight + 0.1f);
        if (Physics.Raycast(upperOrigin, moveDir, 0.6f, _groundLayer, QueryTriggerInteraction.Ignore))
            return;

        // 段差を滑らかに乗り越える
        _rb.position += Vector3.up * _stepSmooth * Time.fixedDeltaTime;
    }

    // ──────── ジャンプ処理 ────────

    private void ProcessJump()
    {
        if (!_jumpRequested) return;
        _jumpRequested = false;

        // コヨーテタイム内のみジャンプ可
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
    public void Move(Vector2 input) => _currentInput = input;

    /// <summary>
    /// ジャンプリクエストを登録する。PlayerInputController から呼ぶ。
    /// 実際のジャンプは次の FixedUpdate で処理される。
    /// </summary>
    public void Jump() => _jumpRequested = true;

    /// <summary>
    /// 登攀モードの切替。PlayerClimbing から呼ぶ。
    /// 通常→登攀: velocity=0 → isKinematic=true の順で設定する（逆順にすると壁埋まりが起きる）。
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
        float   radius = _capsule != null ? _capsule.radius * 0.85f : _groundCheckRadius;
        Vector3 origin = transform.position + Vector3.up * (radius + 0.05f);

        // メイン SphereCast の当たり判定
        Gizmos.color = IsGrounded ? Color.green : (IsOnSteepSlope ? Color.yellow : Color.red);
        Gizmos.DrawWireSphere(origin + Vector3.down * (_groundCheckDistance + 0.05f), radius);

        // 4方向サブ Raycast の可視化
        Gizmos.color = Color.cyan;
        Vector3[] offsets =
        {
             transform.forward * radius * 0.5f,
            -transform.forward * radius * 0.5f,
             transform.right   * radius * 0.5f,
            -transform.right   * radius * 0.5f,
        };
        foreach (var offset in offsets)
        {
            Vector3 rayOrigin = transform.position + Vector3.up * 0.1f + offset;
            Gizmos.DrawLine(rayOrigin, rayOrigin + Vector3.down * (_groundCheckDistance + 0.3f));
        }
    }
}
