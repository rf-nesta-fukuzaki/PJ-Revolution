using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

namespace Sandbox.UI
{
    /// <summary>
    /// UGS/NGO 非依存のシンプルな単機用スタートメニュー。
    /// 既存 MainMenuManager はネットワークロビー（Co-op 用・後回し）なので、単機ビルド導線として別途用意する。
    /// UI は runtime 生成（外部アセット不要）。シーンにはこのコンポーネントを 1 つ置くだけでよい。
    ///  - PLAY → gameSceneName をロード
    ///  - QUIT → アプリ終了（Editor では Play 停止）
    /// </summary>
    public sealed class SandboxStartMenu : MonoBehaviour
    {
        [Tooltip("空欄なら GameFlow.InGameScene（SandboxOfflineCombined）を使用する。")]
        [SerializeField] private string gameSceneName = "";
        [SerializeField] private string title = "PEAK IDIOTS";
        [SerializeField] private string subtitle = "ドタバタ山岳 Co-op ロープアクション";

        private GameObject _titlePanel;
        private GameObject _lobbyPanel;

        private void Start()
        {
            EnsureEventSystem();
            BuildUI();
            GameplayCursorPolicy.SetMenuMode();

            // タイトルに戻った時点でラン横断状態（持ち越し買い物等）を初期化する。
            GameFlow.ResetRun();

            ShowTitle();
        }

        private void BuildUI()
        {
            var canvasGo = new GameObject("StartMenu_Canvas");
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();

            // 背景
            var bg = NewRect("BG", canvas.transform);
            Stretch(bg);
            bg.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 1f);

            BuildTitlePanel(canvas.transform);
            BuildLobbyPanel(canvas.transform);
        }

        // ── タイトルパネル ───────────────────────────────────
        private void BuildTitlePanel(Transform parent)
        {
            var panel = NewRect("TitlePanel", parent);
            Stretch(panel);
            _titlePanel = panel.gameObject;

            var t = CreateText("Title", panel, title, 96, new Vector2(0.5f, 0.72f));
            t.fontStyle = FontStyles.Bold;
            t.color = new Color(1f, 0.92f, 0.6f);
            var st = CreateText("Subtitle", panel, subtitle, 34, new Vector2(0.5f, 0.62f));
            st.color = new Color(0.85f, 0.9f, 1f);

            CreateButton("PlayButton", panel, "PLAY", new Vector2(0.5f, 0.42f),
                new Color(0.2f, 0.55f, 0.25f), OnPlay);
            CreateButton("QuitButton", panel, "QUIT", new Vector2(0.5f, 0.30f),
                new Color(0.5f, 0.2f, 0.2f), OnQuit);

            CreateText("Hint", panel,
                "WASD: 移動 / Space: ジャンプ / 左クリック: ロープ / R: 解放", 24, new Vector2(0.5f, 0.12f));
        }

        // ── ロビーパネル（ソロ用の出発確認）─────────────────
        private void BuildLobbyPanel(Transform parent)
        {
            var panel = NewRect("LobbyPanel", parent);
            Stretch(panel);
            _lobbyPanel = panel.gameObject;

            var h = CreateText("LobbyHeader", panel, "ロビー", 72, new Vector2(0.5f, 0.74f));
            h.fontStyle = FontStyles.Bold;
            h.color = new Color(1f, 0.92f, 0.6f);

            CreateText("LobbyInfo", panel,
                "ソロで出発します。準備ができたら「ゲーム開始」を押してください。",
                30, new Vector2(0.5f, 0.6f));

            CreateButton("StartGameButton", panel, "ゲーム開始", new Vector2(0.5f, 0.42f),
                new Color(0.2f, 0.55f, 0.25f), OnStartGame);
            CreateButton("BackButton", panel, "戻る", new Vector2(0.5f, 0.30f),
                new Color(0.35f, 0.35f, 0.4f), ShowTitle);
        }

        private void ShowTitle()
        {
            _titlePanel?.SetActive(true);
            _lobbyPanel?.SetActive(false);
        }

        private void OnPlay()
        {
            // タイトル → ロビー
            _titlePanel?.SetActive(false);
            _lobbyPanel?.SetActive(true);
        }

        private void OnStartGame()
        {
            // ロビー → インゲーム（遠征は到着後に自動開始）
            if (!string.IsNullOrEmpty(gameSceneName) && gameSceneName != "Sandbox")
            {
                GameplayCursorPolicy.SetGameplayMode();
                SceneManager.LoadScene(gameSceneName);
                return;
            }

            GameFlow.GoToInGame();
        }

        private void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ── UI helpers ──
        private static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static TextMeshProUGUI CreateText(string name, Transform parent, string text, int size, Vector2 anchor)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(1400f, size * 1.6f);
            rt.anchoredPosition = Vector2.zero;
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return tmp;
        }

        private void CreateButton(string name, Transform parent, string label, Vector2 anchor,
            Color color, UnityEngine.Events.UnityAction onClick)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(360f, 90f);
            rt.anchoredPosition = Vector2.zero;
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            var btn = rt.gameObject.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var tRt = NewRect("Text", rt);
            Stretch(tRt);
            var tmp = tRt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 40; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center; tmp.color = Color.white;
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
