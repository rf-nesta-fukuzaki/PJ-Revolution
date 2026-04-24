using System;
using System.Collections.Generic;
using UnityEngine;

public static class BasecampShopItemFactory
{
    private static readonly Dictionary<ShopItemType, Func<GameObject, ItemBase>> s_factories = new()
    {
        { ShopItemType.ShortRope10m, go => go.AddComponent<ShortRopeItem>() },
        { ShopItemType.LongRope25m, go => go.AddComponent<LongRopeItem>() },
        { ShopItemType.IceAxe, go => go.AddComponent<IceAxeItem>() },
        { ShopItemType.AnchorBolt, go => go.AddComponent<AnchorBoltItem>() },
        { ShopItemType.GrapplingHook, go => go.AddComponent<GrapplingHookItem>() },
        { ShopItemType.Stretcher, go => go.AddComponent<StretcherItem>() },
        { ShopItemType.PackingKit, go => go.AddComponent<PackingKitItem>() },
        { ShopItemType.ThermalCase, go => go.AddComponent<ThermalCaseItem>() },
        { ShopItemType.SecureBelt, go => go.AddComponent<SecureBeltItem>() },
        { ShopItemType.FoodPack, go => go.AddComponent<FoodItem>() },
        { ShopItemType.FlareGun, go => go.AddComponent<FlareGunItem>() },
        { ShopItemType.EmergencyRadio, go => go.AddComponent<EmergencyRadioItem>() },
        { ShopItemType.PortableWinch, go => go.AddComponent<PortableWinchItem>() },
        { ShopItemType.BivouacTent, go => go.AddComponent<BivouacTentItem>() },
        { ShopItemType.OxygenTank, go => go.AddComponent<OxygenTankItem>() },
    };

    public static bool TryCreate(GameObject target, ShopItemType itemType, out ItemBase item)
    {
        item = null;

        if (target == null) return false;
        if (!s_factories.TryGetValue(itemType, out var factory)) return false;

        item = factory(target);
        return item != null;
    }
}
