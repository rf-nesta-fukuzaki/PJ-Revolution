using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §5.1 — プレイヤーインベントリ。
/// 重量制限・スロット制限あり。
/// 全アイテムが物理オブジェクトのため、落とす・壊す・投げるが可能。
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    private static readonly List<PlayerInventory> s_registeredInventories = new();

    [Header("容量制限")]
    [SerializeField] private int   _maxSlots  = 4;
    [SerializeField] private float _maxWeight = 8f;

    private readonly List<ItemBase> _items = new();

    private float _cachedWeight;

    public IReadOnlyList<ItemBase> Items       => _items;
    public int                     SlotCount   => _items.Count;
    public float                   TotalWeight => _cachedWeight;
    public bool                    IsFull      => _items.Count >= _maxSlots;
    public static IReadOnlyList<PlayerInventory> RegisteredInventories => s_registeredInventories;

    private void OnEnable()
    {
        if (!s_registeredInventories.Contains(this))
            s_registeredInventories.Add(this);
    }

    private void OnDisable()
    {
        s_registeredInventories.Remove(this);
    }

    // ── アイテム操作 ─────────────────────────────────────────
    public bool TryAdd(ItemBase item)
    {
        if (IsFull)
        {
            Debug.Log("[Inventory] スロット満杯");
            return false;
        }
        if (_cachedWeight + item.Weight > _maxWeight)
        {
            Debug.Log("[Inventory] 重量超過");
            return false;
        }

        _items.Add(item);
        _cachedWeight += item.Weight;
        item.OnStoredInInventory(this);

        // GDD §15.2 — item_pickup
        PPAudioManager.Instance?.PlaySE(SoundId.ItemPickup, item.transform.position);
        return true;
    }

    public void Remove(ItemBase item)
    {
        if (!_items.Remove(item)) return;
        _cachedWeight -= item.Weight;
        item.OnRemovedFromInventory();
    }

    /// <summary>
    /// GDD §15.2 — item_drop。手持ち→地面への明示的な落下。
    /// 投擲（<see cref="ThrowItem"/>）や壊れた結果の消失には使用しない。
    /// </summary>
    public void DropItem(ItemBase item)
    {
        if (!_items.Contains(item)) return;
        Vector3 dropPos = item.transform.position;
        Remove(item);
        PPAudioManager.Instance?.PlaySE(SoundId.ItemDrop, dropPos);
    }

    public bool HasItem(string itemName)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            if (!it.IsBroken && it.ItemName == itemName) return true;
        }
        return false;
    }

    public ItemBase GetItem(string itemName)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var it = _items[i];
            if (!it.IsBroken && it.ItemName == itemName) return it;
        }
        return null;
    }

    /// <summary>アイテムを投げる（物理オブジェクト化して Rigidbody に力付与）。</summary>
    public void ThrowItem(ItemBase item, Vector3 direction, float force = 8f)
    {
        if (!_items.Contains(item)) return;

        Remove(item);
        item.transform.SetParent(null);
        item.transform.position = transform.position + transform.forward * 0.5f + Vector3.up * 0.5f;

        var rb = item.GetComponent<Rigidbody>();
        rb.isKinematic = false;
        rb.AddForce(direction * force, ForceMode.Impulse);

        // GDD §15.2 — item_throw
        PPAudioManager.Instance?.PlaySE(SoundId.ItemThrow, item.transform.position);

        Debug.Log($"[Inventory] {item.ItemName} を投げた");
    }
}
