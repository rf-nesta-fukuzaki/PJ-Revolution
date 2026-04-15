using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GDD §8.1 — メインメニュー。
/// ・ホスト: ルーム作成 → ルームコードを表示
/// ・ゲスト: ルームコード入力 → 参加
/// UGS 初期化が完了してから操作を解禁する。
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    // ── パネル群 ────────────────────────────────────────────────
    [Header("パネル")]
    [SerializeField] private GameObject _topPanel;      // タイトル・ボタン群
    [SerializeField] private GameObject _lobbyPanel;    // ルーム待機画面
    [SerializeField] private GameObject _loadingPanel;  // UGS 初期化中

    // ── トップパネル UI ─────────────────────────────────────────
    [Header("トップパネル")]
    [SerializeField] private TMP_InputField _playerNameInput;
    [SerializeField] private Button         _createRoomButton;
    [SerializeField] private TMP_InputField _joinCodeInput;
    [SerializeField] private Button         _joinRoomButton;
    [SerializeField] private Button         _quitButton;
    [SerializeField] private TMP_Text       _statusText;

    // ── ロビーパネル UI ─────────────────────────────────────────
    [Header("ロビーパネル")]
    [SerializeField] private TMP_Text  _roomCodeDisplay;
    [SerializeField] private TMP_Text  _playerListText;
    [SerializeField] private Button    _startGameButton;   // ホストのみ有効
    [SerializeField] private Button    _leaveRoomButton;
    [SerializeField] private TMP_Text  _lobbyStatusText;

    // ── 定数 ────────────────────────────────────────────────────
    private const string DEFAULT_PLAYER_NAME = "Explorer";
    private const string SCENE_GAME          = "Mountain01";

    // ── ライフサイクル ────────────────────────────────────────────
    private void Start()
    {
        ShowPanel(_loadingPanel);

        // UGS 初期化完了を待つ
        if (NetworkBootstrap.Instance == null)
        {
            // Bootstrap が同シーンにある場合、すぐに使える
            SetStatus("UGS 初期化中...");
        }
        else if (NetworkBootstrap.Instance.IsReady)
        {
            OnBootstrapReady();
        }
        else
        {
            NetworkBootstrap.Instance.OnReady      += OnBootstrapReady;
            NetworkBootstrap.Instance.OnInitError  += OnBootstrapError;
        }

        BindButtons();
        BindLobbyEvents();
    }

    private void OnDestroy()
    {
        if (NetworkBootstrap.Instance == null) return;
        NetworkBootstrap.Instance.OnReady     -= OnBootstrapReady;
        NetworkBootstrap.Instance.OnInitError -= OnBootstrapError;
    }

    // ── UGS コールバック ─────────────────────────────────────────
    private void OnBootstrapReady()
    {
        ShowPanel(_topPanel);
        SetStatus("");
    }

    private void OnBootstrapError(string message)
    {
        ShowPanel(_topPanel);
        SetStatus($"エラー: {message}");
        _createRoomButton.interactable = false;
        _joinRoomButton.interactable   = false;
    }

    // ── ボタンバインド ────────────────────────────────────────────
    private void BindButtons()
    {
        _createRoomButton.onClick.AddListener(OnCreateRoom);
        _joinRoomButton.onClick.AddListener(OnJoinRoom);
        _quitButton.onClick.AddListener(OnQuit);
        _startGameButton.onClick.AddListener(OnStartGame);
        _leaveRoomButton.onClick.AddListener(OnLeaveRoom);
    }

    private void BindLobbyEvents()
    {
        if (LobbyManager.Instance == null) return;

        LobbyManager.Instance.OnRoomCreated   += OnRoomCreated;
        LobbyManager.Instance.OnRoomJoined    += OnRoomJoined;
        LobbyManager.Instance.OnLobbyUpdated  += RefreshLobbyPanel;
        LobbyManager.Instance.OnError         += OnLobbyError;
        LobbyManager.Instance.OnGameStarted   += OnGameStarted;
    }

    // ── ボタンハンドラ ────────────────────────────────────────────
    private void OnCreateRoom()
    {
        _ = OnCreateRoomAsync();
    }

    private async Task OnCreateRoomAsync()
    {
        SetInteractable(false);
        SetStatus("ルーム作成中...");
        string name = GetPlayerName();
        await LobbyManager.Instance.CreateRoomAsync(name);
    }

    private void OnJoinRoom()
    {
        _ = OnJoinRoomAsync();
    }

    private async Task OnJoinRoomAsync()
    {
        string code = _joinCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(code))
        {
            SetStatus("ルームコードを入力してください");
            return;
        }

        SetInteractable(false);
        SetStatus($"ルーム {code} に参加中...");
        string name = GetPlayerName();
        await LobbyManager.Instance.JoinRoomAsync(code, name);
    }

    private void OnStartGame()
    {
        _ = OnStartGameAsync();
    }

    private async Task OnStartGameAsync()
    {
        if (LobbyManager.Instance == null || !LobbyManager.Instance.IsHost) return;

        SetLobbyStatus("ゲーム開始中...");
        _startGameButton.interactable = false;
        await LobbyManager.Instance.StartGameAsync(SCENE_GAME);
    }

    private void OnLeaveRoom()
    {
        _ = OnLeaveRoomAsync();
    }

    private async Task OnLeaveRoomAsync()
    {
        if (LobbyManager.Instance == null) return;
        await LobbyManager.Instance.LeaveLobbyAsync();
        ShowPanel(_topPanel);
        SetInteractable(true);
        SetStatus("");
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── ロビーイベントコールバック ─────────────────────────────────
    private void OnRoomCreated(string roomCode)
    {
        ShowPanel(_lobbyPanel);
        _roomCodeDisplay.text = $"ルームコード：{roomCode}";
        _startGameButton.gameObject.SetActive(true);
        _startGameButton.interactable = true;
        SetLobbyStatus("メンバーを待っています...");
        RefreshLobbyPanel();
    }

    private void OnRoomJoined()
    {
        ShowPanel(_lobbyPanel);
        _startGameButton.gameObject.SetActive(false);
        SetLobbyStatus("ホストのゲーム開始を待っています...");
        RefreshLobbyPanel();
    }

    private void RefreshLobbyPanel()
    {
        if (LobbyManager.Instance == null) return;

        int count = LobbyManager.Instance.PlayerCount;
        _playerListText.text = $"プレイヤー: {count} / 4";
        SetLobbyStatus(LobbyManager.Instance.IsHost
            ? $"{count}人参加中　開始準備OK（最低2人推奨）"
            : "ホストのゲーム開始を待っています...");
    }

    private void OnLobbyError(string message)
    {
        SetInteractable(true);
        SetStatus(message);
    }

    private void OnGameStarted()
    {
        // シーン遷移は LobbyManager → NGO SceneManager が処理するため不要
        SetLobbyStatus("ゲーム開始！");
    }

    // ── ヘルパー ─────────────────────────────────────────────────
    private string GetPlayerName()
    {
        string name = _playerNameInput?.text.Trim();
        return string.IsNullOrEmpty(name) ? DEFAULT_PLAYER_NAME : name;
    }

    private void ShowPanel(GameObject target)
    {
        _topPanel?.SetActive(_topPanel == target);
        _lobbyPanel?.SetActive(_lobbyPanel == target);
        _loadingPanel?.SetActive(_loadingPanel == target);
    }

    private void SetInteractable(bool value)
    {
        _createRoomButton.interactable = value;
        _joinRoomButton.interactable   = value;
    }

    private void SetStatus(string message)
    {
        if (_statusText != null) _statusText.text = message;
    }

    private void SetLobbyStatus(string message)
    {
        if (_lobbyStatusText != null) _lobbyStatusText.text = message;
    }
}
