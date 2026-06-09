using Sandbox.UI;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §14.6 — インベントリ HUD（R.E.P.O. 端末スロット風・4枠）。
/// </summary>
public class InventoryHud : MonoBehaviour
{
    public static InventoryHud Instance { get; private set; }

    private PlayerInventory _boundInventory;
    private GameObject      _panel;
    private readonly SlotView[] _slots = new SlotView[4];

    private struct SlotView
    {
        public RectTransform Root;
        public Image Icon;
        public Image DurabilityFill;
        public TextMeshProUGUI Label;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildUi();
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        Unbind();
    }

    public void Bind(PlayerInventory inventory)
    {
        Unbind();
        _boundInventory = inventory;
        if (_boundInventory != null)
            _boundInventory.OnInventoryChanged += Refresh;
        Refresh();
    }

    private void Unbind()
    {
        if (_boundInventory != null)
            _boundInventory.OnInventoryChanged -= Refresh;
        _boundInventory = null;
    }

    public void Toggle() => SetVisible(_panel != null && !_panel.activeSelf);

    public void SetVisible(bool visible)
    {
        if (_panel != null) _panel.SetActive(visible);
        if (visible) Refresh();
    }

    private void Update()
    {
        if (_boundInventory == null)
            TryAutoBind();
        if (_panel != null && _panel.activeSelf)
            Refresh();
    }

    private void TryAutoBind()
    {
        var inventories = PlayerInventory.RegisteredInventories;
        if (inventories.Count == 0) return;
        Bind(inventories[0]);
    }

    private void Refresh()
    {
        if (_boundInventory == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            var item = _boundInventory.QuickSlotItems.Count > i
                ? _boundInventory.QuickSlotItems[i]
                : null;

            if (item == null && i < _boundInventory.BackpackItems.Count)
                item = _boundInventory.BackpackItems[i];

            var view = _slots[i];
            bool hasItem = item != null && !item.IsBroken;
            if (view.Icon != null)
                view.Icon.color = hasItem ? UiPalette.Amber : new Color(0.2f, 0.22f, 0.26f, 0.5f);
            if (view.DurabilityFill != null)
                view.DurabilityFill.fillAmount = hasItem ? item.DurabilityPct : 0f;
            if (view.Label != null)
                view.Label.text = hasItem ? item.ItemName : $"[{i + 1}]";
        }
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("InventoryHudCanvas");
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGo.AddComponent<GraphicRaycaster>();

        var outer = FlowUiTheme.NewRect("InventoryPanel", canvasGo.transform);
        outer.anchorMin = outer.anchorMax = new Vector2(1f, 0f);
        outer.pivot = new Vector2(1f, 0f);
        outer.anchoredPosition = new Vector2(-20f, 24f);
        outer.sizeDelta = new Vector2(392f, 88f);
        _panel = outer.gameObject;
        FlowUiTheme.AddSprite(outer, UiSprite.RoundedRect(18), FlowUiTheme.TerminalBg);

        var frame = FlowUiTheme.NewRect("Frame", outer);
        FlowUiTheme.Stretch(frame);
        FlowUiTheme.AddSprite(frame, UiSprite.RoundedFrame(18, 2), FlowUiTheme.TerminalBorder)
            .raycastTarget = false;

        var inner = FlowUiTheme.NewRect("Inner", outer);
        FlowUiTheme.Stretch(inner, 2f);

        var header = CreateLabel(inner, "Header", "GEAR", 11, new Vector2(0.02f, 0.92f),
            new Vector2(0.3f, 0.92f), FlowUiTheme.TerminalAccent, FontStyles.Bold);

        for (int i = 0; i < 4; i++)
        {
            var slotFrame = FlowUiTheme.CreateTerminalPanel(inner, $"Slot{i + 1}",
                new Vector2(0f, 0f), new Vector2(0f, 0f),
                new Vector2(10f + i * 94f, 10f), new Vector2(92f + i * 94f, 72f));

            var iconGo = FlowUiTheme.NewRect("Icon", slotFrame);
            iconGo.anchorMin = new Vector2(0.08f, 0.28f);
            iconGo.anchorMax = new Vector2(0.92f, 0.92f);
            iconGo.offsetMin = iconGo.offsetMax = Vector2.zero;
            var icon = iconGo.gameObject.AddComponent<Image>();
            icon.color = new Color(0.2f, 0.22f, 0.26f, 0.5f);

            var barBg = FlowUiTheme.NewRect("DurabilityBg", slotFrame);
            barBg.anchorMin = new Vector2(0.1f, 0.1f);
            barBg.anchorMax = new Vector2(0.9f, 0.22f);
            barBg.offsetMin = barBg.offsetMax = Vector2.zero;
            barBg.gameObject.AddComponent<Image>().color = UiPalette.Track;

            var barFill = FlowUiTheme.NewRect("DurabilityFill", barBg);
            FlowUiTheme.Stretch(barFill);
            var fill = barFill.gameObject.AddComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = UiPalette.Sage;

            var label = CreateLabel(slotFrame, "Label", $"[{i + 1}]", 9,
                new Vector2(0f, 1f), new Vector2(1f, 1f), UiPalette.CreamDim, FontStyles.Normal);
            label.rectTransform.offsetMin = new Vector2(2f, -14f);
            label.rectTransform.offsetMax = new Vector2(-2f, -2f);

            _slots[i] = new SlotView
            {
                Root = slotFrame,
                Icon = icon,
                DurabilityFill = fill,
                Label = label,
            };
        }
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string name, string text, int size,
        Vector2 anchorMin, Vector2 anchorMax, Color color, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = color;
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;
        FlowUiTheme.StyleReadable(tmp, 0.1f);
        return tmp;
    }
}
