#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CaveFogSetup
{
    [MenuItem("Tools/Cave/Apply Fog and Lighting Settings")]
    public static void ApplySettings()
    {
        // Fog
        RenderSettings.fog         = true;
        RenderSettings.fogColor    = new Color(0.02f, 0.02f, 0.03f);
        RenderSettings.fogMode     = FogMode.ExponentialSquared;
        RenderSettings.fogDensity  = 0.015f;

        // Ambient lighting
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.01f, 0.01f, 0.02f);

        EditorUtility.SetDirty(RenderSettings.skybox ? (Object)RenderSettings.skybox : Camera.main);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());

        Debug.Log("[CaveFogSetup] Fog and ambient lighting applied.");
    }
}
#endif
