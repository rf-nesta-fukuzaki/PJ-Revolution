using UnityEngine;
using TMPro;
using UnityEditor;

public class CanvasFixer : MonoBehaviour
{
    [MenuItem("Tools/Fix Canvas Properties")]
    public static void Fix()
    {
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            if (canvas.gameObject.scene.name == null) continue;
            
            if (canvas.name == "Canvas - Title")
            {
                canvas.gameObject.SetActive(true);
            }
            else
            {
                canvas.gameObject.SetActive(false);
            }
        }

        GameObject quitBtn = GameObject.Find("QuitButton");
        if (quitBtn == null)
        {
            quitBtn = GameObject.Find("Btn_Exit");
        }

        if (quitBtn != null && quitBtn.GetComponent<UnityEngine.UI.Button>() != null)
        {
            Transform existingText = quitBtn.transform.Find("Text (TMP)");
            if (existingText != null)
            {
                Undo.DestroyObjectImmediate(existingText.gameObject);
            }

            GameObject textObj = new GameObject("Text (TMP)");
            Undo.RegisterCreatedObjectUndo(textObj, "Create QuitButton Text");
            
            textObj.transform.SetParent(quitBtn.transform, false);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "ゲーム終了";
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.CenterGeoAligned;
            tmp.color = Color.white;
            
            TMP_FontAsset font = FindPreferredTitleFont();
            if (font != null)
            {
                tmp.font = font;
                if (font.material != null)
                {
                    tmp.fontSharedMaterial = font.material;
                }
            }
            else
            {
                Debug.LogWarning("No TMP font asset for title UI was found.");
            }
            
            Debug.Log("QuitButton Text fixed successfully.");
        }
        else
        {
            Debug.LogWarning("Exit button was not found. Skipped TMP text rebuild.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }

    static TMP_FontAsset FindPreferredTitleFont()
    {
        string[] searchQueries =
        {
            "NotoSansJP_Rebuilt t:TMP_FontAsset",
            "NotoSansJP t:TMP_FontAsset",
            "TitleRef_RoundedBold_Fixed t:TMP_FontAsset"
        };

        foreach (string query in searchQueries)
        {
            string[] guids = AssetDatabase.FindAssets(query);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                {
                    return font;
                }
            }
        }

        return TMP_Settings.defaultFontAsset;
    }
}
