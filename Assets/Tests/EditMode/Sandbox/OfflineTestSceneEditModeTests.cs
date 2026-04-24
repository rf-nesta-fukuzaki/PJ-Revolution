#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class OfflineTestSceneEditModeTests
{
    private const string OfflineScenePath = "Assets/Sandbox/Scene/OfflineTestScene.unity";

    [Test]
    public void OfflineTestScene_HasOfflineBootstrapper_AndCoreSystems()
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(OfflineScenePath, OpenSceneMode.Single);

            Assert.That(Object.FindFirstObjectByType<OfflineTestBootstrapper>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<ExpeditionManager>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<GhostSystem>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<ReturnVoteSystem>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<HintManager>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<EmoteSystem>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<WeatherBoardManager>(), Is.Not.Null);
            Assert.That(Object.FindFirstObjectByType<SaveManager>(), Is.Not.Null);
        }
        finally
        {
            if (previous != null && previous.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }

    [Test]
    public void OfflineTestScene_HasMinimumMountainStructure()
    {
        SceneSetup[] previous = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene(OfflineScenePath, OpenSceneMode.Single);

            GameObject zoneRuntime = GameObject.Find("ZoneRuntime");
            Assert.That(zoneRuntime, Is.Not.Null, "ZoneRuntime が見つかりません。");
            Assert.That(zoneRuntime.transform.childCount, Is.GreaterThanOrEqualTo(6),
                "ZoneRuntime 配下のゾーン数が不足しています。");

            int routeGates = Object.FindObjectsByType<RouteGate>(FindObjectsSortMode.None).Length;
            int shrines = Object.FindObjectsByType<ReviveShrine>(FindObjectsSortMode.None).Length;
            int spawnPoints = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None).Length;

            Assert.That(routeGates, Is.GreaterThanOrEqualTo(1), "RouteGate がありません。");
            Assert.That(shrines, Is.GreaterThanOrEqualTo(1), "ReviveShrine がありません。");
            Assert.That(spawnPoints, Is.GreaterThanOrEqualTo(4), "SpawnPoint が不足しています。");
        }
        finally
        {
            if (previous != null && previous.Length > 0)
                EditorSceneManager.RestoreSceneManagerSetup(previous);
        }
    }
}
#endif
