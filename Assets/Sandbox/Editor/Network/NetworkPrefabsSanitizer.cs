#if UNITY_EDITOR
using PeakPlunder.EditorTools;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// DefaultNetworkPrefabs から欠落 GUID を除去し、NetworkManager に PlayerPrefab を配線する。
/// </summary>
public static class NetworkPrefabsSanitizer
{
    private const string PlayerPrefabPath = "Assets/Sandbox/Prefabs/PlayerPrefab.prefab";
    private const string DefaultListPath = "Assets/DefaultNetworkPrefabs.asset";
    private const string PeakListPath = "Assets/Sandbox/Prefabs/PeakPlunderNetworkPrefabs.asset";

    private static readonly string[] ScenePaths =
    {
        "Assets/Sandbox/Scenes/SandboxOfflineCombined.unity",
        "Assets/Sandbox/Scenes/OfflineTestScene.unity",
        "Assets/Sandbox/Scenes/Mountain01.unity",
        "Assets/Sandbox/Scenes/MainMenu.unity",
    };

    [MenuItem(PeakPlunderEditorMenus.Network.SanitizeNetworkPrefabs)]
    public static void SanitizeAll()
    {
        int removed = SanitizePrefabList(DefaultListPath);
        removed += SanitizePrefabList(PeakListPath);

        int wired = WireNetworkManagersInScenes();

        AssetDatabase.SaveAssets();
        Debug.Log($"[NetworkPrefabsSanitizer] 無効 Prefab {removed} 件除去、NetworkManager {wired} 件を修復しました。");
    }

    private static int SanitizePrefabList(string assetPath)
    {
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(assetPath);
        if (list == null)
        {
            Debug.LogWarning($"[NetworkPrefabsSanitizer] リストが見つかりません: {assetPath}");
            return 0;
        }

        var so = new SerializedObject(list);
        var listProp = so.FindProperty("List");
        if (listProp == null || !listProp.isArray) return 0;

        int removed = 0;
        for (int i = listProp.arraySize - 1; i >= 0; i--)
        {
            var prefabProp = listProp.GetArrayElementAtIndex(i).FindPropertyRelative("Prefab");
            if (prefabProp == null || prefabProp.objectReferenceValue == null)
            {
                listProp.DeleteArrayElementAtIndex(i);
                removed++;
            }
        }

        if (removed > 0)
            so.ApplyModifiedPropertiesWithoutUndo();

        return removed;
    }

    private static int WireNetworkManagersInScenes()
    {
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        if (playerPrefab == null)
        {
            Debug.LogError($"[NetworkPrefabsSanitizer] PlayerPrefab が見つかりません: {PlayerPrefabPath}");
            return 0;
        }

        var activeScenePath = SceneManager.GetActiveScene().path;
        int wired = 0;

        foreach (var scenePath in ScenePaths)
        {
            if (!System.IO.File.Exists(scenePath)) continue;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            bool changed = false;

            foreach (var nm in Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None))
            {
                var so = new SerializedObject(nm);
                var playerProp = so.FindProperty("NetworkConfig.PlayerPrefab");
                if (playerProp != null && playerProp.objectReferenceValue == null)
                {
                    playerProp.objectReferenceValue = playerPrefab;
                    changed = true;
                }

                so.ApplyModifiedPropertiesWithoutUndo();

                if (nm.GetComponent<NetworkManagerConfigGuard>() == null)
                {
                    nm.gameObject.AddComponent<NetworkManagerConfigGuard>();
                    changed = true;
                }

                if (changed) wired++;
            }

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        if (!string.IsNullOrEmpty(activeScenePath))
            EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);

        return wired;
    }
}
#endif
