using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// ショップ・ロッカー・山中ドロップ共通のアイテム生成。
/// </summary>
public static class ItemRuntimeFactory
{
    private static Material s_sharedMaterial;

    public static GameObject CreateWorldItem(ShopItemType itemType, Vector3 position, Quaternion rotation)
    {
        var go = CreateBaseObject(itemType);
        if (go == null) return null;

        go.transform.SetPositionAndRotation(position, rotation);
        return go;
    }

    public static GameObject CreateBaseObject(ShopItemType itemType)
    {
        var prefab = NetworkRuntimeItemPrefabs.GetPrefab(itemType);
        GameObject go;

        if (prefab != null)
        {
            go = Object.Instantiate(prefab);
            go.name = itemType.ToString();
        }
        else
        {
            var profile = GetVisualProfile(itemType);
            go = GameObject.CreatePrimitive(profile.Primitive);
            go.name = itemType.ToString();
            go.transform.localScale = profile.Scale;

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = CreateColoredMaterial(profile.Color);
                if (mat != null) rend.sharedMaterial = mat;
            }

            if (go.GetComponent<Rigidbody>() == null)
                go.AddComponent<Rigidbody>();

            ApplyItemLayerAndTag(go);

            if (IsMetal(itemType))
                go.AddComponent<MagneticTarget>();

            if (!BasecampShopItemFactory.TryCreate(go, itemType, out _))
            {
                Object.Destroy(go);
                return null;
            }

            EnsureNetworkComponents(go, itemType);
        }

        return go;
    }

    private static void EnsureNetworkComponents(GameObject go, ShopItemType itemType)
    {
        switch (itemType)
        {
            case ShopItemType.Stretcher:
                EnsureStretcherNetworkStack(go);
                break;

            case ShopItemType.PortableWinch:
                EnsureWinchNetworkStack(go);
                break;
        }
    }

    private static void EnsureStretcherNetworkStack(GameObject go)
    {
        if (go.GetComponent<NetworkObject>() == null)
            go.AddComponent<NetworkObject>();
        if (go.GetComponent<NetworkTransform>() == null)
            go.AddComponent<NetworkTransform>();
        if (go.GetComponent<NetworkRigidbody>() == null)
            go.AddComponent<NetworkRigidbody>();
        if (go.GetComponent<NetworkStretcherSync>() == null)
            go.AddComponent<NetworkStretcherSync>();
    }

    private static void EnsureWinchNetworkStack(GameObject go)
    {
        if (go.GetComponent<NetworkObject>() == null)
            go.AddComponent<NetworkObject>();
        if (go.GetComponent<NetworkTransform>() == null)
            go.AddComponent<NetworkTransform>();
        if (go.GetComponent<NetworkPortableWinchSync>() == null)
            go.AddComponent<NetworkPortableWinchSync>();
        if (go.GetComponent<LineRenderer>() == null)
            go.AddComponent<LineRenderer>();
        if (go.GetComponent<WinchCableChain>() == null)
            go.AddComponent<WinchCableChain>();
        if (go.GetComponent<PortableWinchItem>() == null)
            go.AddComponent<PortableWinchItem>();
    }

    public static GameObject CreateFromDefinition(BasecampShopItemDefinition definition, Vector3 position, Quaternion rotation)
    {
        if (definition == null) return null;
        return CreateWorldItem(definition.ItemType, position, rotation);
    }

    /// <summary>GDD §8.6 — 山中遺留品用。耐久20〜40に設定して返す。</summary>
    public static GameObject CreateFieldDrop(ShopItemType itemType, Vector3 position, Quaternion rotation)
    {
        var go = CreateWorldItem(itemType, position, rotation);
        if (go == null) return null;

        var item = go.GetComponent<ItemBase>();
        if (item != null)
            item.ApplyFieldDropDurability(Random.Range(20f, 40f));

        return go;
    }

    public static bool IsMetal(ShopItemType type) => type switch
    {
        ShopItemType.ShortRope10m   => true,
        ShopItemType.LongRope25m    => true,
        ShopItemType.IceAxe         => true,
        ShopItemType.AnchorBolt     => true,
        ShopItemType.GrapplingHook  => true,
        ShopItemType.PortableWinch  => true,
        _                           => false,
    };

    private readonly struct ItemVisualProfile
    {
        public readonly PrimitiveType Primitive;
        public readonly Vector3       Scale;
        public readonly Color         Color;

        public ItemVisualProfile(PrimitiveType primitive, Vector3 scale, Color color)
        {
            Primitive = primitive;
            Scale     = scale;
            Color     = color;
        }
    }

    private static ItemVisualProfile GetVisualProfile(ShopItemType type) => type switch
    {
        ShopItemType.ShortRope10m   => new(PrimitiveType.Cylinder, new Vector3(0.12f, 0.15f, 0.12f), new Color(0.45f, 0.35f, 0.22f)),
        ShopItemType.LongRope25m    => new(PrimitiveType.Cylinder, new Vector3(0.14f, 0.22f, 0.14f), new Color(0.40f, 0.30f, 0.18f)),
        ShopItemType.IceAxe         => new(PrimitiveType.Capsule,  new Vector3(0.18f, 0.35f, 0.18f), new Color(0.65f, 0.68f, 0.72f)),
        ShopItemType.AnchorBolt     => new(PrimitiveType.Cylinder, new Vector3(0.08f, 0.18f, 0.08f), new Color(0.55f, 0.55f, 0.58f)),
        ShopItemType.GrapplingHook  => new(PrimitiveType.Sphere,   new Vector3(0.22f, 0.22f, 0.22f), new Color(0.72f, 0.55f, 0.18f)),
        ShopItemType.Stretcher      => new(PrimitiveType.Cube,     new Vector3(0.55f, 0.12f, 0.28f), new Color(0.78f, 0.22f, 0.18f)),
        ShopItemType.PackingKit     => new(PrimitiveType.Cube,     new Vector3(0.28f, 0.18f, 0.22f), new Color(0.58f, 0.42f, 0.28f)),
        ShopItemType.ThermalCase    => new(PrimitiveType.Cube,     new Vector3(0.32f, 0.24f, 0.24f), new Color(0.25f, 0.45f, 0.65f)),
        ShopItemType.SecureBelt     => new(PrimitiveType.Cube,     new Vector3(0.30f, 0.08f, 0.18f), new Color(0.35f, 0.35f, 0.38f)),
        ShopItemType.FoodPack        => new(PrimitiveType.Cube,     new Vector3(0.20f, 0.12f, 0.20f), new Color(0.55f, 0.72f, 0.35f)),
        ShopItemType.FlareGun       => new(PrimitiveType.Capsule,  new Vector3(0.14f, 0.28f, 0.14f), new Color(0.85f, 0.25f, 0.15f)),
        ShopItemType.EmergencyRadio => new(PrimitiveType.Cube,     new Vector3(0.24f, 0.14f, 0.16f), new Color(0.22f, 0.28f, 0.32f)),
        ShopItemType.PortableWinch  => new(PrimitiveType.Cube,     new Vector3(0.32f, 0.22f, 0.28f), new Color(0.48f, 0.50f, 0.54f)),
        ShopItemType.BivouacTent    => new(PrimitiveType.Cube,     new Vector3(0.38f, 0.22f, 0.38f), new Color(0.62f, 0.48f, 0.28f)),
        ShopItemType.OxygenTank     => new(PrimitiveType.Cylinder, new Vector3(0.20f, 0.32f, 0.20f), new Color(0.30f, 0.55f, 0.85f)),
        _                           => new(PrimitiveType.Cube,     Vector3.one * 0.25f, new Color(0.55f, 0.45f, 0.35f)),
    };

    private static void ApplyItemLayerAndTag(GameObject go)
    {
        int itemLayer = LayerMask.NameToLayer("Item");
        if (itemLayer >= 0) go.layer = itemLayer;
        if (!go.CompareTag("Untagged")) return;
        try { go.tag = "Item"; }
        catch (UnityException) { /* Tag 未登録時は Default のまま */ }
    }

    private static Material CreateColoredMaterial(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return GetSharedMaterial();

        var mat = new Material(shader) { name = $"ItemRuntime_{color}" };
        mat.color = color;
        return mat;
    }

    private static Material GetSharedMaterial()
    {
        if (s_sharedMaterial != null) return s_sharedMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        s_sharedMaterial = new Material(shader) { name = "ItemRuntimeSharedMaterial" };
        s_sharedMaterial.color = new Color(0.55f, 0.45f, 0.35f);
        return s_sharedMaterial;
    }
}
