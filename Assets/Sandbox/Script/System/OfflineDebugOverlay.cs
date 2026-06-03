using Unity.Netcode;
using UnityEngine;
using Sandbox.UI;

/// <summary>
/// F10 で開閉する統合デバッグメニュー（IMGUI）。
/// オンライン/オフラインのセッション操作（<see cref="SandboxOnlineSessionBootstrap"/>）と
/// デバッグコマンド（<see cref="OfflineDebugCommands"/>）を 1 つのパネルに集約する。
/// </summary>
public sealed class OfflineDebugOverlay : MonoBehaviour
{
    [SerializeField] private float _overlayWidth = 320f;

    private bool _visible = true;
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private GUIStyle _headerStyle;
    private float _cachedUiScale = -1f;
    private string _statusMessage = "起動中...";
    private float _messageTimer;
    private Vector2 _scroll;

    public bool Visible
    {
        get => _visible;
        set => _visible = value;
    }

    public void SetStatusMessage(string message, float durationSeconds = 3f)
    {
        _statusMessage = message ?? string.Empty;
        _messageTimer = durationSeconds;
        Debug.Log($"[OfflineBoot] {_statusMessage}");
    }

    private void Update()
    {
        if (_messageTimer > 0f)
            _messageTimer -= Time.deltaTime;
    }

    private void OnGUI()
    {
        if (!_visible) return;

        float uiScale = Mathf.Clamp(Screen.height / 1080f, 0.9f, 1.35f);
        float panelWidth = Mathf.Clamp(_overlayWidth * uiScale, 360f, 480f);
        float panelHeight = Mathf.Min(Screen.height - 24f, 720f * uiScale);

        EnsureStyles(uiScale);

        var rect = new Rect(10f, 10f, panelWidth, panelHeight);
        GUILayout.BeginArea(rect, GUIContent.none, _boxStyle);
        _scroll = GUILayout.BeginScrollView(_scroll);

        DrawStatusSection();

        var session = SandboxOnlineSessionBootstrap.Instance;
        if (session != null && session.ShowSessionGui)
        {
            GUILayout.Space(8f);
            // 暗い背景上でも読めるよう、委譲描画の間だけ既定ラベル色を共有クリームへ。
            var prevLabelColor = GUI.skin.label.normal.textColor;
            GUI.skin.label.normal.textColor = UiPalette.Cream;
            session.DrawSessionGui();
            GUI.skin.label.normal.textColor = prevLabelColor;
            GUI.enabled = true;
        }

        GUILayout.Space(8f);
        DrawDebugCommandSection();

        GUILayout.Space(6f);
        DrawMessageSection();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawStatusSection()
    {
        var expedition = GameServices.Expedition;
        var networkManager = NetworkManager.Singleton;
        var weather = GameServices.Weather;

        string netStat = networkManager != null && networkManager.IsListening
            ? (networkManager.IsHost ? "Host" : "Client")
            : "未接続";
        string phase = expedition != null ? expedition.Phase.ToString() : "N/A";
        string weatherLabel = weather != null ? weather.CurrentWeather.ToString() : "N/A";

        GUILayout.Label("== Debug Menu (F10) ==", _headerStyle);

        if (LocalCoopSettings.IsActive)
        {
            string mode = LocalCoopSettings.IsOnline ? "Online" : "Local";
            GUILayout.Label($"Co-op ({mode}): 人間 {LocalCoopSettings.HumanCount} / NPC {LocalCoopSettings.NpcFillCount} (計{LocalCoopSettings.MaxPartySize})", _labelStyle);
        }

        GUILayout.Label($"ネット: {netStat}   フェーズ: {phase}   天候: {weatherLabel}", _labelStyle);
    }

    private void DrawDebugCommandSection()
    {
        GUILayout.Label("== デバッグ操作 ==", _headerStyle);

        if (GUILayout.Button("[F1] プレイヤー即死"))        SetStatusMessage(OfflineDebugCommands.KillLocalPlayer());
        if (GUILayout.Button("[F2] ヘリ呼び出し"))          SetStatusMessage(OfflineDebugCommands.CallHelicopter());
        if (GUILayout.Button("[F3] 強制帰還"))              SetStatusMessage(OfflineDebugCommands.ForceReturn());
        if (GUILayout.Button("[F4] 遺物にダメージ25"))      SetStatusMessage(OfflineDebugCommands.DamageFirstRelic());
        if (GUILayout.Button("[F5] 遠征強制開始"))          SetStatusMessage(OfflineDebugCommands.ForceStartExpedition());
        if (GUILayout.Button("[F6] 遺物を帰還エリアへ"))    SetStatusMessage(OfflineDebugCommands.MoveRelicsToReturnZone());
        if (GUILayout.Button("[F7] 祠で幽霊復活"))          SetStatusMessage(OfflineDebugCommands.ReviveGhost());
        if (GUILayout.Button("[F8] 天候切り替え"))          SetStatusMessage(OfflineDebugCommands.CycleWeather());
        if (GUILayout.Button("[F9] スタミナゼロ"))          SetStatusMessage(OfflineDebugCommands.DrainLocalStamina());

        GUILayout.Label("ショートカット: F1〜F9 / F10=開閉", _labelStyle);

        GUILayout.Space(6f);
        GUILayout.Label("== ジップライン（拠点⇄CP）開通 ==", _headerStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("CP1 開通")) SetStatusMessage(OfflineDebugCommands.OpenZipline(0));
        if (GUILayout.Button("CP2 開通")) SetStatusMessage(OfflineDebugCommands.OpenZipline(1));
        if (GUILayout.Button("CP3 開通")) SetStatusMessage(OfflineDebugCommands.OpenZipline(2));
        if (GUILayout.Button("CP4 開通")) SetStatusMessage(OfflineDebugCommands.OpenZipline(3));
        GUILayout.EndHorizontal();
        if (GUILayout.Button("全チェックポイント解放→全開通")) SetStatusMessage(OfflineDebugCommands.OpenAllZiplines());

        if (LocalCoopSettings.IsActive && !LocalCoopSettings.IsOnline)
        {
            GUILayout.Space(4f);
            GUILayout.Label("ローカル: P1=KB+Mouse / P2-4=Gamepad", _labelStyle);
            GUILayout.Label("後入り: GP Start / 後抜け: GP Back+Start長押し", _labelStyle);
            GUILayout.Label("P1後抜け: End / P1後入り: Enter", _labelStyle);
        }
    }

    private void DrawMessageSection()
    {
        if (string.IsNullOrEmpty(_statusMessage)) return;
        var prev = _labelStyle.normal.textColor;
        // 直近メッセージはアンバー強調、時間切れ後は副次トーンへフェード。
        _labelStyle.normal.textColor = _messageTimer > 0f ? UiPalette.Amber : UiPalette.CreamDim;
        GUILayout.Label($"› {_statusMessage}", _labelStyle);
        _labelStyle.normal.textColor = prev;
    }

    private void EnsureStyles(float uiScale)
    {
        if (_boxStyle == null)
        {
            var panelBg = UiPalette.Ink; panelBg.a = 0.92f; // 共有インク色の不透明パネル。
            _boxStyle = new GUIStyle(GUI.skin.box)
            {
                normal = { background = MakeTex(2, 2, panelBg) },
                padding = new RectOffset(10, 10, 10, 10),
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                wordWrap = true,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = UiPalette.Cream },
            };

            _headerStyle = new GUIStyle(_labelStyle)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = UiPalette.Amber },
            };
        }

        if (Mathf.Abs(_cachedUiScale - uiScale) < 0.01f) return;

        int fontSize = Mathf.RoundToInt(Mathf.Lerp(13f, 17f, Mathf.InverseLerp(0.9f, 1.35f, uiScale)));
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
}
