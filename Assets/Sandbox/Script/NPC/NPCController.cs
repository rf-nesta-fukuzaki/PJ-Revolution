using System.Collections;
using UnityEngine;

/// <summary>
/// オフラインテスト用 NPC AI コントローラー。
///
/// NavMesh 不要のシンプルなウェイポイント徘徊 + 障害物回避 + ジャンプを実装する。
/// NetworkBehaviour に依存しないため、NGO（Netcode for GameObjects）なしで動作する。
/// Health / Respawn も内包しており、他コンポーネントへの依存はゼロ。
///
/// Animator パラメーター（ExplorerAnimator.controller と共有）:
///   float   Speed      (0-1) : 移動中=1、停止=0（State Speed Multiplier）
///   float   SpeedBlend (0-1) : 常に0（NPC はスプリントしない）
///   trigger JumpTrigger      : ジャンプ開始時
///   bool    IsGrounded       : 接地状態
///
/// 行動サイクル:
///   1. ランダムウェイポイントを選択（スポーン地点を中心に一定半径内、上方バイアスあり）
///   2. ウェイポイントへ向かって Rigidbody.MovePosition で前進
///   3. 前方に崖・障害物を検出したらジャンプ or 別方向へ転換
///   2 秒間ほぼ動かなかった場合（スタック）は強制的に方向転換
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class NPCController : MonoBehaviour
{
    // ── 移動 ─────────────────────────────────────────────────
    [Header("移動")]
    [SerializeField] private float _moveSpeed          = 4f;
    [SerializeField] private float _wanderRadius       = 20f;
    [SerializeField] private float _waypointTolerance  = 1.8f;
    [SerializeField] private float _targetPickInterval = 4f;

    // ── ジャンプ ──────────────────────────────────────────────
    [Header("ジャンプ")]
    [SerializeField] private float _jumpForce    = 6f;
    [SerializeField] private float _jumpCooldown = 1.5f;

    // ── 接地判定 ──────────────────────────────────────────────
    [Header("接地判定")]
    [SerializeField] private float     _groundCheckOffset = -0.85f;
    [SerializeField] private float     _groundCheckRadius = 0.30f;
    [SerializeField] private LayerMask _groundLayer;

    // ── HP / リスポーン ────────────────────────────────────────
    [Header("HP / リスポーン")]
    [SerializeField] private float _maxHp        = 100f;
    [SerializeField] private float _deathY       = -10f;
    [SerializeField] private float _respawnDelay =  5f;

    // ── アニメーション補間 ──────────────────────────────────────
    [Header("アニメーション")]
    [SerializeField] private float _animSmoothTime = 0.15f;

    // ── 内部状態 ──────────────────────────────────────────────
    private Rigidbody _rb;
    private Animator  _animator;
    private float     _currentHp;
    private bool      _isDead;
    private bool      _isMoving;

    private Vector3 _homePos;
    private Vector3 _waypoint;
    private float   _waypointTimer;
    private float   _jumpTimer;
    private float   _stuckTimer;
    private Vector3 _stuckLastPos;
    private bool    _isGrounded;

    private float _currentSpeed;
    private float _speedVelocity;

    // Animator パラメーターハッシュ（文字列ルックアップを回避）
    private static readonly int SpeedHash       = Animator.StringToHash("Speed");
    private static readonly int SpeedBlendHash  = Animator.StringToHash("SpeedBlend");
    private static readonly int JumpTriggerHash = Animator.StringToHash("JumpTrigger");
    private static readonly int IsGroundedHash  = Animator.StringToHash("IsGrounded");

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _currentHp = _maxHp;
        // Animator は Start() で取得する。
        // AddComponent<NPCController>() 直後に Awake() が走るため、
        // この時点ではまだ子モデルが未アタッチで GetComponentInChildren が null を返す。
    }

    private void Start()
    {
        // Start() は次フレーム冒頭に呼ばれるため、
        // OfflineNPCSpawner.Start() で同フレーム中にアタッチした
        // Explorer モデルの Animator をここで確実に取得できる。
        _animator = GetComponentInChildren<Animator>();

        if (_animator == null)
            Debug.LogWarning($"[NPCController] {gameObject.name}: Animator が見つかりません。モデルまたは AnimatorController を確認してください。");

        if (_groundLayer.value == 0)
            _groundLayer = ~(1 << 2);

        _homePos      = transform.position;
        _stuckLastPos = transform.position;
        _stuckTimer   = 2f;

        PickWaypoint();
    }

    // ── Update: タイマー管理・接地確認・落下死・スタック検出 ──
    private void Update()
    {
        if (_isDead)
        {
            UpdateAnimator(false, false);
            return;
        }

        _jumpTimer     = Mathf.Max(0f, _jumpTimer     - Time.deltaTime);
        _waypointTimer = Mathf.Max(0f, _waypointTimer - Time.deltaTime);
        _stuckTimer   -= Time.deltaTime;

        CheckGrounded();

        if (transform.position.y < _deathY)
            Die();

        if (_stuckTimer <= 0f)
        {
            _stuckTimer = 2f;

            if (Vector3.Distance(transform.position, _stuckLastPos) < 0.4f)
            {
                PickWaypoint();
                TryJump();
            }

            _stuckLastPos = transform.position;
        }

        UpdateAnimator(_isMoving, _isGrounded);
    }

    // ── FixedUpdate: Rigidbody 移動 ───────────────────────────
    private void FixedUpdate()
    {
        if (_isDead) return;

        if (_waypointTimer <= 0f)
            PickWaypoint();

        Vector3 toWaypoint = _waypoint - transform.position;
        toWaypoint.y = 0f;
        float dist = toWaypoint.magnitude;

        if (dist < _waypointTolerance)
        {
            _isMoving = false;
            PickWaypoint();
            return;
        }

        Vector3 dir = toWaypoint.normalized;

        // 崖チェック
        Vector3 aheadCheckOrigin = transform.position + dir * 0.8f + Vector3.up * 0.2f;
        if (!Physics.Raycast(aheadCheckOrigin, Vector3.down, 2.5f, _groundLayer))
        {
            _isMoving = false;
            PickWaypoint();
            return;
        }

        // 前方障害物チェック
        var obstacleRay = new Ray(transform.position + Vector3.up * 0.5f, dir);
        if (Physics.Raycast(obstacleRay, 1.2f, _groundLayer))
        {
            TryJump();
            dir = Quaternion.Euler(0f, Random.Range(-70f, 70f), 0f) * dir;
        }

        // 向きを滑らかに変更
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            8f * Time.fixedDeltaTime);

        _rb.MovePosition(_rb.position + dir * (_moveSpeed * Time.fixedDeltaTime));
        _isMoving = true;
    }

    // ── アニメーター更新 ──────────────────────────────────────
    private void UpdateAnimator(bool moving, bool grounded)
    {
        if (_animator == null) return;

        float targetSpeed = moving ? 1f : 0f;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed,
                                          ref _speedVelocity, _animSmoothTime);

        _animator.SetFloat(SpeedHash,      _currentSpeed);
        _animator.SetFloat(SpeedBlendHash, 0f);   // NPC はスプリントしない
        _animator.SetBool(IsGroundedHash,  grounded);
    }

    // ── 接地確認 ──────────────────────────────────────────────
    private void CheckGrounded()
    {
        Vector3 origin = _rb.position + Vector3.up * _groundCheckOffset;
        _isGrounded = Physics.CheckSphere(
            origin, _groundCheckRadius, _groundLayer, QueryTriggerInteraction.Ignore);
    }

    // ── ジャンプ ──────────────────────────────────────────────
    private void TryJump()
    {
        if (!_isGrounded || _jumpTimer > 0f) return;

        _jumpTimer = _jumpCooldown;
        Vector3 vel = _rb.linearVelocity;
        vel.y = 0f;
        _rb.linearVelocity = vel;
        _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);

        _animator?.SetTrigger(JumpTriggerHash);
    }

    // ── ウェイポイント選択 ────────────────────────────────────
    private void PickWaypoint()
    {
        _waypointTimer = _targetPickInterval + Random.Range(-1f, 1.5f);

        Vector2 xz   = Random.insideUnitCircle * _wanderRadius;
        float   yOff = Random.Range(-3f, 12f);

        _waypoint = new Vector3(
            _homePos.x + xz.x,
            _homePos.y + yOff,
            _homePos.z + xz.y);
    }

    // ── HP / ダメージ ─────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (_isDead || amount <= 0f) return;
        _currentHp = Mathf.Max(0f, _currentHp - amount);
        if (_currentHp <= 0f) Die();
    }

    // ── 死亡 / リスポーン ─────────────────────────────────────
    private void Die()
    {
        if (_isDead) return;

        _isDead            = true;
        _rb.isKinematic    = true;
        _rb.linearVelocity = Vector3.zero;

        Debug.Log($"[NPC] {gameObject.name} が死亡しました");
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnDelay);

        transform.position = _homePos;
        _currentHp         = _maxHp;
        _isDead            = false;
        _isMoving          = false;
        _rb.isKinematic    = false;

        PickWaypoint();
        Debug.Log($"[NPC] {gameObject.name} がリスポーンしました");
    }

    // ── デバッグ表示 ─────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, _waypoint);
        Gizmos.DrawWireSphere(_waypoint, 0.4f);

        Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
        Gizmos.DrawWireSphere(_homePos, _wanderRadius);
    }
}
