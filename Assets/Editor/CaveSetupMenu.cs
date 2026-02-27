using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public class CaveSetupMenu : EditorWindow
{
    [MenuItem("Tools/Setup TestScene Cave Material")]
    public static void Run()
    {
        // 1. Create or Load Material
        string matPath = "Assets/Materials/CaveRock.mat";
        
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        Material caveMat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (caveMat == null)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("URP Lit shader not found. Ensure Universal Render Pipeline is installed.");
                return;
            }
            caveMat = new Material(urpLit);
            
            // Base Map (Albedo) カラー: (0.25, 0.22, 0.2, 1.0)
            caveMat.SetColor("_BaseColor", new Color(0.25f, 0.22f, 0.2f, 1f));
            // Smoothness: 0.15
            caveMat.SetFloat("_Smoothness", 0.15f);
            // Metallic: 0.0
            caveMat.SetFloat("_Metallic", 0.0f);
            
            AssetDatabase.CreateAsset(caveMat, matPath);
        }
        else
        {
            caveMat.SetColor("_BaseColor", new Color(0.25f, 0.22f, 0.2f, 1f));
            caveMat.SetFloat("_Smoothness", 0.15f);
            caveMat.SetFloat("_Metallic", 0.0f);
            EditorUtility.SetDirty(caveMat);
        }
        
        AssetDatabase.SaveAssets();

        // 2. Open TestScene if not open
        Scene testScene = SceneManager.GetSceneByName("TestScene");
        if (!testScene.isLoaded)
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene("Assets/Scenes/TestScene.unity", OpenSceneMode.Single);
            }
            else
            {
                return; // User cancelled save
            }
        }

        // 3. Find CaveGenerator
        var generators = Object.FindObjectsByType<CaveGenerator>(FindObjectsSortMode.None);
        if (generators.Length == 0)
        {
            Debug.LogError("No CaveGenerator found in TestScene!");
            return;
        }

        foreach (var gen in generators)
        {
            gen.GetType().GetField("caveMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
               ?.SetValue(gen, caveMat);
            
            EditorUtility.SetDirty(gen);
            Debug.Log($"Successfully assigned CaveRock material to CaveGenerator on {gen.gameObject.name}");
        }

        // 4. Save Scene
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log("TestScene Setup Complete & Saved. Prefix references are intentionally skipped.");
    }
}
