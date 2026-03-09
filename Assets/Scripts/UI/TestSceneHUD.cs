using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// TestScene 用 OnGUI ベース軽量 HUD。
/// Canvas / EventSystem を一切使わない。
/// Player オブジェクトまたは空の GameObject にアタッチして使用する。
///
/// [表示内容]
///   - 左下: HP / 酸素 / 空腹 / 燃料 バー（低下時点滅）
///   - 中央下: インタラクトプロンプト
///   - ゲーム終了時: 画面中央にリザルトオーバーレイ（R リトライ / Q 終了）
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
    private GUIStyle  _resultTitleStyle;
    private GUIStyle  _resultBodyStyle;

    // ─────────────── バー定数 ───────────────

    private const int BarWidth   = 200;
    private const int BarHeight  = 20;
    private const int BarSpacing = 4;
    private const int Margin     = 10;
    private const int LabelWidth = 44;

    // ─────────────── リザルト状態 ───────────────

    private GameState _gameState = GameState.Exploring;
    private float     _resultElapsedTime;
    private int       _resultGems;

    // ─────────────── Unity Lifecycle ───────────────

    private void OnEnable()
    {
        GameManager.OnGameStateChanged += HandleGameStateChanged;
    }

    private void OnDisable()
    {
        GameManager.OnGameStateChanged -= HandleGameStateChanged;
    }

    private void Start()
    {
        _stats      = FindFirstObjectByType<SurvivalStats>();
        _torch      = FindFirstObjectByType<TorchSystem>();
        _interactor = FindFirstObjectByType<PlayerInteractor>();

        _texWhite = new Texture2D(1, 1);
        _texWhite.SetPixel(0, 0, Color.white);
        _texWhite.Apply();

        // 既にゲーム終了状態で開始している場合に対応（シーンリロード直後などは通常ない）
        if (GameManager.Instance != null)
            _gameState = GameManager.Instance.CurrentState;
    }

    private void Update()
    {
        // Time.timeScale = 0 でも Input.GetKeyDown は動作する
        if (_gameState == GameState.Exploring) return;

        if (Input.GetKeyDown(KeyCode.R))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        else if (Input.GetKeyDown(KeyCode.Q))
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }
    }

    private void OnGUI()
    {
        if (_labelStyle == null) InitStyles();

        if (_gameState == GameState.Exploring)
        {
            DrawHUD();
        }
        else
        {
            DrawHUD();
            DrawResultOverlay();
        }
    }

    // ─────────────── HUD 描画 ───────────────

    private void DrawHUD()
    {
        // ── バー描画（左下） ──
        int totalBarHeight = (BarHeight + BarSpacing) * 4 - BarSpacing;
        int startY         = Screen.height - Margin - totalBarHeight;

        DrawBar(0, startY, "HP",   _stats != null ? _stats.Health / 100f : 0f, new Color(0.8f, 0.1f, 0.1f));
        DrawBar(1, startY, "O2",   _stats != null ? _stats.Oxygen / 100f : 0f, new Color(0.1f, 0.4f, 0.9f));
        DrawBar(2, startY, "食料", _stats != null ? _stats.Hunger / 100f : 0f, new Color(0.9f, 0.5f, 0.1f));
        DrawBar(3, startY, "燃料", _torch != null ? _torch.FuelRatio      : 0f, new Color(0.9f, 0.9f, 0.1f));

        // ── インタラクトプロンプト（中央下） ──
        if (_gameState == GameState.Exploring)
        {
            string prompt = _interactor != null ? _interactor.CurrentPromptText : null;
            if (!string.IsNullOrEmpty(prompt))
            {
                GUI.Label(
                    new Rect(0f, Screen.height - 60f, Screen.width, 40f),
                    prompt,
                    _promptStyle);
            }
        }
    }

    // ─────────────── リザルトオーバーレイ描画 ───────────────

    private void DrawResultOverlay()
    {
        // 半透明背景
        DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));

        float cx = Screen.width  * 0.5f;
        float cy = Screen.height * 0.5f;

        // タイトル
        bool isSuccess = _gameState == GameState.EscapeSuccess;
        string title   = isSuccess ? "脱出成功！" : "ゲームオーバー...";

        // sin 点滅（Time.unscaledTime を使う: timeScale = 0 でも動く）
        float blink = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) * 0.5f;
        _resultTitleStyle.normal.textColor = isSuccess
            ? new Color(1f, 1f, 0.2f, 0.6f + blink * 0.4f)
            : new Color(1f, 0.3f, 0.3f, 0.6f + blink * 0.4f);

        GUI.Label(new Rect(cx - 200f, cy - 80f, 400f, 60f), title, _resultTitleStyle);

        // 探索時間
        int minutes = (int)(_resultElapsedTime / 60f);
        int seconds = (int)(_resultElapsedTime % 60f);
        string body = $"探索時間: {minutes:D2}:{seconds:D2}\n宝石: {_resultGems} 個";
        GUI.Label(new Rect(cx - 200f, cy - 10f, 400f, 60f), body, _resultBodyStyle);

        // リトライ / 終了プロンプト
        GUI.Label(
            new Rect(cx - 200f, cy + 60f, 400f, 40f),
            "[ R ] リトライ　　[ Q ] 終了",
            _resultBodyStyle);
    }

    // ─────────────── バー描画ヘルパー ───────────────

    private void DrawBar(int index, int startY, string label, float ratio, Color color)
    {
        ratio = Mathf.Clamp01(ratio);

        int x = Margin;
        int y = startY + index * (BarHeight + BarSpacing);

        // 30% 以下で点滅（Time.unscaledTime: timeScale=0 でも動作）
        if (ratio <= 0.3f)
        {
            float blink = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
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

    // ─────────────── スタイル初期化 ───────────────

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

        _resultTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 36,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.yellow },
        };

        _resultBodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal    = { textColor = Color.white },
        };
    }

    // ─────────────── イベントハンドラ ───────────────

    private void HandleGameStateChanged(GameState newState)
    {
        _gameState = newState;

        if (newState != GameState.Exploring && GameManager.Instance != null)
        {
            _resultElapsedTime = GameManager.Instance.ElapsedTime;
            _resultGems        = GameManager.Instance.CollectedGems;
        }
    }
}
