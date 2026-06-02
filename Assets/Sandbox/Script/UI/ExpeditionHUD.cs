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
    // 暖色・低彩度のスタイライズドパレット（PEAK のトーンに寄せる）。
    // 山の緑に埋もれないよう、満タン色はアンバー＋不透明に寄せ、縁取りで分離する。
    [SerializeField] private Color _ringFullColor = new Color(0.98f, 0.78f, 0.36f, 1f);
    [SerializeField] private Color _ringLowColor  = new Color(0.97f, 0.40f, 0.28f, 1f);
    // リングの縁取り（どんな背景でもコントラストを確保する暗色アウトライン）。
    [SerializeField] private Color _ringOutlineColor = new Color(0f, 0f, 0f, 0.85f);
    private static Sprite s_ringSprite;

    [Header("ロープ状態")]
    [SerializeField] private Image           _ropeIndicator;
    [SerializeField] private TextMeshProUGUI _ropeLabel;
    [SerializeField] private Color           _ropeConnectedColor = new Color(1f, 0.55f, 0.18f, 1f);
    [SerializeField] private Color           _ropeIdleColor      = new Color(0.55f, 0.57f, 0.62f, 0.7f);

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
        EnsureTimerLabel();
        EnsureRopeIndicator();

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

        // 濃い不透明トラック（常時フル円）で、緑地形の上でもコントラストを確保しつつ
        // 「残量の空き」も示す。色付きフィルはこの上に放射状で重ねる。
        // 両リングに暗色アウトラインを付け、山の緑と同化しないようにする。
        var trackColor = new Color(0.05f, 0.06f, 0.08f, 0.88f);
        CreateRingImage("StaminaRingTrack", trackColor, false, addOutline: true);
        _staminaRing = CreateRingImage("StaminaRing", _ringFullColor, true, addOutline: true);
    }

    private Image CreateRingImage(string goName, Color color, bool radial, bool addOutline = false)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(120f, 120f);
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

        if (addOutline)
        {
            // UI Outline は 4 方向にダーク複製を描き、放射フィルの先端エッジも縁取る。
            var outline = go.AddComponent<Outline>();
            outline.effectColor = _ringOutlineColor;
            outline.effectDistance = new Vector2(2f, 2f);
            outline.useGraphicAlpha = false;
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

    /// <summary>
    /// 左上タイマーを自己修復する。シーンの参照配線が壊れている/未設定だと
    /// UpdateTimerUI が何も書き込めず「00:00.00 のまま動かない」ように見えるため、
    /// _timerLabel が無ければ実行時に生成し、いずれにせよ左上へ整形配置する（非破壊）。
    /// </summary>
    private void EnsureTimerLabel()
    {
        if (_timerLabel == null)
        {
            var existing = transform.Find("TimerLabel");
            if (existing != null) _timerLabel = existing.GetComponent<TextMeshProUGUI>();
        }
        if (_timerLabel == null)
        {
            var go = new GameObject("TimerLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            _timerLabel = go.GetComponent<TextMeshProUGUI>();
            _timerLabel.text = "00:00.00";
        }

        var rt = _timerLabel.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(260f, 54f);
        rt.anchoredPosition = new Vector2(28f, -22f);

        _timerLabel.fontSize  = 34f;
        _timerLabel.fontStyle = FontStyles.Bold;
        _timerLabel.alignment = TextAlignmentOptions.Left;
        _timerLabel.color = UiPalette.Cream;
        _timerLabel.raycastTarget = false;
        _timerLabel.gameObject.SetActive(true);
    }

    /// <summary>
    /// ロープ状態インジケーターを自己修復する。OfflineSceneCreator はスプライト未設定・
    /// 白色の Image を置くため「白い四角」に見える。リングスプライトを割り当て、
    /// idle/接続で配色を変える小アイコン＋ラベルへ整える（非破壊）。
    /// </summary>
    private void EnsureRopeIndicator()
    {
        if (_ropeIndicator == null)
        {
            var existing = transform.Find("RopeIndicator");
            if (existing != null) _ropeIndicator = existing.GetComponent<Image>();
        }
        if (_ropeIndicator == null)
        {
            var go = new GameObject("RopeIndicator", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            _ropeIndicator = go.GetComponent<Image>();
        }

        _ropeIndicator.sprite = GetRingSprite();
        _ropeIndicator.type = Image.Type.Simple;
        _ropeIndicator.preserveAspect = true;
        _ropeIndicator.raycastTarget = false;
        _ropeIndicator.color = _ropeIdleColor;

        var rt = _ropeIndicator.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(26f, 26f);
        rt.anchoredPosition = new Vector2(30f, -84f);

        if (_ropeLabel == null)
        {
            var existing = _ropeIndicator.transform.Find("RopeLabel");
            if (existing != null)
            {
                _ropeLabel = existing.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                var lgo = new GameObject("RopeLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                lgo.transform.SetParent(_ropeIndicator.transform, false);
                _ropeLabel = lgo.GetComponent<TextMeshProUGUI>();
                _ropeLabel.text = "ロープ";
            }
        }

        if (_ropeLabel != null)
        {
            var lrt = _ropeLabel.rectTransform;
            lrt.anchorMin = lrt.anchorMax = new Vector2(0f, 0.5f);
            lrt.pivot = new Vector2(0f, 0.5f);
            lrt.sizeDelta = new Vector2(120f, 26f);
            lrt.anchoredPosition = new Vector2(34f, 0f);
            _ropeLabel.fontSize  = 16f;
            _ropeLabel.alignment = TextAlignmentOptions.Left;
            _ropeLabel.color = UiPalette.CreamDim;
            _ropeLabel.raycastTarget = false;
            UiReadability.MakeReadable(_ropeLabel);
        }
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

        ShowWarning($"チェックポイント {current} 通過！");
    }

    // ── スタミナ ─────────────────────────────────────────────
    // 毎フレームの無駄な Canvas 更新（value/color/fillAmount の dirty 化 → グラフィック
    // リビルド）を避けるため、前回値からの変化があったときだけ反映する。
    private float _lastStaminaPct = -1f;

    private void UpdateStaminaUI()
    {
        float pct = _localPlayerStamina != null ? _localPlayerStamina.StaminaPercent : 1f;
        if (Mathf.Abs(pct - _lastStaminaPct) < 0.0015f) return;
        _lastStaminaPct = pct;

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
    private int _lastRopeConnected = -1; // -1=未初期化, 0=未接続, 1=接続

    private void UpdateRopeUI()
    {
        if (_ropeIndicator == null) return;

        bool connected = GameServices.Ropes != null && GameServices.Ropes.HasAnyRope;
        int connectedFlag = connected ? 1 : 0;
        if (connectedFlag == _lastRopeConnected) return;
        _lastRopeConnected = connectedFlag;

        _ropeIndicator.color = connected ? _ropeConnectedColor : _ropeIdleColor;
        if (_ropeLabel != null)
            _ropeLabel.color = connected ? _ropeConnectedColor : UiPalette.CreamDim;
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
