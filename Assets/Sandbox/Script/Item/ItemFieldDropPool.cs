using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §8.6 — 山中遺留品プール（登攀系・サバイバル系8種）。
/// </summary>
public static class ItemFieldDropPool
{
    private static readonly ShopItemType[] s_pool =
    {
        ShopItemType.ShortRope10m,
        ShopItemType.LongRope25m,
        ShopItemType.IceAxe,
        ShopItemType.AnchorBolt,
        ShopItemType.GrapplingHook,
        ShopItemType.FoodPack,
        ShopItemType.FlareGun,
        ShopItemType.EmergencyRadio,
    };

    public static IReadOnlyList<ShopItemType> Types => s_pool;

    public static ShopItemType PickRandom() => s_pool[Random.Range(0, s_pool.Length)];

    public static GameObject[] BuildRuntimePrefabs()
    {
        var prefabs = new GameObject[s_pool.Length];
        for (int i = 0; i < s_pool.Length; i++)
        {
            var template = ItemRuntimeFactory.CreateBaseObject(s_pool[i]);
            if (template != null)
            {
                template.SetActive(false);
                template.name = $"FieldDrop_{s_pool[i]}";
            }
            prefabs[i] = template;
        }
        return prefabs;
    }
}
