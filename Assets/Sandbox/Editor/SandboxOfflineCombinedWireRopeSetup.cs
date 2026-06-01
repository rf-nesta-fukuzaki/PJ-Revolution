#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sandbox.World;
using Sandbox.World.Integration;

/// <summary>
/// SandboxOfflineCombined.unity に R キー ワイヤーロープ（WireRopeActionController + 力ゲージ HUD）を
/// 組み込むためのエディタ補助。Unity MCP 未使用時はメニューから手動実行する。
/// </summary>
public static class SandboxOfflineCombinedWireRopeSetup
{
    private const string CombinedScenePath = "Assets/Sandbox/Scenes/SandboxOfflineCombined.unity";
    private const string PlayerPrefabPath = "Assets/Sandbox/Prefabs/PlayerPrefab.prefab";

    [MenuItem("ccc/Wire Rope/Setup SandboxOfflineCombined Scene")]
    public static void SetupCombinedScene()
    {
        if (!System.IO.File.Exists(CombinedScenePath))
        {
            Debug.LogError($"[WireRopeSetup] シーンが見つかりません: {CombinedScenePath}");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(CombinedScenePath, OpenSceneMode.Single);
        bool changed = false;

        changed |= EnsureCombinedConformer();
        changed |= EnsurePlayerPrefabWireRope();

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[WireRopeSetup] SandboxOfflineCombined を更新しました。");
        }
        else
        {
            Debug.Log("[WireRopeSetup] SandboxOfflineCombined は既にワイヤーロープ対応済みです。");
        }
    }

    [MenuItem("ccc/Wire Rope/Verify SandboxOfflineCombined Scene")]
    public static void VerifyCombinedScene()
    {
        if (!System.IO.File.Exists(CombinedScenePath))
        {
            Debug.LogError($"[WireRopeSetup] シーンが見つかりません: {CombinedScenePath}");
            return;
        }

        var scene = EditorSceneManager.OpenScene(CombinedScenePath, OpenSceneMode.Single);
        var conformer = Object.FindFirstObjectByType<CombinedTerrainConformer>();
        var terrain = Object.FindFirstObjectByType<TerrainGenerator>();
        Debug.Log(
            $"[WireRopeSetup] Verify '{scene.name}':\n" +
            $"  CombinedTerrainConformer = {(conformer != null ? "OK" : "MISSING")}\n" +
            $"  TerrainGenerator = {(terrain != null ? "OK" : "MISSING")}\n" +
            $"  (HudManager は実行時に Conformer が生成 / Player の WireRope は Prefab または Awake で付与)");
    }

    private static bool EnsureCombinedConformer()
    {
        var terrain = Object.FindFirstObjectByType<TerrainGenerator>();
        if (terrain == null)
        {
            Debug.LogError("[WireRopeSetup] TerrainGenerator が見つかりません。");
            return false;
        }

        if (terrain.GetComponent<CombinedTerrainConformer>() != null) return false;

        terrain.gameObject.AddComponent<CombinedTerrainConformer>();
        Debug.Log("[WireRopeSetup] TerrainGenerator に CombinedTerrainConformer を追加しました。");
        return true;
    }

    private static bool EnsurePlayerPrefabWireRope()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null) return false;

        bool changed = false;
        try
        {
            if (root.GetComponent<WireRopeActionController>() == null)
            {
                root.AddComponent<WireRopeActionController>();
                changed = true;
                Debug.Log("[WireRopeSetup] PlayerPrefab に WireRopeActionController を追加しました。");
            }

            if (changed)
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        return changed;
    }
}
#endif
