using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §14.6 — インベントリ HUD（4スロット・耐久ミニバー）。
/// </summary>
public class InventoryHud : MonoBehaviour
{
    public static InventoryHud Instance { get; private set; }

    private PlayerInventory _boundInventory;
    private GameObject      _panel;
    private readonly SlotView[] _slots = new SlotView[4];

    private struct SlotView
    {
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
                view.Icon.color = hasItem ? new Color(0.85f, 0.75f, 0.45f, 1f) : new Color(0.25f, 0.25f, 0.25f, 0.6f);
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
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        _panel = new GameObject("InventoryPanel");
        _panel.transform.SetParent(canvasGo.transform, false);
        var panelRect = _panel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot     = new Vector2(1f, 0f);
        panelRect.anchoredPosition = new Vector2(-24f, 24f);
        panelRect.sizeDelta = new Vector2(360f, 72f);

        var bg = _panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        for (int i = 0; i < 4; i++)
        {
            var slotGo = new GameObject($"Slot{i + 1}");
            slotGo.transform.SetParent(_panel.transform, false);
            var rect = slotGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot     = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(12f + i * 86f, 0f);
            rect.sizeDelta = new Vector2(76f, 56f);

            var frame = slotGo.AddComponent<Image>();
            frame.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);

            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(slotGo.transform, false);
            var iconRect = iconGo.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(6f, 16f);
            iconRect.offsetMax = new Vector2(-6f, -6f);
            var icon = iconGo.AddComponent<Image>();
            icon.color = new Color(0.25f, 0.25f, 0.25f, 0.6f);

            var barBgGo = new GameObject("DurabilityBg");
            barBgGo.transform.SetParent(slotGo.transform, false);
            var barBgRect = barBgGo.AddComponent<RectTransform>();
            barBgRect.anchorMin = new Vector2(0f, 0f);
            barBgRect.anchorMax = new Vector2(1f, 0f);
            barBgRect.pivot     = new Vector2(0.5f, 0f);
            barBgRect.anchoredPosition = new Vector2(0f, 4f);
            barBgRect.sizeDelta = new Vector2(-12f, 6f);
            barBgGo.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 1f);

            var barGo = new GameObject("DurabilityFill");
            barGo.transform.SetParent(barBgGo.transform, false);
            var barRect = barGo.AddComponent<RectTransform>();
            barRect.anchorMin = Vector2.zero;
            barRect.anchorMax = Vector2.one;
            barRect.offsetMin = Vector2.zero;
            barRect.offsetMax = Vector2.zero;
            var fill = barGo.AddComponent<Image>();
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.color = new Color(0.3f, 0.85f, 0.35f, 1f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(slotGo.transform, false);
            var labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot     = new Vector2(0.5f, 1f);
            labelRect.anchoredPosition = new Vector2(0f, -2f);
            labelRect.sizeDelta = new Vector2(-4f, 14f);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            label.fontSize = 10f;
            label.alignment = TextAlignmentOptions.Center;
            label.text = $"[{i + 1}]";

            _slots[i] = new SlotView { Icon = icon, DurabilityFill = fill, Label = label };
        }
    }
}
