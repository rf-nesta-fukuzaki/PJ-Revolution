using System.Text;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// OfflineTestScene 用 IMGUI デバッグオーバーレイ。
/// </summary>
public sealed class OfflineDebugOverlay : MonoBehaviour
{
    [SerializeField] private float _overlayWidth = 280f;

    private bool _visible = true;
    private GUIStyle _boxStyle;
    private GUIStyle _labelStyle;
    private float _cachedUiScale = -1f;
    private string _statusMessage = "起動中...";
    private float _messageTimer;

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

    private void OnGUI()
    {
        if (!_visible) return;

        float uiScale = Mathf.Clamp(Screen.height / 1080f, 0.9f, 1.35f);
        float panelWidth = Mathf.Clamp(_overlayWidth * uiScale, 320f, 460f);
        float panelHeight = Mathf.Clamp(370f * uiScale, 360f, 560f);
        float panelPad = 12f * uiScale;

        EnsureStyles(uiScale);

        var expedition = GameServices.Expedition;
        var networkManager = NetworkManager.Singleton;
        var weather = GameServices.Weather;

        string netStat = networkManager != null && networkManager.IsHost ? "✓ Host (Offline)" : "✗ 未接続";
        string phase = expedition != null ? expedition.Phase.ToString() : "N/A";
        string weatherLabel = weather != null ? weather.CurrentWeather.ToString() : "N/A";
        string msgColor = _messageTimer > 0f ? "<color=#FFD700>" : "<color=#AAAAAA>";

        var sb = new StringBuilder();
        sb.AppendLine("<b>== Offline Test Scene ==</b>");
        sb.AppendLine($"ネット : {netStat}");
        sb.AppendLine($"フェーズ: {phase}");
        sb.AppendLine($"天候  : {weatherLabel}");
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
        sb.Append(_statusMessage);
        sb.Append("</color>");

        GUI.Box(new Rect(10f, 10f, panelWidth, panelHeight), GUIContent.none, _boxStyle);
        GUI.Label(
            new Rect(10f + panelPad, 10f + panelPad, panelWidth - panelPad * 2f, panelHeight - panelPad * 2f),
            sb.ToString(),
            _labelStyle);
    }

    private void Update()
    {
        if (_messageTimer > 0f)
            _messageTimer -= Time.deltaTime;
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
        }

        if (Mathf.Abs(_cachedUiScale - uiScale) < 0.01f) return;

        int fontSize = Mathf.RoundToInt(Mathf.Lerp(15f, 20f, Mathf.InverseLerp(0.9f, 1.35f, uiScale)));
        _labelStyle.fontSize = fontSize;
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
