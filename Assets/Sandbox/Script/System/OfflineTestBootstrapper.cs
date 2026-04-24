using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// OfflineTestScene 専用ブートストラッパー。
///
/// 【目的】
/// UGS（Unity Gaming Services）/ Lobby の初期化をスキップし、
/// NGO（Netcode for GameObjects）を Host モードでローカル起動する。
/// Host = Server + Client が同一インスタンスで動作するため、
/// NetworkBehaviour の IsServer / IsOwner / IsHost がすべて true となり、
/// ゲームループ全体（ベースキャンプ→登攀→帰還→リザルト）をオフラインで検証できる。
///
/// 【使い方】
/// 1. OfflineTestScene を開いて Play する
/// 2. 自動的に Host モードで起動し、NetworkPlayerSpawner がプレイヤーをスポーンする
/// 3. デバッグキーでゲームフローを任意に操作できる
///
/// 【デバッグキー一覧】
/// F1 : プレイヤー即死（死亡 → 幽霊遷移テスト）
/// F2 : ヘリ呼び出し（HelicopterController.CallHelicopter）
/// F3 : 強制帰還（ExpeditionManager.ReturnToBase）
/// F4 : 遺物にダメージ25（RelicDamageTracker 確認用）
/// F5 : 遠征開始（BasecampShop をスキップして直接 Climbing フェーズへ）
/// F6 : 全遺物を ReturnZone 前へ移動（運搬テスト省略）
/// F7 : 幽霊モードで祠を自動発見（ReviveShrine インタラクトをシミュレート）
/// F8 : 天候を次へ切り替え
/// F9 : スタミナをゼロにする（スタミナ切れ挙動テスト）
/// F10: デバッグオーバーレイ表示切替
/// </summary>
public class OfflineTestBootstrapper : MonoBehaviour
{
    // ── インスペクター ─────────────────────────────────────────
    [Header("オフライン設定")]
    [SerializeField] private string _offlinePlayerName = "OfflinePlayer";
    [SerializeField] private bool   _autoStartHost     = true;

    [Header("デバッグUI")]
    [SerializeField] private bool   _showDebugOverlay  = true;
    [SerializeField] private float  _overlayWidth      = 280f;

    // ── プライベート ──────────────────────────────────────────
    private bool      _initialized;
    private GUIStyle  _boxStyle;
    private GUIStyle  _labelStyle;
    private GUIStyle  _headerStyle;
    private string    _statusMsg  = "起動中...";
    private float     _msgTimer;
    private float     _cachedUiScale = -1f;

    // ── ライフサイクル ────────────────────────────────────────
    private void Start()
    {
        if (_autoStartHost)
            StartCoroutine(StartHostCoroutine());
    }

    private IEnumerator StartHostCoroutine()
    {
        // NetworkManager.Singleton が設定されるまで最大10フレーム待機
        // (スクリプト実行順によって Awake の完了が遅れる場合がある)
        for (int i = 0; i < 10; i++)
        {
            if (NetworkManager.Singleton != null) break;
            yield return null;
        }

        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[OfflineBoot] NetworkManager が見つかりません。" +
                           "シーンに NetworkManager GameObject を配置してください。");
            _statusMsg = "ERROR: NetworkManager なし";
            yield break;
        }

        // Unity Transport をローカルループバックに設定
        // ポート 7777 が前回の Play セッションで残留している場合に備えて空きポートを探す
        var transport = nm.GetComponent<UnityTransport>();
        if (transport != null)
        {
            ushort port = FindAvailableUdpPort(7777);
            transport.SetConnectionData("127.0.0.1", port);
            Debug.Log($"[OfflineBoot] UDP ポート {port} を使用します。");
        }

        // SpawnManager が Host 起動前に同一遺物プレハブを複数生成すると、
        // NGO の ScenePlacedObjects 登録で GlobalObjectIdHash が衝突して StartHost が失敗する。
        // ここで重複クローンを間引き、1フレーム待って破棄を反映してから起動する。
        int trimmed = TrimDuplicateNetworkRelicClones();
        if (trimmed > 0)
        {
            Debug.Log($"[OfflineBoot] Host 起動前に重複遺物クローンを {trimmed} 個削除しました。");
            yield return null;
        }

        // Host モードで起動（= Server + Client の両役を同一プロセスで担う）
        bool ok = nm.StartHost();
        if (!ok)
        {
            Debug.LogError("[OfflineBoot] StartHost() が失敗しました。");
            _statusMsg = "ERROR: StartHost 失敗";
            yield break;
        }

        // 1フレーム待機 → NetworkPlayerSpawner.OnNetworkSpawn が呼ばれるのを待つ
        yield return null;

        // スコアトラッカーにローカルプレイヤーを登録
        GameServices.Score?.RegisterPlayer(0, _offlinePlayerName);

        _initialized = true;
        _statusMsg   = "Host 起動完了";
        Debug.Log("[OfflineBoot] NGO Host 起動完了。ゲームループをオフラインで検証開始。");
    }

    /// <summary>
    /// Host 起動前にシーン上の重複した Network 遺物クローンを除去する。
    /// 同一プレハブ由来の "(Clone)" が複数あると NGO がハッシュ衝突を起こすため、
    /// 1個を残して残りを破棄する。
    /// </summary>
    private static int TrimDuplicateNetworkRelicClones()
    {
        var relics = Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None);
        var seenNames = new HashSet<string>();
        int removed = 0;

        foreach (var relic in relics)
        {
            if (relic == null) continue;

            var go = relic.gameObject;
            if (!go.name.Contains("(Clone)")) continue;
            if (!relic.TryGetComponent<NetworkObject>(out _)) continue;

            string key = go.name.Replace("(Clone)", string.Empty).Trim();
            if (seenNames.Add(key)) continue;

            Object.Destroy(go);
            removed++;
        }

        return removed;
    }

    // ── Update ────────────────────────────────────────────────
    private void Update()
    {
        if (_msgTimer > 0f) _msgTimer -= Time.deltaTime;

        if (!_initialized) return;
        HandleDebugKeys();
    }

    private void HandleDebugKeys()
    {
        if (Input.GetKeyDown(KeyCode.F10))
        {
            _showDebugOverlay = !_showDebugOverlay;
            SetMsg(_showDebugOverlay ? "F10: デバッグUI ON" : "F10: デバッグUI OFF");
        }

        if (Input.GetKeyDown(KeyCode.F1)) KillLocalPlayer();
        if (Input.GetKeyDown(KeyCode.F2)) CallHelicopter();
        if (Input.GetKeyDown(KeyCode.F3)) ForceReturn();
        if (Input.GetKeyDown(KeyCode.F4)) DamageFirstRelic();
        if (Input.GetKeyDown(KeyCode.F5)) ForceStartExpedition();
        if (Input.GetKeyDown(KeyCode.F6)) MoveRelicsToReturnZone();
        if (Input.GetKeyDown(KeyCode.F7)) ReviveGhost();
        if (Input.GetKeyDown(KeyCode.F8)) CycleWeather();
        if (Input.GetKeyDown(KeyCode.F9)) DrainLocalStamina();
    }

    // ── デバッグアクション ────────────────────────────────────
    private void KillLocalPlayer()
    {
        var health = Object.FindFirstObjectByType<PlayerHealthSystem>();
        if (health == null) { SetMsg("PlayerHealthSystem なし"); return; }
        health.TakeDamage(health.MaxHp);
        SetMsg("F1: プレイヤー即死");
    }

    private void CallHelicopter()
    {
        var heli = Object.FindFirstObjectByType<HelicopterController>();
        if (heli == null) { SetMsg("HelicopterController なし"); return; }
        // フレアが上空から撃たれた想定でプレイヤー位置を起点に呼び出す
        var player = Object.FindFirstObjectByType<PlayerHealthSystem>();
        Vector3 origin = player != null ? player.transform.position : Vector3.up * 30f;
        heli.CallHelicopter(origin);
        SetMsg("F2: ヘリ呼び出し");
    }

    private void ForceReturn()
    {
        var em = ExpeditionManager.Instance;
        if (em == null) { SetMsg("ExpeditionManager なし"); return; }
        if (em.Phase != ExpeditionPhase.Climbing)
        {
            SetMsg($"F3: 帰還不可（現在フェーズ: {em.Phase}）");
            return;
        }
        em.ReturnToBase(true);
        SetMsg("F3: 強制帰還");
    }

    private void DamageFirstRelic()
    {
        var relic = Object.FindFirstObjectByType<RelicBase>();
        if (relic == null) { SetMsg("RelicBase なし"); return; }
        relic.ApplyDamage(25f);
        SetMsg($"F4: {relic.RelicName} に 25 ダメージ");
    }

    private void ForceStartExpedition()
    {
        var em = ExpeditionManager.Instance;
        if (em == null) { SetMsg("ExpeditionManager なし"); return; }
        if (em.Phase != ExpeditionPhase.Basecamp)
        {
            SetMsg($"F5: 開始不可（現在フェーズ: {em.Phase}）");
            return;
        }
        em.StartExpedition();
        SetMsg("F5: 遠征強制開始");
    }

    private void MoveRelicsToReturnZone()
    {
        var returnZone = Object.FindFirstObjectByType<ReturnZone>();
        if (returnZone == null) { SetMsg("ReturnZone なし"); return; }

        var relics = Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None);
        if (relics.Length == 0) { SetMsg("遺物なし"); return; }

        Vector3 zonePos = returnZone.transform.position + Vector3.up * 1f;
        for (int i = 0; i < relics.Length; i++)
        {
            if (relics[i] == null) continue;
            relics[i].transform.position = zonePos + Vector3.right * (i * 1.5f);
        }
        SetMsg($"F6: 遺物 {relics.Length} 個を ReturnZone 前へ移動");
    }

    private void ReviveGhost()
    {
        var shrines = Object.FindObjectsByType<ReviveShrine>(FindObjectsSortMode.None);
        if (shrines.Length == 0) { SetMsg("ReviveShrine なし"); return; }

        foreach (var shrine in shrines)
        {
            if (!shrine.IsAvailable) continue;
            shrine.Use();
            SetMsg($"F7: {shrine.gameObject.name} で復活");

            // GhostSystem を直接 Alive に戻す。
            // Heal() は _isDead=true のとき早期 return するため、Revive(50f) で
            // _isDead を明示的に解除してから HP を設定する（GhostSystem.Revive と同じ経路）。
            var ghost = Object.FindFirstObjectByType<GhostSystem>();
            if (ghost != null && ghost.IsGhost)
            {
                var sm = ghost.GetComponent<PlayerStateMachine>();
                sm?.Transition(PlayerState.Alive);
                ghost.GetComponent<PlayerHealthSystem>()?.Revive(50f);
            }
            return;
        }
        SetMsg("F7: 使用可能な祠がありません");
    }

    private void CycleWeather()
    {
        var ws = GameServices.Weather as WeatherSystem;
        if (ws == null) { SetMsg("WeatherSystem なし"); return; }
        ws.CycleToNextWeather();
        SetMsg($"F8: 天候切り替え → {ws.CurrentWeather}");
    }

    private void DrainLocalStamina()
    {
        var stamina = Object.FindFirstObjectByType<StaminaSystem>();
        if (stamina == null) { SetMsg("StaminaSystem なし"); return; }
        stamina.ConsumeAll();
        SetMsg("F9: スタミナゼロ");
    }

    // ── ステータスメッセージ ──────────────────────────────────
    private void SetMsg(string msg)
    {
        _statusMsg = msg;
        _msgTimer  = 3f;
        Debug.Log($"[OfflineBoot] {msg}");
    }

    // ── Debug GUI ─────────────────────────────────────────────
    private void OnGUI()
    {
        if (!_showDebugOverlay) return;

        float uiScale     = Mathf.Clamp(Screen.height / 1080f, 0.9f, 1.35f);
        float panelWidth  = Mathf.Clamp(_overlayWidth * uiScale, 320f, 460f);
        float panelHeight = Mathf.Clamp(370f * uiScale, 360f, 560f);
        float panelPad    = 12f * uiScale;

        EnsureStyles(uiScale);

        var em  = ExpeditionManager.Instance;
        var nm  = NetworkManager.Singleton;
        var ws  = GameServices.Weather;

        string netStat   = nm != null && nm.IsHost ? "✓ Host (Offline)" : "✗ 未接続";
        string phase     = em != null ? em.Phase.ToString() : "N/A";
        string weather   = ws != null ? ws.CurrentWeather.ToString() : "N/A";
        string msgColor  = _msgTimer > 0f ? "<color=#FFD700>" : "<color=#AAAAAA>";

        var sb = new StringBuilder();
        sb.AppendLine("<b>== Offline Test Scene ==</b>");
        sb.AppendLine($"ネット : {netStat}");
        sb.AppendLine($"フェーズ: {phase}");
        sb.AppendLine($"天候  : {weather}");
        sb.AppendLine("─────────────────");
        sb.AppendLine("[F1] プレイヤー即死");
        sb.AppendLine("[F2] ヘリ呼び出し");
        sb.AppendLine("[F3] 強制帰還");
        sb.AppendLine("[F4] 遺物にダメージ25");
        sb.AppendLine("[F5] 遠征強制開始");
        sb.AppendLine("[F6] 遺物を帰還エリアへ");
        sb.AppendLine("[F7] 祠で幽霊復活");
        sb.AppendLine("[F8] 天候切り替え");
        sb.AppendLine("[F9] スタミナゼロ");
        sb.AppendLine("[F10] デバッグUI ON/OFF");
        sb.AppendLine("─────────────────");
        sb.Append($"{msgColor}");
        sb.Append(_statusMsg);
        sb.Append("</color>");

        GUI.Box(new Rect(10f, 10f, panelWidth, panelHeight), GUIContent.none, _boxStyle);
        GUI.Label(
            new Rect(10f + panelPad, 10f + panelPad, panelWidth - panelPad * 2f, panelHeight - panelPad * 2f),
            sb.ToString(),
            _labelStyle
        );
    }

    private void EnsureStyles(float uiScale)
    {
        if (_boxStyle == null)
        {
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, new Color(0f, 0f, 0f, 0.7f)) },
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
            };

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold,
            };
        }

        if (Mathf.Abs(_cachedUiScale - uiScale) < 0.01f) return;

        int fontSize = Mathf.RoundToInt(Mathf.Lerp(15f, 20f, Mathf.InverseLerp(0.9f, 1.35f, uiScale)));
        _labelStyle.fontSize = fontSize;
        _headerStyle.fontSize = fontSize + 1;
        _cachedUiScale = uiScale;
    }

    private static Texture2D MakeTex(int w, int h, Color col)
    {
        var pix = new Color[w * h];
        for (int i = 0; i < pix.Length; i++) pix[i] = col;
        var tex = new Texture2D(w, h);
        tex.SetPixels(pix);
        tex.Apply();
        return tex;
    }

    /// <summary>
    /// startPort から順に空き UDP ポートを探して返す。
    /// 前回 Play セッションでポートが残留している場合の保険。
    /// </summary>
    private static ushort FindAvailableUdpPort(ushort startPort = 7777)
    {
        for (ushort port = startPort; port < startPort + 20; port++)
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                return port;
            }
            catch (SocketException)
            {
                // ポート使用中 → 次を試す
            }
        }
        return startPort;
    }
}
