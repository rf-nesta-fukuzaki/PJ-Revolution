#if UNITY_EDITOR
using System;
using System.IO;
using PeakPlunder.Localization;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakPlunder.EditorTools
{
    public static class OfflineRemediationBatchRunner
    {
        private const string OfflineScenePath = "Assets/Sandbox/Scene/OfflineTestScene.unity";
        private const string PlayerPrefabPath = "Assets/Sandbox/Prefabs/PlayerPrefab.prefab";

        public static void RunOfflineRemediationChecks()
        {
            int passed = 0;
            int failed = 0;

            void Check(string name, Func<bool> assertion)
            {
                try
                {
                    if (!assertion())
                    {
                        failed++;
                        Debug.LogError($"[OfflineChecks][FAIL] {name}");
                        return;
                    }

                    passed++;
                    Debug.Log($"[OfflineChecks][PASS] {name}");
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug.LogError($"[OfflineChecks][FAIL] {name}: {ex.Message}\n{ex}");
                }
            }

            RunSettingsChecks(Check);
            RunLocalizationChecks(Check);
            RunOfflineSceneChecks(Check);

            Debug.Log($"[OfflineChecks] Summary passed={passed}, failed={failed}");
            if (failed > 0)
                throw new Exception($"Offline remediation checks failed. passed={passed}, failed={failed}");
        }

        private static void RunSettingsChecks(Action<string, Func<bool>> check)
        {
            string root = Path.Combine(Path.GetTempPath(), "pj-revolution-offline-checks", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var expected = new SettingsData(
                    resolutionIndex: 0,
                    windowMode: 2,
                    qualityLevel: 3,
                    fpsCap: 2,
                    vSync: false,
                    shadowQuality: 3,
                    particleQuality: 2,
                    masterVolume: 66,
                    bgmVolume: 55,
                    seVolume: 44,
                    voiceVolume: 88,
                    micGain: 120,
                    mouseSensitivity: 4.25f,
                    invertY: true,
                    gamepadPreset: 1,
                    subtitles: true,
                    uiScale: 115,
                    colorBlindMode: 2,
                    reduceCameraShake: true,
                    crosshairColorHex: "#AABBCC");

                SettingsJsonStore.Save(root, expected, "OfflineTester", tutorialHintsEnabled: false);
                string path = SettingsJsonStore.BuildPath(root);
                check("settings.json is created", () => File.Exists(path));
                check("settings.json can be loaded", () => SettingsJsonStore.TryLoad(root, out _, out _));

                bool loaded = SettingsJsonStore.TryLoad(root, out var actual, out var gameplay);
                check("settings round-trip windowMode", () => loaded && actual.windowMode == expected.windowMode);
                check("settings round-trip qualityLevel", () => loaded && actual.qualityLevel == expected.qualityLevel);
                check("settings round-trip masterVolume", () => loaded && actual.masterVolume == expected.masterVolume);
                check("settings round-trip mouseSensitivity", () => loaded && Mathf.Abs(actual.mouseSensitivity - expected.mouseSensitivity) < 0.001f);
                check("settings round-trip playerDisplayName", () => loaded && gameplay.PlayerDisplayName == "OfflineTester");
                check("settings round-trip tutorialHintsEnabled", () => loaded && gameplay.TutorialHintsEnabled == false);
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }

        private static void RunLocalizationChecks(Action<string, Func<bool>> check)
        {
            string previousLanguage = LocalizedText.CurrentFallbackLanguageCode;
            try
            {
                LocalizedText.SetFallbackLanguage("en");
                check("localized fallback en", () => LocalizedText.Get(LocalizationKeys.UIMainMenuPlay, "__unknown_table__") == "Play");

                LocalizedText.SetFallbackLanguage("ja");
                check("localized fallback ja", () => LocalizedText.Get(LocalizationKeys.UIMainMenuPlay, "__unknown_table__") == "プレイ");

                LocalizedText.SetFallbackLanguage("fr");
                check("localized fallback unsupported->ja", () => LocalizedText.CurrentFallbackLanguageCode == "ja");
            }
            finally
            {
                LocalizedText.SetFallbackLanguage(previousLanguage);
            }
        }

        private static void RunOfflineSceneChecks(Action<string, Func<bool>> check)
        {
            SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                EditorSceneManager.OpenScene(OfflineScenePath, OpenSceneMode.Single);

                check("offline scene has OfflineTestBootstrapper",
                    () => UnityEngine.Object.FindFirstObjectByType<OfflineTestBootstrapper>() != null);
                check("offline scene has ExpeditionManager",
                    () => UnityEngine.Object.FindFirstObjectByType<ExpeditionManager>() != null);
                check("offline scene has GhostSystem (scene or player prefab)",
                    HasGhostSystemAvailableOffline);
                check("offline scene has ReturnVoteSystem",
                    () => UnityEngine.Object.FindFirstObjectByType<ReturnVoteSystem>() != null);
                check("offline scene has HintManager",
                    () => UnityEngine.Object.FindFirstObjectByType<HintManager>() != null);
                check("offline scene has EmoteSystem (scene or player prefab)",
                    HasEmoteSystemAvailableOffline);
                check("offline scene has WeatherBoardManager",
                    () => UnityEngine.Object.FindFirstObjectByType<WeatherBoardManager>() != null);
                check("offline scene has SaveManager",
                    () => UnityEngine.Object.FindFirstObjectByType<SaveManager>() != null);

                var zoneRuntime = GameObject.Find("ZoneRuntime");
                check("offline scene has ZoneRuntime", () => zoneRuntime != null);
                check("offline scene has at least 6 zones",
                    () => zoneRuntime != null && zoneRuntime.transform.childCount >= 6);
                check("offline scene has route gates",
                    () => UnityEngine.Object.FindObjectsByType<RouteGate>(FindObjectsSortMode.None).Length >= 1);
                check("offline scene has shrines",
                    () => UnityEngine.Object.FindObjectsByType<ReviveShrine>(FindObjectsSortMode.None).Length >= 1);
                check("offline scene has spawn points",
                    () => UnityEngine.Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).Length >= 4);
            }
            finally
            {
                if (previous != null && previous.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previous);
            }
        }

        private static bool HasGhostSystemAvailableOffline()
        {
            if (UnityEngine.Object.FindFirstObjectByType<GhostSystem>() != null)
                return true;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return playerPrefab != null && playerPrefab.GetComponentInChildren<GhostSystem>(true) != null;
        }

        private static bool HasEmoteSystemAvailableOffline()
        {
            if (UnityEngine.Object.FindFirstObjectByType<EmoteSystem>() != null)
                return true;

            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            return playerPrefab != null && playerPrefab.GetComponentInChildren<EmoteSystem>(true) != null;
        }
    }
}
#endif
