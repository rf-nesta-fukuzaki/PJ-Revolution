using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandbox.UI
{
    /// <summary>
    /// StartMenu 内の Co-op ロビー UI（R.E.P.O. 端末風 + ルームコード表示）。
    /// </summary>
    public sealed class CoopLobbyController : MonoBehaviour
    {
        private const string DefaultPlayerName = "Explorer";

        private GameObject _backdrop;
        private GameObject _initPanel;
        private GameObject _topPanel;
        private GameObject _roomPanel;

        private TMP_InputField _playerNameInput;
        private TMP_InputField _joinCodeInput;
        private TextMeshProUGUI _statusLabel;
        private TextMeshProUGUI _roomCodeLabel;
        private TextMeshProUGUI _playerListLabel;
        private TextMeshProUGUI _lobbyStatusLabel;
        private Button _startGameButton;

        private bool _eventsBound;

        public void ActivatePanel(Transform canvasRoot)
        {
            if (_topPanel == null)
                BuildUi(canvasRoot);

            _backdrop?.SetActive(true);
            ShowPanel(_initPanel);
            CoopNetworkStackFactory.EnsureForTitleScene();
            BindEventsOnce();
            WaitForBootstrap();
        }

        public void Deactivate()
        {
            _backdrop?.SetActive(false);
            _topPanel?.SetActive(false);
            _roomPanel?.SetActive(false);
            _initPanel?.SetActive(false);
        }

        private void OnDestroy() => UnbindEvents();

        private void BuildUi(Transform parent)
        {
            _backdrop = MenuUiKit.NewRect("CoopBackdrop", parent).gameObject;
            MenuUiKit.Stretch(_backdrop.GetComponent<RectTransform>());
            MenuUiKit.CreateCoopBackground(_backdrop.transform);

            _initPanel = CreatePanel(parent, "CoopInitPanel");
            var initBoard = FlowUiTheme.CreateTerminalPanel(_initPanel.transform, "InitBoard",
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-380f, -100f), new Vector2(380f, 100f));
            MenuUiKit.CreateTitleText(initBoard, "CoopInitTitle", "CO-OP TERMINAL", 36,
                new Vector2(0.5f, 0.68f), FlowUiTheme.TerminalAccent);
            _statusLabel = MenuUiKit.CreateBodyText(initBoard, "CoopStatus", "UGS 初期化中...",
                22, new Vector2(0.5f, 0.32f));

            _topPanel = CreatePanel(parent, "CoopTopPanel");
            var topBoard = FlowUiTheme.CreateTerminalPanel(_topPanel.transform, "LobbyBoard",
                new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f),
                new Vector2(-460f, -220f), new Vector2(460f, 220f));

            MenuUiKit.CreateTitleText(topBoard, "CoopHeader", "PARTY LOBBY", 48,
                new Vector2(0.5f, 0.88f));
            MenuUiKit.CreateBodyText(topBoard, "CoopHint",
                "R.E.P.O. 式 — ルームコードを共有して 2〜4 人で遠征",
                18, new Vector2(0.5f, 0.76f), UiPalette.CreamDim);

            _playerNameInput = CreateInputField(topBoard, "PlayerNameInput", "プレイヤー名",
                new Vector2(0.5f, 0.62f), DefaultPlayerName);
            CreateMenuButton(topBoard, "CreateRoomButton", "ルーム作成",
                new Vector2(0.5f, 0.48f), MenuUiKit.BtnPrimary, OnCreateRoom);
            _joinCodeInput = CreateInputField(topBoard, "JoinCodeInput", "ルームコード（6桁）",
                new Vector2(0.5f, 0.34f), "");
            CreateMenuButton(topBoard, "JoinRoomButton", "ルームに参加",
                new Vector2(0.5f, 0.20f), MenuUiKit.BtnSecondary, OnJoinRoom);
            CreateMenuButton(_topPanel.transform, "CoopBackButton", "タイトルへ戻る",
                new Vector2(0.5f, 0.12f), MenuUiKit.BtnNeutral, OnBackToTitle);

            _roomPanel = CreatePanel(parent, "CoopRoomPanel");
            var roomBoard = FlowUiTheme.CreateTerminalPanel(_roomPanel.transform, "RoomBoard",
                new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.52f),
                new Vector2(-460f, -200f), new Vector2(460f, 200f));

            MenuUiKit.CreateTitleText(roomBoard, "RoomHeader", "AWAITING CREW", 42,
                new Vector2(0.5f, 0.86f));

            MenuUiKit.CreateBodyText(roomBoard, "RoomCodeCaption", "SHARE CODE",
                16, new Vector2(0.5f, 0.68f), FlowUiTheme.TerminalAccent);
            _roomCodeLabel = MenuUiKit.CreateTerminalCode(roomBoard, "RoomCode", "------",
                new Vector2(0.5f, 0.52f));

            _playerListLabel = MenuUiKit.CreateBodyText(roomBoard, "PlayerList", "CREW: 0 / 4",
                24, new Vector2(0.5f, 0.32f), UiPalette.Amber);
            _lobbyStatusLabel = MenuUiKit.CreateBodyText(roomBoard, "LobbyStatus", "",
                20, new Vector2(0.5f, 0.18f));

            _startGameButton = CreateMenuButton(_roomPanel.transform, "StartGameButton", "遠征開始",
                new Vector2(0.5f, 0.22f), MenuUiKit.BtnPrimary, OnStartGame);
            CreateMenuButton(_roomPanel.transform, "LeaveRoomButton", "ルーム退出",
                new Vector2(0.5f, 0.12f), MenuUiKit.BtnDanger, OnLeaveRoom);

            Deactivate();
        }

        private void WaitForBootstrap()
        {
            var bootstrap = NetworkBootstrap.Instance;
            if (bootstrap == null)
            {
                SetStatus("NetworkBootstrap がありません");
                ShowPanel(_topPanel);
                return;
            }

            if (bootstrap.IsReady) { OnBootstrapReady(); return; }

            if (bootstrap.HasError)
            {
                SetStatus($"接続エラー: {bootstrap.ErrorMessage}");
                ShowPanel(_topPanel);
                return;
            }

            bootstrap.OnReady += OnBootstrapReady;
            bootstrap.OnInitError += OnBootstrapError;
        }

        private void OnBootstrapReady()
        {
            UnbindBootstrapEvents();
            SetStatus("");
            ShowPanel(_topPanel);
        }

        private void OnBootstrapError(string message)
        {
            UnbindBootstrapEvents();
            SetStatus($"UGS エラー: {message}");
            ShowPanel(_topPanel);
        }

        private void UnbindBootstrapEvents()
        {
            var bootstrap = NetworkBootstrap.Instance;
            if (bootstrap == null) return;
            bootstrap.OnReady -= OnBootstrapReady;
            bootstrap.OnInitError -= OnBootstrapError;
        }

        private void BindEventsOnce()
        {
            if (_eventsBound) return;
            _eventsBound = true;

            var lobby = LobbyManager.Instance;
            if (lobby == null) return;

            lobby.OnRoomCreated  += HandleRoomCreated;
            lobby.OnRoomJoined   += HandleRoomJoined;
            lobby.OnLobbyUpdated += RefreshRoomPanel;
            lobby.OnError        += HandleLobbyError;
            lobby.OnGameStarted  += HandleGameStarted;
        }

        private void UnbindEvents()
        {
            if (!_eventsBound) return;
            _eventsBound = false;
            UnbindBootstrapEvents();

            var lobby = LobbyManager.Instance;
            if (lobby == null) return;

            lobby.OnRoomCreated  -= HandleRoomCreated;
            lobby.OnRoomJoined   -= HandleRoomJoined;
            lobby.OnLobbyUpdated -= RefreshRoomPanel;
            lobby.OnError        -= HandleLobbyError;
            lobby.OnGameStarted  -= HandleGameStarted;
        }

        private void OnCreateRoom() => _ = CreateRoomAsync();

        private async Task CreateRoomAsync()
        {
            var lobby = LobbyManager.Instance;
            if (lobby == null) { SetStatus("LobbyManager がありません"); return; }

            SetStatus("ルーム作成中...");
            LocalCoopSettings.Configure(PartyPlayMode.Online);
            await lobby.CreateRoomAsync(GetPlayerName());
        }

        private void OnJoinRoom() => _ = JoinRoomAsync();

        private async Task JoinRoomAsync()
        {
            string code = _joinCodeInput != null ? _joinCodeInput.text.Trim().ToUpper() : string.Empty;
            if (string.IsNullOrEmpty(code))
            {
                SetStatus("ルームコードを入力してください");
                return;
            }

            var lobby = LobbyManager.Instance;
            if (lobby == null) { SetStatus("LobbyManager がありません"); return; }

            SetStatus($"ルーム {code} に参加中...");
            LocalCoopSettings.Configure(PartyPlayMode.Online);
            await lobby.JoinRoomAsync(code, GetPlayerName());
        }

        private void OnStartGame() => _ = StartGameAsync();

        private async Task StartGameAsync()
        {
            var lobby = LobbyManager.Instance;
            if (lobby == null || !lobby.IsHost) return;

            SetLobbyStatus("ゲーム開始中...");
            if (_startGameButton != null) _startGameButton.interactable = false;

            GameFlow.PrepareCoopDeparture();
            await lobby.StartGameAsync(GameFlow.InGameScene);
        }

        private void OnLeaveRoom() => _ = LeaveRoomAsync();

        private async Task LeaveRoomAsync()
        {
            var lobby = LobbyManager.Instance;
            if (lobby != null)
                await lobby.LeaveLobbyAsync();

            ShowPanel(_topPanel);
            SetStatus("");
        }

        private void OnBackToTitle()
        {
            _ = LeaveRoomAsync();
            Deactivate();
            GetComponent<SandboxStartMenu>()?.ShowTitlePublic();
        }

        private void HandleRoomCreated(string roomCode)
        {
            ShowPanel(_roomPanel);
            if (_roomCodeLabel != null)
                _roomCodeLabel.text = roomCode;
            if (_startGameButton != null)
            {
                _startGameButton.gameObject.SetActive(true);
                _startGameButton.interactable = true;
            }
            SetLobbyStatus("メンバーを待っています...");
            RefreshRoomPanel();
        }

        private void HandleRoomJoined()
        {
            ShowPanel(_roomPanel);
            if (_startGameButton != null)
                _startGameButton.gameObject.SetActive(false);
            SetLobbyStatus("ホストのゲーム開始を待っています...");
            RefreshRoomPanel();
        }

        private void RefreshRoomPanel()
        {
            var lobby = LobbyManager.Instance;
            if (lobby == null) return;

            if (_playerListLabel != null)
                _playerListLabel.text = $"CREW: {lobby.PlayerCount} / 4";

            SetLobbyStatus(lobby.IsHost
                ? $"{lobby.PlayerCount}人参加 — 準備ができたら遠征開始"
                : "ホストの開始を待機中...");
        }

        private void HandleLobbyError(string message)
        {
            SetStatus(message);
            if (_startGameButton != null) _startGameButton.interactable = true;
        }

        private void HandleGameStarted()
        {
            SetLobbyStatus("遠征開始！");
            GameplayCursorPolicy.SetGameplayMode();
        }

        private string GetPlayerName()
        {
            string name = _playerNameInput != null ? _playerNameInput.text.Trim() : string.Empty;
            return string.IsNullOrEmpty(name) ? DefaultPlayerName : name;
        }

        private void ShowPanel(GameObject target)
        {
            if (_initPanel != null) _initPanel.SetActive(_initPanel == target);
            if (_topPanel != null)   _topPanel.SetActive(_topPanel == target);
            if (_roomPanel != null)  _roomPanel.SetActive(_roomPanel == target);
        }

        private void SetStatus(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }

        private void SetLobbyStatus(string message)
        {
            if (_lobbyStatusLabel != null) _lobbyStatusLabel.text = message;
        }

        private static GameObject CreatePanel(Transform parent, string name)
        {
            var rt = MenuUiKit.NewRect(name, parent);
            MenuUiKit.Stretch(rt);
            return rt.gameObject;
        }

        private static Button CreateMenuButton(Transform parent, string name, string label,
            Vector2 anchor, Color color, UnityEngine.Events.UnityAction onClick)
        {
            return MenuUiKit.CreateMenuButton(parent, name, label, anchor,
                new Vector2(380f, 72f), color, onClick);
        }

        private static TMP_InputField CreateInputField(Transform parent, string name, string placeholder,
            Vector2 anchor, string defaultText)
        {
            var panel = FlowUiTheme.CreateTerminalPanel(parent, name + "_Frame",
                anchor, anchor, new Vector2(-210f, -26f), new Vector2(210f, 26f));

            var go = new GameObject(name);
            go.transform.SetParent(panel, false);
            var rt = go.AddComponent<RectTransform>();
            FlowUiTheme.Stretch(rt, 8f);

            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(go.transform, false);
            var textAreaRt = textArea.AddComponent<RectTransform>();
            MenuUiKit.Stretch(textAreaRt);
            textAreaRt.offsetMin = new Vector2(10f, 4f);
            textAreaRt.offsetMax = new Vector2(-10f, -4f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            var textRt = textGo.AddComponent<RectTransform>();
            MenuUiKit.Stretch(textRt);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.fontSize = 22;
            text.color = UiPalette.Cream;
            if (text.font == null && TMP_Settings.defaultFontAsset != null)
                text.font = TMP_Settings.defaultFontAsset;

            var placeholderGo = new GameObject("Placeholder");
            placeholderGo.transform.SetParent(textArea.transform, false);
            var phRt = placeholderGo.AddComponent<RectTransform>();
            MenuUiKit.Stretch(phRt);
            var ph = placeholderGo.AddComponent<TextMeshProUGUI>();
            ph.text = placeholder;
            ph.fontSize = 22;
            ph.color = UiPalette.CreamDim;
            ph.fontStyle = FontStyles.Italic;
            if (ph.font == null && TMP_Settings.defaultFontAsset != null)
                ph.font = TMP_Settings.defaultFontAsset;

            var input = go.AddComponent<TMP_InputField>();
            input.textViewport = textAreaRt;
            input.textComponent = text;
            input.placeholder = ph;
            input.text = defaultText;
            return input;
        }
    }
}
