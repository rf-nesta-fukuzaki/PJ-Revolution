#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only helper for Stage01. Places primitive, kinematic rocks on the
/// active terrain using the MAP_02/MAP_03 zone counts.
/// </summary>
public static class GrappableRockPlacer
{
    private static readonly (string zone, Vector3 center, Vector2 size, int count, Vector2 xzScale, Vector2 yScale)[] Zones =
    {
        ("Z1", new Vector3(0f, 0f, -80f), new Vector2(60f, 40f), 10, new Vector2(0.8f, 2.0f), new Vector2(0.5f, 1.2f)),
        ("Z2", new Vector3(0f, 0f, -30f), new Vector2(50f, 35f), 15, new Vector2(1.5f, 3.0f), new Vector2(0.8f, 1.5f)),
        ("Z3", new Vector3(0f, 0f,  20f), new Vector2(40f, 25f), 10, new Vector2(2.0f, 4.0f), new Vector2(3.0f, 6.0f)),
        ("Z4", new Vector3(0f, 0f,  55f), new Vector2(35f, 20f),  8, new Vector2(1.5f, 3.0f), new Vector2(1.0f, 2.5f)),
        ("Z5", new Vector3(0f, 0f,  90f), new Vector2(30f, 18f),  7, new Vector2(1.2f, 2.5f), new Vector2(1.0f, 2.0f)),
        ("Z6", new Vector3(0f, 0f, 130f), new Vector2(20f, 12f),  3, new Vector2(1.0f, 2.0f), new Vector2(0.8f, 1.5f)),
    };

    [MenuItem("Peak Plunder/Stage01/Place Grappable Rocks")]
    public static void PlaceRocksFromMenu()
    {
        PlaceRocks(GameObject.Find("GrappableRocks"));
    }

    public static void PlaceRocks(GameObject parent)
    {
        if (parent == null)
        {
            Debug.LogError("[RockPlacer] GrappableRocks not found.");
            return;
        }

        ClearChildren(parent.transform);

        var terrain = Terrain.activeTerrain;
        var material = Stage01EditorUtil.GetOrCreateMaterial(
            "Stage01_Rock_Mat",
            new Color(0.43f, 0.39f, 0.33f));

        int totalCreated = 0;
        Random.InitState(4201);

        foreach (var zone in Zones)
        {
            for (int i = 0; i < zone.count; i++)
            {
                float x = zone.center.x + Random.Range(-zone.size.x * 0.5f, zone.size.x * 0.5f);
                float z = zone.center.z + Random.Range(-zone.size.y * 0.5f, zone.size.y * 0.5f);
                float y = Stage01EditorUtil.SampleTerrainHeight(terrain, x, z);

                var rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                rock.name = $"Rock_{zone.zone}_{i + 1:00}";
                rock.transform.SetParent(parent.transform);
                rock.transform.position = new Vector3(x, y, z);
                rock.transform.localScale = new Vector3(
                    Random.Range(zone.xzScale.x, zone.xzScale.y),
                    Random.Range(zone.yScale.x, zone.yScale.y),
                    Random.Range(zone.xzScale.x, zone.xzScale.y));
                rock.transform.rotation = Quaternion.Euler(
                    Random.Range(-15f, 15f),
                    Random.Range(0f, 360f),
                    Random.Range(-15f, 15f));

                Stage01EditorUtil.TrySetTag(rock, "Grappable");
                Stage01EditorUtil.TrySetLayer(rock, "Grappable");
                rock.GetComponent<Renderer>().sharedMaterial = material;

                var rb = rock.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                totalCreated++;
            }
        }

        EditorUtility.SetDirty(parent);
        Debug.Log($"[RockPlacer] 岩 {totalCreated} 個を配置");
    }

    private static void ClearChildren(Transform root)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.GetChild(i).gameObject);
    }
}
#endif
