using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

/// <summary>
/// OfflineTestScene.unity を自動生成するエディタスクリプト。
///
/// 使い方:
///   Unity メニュー → ccc → Create Offline Test Scene
///   または
///   Unity メニュー → ccc → Refresh Offline Test Scene (上書き再生成)
///
/// 生成物:
///   Assets/Sandbox/Scene/OfflineTestScene.unity
///
/// 動作要件:
///   - Play するだけで NGO が Host モードで起動
///   - ネット接続不要（UGS/Lobby 不使用）
///   - GDD 全フェーズ（ベースキャンプ→登攀→帰還→リザルト）をオフラインで検証可能
/// </summary>
public static class OfflineSceneCreator
{
    private const string SCENE_PATH  = "Assets/Sandbox/Scene/OfflineTestScene.unity";
    private const string SCENE_NAME  = "OfflineTestScene";

    // ── メニューアイテム ──────────────────────────────────────
    [MenuItem("ccc/Create Offline Test Scene")]
    public static void CreateScene()
    {
        if (File.Exists(SCENE_PATH))
        {
            bool ok = EditorUtility.DisplayDialog(
                "OfflineTestScene",
                $"{SCENE_PATH} は既に存在します。上書きしますか？",
                "上書き", "キャンセル");
            if (!ok) return;
        }
        BuildScene();
    }

    [MenuItem("ccc/Refresh Offline Test Scene")]
    public static void RefreshScene()
    {
        BuildScene();
    }

    // ── シーン構築本体 ────────────────────────────────────────
    private static void BuildScene()
    {
        // 現在のシーンを保存確認
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // タグ・レイヤーを先に追加
        SetupTagsAndLayers();

        // 新規シーン作成
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        scene.name = SCENE_NAME;

        // ライティング設定
        SetupLighting();

        // ── ゲームオブジェクト構築 ──────────────────────────────

        // 1. NetworkManager
        var nmGo = BuildNetworkManager();

        // 2. ゲームシステム群
        var systemsGo  = BuildGameSystems();

        // 3. 地形・環境
        BuildEnvironment();

        // 4. ベースキャンプ
        BuildBasecamp();

        // 5. 山のゾーン（プラットフォーム群）
        BuildMountainPlatforms();

        // 6. 遺物（全8種を各高度に配置）
        BuildRelics();

        // 7. 復活の祠
        BuildReviveShrines();

        // 8. ハザード
        BuildHazards();

        // 9. クライミングポイント
        BuildClimbingPoints();

        // 10. UI キャンバス全体
        BuildUICanvas(systemsGo);

        // 11. EventSystem
        BuildEventSystem();

        // 12. メインカメラ
        BuildCamera();

        // シーン保存
        EditorSceneManager.SaveScene(scene, SCENE_PATH);

        // NGO の GlobalObjectIdHash を確定させるため強制インポート
        // （コードでAddComponent<NetworkObject>したオブジェクトはOnValidateが呼ばれず
        //   GlobalObjectIdHash=0 のままになる。ForceUpdate で再インポートすると
        //   NGO が OnValidate を呼んでハッシュを割り当てる）
        AssetDatabase.ImportAsset(SCENE_PATH, ImportAssetOptions.ForceUpdate);

        // 再保存（ハッシュが書き込まれた状態で保存する）
        var reloadedScene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Additive);
        EditorSceneManager.SaveScene(reloadedScene);
        EditorSceneManager.CloseScene(reloadedScene, false);

        // Build Settings に追加
        AddSceneToBuildSettings();

        Debug.Log($"[OfflineSceneCreator] {SCENE_PATH} を生成しました。Play して動作確認してください。");
        EditorUtility.DisplayDialog("完了", $"OfflineTestScene を生成しました。\n{SCENE_PATH}", "OK");
    }

    // ── タグ & レイヤー ────────────────────────────────────────
    private static void SetupTagsAndLayers()
    {
        // Tags
        var requiredTags = new[] { "ClimbingPoint", "Grappable", "Checkpoint",
                                    "ReturnZone", "DepartureGate", "Relic" };
        foreach (var tag in requiredTags)
            AddTag(tag);

        // Layers (6-15 は GDD §3.5 の定義に従う)
        AddLayer(6,  "Player");
        AddLayer(7,  "Relic");
        AddLayer(8,  "Item");
        AddLayer(9,  "Rope");
        AddLayer(10, "Interactable");
        AddLayer(11, "Hazard");
        AddLayer(12, "Ghost");
        AddLayer(13, "SafeZone");
        AddLayer(14, "Helicopter");
        AddLayer(15, "RagdollBone");
    }

    private static void AddTag(string tag)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>(
            "ProjectSettings/TagManager.asset"));
        var tags = tagManager.FindProperty("tags");
        for (int i = 0; i < tags.arraySize; i++)
            if (tags.GetArrayElementAtIndex(i).stringValue == tag) return;
        tags.InsertArrayElementAtIndex(tags.arraySize);
        tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddLayer(int index, string layerName)
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>(
            "ProjectSettings/TagManager.asset"));
        var layers = tagManager.FindProperty("layers");
        var element = layers.GetArrayElementAtIndex(index);
        if (string.IsNullOrEmpty(element.stringValue))
        {
            element.stringValue = layerName;
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // ── ライティング ──────────────────────────────────────────
    private static void SetupLighting()
    {
        // Ambient light
        RenderSettings.ambientMode    = AmbientMode.Flat;
        RenderSettings.ambientLight   = new Color(0.4f, 0.45f, 0.55f);
        RenderSettings.fog            = true;
        RenderSettings.fogMode        = FogMode.ExponentialSquared;
        RenderSettings.fogDensity     = 0.005f;

        // Directional Light
        var lightGo  = new GameObject("DirectionalLight");
        var light    = lightGo.AddComponent<Light>();
        light.type   = LightType.Directional;
        light.color  = new Color(1f, 0.96f, 0.84f);
        light.intensity = 1.2f;
        light.shadows   = LightShadows.Hard;
        lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
    }

    // ── NetworkManager ────────────────────────────────────────
    private static GameObject BuildNetworkManager()
    {
        var go = new GameObject("NetworkManager");
        var nm = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<UnityTransport>();

        // UnityTransport を NetworkManager に設定
        var nmSo  = new SerializedObject(nm);
        var tProp = nmSo.FindProperty("NetworkConfig.NetworkTransport");
        if (tProp != null)
        {
            tProp.objectReferenceValue = transport;
            nmSo.ApplyModifiedPropertiesWithoutUndo();
        }

        // Player Prefab を設定（存在する場合）
        var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Sandbox/Prefabs/PlayerPrefab.prefab");
        if (playerPrefab != null)
        {
            var netCfgProp = nmSo.FindProperty("NetworkConfig.PlayerPrefab");
            if (netCfgProp != null)
            {
                netCfgProp.objectReferenceValue = playerPrefab;
                nmSo.ApplyModifiedPropertiesWithoutUndo();
            }

            // NetworkPrefabs リストにも追加
            var prefabsProp = nmSo.FindProperty("NetworkConfig.Prefabs.NetworkPrefabsLists");
            // NetworkManager の PrefabHandler 経由で登録するためシンプルに
        }

        // OfflineTestBootstrapper（起動 + デバッグUI）
        go.AddComponent<OfflineTestBootstrapper>();

        // NetworkPlayerSpawner（in-scene NetworkObject）
        var spawnerGo = new GameObject("NetworkPlayerSpawner");
        spawnerGo.transform.SetParent(go.transform);
        spawnerGo.AddComponent<NetworkObject>();        // in-scene NetworkObject として配置
        var spawner = spawnerGo.AddComponent<NetworkPlayerSpawner>();

        // SpawnPoints を設定
        var spawnPt0 = new GameObject("SpawnPoint_0");
        spawnPt0.transform.SetParent(spawnerGo.transform);
        spawnPt0.transform.position = new Vector3(2f, 0.5f, 0f);
        spawnPt0.AddComponent<SpawnPoint>();

        if (playerPrefab != null)
        {
            var spawnerSo = new SerializedObject(spawner);
            var pfProp    = spawnerSo.FindProperty("_playerPrefab");
            if (pfProp != null)
            {
                pfProp.objectReferenceValue = playerPrefab;
                spawnerSo.ApplyModifiedPropertiesWithoutUndo();
            }
            var ptsProp = spawnerSo.FindProperty("_spawnPoints");
            if (ptsProp != null)
            {
                ptsProp.arraySize = 1;
                ptsProp.GetArrayElementAtIndex(0).objectReferenceValue = spawnPt0.transform;
                spawnerSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // NetworkExpeditionSync（in-scene NetworkObject）
        var syncGo = new GameObject("NetworkExpeditionSync");
        syncGo.transform.SetParent(go.transform);
        syncGo.AddComponent<NetworkObject>();
        syncGo.AddComponent<NetworkExpeditionSync>();

        return go;
    }

    // ── ゲームシステム群 ──────────────────────────────────────
    private static GameObject BuildGameSystems()
    {
        var root = new GameObject("GameSystems");

        // ExpeditionManager
        var emGo = new GameObject("ExpeditionManager");
        emGo.transform.SetParent(root.transform);
        emGo.AddComponent<ExpeditionManager>();

        // ScoreTracker
        var stGo = new GameObject("ScoreTracker");
        stGo.transform.SetParent(root.transform);
        stGo.AddComponent<ScoreTracker>();

        // WeatherSystem
        var wsGo = new GameObject("WeatherSystem");
        wsGo.transform.SetParent(root.transform);
        wsGo.AddComponent<WeatherSystem>();

        // HintManager
        var hmGo = new GameObject("HintManager");
        hmGo.transform.SetParent(root.transform);
        hmGo.AddComponent<HintManager>();

        // SaveManager
        var savGo = new GameObject("SaveManager");
        savGo.transform.SetParent(root.transform);
        savGo.AddComponent<SaveManager>();

        // SpawnManager（L1-L5レイヤー制御）
        var spawnMgrGo = new GameObject("SpawnManager");
        spawnMgrGo.transform.SetParent(root.transform);
        spawnMgrGo.AddComponent<SpawnManager>();

        // RopeManager
        var rmGo = new GameObject("RopeManager");
        rmGo.transform.SetParent(root.transform);
        rmGo.AddComponent<RopeManager>();

        // ReturnVoteSystem
        var rvGo = new GameObject("ReturnVoteSystem");
        rvGo.transform.SetParent(root.transform);
        rvGo.AddComponent<ReturnVoteSystem>();

        // HelicopterController（in-scene NetworkObject）
        var heliSysGo = new GameObject("HelicopterController");
        heliSysGo.transform.SetParent(root.transform);
        heliSysGo.transform.position = new Vector3(0f, 80f, 0f); // 上空から降下してくる
        heliSysGo.AddComponent<NetworkObject>();
        var heli = heliSysGo.AddComponent<HelicopterController>();
        heliSysGo.layer = LayerMask.NameToLayer("Helicopter");

        // HelipadMarker（着陸地点）
        var helipadGo = new GameObject("HelipadMarker");
        helipadGo.transform.SetParent(root.transform);
        helipadGo.transform.position = new Vector3(15f, 0.05f, 0f);
        var helipadFloor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        helipadFloor.transform.SetParent(helipadGo.transform);
        helipadFloor.transform.localPosition = Vector3.zero;
        helipadFloor.transform.localScale    = new Vector3(6f, 0.05f, 6f);
        SetMaterialColor(helipadFloor, new Color(0.9f, 0.7f, 0.1f)); // 黄色

        return root;
    }

    // ── 地形 ─────────────────────────────────────────────────
    private static void BuildEnvironment()
    {
        var envGo = new GameObject("Environment");

        // ── ベースキャンプ床（Ground）─────────────────────────
        var ground = CreatePlatform("Ground", Vector3.zero, new Vector3(80f, 0.5f, 80f),
                                    new Color(0.35f, 0.55f, 0.25f)); // 草色
        ground.transform.SetParent(envGo.transform);
        ground.layer = 0;

        // ── 境界壁（落下防止）─────────────────────────────────
        CreateWall("Wall_N",   new Vector3(0f,  5f,  40f), new Vector3(80f, 10f, 0.5f), envGo.transform);
        CreateWall("Wall_S",   new Vector3(0f,  5f, -40f), new Vector3(80f, 10f, 0.5f), envGo.transform);
        CreateWall("Wall_E",   new Vector3(40f, 5f,   0f), new Vector3(0.5f,10f, 80f),  envGo.transform);
        CreateWall("Wall_W",   new Vector3(-40f,5f,   0f), new Vector3(0.5f,10f, 80f),  envGo.transform);

        // ── 山岳ゾーン（各ゾーン別の標高床プラットフォームは BuildMountainPlatforms で）─
    }

    private static void BuildMountainPlatforms()
    {
        var mountain = new GameObject("Mountain");

        // Zone1: 森林帯 (Y=5-20m)  ──────────────────────────────
        CreatePlatform("Zone1_Rock1", new Vector3(-10f, 5f, 15f),   new Vector3(8f,0.5f,8f),
                       new Color(0.5f,0.4f,0.3f), mountain.transform)
            .tag = "Grappable";
        CreatePlatform("Zone1_Rock2", new Vector3(-5f, 12f, 18f),   new Vector3(6f,0.5f,6f),
                       new Color(0.5f,0.4f,0.3f), mountain.transform)
            .tag = "Grappable";
        CreatePlatform("Zone1_Rock3", new Vector3(0f, 18f, 22f),    new Vector3(7f,0.5f,7f),
                       new Color(0.5f,0.4f,0.3f), mountain.transform)
            .tag = "Grappable";

        // Zone2: 岩場帯 (Y=25-45m) ─────────────────────────────
        CreatePlatform("Zone2_Rock1", new Vector3(5f, 25f, 20f),    new Vector3(7f,0.5f,7f),
                       new Color(0.6f,0.5f,0.4f), mountain.transform)
            .tag = "Grappable";
        CreatePlatform("Zone2_Rock2", new Vector3(10f, 35f, 18f),   new Vector3(6f,0.5f,6f),
                       new Color(0.6f,0.5f,0.4f), mountain.transform)
            .tag = "Grappable";
        CreatePlatform("Zone2_Rock3", new Vector3(8f, 45f, 15f),    new Vector3(5f,0.5f,5f),
                       new Color(0.6f,0.5f,0.4f), mountain.transform)
            .tag = "Grappable";

        // Zone3: 急壁 (Y=50-70m) ───────────────────────────────
        CreatePlatform("Zone3_Ledge1",new Vector3(5f, 55f, 10f),    new Vector3(5f,0.5f,5f),
                       new Color(0.55f,0.45f,0.35f), mountain.transform)
            .tag = "Grappable";
        CreatePlatform("Zone3_Ledge2",new Vector3(0f, 65f, 8f),     new Vector3(4f,0.5f,4f),
                       new Color(0.55f,0.45f,0.35f), mountain.transform)
            .tag = "Grappable";

        // Zone4: 神殿遺跡 (Y=70-85m) ───────────────────────────
        CreatePlatform("Zone4_Temple",new Vector3(-5f, 70f, 5f),    new Vector3(12f,0.5f,12f),
                       new Color(0.7f,0.65f,0.5f), mountain.transform)
            .tag = "Grappable";

        // Zone5: 氷壁 (Y=90-110m) ──────────────────────────────
        CreatePlatform("Zone5_Ice1",  new Vector3(-8f, 90f, 0f),    new Vector3(6f,0.5f,6f),
                       new Color(0.75f,0.9f,1f), mountain.transform)
            .tag = "Grappable";
        CreatePlatform("Zone5_Ice2",  new Vector3(-5f, 105f, -5f),  new Vector3(5f,0.5f,5f),
                       new Color(0.8f,0.95f,1f), mountain.transform)
            .tag = "Grappable";

        // Zone6: 山頂 (Y=120m) ─────────────────────────────────
        CreatePlatform("Zone6_Summit",new Vector3(0f, 120f, 0f),    new Vector3(10f,0.5f,10f),
                       new Color(0.9f,0.9f,0.95f), mountain.transform)
            .tag = "Grappable";

        // 崖の足場を繋ぐ傾斜路（簡易）
        CreateRamp("Ramp_01", new Vector3(-7f, 9f, 16f),   Quaternion.Euler(20f, 0f, 0f),
                   new Vector3(4f, 0.3f, 12f), new Color(0.45f,0.38f,0.28f), mountain.transform);
        CreateRamp("Ramp_02", new Vector3(3f, 30f, 18f),   Quaternion.Euler(35f, 10f, 0f),
                   new Vector3(3f, 0.3f, 10f), new Color(0.5f,0.42f,0.32f), mountain.transform);
    }

    // ── ベースキャンプ ────────────────────────────────────────
    private static void BuildBasecamp()
    {
        var basecamp = new GameObject("Basecamp");

        // ── 出発ゲート ──────────────────────────────────────
        var gateGo = new GameObject("DepartureGate");
        gateGo.transform.SetParent(basecamp.transform);
        gateGo.transform.position = new Vector3(0f, 0f, 10f);
        var dg = gateGo.AddComponent<DepartureGate>();

        // ゲートのビジュアル（簡易アーチ）
        var gatePillarL = CreatePlatform("Pillar_L", new Vector3(-4f, 2f, 0f),
                            new Vector3(0.5f, 4f, 0.5f), new Color(0.6f,0.5f,0.4f));
        var gatePillarR = CreatePlatform("Pillar_R", new Vector3(4f, 2f, 0f),
                            new Vector3(0.5f, 4f, 0.5f), new Color(0.6f,0.5f,0.4f));
        var gateLintel  = CreatePlatform("Lintel",   new Vector3(0f, 4.3f, 0f),
                            new Vector3(8.5f, 0.5f, 0.5f), new Color(0.6f,0.5f,0.4f));
        gatePillarL.transform.SetParent(gateGo.transform);
        gatePillarR.transform.SetParent(gateGo.transform);
        gateLintel.transform.SetParent(gateGo.transform);

        // ── ReturnZone ──────────────────────────────────────
        var rzGo = new GameObject("ReturnZone");
        rzGo.transform.SetParent(basecamp.transform);
        rzGo.transform.position = new Vector3(0f, 0f, -8f);
        rzGo.AddComponent<NetworkObject>();     // in-scene NetworkObject
        rzGo.AddComponent<ReturnZone>();
        rzGo.tag = "ReturnZone";

        // ReturnZone のビジュアル（半透明グリーンの床）
        var rzFloor = CreatePlatform("ReturnZone_Floor", Vector3.zero,
                        new Vector3(10f, 0.1f, 10f), new Color(0f, 1f, 0.3f, 0.3f));
        rzFloor.transform.SetParent(rzGo.transform);
        rzFloor.transform.localPosition = new Vector3(0f, 0f, 0f);

        // ── ショップカウンター ──────────────────────────────
        var shopCounter = CreatePlatform("ShopCounter", new Vector3(-15f, 0.75f, 5f),
                            new Vector3(8f, 1.5f, 2f), new Color(0.5f,0.35f,0.2f));
        shopCounter.transform.SetParent(basecamp.transform);

        // ショップ看板テキスト (3Dテキストで代替)
        var shopLabelGo = new GameObject("ShopSignLabel");
        shopLabelGo.transform.SetParent(basecamp.transform);
        shopLabelGo.transform.position = new Vector3(-15f, 2.5f, 5f);
        shopLabelGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        // ── 情報ボード ──────────────────────────────────────
        var boardGo = CreatePlatform("WeatherBoard", new Vector3(-12f, 1.5f, -3f),
                        new Vector3(3f, 2f, 0.2f), new Color(0.3f,0.2f,0.1f));
        boardGo.transform.SetParent(basecamp.transform);
    }

    // ── 遺物（全8種）────────────────────────────────────────
    private static void BuildRelics()
    {
        var relicRoot = new GameObject("Relics");

        // Prefab パスとスポーン位置の対応
        var relicDefs = new (string prefabPath, Vector3 pos, string name)[]
        {
            ("Assets/Sandbox/Prefabs/Relics/GoldenDuckRelic.prefab",
                new Vector3(-5f, 19f, 22f),    "GoldenDuck_Zone1"),
            ("Assets/Sandbox/Prefabs/Relics/CrystalCupRelic.prefab",
                new Vector3(8f, 46f, 15.5f),   "CrystalCup_Zone2"),
            ("Assets/Sandbox/Prefabs/Relics/GreatStoneSlabRelic.prefab",
                new Vector3(6f, 56f, 10.5f),   "GreatStoneSlab_Zone3"),
            ("Assets/Sandbox/Prefabs/Relics/SingingVaseRelic.prefab",
                new Vector3(-4f, 71f, 5.5f),   "SingingVase_Zone4"),
            ("Assets/Sandbox/Prefabs/Relics/FloatingSphereRelic.prefab",
                new Vector3(-7f, 91f, 0.5f),   "FloatingSphere_Zone5"),
            ("Assets/Sandbox/Prefabs/Relics/TwinStatueRelic.prefab",
                new Vector3(6f, 36f, 18.5f),   "TwinStatue_Zone2"),
            ("Assets/Sandbox/Prefabs/Relics/SlipperyFishStatueRelic.prefab",
                new Vector3(0f, 66f, 8.5f),    "SlipperyFish_Zone3"),
            ("Assets/Sandbox/Prefabs/Relics/MagneticHelmetRelic.prefab",
                new Vector3(-4f, 106f, -4.5f), "MagneticHelmet_Zone5"),
        };

        foreach (var (path, pos, objName) in relicDefs)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            GameObject relicGo;
            if (prefab != null)
            {
                relicGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                relicGo.name = objName;
            }
            else
            {
                // Prefab がない場合はプリミティブで代替
                relicGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                relicGo.name = $"{objName} (placeholder)";
                relicGo.transform.localScale = Vector3.one * 0.8f;
                SetMaterialColor(relicGo, new Color(1f, 0.85f, 0.1f));
                if (relicGo.GetComponent<RelicBase>() == null)
                    Debug.LogWarning($"[Creator] {path} が見つかりません。RelicBase 未設定のプレースホルダー。");
            }
            relicGo.transform.position = pos;
            relicGo.transform.SetParent(relicRoot.transform);
            relicGo.layer = LayerMask.NameToLayer("Relic");
        }
    }

    // ── 復活の祠 ─────────────────────────────────────────────
    private static void BuildReviveShrines()
    {
        var shrineRoot = new GameObject("ReviveShrines");

        var shrinePositions = new (Vector3 pos, string name)[]
        {
            (new Vector3(-12f, 19f, 22f), "Shrine_Zone1"),
            (new Vector3(12f, 45.5f, 15f), "Shrine_Zone2"),
            (new Vector3(-10f, 70.5f, 5f), "Shrine_Zone4"),
        };

        foreach (var (pos, objName) in shrinePositions)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(shrineRoot.transform);
            go.transform.position = pos;

            // ビジュアル（祭壇風）
            var altar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            altar.transform.SetParent(go.transform);
            altar.transform.localPosition = Vector3.zero;
            altar.transform.localScale    = new Vector3(0.8f, 0.5f, 0.8f);
            SetMaterialColor(altar, new Color(0.3f, 0.5f, 0.9f)); // 青白い

            // Trigger Collider
            var trigger = new GameObject("ShrineCollider");
            trigger.transform.SetParent(go.transform);
            trigger.transform.localPosition = new Vector3(0f, 0.5f, 0f);
            var col = trigger.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius    = 1.5f;
            trigger.layer = LayerMask.NameToLayer("Interactable");

            go.AddComponent<ReviveShrine>();
        }
    }

    // ── ハザード ─────────────────────────────────────────────
    private static void BuildHazards()
    {
        var hazardRoot = new GameObject("Hazards");

        // 落石トリガー（Zone2）
        BuildRockfallTrigger("RockfallTrigger_Zone2",
            new Vector3(6f, 26f, 19f), hazardRoot.transform);

        // 落石トリガー（Zone3）
        BuildRockfallTrigger("RockfallTrigger_Zone3",
            new Vector3(4f, 46f, 14f), hazardRoot.transform);

        // 氷面パッチ（Zone2）
        BuildIcePatch("IcePatch_Zone2",
            new Vector3(10f, 35.5f, 17f), new Vector3(4f, 0.05f, 4f), hazardRoot.transform);

        // 氷面パッチ（Zone5）
        BuildIcePatch("IcePatch_Zone5",
            new Vector3(-8f, 90.5f, 0f), new Vector3(5f, 0.05f, 5f), hazardRoot.transform);

        // 崩れる足場（Zone2）
        BuildCollapsiblePlatform("CollapsiblePlatform_Zone2",
            new Vector3(5f, 25.3f, 20.5f), new Vector3(5f, 0.3f, 5f), hazardRoot.transform);

        // 神殿トラップ（Zone4）
        BuildTempleTrap("TempleTrap_Zone4",
            new Vector3(-5f, 70.5f, 5f), hazardRoot.transform);
    }

    private static void BuildRockfallTrigger(string name, Vector3 pos, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;

        var col = go.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size      = new Vector3(4f, 3f, 4f);

        go.AddComponent<RockfallTrigger>();
        go.layer = LayerMask.NameToLayer("Hazard");
    }

    private static void BuildIcePatch(string name, Vector3 pos, Vector3 size, Transform parent)
    {
        var go = CreatePlatform(name, pos, size, new Color(0.8f, 0.95f, 1f, 0.8f));
        go.transform.SetParent(parent);
        go.AddComponent<IcePatch>();
        go.layer = LayerMask.NameToLayer("Hazard");
    }

    private static void BuildCollapsiblePlatform(string name, Vector3 pos, Vector3 size, Transform parent)
    {
        var go = CreatePlatform(name, pos, size, new Color(0.6f, 0.5f, 0.35f));
        go.transform.SetParent(parent);
        go.AddComponent<CollapsiblePlatform>();
        go.layer = 0;
    }

    private static void BuildTempleTrap(string name, Vector3 pos, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.AddComponent<PressurePlateArrow>();
        go.layer = LayerMask.NameToLayer("Hazard");
    }

    // ── クライミングポイント ──────────────────────────────────
    private static void BuildClimbingPoints()
    {
        var cpRoot = new GameObject("ClimbingPoints");

        // Zone1 (Y=5-20m)
        BuildClimbingPoint("CP_Zone1_A", new Vector3(-8f,  7f, 16f), cpRoot.transform);
        BuildClimbingPoint("CP_Zone1_B", new Vector3(-6f, 10f, 18f), cpRoot.transform);
        BuildClimbingPoint("CP_Zone1_C", new Vector3(-3f, 15f, 20f), cpRoot.transform);

        // Zone2 (Y=25-45m)
        BuildClimbingPoint("CP_Zone2_A", new Vector3(4f, 27f, 19f), cpRoot.transform);
        BuildClimbingPoint("CP_Zone2_B", new Vector3(7f, 32f, 17f), cpRoot.transform);
        BuildClimbingPoint("CP_Zone2_C", new Vector3(9f, 40f, 16f), cpRoot.transform);

        // Zone3 (Y=50-70m)
        BuildClimbingPoint("CP_Zone3_A", new Vector3(4f, 52f, 12f), cpRoot.transform);
        BuildClimbingPoint("CP_Zone3_B", new Vector3(2f, 60f, 9f),  cpRoot.transform);

        // Zone4 (Y=70-85m)
        BuildClimbingPoint("CP_Zone4_A", new Vector3(-7f, 72f, 6f), cpRoot.transform);

        // Zone5 (Y=90-110m)
        BuildClimbingPoint("CP_Zone5_A", new Vector3(-9f, 95f, 1f), cpRoot.transform);
        BuildClimbingPoint("CP_Zone5_B", new Vector3(-6f,108f,-3f), cpRoot.transform);
    }

    private static void BuildClimbingPoint(string name, Vector3 pos, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        go.tag   = "ClimbingPoint";
        go.layer = LayerMask.NameToLayer("Interactable");

        var col      = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius    = 0.3f;

        go.AddComponent<GrabPoint>();

        // ビジュアル（小さな黄色の球）
        var viz = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        viz.transform.SetParent(go.transform);
        viz.transform.localPosition = Vector3.zero;
        viz.transform.localScale    = Vector3.one * 0.25f;
        SetMaterialColor(viz, Color.yellow);
        Object.DestroyImmediate(viz.GetComponent<SphereCollider>()); // 不要なコライダーを削除
    }

    // ── UI キャンバス全体 ─────────────────────────────────────
    private static void BuildUICanvas(GameObject systemsGo)
    {
        var canvasGo = new GameObject("UIRoot");
        var canvas   = canvasGo.AddComponent<Canvas>();
        canvas.renderMode        = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder      = 0;
        canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGo.AddComponent<GraphicRaycaster>();

        // ── フェードオーバーレイ ──────────────────────────────
        var fadeGo   = new GameObject("FadeOverlay");
        fadeGo.transform.SetParent(canvasGo.transform, false);
        var fadeImg  = fadeGo.AddComponent<Image>();
        fadeImg.color = Color.black;
        var fadeCg   = fadeGo.AddComponent<CanvasGroup>();
        fadeCg.alpha = 0f;
        StretchFull(fadeGo.GetComponent<RectTransform>());

        // ExpeditionManager の fadeCanvas フィールドに設定
        var emList = Object.FindObjectsByType<ExpeditionManager>(FindObjectsSortMode.None);
        if (emList.Length > 0)
        {
            var emSo = new SerializedObject(emList[0]);
            var fcProp = emSo.FindProperty("_fadeCanvas");
            if (fcProp != null) { fcProp.objectReferenceValue = fadeCg; emSo.ApplyModifiedPropertiesWithoutUndo(); }
        }

        // ── Expedition HUD ──────────────────────────────────
        BuildExpeditionHUD(canvasGo.transform);

        // ── ResultScreen ──────────────────────────────────
        BuildResultScreen(canvasGo.transform);

        // ── BasecampShop UI ──────────────────────────────
        BuildBasecampShopUI(canvasGo.transform);

        // ── ReturnZone カウントダウン UI ──────────────────
        BuildReturnZoneUI(canvasGo.transform);

        // ── ヘリ搭乗タイマー UI ────────────────────────────
        BuildHelicopterUI(canvasGo.transform);

        // ── オフラインデバッグ注記 ────────────────────────
        BuildDebugHint(canvasGo.transform);
    }

    private static void BuildExpeditionHUD(Transform uiRoot)
    {
        var hudGo = new GameObject("ExpeditionHUD");
        hudGo.transform.SetParent(uiRoot, false);

        var hud = hudGo.AddComponent<ExpeditionHUD>();

        // タイマーラベル
        var timerTf = CreateTmpText(hudGo.transform, "TimerLabel", "00:00.00",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(180f, 50f), new Vector2(100f, -30f), 22);

        // チェックポイントラベル
        var cpTf = CreateTmpText(hudGo.transform, "CheckpointLabel", "CP 0/4",
            new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(180f, 30f), new Vector2(100f, -65f), 16);

        // スタミナバー
        var staminaGo = new GameObject("StaminaBar");
        staminaGo.transform.SetParent(hudGo.transform, false);
        var staminaSlider = staminaGo.AddComponent<Slider>();
        var staminaRt     = staminaGo.GetComponent<RectTransform>();
        staminaRt.anchorMin = new Vector2(0f, 1f);
        staminaRt.anchorMax = new Vector2(0f, 1f);
        staminaRt.sizeDelta = new Vector2(180f, 18f);
        staminaRt.anchoredPosition = new Vector2(100f, -95f);

        var staminaBgGo = new GameObject("Background");
        staminaBgGo.transform.SetParent(staminaGo.transform, false);
        var staminaBg = staminaBgGo.AddComponent<Image>();
        staminaBg.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        StretchFull(staminaBg.rectTransform);

        var staminaFillArea = new GameObject("Fill Area");
        staminaFillArea.transform.SetParent(staminaGo.transform, false);
        var faRt = staminaFillArea.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero; faRt.anchorMax = Vector2.one;
        faRt.offsetMin = new Vector2(5f, 5f); faRt.offsetMax = new Vector2(-5f, -5f);

        var staminaFillGo = new GameObject("Fill");
        staminaFillGo.transform.SetParent(staminaFillArea.transform, false);
        var fillImg  = staminaFillGo.AddComponent<Image>();
        fillImg.color = Color.green;
        var fillRt = fillImg.rectTransform;
        fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;

        staminaSlider.fillRect = fillRt;
        staminaSlider.value    = 1f;

        // ロープ状態インジケーター（小さなアイコン）
        var ropeIndGo = new GameObject("RopeIndicator");
        ropeIndGo.transform.SetParent(hudGo.transform, false);
        var ropeImg = ropeIndGo.AddComponent<Image>();
        ropeImg.color = Color.white;
        var ropeRt = ropeImg.rectTransform;
        ropeRt.anchorMin = new Vector2(0f, 1f);
        ropeRt.anchorMax = new Vector2(0f, 1f);
        ropeRt.sizeDelta = new Vector2(30f, 30f);
        ropeRt.anchoredPosition = new Vector2(30f, -118f);

        // 警告ラベル
        var warnTf = CreateTmpText(hudGo.transform, "WarningLabel", "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(400f, 50f), new Vector2(0f, -50f), 24);

        // 遺物リスト親
        var relicListGo = new GameObject("RelicList");
        relicListGo.transform.SetParent(hudGo.transform, false);
        var rlRt = relicListGo.AddComponent<RectTransform>();
        rlRt.anchorMin = new Vector2(1f, 1f);
        rlRt.anchorMax = new Vector2(1f, 1f);
        rlRt.sizeDelta = new Vector2(200f, 200f);
        rlRt.anchoredPosition = new Vector2(-110f, -110f);
        relicListGo.AddComponent<VerticalLayoutGroup>();

        // ExpeditionHUD の SerializedField を設定
        var hudSo = new SerializedObject(hud);
        SetProp(hudSo, "_timerLabel",     timerTf.GetComponent<TextMeshProUGUI>());
        SetProp(hudSo, "_checkpointLabel", cpTf.GetComponent<TextMeshProUGUI>());
        SetProp(hudSo, "_staminaBar",     staminaSlider);
        SetProp(hudSo, "_staminaFill",    fillImg);
        SetProp(hudSo, "_ropeIndicator",  ropeImg);
        SetProp(hudSo, "_relicListParent", relicListGo.transform);
        SetProp(hudSo, "_warningLabel",   warnTf.GetComponent<TextMeshProUGUI>());
        hudSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void BuildResultScreen(Transform uiRoot)
    {
        var rsGo = new GameObject("ResultScreen");
        rsGo.transform.SetParent(uiRoot, false);
        var rs = rsGo.AddComponent<ResultScreen>();

        // パネル
        var panel = new GameObject("Panel");
        panel.transform.SetParent(rsGo.transform, false);
        var panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0f, 0f, 0.85f);
        StretchFull(panel.GetComponent<RectTransform>());
        panel.SetActive(false);

        // チームスコア
        var teamScoreTf = CreateTmpText(panel.transform, "TeamScoreLabel", "TEAM SCORE: 0",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(500f, 60f), new Vector2(0f, -60f), 36);

        var relicSumTf = CreateTmpText(panel.transform, "RelicSummaryLabel", "遺物: 0個",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(400f, 40f), new Vector2(0f, -115f), 22);

        var clearTimeTf = CreateTmpText(panel.transform, "ClearTimeLabel", "タイム: 00:00",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(400f, 40f), new Vector2(0f, -155f), 20);

        // 個人スコア行の親
        var playerRowParent = new GameObject("PlayerRowParent");
        playerRowParent.transform.SetParent(panel.transform, false);
        var prRt = playerRowParent.AddComponent<RectTransform>();
        prRt.anchorMin = new Vector2(0.5f, 0.5f);
        prRt.anchorMax = new Vector2(0.5f, 0.5f);
        prRt.sizeDelta = new Vector2(500f, 200f);
        prRt.anchoredPosition = new Vector2(0f, 30f);
        playerRowParent.AddComponent<VerticalLayoutGroup>();

        // 称号行の親
        var titleRowParent = new GameObject("TitleRowParent");
        titleRowParent.transform.SetParent(panel.transform, false);
        var trRt = titleRowParent.AddComponent<RectTransform>();
        trRt.anchorMin = new Vector2(0.5f, 0f);
        trRt.anchorMax = new Vector2(0.5f, 0f);
        trRt.sizeDelta = new Vector2(500f, 100f);
        trRt.anchoredPosition = new Vector2(0f, 120f);
        titleRowParent.AddComponent<VerticalLayoutGroup>();

        // Retry / ReturnBase ボタン
        var retryBtn  = BuildButton(panel.transform, "RetryButton",  "もう一度",
            new Vector2(-80f, 50f),  new Vector2(0.5f, 0f));
        var returnBtn = BuildButton(panel.transform, "ReturnButton", "メニューへ",
            new Vector2(80f, 50f),   new Vector2(0.5f, 0f));

        // ResultScreen の fields を設定
        var rsSo = new SerializedObject(rs);
        SetProp(rsSo, "_panel",             panel);
        SetProp(rsSo, "_teamScoreLabel",    teamScoreTf.GetComponent<TextMeshProUGUI>());
        SetProp(rsSo, "_relicSummaryLabel", relicSumTf.GetComponent<TextMeshProUGUI>());
        SetProp(rsSo, "_clearTimeLabel",    clearTimeTf.GetComponent<TextMeshProUGUI>());
        SetProp(rsSo, "_playerRowParent",   playerRowParent.transform);
        SetProp(rsSo, "_titleRowParent",    titleRowParent.transform);
        SetProp(rsSo, "_retryButton",       retryBtn);
        SetProp(rsSo, "_returnBaseButton",  returnBtn);
        rsSo.ApplyModifiedPropertiesWithoutUndo();

        // ExpeditionManager に ResultScreen を設定
        var emList = Object.FindObjectsByType<ExpeditionManager>(FindObjectsSortMode.None);
        if (emList.Length > 0)
        {
            var emSo  = new SerializedObject(emList[0]);
            var rsProp = emSo.FindProperty("_resultScreen");
            if (rsProp != null) { rsProp.objectReferenceValue = rs; emSo.ApplyModifiedPropertiesWithoutUndo(); }
        }
    }

    private static void BuildBasecampShopUI(Transform uiRoot)
    {
        var shopGo = new GameObject("BasecampShopUI");
        shopGo.transform.SetParent(uiRoot, false);

        // 半透明パネル
        var panel = new GameObject("ShopPanel");
        panel.transform.SetParent(shopGo.transform, false);
        var panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.1f, 0.08f, 0.05f, 0.92f);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.sizeDelta = new Vector2(340f, 0f);
        panelRt.anchoredPosition = new Vector2(170f, 0f);

        // タイトル
        CreateTmpText(panel.transform, "ShopTitle", "BASECAMP SHOP",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(300f, 50f), new Vector2(0f, -30f), 26);

        // 予算表示
        var budgetTf = CreateTmpText(panel.transform, "BudgetLabel", "予算: 100 pt",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(300f, 35f), new Vector2(0f, -75f), 20);

        // アイテムリスト親（スクロール可能な VerticalLayout）
        var itemListGo = new GameObject("ItemList");
        itemListGo.transform.SetParent(panel.transform, false);
        var ilRt = itemListGo.AddComponent<RectTransform>();
        ilRt.anchorMin = new Vector2(0f, 0f);
        ilRt.anchorMax = new Vector2(1f, 1f);
        ilRt.offsetMin = new Vector2(10f, 60f);
        ilRt.offsetMax = new Vector2(-10f, -110f);
        var vl = itemListGo.AddComponent<VerticalLayoutGroup>();
        vl.spacing     = 4f;
        vl.padding     = new RectOffset(4, 4, 4, 4);
        vl.childControlHeight  = false;
        vl.childControlWidth   = true;

        // 出発ボタン
        var departBtn = BuildButton(panel.transform, "DepartButton", "出 発",
            new Vector2(0f, 40f), new Vector2(0.5f, 0f));

        // 天候ラベル・ルート状況
        var weatherTf = CreateTmpText(panel.transform, "WeatherLabel", "天候: 晴れ",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(300f, 30f), new Vector2(0f, 80f), 16);
        var routeTf = CreateTmpText(panel.transform, "RouteStatusLabel", "ルート: 正常",
            new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(300f, 30f), new Vector2(0f, 50f), 14);

        // BasecampShop コンポーネント（GameSystems の中に追加）
        var shopSystems = Object.FindObjectsByType<ExpeditionManager>(FindObjectsSortMode.None);
        BasecampShop shopComp = null;
        if (shopSystems.Length > 0)
        {
            shopComp = shopSystems[0].gameObject.AddComponent<BasecampShop>();
        }
        else
        {
            var shopCompGo = new GameObject("BasecampShop_Fallback");
            shopCompGo.transform.SetParent(shopGo.transform, false);
            shopComp = shopCompGo.AddComponent<BasecampShop>();
        }

        if (shopComp != null)
        {
            var shopSo = new SerializedObject(shopComp);
            SetProp(shopSo, "_shopPanel",       panel);
            SetProp(shopSo, "_budgetLabel",     budgetTf.GetComponent<TextMeshProUGUI>());
            SetProp(shopSo, "_itemListParent",  itemListGo.transform);
            SetProp(shopSo, "_weatherLabel",    weatherTf.GetComponent<TextMeshProUGUI>());
            SetProp(shopSo, "_routeStatusLabel",routeTf.GetComponent<TextMeshProUGUI>());
            SetProp(shopSo, "_departButton",    departBtn);
            shopSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void BuildReturnZoneUI(Transform uiRoot)
    {
        var rzUiGo = new GameObject("ReturnZoneUI");
        rzUiGo.transform.SetParent(uiRoot, false);

        var panel = new GameObject("CountdownPanel");
        panel.transform.SetParent(rzUiGo.transform, false);
        var panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0f, 0.5f, 0.2f, 0.75f);
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0f);
        panelRt.anchorMax = new Vector2(0.5f, 0f);
        panelRt.sizeDelta = new Vector2(400f, 80f);
        panelRt.anchoredPosition = new Vector2(0f, 80f);
        panel.SetActive(false);

        var countdownTf = CreateTmpText(panel.transform, "CountdownText", "帰還カウントダウン: 120秒",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(380f, 40f), new Vector2(0f, 15f), 22);
        var statusTf = CreateTmpText(panel.transform, "StatusText", "",
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(380f, 30f), new Vector2(0f, -15f), 16);

        // ReturnZone コンポーネントに UI を接続
        var rzList = Object.FindObjectsByType<ReturnZone>(FindObjectsSortMode.None);
        if (rzList.Length > 0)
        {
            var rzSo = new SerializedObject(rzList[0]);
            SetProp(rzSo, "_countdownPanel", panel);
            SetProp(rzSo, "_countdownText", countdownTf.GetComponent<TextMeshProUGUI>());
            SetProp(rzSo, "_statusText",    statusTf.GetComponent<TextMeshProUGUI>());
            rzSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void BuildHelicopterUI(Transform uiRoot)
    {
        var heliUiGo = new GameObject("HelicopterBoardingUI");
        heliUiGo.transform.SetParent(uiRoot, false);

        var timerTf = CreateTmpText(heliUiGo.transform, "BoardingTimerText", "",
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(300f, 50f), new Vector2(0f, -20f), 28);

        // HelicopterController に接続
        var heliList = Object.FindObjectsByType<HelicopterController>(FindObjectsSortMode.None);
        if (heliList.Length > 0)
        {
            var heliSo = new SerializedObject(heliList[0]);
            SetProp(heliSo, "_boardingTimerText", timerTf.GetComponent<TextMeshProUGUI>());
            heliSo.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void BuildDebugHint(Transform uiRoot)
    {
        // 右下に操作ヒントを表示
        CreateTmpText(uiRoot, "DebugHint",
            "WASD: 移動  Space: ジャンプ  Shift: ダッシュ  E: インタラクト\n" +
            "左クリック: 掴む/拾う  右クリック: 投げる  G: 置く  Tab: インベントリ",
            new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(500f, 50f), new Vector2(-260f, 35f), 11,
            TextAlignmentOptions.Right);
    }

    // ── EventSystem ───────────────────────────────────────────
    private static void BuildEventSystem()
    {
        var esGo = new GameObject("EventSystem");
        esGo.AddComponent<EventSystem>();
        esGo.AddComponent<StandaloneInputModule>();
    }

    // ── Camera ───────────────────────────────────────────────
    private static void BuildCamera()
    {
        var camGo  = new GameObject("MainCamera");
        camGo.tag  = "MainCamera";
        var cam    = camGo.AddComponent<Camera>();
        cam.clearFlags        = CameraClearFlags.Skybox;
        cam.fieldOfView       = 60f;
        cam.nearClipPlane     = 0.1f;
        cam.farClipPlane      = 500f;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 2f, -8f);
        camGo.transform.rotation = Quaternion.Euler(10f, 0f, 0f);
        // 注: ExplorerCameraLook は PlayerPrefab からランタイムで追従する
    }

    // ── Build Settings への追加 ──────────────────────────────
    private static void AddSceneToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;
        foreach (var s in scenes)
            if (s.path == SCENE_PATH) return;

        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(scenes)
        {
            new EditorBuildSettingsScene(SCENE_PATH, true)
        };
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ── ユーティリティ ────────────────────────────────────────
    private static GameObject CreatePlatform(string name, Vector3 pos, Vector3 scale,
                                              Color color, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        if (parent != null) go.transform.SetParent(parent);
        SetMaterialColor(go, color);
        return go;
    }

    private static GameObject CreateRamp(string name, Vector3 pos, Quaternion rot,
                                          Vector3 scale, Color color, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position   = pos;
        go.transform.rotation   = rot;
        go.transform.localScale = scale;
        if (parent != null) go.transform.SetParent(parent);
        SetMaterialColor(go, color);
        return go;
    }

    private static GameObject CreateWall(string name, Vector3 pos, Vector3 scale, Transform parent)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position   = pos;
        go.transform.localScale = scale;
        go.transform.SetParent(parent);
        SetMaterialColor(go, new Color(0.4f, 0.35f, 0.3f));
        return go;
    }

    private static void SetMaterialColor(GameObject go, Color color)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null) return;
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ??
                               Shader.Find("Standard"));
        if (mat == null) return;
        mat.color = color;
        mr.sharedMaterial = mat;
    }

    private static TMP_FontAsset s_japaneseFont;

    private static TMP_FontAsset GetJapaneseFont()
    {
        if (s_japaneseFont != null) return s_japaneseFont;

        string[] guids = AssetDatabase.FindAssets("NotoSansJP_Rebuilt t:TMP_FontAsset");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            s_japaneseFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (s_japaneseFont != null) return s_japaneseFont;
        }

        return TMP_Settings.defaultFontAsset;
    }

    private static void ApplyFont(TextMeshProUGUI tmp)
    {
        var font = GetJapaneseFont();
        if (font != null)
            tmp.font = font;
    }

    private static RectTransform CreateTmpText(Transform parent, string name, string text,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 sizeDelta, Vector2 anchoredPos,
        int fontSize, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go  = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        ApplyFont(tmp);
        tmp.text              = text;
        tmp.fontSize          = fontSize;
        tmp.alignment         = align;
        tmp.color             = Color.white;
        var rt = tmp.rectTransform;
        rt.anchorMin      = anchorMin;
        rt.anchorMax      = anchorMax;
        rt.sizeDelta      = sizeDelta;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }

    private static Button BuildButton(Transform parent, string name, string label,
                                       Vector2 anchoredPos, Vector2 anchor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.6f, 0.9f);
        var btn = go.AddComponent<Button>();
        var rt  = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor; rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(140f, 40f);
        rt.anchoredPosition = anchoredPos;

        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(go.transform, false);
        var tmp = labelGo.AddComponent<TextMeshProUGUI>();
        ApplyFont(tmp);
        tmp.text       = label;
        tmp.fontSize   = 18;
        tmp.alignment  = TextAlignmentOptions.Center;
        tmp.color      = Color.white;
        StretchFull(tmp.rectTransform);

        return btn;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void SetProp(SerializedObject so, string propName, Object value)
    {
        var prop = so.FindProperty(propName);
        if (prop != null) prop.objectReferenceValue = value;
    }
}
