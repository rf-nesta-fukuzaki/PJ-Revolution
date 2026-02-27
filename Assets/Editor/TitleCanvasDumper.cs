using UnityEngine;
using TMPro;
using System.Text;
using UnityEditor;

public class TitleCanvasDumper
{
    [MenuItem("Tools/Dump Title Canvas")]
public static void Dump()
{
    var canvas = GameObject.Find("Canvas - Title");
    if (canvas == null) return;
    System.Text.StringBuilder sb = new System.Text.StringBuilder();
    DumpRecursive(canvas.transform, sb, 0);
    System.IO.File.WriteAllText("Assets/Editor/TitleCanvasDump.txt", sb.ToString());
    Debug.Log("Dump saved to Assets/Editor/TitleCanvasDump.txt");
}

    private static void DumpRecursive(Transform t, StringBuilder sb, int depth)
    {
        string indent = new string(' ', depth * 2);
        string activeState = t.gameObject.activeSelf ? "Active" : "Inactive";
        string pos = t.localPosition.ToString();
        
        var txt = t.GetComponent<TMP_Text>();
        string txtInfo = txt != null ? $" [TMP_Text: {txt.text.Replace("\n","\\n")}]" : "";
        
        sb.AppendLine($"{indent}- {t.name} (InstanceID: {t.gameObject.GetInstanceID()}) | Pos: {pos} | State: {activeState}{txtInfo}");
        
        foreach (Transform child in t)
        {
            DumpRecursive(child, sb, depth + 1);
        }
    }
}
