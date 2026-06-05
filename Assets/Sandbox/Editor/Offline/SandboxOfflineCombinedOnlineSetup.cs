#if UNITY_EDITOR
using PeakPlunder.EditorTools;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// SandboxOfflineCombined にオンラインパーティ用コンポーネントを追加する。
/// </summary>
public static class SandboxOfflineCombinedOnlineSetup
{
    private const string CombinedScenePath = "Assets/Sandbox/Scenes/SandboxOfflineCombined.unity";

    [MenuItem(PeakPlunderEditorMenus.Offline.Combined.SetupOnline)]
    public static void SetupCombinedScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(CombinedScenePath, OpenSceneMode.Single);
        var nm = Object.FindFirstObjectByType<NetworkManager>();
        if (nm == null)
        {
            Debug.LogError("[OnlineSetup] NetworkManager が見つかりません。");
            return;
        }

        bool changed = false;
        if (nm.GetComponent<NetworkPartyManager>() == null)
        {
            nm.gameObject.AddComponent<NetworkPartyManager>();
            changed = true;
        }

        if (nm.GetComponent<SandboxOnlineSessionBootstrap>() == null)
        {
            var session = nm.gameObject.AddComponent<SandboxOnlineSessionBootstrap>();
            var so = new SerializedObject(session);
            SetBool(so, "_autoStartOfflineLocal", true);
            SetBool(so, "_showSessionGui", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            changed = true;
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[OnlineSetup] SandboxOfflineCombined をオンライン対応しました。");
        }
        else
        {
            Debug.Log("[OnlineSetup] 既にオンライン対応済みです。");
        }
    }

    private static void SetBool(SerializedObject so, string prop, bool value)
    {
        var p = so.FindProperty(prop);
        if (p != null) p.boolValue = value;
    }
}
#endif
