using Sandbox.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BasecampShop の Inspector 未配線時に R.E.P.O. 端末風ショップ UI を実行時生成する。
/// </summary>
public static class BasecampShopRuntimeUi
{
    public sealed class UiRefs
    {
        public GameObject ShopPanel;
        public TextMeshProUGUI BudgetLabel;
        public Transform ItemListParent;
        public TextMeshProUGUI WeatherLabel;
        public TextMeshProUGUI RouteStatusLabel;
        public Button DepartButton;
        public TextMeshProUGUI ErrorLabel;
    }

    public static UiRefs Build()
    {
        var canvasGo = new GameObject("BasecampShopCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 35;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        // ルートは Canvas を満たす RectTransform にする。素の Transform だと子 ShopPanel の
        // フルハイト・アンカーが解決できず rect 高さ 0 に退化し、UI 全体が一点に潰れてしまう。
        var shopRoot = new GameObject("BasecampShopUI", typeof(RectTransform));
        shopRoot.transform.SetParent(canvasGo.transform, false);
        var rootRt = (RectTransform)shopRoot.transform;
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var panel = CreateShopPanel(shopRoot.transform);

        CreateHeader(panel.transform, "SHOP TERMINAL", 26, new Vector2(0f, 1f), new Vector2(0f, -28f));
        CreateHeader(panel.transform,
            GameFlow.RunCount > 0 ? $"RUN #{GameFlow.RunCount + 1}" : "RUN #1 — FIRST DEPARTURE",
            15, new Vector2(0f, 1f), new Vector2(0f, -58f), UiPalette.CreamDim);

        var budget = CreateLabel(panel.transform, "BudgetLabel", "BUDGET: 100 / 100 pt",
            new Vector2(0.5f, 1f), new Vector2(360f, 36f), new Vector2(0f, -92f), 20, FlowUiTheme.TerminalAccent);
        var itemList = CreateItemList(panel.transform);
        var depart = CreateDepartButton(panel.transform);
        // 既定では anchor(0.5,0)+offset0 で下端から半分はみ出すため、フッター帯の中で持ち上げる。
        var departRt = depart.GetComponent<RectTransform>();
        departRt.anchoredPosition = new Vector2(0f, 44f);
        var weather = CreateLabel(panel.transform, "WeatherLabel",
            $"FORECAST: {GameFlowSessionState.LastWeatherDisplay}",
            new Vector2(0.5f, 0f), new Vector2(360f, 28f), new Vector2(0f, 118f), 14, UiPalette.CreamDim);
        var route = CreateLabel(panel.transform, "RouteStatusLabel",
            GameFlowSessionState.LastRouteSummary.ToUpperInvariant(),
            new Vector2(0.5f, 0f), new Vector2(360f, 28f), new Vector2(0f, 92f), 13, UiPalette.CreamDim);
        var error = CreateLabel(panel.transform, "ErrorLabel", "",
            new Vector2(0.5f, 0f), new Vector2(360f, 28f), new Vector2(0f, 146f), 14, FlowUiTheme.TerminalWarn);
        error.gameObject.SetActive(false);

        return new UiRefs
        {
            ShopPanel        = panel,
            BudgetLabel      = budget,
            ItemListParent   = itemList,
            WeatherLabel     = weather,
            RouteStatusLabel = route,
            DepartButton     = depart,
            ErrorLabel       = error,
        };
    }

    private static GameObject CreateShopPanel(Transform parent)
    {
        var outer = FlowUiTheme.NewRect("ShopPanel", parent);
        outer.anchorMin = new Vector2(0f, 0f);
        outer.anchorMax = new Vector2(0f, 1f);
        outer.pivot     = new Vector2(0f, 0.5f);
        // 名前＋価格＋個数＋購入/返品ボタンを 1 行に収めるには 440px では狭すぎるため拡張する。
        outer.sizeDelta = new Vector2(760f, 0f);
        outer.anchoredPosition = new Vector2(20f, 0f);
        outer.gameObject.AddComponent<Image>().color = FlowUiTheme.TerminalBorder;

        var inner = FlowUiTheme.NewRect("Inner", outer);
        FlowUiTheme.Stretch(inner, 2f);
        inner.gameObject.AddComponent<Image>().color = FlowUiTheme.TerminalBg;

        var accent = FlowUiTheme.NewRect("AccentLine", outer);
        accent.anchorMin = new Vector2(1f, 0f);
        accent.anchorMax = new Vector2(1f, 1f);
        accent.sizeDelta = new Vector2(2f, 0f);
        var accentColor = FlowUiTheme.TerminalAccent;
        accentColor.a *= 0.55f; // ネオン境界が強すぎたため落ち着かせる
        accent.gameObject.AddComponent<Image>().color = accentColor;

        outer.gameObject.SetActive(false);
        return outer.gameObject;
    }

    private static void CreateHeader(Transform panel, string text, int size, Vector2 anchor, Vector2 pos,
        Color? color = null)
    {
        var tmp = CreateLabel(panel, "Header_" + text.GetHashCode(), text, anchor,
            new Vector2(380f, size * 1.6f), pos, size, color ?? UiPalette.Amber, FontStyles.Bold);
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.rectTransform.anchorMin = tmp.rectTransform.anchorMax = anchor;
        tmp.rectTransform.pivot = new Vector2(0f, 1f);
        tmp.rectTransform.anchoredPosition = pos;
        FlowUiTheme.StyleReadable(tmp, 0.16f);
    }

    private static Transform CreateItemList(Transform panel)
    {
        var listFrame = FlowUiTheme.CreateTerminalPanel(panel, "ItemListFrame",
            new Vector2(0f, 0f), new Vector2(1f, 1f),
            new Vector2(14f, 150f), new Vector2(-14f, -120f));

        var itemListGo = new GameObject("ItemList");
        itemListGo.transform.SetParent(listFrame, false);
        var rt = itemListGo.AddComponent<RectTransform>();
        FlowUiTheme.Stretch(rt, 6f);
        var layout = itemListGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(4, 4, 4, 4);
        return itemListGo.transform;
    }

    private static Button CreateDepartButton(Transform panel)
    {
        return MenuUiKit.CreateMenuButton(panel, "DepartButton", "▶  出 発",
            new Vector2(0.5f, 0f), new Vector2(300f, 64f),
            new Color(0.14f, 0.38f, 0.24f, 0.95f),
            null, UiPalette.Amber);
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent, string name, string text,
        Vector2 anchor, Vector2 size, Vector2 pos, float fontSize,
        Color? color = null, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot     = anchor.y > 0.5f ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color ?? UiPalette.Cream;
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        FlowUiTheme.StyleReadable(tmp, 0.12f);
        return tmp;
    }
}
