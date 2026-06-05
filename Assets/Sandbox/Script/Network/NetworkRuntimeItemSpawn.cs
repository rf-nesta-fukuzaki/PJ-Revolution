using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 実行時生成アイテムの NGO スポーン（担架など NetworkStretcherSync 対象）。
/// </summary>
public static class NetworkRuntimeItemSpawn
{
    public static GameObject SpawnWorldItem(ShopItemType itemType, Vector3 position, Quaternion rotation)
    {
        var go = ItemRuntimeFactory.CreateWorldItem(itemType, position, rotation);
        TrySpawnOnServer(go, itemType);
        return go;
    }

    public static GameObject SpawnFieldDrop(ShopItemType itemType, Vector3 position, Quaternion rotation)
    {
        var go = ItemRuntimeFactory.CreateFieldDrop(itemType, position, rotation);
        TrySpawnOnServer(go, itemType);
        return go;
    }

    private static void TrySpawnOnServer(GameObject go, ShopItemType itemType)
    {
        if (go == null) return;
        if (itemType != ShopItemType.Stretcher && itemType != ShopItemType.PortableWinch) return;

        if (!NetworkRuntimeItemPrefabs.HasRegisteredPrefab(itemType))
        {
            Debug.LogWarning(
                $"[NetworkRuntimeItemSpawn] {itemType} の Network Prefab 未登録 — " +
                "Late Join 非対応。Peak Plunder → Network → Build Runtime Item Prefabs を実行してください。");
        }

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null) return;

        if (!netObj.IsSpawned)
            netObj.Spawn(destroyWithScene: true);
    }
}
