#if UNITY_EDITOR
using PeakPlunder.EditorTools;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// GDD §8.3 — 15種の ItemDefinitionSO アセットを生成する。
/// </summary>
public static class ItemDefinitionBootstrap
{
    private const string OutputDir = "Assets/Sandbox/Data/ItemData";

    [MenuItem(PeakPlunderEditorMenus.Items.GenerateItemDefinitions)]
    public static void GenerateAll()
    {
        Directory.CreateDirectory(OutputDir);

        Create("ItemDef_ShortRope", "ショートロープ", 5, 1f, 1, 80f);
        Create("ItemDef_LongRope", "ロングロープ", 10, 2f, 2, 70f);
        Create("ItemDef_IceAxe", "アイスアックス", 8, 1f, 1, 60f);
        Create("ItemDef_AnchorBolt", "アンカーボルト", 6, 1f, 1, 100f);
        Create("ItemDef_GrapplingHook", "グラップリングフック", 12, 2f, 1, 50f);
        Create("ItemDef_Stretcher", "折りたたみ担架", 10, 3f, 3, 70f);
        Create("ItemDef_PackingKit", "梱包キット", 8, 2f, 1, 100f);
        Create("ItemDef_ThermalCase", "サーマルケース", 4, 1f, 1, 90f);
        Create("ItemDef_SecureBelt", "固定ベルト", 6, 1f, 1, 100f);
        Create("ItemDef_Food", "食料", 3, 1f, 1, 100f);
        Create("ItemDef_FlareGun", "フレアガン", 5, 1f, 1, 100f);
        Create("ItemDef_EmergencyRadio", "緊急無線機", 7, 1f, 1, 40f);
        Create("ItemDef_PortableWinch", "ポータブルウインチ", 20, 3f, 2, 50f);
        Create("ItemDef_BivouacTent", "ビバークテント", 15, 2f, 2, 80f);
        Create("ItemDef_OxygenTank", "酸素タンク", 12, 2f, 1, 60f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[ItemDefinitionBootstrap] 15 ItemDefinitionSO assets created.");
    }

    private static void Create(string fileName, string itemName, int cost, float weight, int slots, float durability)
    {
        string path = $"{OutputDir}/{fileName}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<ItemDefinitionSO>(path);
        if (existing != null) return;

        var asset = ScriptableObject.CreateInstance<ItemDefinitionSO>();
        asset.ItemName    = itemName;
        asset.Cost        = cost;
        asset.Weight      = weight;
        asset.Slots       = slots;
        asset.MaxDurability = durability;

        AssetDatabase.CreateAsset(asset, path);
    }
}
#endif
