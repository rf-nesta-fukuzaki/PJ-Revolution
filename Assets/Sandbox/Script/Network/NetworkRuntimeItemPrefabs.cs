using UnityEngine;

/// <summary>
/// Late Join 対応のため DefaultNetworkPrefabs に登録済みの実行時アイテム Prefab を解決する。
/// Prefab 未生成時は null（<see cref="ItemRuntimeFactory"/> がプロシージャル生成にフォールバック）。
/// </summary>
public static class NetworkRuntimeItemPrefabs
{
    private const string StretcherResourcePath = "NetworkItems/StretcherNetworkItem";
    private const string WinchResourcePath     = "NetworkItems/PortableWinchNetworkItem";

    private static GameObject _stretcherPrefab;
    private static GameObject _winchPrefab;

    public static GameObject GetPrefab(ShopItemType itemType) => itemType switch
    {
        ShopItemType.Stretcher     => _stretcherPrefab ??= Resources.Load<GameObject>(StretcherResourcePath),
        ShopItemType.PortableWinch => _winchPrefab     ??= Resources.Load<GameObject>(WinchResourcePath),
        _                          => null,
    };

    public static bool HasRegisteredPrefab(ShopItemType itemType) => GetPrefab(itemType) != null;
}
