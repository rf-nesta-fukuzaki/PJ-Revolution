using UnityEngine;

/// <summary>
/// プレイヤーのインタラクション処理を担当する。
/// 前方に Raycast を飛ばして IInteractable を検出する。
///
/// [フロー]
///   1. Update で前方レイキャスト → IInteractable を検出
///   2. 検出中は UIManager にプロンプトを表示
///   3. E キー押下 → Interact(gameObject) を直接呼ぶ
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Header("インタラクション設定")]
    [Tooltip("レイキャストの最大距離")]
    [SerializeField] private float interactRange = 3f;

    [Tooltip("レイキャスト判定に使用するレイヤーマスク")]
    [SerializeField] private LayerMask interactMask = ~0;

    // ─────────────── 内部状態 ───────────────

    private UIManager     _ui;
    private Camera        _camera;
    private IInteractable _currentTarget;

    // ─────────────── Unity Lifecycle ───────────────

    private void Start()
    {
        _ui     = FindFirstObjectByType<UIManager>();
        _camera = GetComponentInChildren<Camera>();
    }

    private void OnDestroy()
    {
        _ui?.SetInteractPrompt(null);
    }

    private void Update()
    {
        DetectInteractable();

        if (_currentTarget != null && Input.GetKeyDown(KeyCode.E))
            _currentTarget.Interact(gameObject);
    }

    // ─────────────── 内部処理 ───────────────

    private void DetectInteractable()
    {
        Ray ray = _camera != null
            ? new Ray(_camera.transform.position, _camera.transform.forward)
            : new Ray(transform.position + Vector3.up * 1.5f, transform.forward);

        IInteractable found = null;

        if (Physics.Raycast(ray, out RaycastHit hit, interactRange, interactMask))
            found = hit.collider.GetComponentInParent<IInteractable>();

        if (found != _currentTarget)
        {
            _currentTarget = found;
            _ui?.SetInteractPrompt(found?.GetPromptText());
        }
    }
}
