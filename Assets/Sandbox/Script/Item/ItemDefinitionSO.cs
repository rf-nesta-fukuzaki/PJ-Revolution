using UnityEngine;

/// <summary>
/// データ駆動型のアイテムパラメータ定義。
/// ItemBase のハードコードされた Awake フィールド代入を外出しにすることで
/// プレハブを触らずにバランス調整できる。
/// </summary>
[CreateAssetMenu(fileName = "ItemDef_NewItem", menuName = "PeakIdiots/Item Definition")]
public class ItemDefinitionSO : ScriptableObject
{
    [Header("識別")]
    public string ItemName    = "Unknown Item";

    [Header("ショップ")]
    public int    Cost        = 5;
    public string Description = "";

    [Header("物理・スロット")]
    public float  Weight      = 1f;
    public int    Slots       = 1;

    [Header("耐久")]
    [Range(1f, 500f)]
    public float  MaxDurability      = 100f;
    public float  ImpactDamageScale  = 1f;
    public float  ImpactDamageThreshold = 3f;
}
