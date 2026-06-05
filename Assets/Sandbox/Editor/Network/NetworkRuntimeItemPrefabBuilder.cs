#if UNITY_EDITOR
using PeakPlunder.EditorTools;
using System.IO;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 担架・ウインチの NGO Prefab を生成し DefaultNetworkPrefabs / Resources に登録する。
/// メニュー: Peak Plunder → Network → Build Runtime Item Prefabs
/// </summary>
public static class NetworkRuntimeItemPrefabBuilder
{
    private const string ResourcesDir = "Assets/Resources/NetworkItems";
    private const string StretcherPrefabPath = ResourcesDir + "/StretcherNetworkItem.prefab";
    private const string WinchPrefabPath     = ResourcesDir + "/PortableWinchNetworkItem.prefab";
    private const string DefaultPrefabsPath  = "Assets/DefaultNetworkPrefabs.asset";

    [MenuItem(PeakPlunderEditorMenus.Network.BuildRuntimeItemPrefabs)]
    public static void BuildAll()
    {
        Directory.CreateDirectory(ResourcesDir);

        var stretcher = BuildStretcherPrefab();
        var winch     = BuildWinchPrefab();

        RegisterInDefaultNetworkPrefabs(stretcher);
        RegisterInDefaultNetworkPrefabs(winch);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[NetworkRuntimeItemPrefabBuilder] 担架・ウインチ Prefab を生成し DefaultNetworkPrefabs に登録しました。");
    }

    private static GameObject BuildStretcherPrefab()
    {
        var temp = ItemRuntimeFactory.CreateBaseObject(ShopItemType.Stretcher);
        if (temp == null)
        {
            Debug.LogError("[NetworkRuntimeItemPrefabBuilder] Stretcher 生成失敗");
            return null;
        }

        temp.name = "StretcherNetworkItem";
        EnsureStretcherStack(temp);

        var prefab = SavePrefab(temp, StretcherPrefabPath);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    private static GameObject BuildWinchPrefab()
    {
        var temp = ItemRuntimeFactory.CreateBaseObject(ShopItemType.PortableWinch);
        if (temp == null)
        {
            Debug.LogError("[NetworkRuntimeItemPrefabBuilder] Winch 生成失敗");
            return null;
        }

        temp.name = "PortableWinchNetworkItem";
        EnsureWinchStack(temp);

        var prefab = SavePrefab(temp, WinchPrefabPath);
        Object.DestroyImmediate(temp);
        return prefab;
    }

    private static void EnsureStretcherStack(GameObject go)
    {
        if (go.GetComponent<StretcherItem>() == null)
            go.AddComponent<StretcherItem>();
        if (go.GetComponent<NetworkObject>() == null)
            go.AddComponent<NetworkObject>();
        if (go.GetComponent<NetworkTransform>() == null)
            go.AddComponent<NetworkTransform>();
        if (go.GetComponent<NetworkRigidbody>() == null)
            go.AddComponent<NetworkRigidbody>();
        if (go.GetComponent<NetworkStretcherSync>() == null)
            go.AddComponent<NetworkStretcherSync>();
    }

    private static void EnsureWinchStack(GameObject go)
    {
        if (go.GetComponent<LineRenderer>() == null)
            go.AddComponent<LineRenderer>();
        if (go.GetComponent<WinchCableChain>() == null)
            go.AddComponent<WinchCableChain>();
        if (go.GetComponent<PortableWinchItem>() == null)
            go.AddComponent<PortableWinchItem>();
        if (go.GetComponent<NetworkObject>() == null)
            go.AddComponent<NetworkObject>();
        if (go.GetComponent<NetworkTransform>() == null)
            go.AddComponent<NetworkTransform>();
        if (go.GetComponent<NetworkPortableWinchSync>() == null)
            go.AddComponent<NetworkPortableWinchSync>();
    }

    private static GameObject SavePrefab(GameObject temp, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null)
            return PrefabUtility.SaveAsPrefabAsset(temp, path);

        return PrefabUtility.SaveAsPrefabAsset(temp, path);
    }

    private static void RegisterInDefaultNetworkPrefabs(GameObject prefab)
    {
        if (prefab == null) return;

        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(DefaultPrefabsPath);
        if (list == null)
        {
            Debug.LogWarning($"[NetworkRuntimeItemPrefabBuilder] {DefaultPrefabsPath} が見つかりません。");
            return;
        }

        if (list.Contains(prefab)) return;

        list.Add(new NetworkPrefab { Prefab = prefab });
        EditorUtility.SetDirty(list);
    }
}
#endif
