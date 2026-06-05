using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §8.5 — 購入アイテムを物理的にロッカー棚へ出す。
/// </summary>
public class BasecampItemLocker : MonoBehaviour
{
    public static BasecampItemLocker Instance { get; private set; }

    [SerializeField] private Transform[] _shelfPoints;
    [SerializeField] private Transform   _refundShelf;
    [SerializeField] private float       _pickupRange = 2f;

    private readonly List<GameObject> _spawnedItems = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        ResolveShelfPoints();
    }

    private void ResolveShelfPoints()
    {
        if (_shelfPoints != null && _shelfPoints.Length > 0) return;

        var shelves = new System.Collections.Generic.List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("Shelf"))
                shelves.Add(child);
        }
        if (shelves.Count > 0)
            _shelfPoints = shelves.ToArray();
        else
            _shelfPoints = new[] { transform };

        if (_refundShelf == null)
        {
            var refund = transform.Find("RefundShelf");
            if (refund != null) _refundShelf = refund;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void SpawnPurchasedItem(BasecampShopItemDefinition definition)
    {
        if (definition == null) return;

        var point = GetNextShelfPoint();
        var go = NetworkRuntimeItemSpawn.SpawnWorldItem(definition.ItemType, point.position, point.rotation);
        if (go == null) return;

        go.AddComponent<LockerSpawnedItem>().Init(definition.Id);
        _spawnedItems.Add(go);
    }

    private Transform GetNextShelfPoint()
    {
        int index = Mathf.Clamp(_spawnedItems.Count, 0, _shelfPoints.Length - 1);
        return _shelfPoints[index];
    }

    public bool TryRefundNearby(PlayerInventory inventory, Transform cameraTransform)
    {
        if (inventory == null || cameraTransform == null || _refundShelf == null) return false;

        var item = FindItemInRange(cameraTransform.position, cameraTransform.forward, _pickupRange);
        if (item == null) return false;

        var tag = item.GetComponent<LockerSpawnedItem>();
        if (tag == null) return false;

        float dist = Vector3.Distance(item.transform.position, _refundShelf.position);
        if (dist > 1.5f) return false;

        var shop = FindFirstObjectByType<BasecampShop>();
        if (shop == null || !shop.TryRefund(tag.ShopItemId)) return false;

        _spawnedItems.Remove(item.gameObject);
        Destroy(item.gameObject);
        return true;
    }

    private static ItemBase FindItemInRange(Vector3 origin, Vector3 forward, float range)
    {
        if (!Physics.Raycast(origin, forward, out var hit, range)) return null;
        return hit.collider.GetComponentInParent<ItemBase>();
    }
}

public sealed class LockerSpawnedItem : MonoBehaviour
{
    private string _shopItemId;

    public string ShopItemId => _shopItemId;

    public void Init(string shopItemId) => _shopItemId = shopItemId;
}
