#if UNITY_EDITOR
using System.Collections.Generic;
using PeakPlunder.Audio;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// OfflineTestScene の実装漏れを埋めるための反復補完ツール。
/// ccc/Loop Implement OfflineTestScene から実行する。
/// </summary>
public static class OfflineSceneLoopImplementer
{
    private const string ScenePath = "Assets/Sandbox/Scene/OfflineTestScene.unity";
    private const string PlayerPrefabPath = "Assets/Sandbox/Prefabs/PlayerPrefab.prefab";
    private const int MaxIterations = 8;

    [MenuItem("ccc/Loop Implement OfflineTestScene")]
    public static void LoopImplementOfflineTestScene()
    {
        if (!FileExists(ScenePath))
        {
            Debug.LogError($"[OfflineLoop] Scene not found: {ScenePath}");
            return;
        }

        EditorSceneManager.SaveOpenScenes();

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EnsurePlayerPrefabSetup();

        int prevMissingCount = int.MaxValue;
        for (int i = 1; i <= MaxIterations; i++)
        {
            ApplyAugmentations();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            var missing = ValidateRequirements();
            if (missing.Count == 0)
            {
                Debug.Log($"[OfflineLoop] Completed in {i} iteration(s). OfflineTestScene is fully augmented.");
                return;
            }

            Debug.LogWarning($"[OfflineLoop] Iteration {i}: missing {missing.Count} requirement(s): {string.Join(", ", missing)}");
            if (missing.Count >= prevMissingCount)
                break;

            prevMissingCount = missing.Count;
        }

        var finalMissing = ValidateRequirements();
        Debug.LogWarning($"[OfflineLoop] Stopped with {finalMissing.Count} missing requirement(s): {string.Join(", ", finalMissing)}");
    }

    private static bool FileExists(string path) => System.IO.File.Exists(path);

    private static void ApplyAugmentations()
    {
        EnsureCoreSystems();
        EnsureRuntimeZones();
        EnsureWeatherBoard();
        EnsureUiExtensions();
        EnsureReturnVoteUi();
        RemoveSceneAudioListener();
    }

    private static void EnsureCoreSystems()
    {
        var systemsRoot = GetOrCreateRoot("GameSystems");

        EnsureChildComponent<CosmeticManager>(systemsRoot, "CosmeticManager");
        EnsureChildComponent<ColorBlindPaletteService>(systemsRoot, "ColorBlindPaletteService");

        var terrainGo = EnsureChildComponent<MountainTerrainGenerator>(systemsRoot, "MountainTerrainGenerator").gameObject;
        terrainGo.transform.position = Vector3.zero;

        var shrineMgr = EnsureChildComponent<ShrineSpawnManager>(systemsRoot, "ShrineSpawnManager");
        var shrineSo = new SerializedObject(shrineMgr);
        shrineSo.FindProperty("_spawnOnAwake")?.SetValueSafe(false);
        shrineSo.ApplyModifiedPropertiesWithoutUndo();

        var npcSpawner = EnsureChildComponent<OfflineNPCSpawner>(systemsRoot, "OfflineNPCManager");
        var npcSo = new SerializedObject(npcSpawner);
        npcSo.FindProperty("_explorerModelPrefab")?.SetValueSafe(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Sandbox/Model/Explorer.fbx"));
        npcSo.FindProperty("_animatorController")?.SetValueSafe(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Sandbox/Animation/Explorer/ExplorerAnimator.controller"));
        npcSo.ApplyModifiedPropertiesWithoutUndo();

        var audioMgr = EnsureChildComponent<AudioManager>(systemsRoot, "AudioManager");
        var audioSo = new SerializedObject(audioMgr);
        audioSo.FindProperty("_library")?.SetValueSafe(AssetDatabase.LoadAssetAtPath<SoundLibrary>("Assets/Sandbox/Audio/DefaultSoundLibrary.asset"));
        audioSo.FindProperty("_mixer")?.SetValueSafe(AssetDatabase.LoadAssetAtPath<UnityEngine.Audio.AudioMixer>("Assets/Sandbox/Audio/PeakPlunderMixer.mixer"));
        audioSo.ApplyModifiedPropertiesWithoutUndo();

        var bgm = EnsureChildComponent<BgmController>(systemsRoot, "BgmController");
        bgm.enabled = true;

        ConfigureSpawnManagerRelicPool();
    }

    private static void ConfigureSpawnManagerRelicPool()
    {
        var spawnManager = Object.FindFirstObjectByType<SpawnManager>();
        if (spawnManager == null) return;

        var relicPool = new List<GameObject>();
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

        foreach (var path in paths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) relicPool.Add(prefab);
        }

        var so = new SerializedObject(spawnManager);
        var poolProp = so.FindProperty("_relicPrefabPool");
        if (poolProp != null)
        {
            poolProp.arraySize = relicPool.Count;
            for (int i = 0; i < relicPool.Count; i++)
                poolProp.GetArrayElementAtIndex(i).objectReferenceValue = relicPool[i];
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureRuntimeZones()
    {
        var root = GetOrCreateRoot("ZoneRuntime");
        int safeZoneLayer = LayerMask.NameToLayer("SafeZone");

        var zones = new (int id, Vector3 pos)[]
        {
            (1, new Vector3(-8f,   6f, 16f)),
            (2, new Vector3( 8f,  28f, 18f)),
            (3, new Vector3( 3f,  58f, 10f)),
            (4, new Vector3(-6f,  72f,  5f)),
            (5, new Vector3(-7f,  95f, -1f)),
            (6, new Vector3( 0f, 121f,  0f)),
        };

        foreach (var (zoneId, basePos) in zones)
        {
            var zoneRoot = GetOrCreateChild(root, $"Zone{zoneId}_Runtime");
            zoneRoot.transform.position = basePos;

            var gate = EnsureChildComponent<RouteGate>(zoneRoot, "RouteGate");
            gate.transform.localPosition = Vector3.zero;
            EnsureRouteGateBlocker(gate);

            var shelterGo = GetOrCreateChild(zoneRoot, "ShelterZone");
            shelterGo.transform.localPosition = new Vector3(2.5f, 0f, 1.5f);
            if (safeZoneLayer >= 0)
                shelterGo.layer = safeZoneLayer;

            var shelterBox = shelterGo.GetComponent<BoxCollider>();
            if (shelterBox == null)
                shelterBox = shelterGo.AddComponent<BoxCollider>();
            shelterBox.isTrigger = true;
            shelterBox.size = new Vector3(5f, 3f, 5f);
            shelterBox.center = new Vector3(0f, 1.5f, 0f);
            if (shelterGo.GetComponent<ShelterZone>() == null)
                shelterGo.AddComponent<ShelterZone>();

            var relicSp = EnsureSpawnPoint(zoneRoot, "RelicSpawnPoint", new Vector3(0f, 1f, -2f), SpawnLayer.Relic, zoneId, 1f);
            var hazardSp = EnsureSpawnPoint(zoneRoot, "HazardSpawnPoint", new Vector3(-2f, 1f, 2f), SpawnLayer.Hazard, zoneId, 0.45f);
            var itemSp = EnsureSpawnPoint(zoneRoot, "ItemSpawnPoint", new Vector3(2f, 1f, -2f), SpawnLayer.Item, zoneId, 0.5f);

            ConfigureRelicSpawnPointPool(relicSp);
            EnsureSpawnPointMarkerVisual(hazardSp.gameObject, new Color(1f, 0.4f, 0.4f));
            EnsureSpawnPointMarkerVisual(itemSp.gameObject, new Color(0.4f, 0.8f, 1f));
        }
    }

    private static void EnsureRouteGateBlocker(RouteGate gate)
    {
        var blocker = gate.transform.Find("Blocker")?.gameObject;
        if (blocker == null)
        {
            blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Blocker";
            blocker.transform.SetParent(gate.transform, false);
            blocker.transform.localPosition = new Vector3(0f, 1f, 0f);
            blocker.transform.localScale = new Vector3(3f, 2f, 0.5f);
            blocker.GetComponent<Renderer>().sharedMaterial.color = new Color(0.6f, 0.25f, 0.2f);
        }

        var so = new SerializedObject(gate);
        var blockers = so.FindProperty("_blockers");
        if (blockers != null)
        {
            blockers.arraySize = 1;
            blockers.GetArrayElementAtIndex(0).objectReferenceValue = blocker;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SpawnPoint EnsureSpawnPoint(GameObject parent, string name, Vector3 localPos, SpawnLayer layer, int zoneId, float activateChance)
    {
        var go = GetOrCreateChild(parent, name);
        go.transform.localPosition = localPos;
        var sp = GetOrAddComponent<SpawnPoint>(go);

        var so = new SerializedObject(sp);
        so.FindProperty("_layer")?.SetValueSafe((int)layer);
        so.FindProperty("_zoneId")?.SetValueSafe(zoneId);
        so.FindProperty("_activateChance")?.SetValueSafe(activateChance);
        so.ApplyModifiedPropertiesWithoutUndo();

        return sp;
    }

    private static void ConfigureRelicSpawnPointPool(SpawnPoint point)
    {
        var so = new SerializedObject(point);
        var prefabsProp = so.FindProperty("_spawnPrefabs");
        if (prefabsProp == null) return;

        string[] relicPaths =
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

        var prefabs = new List<GameObject>();
        foreach (var path in relicPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null) prefabs.Add(prefab);
        }

        prefabsProp.arraySize = prefabs.Count;
        for (int i = 0; i < prefabs.Count; i++)
            prefabsProp.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureSpawnPointMarkerVisual(GameObject go, Color color)
    {
        var marker = go.transform.Find("Marker")?.gameObject;
        if (marker != null) return;

        marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "Marker";
        marker.transform.SetParent(go.transform, false);
        marker.transform.localPosition = Vector3.zero;
        marker.transform.localScale = Vector3.one * 0.35f;
        var collider = marker.GetComponent<Collider>();
        if (collider != null) Object.DestroyImmediate(collider);
        marker.GetComponent<Renderer>().sharedMaterial.color = color;
    }

    private static void EnsureWeatherBoard()
    {
        var weatherBoard = GameObject.Find("WeatherBoard");
        if (weatherBoard == null) return;

        var boardMgr = GetOrAddComponent<WeatherBoardManager>(weatherBoard);
        var canvasGo = GetOrCreateChild(weatherBoard, "BoardCanvas");
        canvasGo.transform.localPosition = new Vector3(0f, 0f, -0.15f);
        canvasGo.transform.localRotation = Quaternion.identity;
        canvasGo.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        var canvas = GetOrAddComponent<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.WorldSpace;
        if (canvasGo.GetComponent<GraphicRaycaster>() == null)
            canvasGo.AddComponent<GraphicRaycaster>();

        var compactRoot = GetOrCreateChild(canvasGo, "CompactRoot");
        var expandedRoot = GetOrCreateChild(canvasGo, "ExpandedRoot");

        var compactWeather = GetOrCreateTmp(compactRoot.transform, "CompactWeather", "晴れ", new Vector2(0.5f, 0.7f), new Vector2(220f, 30f), 22);
        var compactWind = GetOrCreateTmp(compactRoot.transform, "CompactWind", "風速 0.0m/s", new Vector2(0.5f, 0.3f), new Vector2(220f, 22f), 16);

        var expWeather = GetOrCreateTmp(expandedRoot.transform, "ExpandedWeather", "天候: 晴れ", new Vector2(0.5f, 0.85f), new Vector2(420f, 34f), 22);
        var expWind = GetOrCreateTmp(expandedRoot.transform, "ExpandedWind", "風速 0.0m/s", new Vector2(0.5f, 0.68f), new Vector2(420f, 28f), 18);
        var expRecom = GetOrCreateTmp(expandedRoot.transform, "ExpandedRecommendation", "推奨装備", new Vector2(0.5f, 0.42f), new Vector2(420f, 120f), 14);
        expRecom.alignment = TextAlignmentOptions.TopLeft;
        var expRoute = GetOrCreateTmp(expandedRoot.transform, "ExpandedRoute", "ルート状況", new Vector2(0.5f, 0.12f), new Vector2(420f, 110f), 14);
        expRoute.alignment = TextAlignmentOptions.TopLeft;

        var so = new SerializedObject(boardMgr);
        so.FindProperty("_compactRoot")?.SetValueSafe(compactRoot);
        so.FindProperty("_expandedRoot")?.SetValueSafe(expandedRoot);
        so.FindProperty("_compactWeatherLabel")?.SetValueSafe(compactWeather);
        so.FindProperty("_compactWindLabel")?.SetValueSafe(compactWind);
        so.FindProperty("_expandedWeatherLabel")?.SetValueSafe(expWeather);
        so.FindProperty("_expandedWindLabel")?.SetValueSafe(expWind);
        so.FindProperty("_expandedRecommendationLabel")?.SetValueSafe(expRecom);
        so.FindProperty("_expandedRouteStatusLabel")?.SetValueSafe(expRoute);
        so.ApplyModifiedPropertiesWithoutUndo();

        compactRoot.SetActive(true);
        expandedRoot.SetActive(false);
    }

    private static void EnsureUiExtensions()
    {
        var uiRoot = GameObject.Find("UIRoot");
        if (uiRoot == null) return;

        EnsureMiniCompass(uiRoot.transform);
        EnsureAltitudeMeter(uiRoot.transform);
        EnsureSafeZoneHud(uiRoot.transform);
        EnsureRelicDiscoveryNotifier(uiRoot.transform);
        EnsurePauseAndSettings(uiRoot.transform);
        EnsureShopTutorialOverlay(uiRoot.transform);
    }

    private static void EnsureMiniCompass(Transform uiRoot)
    {
        var root = GetOrCreateChild(uiRoot.gameObject, "MiniCompassHUD");
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.sizeDelta = new Vector2(180f, 180f);
        rt.anchoredPosition = new Vector2(-120f, -120f);

        var dial = GetOrCreateChild(root, "Dial");
        var dialRt = dial.GetComponent<RectTransform>();
        dialRt.anchorMin = new Vector2(0.5f, 0.5f);
        dialRt.anchorMax = new Vector2(0.5f, 0.5f);
        dialRt.sizeDelta = new Vector2(140f, 140f);
        dialRt.anchoredPosition = Vector2.zero;

        GetOrCreateTmp(dial.transform, "N", "N", new Vector2(0.5f, 1f), new Vector2(20f, 20f), 14).rectTransform.anchoredPosition = new Vector2(0f, -8f);
        GetOrCreateTmp(dial.transform, "S", "S", new Vector2(0.5f, 0f), new Vector2(20f, 20f), 14).rectTransform.anchoredPosition = new Vector2(0f, 8f);
        GetOrCreateTmp(dial.transform, "E", "E", new Vector2(1f, 0.5f), new Vector2(20f, 20f), 14).rectTransform.anchoredPosition = new Vector2(-8f, 0f);
        GetOrCreateTmp(dial.transform, "W", "W", new Vector2(0f, 0.5f), new Vector2(20f, 20f), 14).rectTransform.anchoredPosition = new Vector2(8f, 0f);

        var arrowParent = GetOrCreateChild(root, "PinArrows");
        var arrowParentRt = arrowParent.GetComponent<RectTransform>();
        arrowParentRt.anchorMin = new Vector2(0.5f, 0.5f);
        arrowParentRt.anchorMax = new Vector2(0.5f, 0.5f);
        arrowParentRt.sizeDelta = new Vector2(140f, 140f);
        arrowParentRt.anchoredPosition = Vector2.zero;

        var arrowPrefab = GetOrCreateChild(root, "PinArrowPrefab");
        var arrowImg = GetOrAddComponent<Image>(arrowPrefab);
        arrowImg.color = Color.white;
        var arrowRt = arrowPrefab.GetComponent<RectTransform>();
        arrowRt.anchorMin = new Vector2(0.5f, 0.5f);
        arrowRt.anchorMax = new Vector2(0.5f, 0.5f);
        arrowRt.sizeDelta = new Vector2(12f, 18f);
        arrowPrefab.SetActive(false);

        var compass = GetOrAddComponent<MiniCompass>(root);
        var so = new SerializedObject(compass);
        so.FindProperty("_dialTransform")?.SetValueSafe(dialRt);
        so.FindProperty("_pinArrowParent")?.SetValueSafe(arrowParentRt);
        so.FindProperty("_pinArrowPrefab")?.SetValueSafe(arrowPrefab);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureAltitudeMeter(Transform uiRoot)
    {
        var root = GetOrCreateChild(uiRoot.gameObject, "AltitudeMeterHUD");
        var label = GetOrCreateTmp(root.transform, "AltitudeLabel", "0m — ベースキャンプ", new Vector2(0.5f, 1f), new Vector2(420f, 30f), 20);
        label.rectTransform.anchoredPosition = new Vector2(0f, -18f);

        var meter = GetOrAddComponent<AltitudeMeter>(root);
        var so = new SerializedObject(meter);
        so.FindProperty("_label")?.SetValueSafe(label);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureSafeZoneHud(Transform uiRoot)
    {
        var root = GetOrCreateChild(uiRoot.gameObject, "SafeZoneHud");
        var label = GetOrCreateTmp(root.transform, "SafeZoneLabel", "セーフゾーン", new Vector2(0f, 1f), new Vector2(240f, 26f), 18);
        label.rectTransform.anchoredPosition = new Vector2(120f, -140f);

        var indicator = GetOrAddComponent<SafeZoneHudIndicator>(root);
        var so = new SerializedObject(indicator);
        so.FindProperty("_root")?.SetValueSafe(root);
        so.FindProperty("_label")?.SetValueSafe(label);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureRelicDiscoveryNotifier(Transform uiRoot)
    {
        var root = GetOrCreateChild(uiRoot.gameObject, "RelicDiscoveryNotifierUI");
        var rt = root.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.85f);
        rt.anchorMax = new Vector2(0.5f, 0.85f);
        rt.sizeDelta = new Vector2(420f, 60f);
        rt.anchoredPosition = Vector2.zero;

        var bg = GetOrAddComponent<Image>(root);
        bg.color = new Color(0f, 0f, 0f, 0.6f);

        var group = GetOrAddComponent<CanvasGroup>(root);
        var label = GetOrCreateTmp(root.transform, "Label", "遺物を発見！", new Vector2(0.5f, 0.5f), new Vector2(380f, 30f), 22);

        var iconRoot = GetOrCreateChild(root, "IconRoot");
        var iconRt = iconRoot.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.sizeDelta = new Vector2(24f, 24f);
        iconRt.anchoredPosition = new Vector2(24f, 0f);

        var notifier = GetOrAddComponent<RelicDiscoveryNotifier>(root);
        var so = new SerializedObject(notifier);
        so.FindProperty("_group")?.SetValueSafe(group);
        so.FindProperty("_label")?.SetValueSafe(label);
        so.FindProperty("_iconRoot")?.SetValueSafe(iconRoot);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsurePauseAndSettings(Transform uiRoot)
    {
        var settingsRoot = GetOrCreateChild(uiRoot.gameObject, "SettingsRoot");
        var settingsPanel = GetOrCreateChild(settingsRoot, "SettingsPanel");
        StretchCenter(settingsPanel.GetComponent<RectTransform>(), new Vector2(920f, 560f));
        var settingsPanelBg = GetOrAddComponent<Image>(settingsPanel);
        settingsPanelBg.color = new Color(0f, 0f, 0f, 0.88f);

        var tabRow = GetOrCreateChild(settingsPanel, "TabRow");
        StretchCenter(tabRow.GetComponent<RectTransform>(), new Vector2(860f, 56f));
        tabRow.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 220f);
        var tabGraphics = GetOrCreateButton(tabRow.transform, "TabGraphics", "グラフィック", new Vector2(0f, 0.5f), new Vector2(90f, 0f));
        var tabAudio = GetOrCreateButton(tabRow.transform, "TabAudio", "オーディオ", new Vector2(0f, 0.5f), new Vector2(290f, 0f));
        var tabControls = GetOrCreateButton(tabRow.transform, "TabControls", "操作", new Vector2(0f, 0.5f), new Vector2(490f, 0f));
        var tabAccessibility = GetOrCreateButton(tabRow.transform, "TabAccessibility", "アクセシビリティ", new Vector2(0f, 0.5f), new Vector2(690f, 0f));

        var panelGraphics = GetOrCreateChild(settingsPanel, "PanelGraphics");
        var panelAudio = GetOrCreateChild(settingsPanel, "PanelAudio");
        var panelControls = GetOrCreateChild(settingsPanel, "PanelControls");
        var panelAccessibility = GetOrCreateChild(settingsPanel, "PanelAccessibility");
        StretchCenter(panelGraphics.GetComponent<RectTransform>(), new Vector2(860f, 420f));
        StretchCenter(panelAudio.GetComponent<RectTransform>(), new Vector2(860f, 420f));
        StretchCenter(panelControls.GetComponent<RectTransform>(), new Vector2(860f, 420f));
        StretchCenter(panelAccessibility.GetComponent<RectTransform>(), new Vector2(860f, 420f));
        panelGraphics.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -24f);
        panelAudio.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -24f);
        panelControls.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -24f);
        panelAccessibility.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -24f);

        GetOrCreateTmp(panelGraphics.transform, "PanelLabel", "グラフィック設定", new Vector2(0.5f, 0.5f), new Vector2(640f, 40f), 26);
        GetOrCreateTmp(panelAudio.transform, "PanelLabel", "オーディオ設定", new Vector2(0.5f, 0.5f), new Vector2(640f, 40f), 26);
        GetOrCreateTmp(panelControls.transform, "PanelLabel", "操作設定", new Vector2(0.5f, 0.5f), new Vector2(640f, 40f), 26);
        GetOrCreateTmp(panelAccessibility.transform, "PanelLabel", "アクセシビリティ設定", new Vector2(0.5f, 0.5f), new Vector2(640f, 40f), 26);

        var settingsCloseBtn = GetOrCreateButton(settingsPanel.transform, "CloseButton", "閉じる", new Vector2(0.5f, 0f), new Vector2(0f, 30f));

        var settingsServiceRoot = GetOrCreateRoot("SettingsService");
        var legacySettingsManager = settingsRoot.GetComponent<SettingsManager>();
        if (legacySettingsManager != null)
            Object.DestroyImmediate(legacySettingsManager);

        var legacyKeyRebind = settingsRoot.GetComponent<KeyRebindController>();
        if (legacyKeyRebind != null)
            Object.DestroyImmediate(legacyKeyRebind);

        var settingsMgr = GetOrAddComponent<SettingsManager>(settingsServiceRoot);
        var settingsSo = new SerializedObject(settingsMgr);
        settingsSo.FindProperty("_settingsPanel")?.SetValueSafe(settingsPanel);
        settingsSo.FindProperty("_tabGraphics")?.SetValueSafe(tabGraphics);
        settingsSo.FindProperty("_tabAudio")?.SetValueSafe(tabAudio);
        settingsSo.FindProperty("_tabControls")?.SetValueSafe(tabControls);
        settingsSo.FindProperty("_tabAccessibility")?.SetValueSafe(tabAccessibility);
        settingsSo.FindProperty("_panelGraphics")?.SetValueSafe(panelGraphics);
        settingsSo.FindProperty("_panelAudio")?.SetValueSafe(panelAudio);
        settingsSo.FindProperty("_panelControls")?.SetValueSafe(panelControls);
        settingsSo.FindProperty("_panelAccessibility")?.SetValueSafe(panelAccessibility);
        settingsSo.FindProperty("_btnClose")?.SetValueSafe(settingsCloseBtn);
        settingsSo.ApplyModifiedPropertiesWithoutUndo();

        var rebind = GetOrAddComponent<KeyRebindController>(settingsServiceRoot);
        var rebindSo = new SerializedObject(rebind);
        rebindSo.FindProperty("_actions")?.SetValueSafe(AssetDatabase.LoadMainAssetAtPath("Assets/InputSystem_Actions.inputactions"));
        rebindSo.ApplyModifiedPropertiesWithoutUndo();

        var pauseRoot = GetOrCreateChild(uiRoot.gameObject, "PauseMenuRoot");
        var menuPanel = GetOrCreateChild(pauseRoot, "MenuPanel");
        var blurPanel = GetOrCreateChild(pauseRoot, "BlurPanel");
        var confirmPanel = GetOrCreateChild(pauseRoot, "ConfirmLeavePanel");

        var menuBg = GetOrAddComponent<Image>(menuPanel);
        menuBg.color = new Color(0f, 0f, 0f, 0.8f);
        StretchCenter(menuPanel.GetComponent<RectTransform>(), new Vector2(360f, 260f));

        var blurBg = GetOrAddComponent<Image>(blurPanel);
        blurBg.color = new Color(0f, 0f, 0f, 0.35f);
        StretchFull(blurPanel.GetComponent<RectTransform>());

        var confirmBg = GetOrAddComponent<Image>(confirmPanel);
        confirmBg.color = new Color(0f, 0f, 0f, 0.9f);
        StretchCenter(confirmPanel.GetComponent<RectTransform>(), new Vector2(320f, 180f));

        var resumeBtn = GetOrCreateButton(menuPanel.transform, "ResumeButton", "ゲームに戻る", new Vector2(0.5f, 1f), new Vector2(0f, -50f));
        var settingsBtn = GetOrCreateButton(menuPanel.transform, "SettingsButton", "設定", new Vector2(0.5f, 1f), new Vector2(0f, -110f));
        var leaveBtn = GetOrCreateButton(menuPanel.transform, "LeaveButton", "離脱", new Vector2(0.5f, 1f), new Vector2(0f, -170f));

        var yesBtn = GetOrCreateButton(confirmPanel.transform, "ConfirmYes", "はい", new Vector2(0.5f, 0f), new Vector2(-60f, 35f));
        var noBtn = GetOrCreateButton(confirmPanel.transform, "ConfirmNo", "いいえ", new Vector2(0.5f, 0f), new Vector2(60f, 35f));

        var pause = GetOrAddComponent<PauseMenu>(pauseRoot);
        var pauseSo = new SerializedObject(pause);
        pauseSo.FindProperty("_menuRoot")?.SetValueSafe(menuPanel);
        pauseSo.FindProperty("_confirmLeaveRoot")?.SetValueSafe(confirmPanel);
        pauseSo.FindProperty("_blurPanel")?.SetValueSafe(blurPanel);
        pauseSo.FindProperty("_resumeButton")?.SetValueSafe(resumeBtn);
        pauseSo.FindProperty("_settingsButton")?.SetValueSafe(settingsBtn);
        pauseSo.FindProperty("_leaveButton")?.SetValueSafe(leaveBtn);
        pauseSo.FindProperty("_confirmLeaveYes")?.SetValueSafe(yesBtn);
        pauseSo.FindProperty("_confirmLeaveNo")?.SetValueSafe(noBtn);
        pauseSo.FindProperty("_settingsRoot")?.SetValueSafe(settingsPanel);
        pauseSo.ApplyModifiedPropertiesWithoutUndo();

        panelGraphics.SetActive(true);
        panelAudio.SetActive(false);
        panelControls.SetActive(false);
        panelAccessibility.SetActive(false);
        menuPanel.SetActive(false);
        confirmPanel.SetActive(false);
        blurPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    private static void EnsureShopTutorialOverlay(Transform uiRoot)
    {
        var root = GetOrCreateChild(uiRoot.gameObject, "ShopTutorialOverlayUI");
        var panel = GetOrCreateChild(root, "Panel");
        var bg = GetOrAddComponent<Image>(panel);
        bg.color = new Color(0f, 0f, 0f, 0.8f);
        StretchCenter(panel.GetComponent<RectTransform>(), new Vector2(560f, 180f));

        var step = GetOrCreateTmp(panel.transform, "StepLabel", "チュートリアル", new Vector2(0.5f, 1f), new Vector2(500f, 80f), 18);
        step.alignment = TextAlignmentOptions.TopLeft;
        step.rectTransform.anchoredPosition = new Vector2(0f, -16f);

        var counter = GetOrCreateTmp(panel.transform, "CounterLabel", "1/4", new Vector2(1f, 1f), new Vector2(80f, 24f), 16);
        counter.rectTransform.anchoredPosition = new Vector2(-20f, -12f);

        var next = GetOrCreateButton(panel.transform, "NextButton", "次へ", new Vector2(1f, 0f), new Vector2(-80f, 24f));
        var skip = GetOrCreateButton(panel.transform, "SkipButton", "スキップ", new Vector2(1f, 0f), new Vector2(-220f, 24f));

        var overlay = GetOrAddComponent<ShopTutorialOverlay>(root);
        var so = new SerializedObject(overlay);
        so.FindProperty("_root")?.SetValueSafe(panel);
        so.FindProperty("_stepLabel")?.SetValueSafe(step);
        so.FindProperty("_counterLabel")?.SetValueSafe(counter);
        so.FindProperty("_nextButton")?.SetValueSafe(next);
        so.FindProperty("_skipButton")?.SetValueSafe(skip);
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
    }

    private static void EnsureReturnVoteUi()
    {
        var voteSystem = Object.FindFirstObjectByType<ReturnVoteSystem>();
        var uiRoot = GameObject.Find("UIRoot");
        if (voteSystem == null || uiRoot == null) return;

        var panel = GetOrCreateChild(uiRoot, "ReturnVotePanel");
        var bg = GetOrAddComponent<Image>(panel);
        bg.color = new Color(0.05f, 0.1f, 0.2f, 0.82f);

        var rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(420f, 120f);
        rt.anchoredPosition = Vector2.zero;

        var voteText = GetOrCreateTmp(panel.transform, "VoteText", "帰還投票", new Vector2(0.5f, 0.7f), new Vector2(380f, 60f), 20);
        var timerText = GetOrCreateTmp(panel.transform, "TimerText", "残り: 30秒", new Vector2(0.5f, 0.2f), new Vector2(200f, 30f), 16);

        var so = new SerializedObject(voteSystem);
        so.FindProperty("_votePanel")?.SetValueSafe(panel);
        so.FindProperty("_voteText")?.SetValueSafe(voteText);
        so.FindProperty("_timerText")?.SetValueSafe(timerText);
        so.ApplyModifiedPropertiesWithoutUndo();

        panel.SetActive(false);
    }

    private static void RemoveSceneAudioListener()
    {
        var mainCamera = GameObject.Find("MainCamera");
        if (mainCamera == null) return;

        var listener = mainCamera.GetComponent<AudioListener>();
        if (listener != null)
            Object.DestroyImmediate(listener);
    }

    private static void EnsurePlayerPrefabSetup()
    {
        var root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            if (root == null) return;

            root.tag = "Player";
            int playerLayer = LayerMask.NameToLayer("Player");
            if (playerLayer >= 0)
                root.layer = playerLayer;

            EnsurePrefabComponent<ThrowController>(root);
            EnsurePrefabComponent<ShelterOccupant>(root);
            EnsurePrefabComponent<FrostbiteDamage>(root);
            EnsurePrefabComponent<AltitudeSicknessEffect>(root);
            EnsurePrefabComponent<WeatherFrictionAdapter>(root);
            EnsurePrefabComponent<PinSystem>(root);
            EnsurePrefabComponent<EmoteSystem>(root);
            EnsurePrefabComponent<RagdollSystem>(root);
            EnsureEmoteWheelSetup(root);

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureEmoteWheelSetup(GameObject root)
    {
        var emote = root.GetComponent<EmoteSystem>();
        if (emote == null) return;

        var wheelRoot = root.transform.Find("EmoteWheelRoot")?.gameObject;
        if (wheelRoot == null)
        {
            wheelRoot = new GameObject("EmoteWheelRoot");
            wheelRoot.transform.SetParent(root.transform, false);
        }
        wheelRoot.SetActive(false);

        var so = new SerializedObject(emote);
        so.FindProperty("_wheelRoot")?.SetValueSafe(wheelRoot);
        var slots = so.FindProperty("_slots");
        if (slots != null && slots.arraySize < 6)
            slots.arraySize = 6;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsurePrefabComponent<T>(GameObject root) where T : Component
    {
        if (root.GetComponent<T>() == null)
            root.AddComponent<T>();
    }

    private static List<string> ValidateRequirements()
    {
        var missing = new List<string>();

        CheckCount<RouteGate>("RouteGate", 6, missing);
        CheckCount<ShelterZone>("ShelterZone", 6, missing);
        CheckCount<SpawnPoint>("SpawnPoint", 18, missing);
        CheckCount<WeatherBoardManager>("WeatherBoardManager", 1, missing);
        CheckCount<MiniCompass>("MiniCompass", 1, missing);
        CheckCount<AltitudeMeter>("AltitudeMeter", 1, missing);
        CheckCount<SafeZoneHudIndicator>("SafeZoneHudIndicator", 1, missing);
        CheckCount<RelicDiscoveryNotifier>("RelicDiscoveryNotifier", 1, missing);
        CheckCount<PauseMenu>("PauseMenu", 1, missing);
        CheckCount<SettingsManager>("SettingsManager", 1, missing);
        CheckCount<KeyRebindController>("KeyRebindController", 1, missing);
        CheckCount<ColorBlindPaletteService>("ColorBlindPaletteService", 1, missing);
        CheckCount<CosmeticManager>("CosmeticManager", 1, missing);
        CheckCount<ShrineSpawnManager>("ShrineSpawnManager", 1, missing);
        CheckCount<MountainTerrainGenerator>("MountainTerrainGenerator", 1, missing);
        CheckCount<OfflineNPCSpawner>("OfflineNPCSpawner", 1, missing);
        CheckCount<ShopTutorialOverlay>("ShopTutorialOverlay", 1, missing);

        return missing;
    }

    private static void CheckCount<T>(string name, int minCount, List<string> missing) where T : Object
    {
        int count = Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
        if (count < minCount)
            missing.Add($"{name}<{minCount} (actual={count})");
    }

    private static GameObject GetOrCreateRoot(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) return go;
        return new GameObject(name);
    }

    private static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        var child = parent.transform.Find(name);
        if (child != null) return child.gameObject;

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static T EnsureChildComponent<T>(GameObject parent, string childName) where T : Component
    {
        var child = parent.transform.Find(childName)?.gameObject;
        if (child == null)
        {
            child = new GameObject(childName);
            child.transform.SetParent(parent.transform);
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
        }

        return GetOrAddComponent<T>(child);
    }

    private static T GetOrAddComponent<T>(GameObject go) where T : Component
    {
        var component = go.GetComponent<T>();
        if (component == null)
            component = go.AddComponent<T>();
        return component;
    }

    private static TextMeshProUGUI GetOrCreateTmp(Transform parent, string name, string text, Vector2 anchor, Vector2 size, int fontSize)
    {
        var go = parent.Find(name)?.gameObject;
        if (go == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
        }

        var tmp = GetOrAddComponent<TextMeshProUGUI>(go);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        var rt = tmp.rectTransform;
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;

        return tmp;
    }

    private static Button GetOrCreateButton(Transform parent, string name, string label, Vector2 anchor, Vector2 anchoredPos)
    {
        var go = parent.Find(name)?.gameObject;
        if (go == null)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
        }

        var image = GetOrAddComponent<Image>(go);
        image.color = new Color(0.2f, 0.5f, 0.9f);

        var button = GetOrAddComponent<Button>(go);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.sizeDelta = new Vector2(120f, 36f);
        rt.anchoredPosition = anchoredPos;

        var labelGo = go.transform.Find("Label")?.gameObject;
        if (labelGo == null)
        {
            labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
        }

        var tmp = GetOrAddComponent<TextMeshProUGUI>(labelGo);
        tmp.text = label;
        tmp.fontSize = 16;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        if (tmp.font == null && TMP_Settings.defaultFontAsset != null)
            tmp.font = TMP_Settings.defaultFontAsset;

        StretchFull(tmp.rectTransform);
        return button;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void StretchCenter(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = Vector2.zero;
    }

    private static void SetValueSafe(this SerializedProperty prop, Object obj)
    {
        if (prop != null)
            prop.objectReferenceValue = obj;
    }

    private static void SetValueSafe(this SerializedProperty prop, int value)
    {
        if (prop != null)
            prop.intValue = value;
    }

    private static void SetValueSafe(this SerializedProperty prop, bool value)
    {
        if (prop != null)
            prop.boolValue = value;
    }

    private static void SetValueSafe(this SerializedProperty prop, float value)
    {
        if (prop != null)
            prop.floatValue = value;
    }
}
#endif
