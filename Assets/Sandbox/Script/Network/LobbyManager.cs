using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

/// <summary>
/// GDD §8.1 — ルームコード制マルチプレイ管理。
/// ホストがルームを作成してコードを発行 → ゲストがコードで参加。
/// Unity Relay + Lobby によりNAT越えを自動処理。
/// 最大プレイヤー数: 4人。
/// </summary>
public class LobbyManager : MonoBehaviour
{
    public static LobbyManager Instance { get; private set; }

    // ── 定数 ────────────────────────────────────────────────────
    private const int    MAX_PLAYERS      = 4;
    private const string KEY_RELAY_CODE   = "RelayJoinCode";
    private const float  LOBBY_HEARTBEAT_INTERVAL = 15f;
    private const float  LOBBY_POLL_INTERVAL      = 1.5f;

    // ── 状態 ────────────────────────────────────────────────────
    private Lobby  _currentLobby;
    private float  _heartbeatTimer;
    private float  _pollTimer;
    private bool   _heartbeatRequestInFlight;
    private bool   _pollRequestInFlight;

    public bool    IsInLobby   => _currentLobby != null;
    public bool    IsHost      => _currentLobby != null
                               && _currentLobby.HostId == AuthenticationService.Instance.PlayerId;
    public string  RoomCode    => _currentLobby?.LobbyCode ?? string.Empty;
    public int     PlayerCount => _currentLobby?.Players?.Count ?? 0;

    // ── イベント ────────────────────────────────────────────────
    public event Action<string>  OnRoomCreated;    // ルームコードを渡す
    public event Action          OnRoomJoined;
    public event Action          OnLobbyUpdated;
    public event Action<string>  OnError;
    public event Action          OnGameStarted;

    // ── Unity ───────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);   // ルートに移動してから DDOL を呼ぶ
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        _heartbeatTimer -= Time.deltaTime;
        _pollTimer      -= Time.deltaTime;

        if (_heartbeatTimer <= 0f && !_heartbeatRequestInFlight)
            _ = HandleHeartbeatAsync();

        if (_pollTimer <= 0f && !_pollRequestInFlight)
            _ = HandleLobbyPollAsync();
    }

    // ── ルーム作成（ホスト）───────────────────────────────────────
    public async Task CreateRoomAsync(string playerName)
    {
        try
        {
            // 1. Relay アロケーション確保
            var allocation = await RelayService.Instance.CreateAllocationAsync(MAX_PLAYERS - 1);
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // 2. Lobby 作成
            var lobbyOptions = new CreateLobbyOptions
            {
                IsPrivate = true,
                Data = new Dictionary<string, DataObject>
                {
                    { KEY_RELAY_CODE, new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                },
                Player = BuildPlayerData(playerName)
            };

            _currentLobby = await LobbyService.Instance.CreateLobbyAsync(
                "PeakPlunderRoom", MAX_PLAYERS, lobbyOptions);

            // 3. NGO ホスト開始
            SetRelayData(new RelayServerData(allocation, "dtls"));
            NetworkManager.Singleton.StartHost();

            Debug.Log($"[Lobby] ルーム作成完了 — コード: {_currentLobby.LobbyCode}");
            OnRoomCreated?.Invoke(_currentLobby.LobbyCode);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Lobby] ルーム作成失敗: {ex.Message}");
            OnError?.Invoke($"ルーム作成失敗: {ex.Message}");
        }
    }

    // ── ルーム参加（ゲスト）───────────────────────────────────────
    public async Task JoinRoomAsync(string lobbyCode, string playerName)
    {
        try
        {
            // 1. Lobby に参加
            var joinOptions = new JoinLobbyByCodeOptions { Player = BuildPlayerData(playerName) };
            _currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, joinOptions);

            // 2. Relay 接続情報を取得
            string relayJoinCode = _currentLobby.Data[KEY_RELAY_CODE].Value;
            var joinAllocation   = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);

            // 3. NGO クライアント開始
            SetRelayData(new RelayServerData(joinAllocation, "dtls"));
            NetworkManager.Singleton.StartClient();

            Debug.Log($"[Lobby] ルーム参加完了 — コード: {lobbyCode}");
            OnRoomJoined?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Lobby] ルーム参加失敗: {ex.Message}");
            OnError?.Invoke($"ルーム参加失敗: {ex.Message}");
        }
    }

    // ── ゲーム開始（ホストのみ）────────────────────────────────────
    public async Task StartGameAsync(string gameSceneName = "Mountain01")
    {
        if (!IsHost) return;

        try
        {
            await LobbyService.Instance.UpdateLobbyAsync(_currentLobby.Id, new UpdateLobbyOptions
            {
                IsLocked = true
            });

            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName,
                UnityEngine.SceneManagement.LoadSceneMode.Single);

            OnGameStarted?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogError($"[Lobby] ゲーム開始失敗: {ex.Message}");
            OnError?.Invoke($"ゲーム開始失敗: {ex.Message}");
        }
    }

    // ── ロビー退出 ────────────────────────────────────────────────
    public async Task LeaveLobbyAsync()
    {
        if (_currentLobby == null) return;

        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;
            await LobbyService.Instance.RemovePlayerAsync(_currentLobby.Id, playerId);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Lobby] 退出中にエラー（無視）: {ex.Message}");
        }
        finally
        {
            _currentLobby = null;
            NetworkManager.Singleton.Shutdown();
        }
    }

    // ── ハートビート（ロビーが非アクティブ判定されないよう維持）──────
    private async Task HandleHeartbeatAsync()
    {
        // タイマーと in-flight チェックは Update() 側で行う
        if (!IsHost || _currentLobby == null) return;

        _heartbeatTimer = LOBBY_HEARTBEAT_INTERVAL;
        _heartbeatRequestInFlight = true;
        try
        {
            await LobbyService.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Lobby] ハートビート失敗: {ex.Message}");
        }
        finally
        {
            _heartbeatRequestInFlight = false;
        }
    }

    // ── ロビーポーリング（メンバー変化を検知）──────────────────────
    private async Task HandleLobbyPollAsync()
    {
        // タイマーと in-flight チェックは Update() 側で行う
        if (_currentLobby == null) return;

        _pollTimer = LOBBY_POLL_INTERVAL;
        _pollRequestInFlight = true;
        try
        {
            _currentLobby = await LobbyService.Instance.GetLobbyAsync(_currentLobby.Id);
            OnLobbyUpdated?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Lobby] ポーリング失敗: {ex.Message}");
        }
        finally
        {
            _pollRequestInFlight = false;
        }
    }

    // ── ヘルパー ─────────────────────────────────────────────────
    private Player BuildPlayerData(string playerName) => new()
    {
        Data = new Dictionary<string, PlayerDataObject>
        {
            { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Member, playerName) }
        }
    };

    private void SetRelayData(RelayServerData relayData)
    {
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("[Lobby] UnityTransport が見つかりません。Relay 設定に失敗しました。");
            return;
        }
        transport.SetRelayServerData(relayData);
    }

    private void OnDestroy()
    {
        // アプリ終了時にロビーから離脱
        if (_currentLobby != null)
            _ = LeaveLobbyAsync();
    }
}
