using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全コスメアイテムの定義を保持する ScriptableObject マスターデータ。
/// Assets/Data/CosmeticDatabase.asset として配置する。
///
/// [セットアップ手順]
///   1. Project ウィンドウ右クリック → Create → P-REVO/CosmeticDatabase
///   2. Inspector で各カテゴリのアイテムを定義する
///   3. デフォルトアイテム（isDefault = true）を各カテゴリ 1 つ以上設定する
///   4. CosmeticShopUI の Inspector に割り当てる
/// </summary>
[CreateAssetMenu(fileName = "CosmeticDatabase", menuName = "P-REVO/CosmeticDatabase")]
public class CosmeticDatabase : ScriptableObject
{
    // ─────────────── Inspector ───────────────

    [Header("コスメアイテム一覧")]
    [Tooltip("全コスメアイテムの定義リスト。各カテゴリにデフォルトアイテムを 1 つ以上含めること")]
    [SerializeField] private List<CosmeticItemData> _items = new();

    // ─────────────── 公開 API ───────────────

    /// <summary>全アイテムの読み取り専用リスト。</summary>
    public IReadOnlyList<CosmeticItemData> Items => _items;

    /// <summary>
    /// ID でアイテムを検索する。見つからない場合は null を返す。
    /// </summary>
    public CosmeticItemData FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var item in _items)
            if (item.Id == id) return item;
        return null;
    }

    /// <summary>
    /// 指定カテゴリのアイテムを全件返す。
    /// </summary>
    public List<CosmeticItemData> GetByCategory(CosmeticCategory category)
    {
        var result = new List<CosmeticItemData>();
        foreach (var item in _items)
            if (item.Category == category) result.Add(item);
        return result;
    }

    /// <summary>
    /// 指定カテゴリのデフォルトアイテム ID を返す。
    /// デフォルトが存在しない場合は空文字を返す。
    /// </summary>
    public string GetDefaultId(CosmeticCategory category)
    {
        foreach (var item in _items)
            if (item.Category == category && item.IsDefault) return item.Id;
        return string.Empty;
    }

    /// <summary>
    /// 全カテゴリのデフォルト ID を Dictionary で返す。
    /// </summary>
    public Dictionary<CosmeticCategory, string> GetAllDefaultIds()
    {
        var result = new Dictionary<CosmeticCategory, string>();
        foreach (CosmeticCategory cat in Enum.GetValues(typeof(CosmeticCategory)))
            result[cat] = GetDefaultId(cat);
        return result;
    }
}

// ─────────────── データ定義 ───────────────

/// <summary>コスメアイテムのカテゴリ種別。</summary>
public enum CosmeticCategory
{
    /// <summary>帽子 (頭部装飾)</summary>
    Hat,

    /// <summary>ツルハシ (手持ち道具)</summary>
    Pickaxe,

    /// <summary>たいまつ外観</summary>
    TorchSkin,

    /// <summary>羽・尻尾などのアクセサリ</summary>
    Accessory,
}

/// <summary>
/// 1 つのコスメアイテムの定義データ。CosmeticDatabase の要素として使用する。
/// </summary>
[Serializable]
public class CosmeticItemData
{
    [Header("基本情報")]
    [Tooltip("システム内部で使用する一意の文字列 ID（例: hat_default, hat_crown）")]
    [SerializeField] private string _id;

    [Tooltip("ショップ UI に表示するアイテム名")]
    [SerializeField] private string _displayName;

    [Tooltip("このアイテムが属するカテゴリ")]
    [SerializeField] private CosmeticCategory _category;

    [Header("アンロック設定")]
    [Tooltip("アンロックに必要な宝石数。0 かつ isDefault = true なら初期解放済み")]
    [SerializeField] private int _unlockPrice;

    [Tooltip("ゲーム開始時からアンロック済み扱いにするか（各カテゴリ 1 つ必須）")]
    [SerializeField] private bool _isDefault;

    [Header("見た目")]
    [Tooltip("装備スロットに Instantiate する Prefab。null の場合は何も表示しない（装備なし）")]
    [SerializeField] private GameObject _prefab;

    [Tooltip("ショップ UI に表示するサムネイル画像（省略可）")]
    [SerializeField] private Sprite _thumbnail;

    // ─── 公開プロパティ ───

    /// <summary>アイテムの一意 ID。</summary>
    public string Id           => _id;

    /// <summary>ショップに表示するアイテム名。</summary>
    public string DisplayName  => _displayName;

    /// <summary>このアイテムのカテゴリ。</summary>
    public CosmeticCategory Category => _category;

    /// <summary>アンロックに必要な宝石数。</summary>
    public int UnlockPrice     => _unlockPrice;

    /// <summary>初期アンロック済みか。</summary>
    public bool IsDefault      => _isDefault;

    /// <summary>装備スロットに配置する Prefab。</summary>
    public GameObject Prefab   => _prefab;

    /// <summary>ショップ用サムネイル。</summary>
    public Sprite Thumbnail    => _thumbnail;
}
