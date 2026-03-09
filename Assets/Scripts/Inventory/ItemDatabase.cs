using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全アイテム定義を保持するデータベース（ScriptableObject）。
/// Assets/Data/ 以下に1つだけ作成し、ResourceItem などから参照する。
/// </summary>
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "PJ-Revolution/ItemDatabase")]
public class ItemDatabase : ScriptableObject
{
    [Tooltip("登録済み全アイテム一覧")]
    public List<InventoryItem> AllItems = new();

    /// <summary>名前でアイテムを検索する。見つからなければ null を返す。</summary>
    public InventoryItem GetByName(string name)
        => AllItems.Find(i => i.ItemName == name);
}
