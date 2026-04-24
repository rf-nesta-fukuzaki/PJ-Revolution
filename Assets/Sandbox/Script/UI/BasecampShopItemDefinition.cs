using System;
using UnityEngine;

[Serializable]
public sealed class BasecampShopItemDefinition
{
    [SerializeField] private string _id = "shop.unknown";
    [SerializeField] private string _displayName = "Unknown Item";
    [SerializeField] private int _cost = 1;
    [SerializeField] private float _weight = 1f;
    [SerializeField] private int _slots = 1;
    [SerializeField] private float _durability = 100f;
    [SerializeField] [TextArea] private string _description = string.Empty;
    [SerializeField] private ShopItemType _itemType = ShopItemType.ShortRope10m;
    [SerializeField] private bool _isMetal;

    public string Id => _id;
    public string DisplayName => _displayName;
    public int Cost => _cost;
    public float Weight => _weight;
    public int Slots => _slots;
    public float Durability => _durability;
    public string Description => _description;
    public ShopItemType ItemType => _itemType;
    public bool IsMetal => _isMetal;

    public BasecampShopItemDefinition()
    {
    }

    public BasecampShopItemDefinition(
        string id,
        string displayName,
        int cost,
        float weight,
        int slots,
        float durability,
        string description,
        ShopItemType itemType,
        bool isMetal)
    {
        _id = id;
        _displayName = displayName;
        _cost = cost;
        _weight = weight;
        _slots = slots;
        _durability = durability;
        _description = description;
        _itemType = itemType;
        _isMetal = isMetal;
    }

    public bool TryValidate(out string reason)
    {
        if (string.IsNullOrWhiteSpace(_id))
        {
            reason = "itemId が未設定です";
            return false;
        }

        if (string.IsNullOrWhiteSpace(_displayName))
        {
            reason = $"displayName が未設定です (itemId={_id})";
            return false;
        }

        if (_cost <= 0)
        {
            reason = $"cost は 1 以上にしてください (itemId={_id})";
            return false;
        }

        if (_weight <= 0f)
        {
            reason = $"weight は正の値にしてください (itemId={_id})";
            return false;
        }

        if (_slots <= 0)
        {
            reason = $"slots は 1 以上にしてください (itemId={_id})";
            return false;
        }

        if (_durability <= 0f)
        {
            reason = $"durability は正の値にしてください (itemId={_id})";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}

public enum ShopItemType
{
    ShortRope10m,
    LongRope25m,
    IceAxe,
    AnchorBolt,
    GrapplingHook,
    Stretcher,
    PackingKit,
    ThermalCase,
    SecureBelt,
    FoodPack,
    FlareGun,
    EmergencyRadio,
    PortableWinch,
    BivouacTent,
    OxygenTank
}
