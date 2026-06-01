using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// 敵モンスター本体。Patrol→Investigate→Chase→Attack→Search の FSM で駆動し、
/// プレイヤーを発見すると追跡・攻撃し、運搬中の遺物を叩き落とす（R.E.P.O. パリティ）。
/// 移動は NPCController と同じ接地マスク自己修復＋障害物回避の方針を踏襲する。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class EnemyController : MonoBehaviour
{
    [Header("設定")]
    [SerializeField] private EnemyConfigSO _config;

    [Header("接地判定")]
    [SerializeField] private float _groundCheckOffset = -1.0f;
    [SerializeField] private float _groundCheckRadius = 0.35f;
    [SerializeField] private LayerMask _groundLayer;

    private Rigidbody _rb;
    private EnemySensor _sensor;

    private EnemyState _state = EnemyState.Patrol;
    private Vector3 _home;
    private Vector3 _goal;
    private bool _hasGoal;
    private bool _isGrounded;

    private PlayerHealthSystem _target;
    private float _attackTimer;
    private float _stateTimer;
    private float _loseSightTimer;
    private float _jumpTimer;
    private float _roarTimer;

    public EnemyState State => _state;

    /// <summary>スポーナーから設定を注入する（Awake 前に呼ぶ）。</summary>
    public void Configure(EnemyConfigSO config)
    {
        if (config != null) _config = config;
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        if (_config == null) _config = ScriptableObject.CreateInstance<EnemyConfigSO>();
        ResolveGroundMask();
    }

    private void Start()
    {
        _home = transform.position;
        _sensor = new EnemySensor(transform, _config, _groundLayer);
        PickPatrolWaypoint();
    }

    private void OnDestroy() => _sensor?.Dispose();

    /// <summary>地形整合後に徘徊/パトロール基点(_home)を現在の接地位置へ再設定する（CombinedTerrainConformer から）。</summary>
    public void ReanchorHome(Vector3 worldPos) => _home = worldPos;

    private void Update()
    {
        _attackTimer = Mathf.Max(0f, _attackTimer - Time.deltaTime);
        _jumpTimer   = Mathf.Max(0f, _jumpTimer - Time.deltaTime);
        _roarTimer   = Mathf.Max(0f, _roarTimer - Time.deltaTime);
        EnsureGroundMaskValid();
        CheckGrounded();

        switch (_state)
        {
            case EnemyState.Patrol:      TickPatrol();      break;
            case EnemyState.Investigate: TickInvestigate(); break;
            case EnemyState.Chase:       TickChase();       break;
            case EnemyState.Attack:      TickAttack();      break;
            case EnemyState.Search:      TickSearch();      break;
            case EnemyState.Stunned:     TickStunned();     break;
        }
    }

    private void FixedUpdate()
    {
        if (_state == EnemyState.Stunned || _state == EnemyState.Attack || !_hasGoal) return;

        float speed = _state == EnemyState.Chase ? _config.ChaseSpeed : _config.PatrolSpeed;
        MoveToward(_goal, speed);
    }

    // ── ステート処理 ────────────────────────────────────────────
    private void TickPatrol()
    {
        if (AcquireTarget()) return;

        if (_sensor.HasHeardNoise)
        {
            _sensor.ClearHeard();
            EnterInvestigate(_sensor.LastHeardPosition);
            return;
        }

        if (!_hasGoal || ReachedGoal())
            PickPatrolWaypoint();
    }

    private void TickInvestigate()
    {
        if (AcquireTarget()) return;

        _stateTimer -= Time.deltaTime;
        if (_sensor.HasHeardNoise)
        {
            _sensor.ClearHeard();
            _goal = _sensor.LastHeardPosition;
            _stateTimer = _config.InvestigateDuration;
        }

        if (ReachedGoal() || _stateTimer <= 0f)
            EnterPatrol();
    }

    private void TickChase()
    {
        bool seen;
        PlayerHealthSystem t = _sensor.FindTarget(out seen);
        if (t != null) _target = t;

        if (_target == null || _target.IsDead || _target.IsDowned)
        {
            EnterSearch();
            return;
        }

        _goal = seen ? _target.transform.position : _sensor.LastHeardPosition;
        _hasGoal = true;

        if (!seen)
        {
            _loseSightTimer += Time.deltaTime;
            if (_loseSightTimer >= _config.LoseTargetGrace)
            {
                EnterSearch();
                return;
            }
        }
        else _loseSightTimer = 0f;

        if (_roarTimer <= 0f)
        {
            GameServices.Audio?.PlaySE(SoundId.MonsterChase, transform.position);
            _roarTimer = 2.5f;
        }

        if (Vector3.Distance(transform.position, _target.transform.position) <= _config.AttackRange)
            _state = EnemyState.Attack;
    }

    private void TickAttack()
    {
        if (_target == null || _target.IsDead || _target.IsDowned)
        {
            EnterSearch();
            return;
        }

        FaceTowards(_target.transform.position);

        float dist = Vector3.Distance(transform.position, _target.transform.position);
        if (dist > _config.AttackRange * 1.15f)
        {
            _state = EnemyState.Chase;
            return;
        }

        if (_attackTimer <= 0f)
        {
            _attackTimer = _config.AttackCooldown;
            PerformAttack();
        }
    }

    private void TickSearch()
    {
        if (AcquireTarget()) return;

        _stateTimer -= Time.deltaTime;
        _goal = _sensor.LastHeardPosition;
        _hasGoal = true;

        if (ReachedGoal() || _stateTimer <= 0f)
            EnterPatrol();
    }

    private void TickStunned()
    {
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f) EnterPatrol();
    }

    // ── ステート遷移 ────────────────────────────────────────────
    private bool AcquireTarget()
    {
        PlayerHealthSystem t = _sensor.FindTarget(out bool seen);
        if (t != null && seen)
        {
            bool wasHunting = _state == EnemyState.Chase || _state == EnemyState.Attack;
            _target = t;
            _state = EnemyState.Chase;
            _loseSightTimer = 0f;
            _hasGoal = true;
            if (!wasHunting)
                GameServices.Audio?.PlaySE(SoundId.MonsterAlert, transform.position);
            return true;
        }
        return false;
    }

    private void EnterPatrol()
    {
        _state = EnemyState.Patrol;
        _target = null;
        PickPatrolWaypoint();
    }

    private void EnterInvestigate(Vector3 pos)
    {
        _state = EnemyState.Investigate;
        _goal = pos;
        _hasGoal = true;
        _stateTimer = _config.InvestigateDuration;
    }

    private void EnterSearch()
    {
        _state = EnemyState.Search;
        _stateTimer = _config.SearchDuration;
        _goal = _sensor.LastHeardPosition;
        _hasGoal = true;
        _loseSightTimer = 0f;
    }

    /// <summary>フレアガン等で一時的にひるませる外部 API。</summary>
    public void Stun(float duration = -1f)
    {
        _state = EnemyState.Stunned;
        _stateTimer = duration > 0f ? duration : _config.StunDuration;
        _rb.linearVelocity = new Vector3(0f, _rb.linearVelocity.y, 0f);
        GameServices.Audio?.PlaySE(SoundId.MonsterStunned, transform.position);
    }

    // ── 攻撃 ────────────────────────────────────────────────────
    private void PerformAttack()
    {
        GameServices.Audio?.PlaySE(SoundId.MonsterAttack, transform.position);
        _target.TakeDamage(_config.AttackDamage);
        KnockLoot(_target.transform);
    }

    /// <summary>ターゲットが運搬中の遺物をモンスターと反対方向に叩き落とす。</summary>
    private void KnockLoot(Transform holder)
    {
        var carriers = Object.FindObjectsByType<RelicCarrier>(FindObjectsSortMode.None);
        foreach (var c in carriers)
        {
            if (c == null || !c.IsBeingCarried || c.CurrentHolder != holder) continue;
            Vector3 away = (holder.position - transform.position);
            away.y = 0.3f;
            c.Drop(away.normalized * _config.LootKnockImpulse + Vector3.up * 2f);
        }
    }

    // ── 移動 ────────────────────────────────────────────────────
    private void MoveToward(Vector3 target, float speed)
    {
        Vector3 to = target - _rb.position;
        to.y = 0f;
        float dist = to.magnitude;
        if (dist <= _config.WaypointTolerance) return;

        Vector3 dir = to / Mathf.Max(dist, 0.0001f);
        dir = ApplyObstacleAvoidance(dir);
        if (IsCliffAhead(dir)) TryJump();

        FaceTowards(_rb.position + dir);
        _rb.MovePosition(_rb.position + dir * (speed * Time.fixedDeltaTime));
    }

    private void FaceTowards(Vector3 worldPos)
    {
        Vector3 dir = worldPos - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.0001f) return;
        transform.rotation = Quaternion.Slerp(
            transform.rotation, Quaternion.LookRotation(dir), _config.TurnSpeed * Time.deltaTime);
    }

    private Vector3 ApplyObstacleAvoidance(Vector3 dir)
    {
        var ray = new Ray(transform.position + Vector3.up * 0.6f, dir);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1.4f, _groundLayer, QueryTriggerInteraction.Ignore))
            return dir;

        Vector3 tangent = Vector3.Cross(Vector3.up, hit.normal).normalized;
        if (Vector3.Dot(tangent, dir) < 0f) tangent = -tangent;
        TryJump();
        Vector3 adjusted = (dir + tangent * 0.9f).normalized;
        return adjusted.sqrMagnitude > 0.0001f ? adjusted : dir;
    }

    private bool IsCliffAhead(Vector3 dir)
    {
        Vector3 origin = transform.position + dir * 1.1f + Vector3.up * 0.25f;
        return !Physics.Raycast(origin, Vector3.down, 3f, _groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void TryJump()
    {
        if (!_isGrounded || _jumpTimer > 0f) return;
        _jumpTimer = _config.JumpCooldown;
        Vector3 v = _rb.linearVelocity;
        v.y = _config.JumpVelocity;
        _rb.linearVelocity = v;
    }

    private void PickPatrolWaypoint()
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 xz = Random.insideUnitCircle * _config.PatrolRadius;
            Vector3 candidate = new(_home.x + xz.x, transform.position.y + 12f, _home.z + xz.y);
            if (Physics.Raycast(candidate, Vector3.down, out RaycastHit hit, 30f, _groundLayer, QueryTriggerInteraction.Ignore))
            {
                _goal = hit.point;
                _hasGoal = true;
                return;
            }
        }
        _goal = _home;
        _hasGoal = true;
    }

    private bool ReachedGoal()
    {
        Vector3 d = _goal - transform.position;
        d.y = 0f;
        return d.magnitude <= _config.WaypointTolerance;
    }

    // ── 接地・マスク自己修復（NPCController 準拠）────────────────
    private void CheckGrounded()
    {
        Vector3 origin = _rb.position + Vector3.up * _groundCheckOffset;
        _isGrounded = Physics.CheckSphere(origin, _groundCheckRadius, _groundLayer, QueryTriggerInteraction.Ignore)
                   || Physics.Raycast(_rb.position + Vector3.up * 0.1f, Vector3.down,
                          Mathf.Abs(_groundCheckOffset) + _groundCheckRadius + 0.35f,
                          _groundLayer, QueryTriggerInteraction.Ignore);
    }

    private void EnsureGroundMaskValid()
    {
        int mask = _groundLayer.value;
        int playerLayer = LayerMask.NameToLayer("Player");
        bool includesPlayer = playerLayer >= 0 && (mask & (1 << playerLayer)) != 0;
        bool includesIgnoreRaycast = (mask & (1 << 2)) != 0;
        if (mask == 0 || includesPlayer || includesIgnoreRaycast)
            ResolveGroundMask();
    }

    private void ResolveGroundMask()
    {
        int mask = Physics.DefaultRaycastLayers;
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) mask |= 1 << groundLayer;
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) mask &= ~(1 << playerLayer);
        mask &= ~(1 << 2); // Ignore Raycast
        _groundLayer = mask;
    }

    private void OnDrawGizmosSelected()
    {
        if (_config == null) return;
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, _config.VisionRange);
        Gizmos.color = Color.yellow;
        if (_hasGoal) Gizmos.DrawLine(transform.position, _goal);
    }
}
