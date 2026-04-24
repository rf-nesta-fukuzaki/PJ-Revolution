using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// オフラインテスト用 NPC コントローラー。
/// ユーティリティベースのメタAIで「遺物確保 / 帰還搬送 / 味方支援 / 回復 / 探索」を切り替える。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class NPCController : MonoBehaviour
{
    [Header("移動")]
    [SerializeField] private float _moveSpeed = 4.6f;
    [SerializeField] private float _carrySpeedMultiplier = 0.78f;
    [SerializeField] private float _turnSpeed = 8f;
    [SerializeField] private float _waypointTolerance = 1.35f;
    [SerializeField] private float _exploreRadius = 22f;
    [SerializeField] private float _retargetDistance = 2.2f;

    [Header("メタAI")]
    [SerializeField] private float _thinkIntervalMin = 0.35f;
    [SerializeField] private float _thinkIntervalMax = 0.95f;
    [SerializeField] private float _sensorRefreshInterval = 1.0f;
    [SerializeField] private float _hazardAvoidRadius = 4.5f;
    [SerializeField] private bool _verboseDecisionLog = false;

    [Header("遺物")]
    [SerializeField] private float _relicPickupRange = 1.6f;
    [SerializeField] private float _returnZoneDropRange = 2.4f;

    [Header("ジャンプ")]
    [SerializeField] private float _jumpVelocity = 6f;
    [SerializeField] private float _jumpCooldown = 1.2f;

    [Header("接地判定")]
    [SerializeField] private float _groundCheckOffset = -0.85f;
    [SerializeField] private float _groundCheckRadius = 0.30f;
    [SerializeField] private LayerMask _groundLayer;

    [Header("HP / リスポーン")]
    [SerializeField] private float _maxHp = 100f;
    [SerializeField] private float _deathY = -10f;
    [SerializeField] private float _respawnDelay = 5f;

    [Header("アニメーション")]
    [SerializeField] private float _animSmoothTime = 0.15f;

    private Rigidbody _rb;
    private Animator _animator;

    private readonly NPCMetaDecisionEngine _decisionEngine = new();
    private readonly List<RelicCarrier> _relicBuffer = new();
    private readonly List<RelicBase> _relicBaseBuffer = new();
    private readonly List<ShelterZone> _shelterBuffer = new();
    private readonly List<Transform> _hazardBuffer = new();

    private float _currentHp;
    private bool _isDead;
    private bool _isGrounded;
    private bool _isMoving;

    private Vector3 _homePos;
    private Vector3 _stuckLastPos;
    private float _stuckSeconds;
    private float _stuckSampleTimer;
    private float _jumpTimer;
    private float _nextThinkTimer;
    private float _nextSensorRefreshTimer;
    private float _currentSpeed;
    private float _speedVelocity;

    private NpcMetaGoal _currentGoal = NpcMetaGoal.Explore;
    private string _goalReason = string.Empty;
    private Vector3 _goalPosition;
    private bool _hasGoalPosition;
    private Transform _returnZoneTransform;
    private Transform _assistTarget;
    private RelicCarrier _targetRelic;
    private RelicCarrier _carriedRelic;
    private RelicBase _targetRelicBase;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int SpeedBlendHash = Animator.StringToHash("SpeedBlend");
    private static readonly int JumpTriggerHash = Animator.StringToHash("JumpTrigger");
    private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _currentHp = _maxHp;
        _stuckLastPos = transform.position;
        _stuckSampleTimer = 1f;
        ResolveGroundMask();
    }

    private void Start()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_animator == null)
            Debug.LogWarning($"[NPCController] {gameObject.name}: Animator が見つかりません。");

        _homePos = transform.position;
        EnsureScoreRegistration();

        RefreshSensors(force: true);
        ThinkAndSetGoal(force: true);
    }

    private void Update()
    {
        if (_isDead)
        {
            UpdateAnimator(false, false);
            return;
        }

        _jumpTimer = Mathf.Max(0f, _jumpTimer - Time.deltaTime);
        _nextThinkTimer -= Time.deltaTime;
        _nextSensorRefreshTimer -= Time.deltaTime;

        EnsureGroundMaskValid();
        EnsureScoreRegistration();
        CheckGrounded();
        UpdateStuckState();
        CleanupMissingTargets();

        if (transform.position.y < _deathY)
        {
            Die();
            return;
        }

        if (_nextSensorRefreshTimer <= 0f)
            RefreshSensors(force: false);

        if (_nextThinkTimer <= 0f || ShouldForceReplan())
            ThinkAndSetGoal(force: false);

        TryHandleRelicInteraction();
        UpdateAnimator(_isMoving, _isGrounded);
    }

    private void FixedUpdate()
    {
        if (_isDead)
            return;

        if (!_hasGoalPosition)
        {
            _isMoving = false;
            return;
        }

        Vector3 toGoal = _goalPosition - _rb.position;
        toGoal.y = 0f;
        float distance = toGoal.magnitude;
        if (distance <= _waypointTolerance)
        {
            _isMoving = false;
            OnGoalReached();
            return;
        }

        Vector3 dir = toGoal / Mathf.Max(distance, 0.0001f);
        dir = ApplyHazardAvoidance(dir);
        dir = ApplyObstacleAvoidance(dir);

        if (IsCliffAhead(dir))
        {
            TryJump();
            dir = Quaternion.Euler(0f, Random.Range(-95f, 95f), 0f) * dir;
        }

        if (dir.sqrMagnitude < 0.0001f)
        {
            _isMoving = false;
            return;
        }

        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(dir),
            _turnSpeed * Time.fixedDeltaTime);

        float speed = _moveSpeed * (_carriedRelic != null ? _carrySpeedMultiplier : 1f);
        _rb.MovePosition(_rb.position + dir * (speed * Time.fixedDeltaTime));
        _isMoving = true;
    }

    private void ThinkAndSetGoal(bool force)
    {
        RelicCarrier nearestRelic = FindNearestRelicCandidate();
        RelicBase nearestRelicBase = FindNearestRelicBaseCandidate();
        ShelterZone nearestShelter = FindNearestShelter();
        PlayerHealthSystem nearestAlly = FindNearestAlivePlayer();
        bool allyInDanger = nearestAlly != null && nearestAlly.HpPercent <= 0.45f;

        float nearestCarrierDistance = nearestRelic != null ? Vector3.Distance(transform.position, nearestRelic.transform.position) : Mathf.Infinity;
        float nearestBaseDistance = nearestRelicBase != null ? Vector3.Distance(transform.position, nearestRelicBase.transform.position) : Mathf.Infinity;
        float nearestRelicDistance = Mathf.Min(nearestCarrierDistance, nearestBaseDistance);
        float nearestReturnDistance = _returnZoneTransform != null ? Vector3.Distance(transform.position, _returnZoneTransform.position) : Mathf.Infinity;
        float nearestShelterDistance = nearestShelter != null ? Vector3.Distance(transform.position, nearestShelter.transform.position) : Mathf.Infinity;
        float nearestAllyDistance = nearestAlly != null ? Vector3.Distance(transform.position, nearestAlly.transform.position) : Mathf.Infinity;

        var context = new NpcMetaContext(
            hasRelic: _carriedRelic != null,
            hpRatio: _maxHp > 0.001f ? _currentHp / _maxHp : 1f,
            threatLevel: ComputeThreatLevel(),
            expeditionUrgency: ComputeExpeditionUrgency(),
            nearestRelicDistance: nearestRelicDistance,
            nearestReturnZoneDistance: nearestReturnDistance,
            nearestShelterDistance: nearestShelterDistance,
            nearestTeammateDistance: nearestAllyDistance,
            allyInDanger: allyInDanger,
            stuckSeconds: _stuckSeconds);

        NpcMetaDecision decision = _decisionEngine.Decide(context);
        _currentGoal = decision.Goal;
        _goalReason = decision.Reason;

        switch (_currentGoal)
        {
            case NpcMetaGoal.ReturnRelic:
                _goalPosition = _returnZoneTransform != null ? _returnZoneTransform.position : _homePos;
                _hasGoalPosition = true;
                break;

            case NpcMetaGoal.SecureRelic:
                _targetRelic = nearestRelic;
                _targetRelicBase = _targetRelic == null ? nearestRelicBase : null;
                _hasGoalPosition = _targetRelic != null || _targetRelicBase != null;
                if (_targetRelic != null)
                    _goalPosition = _targetRelic.transform.position;
                else if (_targetRelicBase != null)
                    _goalPosition = _targetRelicBase.transform.position;
                break;

            case NpcMetaGoal.Recover:
                _hasGoalPosition = nearestShelter != null;
                if (_hasGoalPosition)
                {
                    _goalPosition = nearestShelter.transform.position;
                }
                else
                {
                    _goalPosition = _homePos;
                    _hasGoalPosition = true;
                }
                break;

            case NpcMetaGoal.AssistTeammate:
                _assistTarget = nearestAlly != null ? nearestAlly.transform : null;
                _hasGoalPosition = _assistTarget != null;
                if (_hasGoalPosition)
                    _goalPosition = _assistTarget.position;
                break;

            case NpcMetaGoal.Repath:
                _goalPosition = PickExploreWaypoint();
                _hasGoalPosition = true;
                break;

            case NpcMetaGoal.Explore:
            default:
                if (!force && _hasGoalPosition && Vector3.Distance(_rb.position, _goalPosition) > _retargetDistance)
                {
                    // 直前ゴールを維持して無駄な目標変更を減らす。
                    break;
                }

                _goalPosition = PickExploreWaypoint();
                _hasGoalPosition = true;
                break;
        }

        if (_verboseDecisionLog)
        {
            Debug.Log($"[NPCMetaAI] {name} goal={_currentGoal} score={decision.Score:F2} reason={_goalReason}");
        }

        _nextThinkTimer = Random.Range(_thinkIntervalMin, _thinkIntervalMax);
    }

    private void RefreshSensors(bool force)
    {
        _nextSensorRefreshTimer = _sensorRefreshInterval;

        _relicBuffer.Clear();
        _relicBuffer.AddRange(Object.FindObjectsByType<RelicCarrier>(FindObjectsSortMode.None));

        _relicBaseBuffer.Clear();
        _relicBaseBuffer.AddRange(Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None));

        _shelterBuffer.Clear();
        _shelterBuffer.AddRange(Object.FindObjectsByType<ShelterZone>(FindObjectsSortMode.None));

        _hazardBuffer.Clear();
        AddHazards(Object.FindObjectsByType<IcePatch>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<CollapsiblePlatform>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<RockfallTrigger>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<FakeFloor>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<PressurePlateArrow>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<PendulumLog>(FindObjectsSortMode.None));
        AddHazards(Object.FindObjectsByType<FallingCeiling>(FindObjectsSortMode.None));

        if (_returnZoneTransform == null || force)
        {
            var returnZone = Object.FindFirstObjectByType<ReturnZone>();
            _returnZoneTransform = returnZone != null ? returnZone.transform : null;
        }
    }

    private void AddHazards<T>(T[] hazards) where T : Component
    {
        foreach (T hazard in hazards)
        {
            if (hazard == null)
                continue;

            _hazardBuffer.Add(hazard.transform);
        }
    }

    private void TryHandleRelicInteraction()
    {
        if (_carriedRelic != null)
        {
            if (_returnZoneTransform != null &&
                Vector3.Distance(transform.position, _returnZoneTransform.position) <= _returnZoneDropRange)
            {
                _carriedRelic.PutDown();
                _carriedRelic = null;
                _targetRelic = null;
                _nextThinkTimer = 0f;
            }

            return;
        }

        if (_targetRelic == null || _targetRelic.IsBeingCarried)
        {
            if (_targetRelicBase != null)
            {
                float baseDistance = Vector3.Distance(transform.position, _targetRelicBase.transform.position);
                if (baseDistance <= _relicPickupRange)
                {
                    // RelicCarrier が無い遺物でも「確保」扱いとしてスコアへ反映する。
                    EnsureScoreRegistration();
                    GameServices.Score?.RegisterCollectedRelic(_targetRelicBase);
                    GameServices.Score?.RecordRelicFound(GetInstanceID());
                    _targetRelicBase = null;
                    _nextThinkTimer = 0f;
                }
            }

            return;
        }

        float distance = Vector3.Distance(transform.position, _targetRelic.transform.position);
        if (distance > _relicPickupRange)
            return;

        _targetRelic.PickUp(transform, GetInstanceID());
        _carriedRelic = _targetRelic;
        _targetRelic = null;
        _targetRelicBase = null;
        _nextThinkTimer = 0f;
    }

    private void CheckGrounded()
    {
        Vector3 sphereOrigin = _rb.position + Vector3.up * _groundCheckOffset;
        bool sphere = Physics.CheckSphere(
            sphereOrigin,
            _groundCheckRadius,
            _groundLayer,
            QueryTriggerInteraction.Ignore);

        bool ray = Physics.Raycast(
            _rb.position + Vector3.up * 0.1f,
            Vector3.down,
            Mathf.Abs(_groundCheckOffset) + _groundCheckRadius + 0.35f,
            _groundLayer,
            QueryTriggerInteraction.Ignore);

        _isGrounded = sphere || ray;
    }

    private void UpdateStuckState()
    {
        _stuckSampleTimer -= Time.deltaTime;
        if (_stuckSampleTimer > 0f)
            return;

        _stuckSampleTimer = 1f;
        float moved = Vector3.Distance(transform.position, _stuckLastPos);
        _stuckLastPos = transform.position;

        if (moved < 0.35f)
            _stuckSeconds += 1f;
        else
            _stuckSeconds = Mathf.Max(0f, _stuckSeconds - 0.6f);
    }

    private void TryJump()
    {
        if (!_isGrounded || _jumpTimer > 0f)
            return;

        _jumpTimer = _jumpCooldown;
        Vector3 velocity = _rb.linearVelocity;
        if (velocity.y < 0f)
            velocity.y = 0f;
        velocity.y = _jumpVelocity;
        _rb.linearVelocity = velocity;
        _animator?.SetTrigger(JumpTriggerHash);
    }

    private void OnGoalReached()
    {
        switch (_currentGoal)
        {
            case NpcMetaGoal.AssistTeammate:
                // 味方に合流したら少し待って次の判断へ。
                _nextThinkTimer = 0.3f;
                break;

            case NpcMetaGoal.ReturnRelic:
                // 返却地点到達時は遺物ドロップ判定を優先。
                TryHandleRelicInteraction();
                _nextThinkTimer = 0f;
                break;

            default:
                _nextThinkTimer = 0f;
                break;
        }
    }

    private void CleanupMissingTargets()
    {
        if (_targetRelic != null && _targetRelic.IsBeingCarried && _targetRelic != _carriedRelic)
            _targetRelic = null;
        if (_targetRelicBase != null && _targetRelicBase.IsDestroyed)
            _targetRelicBase = null;

        if (_assistTarget != null && !_assistTarget.gameObject.activeInHierarchy)
            _assistTarget = null;

        if (_carriedRelic == null && _currentGoal == NpcMetaGoal.ReturnRelic)
            _nextThinkTimer = 0f;
    }

    private bool ShouldForceReplan()
    {
        if (_stuckSeconds >= 2f)
            return true;

        if (_currentGoal == NpcMetaGoal.SecureRelic && (_targetRelic == null || _targetRelic.IsBeingCarried))
            return _targetRelicBase == null;

        if (_currentGoal == NpcMetaGoal.AssistTeammate && _assistTarget == null)
            return true;

        return false;
    }

    private float ComputeThreatLevel()
    {
        float threat = 0f;

        if (!_isGrounded)
            threat += Mathf.Clamp01(-_rb.linearVelocity.y / 10f) * 0.55f;

        if (_maxHp > 0.001f)
            threat += Mathf.Clamp01((0.4f - (_currentHp / _maxHp)) * 1.7f);

        Vector3 forward = transform.forward;
        if (IsCliffAhead(forward))
            threat += 0.25f;

        Transform nearestHazard = FindNearestHazard(_hazardAvoidRadius);
        if (nearestHazard != null)
        {
            float d = Vector3.Distance(transform.position, nearestHazard.position);
            threat += Mathf.Clamp01(1f - (d / Mathf.Max(0.01f, _hazardAvoidRadius))) * 0.45f;
        }

        threat += Mathf.Clamp01(_stuckSeconds / 5f) * 0.2f;
        return Mathf.Clamp01(threat);
    }

    private float ComputeExpeditionUrgency()
    {
        if (GameServices.Expedition == null)
            return 0.3f;

        return GameServices.Expedition.Phase switch
        {
            ExpeditionPhase.Basecamp => 0.15f,
            ExpeditionPhase.Climbing => 0.55f,
            ExpeditionPhase.Returning => 0.9f,
            ExpeditionPhase.Result => 0.1f,
            _ => 0.3f
        };
    }

    private Vector3 ApplyHazardAvoidance(Vector3 dir)
    {
        Transform nearest = FindNearestHazard(_hazardAvoidRadius);
        if (nearest == null)
            return dir;

        Vector3 away = transform.position - nearest.position;
        away.y = 0f;
        float distance = away.magnitude;
        if (distance <= 0.01f)
            return dir;

        float weight = 1f - Mathf.Clamp01(distance / Mathf.Max(0.01f, _hazardAvoidRadius));
        Vector3 blended = (dir + away.normalized * (weight * 1.4f)).normalized;
        return blended.sqrMagnitude > 0.0001f ? blended : dir;
    }

    private Vector3 ApplyObstacleAvoidance(Vector3 dir)
    {
        var ray = new Ray(transform.position + Vector3.up * 0.5f, dir);
        if (!Physics.Raycast(ray, out RaycastHit hit, 1.2f, _groundLayer, QueryTriggerInteraction.Ignore))
            return dir;

        Vector3 tangent = Vector3.Cross(Vector3.up, hit.normal).normalized;
        if (Vector3.Dot(tangent, dir) < 0f)
            tangent = -tangent;

        TryJump();
        Vector3 adjusted = (dir + tangent * 0.85f).normalized;
        return adjusted.sqrMagnitude > 0.0001f ? adjusted : dir;
    }

    private bool IsCliffAhead(Vector3 dir)
    {
        Vector3 aheadOrigin = transform.position + dir * 0.9f + Vector3.up * 0.25f;
        return !Physics.Raycast(
            aheadOrigin,
            Vector3.down,
            2.6f,
            _groundLayer,
            QueryTriggerInteraction.Ignore);
    }

    private Vector3 PickExploreWaypoint()
    {
        Vector3 best = _homePos;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < 10; i++)
        {
            Vector2 xz = Random.insideUnitCircle * _exploreRadius;
            Vector3 candidate = new Vector3(
                _homePos.x + xz.x,
                transform.position.y + Random.Range(-2f, 7f),
                _homePos.z + xz.y);

            if (!TryProjectToGround(candidate, out Vector3 grounded))
                continue;

            float altitudeGain = grounded.y - transform.position.y;
            float distFromCurrent = Vector3.Distance(transform.position, grounded);
            float hazardPenalty = 0f;

            Transform nearbyHazard = FindNearestHazardFrom(grounded, _hazardAvoidRadius);
            if (nearbyHazard != null)
            {
                float hd = Vector3.Distance(grounded, nearbyHazard.position);
                hazardPenalty = 1f - Mathf.Clamp01(hd / Mathf.Max(0.01f, _hazardAvoidRadius));
            }

            float score = (altitudeGain * 0.35f)
                          + (Mathf.Clamp01(distFromCurrent / _exploreRadius) * 0.25f)
                          - (hazardPenalty * 0.8f);

            if (score > bestScore)
            {
                bestScore = score;
                best = grounded;
            }
        }

        return best;
    }

    private bool TryProjectToGround(Vector3 worldPoint, out Vector3 groundedPoint)
    {
        Vector3 rayOrigin = worldPoint + Vector3.up * 12f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 28f, _groundLayer, QueryTriggerInteraction.Ignore))
        {
            groundedPoint = hit.point;
            return true;
        }

        groundedPoint = worldPoint;
        return false;
    }

    private void EnsureGroundMaskValid()
    {
        int mask = _groundLayer.value;
        int defaultLayer = LayerMask.NameToLayer("Default");
        int groundLayer = LayerMask.NameToLayer("Ground");
        int playerLayer = LayerMask.NameToLayer("Player");

        bool missingDefault = defaultLayer >= 0 && (mask & (1 << defaultLayer)) == 0;
        bool missingGround = groundLayer >= 0 && (mask & (1 << groundLayer)) == 0;
        bool includesPlayer = playerLayer >= 0 && (mask & (1 << playerLayer)) != 0;
        bool includesIgnoreRaycast = (mask & (1 << 2)) != 0;

        if (missingDefault || missingGround || includesPlayer || includesIgnoreRaycast)
            ResolveGroundMask();
    }

    private void ResolveGroundMask()
    {
        // 現在値に依存せず、毎回マスクを再構成して不正状態を自己修復する。
        int mask = Physics.DefaultRaycastLayers;

        int defaultLayer = LayerMask.NameToLayer("Default");
        if (defaultLayer >= 0)
            mask |= 1 << defaultLayer;

        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0)
            mask |= 1 << groundLayer;

        // 自己コライダー誤検知を避けるため、Player レイヤーだけ除外する。
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0)
            mask &= ~(1 << playerLayer);

        // Ignore Raycast は常に除外。
        mask &= ~(1 << 2);
        _groundLayer = mask;
    }

    private void EnsureScoreRegistration()
    {
        IScoreService score = GameServices.Score;
        if (score == null)
            return;

        score.RegisterPlayer(GetInstanceID(), gameObject.name);
    }

    private RelicCarrier FindNearestRelicCandidate()
    {
        float best = Mathf.Infinity;
        RelicCarrier bestRelic = null;
        for (int i = 0; i < _relicBuffer.Count; i++)
        {
            RelicCarrier relic = _relicBuffer[i];
            if (relic == null)
                continue;

            if (relic.IsBeingCarried && relic != _carriedRelic)
                continue;

            float d = Vector3.Distance(transform.position, relic.transform.position);
            if (d < best)
            {
                best = d;
                bestRelic = relic;
            }
        }

        return bestRelic;
    }

    private RelicBase FindNearestRelicBaseCandidate()
    {
        float best = Mathf.Infinity;
        RelicBase bestRelic = null;
        for (int i = 0; i < _relicBaseBuffer.Count; i++)
        {
            RelicBase relic = _relicBaseBuffer[i];
            if (relic == null || relic.IsDestroyed || relic.IsHeld)
                continue;

            float d = Vector3.Distance(transform.position, relic.transform.position);
            if (d < best)
            {
                best = d;
                bestRelic = relic;
            }
        }

        return bestRelic;
    }

    private ShelterZone FindNearestShelter()
    {
        float best = Mathf.Infinity;
        ShelterZone bestShelter = null;
        for (int i = 0; i < _shelterBuffer.Count; i++)
        {
            ShelterZone shelter = _shelterBuffer[i];
            if (shelter == null)
                continue;

            float d = Vector3.Distance(transform.position, shelter.transform.position);
            if (d < best)
            {
                best = d;
                bestShelter = shelter;
            }
        }

        return bestShelter;
    }

    private PlayerHealthSystem FindNearestAlivePlayer()
    {
        IReadOnlyList<PlayerHealthSystem> players = PlayerHealthSystem.RegisteredPlayers;
        float best = Mathf.Infinity;
        PlayerHealthSystem bestPlayer = null;

        for (int i = 0; i < players.Count; i++)
        {
            PlayerHealthSystem player = players[i];
            if (player == null || player.IsDead || player.gameObject == gameObject)
                continue;

            float d = Vector3.Distance(transform.position, player.transform.position);
            if (d < best)
            {
                best = d;
                bestPlayer = player;
            }
        }

        return bestPlayer;
    }

    private Transform FindNearestHazard(float withinDistance)
    {
        return FindNearestHazardFrom(transform.position, withinDistance);
    }

    private Transform FindNearestHazardFrom(Vector3 from, float withinDistance)
    {
        float best = withinDistance;
        Transform nearest = null;
        for (int i = 0; i < _hazardBuffer.Count; i++)
        {
            Transform hazard = _hazardBuffer[i];
            if (hazard == null)
                continue;

            float d = Vector3.Distance(from, hazard.position);
            if (d < best)
            {
                best = d;
                nearest = hazard;
            }
        }

        return nearest;
    }

    private void UpdateAnimator(bool moving, bool grounded)
    {
        if (_animator == null)
            return;

        float targetSpeed = moving ? 1f : 0f;
        _currentSpeed = Mathf.SmoothDamp(_currentSpeed, targetSpeed, ref _speedVelocity, _animSmoothTime);

        _animator.SetFloat(SpeedHash, _currentSpeed);
        _animator.SetFloat(SpeedBlendHash, 0f);
        _animator.SetBool(IsGroundedHash, grounded);
    }

    public void TakeDamage(float amount)
    {
        if (_isDead || amount <= 0f)
            return;

        _currentHp = Mathf.Max(0f, _currentHp - amount);
        if (_currentHp <= 0f)
            Die();
    }

    private void Die()
    {
        if (_isDead)
            return;

        _isDead = true;
        _isMoving = false;
        _rb.isKinematic = true;
        _rb.linearVelocity = Vector3.zero;

        if (_carriedRelic != null)
        {
            _carriedRelic.Drop(Vector3.zero);
            _carriedRelic = null;
        }

        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(_respawnDelay);

        transform.position = _homePos;
        _currentHp = _maxHp;
        _isDead = false;
        _rb.isKinematic = false;
        _rb.linearVelocity = Vector3.zero;
        _stuckSeconds = 0f;
        _stuckSampleTimer = 0f;
        _targetRelic = null;
        _carriedRelic = null;
        _assistTarget = null;

        RefreshSensors(force: true);
        ThinkAndSetGoal(force: true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(Application.isPlaying ? _homePos : transform.position, _exploreRadius);

        if (_hasGoalPosition)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, _goalPosition);
            Gizmos.DrawWireSphere(_goalPosition, 0.35f);
        }
    }
}
