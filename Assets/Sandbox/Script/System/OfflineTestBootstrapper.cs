using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

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
        if (_autoStartHost)
            StartCoroutine(BootSequence());
    }

    private IEnumerator BootSequence()
    {
        _overlay.SetStatusMessage("起動中...");
        yield return _hostBootstrap.StartHost(_offlinePlayerName);

        _overlay.SetStatusMessage(_hostBootstrap.IsInitialized
            ? "Host 起動完了"
            : "ERROR: Host 起動失敗");
    }

    private void Update()
    {
        if (!_hostBootstrap.IsInitialized) return;
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
