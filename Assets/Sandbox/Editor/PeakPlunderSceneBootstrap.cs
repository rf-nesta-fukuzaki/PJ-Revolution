#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakPlunder.EditorTools
{
    /// <summary>
    /// GDD §5/§7 — 統合ゲームプレイシーン (Gameplay.unity) のブートストラップ。
    ///
    /// 構造:
    ///   Basecamp/
    ///     ├ Ground (Plane 50×50)
    ///     ├ DepartureGate
    ///     ├ ReturnZone
    ///     ├ BasecampShop
    ///     ├ WeatherBoard
    ///     └ HelicopterPad
    ///   Zones/
    ///     ├ Zone1_LowerSlope   (RouteGate / ShelterZone / SpawnPoints)
    ///     ├ Zone2_IcyRidge
    ///     ├ Zone3_AncientTemple
    ///     ├ Zone4_FoggyValley
    ///     ├ Zone5_Crevasse
    ///     └ Zone6_SummitRuins
    ///   Systems/
    ///     ├ GameStateManager
    ///     ├ PerformanceBudgetMonitor
    ///     ├ AudioManager (DontDestroyOnLoad)
    ///     ├ WeatherSystem
    ///     └ SpawnManager
    ///   Lighting/
    ///     ├ Directional Light
    ///     └ Main Camera
    ///
    /// 起動: Tools > PeakPlunder > Create Gameplay Scene
    /// </summary>
    public static class PeakPlunderSceneBootstrap
    {
        private const string SCENE_DIR  = "Assets/Sandbox/Scenes";
        private const string SCENE_PATH = "Assets/Sandbox/Scenes/Gameplay.unity";

        [MenuItem("Tools/PeakPlunder/Create Gameplay Scene")]
        public static void CreateGameplayScene()
        {
            if (!Directory.Exists(SCENE_DIR))
                Directory.CreateDirectory(SCENE_DIR);

            if (File.Exists(SCENE_PATH))
            {
                Debug.Log($"[PeakPlunder] Gameplay scene already exists at {SCENE_PATH}");
                return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateLighting();
            CreateBasecamp();
            CreateZones();
            CreateSystems();

            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[PeakPlunder] Gameplay scene created at {SCENE_PATH}");
        }

        private static void CreateLighting()
        {
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.95f, 0.88f);
            light.intensity = 1.15f;
            lightGO.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            var cam = camGO.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            camGO.AddComponent<AudioListener>();
            camGO.transform.position = new Vector3(0f, 2.5f, -5f);
            camGO.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
        }

        private static void CreateBasecamp()
        {
            var basecampRoot = new GameObject("Basecamp");

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.SetParent(basecampRoot.transform, false);
            ground.transform.localScale = new Vector3(5f, 1f, 5f);  // 50×50 m

            CreateMarker("DepartureGate", basecampRoot, new Vector3(10f, 0.5f, 0f), Color.cyan);
            CreateMarker("ReturnZone",    basecampRoot, new Vector3(-10f, 0.5f, 0f), Color.green);
            CreateMarker("BasecampShop",  basecampRoot, new Vector3(0f, 0.5f, 10f), Color.yellow);
            CreateMarker("WeatherBoard",  basecampRoot, new Vector3(3f, 0.5f, 3f), Color.white);
            CreateMarker("HelicopterPad", basecampRoot, new Vector3(-15f, 0.1f, -5f), Color.gray);
        }

        private static void CreateZones()
        {
            var zonesRoot = new GameObject("Zones");
            string[] zones = {
                "Zone1_LowerSlope",
                "Zone2_IcyRidge",
                "Zone3_AncientTemple",
                "Zone4_FoggyValley",
                "Zone5_Crevasse",
                "Zone6_SummitRuins",
            };

            for (int i = 0; i < zones.Length; i++)
            {
                var zoneRoot = new GameObject(zones[i]);
                zoneRoot.transform.SetParent(zonesRoot.transform, false);
                zoneRoot.transform.position = new Vector3(60f + i * 30f, 0f, 0f);

                CreateMarker("RouteGate",   zoneRoot, new Vector3(0f, 0.5f, 0f),  Color.red);
                CreateMarker("ShelterZone", zoneRoot, new Vector3(10f, 0.5f, 0f), new Color(0.4f, 0.8f, 1f));
                CreateMarker("SpawnPoint",  zoneRoot, new Vector3(-10f, 0.5f, 0f), Color.magenta);
            }
        }

        private static void CreateSystems()
        {
            var systemsRoot = new GameObject("Systems");

            new GameObject("GameStateManager").transform.SetParent(systemsRoot.transform, false);
            new GameObject("PerformanceBudgetMonitor").transform.SetParent(systemsRoot.transform, false);
            new GameObject("AudioManager").transform.SetParent(systemsRoot.transform, false);
            new GameObject("WeatherSystem").transform.SetParent(systemsRoot.transform, false);
            new GameObject("SpawnManager").transform.SetParent(systemsRoot.transform, false);
        }

        private static void CreateMarker(string name, GameObject parent, Vector3 localPos, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(1f, 1f, 1f);

            var collider = go.GetComponent<BoxCollider>();
            if (collider != null) collider.isTrigger = true;

            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                renderer.sharedMaterial = mat;
            }
        }
    }
}
#endif
