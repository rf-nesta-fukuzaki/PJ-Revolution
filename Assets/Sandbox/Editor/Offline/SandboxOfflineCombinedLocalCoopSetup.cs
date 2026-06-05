#if UNITY_EDITOR
using PeakPlunder.EditorTools;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SandboxOfflineCombined.unity にローカル Co-op ブートストラップを追加する。
/// </summary>
public static class SandboxOfflineCombinedLocalCoopSetup
{
    private const string CombinedScenePath = "Assets/Sandbox/Scenes/SandboxOfflineCombined.unity";
    private const string ExplorerFbxPath = "Assets/Sandbox/Art/Models/Explorer.fbx";
    private const string AnimatorPath = "Assets/Sandbox/Art/Animation/Explorer/ExplorerAnimator.controller";
    private const string PlayerPrefabPath = "Assets/Sandbox/Prefabs/PlayerPrefab.prefab";

    [MenuItem(PeakPlunderEditorMenus.Offline.Combined.SetupLocalCoop)]
    public static void SetupCombinedScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(CombinedScenePath, OpenSceneMode.Single);
        bool changed = false;

        var systems = GameObject.Find("GameSystems");
        if (systems == null)
        {
            systems = new GameObject("GameSystems");
            changed = true;
        }

        var bootstrap = systems.GetComponent<SandboxLocalCoopBootstrap>();
        if (bootstrap == null)
        {
            bootstrap = systems.AddComponent<SandboxLocalCoopBootstrap>();
            changed = true;
        }

        var so = new SerializedObject(bootstrap);
        SetObjectRef(so, "_explorerModelPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(ExplorerFbxPath));
        SetObjectRef(so, "_animatorController", AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimatorPath));
        SetObjectRef(so, "_playerPrefabOverride", AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath));
        SetInt(so, "_humanPlayerCount", 1);
        SetBool(so, "_autoDetectGamepads", true);
        so.ApplyModifiedPropertiesWithoutUndo();

        var legacyNpc = systems.transform.Find("OfflineNPCManager");
        if (legacyNpc != null)
        {
            var npcSpawner = legacyNpc.GetComponent<OfflineNPCSpawner>();
            if (npcSpawner != null)
            {
                var npcSo = new SerializedObject(npcSpawner);
                SetInt(npcSo, "_npcCount", 0);
                npcSo.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
        }

        EnsureSpawnPoints(systems.transform);

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[LocalCoopSetup] SandboxOfflineCombined にローカル Co-op を設定しました。");
        }
        else
        {
            Debug.Log("[LocalCoopSetup] SandboxOfflineCombined は既にローカル Co-op 対応済みです。");
        }
    }

    private static void EnsureSpawnPoints(Transform systemsRoot)
    {
        var spawnerGo = GameObject.Find("NetworkPlayerSpawner");
        if (spawnerGo == null) return;

        var spawner = spawnerGo.GetComponent<NetworkPlayerSpawner>();
        if (spawner == null) return;

        var so = new SerializedObject(spawner);
        var pointsProp = so.FindProperty("_spawnPoints");
        if (pointsProp == null) return;

        if (pointsProp.arraySize >= 4) return;

        var spawnerTransform = spawnerGo.transform;
        pointsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var element = pointsProp.GetArrayElementAtIndex(i);
            if (element.objectReferenceValue != null) continue;

            var pointGo = new GameObject($"SpawnPoint_{i}");
            pointGo.transform.SetParent(spawnerTransform, false);
            pointGo.transform.localPosition = new Vector3(i * 2.5f, 2f, 0f);
            element.objectReferenceValue = pointGo.transform;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectRef(SerializedObject so, string prop, Object value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.objectReferenceValue = value;
    }

    private static void SetInt(SerializedObject so, string prop, int value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.intValue = value;
    }

    private static void SetBool(SerializedObject so, string prop, bool value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.boolValue = value;
    }
}
#endif
