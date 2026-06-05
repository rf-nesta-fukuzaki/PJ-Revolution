#if UNITY_EDITOR
using PeakPlunder.EditorTools;
using UnityEditor;
using UnityEngine;

/// <summary>
/// PlayerPrefab と Gameplay シーンへアイテム系コンポーネントを配線する。
/// </summary>
public static class ItemGameplayWireup
{
    private const string PlayerPrefabPath = "Assets/Sandbox/Prefabs/PlayerPrefab.prefab";

    [MenuItem(PeakPlunderEditorMenus.Items.WireItemGameplaySystems)]
    public static void WireAll()
    {
        WirePlayerPrefab();
        Debug.Log("[ItemGameplayWireup] PlayerPrefab へ ItemUseController / ItemHandController を追加しました。");
    }

    [MenuItem(PeakPlunderEditorMenus.Items.WirePlayerPrefabOnly)]
    public static void WirePlayerPrefab()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            EnsureComponent<ItemUseController>(root);
            EnsureComponent<ItemHandController>(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
    }

    private static void EnsureComponent<T>(GameObject root) where T : Component
    {
        if (root.GetComponent<T>() == null)
            root.AddComponent<T>();
    }
}
#endif
