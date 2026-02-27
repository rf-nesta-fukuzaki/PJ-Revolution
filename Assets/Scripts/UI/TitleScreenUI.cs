using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// タイトル画面の UI 制御コンポーネント。
///
/// [ボタン構成]
///   - ゲーム開始 : ロビー画面（UIScreen.Lobby）へ遷移
///   - オプション : オプション画面を表示
///   - 終了       : Application.Quit()（エディタでは再生停止）
///
/// [カーソル]
///   OnEnable 時にカーソルを表示・ロック解除する。
/// </summary>
public class TitleScreenUI : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("ボタン参照")]
    [Tooltip("「ゲーム開始」ボタン")]
    [SerializeField] private Button startButton;

    [Tooltip("「オプション」ボタン")]
    [SerializeField] private Button optionsButton;

    [Tooltip("「終了」ボタン")]
    [SerializeField] private Button quitButton;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (startButton   != null) startButton.onClick.AddListener(OnStartClicked);
        if (optionsButton != null) optionsButton.onClick.AddListener(OnOptionsClicked);
        if (quitButton    != null) quitButton.onClick.AddListener(OnQuitClicked);
    }

    private void OnEnable()
    {
        // タイトル画面ではカーソルを表示・ロック解除する
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    // ─────────────── ボタンハンドラ ───────────────

    private void OnStartClicked()
    {
        UIFlowController.Instance?.GoTo(UIScreen.Lobby);
    }

    private void OnOptionsClicked()
    {
        UIFlowController.Instance?.SetOptionsVisible(true);
    }

    private void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
