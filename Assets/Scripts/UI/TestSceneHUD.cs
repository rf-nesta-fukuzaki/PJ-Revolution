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
///   - Tab キー: インベントリパネルをトグル（Exploring 状態のときのみ）
///   - ゲーム終了時: 画面中央にリザルトオーバーレイ（R リトライ / Q 終了）
/// </summary>
public class TestSceneHUD : MonoBehaviour
{
    // ─────────────── 参照 ───────────────

    private SurvivalStats    _stats;
    private TorchSystem      _torch;
    private PlayerInteractor _interactor;
    private InventorySystem  _inventory;

    // ─────────────── テクスチャ / スタイル ───────────────

    private Texture2D _texWhite;
    private GUIStyle  _labelStyle;
    private GUIStyle  _promptStyle;
    private GUIStyle  _resultTitleStyle;
    private GUIStyle  _resultBodyStyle;
    private GUIStyle  _invTitleStyle;
    private GUIStyle  _slotLabelStyle;

    // ─────────────── バー定数 ───────────────

    private const int BarWidth   = 200;
    private const int BarHeight  = 20;
    private const int BarSpacing = 4;
    private const int Margin     = 10;
    private const int LabelWidth = 44;

    // ─────────────── インベントリ定数 ───────────────

    private const int InvWidth    = 640;
    private const int InvHeight   = 400;
    private const int SlotSize    = 60;
    private const int SlotCols    = 8;
    private const int MaxSlots    = 32;
    private const int SlotPadding = 4;

    // ─────────────── リザルト状態 ───────────────

    private GameState _gameState = GameState.Exploring;
    private float     _resultElapsedTime;
    private int       _resultGems;

    // ─────────────── インベントリ状態 ───────────────

    private bool _showInventory;
    private int  _selectedSlot;

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
        _inventory  = FindFirstObjectByType<InventorySystem>();

        _texWhite = new Texture2D(1, 1);
        _texWhite.SetPixel(0, 0, Color.white);
        _texWhite.Apply();

        if (GameManager.Instance != null)
            _gameState = GameManager.Instance.CurrentState;
    }

    private void Update()
    {
        // Tab: インベントリトグル（Exploring 中のみ）
        if (_gameState == GameState.Exploring && Input.GetKeyDown(KeyCode.Tab))
        {
            _showInventory = !_showInventory;
            Time.timeScale = _showInventory ? 0f : 1f;
        }

        // インベントリ表示中: 1〜8 キーでスロット使用
        if (_showInventory && _inventory != null)
        {
            for (int i = 0; i < 8; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    _selectedSlot = i;
                    UseItemAtSlot(i);
                    break;
                }
            }
        }

        // ゲーム終了時: R リトライ / Q 終了
        if (_gameState != GameState.Exploring)
        {
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
    }

    private void OnGUI()
    {
        // HUDCanvas が存在する場合は OnGUI HUD をスキップ
        if (FindFirstObjectByType<Canvas>() != null) return;

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

        if (_showInventory)
            DrawInventory();
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

    // ─────────────── インベントリ描画 ───────────────

    private void DrawInventory()
    {
        float cx    = (Screen.width  - InvWidth)  * 0.5f;
        float cy    = (Screen.height - InvHeight) * 0.5f;
        var   panel = new Rect(cx, cy, InvWidth, InvHeight);

        // 半透明黒背景
        DrawRect(panel, new Color(0f, 0f, 0f, 0.75f));

        // タイトル
        float cw  = _inventory != null ? _inventory.CurrentWeight : 0f;
        float mw  = _inventory != null ? _inventory.MaxWeight     : 0f;
        string title = $"インベントリ（重量: {cw:F1}/{mw:F1} kg）";
        GUI.Label(new Rect(cx + 10f, cy + 10f, InvWidth - 20f, 30f), title, _invTitleStyle);

        // スロット描画
        var slots = _inventory != null ? _inventory.Slots : null;
        int count = slots != null ? Mathf.Min(slots.Count, MaxSlots) : 0;

        for (int i = 0; i < MaxSlots; i++)
        {
            int col = i % SlotCols;
            int row = i / SlotCols;

            float sx = cx + SlotPadding + col * (SlotSize + SlotPadding);
            float sy = cy + 50f + SlotPadding + row * (SlotSize + SlotPadding);
            var   sr = new Rect(sx, sy, SlotSize, SlotSize);

            // スロット背景
            bool isSelected = (i == _selectedSlot);
            DrawRect(sr, new Color(0.2f, 0.2f, 0.2f, 0.85f));

            // 選択ハイライト（黄色枠）
            if (isSelected)
            {
                DrawBorder(sr, Color.yellow, 2);
            }

            if (i < count)
            {
                var slot = slots[i];

                // アイコン
                if (slot.Item.Icon != null)
                {
                    GUI.DrawTexture(
                        new Rect(sx + 2f, sy + 2f, SlotSize - 4f, SlotSize - 20f),
                        slot.Item.Icon.texture);
                }

                // アイテム名（短縮: 最大4文字）
                string shortName = slot.Item.ItemName.Length > 4
                    ? slot.Item.ItemName.Substring(0, 4)
                    : slot.Item.ItemName;

                // カウント
                string slotText = slot.Count > 1
                    ? $"{shortName}\n×{slot.Count}"
                    : shortName;

                GUI.Label(new Rect(sx + 2f, sy + SlotSize - 18f, SlotSize - 4f, 18f),
                          slotText, _slotLabelStyle);
            }
        }

        // 操作ガイド
        GUI.Label(
            new Rect(cx + 10f, cy + InvHeight - 30f, InvWidth - 20f, 24f),
            "[ 1〜8 ] 使用　　[ Tab ] 閉じる",
            _slotLabelStyle);
    }

    // ─────────────── アイテム使用処理 ───────────────

    private void UseItemAtSlot(int slotIndex)
    {
        if (_inventory == null) return;
        var slots = _inventory.Slots;
        if (slotIndex < 0 || slotIndex >= slots.Count) return;

        var slot = slots[slotIndex];
        if (!slot.Item.IsConsumable)
        {
            Debug.Log($"[TestSceneHUD] '{slot.Item.ItemName}' は使用できないアイテムです。");
            return;
        }

        if (!_inventory.TryRemoveItem(slot.Item))
        {
            Debug.Log($"[TestSceneHUD] '{slot.Item.ItemName}' の取り出しに失敗しました。");
            return;
        }

        switch (slot.Item.ConsumableEffect)
        {
            case ResourceItemType.Food:
                if (_stats != null) _stats.ApplyStatModification(StatType.Hunger, 30f);
                Debug.Log($"[TestSceneHUD] 食料使用: 空腹 +30");
                break;

            case ResourceItemType.OxygenTank:
                if (_stats != null) _stats.ApplyStatModification(StatType.Oxygen, 50f);
                Debug.Log($"[TestSceneHUD] 酸素タンク使用: 酸素 +50");
                break;

            case ResourceItemType.Medkit:
                if (_stats != null) _stats.ApplyStatModification(StatType.Health, 40f);
                Debug.Log($"[TestSceneHUD] 医療キット使用: HP +40");
                break;

            case ResourceItemType.FuelCanister:
                if (_torch != null) _torch.RefillFuel(30f);
                Debug.Log($"[TestSceneHUD] 燃料カニスター使用: 燃料 +30");
                break;

            default:
                Debug.Log($"[TestSceneHUD] '{slot.Item.ItemName}' は使用処理が未定義です。");
                break;
        }
    }

    // ─────────────── リザルトオーバーレイ描画 ───────────────

    private void DrawResultOverlay()
    {
        DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.6f));

        float cx = Screen.width  * 0.5f;
        float cy = Screen.height * 0.5f;

        bool isSuccess = _gameState == GameState.EscapeSuccess;
        string title   = isSuccess ? "脱出成功！" : "ゲームオーバー...";

        // デイリーチャレンジモードのときはタイトルに日付を付記する
        if (DailyChallenge.IsDailyMode)
            title = $"デイリーチャレンジ {DailyChallenge.GetDailyDateString()}";

        float blink = (Mathf.Sin(Time.unscaledTime * 3f) + 1f) * 0.5f;
        _resultTitleStyle.normal.textColor = isSuccess
            ? new Color(1f, 1f, 0.2f, 0.6f + blink * 0.4f)
            : new Color(1f, 0.3f, 0.3f, 0.6f + blink * 0.4f);

        GUI.Label(new Rect(cx - 200f, cy - 80f, 400f, 60f), title, _resultTitleStyle);

        int minutes = (int)(_resultElapsedTime / 60f);
        int seconds = (int)(_resultElapsedTime % 60f);
        string body = $"探索時間: {minutes:D2}:{seconds:D2}\n宝石: {_resultGems} 個";

        // デイリーチャレンジモードのときはベストスコアを追記する
        if (DailyChallenge.IsDailyMode)
        {
            var (bestTime, bestGems) = DailyChallenge.GetBestScore();
            int bm = (int)(bestTime / 60f);
            int bs = (int)(bestTime % 60f);
            body += $"\n\nベスト: {bm:D2}:{bs:D2} / {bestGems} 個";
        }

        GUI.Label(new Rect(cx - 200f, cy - 10f, 400f, 80f), body, _resultBodyStyle);

        GUI.Label(
            new Rect(cx - 200f, cy + 80f, 400f, 40f),
            "[ R ] リトライ　　[ Q ] 終了",
            _resultBodyStyle);
    }

    // ─────────────── バー描画ヘルパー ───────────────

    private void DrawBar(int index, int startY, string label, float ratio, Color color)
    {
        ratio = Mathf.Clamp01(ratio);

        int x = Margin;
        int y = startY + index * (BarHeight + BarSpacing);

        if (ratio <= 0.3f)
        {
            float blink = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            color = new Color(color.r, color.g, color.b, blink);
        }

        GUI.Label(new Rect(x, y, LabelWidth, BarHeight), label, _labelStyle);

        int barX = x + LabelWidth;

        DrawRect(new Rect(barX, y, BarWidth, BarHeight), new Color(0.2f, 0.2f, 0.2f, 0.8f));

        if (ratio > 0f)
            DrawRect(new Rect(barX, y, BarWidth * ratio, BarHeight), color);
    }

    private void DrawRect(Rect rect, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(rect, _texWhite);
        GUI.color = Color.white;
    }

    private void DrawBorder(Rect rect, Color color, int thickness)
    {
        DrawRect(new Rect(rect.x,                       rect.y,                        rect.width,  thickness), color);
        DrawRect(new Rect(rect.x,                       rect.y + rect.height - thickness, rect.width, thickness), color);
        DrawRect(new Rect(rect.x,                       rect.y,                        thickness,   rect.height), color);
        DrawRect(new Rect(rect.x + rect.width - thickness, rect.y,                    thickness,   rect.height), color);
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

        _invTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 15,
            fontStyle = FontStyle.Bold,
            normal    = { textColor = Color.white },
        };

        _slotLabelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize  = 11,
            alignment = TextAnchor.LowerCenter,
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
