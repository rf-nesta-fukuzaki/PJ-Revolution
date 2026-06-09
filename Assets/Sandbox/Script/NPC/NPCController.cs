using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

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
    [Tooltip("他 NPC とこの距離[m]以内で反発し間隔を空ける（拠点等で団子にならない）。")]
    [SerializeField] private float _separationRadius = 2.2f;
    [Tooltip("最寄りプレイヤー(一人称カメラ)からはこの距離[m]まで離れる。NPC 同士より広くして『巨大な顔』で画面を覆うのを防ぐ。")]
    [SerializeField] private float _playerPersonalSpace = 3.2f;
    [Tooltip("ローカルカメラからこの距離[m]より近いと、自分のボディ描画を一時的に隠す（影は維持）。" +
             "至近で重なって『巨大な顔』が視界を覆うのを防ぐ。中距離では仲間として通常表示。")]
    [SerializeField] private float _cameraHardClearance = 1.8f;
    [SerializeField] private float _separationStrength = 1.1f;

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
    private bool _scoreRegistered;

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
    private Transform _nearestAllyTf; // 分離ステアリング用にキャッシュした最寄りプレイヤー
    private Transform _localCameraTf; // ローカル一人称カメラ。NPC がこの視界を覆わないよう反発／至近フェードに使う。
    private readonly List<Renderer> _bodyRenderers = new(); // 至近フェード対象のボディ描画（影は維持）
    private bool _bodyHiddenForCamera; // 現在カメラ至近フェードでボディを隠しているか
    private int _knownRendererCount = -1; // 子レンダラー数の変化検知（コスメ装着等で再走査）

    // 全 NPC の登録簿（分離ステアリングで互いの位置を参照。FindObjectsByType の毎フレーム呼び出しを避ける）。
    private static readonly List<NPCController> s_all = new List<NPCController>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => s_all.Clear();

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

    private void OnEnable()  { if (!s_all.Contains(this)) s_all.Add(this); }
    private void OnDisable() { s_all.Remove(this); }

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

    /// <summary>
    /// 地形整合後に徘徊/リスポーン基点(_homePos)を現在の接地位置へ再設定する。
    /// CombinedTerrainConformer から呼ぶ。Awake/Start 時点ではフラット前提の旧座標(地形下)に
    /// なっているため、これを呼ばないと死亡リスポーンで地形の下に湧いてしまう。
    /// </summary>
    public void ReanchorHome(Vector3 worldPos)
    {
        _homePos = worldPos;
        _stuckLastPos = worldPos;
        _stuckSeconds = 0f;
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

        StepBehaviour();
    }

    private void StepBehaviour()
    {
        if (!_hasGoalPosition)
        {
            // 目標が無いアイドル状態でも、近接する他エージェント/プレイヤーからは押し離す。
            // これが無いと、仲間 NPC が一人称カメラの至近に居座り「巨大な顔」で画面を覆う。
            Vector3 idleSep = ComputeSeparationPush();
            if (idleSep.sqrMagnitude > 0.0001f)
            {
                _rb.MovePosition(_rb.position + idleSep.normalized * (_moveSpeed * 0.5f * Time.fixedDeltaTime));
                _isMoving = true;
            }
            else
            {
                _isMoving = false;
            }
            return;
        }

        Vector3 sep = ComputeSeparationPush();

        Vector3 toGoal = _goalPosition - _rb.position;
        toGoal.y = 0f;
        float distance = toGoal.magnitude;
        if (distance <= _waypointTolerance)
        {
            // ゴール到達。近接する他エージェントがあれば押し合って間隔を空ける（拠点等での重なり防止）。
            if (sep.sqrMagnitude > 0.0001f)
                _rb.MovePosition(_rb.position + sep.normalized * (_moveSpeed * 0.5f * Time.fixedDeltaTime));
            _isMoving = false;
            OnGoalReached();
            return;
        }

        Vector3 dir = toGoal / Mathf.Max(distance, 0.0001f);
        dir = ApplyHazardAvoidance(dir);
        dir = ApplyObstacleAvoidance(dir);
        if (sep.sqrMagnitude > 0.0001f)
            dir = (dir + sep * _separationStrength).normalized;

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

    /// <summary>
    /// ローカル一人称カメラの至近（_cameraHardClearance 未満）にいる間は、自分のボディ描画を
    /// 一時的に隠す（影は維持）。物理で押し出すと追従ゴールとの綱引きで境界に整列して
    /// 「壁」になるため、押さずに『重なって視界を覆うときだけ消す』方式にする。
    /// 仲間として見たい中距離(クリアランス以遠)では通常表示に戻す。冪等。
    /// </summary>
    private void UpdateCameraOcclusionFade()
    {
        if (_cameraHardClearance <= 0f) return;

        // フェード判定はローカル一人称カメラ（＝Game View を描画している Camera.main）を毎フレーム直接参照する。
        // センサー用にキャッシュした _localCameraTf は他カメラを指す場合があり、ここでは使わない。
        var cam = Camera.main;
        if (cam == null) return;

        Vector3 d = _rb.position - cam.transform.position; d.y = 0f;
        bool shouldHide = d.sqrMagnitude < _cameraHardClearance * _cameraHardClearance;

        // 近接中は毎フレーム再適用する（コスメ装着で char1 が後から子に追加されても確実に隠すため）。
        // 遠ざかったときだけ一度復帰させる。状態変化が無く遠方なら何もしない（軽量）。
        if (shouldHide)
        {
            EnsureBodyRendererCache();
            ApplyBodyShadowMode(ShadowCastingMode.ShadowsOnly);
            _bodyHiddenForCamera = true;
        }
        else if (_bodyHiddenForCamera)
        {
            EnsureBodyRendererCache();
            ApplyBodyShadowMode(ShadowCastingMode.On);
            _bodyHiddenForCamera = false;
        }
    }

    private void ApplyBodyShadowMode(ShadowCastingMode mode)
    {
        for (int i = 0; i < _bodyRenderers.Count; i++)
        {
            var r = _bodyRenderers[i];
            if (r != null && r.shadowCastingMode != mode)
                r.shadowCastingMode = mode;
        }
    }

    /// <summary>
    /// ボディレンダラー一覧を取得・キャッシュする。子レンダラー数が変化したら再走査する
    /// （NPC は起動時はルートのプリミティブのみで、char1 等のモデルが後から子に追加されるため）。
    /// </summary>
    private void EnsureBodyRendererCache()
    {
        var found = GetComponentsInChildren<Renderer>(true);
        if (found.Length == _knownRendererCount && _bodyRenderers.Count > 0) return;

        _knownRendererCount = found.Length;
        _bodyRenderers.Clear();
        for (int i = 0; i < found.Length; i++)
        {
            var r = found[i];
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
                _bodyRenderers.Add(r);
        }
    }

    private void LateUpdate()
    {
        if (_isDead) return;
        UpdateCameraOcclusionFade();
    }

    /// <summary>
    /// 近接する他エージェント(他NPC＋最寄りプレイヤー)から離れる XZ 反発ベクトルを返す（重なり防止のステアリング）。
    /// _separationRadius 以内の相手ごとに「離れる向き×近さ」を加算。0=近接相手なし。
    /// 共有ゴール（拠点/ReturnZone 等）に集まっても _waypointTolerance の範囲で互いを押し離し、団子状の重なりを防ぐ。
    /// </summary>
    private Vector3 ComputeSeparationPush()
    {
        Vector3 me = _rb.position;
        float r = Mathf.Max(0.1f, _separationRadius);
        Vector3 push = Vector3.zero;

        for (int i = 0; i < s_all.Count; i++)
        {
            var o = s_all[i];
            if (o == null || o == this) continue;
            Vector3 d = me - o.transform.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist > 0.001f && dist < r)
                push += d / dist * (1f - dist / r);
        }
        // ローカル一人称カメラからは NPC 同士より広い個人空間を確保する（「巨大な顔」で画面を覆うのを防ぐ）。
        // RegisteredPlayers には他 NPC も含まれ最寄り＝人間とは限らないため、カメラ位置を直接の反発源にする。
        float pr = Mathf.Max(r, _playerPersonalSpace);
        if (_localCameraTf != null)
        {
            Vector3 d = me - _localCameraTf.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist > 0.001f && dist < pr)
                push += d / dist * (1f - dist / pr) * 1.6f; // カメラ前は強めに押し出す
        }
        else if (_nearestAllyTf != null)
        {
            Vector3 d = me - _nearestAllyTf.position; d.y = 0f;
            float dist = d.magnitude;
            if (dist > 0.001f && dist < pr)
                push += d / dist * (1f - dist / pr);
        }
        return push;
    }

    private void ThinkAndSetGoal(bool force)
    {
        RelicCarrier nearestRelic = FindNearestRelicCandidate();
        RelicBase nearestRelicBase = FindNearestRelicBaseCandidate();
        ShelterZone nearestShelter = FindNearestShelter();
        PlayerHealthSystem nearestAlly = FindNearestAlivePlayer();
        _nearestAllyTf = nearestAlly != null ? nearestAlly.transform : null; // 分離用にキャッシュ
        // ローカル一人称カメラを更新（無効化されたら取り直す）。NPC がこの視界を覆わないようにする。
        if (_localCameraTf == null || !_localCameraTf.gameObject.activeInHierarchy)
        {
            var cam = Camera.main;
            _localCameraTf = cam != null ? cam.transform : null;
        }
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

        // 全 NPC で共有するスロットル付きキャッシュを更新（実走査はグローバルに 1 回）。
        // 以前は NPC ごとに約 11 回の FindObjectsByType を毎秒実行しており、NPC 数に比例した
        // 全シーン走査が周期的フレームスパイクの主因だった。ここではキャッシュをコピーするだけ。
        NpcSensorCache.EnsureFresh(_sensorRefreshInterval);

        _relicBuffer.Clear();
        _relicBuffer.AddRange(NpcSensorCache.Carriers);

        _relicBaseBuffer.Clear();
        _relicBaseBuffer.AddRange(NpcSensorCache.Bases);

        _shelterBuffer.Clear();
        _shelterBuffer.AddRange(NpcSensorCache.Shelters);

        _hazardBuffer.Clear();
        _hazardBuffer.AddRange(NpcSensorCache.Hazards);

        if (_returnZoneTransform == null || force)
            _returnZoneTransform = NpcSensorCache.ReturnZone;
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
        if (_scoreRegistered) return;

        IScoreService score = GameServices.Score;
        if (score == null) return; // サービスが Start 後に生成される構成では次フレーム以降に再試行。

        // 登録は冪等（ScoreTracker 側で id 重複は無視）。一度成功したら以降は
        // gameObject.name の毎フレーム文字列確保ごとスキップする。
        score.RegisterPlayer(GetInstanceID(), gameObject.name);
        _scoreRegistered = true;
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
