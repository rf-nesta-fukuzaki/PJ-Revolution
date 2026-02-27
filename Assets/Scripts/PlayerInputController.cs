using UnityEngine;

/// <summary>
/// 旧 Input System (Input.GetAxis / GetButton) を使ってプレイヤーの入力を受け取り、
/// PlayerMovement / TorchSystem を直接呼び出すコントローラー。
///
/// [ステートマシン対応]
///   - Update の先頭で PlayerStateManager.CurrentState を確認する。
///   - Normal 以外の状態（Climbing / Downed）は移動・ジャンプ入力を送信しない。
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerInputController : MonoBehaviour
{
    [Header("参照設定")]
    [Tooltip("制御対象の PlayerMovement。省略時は同一GameObject から自動取得")]
    [SerializeField] private PlayerMovement playerMovement;

    [Tooltip("制御対象の TorchSystem。子 GameObject に配置時は指定")]
    [SerializeField] private TorchSystem torchSystem;

    [Header("デバッグ設定")]
    [Tooltip("F キー押下で補充する燃料量")]
    [SerializeField] private float debugRefillAmount = 50f;

    // ─────────────── 内部参照 ───────────────

    private PlayerStateManager _stateManager;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        _stateManager = GetComponent<PlayerStateManager>();
    }

    private void Update()
    {
        // たいまつ・デバッグは状態に関わらず常に処理する
        HandleTorch();
        HandleDebug();

        // Normal 状態のみ移動・ジャンプを受け付ける
        if (!IsNormalState()) return;

        HandleMovement();
        HandleJump();
    }

    // ─────────────── 状態チェック ───────────────

    private bool IsNormalState()
    {
        if (_stateManager == null) return true;
        return _stateManager.CurrentState == PlayerState.Normal;
    }

    // ─────────────── 入力処理 ───────────────

    private void HandleMovement()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        playerMovement.Move(new Vector2(h, v));
    }

    private void HandleJump()
    {
        if (!Input.GetButtonDown("Jump")) return;
        playerMovement.Jump();
    }

    private void HandleTorch()
    {
        if (!Input.GetMouseButtonDown(1)) return;
        torchSystem?.ToggleTorch();
    }

    private void HandleDebug()
    {
        if (!Input.GetKeyDown(KeyCode.F)) return;
        torchSystem?.RefillFuel(debugRefillAmount);
    }
}
