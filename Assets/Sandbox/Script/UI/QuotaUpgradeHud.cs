using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 抽出ノルマ・所持金・恒久アップグレードを画面表示する HUD（R.E.P.O. の可視化）。
/// PEAK 風のミニマル/diegetic UI に合わせ、runtime 生成の Canvas + TextMeshPro で描画する
/// （旧 OnGUI 実装からの移行。プロジェクト内の他 HUD は全て TMP/Canvas なので様式を統一）。
/// 数字キーで購入できる: 1=最大HP / 2=最大スタミナ / 3=ダッシュ速度。購入後はローカルプレイヤーへ即再適用。
/// </summary>
public class QuotaUpgradeHud : MonoBehaviour
{
    [Header("Refresh")]
    [SerializeField] private float _refreshInterval = 0.5f;

    [Header("Font（未設定なら NotoSansJP を実行時に解決）")]
    [Tooltip("日本語 TMP フォント。未設定時はシーン内の既存 TMP / TMP_Settings から自動解決する。")]
    [SerializeField] private TMP_FontAsset _jpFont;

    [Header("Canvas")]
    [SerializeField] private Vector2 _referenceResolution = new Vector2(1920f, 1080f);
    [SerializeField] private int _sortingOrder = 5;

    // ── 色（PEAK 風ミニマル: 暗い半透明パネル + 高コントラスト文字で高視認）──
    private static readonly Color PanelBg = new Color(0.07f, 0.08f, 0.10f, 0.50f);
    private static readonly Color TextMain = new Color(0.96f, 0.97f, 1.00f);
    private static readonly Color TextDim  = new Color(0.74f, 0.80f, 0.86f);
    private static readonly Color Gold     = new Color(1.00f, 0.86f, 0.40f);
    private static readonly Color Good      = new Color(0.50f, 1.00f, 0.60f);

    private float _refreshTimer;
    private int   _cachedExtracted;
    private bool  _built;

    private TextMeshProUGUI _quotaTitle, _quotaValue, _moneyLabel, _up1, _up2, _up3;

    /// <summary>テスト用：最後にキャッシュした搬入価値。</summary>
    public int CachedExtracted => _cachedExtracted;

    private void Start()
    {
        BuildUi();
        RefreshTexts(); // 初期表示
    }

    private void Update()
    {
        _refreshTimer -= Time.deltaTime;
        if (_refreshTimer <= 0f)
        {
            var q = ExtractionQuotaSystem.Instance;
            _cachedExtracted = q != null ? q.PeekExtractedValue() : 0;
            _refreshTimer = _refreshInterval;
            RefreshTexts();
        }
        HandlePurchaseKeys();
    }

    private void HandlePurchaseKeys()
    {
        if (InputStateReader.KeyPressedThisFrame(Key.Digit1)) TryBuy(UpgradeType.MaxHealth);
        if (InputStateReader.KeyPressedThisFrame(Key.Digit2)) TryBuy(UpgradeType.MaxStamina);
        if (InputStateReader.KeyPressedThisFrame(Key.Digit3)) TryBuy(UpgradeType.SprintSpeed);
    }

    private void TryBuy(UpgradeType type)
    {
        if (UpgradeStore.TryPurchase(type))
        {
            ReapplyToLocalPlayer();
            RefreshTexts(); // 購入直後に残高/レベル/価格を即反映（0.5s 待たない）
        }
    }

    private static void ReapplyToLocalPlayer()
    {
        foreach (var p in PlayerHealthSystem.RegisteredPlayers)
        {
            var applier = p != null ? p.GetComponent<PlayerUpgradeApplier>() : null;
            if (applier != null) applier.Apply();
        }
    }

    // ── 表示更新 ────────────────────────────────────────────────
    private void RefreshTexts()
    {
        if (!_built) return;

        var q = ExtractionQuotaSystem.Instance;
        int required = q != null ? q.RequiredQuota : 0;
        int level    = q != null ? q.Level : 1;
        bool met     = _cachedExtracted >= required && required > 0;

        _quotaTitle.text = $"ノルマ Lv{level}";
        _quotaValue.text = met ? $"抽出 {_cachedExtracted} / {required} pt  [達成]"
                               : $"抽出 {_cachedExtracted} / {required} pt";
        _quotaValue.color = met ? Good : TextMain;

        _moneyLabel.text = $"所持金 {CurrencyWallet.Balance} G";
        _up1.text = UpgradeLine("[1] 最大HP",     UpgradeType.MaxHealth);
        _up2.text = UpgradeLine("[2] 最大スタミナ", UpgradeType.MaxStamina);
        _up3.text = UpgradeLine("[3] ダッシュ速度",  UpgradeType.SprintSpeed);
    }

    private static string UpgradeLine(string name, UpgradeType type)
    {
        int lv = UpgradeStore.GetLevel(type);
        if (!UpgradeStore.CanUpgrade(type))
            return $"{name}  Lv{lv}/{UpgradeStore.MaxLevel} (MAX)";
        return $"{name}  Lv{lv}/{UpgradeStore.MaxLevel}  次 {UpgradeStore.GetCost(type)}G";
    }

    // ── UI 構築（runtime・コード生成）─────────────────────────────
    private void BuildUi()
    {
        if (_built) return;
        var font = ResolveJapaneseFont();

        var canvasGo = new GameObject("QuotaHudCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = _sortingOrder;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = _referenceResolution;
        scaler.matchWidthOrHeight  = 0.5f;
        // GraphicRaycaster は入力不要なので付けない（数字キーは InputStateReader 経由）。

        // ── ノルマパネル（上中央）──
        var quotaPanel = MakePanel(canvasGo.transform, "QuotaPanel",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(380f, 78f), new Vector2(0f, -14f));
        _quotaTitle = MakeText(quotaPanel, "Title", 8f, 26f, 22, TextDim,  TextAlignmentOptions.Center, "ノルマ Lv1");
        _quotaValue = MakeText(quotaPanel, "Value", 34f, 36f, 28, TextMain, TextAlignmentOptions.Center, "抽出 0 / 0 pt");

        // ── 所持金 + アップグレード（左下）──
        var upPanel = MakePanel(canvasGo.transform, "UpgradePanel",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(330f, 158f), new Vector2(16f, 16f));
        _moneyLabel = MakeText(upPanel, "Money", 8f,  30f, 24, Gold,     TextAlignmentOptions.Left, "所持金 0 G");
        _up1        = MakeText(upPanel, "Up1",   42f, 26f, 19, TextMain, TextAlignmentOptions.Left, "[1] 最大HP");
        _up2        = MakeText(upPanel, "Up2",   70f, 26f, 19, TextMain, TextAlignmentOptions.Left, "[2] 最大スタミナ");
        _up3        = MakeText(upPanel, "Up3",   98f, 26f, 19, TextMain, TextAlignmentOptions.Left, "[3] ダッシュ速度");

        if (font != null)
            foreach (var t in canvasGo.GetComponentsInChildren<TextMeshProUGUI>(true))
                t.font = font;

        _built = true;
    }

    private RectTransform MakePanel(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = PanelBg;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = pivot;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return rt;
    }

    /// <summary>パネル左上を原点に、横幅いっぱい(左右 12px インセット)・高さ height の行を yTop 下に配置。</summary>
    private TextMeshProUGUI MakeText(RectTransform panel, string name, float yTop, float height,
        int fontSize, Color color, TextAlignmentOptions align, string initial)
    {
        const float padX = 12f;
        var go = new GameObject(name);
        go.transform.SetParent(panel, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = initial;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;

        var rt = tmp.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f); rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(panel.sizeDelta.x - padX * 2f, height);
        rt.anchoredPosition = new Vector2(padX, -yTop);
        return tmp;
    }

    /// <summary>
    /// 実行時に日本語 TMP フォントを解決する。AssetDatabase は使えないので:
    /// ① Inspector 指定 → ② ロード済みフォントから "NotoSansJP" を名前検索 →
    /// ③ シーン内既存 TMP のフォント（OfflineSceneCreator が NotoSansJP を適用済み）→ ④ TMP 既定。
    /// </summary>
    private TMP_FontAsset ResolveJapaneseFont()
    {
        if (_jpFont != null) return _jpFont;

        foreach (var f in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            if (f != null && f.name.IndexOf("NotoSansJP", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return f;

        var existing = FindFirstObjectByType<TextMeshProUGUI>();
        if (existing != null && existing.font != null) return existing.font;

        return TMP_Settings.defaultFontAsset;
    }
}
