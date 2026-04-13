using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// HUD 全体を管理するシングルトン。
/// - タイマー / チェックポイント表示
/// - クロスヘア（照準インジケーター）
/// - クリア画面
/// - フェードアウト / インパネル
/// </summary>
public class HudManager : MonoBehaviour
{
    public static HudManager Instance { get; private set; }

    [Header("UI References (省略時は自動生成)")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI checkpointText;
    [SerializeField] private TextMeshProUGUI altitudeText;
    [SerializeField] private Image crosshairRing;
    [SerializeField] private Image crosshairDot;
    [SerializeField] private GameObject summitPanel;
    [SerializeField] private TextMeshProUGUI summitTimeText;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Crosshair Colors")]
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color grappableColor = Color.green;
    [SerializeField] private Color attachedColor = new Color(1f, 0.5f, 0f);

    private GrappleHook _grappleHook;
    private RopeSystem _ropeSystem;
    private Transform _playerTransform;
    private Canvas _canvas;
    private float _messageTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _grappleHook = player.GetComponentInChildren<GrappleHook>();
            _ropeSystem = player.GetComponentInChildren<RopeSystem>();
            _playerTransform = player.transform;
        }

        EnsureCanvas();
        CreateUIIfMissing();

        if (summitPanel != null) summitPanel.SetActive(false);
    }

    private void Update()
    {
        UpdateTimer();
        UpdateCheckpoint();
        UpdateAltitude();
        UpdateCrosshair();

        if (_messageTimer > 0f)
        {
            _messageTimer -= Time.deltaTime;
            if (_messageTimer <= 0f && messageText != null)
                messageText.gameObject.SetActive(false);
        }
    }

    // ─── 更新処理 ───

    private void UpdateTimer()
    {
        if (timerText == null || TimerDisplay.Instance == null) return;
        timerText.text = TimerDisplay.Instance.GetFormattedTime();
    }

    private void UpdateCheckpoint()
    {
        if (checkpointText == null || CheckpointSystem.Instance == null) return;
        int cur = CheckpointSystem.Instance.GetCurrentCheckpointIndex() + 1;
        int total = CheckpointSystem.Instance.GetTotalCheckpoints();
        checkpointText.text = cur > 0 ? $"Checkpoint {cur}/{total}" : "";
    }

    private void UpdateAltitude()
    {
        if (altitudeText == null || _playerTransform == null) return;
        altitudeText.text = $"Alt: {_playerTransform.position.y:0}m";
    }

    private void UpdateCrosshair()
    {
        if (crosshairRing == null) return;

        if (_ropeSystem != null && _ropeSystem.IsAttached)
        {
            crosshairRing.color = attachedColor;
            crosshairRing.gameObject.SetActive(true);
        }
        else if (_grappleHook != null && _grappleHook.IsAimingAtGrappable())
        {
            crosshairRing.color = grappableColor;
            crosshairRing.gameObject.SetActive(true);
        }
        else
        {
            crosshairRing.gameObject.SetActive(false);
        }
    }

    // ─── 公開 API ───

    public void ShowCheckpointMessage(string msg)
    {
        if (messageText == null) return;
        messageText.text = msg;
        messageText.gameObject.SetActive(true);
        _messageTimer = 3f;
    }

    public void ShowSummitReached(float elapsed)
    {
        if (summitPanel != null) summitPanel.SetActive(true);
        if (summitTimeText != null)
        {
            int min = Mathf.FloorToInt(elapsed / 60f);
            float sec = elapsed % 60f;
            float best = PlayerPrefs.GetFloat("BestTime", float.MaxValue);
            string bestStr = best < float.MaxValue
                ? $"Best: {Mathf.FloorToInt(best / 60f):00}:{best % 60f:00.00}"
                : "";
            summitTimeText.text = $"SUMMIT REACHED!\n{min:00}:{sec:00.00}\n{bestStr}";
        }
    }

    public void FadeOut() => IrisTransition.Instance.IrisOut();
    public void FadeIn()  => IrisTransition.Instance.IrisIn();

    // ─── 自動生成 ───

    private void EnsureCanvas()
    {
        _canvas = GetComponentInChildren<Canvas>();
        if (_canvas != null) return;

        var canvasGo = new GameObject("HUD_Canvas");
        canvasGo.transform.SetParent(transform);
        _canvas = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.AddComponent<CanvasScaler>();
        canvasGo.AddComponent<GraphicRaycaster>();
    }

    private void CreateUIIfMissing()
    {
        Transform parent = _canvas.transform;

        if (timerText == null)
            timerText = CreateText(parent, "TimerText", "00:00.00", 24,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f));

        if (checkpointText == null)
            checkpointText = CreateText(parent, "CheckpointText", "", 20,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -50f));

        if (altitudeText == null)
            altitudeText = CreateText(parent, "AltitudeText", "Alt: 0m", 18,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f));

        if (messageText == null)
        {
            messageText = CreateText(parent, "MessageText", "", 30,
                new Vector2(0.5f, 0.7f), new Vector2(0.5f, 0.7f), Vector2.zero);
            messageText.gameObject.SetActive(false);
        }

        // クロスヘア
        if (crosshairDot == null) crosshairDot = CreateCrosshairDot(parent);
        if (crosshairRing == null) crosshairRing = CreateCrosshairRing(parent);

        // クリアパネル
        if (summitPanel == null) CreateSummitPanel(parent);
    }

    private TextMeshProUGUI CreateText(Transform parent, string name, string defaultText,
        int fontSize, Vector2 anchor, Vector2 pivot, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = new Vector2(300f, 40f);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        return tmp;
    }

    private Image CreateCrosshairDot(Transform parent)
    {
        var go = new GameObject("Crosshair_Dot");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(6f, 6f);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = Color.white;
        return img;
    }

    private Image CreateCrosshairRing(Transform parent)
    {
        var go = new GameObject("Crosshair_Ring");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(30f, 30f);
        rt.anchoredPosition = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = grappableColor;
        // 中空の輪はテクスチャで表現するか、スプライト設定で対応
        // ここでは簡易的に半透明の輪として表示
        img.color = new Color(grappableColor.r, grappableColor.g, grappableColor.b, 0.6f);
        go.SetActive(false);
        return img;
    }

    private void CreateSummitPanel(Transform parent)
    {
        var panel = new GameObject("SummitPanel");
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(500f, 300f);
        rt.anchoredPosition = Vector2.zero;

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.7f);

        summitPanel = panel;

        // テキスト
        var textGo = new GameObject("SummitText");
        textGo.transform.SetParent(panel.transform, false);
        var trt = textGo.AddComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.sizeDelta = Vector2.zero;
        summitTimeText = textGo.AddComponent<TextMeshProUGUI>();
        summitTimeText.text = "SUMMIT REACHED!";
        summitTimeText.fontSize = 36;
        summitTimeText.color = Color.yellow;
        summitTimeText.alignment = TextAlignmentOptions.Center;

        // リトライボタン
        var btnGo = new GameObject("RetryButton");
        btnGo.transform.SetParent(panel.transform, false);
        var brt = btnGo.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0.5f, 0f);
        brt.anchorMax = new Vector2(0.5f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.sizeDelta = new Vector2(200f, 50f);
        brt.anchoredPosition = new Vector2(0f, 20f);
        btnGo.AddComponent<Image>().color = new Color(0.2f, 0.6f, 0.2f);
        var btn = btnGo.AddComponent<Button>();
        btn.onClick.AddListener(() => IrisTransition.Instance.ReloadScene());

        var btnText = new GameObject("Text");
        btnText.transform.SetParent(btnGo.transform, false);
        var btnTrt = btnText.AddComponent<RectTransform>();
        btnTrt.anchorMin = Vector2.zero;
        btnTrt.anchorMax = Vector2.one;
        btnTrt.sizeDelta = Vector2.zero;
        var tmp = btnText.AddComponent<TextMeshProUGUI>();
        tmp.text = "RETRY";
        tmp.fontSize = 24;
        tmp.color = Color.white;
        tmp.alignment = TextAlignmentOptions.Center;

        panel.SetActive(false);
    }
}
