using System;
using Sandbox.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopItemRow : MonoBehaviour
{
    private const float ROW_HEIGHT     = 56f;
    private const float NAME_WIDTH     = 150f;
    private const float COST_WIDTH     = 80f;
    private const float COUNT_WIDTH    = 56f;
    private const float BUTTON_WIDTH   = 88f;
    private const float BUTTON_HEIGHT  = 40f;

    public string ItemId { get; private set; }

    private BasecampShopItemDefinition _item;
    private Func<string, bool> _purchaseAction;
    private Func<string, bool> _refundAction;

    private TextMeshProUGUI _nameLabel;
    private TextMeshProUGUI _costLabel;
    private TextMeshProUGUI _countLabel;
    private Button _buyButton;
    private Button _refundButton;

    public void Init(
        BasecampShopItemDefinition item,
        Func<string, bool> purchaseAction,
        Func<string, bool> refundAction)
    {
        _item = item;
        _purchaseAction = purchaseAction;
        _refundAction = refundAction;
        ItemId = item.Id;

        SetupUI();
        Refresh(0, 0);
    }

    public void Refresh(int purchasedCount, int remainingBudget)
    {
        if (_item == null) return;

        if (_nameLabel != null)
            _nameLabel.text = $"{_item.DisplayName}  <size=80%><color=#9aa6b2>重{_item.Weight}/枠{_item.Slots}</color></size>";

        if (_costLabel != null)
            _costLabel.text = $"{_item.Cost}pt";

        if (_countLabel != null)
            _countLabel.text = $"×{purchasedCount}";

        if (_buyButton != null)
            _buyButton.interactable = remainingBudget >= _item.Cost;

        if (_refundButton != null)
            _refundButton.interactable = purchasedCount > 0;
    }

    private void SetupUI()
    {
        _nameLabel = transform.Find("NameLabel")?.GetComponent<TextMeshProUGUI>();
        _costLabel = transform.Find("CostLabel")?.GetComponent<TextMeshProUGUI>();
        _countLabel = transform.Find("CountLabel")?.GetComponent<TextMeshProUGUI>();
        _buyButton = transform.Find("BuyButton")?.GetComponent<Button>();
        _refundButton = transform.Find("RefundButton")?.GetComponent<Button>();

        bool needsFallback = _nameLabel == null
                             || _costLabel == null
                             || _countLabel == null
                             || _buyButton == null
                             || _refundButton == null;
        if (needsFallback)
            EnsureFallbackRowLayout();

        if (_nameLabel == null)
            _nameLabel = CreateFallbackLabel("NameLabel", _item.DisplayName, NAME_WIDTH, TextAlignmentOptions.MidlineLeft);

        if (_costLabel == null)
            _costLabel = CreateFallbackLabel("CostLabel", $"{_item.Cost}pt", COST_WIDTH, TextAlignmentOptions.Center);

        if (_countLabel == null)
            _countLabel = CreateFallbackLabel("CountLabel", "×0", COUNT_WIDTH, TextAlignmentOptions.Center);

        if (_buyButton == null)
            _buyButton = CreateFallbackButton("BuyButton", "購入", MenuUiKit.BtnPrimary);

        if (_refundButton == null)
            _refundButton = CreateFallbackButton("RefundButton", "返品", MenuUiKit.BtnDanger);

        if (_buyButton != null)
        {
            _buyButton.onClick.RemoveAllListeners();
            _buyButton.onClick.AddListener(() => _purchaseAction?.Invoke(ItemId));
        }

        if (_refundButton != null)
        {
            _refundButton.onClick.RemoveAllListeners();
            _refundButton.onClick.AddListener(() => _refundAction?.Invoke(ItemId));
        }
    }

    private void EnsureFallbackRowLayout()
    {
        var layout = GetComponent<HorizontalLayoutGroup>();
        if (layout == null)
            layout = gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        // childControlWidth=true で各列の LayoutElement 幅を尊重させる。
        // false のままだと子の RectTransform 幅が未定義のまま潰れ、名前が読めなくなる。
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childScaleWidth = false;
        layout.childScaleHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(12, 12, 8, 8);

        var layoutElement = GetComponent<LayoutElement>();
        if (layoutElement == null)
            layoutElement = gameObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = ROW_HEIGHT;
        layoutElement.preferredHeight = ROW_HEIGHT;
        layoutElement.flexibleHeight = 0f;
        layoutElement.flexibleWidth = 1f;

        var background = GetComponent<Image>();
        if (background == null)
            background = gameObject.AddComponent<Image>();
        background.color = new Color(0.06f, 0.08f, 0.11f, 0.92f);

        var border = new GameObject("RowBorder");
        border.transform.SetParent(transform, false);
        var brt = border.AddComponent<RectTransform>();
        brt.anchorMin = Vector2.zero;
        brt.anchorMax = Vector2.one;
        brt.offsetMin = Vector2.zero;
        brt.offsetMax = Vector2.zero;
        var borderImg = border.AddComponent<Image>();
        borderImg.color = FlowUiTheme.TerminalBorder;
        borderImg.raycastTarget = false;
        border.transform.SetAsFirstSibling();
    }

    private TextMeshProUGUI CreateFallbackLabel(string objectName, string text, float preferredWidth, TextAlignmentOptions alignment)
    {
        var go = new GameObject(objectName);
        go.transform.SetParent(transform, false);

        var layoutElement = go.AddComponent<LayoutElement>();
        layoutElement.minWidth = preferredWidth;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.flexibleWidth = objectName == "NameLabel" ? 1f : 0f;
        layoutElement.minHeight = BUTTON_HEIGHT;
        layoutElement.preferredHeight = BUTTON_HEIGHT;

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 14f;
        tmp.fontSizeMax = 24f;
        tmp.fontSize = 20f;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        tmp.color = UiPalette.Cream;
        FlowUiTheme.StyleReadable(tmp, 0.1f);
        return tmp;
    }

    private Button CreateFallbackButton(string objectName, string label, Color color)
    {
        var buttonGo = new GameObject(objectName);
        buttonGo.transform.SetParent(transform, false);

        var layoutElement = buttonGo.AddComponent<LayoutElement>();
        layoutElement.minWidth = BUTTON_WIDTH;
        layoutElement.preferredWidth = BUTTON_WIDTH;
        layoutElement.minHeight = BUTTON_HEIGHT;
        layoutElement.preferredHeight = BUTTON_HEIGHT;
        layoutElement.flexibleWidth = 0f;

        var image = buttonGo.AddComponent<Image>();
        image.color = color;

        var button = buttonGo.AddComponent<Button>();
        button.targetGraphic = image;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(buttonGo.transform, false);

        var rect = labelGo.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 13f;
        tmp.fontSizeMax = 22f;
        tmp.fontSize = 18f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.color = UiPalette.Cream;
        tmp.raycastTarget = false;
        FlowUiTheme.StyleReadable(tmp, 0.1f);

        return button;
    }
}
