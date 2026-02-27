using UnityEngine;

/// <summary>
/// UI 画面遷移の中央管理コンポーネント。
///
/// [管理する画面状態]
///   Title        : タイトル画面
///   Lobby        : ロビー（接続方法選択）画面
///   Playing      : ゲームプレイ中（HUD のみ表示）
///   Result       : リザルト画面
///   CosmeticShop : コスメティックショップ画面
///
/// [使い方]
///   各 UI スクリプトから UIFlowController.Instance.GoTo(UIScreen.XXX) を呼ぶ。
///   Inspector で各 Canvas の GameObject を SerializeField に割り当てる。
///
/// [注意]
///   UIManager / ResultUI / CosmeticShopUI は変更しない。
///   このコントローラーから SetActive で表示/非表示を制御するだけ。
/// </summary>
public class UIFlowController : MonoBehaviour
{
    // ─────────────── Singleton ───────────────

    public static UIFlowController Instance { get; private set; }

    // ─────────────── Inspector: Canvas 参照 ───────────────

    [Header("Canvas GameObject 参照")]
    [Tooltip("タイトル画面の Canvas GameObject")]
    [SerializeField] private GameObject titleCanvas;

    [Tooltip("ロビー画面の Canvas GameObject")]
    [SerializeField] private GameObject lobbyCanvas;

    [Tooltip("HUD (ゲームプレイ中) の Canvas GameObject")]
    [SerializeField] private GameObject hudCanvas;

    [Tooltip("リザルト画面の Canvas GameObject")]
    [SerializeField] private GameObject resultCanvas;

    [Tooltip("コスメティックショップの Canvas GameObject")]
    [SerializeField] private GameObject cosmeticShopCanvas;

    [Tooltip("オプション画面の Canvas GameObject")]
    [SerializeField] private GameObject optionsCanvas;

    // ─────────────── 状態 ───────────────

    private UIScreen _currentScreen = UIScreen.Title;

    public UIScreen CurrentScreen => _currentScreen;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // 起動時はタイトル画面を表示する
        GoTo(UIScreen.Title);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// 指定した画面に遷移する。対応する Canvas の表示/非表示を自動で切り替える。
    /// </summary>
    public void GoTo(UIScreen screen)
    {
        _currentScreen = screen;
        ApplyScreenVisibility();
    }

    /// <summary>
    /// オプション Canvas の表示/非表示をトグルする。
    /// PauseManager から呼ばれる。現在の画面状態は変えない。
    /// </summary>
    public void ToggleOptions()
    {
        if (optionsCanvas == null) return;
        optionsCanvas.SetActive(!optionsCanvas.activeSelf);
    }

    /// <summary>
    /// オプション Canvas を指定した表示状態にする。
    /// </summary>
    public void SetOptionsVisible(bool visible)
    {
        if (optionsCanvas != null)
            optionsCanvas.SetActive(visible);
    }

    /// <summary>
    /// オプション Canvas が現在表示中かどうかを返す。
    /// </summary>
    public bool IsOptionsVisible =>
        optionsCanvas != null && optionsCanvas.activeSelf;

    // ─────────────── 内部処理 ───────────────

    private void ApplyScreenVisibility()
    {
        SetActive(titleCanvas,        _currentScreen == UIScreen.Title);
        SetActive(lobbyCanvas,        _currentScreen == UIScreen.Lobby);
        SetActive(hudCanvas,          _currentScreen == UIScreen.Playing);
        SetActive(resultCanvas,       _currentScreen == UIScreen.Result);
        SetActive(cosmeticShopCanvas, _currentScreen == UIScreen.CosmeticShop);

        // オプションは画面遷移時に必ず閉じる
        SetActive(optionsCanvas, false);
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }
}

/// <summary>UIFlowController が管理する画面状態の列挙型。</summary>
public enum UIScreen
{
    Title,
    Lobby,
    Playing,
    Result,
    CosmeticShop,
}
