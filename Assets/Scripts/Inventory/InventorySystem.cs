using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーのインベントリを管理する MonoBehaviour。
/// PlayerPrefab ルートにアタッチして使用する。
/// </summary>
public class InventorySystem : MonoBehaviour
{
    // ─── スロット定義 ────────────────────────────────────────────────

    [Serializable]
    public struct InventorySlot
    {
        public InventoryItem Item;
        public int           Count;
    }

    // ─── Inspector ───────────────────────────────────────────────────

    [SerializeField] private float _maxWeight = 30f;

    // ─── 内部状態 ─────────────────────────────────────────────────────

    private readonly List<InventorySlot> _slots = new();

    // ─── 公開プロパティ ───────────────────────────────────────────────

    /// <summary>全スロットの合計重量</summary>
    public float CurrentWeight
    {
        get
        {
            float total = 0f;
            foreach (var slot in _slots)
                total += slot.Item.Weight * slot.Count;
            return total;
        }
    }

    public float MaxWeight => _maxWeight;

    /// <summary>UpgradeSystem から最大重量を変更する。</summary>
    public void SetMaxWeight(float v) => _maxWeight = Mathf.Max(0f, v);

    public IReadOnlyList<InventorySlot> Slots => _slots;

    // ─── イベント ─────────────────────────────────────────────────────

    /// <summary>スロット内容が変化したときに発火する</summary>
    public event Action OnInventoryChanged;

    // ─── 公開 API ─────────────────────────────────────────────────────

    /// <summary>
    /// アイテムを追加する。重量超過の場合は失敗して false を返す。
    /// スタック可能なスロットがあれば加算、なければ新規スロットを作成する。
    /// </summary>
    public bool TryAddItem(InventoryItem item, int count = 1)
    {
        if (item == null || count <= 0) return false;

        if (CurrentWeight + item.Weight * count > _maxWeight)
            return false;

        int remaining = count;

        // 既存スロットへのスタック
        for (int i = 0; i < _slots.Count && remaining > 0; i++)
        {
            var slot = _slots[i];
            if (slot.Item != item) continue;
            if (slot.Count >= item.MaxStack) continue;

            int add = Mathf.Min(remaining, item.MaxStack - slot.Count);
            slot.Count += add;
            _slots[i]   = slot;
            remaining  -= add;
        }

        // 新規スロット
        while (remaining > 0)
        {
            int add = Mathf.Min(remaining, item.MaxStack);
            _slots.Add(new InventorySlot { Item = item, Count = add });
            remaining -= add;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// アイテムを指定数だけ取り除く。個数が足りない場合は失敗して false を返す。
    /// </summary>
    public bool TryRemoveItem(InventoryItem item, int count = 1)
    {
        if (item == null || count <= 0) return false;
        if (GetItemCount(item) < count) return false;

        int remaining = count;
        for (int i = _slots.Count - 1; i >= 0 && remaining > 0; i--)
        {
            var slot = _slots[i];
            if (slot.Item != item) continue;

            int remove = Mathf.Min(remaining, slot.Count);
            slot.Count -= remove;
            remaining  -= remove;

            if (slot.Count == 0)
                _slots.RemoveAt(i);
            else
                _slots[i] = slot;
        }

        OnInventoryChanged?.Invoke();
        return true;
    }

    /// <summary>指定アイテムを count 個以上所持しているか</summary>
    public bool HasItem(InventoryItem item, int count = 1)
        => GetItemCount(item) >= count;

    /// <summary>指定アイテムの合計所持数を返す</summary>
    public int GetItemCount(InventoryItem item)
    {
        if (item == null) return 0;
        int total = 0;
        foreach (var slot in _slots)
            if (slot.Item == item)
                total += slot.Count;
        return total;
    }
}
