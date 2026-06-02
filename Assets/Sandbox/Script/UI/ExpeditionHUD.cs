using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandbox.UI;

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

    [Header("スタミナリング（中央集約ミニマル / PEAK寄り）")]
    [Tooltip("true: スタミナを照準周りの放射リングに集約し、左上バーは隠す")]
    [SerializeField] private bool  _useCenterStaminaRing = true;
    [SerializeField] private Image _staminaRing;
    // 暖色・低彩度のスタイライズドパレット（PEAK のトーンに寄せる）
    [SerializeField] private Color _ringFullColor = new Color(0.56f, 0.82f, 0.46f, 0.92f);
    [SerializeField] private Color _ringLowColor  = new Color(0.94f, 0.44f, 0.30f, 0.98f);
    private static Sprite s_ringSprite;

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
    private readonly ExpeditionTimer _fallbackTimer = new();
    private readonly ExpeditionHudReadModel _hudReadModel = new();
    private ExpeditionTimer _subscribedTimer;
    private string                       _displayTime = "00:00.00";
    private int                              _currentCheckpoint;
    private int                              _totalCheckpoints = 4;
    private float                            _warningTimer;

    private readonly List<RelicHudEntry>     _relicEntries = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        EnsureFullScreenRoot();
        EnsureCenterStaminaRing();

        if (_hudCanvasGroup == null)
            _hudCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
    }

    /// <summary>
    /// スタミナを「照準周りの放射リング」に集約する（中央集約ミニマル）。
    /// 中央の3D空間を遮らない細いリングで、減少時は暖色レッドへ。
    /// 既存の左上スタミナバーは隠して情報を中央へ一本化する。シーンアセットは書き換えない（非破壊）。
    /// </summary>
    private void EnsureCenterStaminaRing()
    {
        if (!_useCenterStaminaRing) return;

        if (_staminaBar != null) _staminaBar.gameObject.SetActive(false);

        // 中央集約ミニマル: 常時表示の CP ラベルは隠す（通過時は警告で一瞬だけ出る）。
        if (_checkpointLabel != null) _checkpointLabel.gameObject.SetActive(false);

        if (_staminaRing != null) return;

        // 暗いトラック（常時フル円）で、緑地形の上でもコントラストを確保しつつ
        // 「残量の空き」も示す。色付きフィルはこの上に放射状で重ねる。
        CreateRingImage("StaminaRingTrack", UiPalette.Track, false);
        _staminaRing = CreateRingImage("StaminaRing", _ringFullColor, true);
    }

    private Image CreateRingImage(string goName, Color color, bool radial)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(116f, 116f);
        rt.anchoredPosition = Vector2.zero;

        var img = go.AddComponent<Image>();
        img.sprite = GetRingSprite();
        img.preserveAspect = true;
        img.raycastTarget = false;
        img.color = color;

        if (radial)
        {
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillOrigin = (int)Image.Origin360.Top;
            img.fillClockwise = true;
        }
        return img;
    }

    /// <summary>ソフトな環状（ドーナツ）スプライトを手続き生成する（アンチエイリアス付き）。</summary>
    private static Sprite GetRingSprite()
    {
        if (s_ringSprite != null) return s_ringSprite;

        const int size = 128;
        const float thickness = 13f;
        const float aa = 1.5f;

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (size - 1) * 0.5f;
        float rOuter = c - 2f;
        float rInner = rOuter - thickness;

        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Min(Mathf.Clamp01((rOuter - d) / aa), Mathf.Clamp01((d - rInner) / aa));
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }
        tex.SetPixels(px);
        tex.Apply();

        s_ringSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        return s_ringSprite;
    }

    /// <summary>
    /// ルートが素の Transform だと、子の四隅アンカー（タイマー=左上 / 遺物=右上 / 警告=上中央）が
    /// 0×0 の原点矩形（= Canvas 中央）基準で解決され、HUD 全体が画面中央（照準域）へ寄ってしまう。
    /// ルートを全画面ストレッチの RectTransform にして、子要素を本来の四隅へ正しく配置する。
    /// シーンアセットは書き換えず実行時に補正する（非破壊）。
    /// </summary>
    private void EnsureFullScreenRoot()
    {
        var rt = transform as RectTransform;
        if (rt == null) rt = gameObject.AddComponent<RectTransform>();

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.localScale = Vector3.one;
        rt.localPosition = Vector3.zero;
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
        UnsubscribeTimer();
    }

    private ExpeditionTimer ResolveTimer()
    {
        return GameServices.Expedition != null ? GameServices.Expedition.Timer : _fallbackTimer;
    }

    private void SubscribeTimer(ExpeditionTimer timer)
    {
        if (_subscribedTimer == timer) return;
        UnsubscribeTimer();
        _subscribedTimer = timer;
        _subscribedTimer.OnFormattedTimeChanged += HandleTimerFormatted;
    }

    private void UnsubscribeTimer()
    {
        if (_subscribedTimer == null) return;
        _subscribedTimer.OnFormattedTimeChanged -= HandleTimerFormatted;
        _subscribedTimer = null;
    }

    private void HandleTimerFormatted(string formatted)
    {
        _displayTime = formatted;
        UpdateTimerUI();
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
        StyleTimerLabel();
    }

    /// <summary>
    /// タイマーをパレットのクリーム色＋ソフトシャドウで仕上げる。
    /// フォント統一(UiFontUnifier, 各 Awake)後に確実に適用したいので Start で行う
    /// （font 差し替えはマテリアルを戻すため Awake では underlay が消えうる）。
    /// </summary>
    private void StyleTimerLabel()
    {
        if (_timerLabel == null) return;
        _timerLabel.color = UiPalette.Cream;
        UiReadability.MakeReadable(_timerLabel);
    }

    private void Update()
    {
        var timer = ResolveTimer();
        if (ReferenceEquals(timer, _fallbackTimer) && timer.IsRunning)
            timer.Tick(Time.deltaTime);

        ResolveLocalStaminaIfNeeded();
        UpdateStaminaUI();
        UpdateRopeUI();
        UpdateWarning();
    }

    /// <summary>
    /// _localPlayerStamina は Inspector 未設定（combined ではプレイヤーが実行時生成）。
    /// HUD の他要素と同じく Player タグから StaminaSystem を一度だけ解決し、
    /// スタミナリングが実際の残量を反映するようにする。
    /// </summary>
    private void ResolveLocalStaminaIfNeeded()
    {
        if (_localPlayerStamina != null) return;

        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
            _localPlayerStamina = player.GetComponentInChildren<StaminaSystem>();
    }

    // ── タイマー ─────────────────────────────────────────────
    public void StartTimer()
    {
        var timer = ResolveTimer();
        SubscribeTimer(timer);
        timer.Start();
        SetHudVisible(true);
        UpdateTimerUI();
    }

    public void StopTimer()
    {
        ResolveTimer().Stop();
        SetHudVisible(false);
        SetWarning("");
    }

    private void UpdateTimerUI()
    {
        if (_timerLabel == null) return;
        _timerLabel.text = _displayTime;
    }

    public float GetElapsedTime() => ResolveTimer().ElapsedSeconds;

    // ── チェックポイント ──────────────────────────────────────
    // シグネチャは ExpeditionEvents.OnCheckpointReached: Action<int, int> に合わせる
    public void SetCheckpoint(int current, int total)
    {
        _currentCheckpoint = current;
        _totalCheckpoints  = total;

        if (_checkpointLabel != null)
            _checkpointLabel.text = $"チェックポイント {current}/{total}";

        _hudReadModel.SetTransientMessage($"チェックポイント {current} 通過！", _warningDisplayTime);
        ShowWarning($"チェックポイント {current} 通過！");
    }

    // ── スタミナ ─────────────────────────────────────────────
    private void UpdateStaminaUI()
    {
        float pct = _localPlayerStamina != null ? _localPlayerStamina.StaminaPercent : 1f;

        if (_staminaBar != null) _staminaBar.value = pct;
        if (_staminaFill != null)
            _staminaFill.color = Color.Lerp(_staminaLowColor, _staminaFullColor, pct);

        if (_staminaRing != null)
        {
            _staminaRing.fillAmount = pct;
            _staminaRing.color = Color.Lerp(_ringLowColor, _ringFullColor, pct);
        }
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
