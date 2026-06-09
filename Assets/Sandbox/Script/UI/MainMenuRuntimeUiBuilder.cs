using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Sandbox.UI
{
    /// <summary>
    /// MainMenu.unity 向け PEAK/R.E.P.O. テーマ UI の実行時構築。
    /// Inspector 未配線時に MainMenuManager へ配線する。
    /// </summary>
    public static class MainMenuRuntimeUiBuilder
    {
        private sealed class MainMenuWiring
        {
            public GameObject LoadingPanel;
            public GameObject TopPanel;
            public GameObject LobbyPanel;
            public TMP_InputField PlayerNameInput;
            public Button CreateRoomButton;
            public TMP_InputField JoinCodeInput;
            public Button JoinRoomButton;
            public Button QuitButton;
            public TMP_Text StatusText;
            public TMP_Text RoomCodeDisplay;
            public TMP_Text PlayerListText;
            public Button StartGameButton;
            public Button LeaveRoomButton;
            public TMP_Text LobbyStatusText;
        }

        public static void EnsureThemed(MainMenuManager manager)
        {
            if (manager == null) return;
            if (GetField<GameObject>(manager, "_topPanel") != null) return;

            var wiring = BuildUi(manager);
            ApplyWiring(manager, wiring, onlyIfNull: true);

            if (Application.isPlaying)
            {
                CoopNetworkStackFactory.EnsureForTitleScene();
                MenuSceneBootstrap.EnsureForActiveScene(manager.transform);
            }
        }

#if UNITY_EDITOR
        /// <summary>エディタ上でテーマ UI を構築し SerializedObject 経由でシーンに保存する。</summary>
        public static void EnsureThemedAndSave(MainMenuManager manager)
        {
            if (manager == null) return;

            RemoveExistingThemedCanvas(manager.transform);

            var wiring = BuildUi(manager);
            EnsureMenuBootstrapInScene(manager.transform);
            SaveWiring(manager, wiring);

            var canvas = manager.transform.Find("MainMenu_ThemedCanvas");
            if (canvas != null)
                EditorUtility.SetDirty(canvas.gameObject);
        }

        private static void RemoveExistingThemedCanvas(Transform parent)
        {
            var existing = parent.Find("MainMenu_ThemedCanvas");
            if (existing != null)
                Object.DestroyImmediate(existing.gameObject);
        }

        private static void SaveWiring(MainMenuManager manager, MainMenuWiring wiring)
        {
            var so = new SerializedObject(manager);
            so.Update();
            SetProp(so, "_loadingPanel", wiring.LoadingPanel);
            SetProp(so, "_topPanel", wiring.TopPanel);
            SetProp(so, "_lobbyPanel", wiring.LobbyPanel);
            SetProp(so, "_playerNameInput", wiring.PlayerNameInput);
            SetProp(so, "_createRoomButton", wiring.CreateRoomButton);
            SetProp(so, "_joinCodeInput", wiring.JoinCodeInput);
            SetProp(so, "_joinRoomButton", wiring.JoinRoomButton);
            SetProp(so, "_quitButton", wiring.QuitButton);
            SetProp(so, "_statusText", wiring.StatusText);
            SetProp(so, "_roomCodeDisplay", wiring.RoomCodeDisplay);
            SetProp(so, "_playerListText", wiring.PlayerListText);
            SetProp(so, "_startGameButton", wiring.StartGameButton);
            SetProp(so, "_leaveRoomButton", wiring.LeaveRoomButton);
            SetProp(so, "_lobbyStatusText", wiring.LobbyStatusText);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(manager);
        }

        private static void EnsureMenuBootstrapInScene(Transform parent)
        {
            if (Object.FindFirstObjectByType<MenuSceneBootstrap>() != null) return;

            var go = new GameObject("MenuSceneBootstrap");
            go.transform.SetParent(parent, false);
            var bootstrap = go.AddComponent<MenuSceneBootstrap>();
            bootstrap.Configure(MenuSceneBootstrap.MenuSceneKind.Title);
            EditorUtility.SetDirty(go);
        }

        private static void SetProp(SerializedObject so, string name, Object value)
        {
            var prop = so.FindProperty(name);
            if (prop != null) prop.objectReferenceValue = value;
        }
#endif

        private static MainMenuWiring BuildUi(MainMenuManager manager)
        {
            var wiring = new MainMenuWiring();

            var canvas = MenuUiKit.CreateOverlayCanvas(manager.transform, "MainMenu_ThemedCanvas", 0);
            FlowUiTheme.CreateSceneBackdrop(canvas.transform, FlowUiTheme.SceneFlavor.CoopRepo);

            wiring.LoadingPanel = CreateFullScreenPanel(canvas.transform, "LoadingPanel");
            MenuUiKit.CreateTitleText(wiring.LoadingPanel.transform, "LoadingTitle", "CO-OP TERMINAL", 40,
                new Vector2(0.5f, 0.55f), FlowUiTheme.TerminalAccent);
            MenuUiKit.CreateBodyText(wiring.LoadingPanel.transform, "LoadingStatus", "UGS 初期化中...",
                22, new Vector2(0.5f, 0.45f));

            wiring.TopPanel = CreateFullScreenPanel(canvas.transform, "TopPanel");
            var topBoard = FlowUiTheme.CreateTerminalPanel(wiring.TopPanel.transform, "TopBoard",
                new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f),
                new Vector2(-460f, -220f), new Vector2(460f, 220f));
            MenuUiKit.CreateTitleText(topBoard, "Header", "PEAK IDIOTS — ONLINE", 44, new Vector2(0.5f, 0.86f));
            wiring.PlayerNameInput = CreateInput(topBoard, "PlayerNameInput", "プレイヤー名", new Vector2(0.5f, 0.66f), "Explorer");
            wiring.CreateRoomButton = MenuUiKit.CreateMenuButton(topBoard, "CreateRoomButton", "ルーム作成",
                new Vector2(0.5f, 0.48f), new Vector2(360f, 64f), MenuUiKit.BtnPrimary, null);
            wiring.JoinCodeInput = CreateInput(topBoard, "JoinCodeInput", "ルームコード", new Vector2(0.5f, 0.32f), "");
            wiring.JoinRoomButton = MenuUiKit.CreateMenuButton(topBoard, "JoinRoomButton", "ルームに参加",
                new Vector2(0.5f, 0.18f), new Vector2(360f, 64f), MenuUiKit.BtnSecondary, null);
            wiring.QuitButton = MenuUiKit.CreateMenuButton(wiring.TopPanel.transform, "QuitButton", "終了",
                new Vector2(0.5f, 0.1f), new Vector2(240f, 52f), MenuUiKit.BtnNeutral, null);
            wiring.StatusText = MenuUiKit.CreateBodyText(topBoard, "StatusText", "", 18, new Vector2(0.5f, 0.04f));

            wiring.LobbyPanel = CreateFullScreenPanel(canvas.transform, "LobbyPanel");
            var lobbyBoard = FlowUiTheme.CreateTerminalPanel(wiring.LobbyPanel.transform, "LobbyBoard",
                new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f),
                new Vector2(-460f, -200f), new Vector2(460f, 200f));
            MenuUiKit.CreateTitleText(lobbyBoard, "LobbyHeader", "AWAITING CREW", 40, new Vector2(0.5f, 0.86f));
            wiring.RoomCodeDisplay = MenuUiKit.CreateTerminalCode(lobbyBoard, "RoomCode", "------", new Vector2(0.5f, 0.58f));
            wiring.PlayerListText = MenuUiKit.CreateBodyText(lobbyBoard, "PlayerList", "CREW: 0 / 4",
                22, new Vector2(0.5f, 0.36f), UiPalette.Amber);
            wiring.LobbyStatusText = MenuUiKit.CreateBodyText(lobbyBoard, "LobbyStatus", "",
                18, new Vector2(0.5f, 0.18f));
            wiring.StartGameButton = MenuUiKit.CreateMenuButton(wiring.LobbyPanel.transform, "StartGameButton", "遠征開始",
                new Vector2(0.5f, 0.22f), new Vector2(360f, 64f), MenuUiKit.BtnPrimary, null);
            wiring.LeaveRoomButton = MenuUiKit.CreateMenuButton(wiring.LobbyPanel.transform, "LeaveRoomButton", "ルーム退出",
                new Vector2(0.5f, 0.12f), new Vector2(280f, 52f), MenuUiKit.BtnDanger, null);

            wiring.LoadingPanel.SetActive(true);
            wiring.TopPanel.SetActive(false);
            wiring.LobbyPanel.SetActive(false);

            return wiring;
        }

        private static void ApplyWiring(MainMenuManager manager, MainMenuWiring wiring, bool onlyIfNull)
        {
            SetField(manager, "_loadingPanel", wiring.LoadingPanel, onlyIfNull);
            SetField(manager, "_topPanel", wiring.TopPanel, onlyIfNull);
            SetField(manager, "_lobbyPanel", wiring.LobbyPanel, onlyIfNull);
            SetField(manager, "_playerNameInput", wiring.PlayerNameInput, onlyIfNull);
            SetField(manager, "_createRoomButton", wiring.CreateRoomButton, onlyIfNull);
            SetField(manager, "_joinCodeInput", wiring.JoinCodeInput, onlyIfNull);
            SetField(manager, "_joinRoomButton", wiring.JoinRoomButton, onlyIfNull);
            SetField(manager, "_quitButton", wiring.QuitButton, onlyIfNull);
            SetField(manager, "_statusText", wiring.StatusText, onlyIfNull);
            SetField(manager, "_roomCodeDisplay", wiring.RoomCodeDisplay, onlyIfNull);
            SetField(manager, "_playerListText", wiring.PlayerListText, onlyIfNull);
            SetField(manager, "_startGameButton", wiring.StartGameButton, onlyIfNull);
            SetField(manager, "_leaveRoomButton", wiring.LeaveRoomButton, onlyIfNull);
            SetField(manager, "_lobbyStatusText", wiring.LobbyStatusText, onlyIfNull);
        }

        private static GameObject CreateFullScreenPanel(Transform parent, string name)
        {
            var go = MenuUiKit.NewRect(name, parent).gameObject;
            MenuUiKit.Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        private static TMP_InputField CreateInput(Transform parent, string name, string placeholder, Vector2 anchor, string defaultText)
        {
            var frame = FlowUiTheme.CreateTerminalPanel(parent, name + "_Frame",
                anchor, anchor, new Vector2(-200f, -24f), new Vector2(200f, 24f));

            var go = new GameObject(name);
            go.transform.SetParent(frame, false);
            var rt = go.AddComponent<RectTransform>();
            FlowUiTheme.Stretch(rt, 8f);

            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(go.transform, false);
            var textAreaRt = textArea.AddComponent<RectTransform>();
            MenuUiKit.Stretch(textAreaRt);
            textAreaRt.offsetMin = new Vector2(8f, 4f);
            textAreaRt.offsetMax = new Vector2(-8f, -4f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            MenuUiKit.Stretch(textGo.AddComponent<RectTransform>());
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 20;
            text.color = UiPalette.Cream;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textArea.transform, false);
            MenuUiKit.Stretch(phGo.AddComponent<RectTransform>());
            var ph = phGo.AddComponent<TextMeshProUGUI>();
            ph.text = placeholder;
            ph.fontSize = 20;
            ph.color = UiPalette.CreamDim;
            ph.fontStyle = FontStyles.Italic;

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = textAreaRt;
            input.textComponent = text;
            input.placeholder = ph;
            input.text = defaultText;
            MenuUiKit.EnsureDefaultFont(text);
            MenuUiKit.EnsureDefaultFont(ph);
            return input;
        }

        private static void SetField(MainMenuManager mgr, string field, object value, bool onlyIfNull)
        {
            if (value == null) return;
            var f = typeof(MainMenuManager).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (f == null) return;
            if (onlyIfNull && f.GetValue(mgr) != null) return;
            f.SetValue(mgr, value);
        }

        private static T GetField<T>(object target, string name) where T : class
        {
            try
            {
                var field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                return field?.GetValue(target) as T;
            }
            catch { return null; }
        }
    }
}
