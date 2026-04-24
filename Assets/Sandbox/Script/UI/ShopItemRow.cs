using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ShopItemRow : MonoBehaviour
{
    private const float ROW_HEIGHT     = 56f;
    private const float NAME_WIDTH     = 460f;
    private const float COST_WIDTH     = 88f;
    private const float COUNT_WIDTH    = 64f;
    private const float BUTTON_WIDTH   = 92f;
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
            _nameLabel.text = $"{_item.DisplayName}  [{_item.Cost}pt / 重{_item.Weight} / 枠{_item.Slots}]";

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
            _buyButton = CreateFallbackButton("BuyButton", "購入", new Color(0.17f, 0.45f, 0.21f, 0.95f));

        if (_refundButton == null)
            _refundButton = CreateFallbackButton("RefundButton", "返品", new Color(0.35f, 0.19f, 0.19f, 0.95f));

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
        layout.childControlWidth = false;
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
        background.color = new Color(0.09f, 0.13f, 0.18f, 0.92f);
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
        tmp.margin = new Vector4(6f, 2f, 6f, 2f);
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
        tmp.color = Color.white;
        tmp.raycastTarget = false;

        return button;
    }
}
