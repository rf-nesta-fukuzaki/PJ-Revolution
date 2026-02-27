using UnityEngine;

/// <summary>
/// プレイヤーの行動状態を管理するステートマシン。
///
/// [責務]
///   - 状態遷移の一元管理（PlayerClimbing / SurvivalStats からのイベントを受け取る）
///   - Rigidbody.isKinematic の切り替えをここだけで行う（タイミング起因バグを排除）
///   - コンポーネントの enabled は切り替えない
///   - OnStateChanged で各コンポーネントが CanMove / CanInput を判定する
///
/// [状態定義]
///   Normal     : 通常移動・入力有効
///   Climbing   : ロープ登攀中（isKinematic = true, PlayerMovement 無効）
///   Downed     : HP 0 でダウン（全入力無効）
///   Interacting: 将来拡張用（現在は未使用）
///
/// [isKinematic 切り替えフロー]
///   Normal → Climbing: linearVelocity = 0 → isKinematic = true
///   Climbing → Normal: isKinematic = false → 壁埋まり防止の微小上方力
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerStateManager : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("プレイヤー状態管理")]
    [Tooltip("登攀→通常状態の遷移時に加える上向き力 (壁埋まり防止用) (m/s)")]
    [SerializeField] private float antiEmbedForce = 0.5f;

    // ─────────────── 状態 ───────────────

    /// <summary>現在のプレイヤー状態。</summary>
    public PlayerState CurrentState { get; private set; } = PlayerState.Normal;

    // ─────────────── 内部参照 ───────────────

    private Rigidbody     _rb;
    private SurvivalStats _survivalStats;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        _rb            = GetComponent<Rigidbody>();
        _survivalStats = GetComponent<SurvivalStats>();
    }

    private void Start()
    {
        _rb.isKinematic = false;

        if (_survivalStats != null)
            _survivalStats.OnIsDownedChanged += OnIsDownedChanged;
    }

    private void OnDestroy()
    {
        if (_survivalStats != null)
            _survivalStats.OnIsDownedChanged -= OnIsDownedChanged;
    }

    // ─────────────── Update（ダウン状態ポーリング） ───────────────

    private void Update()
    {
        if (_survivalStats == null) return;

        bool isDowned = _survivalStats.IsDowned;
        if (isDowned && CurrentState != PlayerState.Downed)
            SetState(PlayerState.Downed);
        else if (!isDowned && CurrentState == PlayerState.Downed)
            SetState(PlayerState.Normal);
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>状態を変更する。</summary>
    public void SetState(PlayerState newState)
    {
        if (CurrentState == newState) return;

        var prev = CurrentState;
        CurrentState = newState;
        OnStateChanged(prev, newState);
    }

    /// <summary>状態変更を要求する（SetState の別名）。</summary>
    public void RequestStateChange(PlayerState newState)
    {
        SetState(newState);
    }

    // ─────────────── 状態遷移ハンドラ ───────────────

    private void OnStateChanged(PlayerState prev, PlayerState current)
    {
        switch (current)
        {
            case PlayerState.Normal:
                _rb.isKinematic = false;
                if (prev == PlayerState.Climbing)
                    _rb.AddForce(Vector3.up * antiEmbedForce, ForceMode.VelocityChange);
                break;

            case PlayerState.Climbing:
                _rb.linearVelocity = Vector3.zero;
                _rb.isKinematic    = true;
                break;

            case PlayerState.Downed:
                if (!_rb.isKinematic)
                    _rb.linearVelocity = Vector3.zero;
                break;
        }

        Debug.Log($"[PlayerStateManager] {prev} → {current}");
    }

    private void OnIsDownedChanged(bool prev, bool current)
    {
        if (current)
            SetState(PlayerState.Downed);
        else if (CurrentState == PlayerState.Downed)
            SetState(PlayerState.Normal);
    }
}

/// <summary>プレイヤーの行動状態。</summary>
public enum PlayerState
{
    Normal,
    Climbing,
    Downed,
    Interacting,
}
