using UnityEngine;

/// <summary>
/// コウモリ型モンスターのステートマシン AI。
///
/// [ステート一覧]
///   Sleeping  : スポーン位置で静止待機。IsPlayerDetected() で Alerted へ。
///   Alerted   : 気づき状態（_alertDuration 秒の猶予）。sin 振動演出。
///   Chasing   : 最も近い有効プレイヤーを追尾。_alertCallRadius 内の仲間を起こす。
///   Attacking : 攻撃射程内で _attackCooldown 間隔のダメージ付与。
///   Fleeing   : 点灯たいまつから反対方向へ逃走。
///   Returning : スポーン位置（_homePosition）へ帰還。
///
/// [群れ呼び出し]
///   Alerted → Chasing 遷移時に _alertCallRadius 内の他の BatAI へ WakeUp() を呼ぶ。
///
/// [回避機動]
///   個体固有の速度揺らぎ（±20%）と横揺れ周波数で、複数個体が一列に並ばない群れ感を演出。
/// </summary>
[RequireComponent(typeof(BatPerception))]
public class BatAI : MonoBehaviour
{
    // ─────────────── Inspector (移動) ───────────────

    [Header("🦇 移動")]
    [Tooltip("追尾速度（m/s）")]
    [Range(1f, 15f)]
    [SerializeField] private float _chaseSpeed = 5f;

    [Tooltip("逃走速度（m/s）")]
    [Range(1f, 20f)]
    [SerializeField] private float _fleeSpeed = 7f;

    [Tooltip("帰還速度（m/s）")]
    [Range(1f, 10f)]
    [SerializeField] private float _returnSpeed = 3f;

    [Tooltip("Y 軸補間速度（追尾時にターゲット頭上への滑らかな追従）")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _ySmoothing = 2f;

    // ─────────────── Inspector (群れ) ───────────────

    [Header("🐾 群れ呼び出し")]
    [Tooltip("Chasing 遷移時に WakeUp() を呼ぶ範囲（m）")]
    [Range(5f, 30f)]
    [SerializeField] private float _alertCallRadius = 15f;

    // ─────────────── Inspector (攻撃) ───────────────

    [Header("⚔️ 攻撃")]
    [Tooltip("攻撃射程（m）")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _attackRange = 1.5f;

    [Tooltip("攻撃間隔（秒）")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _attackCooldown = 1.5f;

    [Tooltip("1 回の攻撃ダメージ")]
    [Range(5f, 50f)]
    [SerializeField] private float _attackDamage = 15f;

    // ─────────────── Inspector (たいまつ反応) ───────────────

    [Header("🔥 たいまつ反応")]
    [Tooltip("この距離以内の点灯たいまつから逃走（m）")]
    [Range(1f, 10f)]
    [SerializeField] private float _fleeRadius = 3f;

    [Tooltip("逃走終了距離（m）。ここまで離れたら Returning へ移行")]
    [Range(5f, 30f)]
    [SerializeField] private float _fleeDistance = 15f;

    // ─────────────── Inspector (タイマー) ───────────────

    [Header("⏱️ タイマー")]
    [Tooltip("Alerted 状態の持続時間（秒）")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _alertDuration = 1.5f;

    [Tooltip("たいまつ消灯後に Chasing 再開するまでの待機時間（秒）")]
    [Range(0.5f, 5f)]
    [SerializeField] private float _fleeResumeDelay = 2f;

    // ─────────────── Inspector (デバッグ) ───────────────

    [Header("🔧 デバッグ")]
    [Tooltip("現在の状態（読み取り専用）")]
    [SerializeField] private BatState _debugCurrentState;

    // ─────────────── 後方互換フィールド (BatSpawner が参照) ───────────────

    [HideInInspector] public Transform   プレイヤーTransform;
    [HideInInspector] public SurvivalStats プレイヤーステータス;
    [HideInInspector] public TorchSystem プレイヤーたいまつ;

    // ─────────────── 状態 ───────────────

    /// <summary>現在の AI ステート。</summary>
    public BatState CurrentState { get; private set; } = BatState.Sleeping;

    // ─────────────── 内部変数 ───────────────

    private BatPerception _perception;
    private Vector3       _homePosition;
    private Vector3       _alertStartPosition;

    private float _attackTimer;
    private float _alertTimer;
    private float _fleeResumeTimer;
    private bool  _fleeResumeWaiting;

    // 回避機動: Awake で個体固有の値を生成
    private float _chaseSpeedMultiplier; // ±20% 揺らぎ
    private float _swerveFrequency;      // 横揺れ周波数（個体固有）

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        _perception   = GetComponent<BatPerception>();
        _homePosition = transform.position;

        // 個体固有の乱数パラメータを生成（GetInstanceID でシードが個体ごとに変わる）
        var rng = new System.Random(GetInstanceID());
        _chaseSpeedMultiplier = 0.8f + (float)rng.NextDouble() * 0.4f; // 0.8 〜 1.2（±20%）
        _swerveFrequency      = 1.5f + (float)rng.NextDouble() * 2.0f; // 1.5 〜 3.5 Hz
    }

    private void Start()
    {
        _homePosition  = transform.position;
        CurrentState   = BatState.Sleeping;
        _debugCurrentState = BatState.Sleeping;
    }

    private void Update()
    {
        _debugCurrentState = CurrentState;

        switch (CurrentState)
        {
            case BatState.Sleeping:  UpdateSleeping();  break;
            case BatState.Alerted:   UpdateAlerted();   break;
            case BatState.Chasing:   UpdateChasing();   break;
            case BatState.Attacking: UpdateAttacking(); break;
            case BatState.Fleeing:   UpdateFleeing();   break;
            case BatState.Returning: UpdateReturning(); break;
        }
    }

    // ─────────────── ステート更新 ───────────────

    private void UpdateSleeping()
    {
        if (_perception.IsPlayerDetected())
            SetState(BatState.Alerted);
    }

    private void UpdateAlerted()
    {
        _alertTimer += Time.deltaTime;

        float vibration = Mathf.Sin(Time.time * 10f) * 0.1f;
        transform.position = new Vector3(
            _alertStartPosition.x,
            _alertStartPosition.y + vibration,
            _alertStartPosition.z);

        if (!_perception.IsPlayerDetected())
        {
            SetState(BatState.Sleeping);
            return;
        }

        if (_alertTimer >= _alertDuration)
        {
            if (_perception.GetNearestLitTorchDistance() < _fleeRadius)
                SetState(BatState.Fleeing);
            else
                SetState(BatState.Chasing);
        }
    }

    private void UpdateChasing()
    {
        if (_perception.NoValidTargets())                            { SetState(BatState.Returning); return; }
        if (_perception.GetNearestLitTorchDistance() < _fleeRadius) { SetState(BatState.Fleeing);   return; }
        if (_perception.GetNearestPlayerDistance() < _attackRange)  { SetState(BatState.Attacking); return; }

        Transform target = _perception.GetNearestPlayer();
        if (target == null) { SetState(BatState.Returning); return; }

        MoveTowardTarget(target);
    }

    private void UpdateAttacking()
    {
        if (_perception.NoValidTargets())                            { SetState(BatState.Returning); return; }
        if (_perception.GetNearestLitTorchDistance() < _fleeRadius) { SetState(BatState.Fleeing);   return; }

        Transform target = _perception.GetNearestPlayer();
        if (target == null) { SetState(BatState.Returning); return; }

        if (_perception.GetNearestPlayerDistance() > _attackRange)
        {
            SetState(BatState.Chasing);
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
                SetState(BatState.Chasing);
            }
            return;
        }

        _fleeResumeWaiting = false;
        _fleeResumeTimer   = 0f;

        if (_perception.GetNearestLitTorchDistance() > _fleeDistance)
        {
            SetState(BatState.Returning);
            return;
        }

        Vector3 fleeDir = (transform.position - nearestTorch.position).normalized;
        transform.position += fleeDir * _fleeSpeed * Time.deltaTime;
        FaceDirection(fleeDir);
    }

    private void UpdateReturning()
    {
        if (_perception.IsPlayerDetected())
        {
            SetState(BatState.Alerted);
            return;
        }

        Vector3 toHome = _homePosition - transform.position;

        if (toHome.sqrMagnitude < 0.5f * 0.5f)
        {
            transform.position = _homePosition;
            SetState(BatState.Sleeping);
            return;
        }

        Vector3 dir = toHome.normalized;
        transform.position += dir * _returnSpeed * Time.deltaTime;
        FaceDirection(dir);
    }

    // ─────────────── ステート遷移 ───────────────

    private void SetState(BatState next)
    {
        if (CurrentState == next) return;

        CurrentState = next;
        _debugCurrentState = next;
        InitState(next);
    }

    private void InitState(BatState state)
    {
        switch (state)
        {
            case BatState.Alerted:
                _alertTimer         = 0f;
                _alertStartPosition = transform.position;
                break;

            case BatState.Chasing:
                CallNearbyBats();
                break;

            case BatState.Attacking:
                _attackTimer = 0f;
                break;

            case BatState.Fleeing:
                _fleeResumeWaiting = false;
                _fleeResumeTimer   = 0f;
                break;
        }
    }

    // ─────────────── 群れ呼び出し ───────────────

    /// <summary>
    /// _alertCallRadius 内の Sleeping 状態の他の BatAI を起こす。
    /// Alerted → Chasing 遷移時に一度だけ呼ばれる。
    /// </summary>
    private void CallNearbyBats()
    {
        var hits = Physics.OverlapSphere(transform.position, _alertCallRadius);
        foreach (var hit in hits)
        {
            var bat = hit.GetComponent<BatAI>();
            if (bat == null || bat == this) continue;
            bat.WakeUp();
        }
    }

    /// <summary>
    /// 外部（仲間の群れ呼び出し）から起こされたときの処理。
    /// Sleeping 状態のときのみ Alerted へ遷移する。
    /// </summary>
    public void WakeUp()
    {
        if (CurrentState == BatState.Sleeping)
            SetState(BatState.Alerted);
    }

    // ─────────────── 攻撃 ───────────────

    private void PerformBite()
    {
        var targetStats = _perception.GetNearestPlayerStats();

        if (targetStats == null)
            targetStats = プレイヤーステータス;

        if (targetStats == null)       return;
        if (targetStats.IsDowned)      return;

        targetStats.ApplyStatModification(StatType.Health, -_attackDamage);

        Debug.Log($"[BatAI] 噛みつき！ -{_attackDamage} ダメージ / 残HP: {targetStats.Health:F0}");
    }

    // ─────────────── 移動補助 ───────────────

    private void MoveTowardTarget(Transform target)
    {
        float targetY   = Mathf.Lerp(transform.position.y, target.position.y + 1f, _ySmoothing * Time.deltaTime);
        Vector3 flatDest = new Vector3(target.position.x, targetY, target.position.z);

        Vector3 dir = (flatDest - transform.position);
        if (dir.sqrMagnitude < 0.001f) return;

        // 回避機動: 個体固有の速度揺らぎ＋横揺れで群れが一列に並ばないようにする
        float swerve = Mathf.Sin(Time.time * _swerveFrequency) * 0.5f;
        Vector3 move = dir.normalized * (_chaseSpeed * _chaseSpeedMultiplier)
                     + transform.right * swerve;

        transform.position += move * Time.deltaTime;
        FaceDirection(dir.normalized);
    }

    private void FaceDirection(Vector3 direction)
    {
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
        Gizmos.DrawWireSphere(transform.position, p.攻撃距離);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, p.退散距離);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, _fleeRadius);

        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, _fleeDistance);
    }
}

/// <summary>コウモリ AI のステート定義。</summary>
public enum BatState : byte
{
    Sleeping,
    Alerted,
    Chasing,
    Attacking,
    Fleeing,
    Returning,
}
