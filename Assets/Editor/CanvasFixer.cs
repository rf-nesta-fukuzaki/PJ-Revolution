using UnityEngine;
using TMPro;
using UnityEditor;

public class CanvasFixer : MonoBehaviour
{
    [MenuItem("Tools/Fix Canvas Properties")]
    public static void Fix()
    {
        // 1. TitleCanvas 以外を false に
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

        // 2. QuitButtonの修正
        GameObject quitBtn = GameObject.Find("QuitButton");
        if (quitBtn != null && quitBtn.GetComponent<UnityEngine.UI.Button>() != null)
        {
            // すでに存在するかチェック
            Transform existingText = quitBtn.transform.Find("Text (TMP)");
            if (existingText != null)
            {
                Undo.DestroyObjectImmediate(existingText.gameObject);
            }

            // 新規作成
            GameObject textObj = new GameObject("Text (TMP)");
            Undo.RegisterCreatedObjectUndo(textObj, "Create QuitButton Text");
            
            textObj.transform.SetParent(quitBtn.transform, false);
            
            // RectTransform
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            
            // TMP_Text
            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "ゲーム終了";
            tmp.fontSize = 28;
            tmp.alignment = TextAlignmentOptions.CenterGeoAligned;
            tmp.color = Color.white;
            
            // Font setup
            string[] guids = AssetDatabase.FindAssets("NotoSansJP-VariableFont_wght SDF t:TMP_FontAsset");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (font != null)
                {
                    tmp.font = font;
                }
            }
            else
            {
                Debug.LogWarning("NotoSansJP-VariableFont_wght SDF font not found.");
            }
            
            Debug.Log("QuitButton Text fixed successfully.");
        }
        else
        {
            Debug.LogError("QuitButton not found.");
        }

        // Scene変更のマーク
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
