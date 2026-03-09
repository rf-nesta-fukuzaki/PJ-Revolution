using UnityEngine;

/// <summary>
/// 洞窟トカゲ型モンスターのステートマシン AI。
///
/// [ステート一覧]
///   Sleeping  : スポーン位置で静止待機。IsPlayerDetected() で Alerted へ。
///   Alerted   : 気づき状態（_alertDuration 秒の猶予）。左右微振動演出。
///   Chasing   : 最も近い有効プレイヤーを地上追尾。しゃがみ/匍匐中は速度 1.5 倍。
///   Attacking : 攻撃射程内で _attackCooldown 間隔のダメージ付与。
///   Fleeing   : 点灯たいまつから反対方向へ逃走（fleeRadius 4m）。
///   Returning : スポーン位置（_homePosition）へ帰還。
///
/// [BatAI との主な違い]
///   - 地上移動型: 下方向 Raycast で Y 座標を地面にスナップする。
///   - 速度: 追尾 3m/s, 逃走 4m/s, 帰還 2m/s（コウモリより低速）。
///   - 攻撃: ダメージ 10, クールダウン 2 秒, 射程 1.8m。
///   - たいまつ反応: fleeRadius 4m（コウモリより遠い）。
///   - しゃがみ/匍匐検出: 追尾ターゲットの PlayerMovement を参照して速度倍増。
/// </summary>
[RequireComponent(typeof(BatPerception))]
public class LizardAI : MonoBehaviour
{
    // ─────────────── Inspector (移動) ───────────────

    [Header("🦎 トカゲ 移動")]
    [Tooltip("追尾速度（m/s）")]
    [Range(1f, 10f)]
    [SerializeField] private float _chaseSpeed = 3f;

    [Tooltip("逃走速度（m/s）")]
    [Range(1f, 15f)]
    [SerializeField] private float _fleeSpeed = 4f;

    [Tooltip("帰還速度（m/s）")]
    [Range(1f, 10f)]
    [SerializeField] private float _returnSpeed = 2f;

    [Tooltip("しゃがみ/匍匐中プレイヤーへの追尾速度倍率")]
    [Range(1f, 3f)]
    [SerializeField] private float _crouchChaseMultiplier = 1.5f;

    [Tooltip("地面スナップ用 Raycast 距離（m）。短距離なら天井問題は発生しない")]
    [Range(0.5f, 3f)]
    [SerializeField] private float _groundSnapDistance = 2f;

    // ─────────────── Inspector (攻撃) ───────────────

    [Header("🦎 トカゲ 攻撃")]
    [Tooltip("攻撃射程（m）")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _attackRange = 1.8f;

    [Tooltip("攻撃間隔（秒）")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _attackCooldown = 2f;

    [Tooltip("1 回の攻撃ダメージ")]
    [Range(1f, 50f)]
    [SerializeField] private float _attackDamage = 10f;

    // ─────────────── Inspector (たいまつ反応) ───────────────

    [Header("🦎 トカゲ たいまつ反応")]
    [Tooltip("この距離以内の点灯たいまつから逃走（m）")]
    [Range(1f, 15f)]
    [SerializeField] private float _fleeRadius = 4f;

    [Tooltip("逃走終了距離（m）。ここまで離れたら Returning へ移行")]
    [Range(5f, 30f)]
    [SerializeField] private float _fleeDistance = 15f;

    // ─────────────── Inspector (タイマー) ───────────────

    [Header("🦎 トカゲ タイマー")]
    [Tooltip("Alerted 状態の持続時間（秒）")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _alertDuration = 1.5f;

    [Tooltip("たいまつ消灯後に Chasing 再開するまでの待機時間（秒）")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _fleeResumeDelay = 2f;

    // ─────────────── Inspector (デバッグ) ───────────────

    [Header("🔧 デバッグ")]
    [Tooltip("現在の状態（読み取り専用）")]
    [SerializeField] private LizardState _debugCurrentState;

    // ─────────────── 状態 ───────────────

    /// <summary>現在の AI ステート。</summary>
    public LizardState CurrentState { get; private set; } = LizardState.Sleeping;

    // ─────────────── 内部変数 ───────────────

    private BatPerception _perception;
    private Vector3       _homePosition;
    private Vector3       _alertStartPosition;

    private float _attackTimer;
    private float _alertTimer;
    private float _fleeResumeTimer;
    private bool  _fleeResumeWaiting;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        _perception   = GetComponent<BatPerception>();
        _homePosition = transform.position;
    }

    private void Start()
    {
        _homePosition      = transform.position;
        CurrentState       = LizardState.Sleeping;
        _debugCurrentState = LizardState.Sleeping;
    }

    private void Update()
    {
        _debugCurrentState = CurrentState;

        switch (CurrentState)
        {
            case LizardState.Sleeping:  UpdateSleeping();  break;
            case LizardState.Alerted:   UpdateAlerted();   break;
            case LizardState.Chasing:   UpdateChasing();   break;
            case LizardState.Attacking: UpdateAttacking(); break;
            case LizardState.Fleeing:   UpdateFleeing();   break;
            case LizardState.Returning: UpdateReturning(); break;
        }
    }

    // ─────────────── ステート更新 ───────────────

    private void UpdateSleeping()
    {
        if (_perception.IsPlayerDetected())
            SetState(LizardState.Alerted);
    }

    private void UpdateAlerted()
    {
        _alertTimer += Time.deltaTime;

        // 左右微振動演出（地上らしく X 軸のみ揺らす）
        float vibration = Mathf.Sin(Time.time * 8f) * 0.08f;
        transform.position = new Vector3(
            _alertStartPosition.x + vibration,
            _alertStartPosition.y,
            _alertStartPosition.z);

        if (!_perception.IsPlayerDetected())
        {
            SetState(LizardState.Sleeping);
            return;
        }

        if (_alertTimer >= _alertDuration)
        {
            if (_perception.GetNearestLitTorchDistance() < _fleeRadius)
                SetState(LizardState.Fleeing);
            else
                SetState(LizardState.Chasing);
        }
    }

    private void UpdateChasing()
    {
        if (_perception.NoValidTargets())                            { SetState(LizardState.Returning); return; }
        if (_perception.GetNearestLitTorchDistance() < _fleeRadius) { SetState(LizardState.Fleeing);   return; }
        if (_perception.GetNearestPlayerDistance() < _attackRange)  { SetState(LizardState.Attacking); return; }

        Transform target = _perception.GetNearestPlayer();
        if (target == null) { SetState(LizardState.Returning); return; }

        MoveTowardTarget(target);
    }

    private void UpdateAttacking()
    {
        if (_perception.NoValidTargets())                            { SetState(LizardState.Returning); return; }
        if (_perception.GetNearestLitTorchDistance() < _fleeRadius) { SetState(LizardState.Fleeing);   return; }

        Transform target = _perception.GetNearestPlayer();
        if (target == null) { SetState(LizardState.Returning); return; }

        if (_perception.GetNearestPlayerDistance() > _attackRange)
        {
            SetState(LizardState.Chasing);
            return;
        }

        MoveTowardTarget(target);

        _attackTimer += Time.deltaTime;
        if (_attackTimer >= _attackCooldown)
        {
            _attackTimer = 0f;
            PerformBite();
        }
    }

    private void UpdateFleeing()
    {
        Transform nearestTorch = _perception.GetNearestLitTorch();

        if (nearestTorch == null)
        {
            if (!_fleeResumeWaiting)
            {
                _fleeResumeWaiting = true;
                _fleeResumeTimer   = 0f;
            }

            _fleeResumeTimer += Time.deltaTime;
            if (_fleeResumeTimer >= _fleeResumeDelay)
            {
                _fleeResumeWaiting = false;
                SetState(LizardState.Chasing);
            }
            return;
        }

        _fleeResumeWaiting = false;
        _fleeResumeTimer   = 0f;

        if (_perception.GetNearestLitTorchDistance() > _fleeDistance)
        {
            SetState(LizardState.Returning);
            return;
        }

        Vector3 fleeDir = (transform.position - nearestTorch.position);
        fleeDir.y = 0f; // 地上なので Y 成分を除去
        if (fleeDir.sqrMagnitude < 0.001f)
            fleeDir = transform.forward; // フォールバック
        fleeDir = fleeDir.normalized;

        MoveGrounded(fleeDir * _fleeSpeed);
        FaceDirection(fleeDir);
    }

    private void UpdateReturning()
    {
        if (_perception.IsPlayerDetected())
        {
            SetState(LizardState.Alerted);
            return;
        }

        Vector3 toHome = _homePosition - transform.position;
        toHome.y = 0f; // XZ 平面での帰還

        if (toHome.sqrMagnitude < 0.5f * 0.5f)
        {
            transform.position = SnapToGround(_homePosition);
            SetState(LizardState.Sleeping);
            return;
        }

        Vector3 dir = toHome.normalized;
        MoveGrounded(dir * _returnSpeed);
        FaceDirection(dir);
    }

    // ─────────────── ステート遷移 ───────────────

    private void SetState(LizardState next)
    {
        if (CurrentState == next) return;

        CurrentState = next;
        _debugCurrentState = next;
        InitState(next);
    }

    private void InitState(LizardState state)
    {
        switch (state)
        {
            case LizardState.Alerted:
                _alertTimer         = 0f;
                _alertStartPosition = transform.position;
                break;

            case LizardState.Attacking:
                _attackTimer = 0f;
                break;

            case LizardState.Fleeing:
                _fleeResumeWaiting = false;
                _fleeResumeTimer   = 0f;
                break;
        }
    }

    // ─────────────── 攻撃 ───────────────

    private void PerformBite()
    {
        var targetStats = _perception.GetNearestPlayerStats();
        if (targetStats == null) return;
        if (targetStats.IsDowned)  return;

        targetStats.ApplyStatModification(StatType.Health, -_attackDamage);

        Debug.Log($"[LizardAI] 噛みつき！ -{_attackDamage} ダメージ / 残HP: {targetStats.Health:F0}");
    }

    // ─────────────── 移動補助 ───────────────

    /// <summary>
    /// ターゲットへ向かって地上移動する。
    /// しゃがみ/匍匐中のターゲットには速度倍率を適用する。
    /// </summary>
    private void MoveTowardTarget(Transform target)
    {
        // XZ 平面での方向（Y は地面スナップで管理）
        Vector3 flatDir = target.position - transform.position;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude < 0.001f) return;
        flatDir = flatDir.normalized;

        float speed = _chaseSpeed * GetCrouchMultiplier(target);

        MoveGrounded(flatDir * speed);
        FaceDirection(flatDir);
    }

    /// <summary>
    /// ターゲットの PlayerMovement を参照してしゃがみ/匍匐倍率を返す。
    /// </summary>
    private float GetCrouchMultiplier(Transform target)
    {
        if (target == null) return 1f;

        var pm = target.GetComponent<PlayerMovement>();
        if (pm == null) return 1f;

        return (pm.IsCrouching || pm.IsProne) ? _crouchChaseMultiplier : 1f;
    }

    /// <summary>
    /// 指定速度ベクトルで移動し、下方向 Raycast で Y を地面にスナップする。
    /// Raycast が失敗した場合は Y をそのまま維持するフォールバック付き。
    /// </summary>
    private void MoveGrounded(Vector3 horizontalVelocity)
    {
        Vector3 newPos = transform.position + horizontalVelocity * Time.deltaTime;
        newPos = SnapToGround(newPos);
        transform.position = newPos;
    }

    /// <summary>
    /// 下方向 Raycast でワールド Y 座標を地面にスナップする。
    /// Raycast が失敗した場合は元の Y を維持する。
    /// </summary>
    private Vector3 SnapToGround(Vector3 position)
    {
        // 少し上から撃って自分の足元地面を検出する（短距離なので天井問題は起きにくい）
        Vector3 origin = new Vector3(position.x, position.y + 0.5f, position.z);

        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, _groundSnapDistance + 0.5f,
            ~0, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y;
        }
        // Raycast 失敗時は Y をそのまま維持（フォールバック）

        return position;
    }

    private void FaceDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f) return;
        Quaternion target = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 5f * Time.deltaTime);
    }

    // ─────────────── Gizmos ───────────────

    private void OnDrawGizmosSelected()
    {
        var p = GetComponent<BatPerception>();
        if (p == null) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, p.起床距離);

        Gizmos.color = new Color(1f, 0.5f, 0f);
        Gizmos.DrawWireSphere(transform.position, p.接近起床距離);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, p.追尾開始距離);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _attackRange);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _fleeRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, _fleeDistance);
    }
}

/// <summary>トカゲ AI のステート定義。</summary>
public enum LizardState : byte
{
    Sleeping,
    Alerted,
    Chasing,
    Attacking,
    Fleeing,
    Returning,
}
