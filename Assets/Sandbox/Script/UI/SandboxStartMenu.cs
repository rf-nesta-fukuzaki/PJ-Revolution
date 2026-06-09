using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace Sandbox.UI
{
    /// <summary>
    /// PEAK 風タイトル + ソロ/Co-op ロビー導線。
    /// </summary>
    public sealed class SandboxStartMenu : MonoBehaviour
    {
        [SerializeField] private string gameSceneName = "";
        [SerializeField] private string title = "PEAK IDIOTS";
        [SerializeField] private string subtitle = "ドタバタ山岳 Co-op ロープアクション";
        [SerializeField] private string tagline = "登るのは簡単。運ぶと全員バカになる。";

        private GameObject _titlePanel;
        private GameObject _soloLobbyPanel;
        private TextMeshProUGUI _lobbyRunLabel;
        private CoopLobbyController _coopLobby;
        private Transform _canvasRoot;

        private void Awake()
        {
            MenuSceneBootstrap.EnsureForActiveScene(transform);
            _coopLobby = GetComponent<CoopLobbyController>();
            if (_coopLobby == null)
                _coopLobby = gameObject.AddComponent<CoopLobbyController>();
        }

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            GameplayCursorPolicy.SetMenuMode();
            GameFlow.ResetRun();
            ShowTitle();
        }

        private void BuildUI()
        {
            var canvas = MenuUiKit.CreateOverlayCanvas(transform, "StartMenu_Canvas");
            _canvasRoot = canvas.transform;
            FlowUiTheme.CreateSceneBackdrop(canvas.transform, FlowUiTheme.SceneFlavor.TitlePeak);

            BuildTitlePanel(canvas.transform);
            BuildSoloLobbyPanel(canvas.transform);
        }

        private void BuildTitlePanel(Transform parent)
        {
            var panel = MenuUiKit.NewRect("TitlePanel", parent);
            MenuUiKit.Stretch(panel);
            _titlePanel = panel.gameObject;

            // ── PEAK 風ロゴ ───────────────────────────────────────────
            // 重い端末枠箱をやめ、背面のソフトグロー＋淡いスクリム＋強い影で
            // 夕景の空に映える軽快なエンブレムにする（PEAK 優先）。
            var titleAnchor = new Vector2(0.5f, 0.775f);

            // 背面の柔らかい暗グロー（文字を空から浮かせる芯）
            var glow = MenuUiKit.NewRect("TitleGlow", panel);
            glow.anchorMin = glow.anchorMax = titleAnchor;
            glow.sizeDelta = new Vector2(1500f, 760f);
            glow.anchoredPosition = new Vector2(0f, -10f);
            FlowUiTheme.AddSprite(glow, UiSprite.RadialGlow(2.1f),
                new Color(0.03f, 0.02f, 0.05f, 0.5f), UnityEngine.UI.Image.Type.Simple).raycastTarget = false;

            // 上下が透けるソフトスクリム帯（枠なし・縦グラデで中央のみ僅かに沈める）
            var scrim = MenuUiKit.NewRect("TitleScrim", panel);
            scrim.anchorMin = scrim.anchorMax = titleAnchor;
            scrim.sizeDelta = new Vector2(1000f, 250f);
            scrim.anchoredPosition = Vector2.zero;
            FlowUiTheme.AddSprite(scrim, UiSprite.VerticalGradient3(
                new Color(0.05f, 0.04f, 0.07f, 0f),
                new Color(0.05f, 0.04f, 0.07f, 0.5f),
                new Color(0.05f, 0.04f, 0.07f, 0f), 0.5f),
                Color.white, UnityEngine.UI.Image.Type.Simple).raycastTarget = false;

            var titleTmp = MenuUiKit.CreateTitleText(panel, "Title", title, 96, titleAnchor);
            titleTmp.characterSpacing = 6f;
            FlowUiTheme.StyleReadable(titleTmp, 0.32f);

            // タイトル下のアンバー区切り線
            var divider = MenuUiKit.NewRect("Divider", panel);
            divider.anchorMin = divider.anchorMax = new Vector2(0.5f, 0.725f);
            divider.sizeDelta = new Vector2(560f, 2f);
            divider.anchoredPosition = Vector2.zero;
            FlowUiTheme.AddSprite(divider, UiSprite.RoundedRect(2),
                new Color(UiPalette.Amber.r, UiPalette.Amber.g, UiPalette.Amber.b, 0.75f));

            MenuUiKit.CreateBodyText(panel, "Subtitle", subtitle, 27, new Vector2(0.5f, 0.70f),
                UiPalette.Cream).characterSpacing = 4f;

            // タグライン（暖色・控えめなソフトプレートで可読性確保）
            var taglinePlate = MenuUiKit.NewRect("TaglinePlate", panel);
            taglinePlate.anchorMin = taglinePlate.anchorMax = new Vector2(0.5f, 0.575f);
            taglinePlate.sizeDelta = new Vector2(740f, 54f);
            taglinePlate.anchoredPosition = Vector2.zero;
            FlowUiTheme.AddSprite(taglinePlate, UiSprite.RoundedRect(22), new Color(0.06f, 0.05f, 0.07f, 0.5f));
            MenuUiKit.CreateBodyText(taglinePlate, "Tagline", tagline, 26, new Vector2(0.5f, 0.5f),
                UiPalette.Amber).fontStyle = FontStyles.Italic;

            // PEAK 暖色基調のボタン配色（緑の濁りをやめ、アンバー系で統一）
            Color soloFill = new Color(0.50f, 0.32f, 0.15f, 0.96f);   // 暖かい琥珀ブラウン
            Color coopFill = new Color(0.14f, 0.30f, 0.36f, 0.96f);   // 補色の冷たいティール（R.E.P.O. 寄せ）
            MenuUiKit.CreateMenuButton(panel, "PlayButton", "▶  ソロ遠征",
                new Vector2(0.5f, 0.40f), new Vector2(440f, 80f), soloFill, OnPlaySolo,
                UiPalette.Amber);
            MenuUiKit.CreateMenuButton(panel, "CoopButton", "▶  Co-op（2〜4人）",
                new Vector2(0.5f, 0.295f), new Vector2(440f, 80f), coopFill, OnPlayCoop,
                FlowUiTheme.TerminalAccent);
            MenuUiKit.CreateMenuButton(panel, "QuitButton", "終了",
                new Vector2(0.5f, 0.19f), new Vector2(300f, 58f), MenuUiKit.BtnNeutral, OnQuit);

            // フッター（操作ヒント帯）
            var hintBar = MenuUiKit.NewRect("HintBar", panel);
            hintBar.anchorMin = new Vector2(0.5f, 0.065f);
            hintBar.anchorMax = new Vector2(0.5f, 0.065f);
            hintBar.sizeDelta = new Vector2(1180f, 44f);
            hintBar.anchoredPosition = Vector2.zero;
            FlowUiTheme.AddSprite(hintBar, UiSprite.RoundedRect(18), new Color(0.05f, 0.05f, 0.07f, 0.5f));
            MenuUiKit.CreateBodyText(hintBar,  "Hint",
                "WASD 移動    SPACE ジャンプ    左クリック ロープ    R 解放    Co-op はプロキシミティ VC",
                20, new Vector2(0.5f, 0.5f), UiPalette.Cream).characterSpacing = 1f;

            MenuUiKit.CreateBodyText(panel, "Version", "EARLY ACCESS  ·  Stage01 Loop",
                16, new Vector2(0.985f, 0.028f), UiPalette.CreamDim).alignment = TextAlignmentOptions.Right;
        }

        private void BuildSoloLobbyPanel(Transform parent)
        {
            var panel = MenuUiKit.NewRect("SoloLobbyPanel", parent);
            MenuUiKit.Stretch(panel);
            _soloLobbyPanel = panel.gameObject;

            var board = FlowUiTheme.CreateTerminalPanel(panel, "SoloBoard",
                new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f),
                new Vector2(-440f, -160f), new Vector2(440f, 160f));

            MenuUiKit.CreateTitleText(board, "LobbyHeader", "ソロ出発確認", 48, new Vector2(0.5f, 0.78f));
            MenuUiKit.CreateBodyText(board, "LobbyInfo",
                "ベースキャンプで装備を整え、山頂を目指す。\n初回はショップをスキップして即出発。",
                24, new Vector2(0.5f, 0.52f));

            _lobbyRunLabel = MenuUiKit.CreateBodyText(board, "LobbyRunInfo",
                "遠征 #1", 22, new Vector2(0.5f, 0.28f), UiPalette.Amber);

            MenuUiKit.CreateMenuButton(panel, "StartGameButton", "出発する",
                new Vector2(0.5f, 0.22f), new Vector2(380f, 80f), MenuUiKit.BtnPrimary, OnStartGame,
                UiPalette.Amber);
            MenuUiKit.CreateMenuButton(panel, "BackButton", "戻る",
                new Vector2(0.5f, 0.12f), new Vector2(260f, 56f), MenuUiKit.BtnNeutral, ShowTitle);
        }

        public void ShowTitlePublic() => ShowTitle();

        private void ShowTitle()
        {
            _titlePanel?.SetActive(true);
            _soloLobbyPanel?.SetActive(false);
            _coopLobby?.Deactivate();
        }

        private void OnPlaySolo()
        {
            _titlePanel?.SetActive(false);
            _soloLobbyPanel?.SetActive(true);
            _coopLobby?.Deactivate();
            RefreshLobbyInfo();
        }

        private void OnPlayCoop()
        {
            _titlePanel?.SetActive(false);
            _soloLobbyPanel?.SetActive(false);
            _coopLobby?.ActivatePanel(_canvasRoot);
        }

        public void OnPlay() => OnPlaySolo();

        public void OnStartGame()
        {
            if (!string.IsNullOrEmpty(gameSceneName) && gameSceneName != "Sandbox")
            {
                GameplayCursorPolicy.SetGameplayMode();
                SceneManager.LoadScene(gameSceneName);
                return;
            }
            GameFlow.GoToInGame();
        }

        private void RefreshLobbyInfo()
        {
            if (_lobbyRunLabel == null) return;
            int nextRun = GameFlow.RunCount + 1;
            _lobbyRunLabel.text = nextRun <= 1
                ? "遠征 #1 — 初回出発（装備持ち込みなし）"
                : $"遠征 #{nextRun} — 前回の装備をベースキャンプで確認";
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }
    }
}
