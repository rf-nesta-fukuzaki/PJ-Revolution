using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandbox.World;

namespace Sandbox.UI
{
/// <summary>
/// サンドボックス遠征 HUD のプレゼンテーション層。
/// ドメインロジック (ExpeditionHudReadModel) とデータソース (TimerDisplay / CheckpointSystem) を
/// イベント購読で接続し、Update ポーリングを最小化する。
/// </summary>
public class HudManager : MonoBehaviour
{
    private static HudManager _instance;

    [System.Obsolete("FindFirstObjectByType<HudManager>() または GameServices 経由の HUD イベントを使用してください")]
    public static HudManager Instance => _instance;

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

    private readonly ExpeditionHudReadModel _readModel = new();
    private Transform _playerTransform;
    private Canvas _canvas;

    private string _formattedTime = "00:00.00";
    private int _checkpointIndex = -1;
    private int _totalCheckpoints;
    private float _lastPublishedAltitude = float.MinValue;
    private bool _forcePublish;

    private const float AltitudePublishStepMeters = 1f;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        _readModel.OnSnapshotChanged += ApplySnapshot;
    }

    private void OnDestroy()
    {
        _readModel.OnSnapshotChanged -= ApplySnapshot;
        if (_instance == this) _instance = null;
    }

    private void Start()
    {
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _playerTransform = player.transform;

        EnsureCanvas();
        CreateUIIfMissing();

        if (summitPanel != null) summitPanel.SetActive(false);

        var timer = GameServices.Timer;
        if (timer != null)
            _formattedTime = timer.GetFormattedTime();

        PublishCurrentSnapshot();
    }

    private void OnEnable()
    {
        ExpeditionEvents.OnSummitReached += HandleSummitReached;

        var timer = GameServices.Timer;
        if (timer != null)
            timer.FormattedTimeChanged += HandleTimerChanged;

        var checkpoints = GameServices.Checkpoints;
        if (checkpoints != null)
        {
            checkpoints.CheckpointReached += HandleCheckpointMessage;
            checkpoints.CheckpointProgressChanged += HandleCheckpointProgress;
            checkpoints.RespawnStarted += HandleRespawnStarted;
            checkpoints.RespawnCompleted += HandleRespawnCompleted;
        }
    }

    private void OnDisable()
    {
        ExpeditionEvents.OnSummitReached -= HandleSummitReached;

        var timer = GameServices.Timer;
        if (timer != null)
            timer.FormattedTimeChanged -= HandleTimerChanged;

        var checkpoints = GameServices.Checkpoints;
        if (checkpoints != null)
        {
            checkpoints.CheckpointReached -= HandleCheckpointMessage;
            checkpoints.CheckpointProgressChanged -= HandleCheckpointProgress;
            checkpoints.RespawnStarted -= HandleRespawnStarted;
            checkpoints.RespawnCompleted -= HandleRespawnCompleted;
        }
    }

    private void Update()
    {
        if (_readModel.TickTransientMessage(Time.deltaTime))
            _forcePublish = true;

        if (_playerTransform == null)
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) _playerTransform = player.transform;
        }

        if (_playerTransform != null)
            PublishCurrentSnapshot();

        UpdateCrosshair();
    }

    private void HandleTimerChanged(string formattedTime)
    {
        _formattedTime = formattedTime;
        _forcePublish = true;
        PublishCurrentSnapshot();
    }

    private void HandleCheckpointProgress(int currentIndex, int total)
    {
        _checkpointIndex = currentIndex;
        _totalCheckpoints = total;
        _forcePublish = true;
        PublishCurrentSnapshot();
    }

    private void HandleCheckpointMessage(int index, string message)
    {
        ShowCheckpointMessage(message);
    }

    private void HandleSummitReached(float elapsed)
    {
        ShowSummitReached(elapsed);
    }

    private void HandleRespawnStarted()
    {
        FadeOut();
    }

    private void HandleRespawnCompleted()
    {
        FadeIn();
    }

    private void PublishCurrentSnapshot()
    {
        float altitude = _playerTransform != null ? _playerTransform.position.y : 0f;
        float roundedAltitude = Mathf.Floor(altitude / AltitudePublishStepMeters) * AltitudePublishStepMeters;

        if (!_forcePublish && Mathf.Approximately(roundedAltitude, _lastPublishedAltitude))
            return;

        _forcePublish = false;
        _lastPublishedAltitude = roundedAltitude;
        _readModel.Publish(_formattedTime, _checkpointIndex, _totalCheckpoints, roundedAltitude);
    }

    private void ApplySnapshot(ExpeditionHudSnapshot snapshot)
    {
        if (timerText != null)
            timerText.text = snapshot.FormattedTime;

        if (checkpointText != null)
            checkpointText.text = snapshot.CheckpointLabel;

        if (altitudeText != null)
            altitudeText.text = snapshot.AltitudeLabel;

        if (messageText == null) return;

        bool showMessage = !string.IsNullOrEmpty(snapshot.TransientMessage)
                           && snapshot.TransientMessageRemainingSeconds > 0f;
        messageText.gameObject.SetActive(showMessage);
        if (showMessage)
            messageText.text = snapshot.TransientMessage;
    }

    private void UpdateCrosshair()
    {
        // L Rope/Grapple 退役後はリング非表示（Step 3.2 以降で P 側ロープと再統合予定）。
        if (crosshairRing != null) crosshairRing.gameObject.SetActive(false);
    }

    // ─── 公開 API ───

    public void ShowCheckpointMessage(string msg)
    {
        _readModel.SetTransientMessage(msg, 3f);
        _forcePublish = true;
        PublishCurrentSnapshot();
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

    public void FadeOut() => GameServices.SceneFade?.IrisOut();
    public void FadeIn()  => GameServices.SceneFade?.IrisIn();

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

        if (crosshairDot == null) crosshairDot = CreateCrosshairDot(parent);
        if (crosshairRing == null) crosshairRing = CreateCrosshairRing(parent);

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
        btn.onClick.AddListener(() => GameServices.SceneFade?.ReloadScene());

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
}
