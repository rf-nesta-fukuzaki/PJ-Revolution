#if UNITY_EDITOR
using PPAudioManager = PeakPlunder.Audio.AudioManager;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Builds the Stage01 Mountain01 layout described in docs/map-stage01.
/// This keeps the high-volume scene work in Unity APIs instead of editing scene YAML by hand.
/// </summary>
public static class Stage01MapBuilder
{
    private const string GameplayScenePath = "Assets/Sandbox/Scenes/Gameplay.unity";

    private static readonly string[] GeneratedRoots =
    {
        "World",
        "GrappableRocks",
        "IcePatches",
        "Checkpoints",
        "RouteGates",
        "RelicSpawnPoints",
        "PlayerSpawnPoints",
        "HazardSpawnPoints"
    };

    [MenuItem("Peak Plunder/Stage01/Build Gameplay Scene")]
    public static void BuildGameplayScene()
    {
        var scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);

        EnsureTagsAndLayers();
        DeleteGeneratedRoots();

        var materials = Stage01Materials.Load();
        var prefabs = Stage01Prefabs.CreateOrLoad(materials);

        var gameManager = EnsureGameManager();
        var world = CreateRoot("World");
        var mountain = CreateChild(world, "Mountain");
        var generator = mountain.AddComponent<MountainTerrainGenerator>();
        ConfigureMountainGenerator(generator);

        var basecamp = CreateZone(world, "Basecamp");
        var zone1 = CreateZone(world, "Zone1_Forest");
        var zone2 = CreateZone(world, "Zone2_RockySlope");
        var zone3 = CreateZone(world, "Zone3_CliffWall");
        var zone4 = CreateZone(world, "Zone4_TempleRuins");
        var zone5 = CreateZone(world, "Zone5_IceWall");
        var zone6 = CreateZone(world, "Zone6_Summit");

        var rocksRoot = CreateRoot("GrappableRocks");
        var iceRoot = CreateRoot("IcePatches");
        var checkpointsRoot = CreateRoot("Checkpoints");
        var routeGatesRoot = CreateRoot("RouteGates");
        var relicRoot = CreateRoot("RelicSpawnPoints");
        var playerSpawnRoot = CreateRoot("PlayerSpawnPoints");
        var hazardRoot = CreateRoot("HazardSpawnPoints");

        generator.Generate();
        var terrain = Terrain.activeTerrain;
        BuildPeakScaleLandmarks(mountain, terrain, materials);
        BuildPeakAscentSetpieces(mountain, terrain, materials);

        GrappableRockPlacer.PlaceRocks(rocksRoot);
        BuildBasecamp(basecamp, playerSpawnRoot, materials);
        BuildZone1(zone1, routeGatesRoot, relicRoot, hazardRoot, materials, prefabs, terrain);
        BuildZone2(zone2, routeGatesRoot, relicRoot, hazardRoot, materials, prefabs, terrain);
        BuildZone3(zone3, routeGatesRoot, relicRoot, hazardRoot, materials, prefabs, terrain);
        BuildZone4(zone4, relicRoot, hazardRoot, materials, prefabs, terrain);
        BuildZone5(zone5, iceRoot, routeGatesRoot, relicRoot, hazardRoot, materials, prefabs, terrain);
        BuildZone6(zone6, iceRoot, relicRoot, hazardRoot, materials, prefabs, terrain);

        var checkpointRefs = BuildRespawnCheckpoints(checkpointsRoot, terrain);
        ConfigureManagers(gameManager, checkpointRefs, prefabs);
        generator.SnapObjectsToTerrain();
        ConfigureLighting();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Stage01MapValidator.ValidateGameplayScene();
        Debug.Log("[Stage01MapBuilder] Stage01 Gameplay scene build complete.");
    }

    private static void EnsureTagsAndLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var tags = tagManager.FindProperty("tags");
        foreach (string tag in new[] { "Grappable", "Checkpoint", "ReturnZone", "SummitGoal" })
            AddStringIfMissing(tags, tag);

        var layers = tagManager.FindProperty("layers");
        AddLayerIfMissing(layers, "Grappable");
        AddLayerIfMissing(layers, "Hazard");

        tagManager.ApplyModifiedProperties();
    }

    private static void AddStringIfMissing(SerializedProperty array, string value)
    {
        for (int i = 0; i < array.arraySize; i++)
        {
            if (array.GetArrayElementAtIndex(i).stringValue == value)
                return;
        }

        array.InsertArrayElementAtIndex(array.arraySize);
        array.GetArrayElementAtIndex(array.arraySize - 1).stringValue = value;
    }

    private static void AddLayerIfMissing(SerializedProperty layers, string layerName)
    {
        for (int i = 0; i < layers.arraySize; i++)
        {
            if (layers.GetArrayElementAtIndex(i).stringValue == layerName)
                return;
        }

        for (int i = 8; i < layers.arraySize; i++)
        {
            if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
                continue;

            layers.GetArrayElementAtIndex(i).stringValue = layerName;
            return;
        }
    }

    private static void DeleteGeneratedRoots()
    {
        foreach (string rootName in GeneratedRoots)
        {
            var root = GameObject.Find(rootName);
            if (root != null)
                Object.DestroyImmediate(root);
        }
    }

    private static GameObject EnsureGameManager()
    {
        var go = GameObject.Find("GameManager") ?? new GameObject("GameManager");
        if (go.GetComponent<SpawnManager>() == null) go.AddComponent<SpawnManager>();
        if (go.GetComponent<ExpeditionManager>() == null) go.AddComponent<ExpeditionManager>();
        if (go.GetComponent<ScoreTracker>() == null) go.AddComponent<ScoreTracker>();
        if (go.GetComponent<PPAudioManager>() == null) go.AddComponent<PPAudioManager>();
        return go;
    }

    private static GameObject CreateRoot(string name)
    {
        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        return go;
    }

    private static GameObject CreateChild(GameObject parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        return go;
    }

    private static GameObject CreateZone(GameObject world, string name)
    {
        var zone = CreateChild(world, name);
        zone.transform.position = Vector3.zero;
        return zone;
    }

    private static void ConfigureMountainGenerator(MountainTerrainGenerator generator)
    {
        var so = new SerializedObject(generator);
        so.FindProperty("_terrainWidth").floatValue = 300f;
        so.FindProperty("_terrainLength").floatValue = 300f;
        so.FindProperty("_terrainHeight").floatValue = 520f;
        so.FindProperty("_resolution").intValue = 513;
        so.FindProperty("_seed").intValue = 42;
        so.FindProperty("_scale1").floatValue = 0.0065f;
        so.FindProperty("_amp1").floatValue = 0.62f;
        so.FindProperty("_scale2").floatValue = 0.020f;
        so.FindProperty("_amp2").floatValue = 0.34f;
        so.FindProperty("_scale3").floatValue = 0.075f;
        so.FindProperty("_amp3").floatValue = 0.12f;
        so.FindProperty("_routeMeanderMeters").floatValue = 14f;
        so.FindProperty("_routeBaseWidth").floatValue = 24f;
        so.FindProperty("_routeSummitWidth").floatValue = 5.5f;
        so.FindProperty("_routeFlattenStrength").floatValue = 0.29f;
        so.FindProperty("_fractalOctaves").intValue = 6;
        so.FindProperty("_fractalPersistence").floatValue = 0.47f;
        so.FindProperty("_fractalLacunarity").floatValue = 2.12f;
        so.FindProperty("_domainWarpScale").floatValue = 0.010f;
        so.FindProperty("_domainWarpMeters").floatValue = 18f;
        so.ApplyModifiedProperties();
    }

    private static void BuildPeakScaleLandmarks(GameObject mountain, Terrain terrain, Stage01Materials materials)
    {
        var root = CreateChild(mountain, "PeakScaleLandmarks");

        CreateSkylinePeak(root, "SkylinePeak_North_01", -92f, 136f, 38f, 185f, 0.85f, terrain, materials.Snow);
        CreateSkylinePeak(root, "SkylinePeak_North_02", -42f, 145f, 31f, 225f, 1.15f, terrain, materials.Snow);
        CreateSkylinePeak(root, "SkylinePeak_North_03", 38f, 144f, 34f, 215f, 0.65f, terrain, materials.Snow);
        CreateSkylinePeak(root, "SkylinePeak_North_04", 96f, 134f, 40f, 178f, 1.35f, terrain, materials.Snow);
        CreateSkylinePeak(root, "SkylinePeak_West_01", -132f, -24f, 34f, 150f, 0.25f, terrain, materials.Rock);
        CreateSkylinePeak(root, "SkylinePeak_West_02", -136f, 58f, 42f, 190f, 1.75f, terrain, materials.Stone);
        CreateSkylinePeak(root, "SkylinePeak_East_01", 130f, -10f, 36f, 165f, 2.15f, terrain, materials.Rock);
        CreateSkylinePeak(root, "SkylinePeak_East_02", 134f, 76f, 44f, 205f, 2.65f, terrain, materials.Snow);

        CreateMegaRock(root, "MegaFace_Z2_Left", -58f, -30f, new Vector3(18f, 92f, 34f), new Vector3(0f, -8f, -6f), terrain, materials.Rock);
        CreateMegaRock(root, "MegaFace_Z2_Right", 56f, -20f, new Vector3(20f, 105f, 38f), new Vector3(0f, 12f, 7f), terrain, materials.Rock);
        CreateMegaRock(root, "CliffGate_Z3_Left", -34f, 22f, new Vector3(14f, 118f, 28f), new Vector3(0f, -18f, -4f), terrain, materials.Stone);
        CreateMegaRock(root, "CliffGate_Z3_Right", 32f, 30f, new Vector3(15f, 126f, 30f), new Vector3(0f, 15f, 5f), terrain, materials.Stone);
        CreateMegaRock(root, "TempleBackWall_Z4", 0f, 78f, new Vector3(58f, 88f, 12f), new Vector3(-4f, 0f, 0f), terrain, materials.Temple);
        CreateMegaRock(root, "IceNeedle_Z5_Left", -30f, 105f, new Vector3(10f, 128f, 12f), new Vector3(0f, -8f, -3f), terrain, materials.Ice);
        CreateMegaRock(root, "IceNeedle_Z5_Right", 29f, 112f, new Vector3(9f, 142f, 11f), new Vector3(0f, 10f, 4f), terrain, materials.Ice);
        CreateMegaRock(root, "SummitShoulder_Left", -26f, 132f, new Vector3(16f, 116f, 24f), new Vector3(0f, -12f, -5f), terrain, materials.Snow);
        CreateMegaRock(root, "SummitShoulder_Right", 24f, 134f, new Vector3(17f, 124f, 24f), new Vector3(0f, 11f, 4f), terrain, materials.Snow);
        CreateMegaRock(root, "SummitNeedle_Back", 0f, 148f, new Vector3(20f, 155f, 16f), new Vector3(-7f, 0f, 0f), terrain, materials.Snow);
    }

    private static void CreateMegaRock(GameObject parent, string name, float x, float z, Vector3 scale, Vector3 rotation, Terrain terrain, Material material)
    {
        var rock = CreatePrimitive(parent, name, PrimitiveType.Cube, scale, TerrainPos(terrain, x, z, scale.y * 0.5f), material, worldSpace: true);
        rock.transform.rotation = Quaternion.Euler(rotation);
        Stage01EditorUtil.TrySetTag(rock, "Grappable");
        Stage01EditorUtil.TrySetLayer(rock, "Grappable");

        var rb = rock.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private static void CreateSkylinePeak(GameObject parent, string name, float x, float z, float radius, float height, float phase, Terrain terrain, Material material)
    {
        const int sides = 7;
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform);
        go.transform.position = TerrainPos(terrain, x, z, 0f);
        go.transform.rotation = Quaternion.Euler(0f, phase * 57.29578f, 0f);

        var vertices = new Vector3[sides + 2];
        vertices[0] = Vector3.zero;
        for (int i = 0; i < sides; i++)
        {
            float angle = (Mathf.PI * 2f * i / sides) + phase;
            float wobble = 0.72f + Mathf.PerlinNoise(phase * 11f, i * 0.37f) * 0.56f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius * wobble, 0f, Mathf.Sin(angle) * radius * wobble);
        }
        vertices[sides + 1] = new Vector3(radius * 0.12f * Mathf.Sin(phase * 3.1f), height, radius * 0.10f * Mathf.Cos(phase * 2.7f));

        var triangles = new int[sides * 6];
        int t = 0;
        for (int i = 0; i < sides; i++)
        {
            int next = i == sides - 1 ? 1 : i + 2;
            triangles[t++] = 0;
            triangles[t++] = next;
            triangles[t++] = i + 1;
            triangles[t++] = i + 1;
            triangles[t++] = next;
            triangles[t++] = sides + 1;
        }

        var mesh = new Mesh { name = $"{name}_Mesh" };
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        var collider = go.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;

        Stage01EditorUtil.TrySetTag(go, "Grappable");
        Stage01EditorUtil.TrySetLayer(go, "Grappable");

        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private static void BuildPeakAscentSetpieces(GameObject mountain, Terrain terrain, Stage01Materials materials)
    {
        var root = CreateChild(mountain, "PeakAscentSetpieces");

        CreateRouteLedge(root, "Z2_FirstScaryTraverse_A", -18f, -28f, 4f, new Vector3(20f, 1.2f, 4f), new Vector3(0f, 18f, -5f), terrain, materials.Stone);
        CreateRouteLedge(root, "Z2_FirstScaryTraverse_B", 7f, -12f, 5f, new Vector3(18f, 1.1f, 3.5f), new Vector3(0f, -22f, 4f), terrain, materials.Stone);
        CreateClimbWall(root, "Z3_RopeWall_Main", 0f, 18f, 12f, new Vector3(26f, 42f, 5f), new Vector3(-8f, 0f, 0f), terrain, materials.Rock);
        CreateRouteLedge(root, "Z3_TinyRestShelf", -9f, 38f, 24f, new Vector3(12f, 1f, 4f), new Vector3(0f, 12f, 7f), terrain, materials.Rock);
        CreateClimbWall(root, "Z4_TempleCliffBack", 0f, 76f, 8f, new Vector3(38f, 34f, 4f), new Vector3(-6f, 0f, 0f), terrain, materials.Temple);
        CreateRouteLedge(root, "Z5_IceKnifeRidge_A", -8f, 100f, 8f, new Vector3(32f, 1f, 2.2f), new Vector3(0f, 24f, 0f), terrain, materials.Ice);
        CreateRouteLedge(root, "Z5_IceKnifeRidge_B", 12f, 116f, 10f, new Vector3(28f, 1f, 2f), new Vector3(0f, -18f, 0f), terrain, materials.Ice);
        CreateClimbWall(root, "Z6_FinalSummitWall", 0f, 126f, 10f, new Vector3(30f, 48f, 5f), new Vector3(-10f, 0f, 0f), terrain, materials.Snow);
        CreateRouteLedge(root, "Z6_FinalBreathShelf", 0f, 137f, 16f, new Vector3(16f, 1.2f, 5f), new Vector3(0f, 0f, 0f), terrain, materials.Snow);
    }

    private static void CreateRouteLedge(GameObject parent, string name, float x, float z, float yOffset, Vector3 scale, Vector3 rotation, Terrain terrain, Material material)
    {
        var ledge = CreatePrimitive(parent, name, PrimitiveType.Cube, scale, TerrainPos(terrain, x, z, yOffset), material, worldSpace: true);
        ledge.transform.rotation = Quaternion.Euler(rotation);
        Stage01EditorUtil.TrySetTag(ledge, "Grappable");
        Stage01EditorUtil.TrySetLayer(ledge, "Grappable");

        var rb = ledge.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private static void CreateClimbWall(GameObject parent, string name, float x, float z, float yOffset, Vector3 scale, Vector3 rotation, Terrain terrain, Material material)
    {
        var wall = CreatePrimitive(parent, name, PrimitiveType.Cube, scale, TerrainPos(terrain, x, z, yOffset + scale.y * 0.5f), material, worldSpace: true);
        wall.transform.rotation = Quaternion.Euler(rotation);
        Stage01EditorUtil.TrySetTag(wall, "Grappable");
        Stage01EditorUtil.TrySetLayer(wall, "Grappable");

        var rb = wall.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private static void BuildBasecamp(GameObject basecamp, GameObject playerSpawnRoot, Stage01Materials materials)
    {
        var shop = CreateChild(basecamp, "BasecampShopArea");
        shop.transform.position = Pos(-15f, -135f, 1f);
        shop.AddComponent<BasecampShop>();
        CreatePrimitive(shop, "Counter_01", PrimitiveType.Cube, new Vector3(4f, 1f, 1f), new Vector3(-1.5f, 0.5f, 0f), materials.Wood);
        CreatePrimitive(shop, "Counter_02", PrimitiveType.Cube, new Vector3(4f, 1f, 1f), new Vector3(1.5f, 0.5f, 1.5f), materials.Wood);

        var departure = CreateChild(basecamp, "DeparturePoint");
        departure.transform.position = Pos(0f, -115f, 1f);
        departure.AddComponent<DepartureGate>();
        CreatePrimitive(departure, "Gate_Post_L", PrimitiveType.Cylinder, new Vector3(0.4f, 3f, 0.4f), new Vector3(-4f, 3f, 0f), materials.Forest);
        CreatePrimitive(departure, "Gate_Post_R", PrimitiveType.Cylinder, new Vector3(0.4f, 3f, 0.4f), new Vector3(4f, 3f, 0f), materials.Forest);
        CreatePrimitive(departure, "Gate_Beam", PrimitiveType.Cube, new Vector3(9f, 0.4f, 0.6f), new Vector3(0f, 6f, 0f), materials.Forest);

        var returnPoint = CreateChild(basecamp, "ReturnPoint");
        returnPoint.transform.position = Pos(0f, -140f, 1f);
        if (returnPoint.GetComponent<Unity.Netcode.NetworkObject>() == null)
            returnPoint.AddComponent<Unity.Netcode.NetworkObject>();
        returnPoint.AddComponent<ReturnZone>();
        Stage01EditorUtil.TrySetTag(returnPoint, "ReturnZone");

        var spawn = CreateChild(playerSpawnRoot, "PlayerSpawn_Basecamp");
        spawn.transform.position = new Vector3(0f, 2f, -130f);
        var sp = spawn.AddComponent<SpawnPoint>();
        ConfigureSpawnPoint(sp, SpawnLayer.Route, 0, 1f, true, null);

        var anchor = CreateChild(basecamp, "SpawnAnchor_Basecamp");
        anchor.transform.position = new Vector3(0f, 2f, -130f);
    }

    private static void BuildZone1(GameObject zone, GameObject routeRoot, GameObject relicRoot, GameObject hazardRoot, Stage01Materials materials, Stage01Prefabs prefabs, Terrain terrain)
    {
        var trees = CreateChild(zone, "Forest_Trees_Root");
        Random.InitState(1101);
        for (int i = 0; i < 20; i++)
        {
            float x = Random.Range(-30f, 30f);
            float z = Random.Range(-110f, -60f);
            CreateTree(trees, $"Tree_Z1_{i + 1:00}", x, z, terrain, materials);
        }

        CreateZoneCheckpoint(zone, "Checkpoint_Zone1", 0, 8f, 0f, -80f, terrain, materials);
        CreateShrine(zone, "Zone1_Shrine", -20f, -70f, terrain, materials);
        CreateRouteGate(routeRoot, "RouteGate_Z2_Shortcut", "Forest Shortcut", 25f, -65f, terrain, materials.Rock, new[] { new Vector3(3f, 2f, 2f), new Vector3(2f, 3f, 1.5f), new Vector3(2f, 2f, 2.5f) });
        CreateRelicSpawn(relicRoot, "RelicSpawn_Z1_A", -10f, -95f, 1, 0.60f, terrain);
        CreateRelicSpawn(relicRoot, "RelicSpawn_Z1_B", 18f, -72f, 1, 0.50f, terrain);
        CreateHazardSpawn(hazardRoot, "Hazard_Z1_Rock_01", 0f, -75f, 50f, 1, prefabs.Rockfall, terrain);
        CreateItemSpawn(hazardRoot, "ItemDrop_Z1_A", -5f, -100f, 1, prefabs.PlaceholderItem, terrain);
    }

    private static void BuildZone2(GameObject zone, GameObject routeRoot, GameObject relicRoot, GameObject hazardRoot, Stage01Materials materials, Stage01Prefabs prefabs, Terrain terrain)
    {
        CreateZoneCheckpoint(zone, "Checkpoint_Zone2", 1, 10f, 0f, -25f, terrain, materials);
        CreateShrine(zone, "Zone2_Shrine", -25f, -10f, terrain, materials);
        CreateRouteGate(routeRoot, "RouteGate_Z3_Bridge", "West Bridge", -20f, -5f, terrain, materials.Wood, new[] { new Vector3(8f, 0.5f, 2f) });
        CreateCollapsible(zone, "Bridge_Z3_A", -20f, 0f, 2f, new Vector3(4f, 0.4f, 6f), terrain, materials.Wood);
        CreateCollapsible(zone, "Bridge_Z3_B", 10f, 5f, 3f, new Vector3(3f, 0.4f, 5f), terrain, materials.Wood);
        CreateRelicSpawn(relicRoot, "RelicSpawn_Z2_A", -15f, -40f, 2, 0.65f, terrain);
        CreateRelicSpawn(relicRoot, "RelicSpawn_Z2_B", 20f, -20f, 2, 0.55f, terrain);
        CreateHazardSpawn(hazardRoot, "Hazard_Z2_Rock_01", 5f, -35f, 60f, 2, prefabs.Rockfall, terrain);
        CreateHazardSpawn(hazardRoot, "Hazard_Z2_Rock_02", -10f, -15f, 70f, 2, prefabs.Rockfall, terrain);
        CreateItemSpawn(hazardRoot, "ItemDrop_Z2_A", 12f, -38f, 2, prefabs.PlaceholderItem, terrain);
    }

    private static void BuildZone3(GameObject zone, GameObject routeRoot, GameObject relicRoot, GameObject hazardRoot, Stage01Materials materials, Stage01Prefabs prefabs, Terrain terrain)
    {
        CreateZoneCheckpoint(zone, "Checkpoint_Zone3", 2, 8f, 0f, 25f, terrain, materials);
        CreateShrine(zone, "Zone3_Shrine", 20f, 30f, terrain, materials);
        CreateRouteGate(routeRoot, "RouteGate_Z4_East", "East Cliff Pass", 25f, 40f, terrain, materials.Rock, new[] { new Vector3(4f, 4f, 3f), new Vector3(3f, 3f, 4f) });
        CreateRelicSpawn(relicRoot, "RelicSpawn_Z3_A", -10f, 20f, 3, 0.60f, terrain, 5f);
        CreateHazardSpawn(hazardRoot, "Hazard_Z3_Collapse_01", 0f, 10f, 0f, 3, prefabs.CollapsiblePlatform, terrain);
        CreateHazardSpawn(hazardRoot, "Hazard_Z3_Collapse_02", -15f, 30f, 0f, 3, prefabs.CollapsiblePlatform, terrain);
        CreateItemSpawn(hazardRoot, "ItemDrop_Z3_A", -8f, 15f, 3, prefabs.PlaceholderItem, terrain);
    }

    private static void BuildZone4(GameObject zone, GameObject relicRoot, GameObject hazardRoot, Stage01Materials materials, Stage01Prefabs prefabs, Terrain terrain)
    {
        var temple = CreateChild(zone, "Temple_Geometry");
        temple.transform.position = Pos(0f, 62f, Stage01EditorUtil.SampleTerrainHeight(terrain, 0f, 62f));
        CreateGrappablePrimitive(temple, "TempleWall_N", PrimitiveType.Cube, new Vector3(20f, 8f, 1f), new Vector3(0f, 4f, 10f), materials.Temple);
        CreateGrappablePrimitive(temple, "TempleWall_S", PrimitiveType.Cube, new Vector3(20f, 8f, 1f), new Vector3(0f, 4f, -10f), materials.Temple);
        CreateGrappablePrimitive(temple, "TempleWall_E", PrimitiveType.Cube, new Vector3(1f, 8f, 20f), new Vector3(10f, 4f, 0f), materials.Temple);
        CreateGrappablePrimitive(temple, "TempleWall_W", PrimitiveType.Cube, new Vector3(1f, 8f, 20f), new Vector3(-10f, 4f, 0f), materials.Temple);
        CreateGrappablePrimitive(temple, "TempleColumn_01", PrimitiveType.Cylinder, new Vector3(0.8f, 5f, 0.8f), new Vector3(-8f, 5f, -10f), materials.Temple);
        CreateGrappablePrimitive(temple, "TempleColumn_02", PrimitiveType.Cylinder, new Vector3(0.8f, 5f, 0.8f), new Vector3(8f, 5f, -10f), materials.Temple);
        CreateGrappablePrimitive(temple, "TempleColumn_03", PrimitiveType.Cylinder, new Vector3(0.8f, 5f, 0.8f), new Vector3(-8f, 5f, 10f), materials.Temple);
        CreateGrappablePrimitive(temple, "TempleColumn_04", PrimitiveType.Cylinder, new Vector3(0.8f, 5f, 0.8f), new Vector3(8f, 5f, 10f), materials.Temple);
        CreatePrimitive(temple, "TempleRoof_A", PrimitiveType.Cube, new Vector3(15f, 0.5f, 8f), new Vector3(-2f, 8f, 0f), materials.Temple);

        CreateTempleTrap(zone, "Temple_Traps", 0f, 58f, terrain, materials);
        CreateZoneCheckpoint(zone, "Checkpoint_Zone4", 3, 8f, 0f, 75f, terrain, materials, 5f);
        CreateShrine(zone, "Zone4_Shrine", 18f, 55f, terrain, materials);
        CreateRelicSpawn(relicRoot, "RelicSpawn_Z4_A", -5f, 60f, 4, 0.70f, terrain, 2f);
        CreateRelicSpawn(relicRoot, "RelicSpawn_Z4_B", 5f, 68f, 4, 0.60f, terrain, 2f);
        CreateHazardSpawn(hazardRoot, "Hazard_Z4_Trap_01", 0f, 58f, 0f, 4, prefabs.TempleTrap, terrain);
        CreateItemSpawn(hazardRoot, "ItemDrop_Z4_A", 15f, 55f, 4, prefabs.PlaceholderItem, terrain);
    }

    private static void BuildZone5(GameObject zone, GameObject iceRoot, GameObject routeRoot, GameObject relicRoot, GameObject hazardRoot, Stage01Materials materials, Stage01Prefabs prefabs, Terrain terrain)
    {
        CreateGrappableWorldPrimitive(zone, "IceFormation_01", PrimitiveType.Cube, -5f, 95f, 4f, new Vector3(5f, 8f, 3f), materials.Ice, terrain);
        CreateGrappableWorldPrimitive(zone, "IceFormation_02", PrimitiveType.Cube, 8f, 105f, 3f, new Vector3(3f, 6f, 4f), materials.Ice, terrain);
        float[,] ice = { { -15f, 83f, 4f, 4f }, { 10f, 88f, 3f, 5f }, { -5f, 93f, 5f, 3f }, { 18f, 97f, 4f, 4f }, { 0f, 102f, 6f, 4f }, { -12f, 107f, 3f, 6f }, { 5f, 112f, 4f, 3f }, { -20f, 116f, 5f, 5f }, { 15f, 118f, 3f, 4f }, { 0f, 120f, 7f, 4f } };
        for (int i = 0; i < ice.GetLength(0); i++)
            CreateIcePatch(iceRoot, $"IcePatch_Z5_{i + 1:00}", ice[i, 0], ice[i, 1], new Vector3(ice[i, 2], 0.15f, ice[i, 3]), terrain, materials);
        CreateRouteGate(routeRoot, "RouteGate_Z5_Couloir", "Snow Couloir", -10f, 100f, terrain, materials.Snow, new[] { new Vector3(8f, 10f, 3f) });
        CreateZoneCheckpoint(zone, "Checkpoint_Zone5", 4, 8f, 0f, 115f, terrain, materials);
        CreateShrine(zone, "Zone5_Shrine", 22f, 90f, terrain, materials);
        CreateRelicSpawn(relicRoot, "RelicSpawn_Z5_A", 0f, 95f, 5, 0.50f, terrain, 2f);
        CreateHazardSpawn(hazardRoot, "Hazard_Z5_Ice_01", 0f, 88f, 30f, 5, prefabs.Rockfall, terrain);
        CreateHazardSpawn(hazardRoot, "Hazard_Z5_Ice_02", -5f, 110f, 35f, 5, prefabs.Rockfall, terrain);
        CreateItemSpawn(hazardRoot, "ItemDrop_Z5_A", -18f, 92f, 5, prefabs.PlaceholderItem, terrain);
    }

    private static void BuildZone6(GameObject zone, GameObject iceRoot, GameObject relicRoot, GameObject hazardRoot, Stage01Materials materials, Stage01Prefabs prefabs, Terrain terrain)
    {
        CreateIcePatch(iceRoot, "IcePatch_Z6_01", -8f, 122f, new Vector3(5f, 0.15f, 5f), terrain, materials);
        CreateIcePatch(iceRoot, "IcePatch_Z6_02", 12f, 128f, new Vector3(4f, 0.15f, 4f), terrain, materials);

        var flag = CreateChild(zone, "SummitFlag");
        flag.transform.position = TerrainPos(terrain, 0f, 135f, 0f);
        CreateGrappablePrimitive(flag, "FlagPole", PrimitiveType.Cylinder, new Vector3(0.15f, 5f, 0.15f), new Vector3(0f, 2.5f, 0f), materials.Metal);
        CreatePrimitive(flag, "Flag", PrimitiveType.Cube, new Vector3(2f, 1.2f, 0.05f), new Vector3(1f, 5f, 0f), materials.Gold);

        var summit = CreateChild(zone, "Summit_Geometry");
        summit.transform.position = TerrainPos(terrain, 0f, 130f, 0.4f);
        CreatePrimitive(summit, "SummitRuins", PrimitiveType.Cube, new Vector3(20f, 2f, 20f), Vector3.zero, materials.Stone);
        CreateGrappablePrimitive(summit, "SummitAltar", PrimitiveType.Cube, new Vector3(3f, 1f, 3f), new Vector3(0f, 2f, 0f), materials.Temple);

        var goal = CreateChild(zone, "SummitGoal");
        goal.transform.position = TerrainPos(terrain, 0f, 133f, 3f);
        var sphere = goal.AddComponent<SphereCollider>();
        sphere.radius = 10f;
        sphere.isTrigger = true;
        goal.AddComponent<SummitGoalTrigger>();
        Stage01EditorUtil.TrySetTag(goal, "SummitGoal");

        CreateShrine(zone, "Summit_Shrine", -10f, 125f, terrain, materials);
        CreateRelicSpawn(relicRoot, "RelicSpawn_Z6_A", 0f, 130f, 6, 0.40f, terrain, 2f);
        CreateHazardSpawn(hazardRoot, "Hazard_Z6_Rock_01", 5f, 122f, 20f, 6, prefabs.Rockfall, terrain);
    }

    private static Transform[] BuildRespawnCheckpoints(GameObject root, Terrain terrain)
    {
        var specs = new[] { (0f, -80f), (0f, -25f), (0f, 25f), (0f, 115f) };
        var result = new Transform[specs.Length];
        for (int i = 0; i < specs.Length; i++)
        {
            var cp = CreateChild(root, $"Checkpoint_{i + 1:00}");
            cp.transform.position = TerrainPos(terrain, specs[i].Item1, specs[i].Item2, 1.5f);
            result[i] = cp.transform;
        }
        return result;
    }

    private static void ConfigureManagers(GameObject gameManager, Transform[] checkpoints, Stage01Prefabs prefabs)
    {
        var expedition = gameManager.GetComponent<ExpeditionManager>();
        var expSo = new SerializedObject(expedition);
        SetObject(expSo, "_spawnManager", gameManager.GetComponent<SpawnManager>());
        var cps = expSo.FindProperty("_checkpoints");
        cps.arraySize = checkpoints.Length;
        for (int i = 0; i < checkpoints.Length; i++)
            cps.GetArrayElementAtIndex(i).objectReferenceValue = checkpoints[i];
        expSo.ApplyModifiedProperties();

        var spawn = gameManager.GetComponent<SpawnManager>();
        var spawnSo = new SerializedObject(spawn);
        spawnSo.FindProperty("_minRelics").intValue = 3;
        spawnSo.FindProperty("_maxRelics").intValue = 5;
        spawnSo.FindProperty("_hazardDensity").floatValue = 0.4f;
        spawnSo.FindProperty("_routeOpenChance").floatValue = 0.5f;
        var relics = spawnSo.FindProperty("_relicPrefabPool");
        relics.arraySize = prefabs.Relics.Length;
        for (int i = 0; i < prefabs.Relics.Length; i++)
            relics.GetArrayElementAtIndex(i).objectReferenceValue = prefabs.Relics[i];
        spawnSo.ApplyModifiedProperties();
    }

    private static void ConfigureLighting()
    {
        var light = Object.FindFirstObjectByType<Light>();
        if (light == null)
        {
            var go = new GameObject("Directional Light");
            light = go.AddComponent<Light>();
            light.type = LightType.Directional;
        }
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        light.color = new Color(1f, 0.96f, 0.88f);
        light.intensity = 1f;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = new Color(0.78f, 0.85f, 0.91f);
        RenderSettings.fogDensity = 0.0016f;

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 1200f);
    }

    private static void CreateTree(GameObject parent, string name, float x, float z, Terrain terrain, Stage01Materials materials)
    {
        var tree = CreateChild(parent, name);
        tree.transform.position = TerrainPos(terrain, x, z, 0f);
        var trunk = CreatePrimitive(tree, "Trunk", PrimitiveType.Cylinder, new Vector3(0.5f, 3f, 0.5f), new Vector3(0f, 1.5f, 0f), materials.Wood);
        Stage01EditorUtil.TrySetTag(trunk, "Grappable");
        Stage01EditorUtil.TrySetLayer(trunk, "Grappable");
        var rb = trunk.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        CreatePrimitive(tree, "Leaves", PrimitiveType.Sphere, new Vector3(3f, 2f, 3f), new Vector3(0f, 4f, 0f), materials.Forest);
    }

    private static void CreateZoneCheckpoint(GameObject parent, string name, int index, float radius, float x, float z, Terrain terrain, Stage01Materials materials, float yOffset = 0f)
    {
        var cp = CreateChild(parent, name);
        cp.transform.position = TerrainPos(terrain, x, z, yOffset);
        var trigger = cp.AddComponent<ZoneCheckpoint>();
        var so = new SerializedObject(trigger);
        so.FindProperty("_checkpointIndex").intValue = index;
        so.FindProperty("_triggerRadius").floatValue = radius;
        so.ApplyModifiedProperties();
        CreatePrimitive(cp, "CheckpointMarker", PrimitiveType.Cylinder, new Vector3(0.3f, 2f, 0.3f), Vector3.up, materials.Gold);
    }

    private static void CreateShrine(GameObject parent, string name, float x, float z, Terrain terrain, Stage01Materials materials)
    {
        var shrine = CreateChild(parent, name);
        shrine.transform.position = TerrainPos(terrain, x, z, 0f);
        shrine.AddComponent<ReviveShrine>();
        CreatePrimitive(shrine, "ShrineBase", PrimitiveType.Cube, new Vector3(1.2f, 0.2f, 1.2f), new Vector3(0f, 0.1f, 0f), materials.Stone);
        CreatePrimitive(shrine, "ShrinePillar", PrimitiveType.Cylinder, new Vector3(0.3f, 1.5f, 0.3f), new Vector3(0f, 0.95f, 0f), materials.Stone);
        CreatePrimitive(shrine, "ShrineCrystal", PrimitiveType.Sphere, new Vector3(0.6f, 0.6f, 0.6f), new Vector3(0f, 1.9f, 0f), materials.Cyan);
    }

    private static void CreateRouteGate(GameObject root, string name, string routeName, float x, float z, Terrain terrain, Material material, Vector3[] blockerScales)
    {
        var gate = CreateChild(root, name);
        gate.transform.position = TerrainPos(terrain, x, z, 0f);
        var blockers = new GameObject[blockerScales.Length];
        for (int i = 0; i < blockerScales.Length; i++)
        {
            blockers[i] = CreatePrimitive(gate, $"Blocker_{i + 1:00}", PrimitiveType.Cube, blockerScales[i], new Vector3(i * 2f, blockerScales[i].y * 0.5f, 0f), material);
            blockers[i].AddComponent<Rigidbody>().isKinematic = true;
        }

        var routeGate = gate.AddComponent<RouteGate>();
        var so = new SerializedObject(routeGate);
        so.FindProperty("_routeName").stringValue = routeName;
        so.FindProperty("_defaultOpen").boolValue = true;
        var prop = so.FindProperty("_blockers");
        prop.arraySize = blockers.Length;
        for (int i = 0; i < blockers.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = blockers[i];
        so.ApplyModifiedProperties();
    }

    private static void CreateRelicSpawn(GameObject root, string name, float x, float z, int zoneId, float chance, Terrain terrain, float yOffset = 0.5f)
    {
        var go = CreateChild(root, name);
        go.transform.position = TerrainPos(terrain, x, z, yOffset);
        ConfigureSpawnPoint(go.AddComponent<SpawnPoint>(), SpawnLayer.Relic, zoneId, chance, true, null);
    }

    private static void CreateHazardSpawn(GameObject root, string name, float x, float z, float yOffset, int zoneId, GameObject prefab, Terrain terrain)
    {
        var go = CreateChild(root, name);
        go.transform.position = TerrainPos(terrain, x, z, yOffset);
        ConfigureSpawnPoint(go.AddComponent<SpawnPoint>(), SpawnLayer.Hazard, zoneId, 0.4f, false, new[] { prefab });
    }

    private static void CreateItemSpawn(GameObject root, string name, float x, float z, int zoneId, GameObject prefab, Terrain terrain)
    {
        var go = CreateChild(root, name);
        go.transform.position = TerrainPos(terrain, x, z, 0.5f);
        ConfigureSpawnPoint(go.AddComponent<SpawnPoint>(), SpawnLayer.Item, zoneId, 0.5f, false, new[] { prefab });
    }

    private static void CreateIcePatch(GameObject parent, string name, float x, float z, Vector3 scale, Terrain terrain, Stage01Materials materials)
    {
        var patch = CreatePrimitive(parent, name, PrimitiveType.Cube, scale, TerrainPos(terrain, x, z, 0.1f), materials.Ice, worldSpace: true);
        patch.AddComponent<IcePatch>();
        var col = patch.GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
        Stage01EditorUtil.TrySetLayer(patch, "Hazard");
    }

    private static void CreateCollapsible(GameObject parent, string name, float x, float z, float yOffset, Vector3 scale, Terrain terrain, Material material)
    {
        var root = CreateChild(parent, name);
        root.transform.position = TerrainPos(terrain, x, z, yOffset);
        root.AddComponent<CollapsiblePlatform>();
        var trigger = root.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = scale + new Vector3(0f, 1f, 0f);
        trigger.center = Vector3.up * 0.5f;
        CreatePrimitive(root, "Solid", PrimitiveType.Cube, scale, Vector3.zero, material);
        Stage01EditorUtil.TrySetLayer(root, "Hazard");
    }

    private static void CreateTempleTrap(GameObject parent, string name, float x, float z, Terrain terrain, Stage01Materials materials)
    {
        var trap = CreatePrimitive(parent, name, PrimitiveType.Cube, new Vector3(3f, 0.2f, 3f), TerrainPos(terrain, x, z, 0.2f), materials.Temple, worldSpace: true);
        var col = trap.GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
        var pressure = trap.AddComponent<PressurePlateArrow>();
        var arrowA = CreateChild(trap, "ArrowSpawn_A");
        arrowA.transform.localPosition = new Vector3(-4f, 1.5f, 0f);
        var arrowB = CreateChild(trap, "ArrowSpawn_B");
        arrowB.transform.localPosition = new Vector3(4f, 1.5f, 0f);
        var so = new SerializedObject(pressure);
        var points = so.FindProperty("_arrowSpawnPoints");
        points.arraySize = 2;
        points.GetArrayElementAtIndex(0).objectReferenceValue = arrowA.transform;
        points.GetArrayElementAtIndex(1).objectReferenceValue = arrowB.transform;
        so.ApplyModifiedProperties();
        Stage01EditorUtil.TrySetLayer(trap, "Hazard");
    }

    private static GameObject CreateGrappableWorldPrimitive(GameObject parent, string name, PrimitiveType type, float x, float z, float yOffset, Vector3 scale, Material material, Terrain terrain)
    {
        var go = CreatePrimitive(parent, name, type, scale, TerrainPos(terrain, x, z, yOffset), material, worldSpace: true);
        Stage01EditorUtil.TrySetTag(go, "Grappable");
        Stage01EditorUtil.TrySetLayer(go, "Grappable");
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        return go;
    }

    private static GameObject CreateGrappablePrimitive(GameObject parent, string name, PrimitiveType type, Vector3 scale, Vector3 localPosition, Material material)
    {
        var go = CreatePrimitive(parent, name, type, scale, localPosition, material);
        Stage01EditorUtil.TrySetTag(go, "Grappable");
        Stage01EditorUtil.TrySetLayer(go, "Grappable");
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        return go;
    }

    private static GameObject CreatePrimitive(GameObject parent, string name, PrimitiveType type, Vector3 scale, Vector3 position, Material material, bool worldSpace = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent.transform);
        if (worldSpace) go.transform.position = position;
        else go.transform.localPosition = position;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        if (renderer != null) renderer.sharedMaterial = material;
        return go;
    }

    private static void ConfigureSpawnPoint(SpawnPoint spawnPoint, SpawnLayer layer, int zoneId, float chance, bool pickRandom, GameObject[] prefabs)
    {
        var so = new SerializedObject(spawnPoint);
        so.FindProperty("_layer").enumValueIndex = (int)layer;
        so.FindProperty("_zoneId").intValue = zoneId;
        so.FindProperty("_activateChance").floatValue = chance;
        so.FindProperty("_pickRandom").boolValue = pickRandom;
        var prefabProp = so.FindProperty("_spawnPrefabs");
        prefabProp.arraySize = prefabs?.Length ?? 0;
        if (prefabs != null)
        {
            for (int i = 0; i < prefabs.Length; i++)
                prefabProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
        }
        so.ApplyModifiedProperties();
    }

    private static void SetObject(SerializedObject so, string propertyName, Object value)
    {
        var prop = so.FindProperty(propertyName);
        if (prop != null)
            prop.objectReferenceValue = value;
    }

    private static Vector3 TerrainPos(Terrain terrain, float x, float z, float yOffset)
    {
        return new Vector3(x, Stage01EditorUtil.SampleTerrainHeight(terrain, x, z) + yOffset, z);
    }

    private static Vector3 Pos(float x, float z, float y)
    {
        return new Vector3(x, y, z);
    }
}

internal sealed class Stage01Materials
{
    public Material Rock;
    public Material Wood;
    public Material Forest;
    public Material Temple;
    public Material Ice;
    public Material Snow;
    public Material Gold;
    public Material Stone;
    public Material Metal;
    public Material Cyan;

    public static Stage01Materials Load()
    {
        return new Stage01Materials
        {
            Rock = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Rock_Mat", new Color(0.43f, 0.39f, 0.33f)),
            Wood = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Wood_Mat", new Color(0.36f, 0.24f, 0.15f)),
            Forest = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Forest_Mat", new Color(0.18f, 0.35f, 0.15f)),
            Temple = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Temple_Mat", new Color(0.72f, 0.65f, 0.52f)),
            Ice = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Ice_Mat", new Color(0.67f, 0.88f, 1f), 0.95f, 0.1f),
            Snow = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Snow_Mat", new Color(0.91f, 0.96f, 1f)),
            Gold = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Gold_Mat", new Color(1f, 0.84f, 0f)),
            Stone = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Stone_Mat", new Color(0.45f, 0.45f, 0.45f)),
            Metal = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Metal_Mat", new Color(0.75f, 0.75f, 0.75f), 0.5f, 0.4f),
            Cyan = Stage01EditorUtil.GetOrCreateMaterial("Stage01_Cyan_Mat", Color.cyan),
        };
    }
}

internal sealed class Stage01Prefabs
{
    public GameObject Rockfall;
    public GameObject CollapsiblePlatform;
    public GameObject TempleTrap;
    public GameObject PlaceholderItem;
    public GameObject[] Relics;

    public static Stage01Prefabs CreateOrLoad(Stage01Materials materials)
    {
        const string hazardDir = "Assets/Sandbox/Prefabs/Hazards";
        const string itemDir = "Assets/Sandbox/Prefabs/Items";
        Stage01EditorUtil.EnsureAssetFolder(hazardDir);
        Stage01EditorUtil.EnsureAssetFolder(itemDir);

        return new Stage01Prefabs
        {
            Rockfall = CreateRockfallPrefab($"{hazardDir}/Stage01_RockfallTrigger.prefab"),
            CollapsiblePlatform = CreateCollapsiblePrefab($"{hazardDir}/Stage01_CollapsiblePlatform.prefab", materials.Wood),
            TempleTrap = CreateTempleTrapPrefab($"{hazardDir}/Stage01_TempleTrap.prefab", materials.Temple),
            PlaceholderItem = CreateItemPrefab($"{itemDir}/Stage01_FieldSupply.prefab", materials.Gold),
            Relics = LoadRelicPrefabs()
        };
    }

    private static GameObject CreateRockfallPrefab(string path)
    {
        var go = new GameObject("Stage01_RockfallTrigger");
        go.AddComponent<RockfallTrigger>();
        return SavePrefab(go, path);
    }

    private static GameObject CreateCollapsiblePrefab(string path, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Stage01_CollapsiblePlatform";
        go.transform.localScale = new Vector3(4f, 0.4f, 4f);
        go.GetComponent<Renderer>().sharedMaterial = material;
        go.AddComponent<CollapsiblePlatform>();
        var col = go.GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
        return SavePrefab(go, path);
    }

    private static GameObject CreateTempleTrapPrefab(string path, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Stage01_TempleTrap";
        go.transform.localScale = new Vector3(3f, 0.2f, 3f);
        go.GetComponent<Renderer>().sharedMaterial = material;
        var col = go.GetComponent<BoxCollider>();
        if (col != null) col.isTrigger = true;
        var pressure = go.AddComponent<PressurePlateArrow>();
        var arrowA = new GameObject("ArrowSpawn_A");
        arrowA.transform.SetParent(go.transform);
        arrowA.transform.localPosition = new Vector3(-4f, 1.5f, 0f);
        var arrowB = new GameObject("ArrowSpawn_B");
        arrowB.transform.SetParent(go.transform);
        arrowB.transform.localPosition = new Vector3(4f, 1.5f, 0f);

        var so = new SerializedObject(pressure);
        var points = so.FindProperty("_arrowSpawnPoints");
        points.arraySize = 2;
        points.GetArrayElementAtIndex(0).objectReferenceValue = arrowA.transform;
        points.GetArrayElementAtIndex(1).objectReferenceValue = arrowB.transform;
        so.ApplyModifiedProperties();
        return SavePrefab(go, path);
    }

    private static GameObject CreateItemPrefab(string path, Material material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        go.name = "Stage01_FieldSupply";
        go.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
        go.GetComponent<Renderer>().sharedMaterial = material;
        return SavePrefab(go, path);
    }

    private static GameObject[] LoadRelicPrefabs()
    {
        string[] paths =
        {
            "Assets/Sandbox/Prefabs/Relics/GoldenDuckRelic.prefab",
            "Assets/Sandbox/Prefabs/Relics/CrystalCupRelic.prefab",
            "Assets/Sandbox/Prefabs/Relics/GreatStoneSlabRelic.prefab",
            "Assets/Sandbox/Prefabs/Relics/SingingVaseRelic.prefab",
            "Assets/Sandbox/Prefabs/Relics/FloatingSphereRelic.prefab",
            "Assets/Sandbox/Prefabs/Relics/TwinStatueRelic.prefab",
            "Assets/Sandbox/Prefabs/Relics/SlipperyFishStatueRelic.prefab",
            "Assets/Sandbox/Prefabs/Relics/MagneticHelmetRelic.prefab"
        };

        var prefabs = new GameObject[paths.Length];
        for (int i = 0; i < paths.Length; i++)
            prefabs[i] = AssetDatabase.LoadAssetAtPath<GameObject>(paths[i]);
        return prefabs;
    }

    private static GameObject SavePrefab(GameObject go, string path)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
        Object.DestroyImmediate(go);
        return prefab;
    }
}

internal static class Stage01EditorUtil
{
    public static Material GetOrCreateMaterial(string name, Color color, float smoothness = 0.3f, float metallic = 0f)
    {
        const string dir = "Assets/Sandbox/Materials/Stage01";
        EnsureAssetFolder(dir);

        string path = $"{dir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, path);
        }

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        else mat.color = color;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    public static float SampleTerrainHeight(Terrain terrain, float x, float z)
    {
        return terrain != null ? terrain.SampleHeight(new Vector3(x, 0f, z)) : 0f;
    }

    public static void TrySetTag(GameObject go, string tagName)
    {
        try
        {
            go.tag = tagName;
        }
        catch (UnityException)
        {
            Debug.LogWarning($"[Stage01] Tag '{tagName}' is not defined.");
        }
    }

    public static void TrySetLayer(GameObject go, string layerName)
    {
        int layer = LayerMask.NameToLayer(layerName);
        if (layer < 0) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            TrySetLayer(child.gameObject, layerName);
    }

    public static void EnsureAssetFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
}
#endif
