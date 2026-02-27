using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class PrefabApplyHelper
{
    [MenuItem("Tools/Apply PlayerPrefab Setup")]
public static void ApplyPlayerPrefabSetup()
    {
        ApplyAndCleanup("PlayerPrefab_Setup");
    }
    
    [MenuItem("Tools/Apply BatPrefab Setup")]
    public static void ApplyBatPrefabSetup()
    {
        // BatPrefab is now linked, just apply overrides
        var go = GameObject.Find("BatPrefab");
        if (go == null) { Debug.LogError("BatPrefab not found"); return; }
        var root = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        if (root == null) { Debug.LogError("Not a prefab instance"); return; }
        PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
        Debug.Log($"[PrefabApplyHelper] BatPrefab overrides applied.");
    }
    
    private static void ApplyAndCleanup(string goName)
    {
        var go = GameObject.Find(goName);
        if (go == null) { Debug.LogError($"[PrefabApplyHelper] {goName} not found in scene."); return; }
        var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(go);
        if (prefabRoot == null) { Debug.LogError("[PrefabApplyHelper] Not a prefab instance."); return; }
        string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(prefabRoot);
        PrefabUtility.ApplyPrefabInstance(prefabRoot, InteractionMode.AutomatedAction);
        Debug.Log($"[PrefabApplyHelper] Applied prefab overrides to {assetPath}");
        Object.DestroyImmediate(prefabRoot);
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[PrefabApplyHelper] Destroyed temp instance and saved.");
    }
}
