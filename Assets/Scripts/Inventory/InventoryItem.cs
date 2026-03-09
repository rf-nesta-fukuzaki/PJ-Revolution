using UnityEngine;

/// <summary>アイテムのカテゴリ分類</summary>
public enum ItemCategory
{
    Consumable,
    Tool,
    Material,
    Key,
}

/// <summary>
/// インベントリアイテムの定義データ（ScriptableObject）。
/// Assets/Data/Items/ 以下に .asset を作成して使用する。
/// </summary>
[CreateAssetMenu(fileName = "NewItem", menuName = "PJ-Revolution/InventoryItem")]
public class InventoryItem : ScriptableObject
{
    [Tooltip("アイテムの表示名")]
    public string ItemName;

    [Tooltip("アイテムの説明文")]
    [TextArea(2, 4)]
    public string Description;

    [Tooltip("UI に表示するアイコン（null 許容）")]
    public Sprite Icon;

    [Tooltip("アイテム 1 個あたりの重量 (kg)")]
    public float Weight = 1f;

    [Tooltip("1 スロットに積めるスタック上限")]
    public int MaxStack = 1;

    [Tooltip("アイテムのカテゴリ")]
    public ItemCategory Category;

    [Tooltip("使用時に消費されるか")]
    public bool IsConsumable;

    [Tooltip("消費アイテムの効果種別（IsConsumable == true のときのみ参照）")]
    public ResourceItemType ConsumableEffect;
}
