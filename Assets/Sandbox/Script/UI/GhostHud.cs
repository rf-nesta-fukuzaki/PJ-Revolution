using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Sandbox.UI;

/// <summary>
/// GDD §14.4 — 死亡・幽霊 UI（手続き生成・非破壊・自己完結）。
///
/// 表示要素:
///   - 死亡フラッシュ（赤の全画面ワイプ + 「あなたは死んだ」）。生存→幽霊の遷移時に一度。
///   - 幽霊オーバーレイ（薄い青味フィルタ + 「幽霊モード ― 祠で復活しよう」）。
///   - 範囲制限警告（GhostSystem.IsNearLeashBoundary が true の間、赤点滅）。
///   - 復活チャネリングバー（GhostSystem.IsChannelingRevive 中、ReviveChannelProgress01 を表示）。
///   - ピン操作プロンプト（[F] ピンを打つ）。
///
/// 既存 HUD（ExpeditionHUD, sortingOrder 100）の少し下（90）に独立 Canvas を張り、
/// 幽霊でない間は全要素を非表示にする。ItemGameplayBootstrap から EnsureExists() で生成。
/// </summary>
public sealed class GhostHud : MonoBehaviour
{
    public static GhostHud Instance { get; private set; }

    private const int   SORTING_ORDER       = 90;   // HUD(100) の少し下。死亡フラッシュ/青味で HUD を潰さない
    private const float DEATH_FLASH_SECONDS = 1.4f;

    private CanvasGroup     _ghostGroup;     // 幽霊中のみ表示する要素群
    private Image           _blueTint;
    private TextMeshProUGUI _ghostLabel;
    private TextMeshProUGUI _pinPrompt;
    private TextMeshProUGUI _leashWarning;
    private GameObject      _channelPanel;
    private Image           _channelFill;
    private RectTransform   _channelFillRT;
    private Image           _deathFlash;
    private TextMeshProUGUI _deathText;

    private GhostSystem _localGhost;
    private bool        _wasGhost;
    private float       _deathFlashTimer;
    private TMP_FontAsset _font;

    public static void EnsureExists()
    {
        if (Instance != null) return;
        new GameObject(nameof(GhostHud)).AddComponent<GhostHud>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _font = UiFontUnifier.ResolveProjectFont();
        BuildUi();
        SetGhostVisible(false);
        SetDeathFlash(0f);
    }

    // ── 構築 ─────────────────────────────────────────────────
    private void BuildUi()
    {
        var canvasGo = new GameObject("GhostHudCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SORTING_ORDER;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;
        var root = (RectTransform)canvasGo.transform;

        // 幽霊要素グループ
        var groupGo = new GameObject("GhostElements", typeof(RectTransform), typeof(CanvasGroup));
        groupGo.transform.SetParent(root, false);
        var groupRT = (RectTransform)groupGo.transform;
        Stretch(groupRT);
        _ghostGroup = groupGo.GetComponent<CanvasGroup>();
        _ghostGroup.interactable   = false;
        _ghostGroup.blocksRaycasts = false;

        // 青味フィルタ（全画面）
        _blueTint = CreateImage("BlueTint", groupRT, new Color(0.30f, 0.55f, 0.95f, 0.12f));
        Stretch(_blueTint.rectTransform);

        // 「幽霊モード」ラベル（上中央）
        _ghostLabel = CreateText("GhostLabel", groupRT, "幽霊モード ― 祠を探して復活しよう",
            34f, new Color(0.72f, 0.88f, 1f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
        AnchorTopCenter(_ghostLabel.rectTransform, new Vector2(900f, 50f), -70f);

        // 範囲制限警告（幽霊ラベルの下・赤点滅）
        _leashWarning = CreateText("LeashWarning", groupRT, "範囲制限に近づいています",
            26f, UiPalette.Coral, TextAlignmentOptions.Center, FontStyles.Bold);
        AnchorTopCenter(_leashWarning.rectTransform, new Vector2(800f, 40f), -124f);

        // ピン操作プロンプト（下中央）
        _pinPrompt = CreateText("PinPrompt", groupRT, "[F] ピンを打つ   [WASD/Space/Ctrl] 自由移動",
            22f, UiPalette.CreamDim, TextAlignmentOptions.Center, FontStyles.Normal);
        AnchorBottomCenter(_pinPrompt.rectTransform, new Vector2(900f, 36f), 48f);

        BuildChannelPanel(groupRT);

        // 死亡フラッシュ（全画面・赤）と「あなたは死んだ」
        _deathFlash = CreateImage("DeathFlash", root, new Color(0.65f, 0.06f, 0.06f, 0f));
        Stretch(_deathFlash.rectTransform);
        _deathText = CreateText("DeathText", root, "あなたは死んだ",
            56f, new Color(1f, 0.92f, 0.92f, 0f), TextAlignmentOptions.Center, FontStyles.Bold);
        AnchorMiddleCenter(_deathText.rectTransform, new Vector2(900f, 90f));
    }

    private void BuildChannelPanel(RectTransform parent)
    {
        _channelPanel = new GameObject("ChannelPanel", typeof(RectTransform));
        _channelPanel.transform.SetParent(parent, false);
        var panelRT = (RectTransform)_channelPanel.transform;
        AnchorMiddleCenter(panelRT, new Vector2(420f, 70f));
        panelRT.anchoredPosition = new Vector2(0f, -150f);

        var label = CreateText("ChannelLabel", panelRT, "祠で復活中… (E 長押し)",
            22f, UiPalette.Cream, TextAlignmentOptions.Center, FontStyles.Bold);
        AnchorTopCenter(label.rectTransform, new Vector2(420f, 28f), 0f);

        var track = CreateImage("ChannelTrack", panelRT, UiPalette.Track);
        var trackRT = track.rectTransform;
        trackRT.anchorMin = new Vector2(0.5f, 0.5f);
        trackRT.anchorMax = new Vector2(0.5f, 0.5f);
        trackRT.pivot     = new Vector2(0.5f, 0.5f);
        trackRT.sizeDelta = new Vector2(400f, 22f);
        trackRT.anchoredPosition = new Vector2(0f, -16f);

        _channelFill = CreateImage("ChannelFill", trackRT, UiPalette.Amber);
        _channelFillRT = _channelFill.rectTransform;
        _channelFillRT.anchorMin = new Vector2(0f, 0f);
        _channelFillRT.anchorMax = new Vector2(0f, 1f);
        _channelFillRT.pivot     = new Vector2(0f, 0.5f);
        _channelFillRT.offsetMin = Vector2.zero;
        _channelFillRT.offsetMax = Vector2.zero;
        _channelFillRT.sizeDelta = new Vector2(0f, 0f);

        _channelPanel.SetActive(false);
    }

    // ── 更新 ─────────────────────────────────────────────────
    private void Update()
    {
        ResolveLocalGhost();
        bool isGhost = _localGhost != null && _localGhost.IsGhost;

        if (isGhost && !_wasGhost) TriggerDeathFlash();
        _wasGhost = isGhost;

        SetGhostVisible(isGhost);
        if (isGhost) UpdateGhostElements();

        UpdateDeathFlash();
    }

    private void UpdateGhostElements()
    {
        // 範囲制限警告（赤点滅）
        bool warn = _localGhost.IsNearLeashBoundary;
        _leashWarning.gameObject.SetActive(warn);
        if (warn)
        {
            float a = 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6f));
            var c = _leashWarning.color; c.a = a; _leashWarning.color = c;
        }

        // 復活チャネリングバー
        bool channeling = _localGhost.IsChannelingRevive;
        _channelPanel.SetActive(channeling);
        if (channeling)
        {
            float p = Mathf.Clamp01(_localGhost.ReviveChannelProgress01);
            _channelFillRT.anchorMax = new Vector2(p, 1f);
        }
    }

    private void TriggerDeathFlash() => _deathFlashTimer = DEATH_FLASH_SECONDS;

    private void UpdateDeathFlash()
    {
        if (_deathFlashTimer <= 0f) { SetDeathFlash(0f); return; }
        _deathFlashTimer -= Time.unscaledDeltaTime;
        // 0→1 の経過比。序盤に強く赤、後半でフェードアウト。
        float t = 1f - Mathf.Clamp01(_deathFlashTimer / DEATH_FLASH_SECONDS);
        float flashA = Mathf.Lerp(0.6f, 0f, t);
        float textA  = t < 0.25f ? Mathf.InverseLerp(0f, 0.25f, t)         // フェードイン
                                 : Mathf.InverseLerp(1f, 0.6f, t);          // フェードアウト
        SetDeathFlash(flashA, Mathf.Clamp01(textA));
    }

    private void SetDeathFlash(float flashAlpha, float textAlpha = 0f)
    {
        if (_deathFlash != null) { var c = _deathFlash.color; c.a = flashAlpha; _deathFlash.color = c; }
        if (_deathText  != null) { var c = _deathText.color;  c.a = textAlpha;  _deathText.color  = c; }
    }

    private void SetGhostVisible(bool visible)
    {
        if (_ghostGroup == null) return;
        _ghostGroup.alpha = visible ? 1f : 0f;
        if (!visible && _channelPanel != null) _channelPanel.SetActive(false);
    }

    private void ResolveLocalGhost()
    {
        if (_localGhost != null) return;

        // ネット時は所有権のある幽霊を優先、無ければタグ付き Player（既存 HUD と同じ解決）。
        foreach (var g in FindObjectsByType<GhostSystem>(FindObjectsSortMode.None))
        {
            if (g != null && g.IsOwner) { _localGhost = g; return; }
        }
        var player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) _localGhost = player.GetComponentInChildren<GhostSystem>();
    }

    // ── 生成ヘルパー ──────────────────────────────────────────
    private TextMeshProUGUI CreateText(string name, RectTransform parent, string text,
        float fontSize, Color color, TextAlignmentOptions align, FontStyles style)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var tmp = go.GetComponent<TextMeshProUGUI>();
        if (_font != null) tmp.font = _font;
        tmp.text          = text;
        tmp.fontSize      = fontSize;
        tmp.color         = color;
        tmp.alignment     = align;
        tmp.fontStyle     = style;
        tmp.raycastTarget = false;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    private static Image CreateImage(string name, RectTransform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void AnchorTopCenter(RectTransform rt, Vector2 size, float top)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot     = new Vector2(0.5f, 1f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(0f, top);
    }

    private static void AnchorBottomCenter(RectTransform rt, Vector2 size, float bottom)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot     = new Vector2(0.5f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = new Vector2(0f, bottom);
    }

    private static void AnchorMiddleCenter(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
