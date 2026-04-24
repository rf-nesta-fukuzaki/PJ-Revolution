using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §2.1 / §9 — 遠征中の HUD。
/// タイマー・チェックポイント・スタミナ・ロープ状態・遺物リストを表示。
/// </summary>
public class ExpeditionHUD : MonoBehaviour
{
    public static ExpeditionHUD Instance { get; private set; }

    [Header("タイマー")]
    [SerializeField] private TextMeshProUGUI _timerLabel;

    [Header("チェックポイント")]
    [SerializeField] private TextMeshProUGUI _checkpointLabel;

    [Header("スタミナ")]
    [SerializeField] private Slider          _staminaBar;
    [SerializeField] private Image           _staminaFill;
    [SerializeField] private Color           _staminaFullColor   = Color.green;
    [SerializeField] private Color           _staminaLowColor    = Color.red;

    [Header("ロープ状態")]
    [SerializeField] private Image           _ropeIndicator;
    [SerializeField] private Color           _ropeConnectedColor = new Color(1f, 0.5f, 0f);
    [SerializeField] private Color           _ropeIdleColor      = Color.white;

    [Header("遺物リスト")]
    [SerializeField] private Transform       _relicListParent;
    [SerializeField] private GameObject      _relicListItemPrefab;

    [Header("警告")]
    [SerializeField] private TextMeshProUGUI _warningLabel;
    [SerializeField] private float           _warningDisplayTime = 3f;

    [Header("プレイヤー参照")]
    [SerializeField] private StaminaSystem   _localPlayerStamina;

    [Header("表示制御")]
    [SerializeField] private CanvasGroup     _hudCanvasGroup;

    // ── 内部状態 ─────────────────────────────────────────────
    private float                            _elapsedTime;
    private bool                             _timerRunning;
    private int                              _currentCheckpoint;
    private int                              _totalCheckpoints = 4;
    private float                            _warningTimer;

    private readonly List<RelicHudEntry>     _relicEntries = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (_hudCanvasGroup == null)
            _hudCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        ExpeditionEvents.OnExpeditionStarted   += StartTimer;
        ExpeditionEvents.OnExpeditionEnded     += StopTimer;
        ExpeditionEvents.OnCheckpointReached   += SetCheckpoint;
    }

    private void OnDisable()
    {
        ExpeditionEvents.OnExpeditionStarted   -= StartTimer;
        ExpeditionEvents.OnExpeditionEnded     -= StopTimer;
        ExpeditionEvents.OnCheckpointReached   -= SetCheckpoint;
    }

    private void Start()
    {
        // タイマー開始は ExpeditionEvents.OnExpeditionStarted イベント経由に変更。
        // テストシーン（ExpeditionManager なし）での互換性のためフォールバックを残す。
        if (GameServices.Expedition == null)
        {
            StartTimer();
        }
        else
        {
            StopTimer();
        }

        SetWarning("");
    }

    private void Update()
    {
        if (_timerRunning)
            _elapsedTime += Time.deltaTime;

        UpdateTimerUI();
        UpdateStaminaUI();
        UpdateRopeUI();
        UpdateWarning();
    }

    // ── タイマー ─────────────────────────────────────────────
    public void StartTimer()
    {
        _timerRunning = true;
        SetHudVisible(true);
    }

    public void StopTimer()
    {
        _timerRunning = false;
        SetHudVisible(false);
        SetWarning("");
    }

    private void UpdateTimerUI()
    {
        if (_timerLabel == null) return;
        int   min   = (int)_elapsedTime / 60;
        float sec   = _elapsedTime % 60f;
        _timerLabel.text = $"{min:00}:{sec:00.00}";
    }

    public float GetElapsedTime() => _elapsedTime;

    // ── チェックポイント ──────────────────────────────────────
    // シグネチャは ExpeditionEvents.OnCheckpointReached: Action<int, int> に合わせる
    public void SetCheckpoint(int current, int total)
    {
        _currentCheckpoint = current;
        _totalCheckpoints  = total;

        if (_checkpointLabel != null)
            _checkpointLabel.text = $"チェックポイント {current}/{total}";

        ShowWarning($"チェックポイント {current} 通過！");
    }

    // ── スタミナ ─────────────────────────────────────────────
    private void UpdateStaminaUI()
    {
        if (_staminaBar == null || _localPlayerStamina == null) return;

        float pct = _localPlayerStamina.StaminaPercent;
        _staminaBar.value = pct;

        if (_staminaFill != null)
            _staminaFill.color = Color.Lerp(_staminaLowColor, _staminaFullColor, pct);
    }

    // ── ロープ状態 ────────────────────────────────────────────
    private void UpdateRopeUI()
    {
        if (_ropeIndicator == null || GameServices.Ropes == null) return;

        bool connected = GameServices.Ropes.HasAnyRope;
        _ropeIndicator.color = connected ? _ropeConnectedColor : _ropeIdleColor;
    }

    // ── 遺物リスト ────────────────────────────────────────────
    public void RegisterRelic(RelicBase relic)
    {
        if (_relicListParent == null || _relicListItemPrefab == null) return;

        var go    = Instantiate(_relicListItemPrefab, _relicListParent);
        var entry = go.GetComponent<RelicHudEntry>();
        if (entry != null)
        {
            entry.Initialize(relic);
            _relicEntries.Add(entry);
        }
    }

    // ── 警告 ─────────────────────────────────────────────────
    public void ShowWarning(string message)
    {
        if (_warningLabel == null) return;
        _warningLabel.text    = message;
        _warningLabel.enabled = true;
        _warningTimer         = _warningDisplayTime;
    }

    private void SetWarning(string message)
    {
        if (_warningLabel == null) return;
        _warningLabel.text    = message;
        _warningLabel.enabled = !string.IsNullOrEmpty(message);
    }

    private void UpdateWarning()
    {
        if (_warningTimer <= 0f) return;
        _warningTimer -= Time.deltaTime;
        if (_warningTimer <= 0f)
            SetWarning("");
    }

    private void SetHudVisible(bool visible)
    {
        if (_hudCanvasGroup == null) return;
        _hudCanvasGroup.alpha = visible ? 1f : 0f;
        _hudCanvasGroup.interactable = visible;
        _hudCanvasGroup.blocksRaycasts = visible;
    }
}
