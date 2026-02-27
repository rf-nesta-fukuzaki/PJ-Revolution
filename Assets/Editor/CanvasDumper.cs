using UnityEngine;
using TMPro;
using System.Text;
using System.Linq;
using UnityEditor;

public class CanvasDumper : MonoBehaviour
{
    [MenuItem("Tools/Dump All Canvases")]
    public static void Dump()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("=== CANVAS DUMP START ===");
        
        // Find all canvases in the active scene (including inactive ones)
        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        
        foreach (Canvas canvas in canvases)
        {
            // Filter out prefabs, we only want scene objects
            if (canvas.gameObject.scene.name == null) continue;
            
            sb.AppendLine($"\nCanvas Name: {canvas.name}");
            sb.AppendLine($"Active: {canvas.gameObject.activeSelf}");
            sb.AppendLine($"Sort Order: {canvas.sortingOrder}");
            
            // Find all TMP_Text components in children (including inactive ones)
            TMP_Text[] texts = canvas.GetComponentsInChildren<TMP_Text>(true);
            if (texts.Length > 0)
            {
                sb.AppendLine("TMP_Text Contents:");
                foreach (TMP_Text t in texts)
                {
                    sb.AppendLine($"  - {t.gameObject.name}: \"{t.text.Replace("\n", "\\n")}\"");
                }
            }
            else
            {
                sb.AppendLine("TMP_Text Contents: None");
            }
        }
        
        sb.AppendLine("=== CANVAS DUMP END ===");
        
        System.IO.File.WriteAllText("Assets/Editor/AllCanvasDump.txt", sb.ToString());
        Debug.Log("Dump saved to Assets/Editor/AllCanvasDump.txt");
    }
}
