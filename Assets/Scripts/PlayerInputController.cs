using UnityEngine;

/// <summary>
/// New Input System でプレイヤー入力を受け取り PlayerMovement / GrappleHook を制御する。
/// </summary>
[RequireComponent(typeof(PlayerMovement))]
public class PlayerInputController : MonoBehaviour
{
    [Header("参照設定")]
    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private GrappleHook grappleHook;

    private PlayerStateManager _stateManager;

    private void Awake()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();
        if (grappleHook == null)
            grappleHook = GetComponentInChildren<GrappleHook>();

        _stateManager = GetComponent<PlayerStateManager>();
    }

    private void Update()
    {
        if (!IsNormalOrSwinging()) return;

        HandleMovement();
        HandleJump();
        HandleRope();
    }

    private bool IsNormalOrSwinging()
    {
        if (_stateManager == null) return true;
        var s = _stateManager.CurrentState;
        return s == PlayerState.Normal || s == PlayerState.Swinging;
    }

    private void HandleMovement()
    {
        Vector2 moveInput = InputStateReader.ReadMoveVectorRaw();
        playerMovement.Move(moveInput);
    }

    private void HandleJump()
    {
        if (InputStateReader.JumpPressedThisFrame())
        {
            playerMovement.Jump();
            playerMovement.JumpHold();
        }
        if (InputStateReader.JumpReleasedThisFrame())
            playerMovement.JumpRelease();
    }

    private void HandleRope()
    {
        if (grappleHook == null) return;

        // 左クリック: スイング
        if (InputStateReader.PrimaryPointerPressedThisFrame())
            FireRope();

        // 右クリック: 引っ張り
        if (InputStateReader.SecondaryPointerPressedThisFrame())
            PullRope();

        // R キー: ロープ解放
        if (InputStateReader.ReleaseRopePressedThisFrame())
            ReleaseRope();
    }

    public void FireRope()  => grappleHook?.FireSwing();
    public void PullRope()  => grappleHook?.FirePull();
    public void ReleaseRope() => grappleHook?.Release();
}
