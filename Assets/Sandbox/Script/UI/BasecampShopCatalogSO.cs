using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "BasecampShopCatalog", menuName = "PeakIdiots/Basecamp/Shop Catalog")]
public sealed class BasecampShopCatalogSO : ScriptableObject
{
    [SerializeField] private List<BasecampShopItemDefinition> _items = new();

    public IReadOnlyList<BasecampShopItemDefinition> Items => _items;
}
