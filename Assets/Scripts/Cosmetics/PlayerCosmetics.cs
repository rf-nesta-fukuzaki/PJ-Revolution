using UnityEngine;

/// <summary>
/// プレイヤーの装備中コスメアイテムを管理するコンポーネント。
/// 各スロットの子 GameObject に Prefab を差し替える。
///
/// [PlayerPrefab セットアップ]
///   PlayerPrefab ルートにこのスクリプトをアタッチし、
///   Inspector で以下の子 GameObject を割り当てる:
///     HatSlot / PickaxeSlot / TorchSkinSlot / AccessorySlot
///
/// [装備変更フロー]
///   RequestEquip() を呼ぶとスロットの Prefab を即時差し替える。
/// </summary>
public class PlayerCosmetics : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("コスメデータベース")]
    [Tooltip("全アイテム定義を保持する ScriptableObject")]
    [SerializeField] private CosmeticDatabase _database;

    [Header("装備スロット (子 GameObject)")]
    [Tooltip("帽子を Instantiate する親 Transform")]
    [SerializeField] private Transform _hatSlot;

    [Tooltip("ツルハシを Instantiate する親 Transform")]
    [SerializeField] private Transform _pickaxeSlot;

    [Tooltip("たいまつ外観を Instantiate する親 Transform")]
    [SerializeField] private Transform _torchSkinSlot;

    [Tooltip("アクセサリ（羽・尻尾等）を Instantiate する親 Transform")]
    [SerializeField] private Transform _accessorySlot;

    // ─────────────── 装備状態 ───────────────

    private string _equippedHat       = string.Empty;
    private string _equippedPickaxe   = string.Empty;
    private string _equippedTorchSkin = string.Empty;
    private string _equippedAccessory = string.Empty;

    public string EquippedHat       => _equippedHat;
    public string EquippedPickaxe   => _equippedPickaxe;
    public string EquippedTorchSkin => _equippedTorchSkin;
    public string EquippedAccessory => _equippedAccessory;

    // ─────────────── Unity Lifecycle ───────────────

    private void Start()
    {
        ApplySlot(CosmeticCategory.Hat,       _equippedHat);
        ApplySlot(CosmeticCategory.Pickaxe,   _equippedPickaxe);
        ApplySlot(CosmeticCategory.TorchSkin, _equippedTorchSkin);
        ApplySlot(CosmeticCategory.Accessory, _equippedAccessory);
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// コスメアイテムを装備する。CosmeticShopUI から呼ぶ。
    /// </summary>
    public void RequestEquip(CosmeticCategory category, string itemId)
    {
        switch (category)
        {
            case CosmeticCategory.Hat:       _equippedHat       = itemId; break;
            case CosmeticCategory.Pickaxe:   _equippedPickaxe   = itemId; break;
            case CosmeticCategory.TorchSkin: _equippedTorchSkin = itemId; break;
            case CosmeticCategory.Accessory: _equippedAccessory = itemId; break;
        }
        ApplySlot(category, itemId);
        Debug.Log($"[PlayerCosmetics] 装備変更: {category} = {itemId}");
    }

    /// <summary>外部から直接スロットに反映する（PlayerCosmeticSaveData 等から呼ぶ）。</summary>
    public void EquipLocal(CosmeticCategory category, string itemId)
    {
        RequestEquip(category, itemId);
    }

    // ─────────────── 内部処理 ───────────────

    private void ApplySlot(CosmeticCategory category, string itemId)
    {
        Transform slot = GetSlot(category);
        if (slot == null) return;

        for (int i = slot.childCount - 1; i >= 0; i--)
            Destroy(slot.GetChild(i).gameObject);

        if (_database == null) return;

        var data = _database.FindById(itemId);
        if (data == null || data.Prefab == null) return;

        var instance = Instantiate(data.Prefab, slot);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale    = Vector3.one;
    }

    private Transform GetSlot(CosmeticCategory category)
    {
        return category switch
        {
            CosmeticCategory.Hat       => _hatSlot,
            CosmeticCategory.Pickaxe   => _pickaxeSlot,
            CosmeticCategory.TorchSkin => _torchSkinSlot,
            CosmeticCategory.Accessory => _accessorySlot,
            _                          => null,
        };
    }
}
