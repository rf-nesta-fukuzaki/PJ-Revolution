using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// ショップ ID から実アイテムを生成してローカルプレイヤーへ渡す共有ヘルパー。
/// <see cref="RunLoadout"/>（ショップ持ち越し）と将来の支給系の双方から再利用する。
/// 外部アセット不要: 見た目は Cube プリミティブ＋URP マテリアルで代替する。
/// </summary>
public static class BasecampLoadoutSpawner
{
    private static Material s_runtimeItemMaterial;
    private static Dictionary<string, BasecampShopItemDefinition> s_catalog;

    private static Dictionary<string, BasecampShopItemDefinition> Catalog
    {
        get
        {
            if (s_catalog != null) return s_catalog;
            s_catalog = new Dictionary<string, BasecampShopItemDefinition>();
            foreach (var def in BasecampShopDefaultCatalog.Create())
            {
                if (def != null && !s_catalog.ContainsKey(def.Id))
                    s_catalog[def.Id] = def;
            }
            return s_catalog;
        }
    }

    /// <summary>ショップ ID のアイテムを 1 つ生成し、ローカルプレイヤーへ付与する。</summary>
    public static bool SpawnToPlayer(string shopItemId)
    {
        if (string.IsNullOrEmpty(shopItemId)) return false;
        if (!Catalog.TryGetValue(shopItemId, out var def) || def == null)
        {
            Debug.LogWarning($"[Loadout] 未知のショップ ID をスキップ: {shopItemId}");
            return false;
        }

        var go = CreateItemObject(def);
        if (go == null) return false;

        var item = go.GetComponent<ItemBase>();
        if (item == null) { Object.Destroy(go); return false; }

        var inventory = FindLocalPlayerInventory();
        if (inventory != null && inventory.TryAdd(item))
            return true;

        // インベントリ満杯 / 未検出時は足元（あればプレイヤー、なければ原点付近）へ落とす。
        Vector3 dropPos = inventory != null
            ? inventory.transform.position + Vector3.up * 0.6f
            : new Vector3(0f, 1.5f, 0f);
        go.transform.position = dropPos;
        return true;
    }

    private static GameObject CreateItemObject(BasecampShopItemDefinition definition)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = definition.DisplayName;
        go.transform.localScale = Vector3.one * 0.25f;

        var rend = go.GetComponent<Renderer>();
        if (rend != null)
        {
            var material = GetRuntimeItemMaterial();
            if (material != null) rend.sharedMaterial = material;
        }

        go.AddComponent<Rigidbody>();

        if (definition.IsMetal)
            go.AddComponent<MagneticTarget>();

        if (!BasecampShopItemFactory.TryCreate(go, definition.ItemType, out _))
        {
            Debug.LogError($"[Loadout] ItemType のファクトリが未定義です: {definition.ItemType}");
            Object.Destroy(go);
            return null;
        }

        return go;
    }

    private static Material GetRuntimeItemMaterial()
    {
        if (s_runtimeItemMaterial != null) return s_runtimeItemMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        s_runtimeItemMaterial = new Material(shader) { name = "BasecampLoadoutRuntimeItemMaterial" };
        return s_runtimeItemMaterial;
    }

    private static PlayerInventory FindLocalPlayerInventory()
    {
        var inventories = PlayerInventory.RegisteredInventories;
        if (inventories == null || inventories.Count == 0) return null;

        foreach (var inv in inventories)
        {
            var no = inv.GetComponent<NetworkObject>();
            if (no != null && no.IsOwner) return inv;
        }
        return inventories[0];
    }
}
