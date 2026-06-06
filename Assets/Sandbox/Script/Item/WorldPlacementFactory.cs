using UnityEngine;

/// <summary>
/// アンカーボルト・ビバークテント・山中ドロップなどワールド配置オブジェクトの共通生成。
/// オフライン／NGO ClientRpc の両方から同じ見た目・コンポーネント構成を保証する。
/// </summary>
public static class WorldPlacementFactory
{
    public static GameObject CreateAnchorBolt(Vector3 position, Vector3 normal)
    {
        var anchor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        anchor.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));
        anchor.transform.localScale = new Vector3(0.08f, 0.15f, 0.08f);

        var rend = anchor.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(0.6f, 0.5f, 0.3f);

        var col = anchor.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = false;

        var anchorRb = anchor.AddComponent<Rigidbody>();
        anchorRb.isKinematic = true;
        anchorRb.useGravity  = false;

        anchor.name = "AnchorBolt_Placed";
        GameServices.Ropes?.RegisterAnchorPoint(anchor.transform);
        return anchor;
    }

    public static GameObject CreateBivouacTent(Vector3 position, Quaternion rotation, float shelterRadius)
    {
        var tent = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tent.transform.SetPositionAndRotation(position + Vector3.up, rotation);
        tent.transform.localScale = new Vector3(3f, 2f, 3f);

        var rend = tent.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(0.2f, 0.5f, 0.8f);

        var col = tent.GetComponent<BoxCollider>();
        if (col != null)
            col.isTrigger = false;

        tent.name = "BivouacTent_Placed";

        var shelterChild = new GameObject("ShelterZone");
        shelterChild.transform.SetParent(tent.transform, false);
        shelterChild.transform.localPosition = Vector3.zero;
        var shelterCol = shelterChild.AddComponent<SphereCollider>();
        shelterCol.isTrigger = true;
        shelterCol.radius    = shelterRadius;
        shelterChild.AddComponent<ShelterZone>();

        var checkpoint = tent.AddComponent<BivouacCheckpoint>();
        checkpoint.Init(shelterRadius);

        return tent;
    }

    public static GameObject CreateFieldDrop(ShopItemType itemType, Vector3 position, Quaternion rotation, float durability)
    {
        var go = ItemRuntimeFactory.CreateWorldItem(itemType, position, rotation);
        if (go == null)
            return null;

        var item = go.GetComponent<ItemBase>();
        if (item != null)
            item.ApplyFieldDropDurability(durability);

        return go;
    }

    public static int MakePlacementKey(ShopItemType type, Vector3 position)
    {
        int x = Mathf.RoundToInt(position.x * 10f);
        int y = Mathf.RoundToInt(position.y * 10f);
        int z = Mathf.RoundToInt(position.z * 10f);
        return System.HashCode.Combine((int)type, x, y, z);
    }

    public static GameObject CreateFlareProjectile(Vector3 origin, Vector3 direction, float speed, float burnTime, float visibleRange)
    {
        var dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;

        var flare = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        flare.name = "Flare";
        // 発射点（カメラ位置）でプレイヤー自身のコライダーと重ならないよう前方へ少しオフセットする。
        flare.transform.position   = origin + dir * 0.8f;
        flare.transform.localScale = Vector3.one * 0.1f;

        var rend = flare.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = new Color(1f, 0.4f, 0f);

        var rb = flare.AddComponent<Rigidbody>();
        rb.linearVelocity = dir * speed;
        // 高速・小型のため確実に着地判定できるよう連続衝突判定にする。
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        var flareComp = flare.AddComponent<FlareBehavior>();
        flareComp.Init(burnTime, visibleRange);

        // trigger にすることで視線(LoS)判定を自分のコライダーで遮らない。
        // 着地検出は FlareBehavior が OnTriggerEnter / OnCollisionEnter の双方を処理する。
        var col = flare.GetComponent<SphereCollider>();
        if (col != null)
            col.isTrigger = true;

        return flare;
    }

    public static int MakePlacementKey(byte placementKind, Vector3 position)
    {
        int x = Mathf.RoundToInt(position.x * 10f);
        int y = Mathf.RoundToInt(position.y * 10f);
        int z = Mathf.RoundToInt(position.z * 10f);
        return System.HashCode.Combine(placementKind, x, y, z);
    }
}
