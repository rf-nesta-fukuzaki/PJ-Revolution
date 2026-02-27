using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーのロープ登攀を管理するコンポーネント。
///
/// [アーキテクチャ]
///   - PlayerPrefab ルートに追加する。
///   - Update が登攀入力を受け取り、FixedUpdate で位置を更新する。
///
/// [ステートマシン連携]
///   - StartClimbing(): PlayerStateManager.SetState(Climbing) を呼ぶ。
///   - ロープへのスナップは isKinematic 確定後の次フレームで実行。
///   - StopClimbing(): PlayerStateManager.SetState(Normal) を呼ぶ。
///
/// [isKinematic と MovePosition]
///   Climbing 中は isKinematic = true のため linearVelocity が無効。
///   FixedUpdate での移動は MovePosition を使う。
///
/// [スタミナ連携]
///   登攀中は SurvivalStats.Hunger を毎 FixedUpdate で staminaDrainPerSecond 消費する。
/// </summary>
public class PlayerClimbing : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("登攀設定")]
    [Tooltip("ロープを登る速度 (m/s)")]
    [SerializeField] private float climbSpeed = 3f;

    [Tooltip("Space キー押下で崖から飛び出す時の追加速度 (m/s)")]
    [SerializeField] private float exitJumpSpeed = 6f;

    [Tooltip("登攀中の毎秒スタミナ (空腹値) 消費量")]
    [SerializeField] private float staminaDrainPerSecond = 8f;

    [Tooltip("ロープ上端到達時の自動離脱判定距離")]
    [SerializeField] private float topExitThreshold = 0.3f;

    // ─────────────── 状態 ───────────────

    private bool _isClimbing;

    /// <summary>登攀中かどうか。</summary>
    public bool IsClimbing => _isClimbing;

    // ─────────────── 公開プロパティ ───────────────

    /// <summary>ロープアイテムを所持しているか。</summary>
    public bool HasRope { get; private set; } = true;

    // ─────────────── 内部状態 ───────────────

    private RopeController _currentRope;
    private float          _climbInputBuffer;
    private bool           _jumpBuffer;

    // ─────────────── コンポーネント参照 ───────────────

    private Rigidbody          _rb;
    private PlayerStateManager _stateManager;
    private SurvivalStats      _survivalStats;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        _rb            = GetComponent<Rigidbody>();
        _stateManager  = GetComponent<PlayerStateManager>();
        _survivalStats = GetComponent<SurvivalStats>();

        if (_stateManager == null)
            Debug.LogError("[PlayerClimbing] PlayerStateManager が見つかりません。");
    }

    private void FixedUpdate()
    {
        if (!_isClimbing) return;
        ProcessClimbing();
    }

    private void Update()
    {
        if (!_isClimbing) return;

        float vertical = Input.GetAxis("Vertical");
        bool jump = Input.GetButtonDown("Jump");
        ProcessClimbInput(vertical, jump);
    }

    // ─────────────── 登攀入力処理 ───────────────

    private void ProcessClimbInput(float vertical, bool jump)
    {
        _climbInputBuffer = vertical;
        _jumpBuffer = _jumpBuffer || jump;
    }

    // ─────────────── 登攀物理処理 ───────────────

    private void ProcessClimbing()
    {
        if (_currentRope == null)
        {
            StopClimbing(false);
            return;
        }

        _survivalStats?.ApplyStatModification(
            StatType.Hunger, -staminaDrainPerSecond * Time.fixedDeltaTime);

        if (_survivalStats != null && _survivalStats.Hunger <= 0f)
        {
            Debug.Log("[PlayerClimbing] スタミナ切れで落下");
            StopClimbing(false);
            return;
        }

        if (_jumpBuffer)
        {
            _jumpBuffer = false;
            StopClimbing(withJump: true);
            return;
        }

        Vector3 pos = _rb.position;
        pos.y += _climbInputBuffer * climbSpeed * Time.fixedDeltaTime;

        pos.x = _currentRope.transform.position.x;
        pos.z = _currentRope.transform.position.z;

        float topY = _currentRope.TopPosition.y;
        if (pos.y >= topY - topExitThreshold && _climbInputBuffer > 0f)
        {
            pos.y = topY + 0.6f;
            _rb.MovePosition(pos);
            Debug.Log("[PlayerClimbing] 登頂完了");
            StopClimbing(false);
            return;
        }

        float bottomY = _currentRope.BottomPosition.y;
        if (pos.y <= bottomY + 0.1f && _climbInputBuffer < 0f)
        {
            Debug.Log("[PlayerClimbing] 下端に到達して落下");
            StopClimbing(false);
            return;
        }

        _rb.MovePosition(pos);
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>登攀を開始する。RopeController.Interact() から呼ぶ。</summary>
    public void StartClimbing(RopeController rope)
    {
        if (_isClimbing) return;
        if (_stateManager == null) return;

        _currentRope      = rope;
        _climbInputBuffer = 0f;
        _jumpBuffer       = false;

        _stateManager.SetState(PlayerState.Climbing);
        _isClimbing = true;

        StartCoroutine(SnapToRopeNextFrame(rope));

        Debug.Log($"[PlayerClimbing] {gameObject.name} が登攀開始");
    }

    /// <summary>ロープアイテムを消費する。</summary>
    public void ConsumeRope() => HasRope = false;

    /// <summary>ロープアイテムを付与する。</summary>
    public void GiveRope() => HasRope = true;

    // ─────────────── 内部処理 ───────────────

    private void StopClimbing(bool withJump)
    {
        if (!_isClimbing) return;

        _isClimbing = false;
        _stateManager.SetState(PlayerState.Normal);

        if (withJump)
            _rb.AddForce(Vector3.up * exitJumpSpeed, ForceMode.VelocityChange);

        _currentRope      = null;
        _climbInputBuffer = 0f;
        _jumpBuffer       = false;

        Debug.Log($"[PlayerClimbing] {gameObject.name} が登攀終了 (jump={withJump})");
    }

    private IEnumerator SnapToRopeNextFrame(RopeController rope)
    {
        yield return null;

        if (!_isClimbing || _currentRope != rope) yield break;

        Vector3 pos = _rb.position;
        pos.x = rope.transform.position.x;
        pos.z = rope.transform.position.z;
        _rb.position = pos;
    }
}
