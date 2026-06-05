using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// BasecampShop の Inspector 未配線時に最小ショップ UI を実行時生成する。
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
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        var shopRoot = new GameObject("BasecampShopUI");
        shopRoot.transform.SetParent(canvasGo.transform, false);

        var panel = CreatePanel(shopRoot.transform);
        var budget = CreateLabel(panel.transform, "BudgetLabel", "予算: 100 pt",
            new Vector2(0.5f, 1f), new Vector2(300f, 35f), new Vector2(0f, -75f), 20);
        var itemList = CreateItemList(panel.transform);
        var depart = CreateButton(panel.transform, "DepartButton", "出 発",
            new Vector2(0.5f, 0f), new Vector2(200f, 44f), new Vector2(0f, 24f));
        var weather = CreateLabel(panel.transform, "WeatherLabel", "天候: --",
            new Vector2(0.5f, 0f), new Vector2(300f, 28f), new Vector2(0f, 80f), 16);
        var route = CreateLabel(panel.transform, "RouteStatusLabel", "ルート: --",
            new Vector2(0.5f, 0f), new Vector2(300f, 28f), new Vector2(0f, 50f), 14);
        var error = CreateLabel(panel.transform, "ErrorLabel", "",
            new Vector2(0.5f, 0f), new Vector2(300f, 28f), new Vector2(0f, 110f), 14);
        error.gameObject.SetActive(false);
        error.color = new Color(1f, 0.45f, 0.35f, 1f);

        CreateLabel(panel.transform, "ShopTitle", "BASECAMP SHOP",
            new Vector2(0.5f, 1f), new Vector2(300f, 50f), new Vector2(0f, -30f), 26);

        return new UiRefs
        {
            ShopPanel       = panel,
            BudgetLabel     = budget,
            ItemListParent  = itemList,
            WeatherLabel    = weather,
            RouteStatusLabel = route,
            DepartButton    = depart,
            ErrorLabel      = error,
        };
    }

    private static GameObject CreatePanel(Transform parent)
    {
        var panel = new GameObject("ShopPanel");
        panel.transform.SetParent(parent, false);
        var rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 0.5f);
        rt.sizeDelta = new Vector2(360f, 0f);
        rt.anchoredPosition = new Vector2(12f, 0f);

        var bg = panel.AddComponent<Image>();
        bg.color = new Color(0.1f, 0.08f, 0.05f, 0.92f);
        panel.SetActive(false);
        return panel;
    }

    private static Transform CreateItemList(Transform panel)
    {
        var itemListGo = new GameObject("ItemList");
        itemListGo.transform.SetParent(panel, false);
        var rt = itemListGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.offsetMin = new Vector2(10f, 60f);
        rt.offsetMax = new Vector2(-10f, -110f);
        itemListGo.AddComponent<VerticalLayoutGroup>().spacing = 4f;
        return itemListGo.transform;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent, string name, string text,
        Vector2 anchor, Vector2 size, Vector2 pos, float fontSize)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot     = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    private static Button CreateButton(
        Transform parent, string name, string label,
        Vector2 anchor, Vector2 size, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot     = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;

        var image = go.AddComponent<Image>();
        image.color = new Color(0.22f, 0.48f, 0.28f, 1f);
        var button = go.AddComponent<Button>();

        var textGo = new GameObject("Text");
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.fontSize = 20;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;

        return button;
    }
}
