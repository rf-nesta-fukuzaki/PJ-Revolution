using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// SandboxOfflineCombined 向けオンライン/LAN セッション開始 UI とモード切替。
/// </summary>
[DefaultExecutionOrder(-10)]
public sealed class SandboxOnlineSessionBootstrap : MonoBehaviour
{
    public static SandboxOnlineSessionBootstrap Instance { get; private set; }

    [Header("起動")]
    [Tooltip("true なら従来どおり即オフライン Host。false ならセッション UI から選択。")]
    [SerializeField] private bool _autoStartOfflineLocal = true;

    [Header("LAN")]
    [SerializeField] private ushort _listenPort = 7777;
    [SerializeField] private string _joinAddress = "127.0.0.1";

    [Header("UI")]
    [SerializeField] private bool _showSessionGui = true;

    private bool _sessionStarted;
    private string _status = "セッション未開始";
    private string _lanJoinAddresses = string.Empty;
    private ushort _activeListenPort;
    private OfflineHostBootstrap _offlineBoot;
    private readonly LanDiscovery _discovery = new();
    private bool _scanning;
    private bool _transitioning;

    public bool SessionStarted => _sessionStarted;
    public bool DeferOfflineAutoHost => !_autoStartOfflineLocal && !_sessionStarted;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        _offlineBoot = new OfflineHostBootstrap();

        EnsurePartyComponents();
    }

    private void OnDestroy()
    {
        _discovery.Dispose();
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.name.Contains("SandboxOfflineCombined")) return;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            // 既に起動済み。別ブートストラッパー（オフライン自動 Host 等）が
            // モードを設定していればそれを尊重し、未設定時のみオフライン扱いとする。
            _sessionStarted = true;
            if (!LocalCoopSettings.IsActive)
                LocalCoopSettings.Configure(PartyPlayMode.OfflineLocal);

            if (LocalCoopSettings.IsOnline)
            {
                RefreshLanJoinHintFromTransport(nm);
                if (nm.IsHost)
                    StartDiscoveryResponder();
                _status = nm.IsHost ? "オンライン Host（接続済み）" : "オンライン Client（接続済み）";
            }
            else
            {
                _status = "オフライン ローカル Co-op 稼働中";
            }
            return;
        }

        if (_autoStartOfflineLocal)
            StartCoroutine(StartOfflineLocalCoroutine());
        else
            _status = "モードを選択してください";
    }

    private void EnsurePartyComponents()
    {
        if (GetComponent<NetworkPartyManager>() == null)
            gameObject.AddComponent<NetworkPartyManager>();

        if (Object.FindFirstObjectByType<SandboxLocalCoopBootstrap>() == null)
        {
            var go = new GameObject("SandboxLocalCoop");
            go.AddComponent<SandboxLocalCoopBootstrap>();
        }
    }

    public bool ShowSessionGui => _showSessionGui;

    /// <summary>
    /// F10 統合デバッグメニュー（OfflineDebugOverlay）から呼ばれ、
    /// セッション操作（オフライン/オンライン Host・LAN 検索・Join）を描画する。
    /// 外枠（Box/Area/Scroll）は呼び出し側が用意する。
    /// </summary>
    public void DrawSessionGui()
    {
        if (!_showSessionGui) return;

        var nm = NetworkManager.Singleton;
        bool showLanJoinHint = _sessionStarted
                               && LocalCoopSettings.IsOnline
                               && nm != null
                               && nm.IsHost
                               && !string.IsNullOrEmpty(_lanJoinAddresses);

        GUILayout.Label("== セッション ==");
        GUILayout.Label(_status);
        GUILayout.Label($"Mode: {LocalCoopSettings.PlayMode}  Humans: {LocalCoopSettings.HumanCount}/{LocalCoopSettings.MaxPartySize}");

        if (!_sessionStarted)
        {
            if (GUILayout.Button("オフライン ローカル Co-op (Host)"))
                StartCoroutine(StartOfflineLocalCoroutine());

            GUILayout.Space(6f);
            GUILayout.Label("── オンライン LAN ──");

            if (GUILayout.Button($"オンライン LAN Host (UDP {_listenPort})"))
                StartCoroutine(StartOnlineHostCoroutine());

            GUILayout.Space(6f);
            DrawDiscoveryJoinSection();

            GUILayout.Space(6f);
            GUILayout.Label("── 手動 Join（IP 直接指定）──");
            GUILayout.BeginHorizontal();
            GUILayout.Label("接続先 IP", GUILayout.Width(70f));
            _joinAddress = GUILayout.TextField(_joinAddress, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            if (GUILayout.Button($"手動 Join → {_joinAddress}:{_listenPort}"))
                StartCoroutine(StartOnlineClientCoroutine());
        }
        else
        {
            if (nm != null && nm.IsListening)
                GUILayout.Label(nm.IsHost ? "役割: Host" : "役割: Client");
            if (NetworkPartyManager.Instance != null && LocalCoopSettings.IsOnline)
                GUILayout.Label($"Party ready: {NetworkPartyManager.Instance.IsPartyReady}");

            if (showLanJoinHint)
            {
                GUILayout.Space(6f);
                GUILayout.Label("── 他 PC / Unity に入力するアドレス ──", GUI.skin.box);
                foreach (string line in _lanJoinAddresses.Split('\n'))
                    GUILayout.Label(line, GUI.skin.box);

                GUILayout.Label("※ 127.0.0.1 は Host と同じ PC のみ有効");
                if (GUILayout.Button("先頭の IP を Join 欄にコピー（この端末が Client のとき）"))
                    _joinAddress = LanAddressUtility.GetPrimaryLanIPv4();
            }
            else if (LocalCoopSettings.IsOnline && nm != null && nm.IsClient)
            {
                GUILayout.Space(6f);
                GUILayout.Label($"接続先: {_joinAddress}:{_activeListenPort}");
            }

            GUILayout.Space(8f);
            GUILayout.Label("── モード切替（セッション再起動）──", GUI.skin.box);

            bool isOffline = !LocalCoopSettings.IsOnline;
            bool isOnlineHost = LocalCoopSettings.IsOnline && nm != null && nm.IsHost;

            GUI.enabled = !isOffline;
            if (GUILayout.Button("オフライン ローカルに切替"))
                SwitchToOfflineHost();

            GUI.enabled = !isOnlineHost;
            if (GUILayout.Button("オンライン Host に切替"))
                SwitchToOnlineHost();
            GUI.enabled = true;

            GUILayout.BeginHorizontal();
            GUILayout.Label("参加先IP", GUILayout.Width(60f));
            _joinAddress = GUILayout.TextField(_joinAddress, GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            if (GUILayout.Button($"オンライン ゲスト参加に切替 → {_joinAddress}:{_listenPort}"))
                SwitchToOnlineGuest(_joinAddress);

            GUILayout.Space(4f);
            if (GUILayout.Button("セッション停止（メニューへ戻る）"))
                StopSession();
        }
    }

    private void DrawDiscoveryJoinSection()
    {
        GUILayout.Label("── ホストを自動検索（IP 入力不要）──", GUI.skin.box);

        if (!_scanning)
        {
            if (GUILayout.Button("LAN ホストを検索"))
            {
                _discovery.StartClientScan();
                _scanning = true;
            }
            return;
        }

        var hosts = _discovery.GetHosts();
        GUILayout.Label(hosts.Count > 0
            ? $"検索中... {hosts.Count} 件のホストを検出"
            : "検索中... ホストを探しています");

        foreach (var host in hosts)
        {
            if (GUILayout.Button($"参加 → {host.HostName}  [{host.Address}:{host.GamePort}]"))
            {
                _joinAddress = host.Address;
                _listenPort = host.GamePort;
                StartCoroutine(StartOnlineClientCoroutine());
                return;
            }
        }

        if (GUILayout.Button("検索を停止"))
        {
            _discovery.Stop();
            _scanning = false;
        }
    }

    private void RefreshLanJoinHintFromTransport(NetworkManager nm)
    {
        if (nm == null || !nm.IsHost) return;

        var transport = nm.GetComponent<UnityTransport>();
        _activeListenPort = transport != null ? transport.ConnectionData.Port : _listenPort;
        _lanJoinAddresses = LanAddressUtility.FormatJoinAddresses(_activeListenPort);
    }

    public IEnumerator StartOfflineLocalCoroutine()
    {
        if (_sessionStarted) yield break;
        _sessionStarted = true;

        LocalCoopSettings.Configure(PartyPlayMode.OfflineLocal);
        _status = "オフライン Host 起動中...";

        yield return _offlineBoot.StartHost("OfflinePlayer");
        _status = _offlineBoot.IsInitialized ? "オフライン ローカル Co-op 稼働中" : "Host 起動失敗";
        if (_offlineBoot.IsInitialized)
            SandboxLocalCoopBootstrap.Instance?.RebuildParty();
    }

    public IEnumerator StartOnlineHostCoroutine()
    {
        if (_sessionStarted) yield break;
        _sessionStarted = true;

        LocalCoopSettings.Configure(PartyPlayMode.Online);
        _status = "オンライン Host 起動中...";

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            _status = "NetworkManager がありません";
            yield break;
        }

        var transport = nm.GetComponent<UnityTransport>();
        if (transport != null)
        {
            ushort port = OfflineHostBootstrap.FindAvailableUdpPort(_listenPort);
            transport.SetConnectionData("0.0.0.0", port);
            _status = $"Host 起動 (UDP {port})...";
        }

        int trimmed = OfflineHostBootstrap.TrimDuplicateNetworkRelicClones();
        if (trimmed > 0) yield return null;

        if (!nm.StartHost())
        {
            _status = "StartHost 失敗";
            _sessionStarted = false;
            yield break;
        }

        yield return null;

        _activeListenPort = transport != null ? transport.ConnectionData.Port : _listenPort;
        _lanJoinAddresses = LanAddressUtility.FormatJoinAddresses(_activeListenPort);
        StartDiscoveryResponder();
        SandboxLocalCoopBootstrap.Instance?.RebuildParty();
        _status = $"オンライン Host 稼働中 (UDP {_activeListenPort})";
    }

    private void StartDiscoveryResponder()
    {
        ushort gamePort = _activeListenPort != 0 ? _activeListenPort : _listenPort;
        _discovery.StartResponder(gamePort, $"{SystemInfo.deviceName} (Host)");
    }

    public IEnumerator StartOnlineClientCoroutine()
    {
        if (_sessionStarted) yield break;
        _sessionStarted = true;
        _discovery.Stop();
        _scanning = false;

        LocalCoopSettings.Configure(PartyPlayMode.Online);
        _status = "クライアント接続中...";

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            _status = "NetworkManager がありません";
            yield break;
        }

        var transport = nm.GetComponent<UnityTransport>();
        _activeListenPort = _listenPort;
        if (transport != null)
            transport.SetConnectionData(_joinAddress, _activeListenPort);

        if (!nm.StartClient())
        {
            _status = "StartClient 失敗";
            _sessionStarted = false;
            yield break;
        }

        yield return null;
        SandboxLocalCoopBootstrap.Instance?.RebuildParty();
        _status = $"クライアント接続: {_joinAddress}:{_activeListenPort}";
    }

    // ── セッション切替（オフライン/オンライン・ホスト/ゲスト）──────────────
    public void SwitchToOfflineHost() => StartCoroutine(SwitchSessionCoroutine(SessionTarget.OfflineHost, null));
    public void SwitchToOnlineHost() => StartCoroutine(SwitchSessionCoroutine(SessionTarget.OnlineHost, null));
    public void SwitchToOnlineGuest(string joinAddress) => StartCoroutine(SwitchSessionCoroutine(SessionTarget.OnlineGuest, joinAddress));
    public void StopSession() => StartCoroutine(SwitchSessionCoroutine(SessionTarget.MenuOnly, null));

    private enum SessionTarget { MenuOnly, OfflineHost, OnlineHost, OnlineGuest }

    private IEnumerator SwitchSessionCoroutine(SessionTarget target, string joinAddress)
    {
        if (_transitioning)
            yield break;
        _transitioning = true;

        // ゲスト切替は接続先を先に確定（停止前に値が変わらないように）。
        if (target == SessionTarget.OnlineGuest && !string.IsNullOrWhiteSpace(joinAddress))
            _joinAddress = joinAddress.Trim();

        yield return ShutdownCurrentCoroutine();

        switch (target)
        {
            case SessionTarget.OfflineHost:
                yield return StartOfflineLocalCoroutine();
                break;
            case SessionTarget.OnlineHost:
                yield return StartOnlineHostCoroutine();
                break;
            case SessionTarget.OnlineGuest:
                yield return StartOnlineClientCoroutine();
                break;
            case SessionTarget.MenuOnly:
                _status = "セッション停止（モードを選択してください）";
                break;
        }

        _transitioning = false;
    }

    private IEnumerator ShutdownCurrentCoroutine()
    {
        _discovery.Stop();
        _scanning = false;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            // 非ネットワークアクター（オフライン NPC 等）は Shutdown では消えないため先に破棄。
            SandboxLocalCoopBootstrap.Instance?.RebuildPartyTeardownOnly();
            nm.Shutdown();

            // IsListening 解除に加えて ShutdownInProgress 完了も待つ（再起動の安定化）。
            float t = 0f;
            while ((nm.IsListening || nm.ShutdownInProgress) && t < 5f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }

            // 念のため数フレーム空けて NGO 内部のクリーンアップ完了を待つ。
            yield return null;
            yield return null;
        }

        _sessionStarted = false;
        _lanJoinAddresses = string.Empty;
        _activeListenPort = 0;
        yield return null;
    }
}
