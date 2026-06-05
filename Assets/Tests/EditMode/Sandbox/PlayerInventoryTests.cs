using NUnit.Framework;
using UnityEngine;

public class PlayerInventoryTests
{
    [Test]
    public void TryAdd_respects_weight_limit_of_6_units()
    {
        var go = new GameObject("Player");
        var inv = go.AddComponent<PlayerInventory>();

        var heavy = CreateTestItem("Heavy", weight: 4f, slots: 1);
        var medium = CreateTestItem("Medium", weight: 3f, slots: 1);

        Assert.IsTrue(inv.TryAdd(heavy));
        Assert.IsFalse(inv.TryAdd(medium), "4 + 3 > 6 weight cap");

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(heavy.gameObject);
    }

    [Test]
    public void TryAdd_respects_slot_units_including_large_items()
    {
        var go = new GameObject("Player");
        var inv = go.AddComponent<PlayerInventory>();

        var stretcher = CreateTestItem("Stretcher", weight: 1f, slots: 3);
        var small = CreateTestItem("Small", weight: 1f, slots: 2);

        Assert.IsTrue(inv.TryAdd(stretcher));
        Assert.IsFalse(inv.TryAdd(small), "3 + 2 > 4 slot units");

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(stretcher.gameObject);
    }

    [Test]
    public void DropAllOnDeath_releases_all_items()
    {
        var go = new GameObject("Player");
        var inv = go.AddComponent<PlayerInventory>();

        var a = CreateTestItem("A", 1f, 1);
        var b = CreateTestItem("B", 1f, 1);
        Assert.IsTrue(inv.TryAdd(a));
        Assert.IsTrue(inv.TryAdd(b));

        inv.DropAllOnDeath();

        Assert.IsFalse(inv.HasHandItem);
        Assert.AreEqual(0, inv.BackpackItems.Count);

        Object.DestroyImmediate(go);
        Object.DestroyImmediate(a.gameObject);
        Object.DestroyImmediate(b.gameObject);
    }

    private static ItemBase CreateTestItem(string name, float weight, int slots)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.AddComponent<Rigidbody>();
        var item = go.AddComponent<TestInventoryItem>();
        item.Init(name, weight, slots);
        return item;
    }

    private sealed class TestInventoryItem : ItemBase
    {
        public void Init(string itemName, float weight, int slots)
        {
            _itemName          = itemName;
            _weight            = weight;
            _slots             = slots;
            _maxDurability     = 100f;
            _currentDurability = 100f;
            _rb                = GetComponent<Rigidbody>();
        }

        protected override void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _currentDurability = _maxDurability;
        }
    }
}
