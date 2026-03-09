using UnityEngine;

/// <summary>
/// TestScene 用 OnGUI ベース軽量 HUD。
/// Canvas / EventSystem を一切使わない。
/// Player オブジェクトまたは空の GameObject にアタッチして使用する。
///
/// [表示内容]
///   - 左下: HP / 酸素 / 空腹 / 燃料 バー（低下時点滅）
///   - 中央下: インタラクトプロンプト
/// </summary>
public class TestSceneHUD : MonoBehaviour
{
    // ─────────────── 参照 ───────────────

    private SurvivalStats    _stats;
    private TorchSystem      _torch;
    private PlayerInteractor _interactor;

    // ─────────────── テクスチャ / スタイル ───────────────

    private Texture2D _texWhite;
    private GUIStyle  _labelStyle;
    private GUIStyle  _promptStyle;

    // ─────────────── バー定数 ───────────────

    private const int BarWidth   = 200;
    private const int BarHeight  = 20;
    private const int BarSpacing = 4;
    private const int Margin     = 10;
    private const int LabelWidth = 44;

    // ─────────────── Unity Lifecycle ───────────────

    private void Start()
    {
        _stats      = FindFirstObjectByType<SurvivalStats>();
        _torch      = FindFirstObjectByType<TorchSystem>();
        _interactor = FindFirstObjectByType<PlayerInteractor>();

        _texWhite = new Texture2D(1, 1);
        _texWhite.SetPixel(0, 0, Color.white);
        _texWhite.Apply();
    }

    private void OnGUI()
    {
        if (_labelStyle == null) InitStyles();

        // ── バー描画（左下） ──
        int totalBarHeight = (BarHeight + BarSpacing) * 4 - BarSpacing;
        int startY         = Screen.height - Margin - totalBarHeight;

        DrawBar(0, startY, "HP",   _stats != null ? _stats.Health / 100f : 0f, new Color(0.8f, 0.1f, 0.1f));
        DrawBar(1, startY, "O2",   _stats != null ? _stats.Oxygen / 100f : 0f, new Color(0.1f, 0.4f, 0.9f));
        DrawBar(2, startY, "食料", _stats != null ? _stats.Hunger / 100f : 0f, new Color(0.9f, 0.5f, 0.1f));
        DrawBar(3, startY, "燃料", _torch != null ? _torch.FuelRatio      : 0f, new Color(0.9f, 0.9f, 0.1f));

        // ── インタラクトプロンプト（中央下） ──
        string prompt = _interactor != null ? _interactor.CurrentPromptText : null;
        if (!string.IsNullOrEmpty(prompt))
        {
            GUI.Label(
                new Rect(0f, Screen.height - 60f, Screen.width, 40f),
                prompt,
                _promptStyle);
        }
    }

    // ─────────────── 内部処理 ───────────────

    private void DrawBar(int index, int startY, string label, float ratio, Color color)
    {
        ratio = Mathf.Clamp01(ratio);

        int x = Margin;
        int y = startY + index * (BarHeight + BarSpacing);

        // 30% 以下で点滅
        if (ratio <= 0.3f)
        {
            float blink = Mathf.Abs(Mathf.Sin(Time.time * 4f));
            color = new Color(color.r, color.g, color.b, blink);
        }

        // ラベル
        GUI.Label(new Rect(x, y, LabelWidth, BarHeight), label, _labelStyle);

        int barX = x + LabelWidth;

        // 背景（暗灰色）
        DrawRect(new Rect(barX, y, BarWidth, BarHeight), new Color(0.2f, 0.2f, 0.2f, 0.8f));

        // 前景
        if (ratio > 0f)
            DrawRect(new Rect(barX, y, BarWidth * ratio, BarHeight), color);
    }

    private void DrawRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, _texWhite);
        GUI.color = Color.white;
    }

    private void InitStyles()
    {
        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 13,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white },
        };

        _promptStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white },
        };
    }
}
