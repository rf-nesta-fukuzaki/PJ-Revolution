using UnityEngine;

/// <summary>
/// 旧 Input System でプレイヤー入力を受け取り PlayerMovement / GrappleHook を制御する。
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
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");
        playerMovement.Move(new Vector2(h, v));
    }

    private void HandleJump()
    {
        if (Input.GetButtonDown("Jump"))
        {
            playerMovement.Jump();
            playerMovement.JumpHold();
        }
        if (Input.GetButtonUp("Jump"))
            playerMovement.JumpRelease();
    }

    private void HandleRope()
    {
        if (grappleHook == null) return;

        // 左クリック: スイング
        if (Input.GetMouseButtonDown(0))
            FireRope();

        // 右クリック: 引っ張り
        if (Input.GetMouseButtonDown(1))
            PullRope();

        // R キー: ロープ解放
        if (Input.GetKeyDown(KeyCode.R))
            ReleaseRope();
    }

    public void FireRope()  => grappleHook?.FireSwing();
    public void PullRope()  => grappleHook?.FirePull();
    public void ReleaseRope() => grappleHook?.Release();
}
