using System;
using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §5.1 / §4.3 — バックパック（4スロット単位・6重量）と手持ちアイテムを管理。
/// </summary>
public class PlayerInventory : MonoBehaviour
{
    private static readonly List<PlayerInventory> s_registeredInventories = new();

    [Header("容量制限 (GDD §8.1)")]
    [SerializeField] private int   _maxSlotUnits = 4;
    [SerializeField] private float _maxWeight    = 6f;

    [Header("アンカー")]
    [SerializeField] private Transform _handAnchor;
    [SerializeField] private Vector3   _handLocalOffset = new Vector3(0.35f, -0.25f, 0.55f);

    private readonly List<ItemBase> _backpack = new();
    private readonly ItemBase[]     _quickSlots = new ItemBase[4];
    private ItemBase _handItem;
    private float    _cachedWeight;

    public ItemBase                 HandItem              => _handItem;
    public IReadOnlyList<ItemBase>  BackpackItems         => _backpack;
    public IReadOnlyList<ItemBase>  QuickSlotItems        => _quickSlots;
    public int                      UsedSlotUnits           => ComputeUsedSlotUnits();
    public float                    TotalWeight             => _cachedWeight;
    public bool                     IsBackpackFull          => UsedSlotUnits >= _maxSlotUnits;
    public bool                     HasHandItem             => _handItem != null && !_handItem.IsBroken;
    public static IReadOnlyList<PlayerInventory> RegisteredInventories => s_registeredInventories;

    /// <summary>互換: 手持ち + バックパック内の全アイテム。</summary>
    public IReadOnlyList<ItemBase> Items
    {
        get
        {
            s_itemsScratch.Clear();
            if (_handItem != null) s_itemsScratch.Add(_handItem);
            s_itemsScratch.AddRange(_backpack);
            return s_itemsScratch;
        }
    }

    public int SlotCount => (_handItem != null ? 1 : 0) + _backpack.Count;
    public bool IsFull => IsBackpackFull && HasHandItem;

    public event Action OnInventoryChanged;

    private static readonly List<ItemBase> s_itemsScratch = new();

    private void Awake()
    {
        if (_handAnchor == null)
        {
            var cam = GetComponentInChildren<Camera>();
            _handAnchor = cam != null ? cam.transform : transform;
        }
    }

    private void OnEnable()
    {
        if (!s_registeredInventories.Contains(this))
            s_registeredInventories.Add(this);
    }

    private void OnDisable()
    {
        s_registeredInventories.Remove(this);
    }

    // ── 拾う / 格納 ───────────────────────────────────────────
    public bool TryAdd(ItemBase item)
    {
        if (item == null || item.IsBroken) return false;

        if (!HasHandItem)
            return TryEquipHand(item);

        if (CanStoreInBackpack(item))
            return TryStoreInBackpack(item);

        Debug.Log("[Inventory] スロット満杯または重量超過");
        return false;
    }

    public bool CanStoreInBackpack(ItemBase item)
    {
        if (item == null) return false;
        if (UsedSlotUnits + item.Slots > _maxSlotUnits) return false;
        if (_cachedWeight + item.Weight > _maxWeight) return false;
        return true;
    }

    public bool TryEquipHand(ItemBase item)
    {
        if (item == null || item.IsBroken) return false;
        if (_handItem == item) return true;

        if (_handItem != null && !TryStoreInBackpack(_handItem))
            return false;

        bool fromBackpack = _backpack.Remove(item);
        if (!fromBackpack)
            _cachedWeight += item.Weight;

        _handItem = item;
        item.OnEquippedInHand(this, _handAnchor, _handLocalOffset);

        GameServices.Audio?.PlaySE(SoundId.ItemPickup, item.transform.position);
        NotifyChanged();
        return true;
    }

    public bool TryStoreInBackpack(ItemBase item)
    {
        if (item == null || item.IsBroken) return false;
        if (_backpack.Contains(item)) return true;

        if (_handItem == item)
        {
            if (!CanStoreInBackpackIgnoringHand(item)) return false;
            _handItem = null;
            item.OnRemovedFromHand();
            _backpack.Add(item);
            item.OnStoredInInventory(this);
            NotifyChanged();
            return true;
        }

        if (!CanStoreInBackpack(item)) return false;
        _backpack.Add(item);
        _cachedWeight += item.Weight;
        item.OnStoredInInventory(this);
        AssignQuickSlotIfEmpty(item);
        NotifyChanged();
        return true;
    }

    private void AssignQuickSlotIfEmpty(ItemBase item)
    {
        for (int i = 0; i < _quickSlots.Length; i++)
        {
            if (_quickSlots[i] != null) continue;
            _quickSlots[i] = item;
            return;
        }
    }

    private bool CanStoreInBackpackIgnoringHand(ItemBase item)
    {
        int units = UsedSlotUnits - (_handItem == item ? item.Slots : 0);
        if (units + item.Slots > _maxSlotUnits) return false;
        return _cachedWeight <= _maxWeight;
    }

    public bool TryQuickEquip(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _quickSlots.Length) return false;
        var item = _quickSlots[slotIndex];
        if (item == null || item.IsBroken) return false;
        return TryEquipHand(item);
    }

    public void AssignQuickSlot(int slotIndex, ItemBase item)
    {
        if (slotIndex < 0 || slotIndex >= _quickSlots.Length) return;
        _quickSlots[slotIndex] = item;
        NotifyChanged();
    }

    public void Remove(ItemBase item)
    {
        if (item == null) return;

        if (_handItem == item)
        {
            _handItem = null;
            item.OnRemovedFromHand();
        }
        else if (_backpack.Remove(item))
        {
            _cachedWeight -= item.Weight;
            item.OnRemovedFromInventory();
        }

        for (int i = 0; i < _quickSlots.Length; i++)
        {
            if (_quickSlots[i] == item) _quickSlots[i] = null;
        }

        NotifyChanged();
    }

    /// <summary>GDD §4.8 — 丁寧に足元へ置く。</summary>
    public void DropHandItemGently()
    {
        if (!HasHandItem) return;

        var item = _handItem;
        _handItem = null;
        item.OnRemovedFromHand();

        item.transform.SetParent(null);
        item.transform.position = transform.position + transform.forward * 0.4f + Vector3.up * 0.15f;

        var rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        GameServices.Audio?.PlaySE(SoundId.ItemDrop, item.transform.position);
        NotifyChanged();
    }

    public void DropItem(ItemBase item)
    {
        if (item == null) return;
        Vector3 pos = item.transform.position;
        Remove(item);
        GameServices.Audio?.PlaySE(SoundId.ItemDrop, pos);
    }

    public void ThrowItem(ItemBase item, Vector3 direction, float force = 8f)
    {
        if (item == null) return;
        Remove(item);

        item.transform.SetParent(null);
        item.transform.position = transform.position + transform.forward * 0.5f + Vector3.up * 0.5f;

        var rb = item.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.AddForce(direction * force, ForceMode.Impulse);
        }

        GameServices.Audio?.PlaySE(SoundId.ItemThrow, item.transform.position);
        Debug.Log($"[Inventory] {item.ItemName} を投げた");
    }

    /// <summary>GDD §4.1 — 死亡時に全アイテムを地面へ。</summary>
    public void DropAllOnDeath()
    {
        if (_handItem != null)
        {
            var item = _handItem;
            _handItem = null;
            ReleaseItemToWorld(item);
        }

        for (int i = _backpack.Count - 1; i >= 0; i--)
        {
            var item = _backpack[i];
            _backpack.RemoveAt(i);
            _cachedWeight -= item.Weight;
            ReleaseItemToWorld(item);
        }

        for (int i = 0; i < _quickSlots.Length; i++)
            _quickSlots[i] = null;

        NotifyChanged();
    }

    private static void ReleaseItemToWorld(ItemBase item)
    {
        item.OnRemovedFromInventory();
        item.transform.SetParent(null);
        var rb = item.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;
    }

    public bool HasItem(string itemName)
    {
        if (_handItem != null && !_handItem.IsBroken && _handItem.ItemName == itemName) return true;
        for (int i = 0; i < _backpack.Count; i++)
        {
            var it = _backpack[i];
            if (!it.IsBroken && it.ItemName == itemName) return true;
        }
        return false;
    }

    public ItemBase GetItem(string itemName)
    {
        if (_handItem != null && !_handItem.IsBroken && _handItem.ItemName == itemName)
            return _handItem;
        for (int i = 0; i < _backpack.Count; i++)
        {
            var it = _backpack[i];
            if (!it.IsBroken && it.ItemName == itemName) return it;
        }
        return null;
    }

    public ItemBase GetHandOrFirstBackpackItem()
    {
        if (HasHandItem) return _handItem;
        for (int i = 0; i < _backpack.Count; i++)
        {
            if (!_backpack[i].IsBroken) return _backpack[i];
        }
        return null;
    }

    private int ComputeUsedSlotUnits()
    {
        int units = 0;
        if (_handItem != null) units += _handItem.Slots;
        for (int i = 0; i < _backpack.Count; i++)
            units += _backpack[i].Slots;
        return units;
    }

    private void NotifyChanged() => OnInventoryChanged?.Invoke();
}
