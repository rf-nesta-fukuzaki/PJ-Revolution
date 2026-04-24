using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

public sealed class SettingsJsonStoreTests
{
    [Test]
    public void Save_CreatesSettingsJson_WithExpectedRootSections()
    {
        string root = CreateTempRoot();
        try
        {
            var settings = CreateSampleSettings();

            SettingsJsonStore.Save(root, settings, "OfflineTester", tutorialHintsEnabled: false);
            string settingsPath = SettingsJsonStore.BuildPath(root);

            Assert.That(File.Exists(settingsPath), Is.True, "settings.json が作成されていません。");

            string json = File.ReadAllText(settingsPath);
            Assert.That(json, Does.Contain("\"graphics\""));
            Assert.That(json, Does.Contain("\"audio\""));
            Assert.That(json, Does.Contain("\"controls\""));
            Assert.That(json, Does.Contain("\"accessibility\""));
            Assert.That(json, Does.Contain("\"gameplay\""));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Test]
    public void SaveThenLoad_RoundTrips_SettingsAndGameplay()
    {
        string root = CreateTempRoot();
        try
        {
            var expected = CreateSampleSettings();
            SettingsJsonStore.Save(root, expected, "TesterA", tutorialHintsEnabled: true);

            bool loaded = SettingsJsonStore.TryLoad(root, out var actual, out var gameplay);

            Assert.That(loaded, Is.True, "settings.json の読み込みに失敗しました。");
            Assert.That(actual.windowMode, Is.EqualTo(expected.windowMode));
            Assert.That(actual.qualityLevel, Is.EqualTo(expected.qualityLevel));
            Assert.That(actual.fpsCap, Is.EqualTo(expected.fpsCap));
            Assert.That(actual.masterVolume, Is.EqualTo(expected.masterVolume));
            Assert.That(actual.mouseSensitivity, Is.EqualTo(expected.mouseSensitivity).Within(0.001f));
            Assert.That(actual.invertY, Is.EqualTo(expected.invertY));
            Assert.That(actual.colorBlindMode, Is.EqualTo(expected.colorBlindMode));
            Assert.That(actual.crosshairColorHex, Is.EqualTo(expected.crosshairColorHex));
            Assert.That(gameplay.PlayerDisplayName, Is.EqualTo("TesterA"));
            Assert.That(gameplay.TutorialHintsEnabled, Is.True);
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    [Test]
    public void TryLoad_ReturnsFalse_WhenSettingsFileMissing()
    {
        string root = CreateTempRoot();
        try
        {
            bool loaded = SettingsJsonStore.TryLoad(root, out _, out var gameplay);
            Assert.That(loaded, Is.False);
            Assert.That(gameplay.TutorialHintsEnabled, Is.True);
            Assert.That(gameplay.PlayerDisplayName, Is.EqualTo(string.Empty));
        }
        finally
        {
            CleanupTempRoot(root);
        }
    }

    private static SettingsData CreateSampleSettings()
    {
        return new SettingsData(
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
    }

    private static string CreateTempRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "pj-revolution-settings-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void CleanupTempRoot(string root)
    {
        if (!Directory.Exists(root)) return;
        Directory.Delete(root, recursive: true);
    }
}
