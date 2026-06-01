using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// OfflineTestScene 専用ブートストラッパー（薄いオーケストレーター）。
/// Host 起動 / デバッグコマンド / オーバーレイ表示を各コンポーネントに委譲する。
/// </summary>
public class OfflineTestBootstrapper : MonoBehaviour
{
    [Header("オフライン設定")]
    [SerializeField] private string _offlinePlayerName = "OfflinePlayer";
    [SerializeField] private bool _autoStartHost = true;

    [Header("デバッグUI")]
    [SerializeField] private bool _showDebugOverlay = true;

    [Header("敵モンスター (PEAK/R.E.P.O. パリティ)")]
    [SerializeField] private bool _spawnEnemies = true;

    private readonly OfflineHostBootstrap _hostBootstrap = new();
    private OfflineDebugOverlay _overlay;

    private void Awake()
    {
        EnsureLocalCoopForCombinedScene();

        _overlay = GetComponent<OfflineDebugOverlay>();
        if (_overlay == null)
            _overlay = gameObject.AddComponent<OfflineDebugOverlay>();

        _overlay.Visible = _showDebugOverlay;

        // モンスタースポナーを「無ければ生成」（非破壊）。Climbing フェーズ開始で湧く。
        if (_spawnEnemies && Object.FindFirstObjectByType<EnemySpawner>() == null)
            new GameObject("EnemySpawner").AddComponent<EnemySpawner>();

        // 抽出ノルマシステムを「無ければ生成」（非破壊・R.E.P.O. 勝敗ループ）。
        if (Object.FindFirstObjectByType<ExtractionQuotaSystem>() == null)
            new GameObject("ExtractionQuotaSystem").AddComponent<ExtractionQuotaSystem>();

        // ノルマ/所持金/アップグレード HUD を「無ければ生成」（非破壊）。
        if (Object.FindFirstObjectByType<QuotaUpgradeHud>() == null)
            new GameObject("QuotaUpgradeHud").AddComponent<QuotaUpgradeHud>();
    }

    private void Start()
    {
        if (!_autoStartHost) return;

        // SandboxOfflineCombined ではセッションブートストラッパーが Host 起動を
        // 一元管理する（オフライン自動 Host / オンライン UI の両方）。
        // 二重 StartHost（"Cannot start Host while an instance is already running"）を
        // 避けるため、セッションが存在する場合は起動を完全に委譲する。
        if (SandboxOnlineSessionBootstrap.Instance != null)
            return;

        StartCoroutine(BootSequence());
    }

    private static void EnsureLocalCoopForCombinedScene()
    {
        var scene = SceneManager.GetActiveScene();
        if (!scene.name.Contains("SandboxOfflineCombined")) return;

        var nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm != null && nm.GetComponent<SandboxOnlineSessionBootstrap>() == null)
            nm.gameObject.AddComponent<SandboxOnlineSessionBootstrap>();
        if (nm != null && nm.GetComponent<NetworkPartyManager>() == null)
            nm.gameObject.AddComponent<NetworkPartyManager>();

        if (Object.FindFirstObjectByType<SandboxLocalCoopBootstrap>() != null) return;

        var go = new GameObject("SandboxLocalCoop");
        go.AddComponent<SandboxLocalCoopBootstrap>();
        Debug.Log("[OfflineBoot] SandboxLocalCoopBootstrap を自動生成しました。");
    }

    private IEnumerator BootSequence()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            _overlay.SetStatusMessage(nm.IsHost ? "既に Host 接続済み" : "既に Client 接続済み");
            yield break;
        }

        LocalCoopSettings.Configure(PartyPlayMode.OfflineLocal);
        _overlay.SetStatusMessage("起動中...");
        yield return _hostBootstrap.StartHost(_offlinePlayerName);

        _overlay.SetStatusMessage(_hostBootstrap.IsInitialized
            ? "Host 起動完了"
            : "ERROR: Host 起動失敗");
    }

    private void Update()
    {
        // Host 起動者がセッションブートストラッパーであってもデバッグキーを有効にする。
        var nm = NetworkManager.Singleton;
        bool hostReady = _hostBootstrap.IsInitialized || (nm != null && nm.IsListening);
        if (!hostReady) return;
        HandleDebugKeys();
    }

    private void HandleDebugKeys()
    {
        if (InputStateReader.KeyPressedThisFrame(Key.F10))
        {
            _overlay.Visible = !_overlay.Visible;
            _overlay.SetStatusMessage(_overlay.Visible ? "F10: デバッグUI ON" : "F10: デバッグUI OFF");
        }

        if (InputStateReader.KeyPressedThisFrame(Key.F1))  _overlay.SetStatusMessage(OfflineDebugCommands.KillLocalPlayer());
        if (InputStateReader.KeyPressedThisFrame(Key.F2))  _overlay.SetStatusMessage(OfflineDebugCommands.CallHelicopter());
        if (InputStateReader.KeyPressedThisFrame(Key.F3))  _overlay.SetStatusMessage(OfflineDebugCommands.ForceReturn());
        if (InputStateReader.KeyPressedThisFrame(Key.F4))  _overlay.SetStatusMessage(OfflineDebugCommands.DamageFirstRelic());
        if (InputStateReader.KeyPressedThisFrame(Key.F5))  _overlay.SetStatusMessage(OfflineDebugCommands.ForceStartExpedition());
        if (InputStateReader.KeyPressedThisFrame(Key.F6))  _overlay.SetStatusMessage(OfflineDebugCommands.MoveRelicsToReturnZone());
        if (InputStateReader.KeyPressedThisFrame(Key.F7))  _overlay.SetStatusMessage(OfflineDebugCommands.ReviveGhost());
        if (InputStateReader.KeyPressedThisFrame(Key.F8))  _overlay.SetStatusMessage(OfflineDebugCommands.CycleWeather());
        if (InputStateReader.KeyPressedThisFrame(Key.F9))  _overlay.SetStatusMessage(OfflineDebugCommands.DrainLocalStamina());
    }
}
