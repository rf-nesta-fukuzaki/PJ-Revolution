using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ロビー画面の UI 制御コンポーネント。
/// シングルプレイ専用。「ソロで開始」ボタンのみ。
///
/// [ボタン構成]
///   - ソロで開始 : CaveGenerator を起動してゲームを開始する
///   - 戻る       : タイトル画面に戻る
/// </summary>
public class LobbyUI : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("ボタン参照")]
    [Tooltip("「ソロで開始」ボタン")]
    [SerializeField] private Button soloButton;

    [Tooltip("「戻る」ボタン")]
    [SerializeField] private Button backButton;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (soloButton != null) soloButton.onClick.AddListener(OnSoloClicked);
        if (backButton != null) backButton.onClick.AddListener(OnBackClicked);
    }

    private void OnEnable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ─────────────── ボタンハンドラ ───────────────

    private void OnSoloClicked()
    {
        UIFlowController.Instance?.GoTo(UIScreen.Playing);
    }

    private void OnBackClicked()
    {
        UIFlowController.Instance?.GoTo(UIScreen.Title);
    }
}
