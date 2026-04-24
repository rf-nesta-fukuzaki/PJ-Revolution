using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public readonly struct SettingsGameplaySnapshot
{
    public static readonly SettingsGameplaySnapshot Default = new(string.Empty, true);

    public string PlayerDisplayName { get; }
    public bool TutorialHintsEnabled { get; }

    public SettingsGameplaySnapshot(string playerDisplayName, bool tutorialHintsEnabled)
    {
        PlayerDisplayName = playerDisplayName ?? string.Empty;
        TutorialHintsEnabled = tutorialHintsEnabled;
    }
}

/// <summary>
/// GDD §18.6 settings.json の入出力。
/// オフライン検証シーンでも仕様に沿った設定永続化を行う。
/// </summary>
public static class SettingsJsonStore
{
    private const string SaveDirName = "ccc";
    private const string SettingsFileName = "settings.json";
    private const string SchemaVersion = "1.0";

    private static readonly int[] FpsOptions = { 30, 60, 120, 0 };

    public static string BuildPath(string persistentDataPath)
    {
        string root = string.IsNullOrWhiteSpace(persistentDataPath)
            ? Application.persistentDataPath
            : persistentDataPath;
        return Path.Combine(root, SaveDirName, SettingsFileName);
    }

    public static void Save(string persistentDataPath, SettingsData settings, string playerDisplayName, bool tutorialHintsEnabled)
    {
        string path = BuildPath(persistentDataPath);
        try
        {
            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var document = CreateDocument(settings, playerDisplayName, tutorialHintsEnabled);
            string json = JsonUtility.ToJson(document, prettyPrint: true);
            File.WriteAllText(path, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SettingsJsonStore] settings.json 保存失敗: {e.Message}");
        }
    }

    public static bool TryLoad(string persistentDataPath, out SettingsData settings, out SettingsGameplaySnapshot gameplay)
    {
        settings = default;
        gameplay = SettingsGameplaySnapshot.Default;

        string path = BuildPath(persistentDataPath);
        if (!File.Exists(path)) return false;

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return false;

            var document = JsonUtility.FromJson<SettingsJsonDocument>(json);
            if (document == null) return false;

            settings = ToSettingsData(document);
            gameplay = ToGameplaySnapshot(document);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SettingsJsonStore] settings.json 読み込み失敗: {e.Message}");
            return false;
        }
    }

    private static SettingsJsonDocument CreateDocument(
        SettingsData settings,
        string playerDisplayName,
        bool tutorialHintsEnabled)
    {
        return new SettingsJsonDocument
        {
            version = SchemaVersion,
            graphics = new GraphicsJson
            {
                resolution = ToResolutionToken(settings.resolutionIndex),
                windowMode = ToWindowModeToken(settings.windowMode),
                qualityPreset = ToQualityPresetToken(settings.qualityLevel),
                fpsLimit = ToFpsLimit(settings.fpsCap),
                vSync = settings.vSync,
                shadowQuality = ToShadowQualityToken(settings.shadowQuality),
                particleQuality = ToParticleQualityToken(settings.particleQuality)
            },
            audio = new AudioJson
            {
                masterVolume = Mathf.Clamp(settings.masterVolume, 0, 100),
                bgmVolume = Mathf.Clamp(settings.bgmVolume, 0, 100),
                seVolume = Mathf.Clamp(settings.seVolume, 0, 100),
                voiceChatVolume = Mathf.Clamp(settings.voiceVolume, 0, 100),
                micGain = Mathf.Clamp(settings.micGain, 0, 200)
            },
            controls = new ControlsJson
            {
                mouseSensitivity = Mathf.Clamp(settings.mouseSensitivity, 0.5f, 10f),
                mouseYInvert = settings.invertY,
                gamepadPreset = ToGamepadPresetToken(settings.gamepadPreset),
                keyBindings = new KeyBindingsJson()
            },
            accessibility = new AccessibilityJson
            {
                subtitles = settings.subtitles,
                uiScale = Mathf.Clamp(settings.uiScale, 80, 150),
                colorBlindMode = ToColorBlindModeToken(settings.colorBlindMode),
                crosshairColor = NormalizeCrosshairHex(settings.crosshairColorHex),
                cameraShakeReduction = settings.reduceCameraShake
            },
            gameplay = new GameplayJson
            {
                playerDisplayName = playerDisplayName ?? string.Empty,
                tutorialHintsEnabled = tutorialHintsEnabled
            }
        };
    }

    private static SettingsData ToSettingsData(SettingsJsonDocument document)
    {
        var graphics = document.graphics ?? new GraphicsJson();
        var audio = document.audio ?? new AudioJson();
        var controls = document.controls ?? new ControlsJson();
        var accessibility = document.accessibility ?? new AccessibilityJson();

        return new SettingsData(
            resolutionIndex: ToResolutionIndex(graphics.resolution),
            windowMode: ToWindowModeIndex(graphics.windowMode),
            qualityLevel: ToQualityPresetIndex(graphics.qualityPreset),
            fpsCap: ToFpsCapIndex(graphics.fpsLimit),
            vSync: graphics.vSync,
            shadowQuality: ToShadowQualityIndex(graphics.shadowQuality),
            particleQuality: ToParticleQualityIndex(graphics.particleQuality),
            masterVolume: Mathf.Clamp(audio.masterVolume, 0, 100),
            bgmVolume: Mathf.Clamp(audio.bgmVolume, 0, 100),
            seVolume: Mathf.Clamp(audio.seVolume, 0, 100),
            voiceVolume: Mathf.Clamp(audio.voiceChatVolume, 0, 100),
            micGain: Mathf.Clamp(audio.micGain, 0, 200),
            mouseSensitivity: Mathf.Clamp(controls.mouseSensitivity, 0.5f, 10f),
            invertY: controls.mouseYInvert,
            gamepadPreset: ToGamepadPresetIndex(controls.gamepadPreset),
            subtitles: accessibility.subtitles,
            uiScale: Mathf.Clamp(accessibility.uiScale, 80, 150),
            colorBlindMode: ToColorBlindModeIndex(accessibility.colorBlindMode),
            reduceCameraShake: accessibility.cameraShakeReduction,
            crosshairColorHex: NormalizeCrosshairHex(accessibility.crosshairColor));
    }

    private static SettingsGameplaySnapshot ToGameplaySnapshot(SettingsJsonDocument document)
    {
        var gameplay = document.gameplay ?? new GameplayJson();
        return new SettingsGameplaySnapshot(gameplay.playerDisplayName, gameplay.tutorialHintsEnabled);
    }

    private static string ToResolutionToken(int resolutionIndex)
    {
        var resolutions = Screen.resolutions;
        if (resolutions.Length == 0) return $"{Screen.width}x{Screen.height}";

        int index = Mathf.Clamp(resolutionIndex, 0, resolutions.Length - 1);
        Resolution r = resolutions[index];
        return $"{r.width}x{r.height}";
    }

    private static int ToResolutionIndex(string resolutionToken)
    {
        var resolutions = Screen.resolutions;
        if (resolutions.Length == 0) return 0;

        if (!TryParseResolution(resolutionToken, out int width, out int height))
            return resolutions.Length - 1;

        for (int i = resolutions.Length - 1; i >= 0; i--)
        {
            if (resolutions[i].width == width && resolutions[i].height == height)
                return i;
        }

        return resolutions.Length - 1;
    }

    private static bool TryParseResolution(string token, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (string.IsNullOrWhiteSpace(token)) return false;

        string[] parts = token.Split('x', 'X');
        if (parts.Length != 2) return false;

        bool parsedWidth = int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width);
        bool parsedHeight = int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
        return parsedWidth && parsedHeight && width > 0 && height > 0;
    }

    private static string ToWindowModeToken(int windowMode)
    {
        return windowMode switch
        {
            1 => "windowed",
            2 => "borderless",
            _ => "fullscreen",
        };
    }

    private static int ToWindowModeIndex(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 0;
        string normalized = token.Trim().ToLowerInvariant();
        if (normalized == "windowed") return 1;
        if (normalized == "borderless") return 2;
        return 0;
    }

    private static string ToQualityPresetToken(int qualityLevel)
    {
        return qualityLevel switch
        {
            <= 0 => "low",
            1 => "medium",
            2 => "high",
            _ => "ultra",
        };
    }

    private static int ToQualityPresetIndex(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 2;
        string normalized = token.Trim().ToLowerInvariant();
        return normalized switch
        {
            "low" => 0,
            "medium" => 1,
            "high" => 2,
            "ultra" => 3,
            _ => 2,
        };
    }

    private static int ToFpsLimit(int fpsCapIndex)
    {
        if (fpsCapIndex < 0 || fpsCapIndex >= FpsOptions.Length) return 60;
        return FpsOptions[fpsCapIndex];
    }

    private static int ToFpsCapIndex(int fpsLimit)
    {
        if (fpsLimit <= 0) return 3;
        if (fpsLimit <= 30) return 0;
        if (fpsLimit <= 60) return 1;
        return 2;
    }

    private static string ToShadowQualityToken(int quality)
    {
        return quality switch
        {
            <= 0 => "off",
            1 => "low",
            2 => "medium",
            _ => "high",
        };
    }

    private static int ToShadowQualityIndex(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 2;
        string normalized = token.Trim().ToLowerInvariant();
        return normalized switch
        {
            "off" => 0,
            "low" => 1,
            "medium" => 2,
            "high" => 3,
            _ => 2,
        };
    }

    private static string ToParticleQualityToken(int quality)
    {
        return quality switch
        {
            <= 0 => "low",
            1 => "medium",
            _ => "high",
        };
    }

    private static int ToParticleQualityIndex(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 1;
        string normalized = token.Trim().ToLowerInvariant();
        return normalized switch
        {
            "low" => 0,
            "medium" => 1,
            "high" => 2,
            _ => 1,
        };
    }

    private static string ToGamepadPresetToken(int preset)
    {
        return preset switch
        {
            1 => "alternative_a",
            2 => "alternative_b",
            _ => "default",
        };
    }

    private static int ToGamepadPresetIndex(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 0;
        string normalized = token.Trim().ToLowerInvariant();
        return normalized switch
        {
            "alternative_a" => 1,
            "alternative_b" => 2,
            _ => 0,
        };
    }

    private static string ToColorBlindModeToken(int mode)
    {
        return mode switch
        {
            1 => "protan",
            2 => "deutan",
            3 => "tritan",
            _ => "off",
        };
    }

    private static int ToColorBlindModeIndex(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return 0;
        string normalized = token.Trim().ToLowerInvariant();
        return normalized switch
        {
            "protan" => 1,
            "deutan" => 2,
            "tritan" => 3,
            _ => 0,
        };
    }

    private static string NormalizeCrosshairHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return "#FFFFFF";
        string normalized = hex.Trim();
        if (!normalized.StartsWith("#", StringComparison.Ordinal))
            normalized = $"#{normalized}";

        return ColorUtility.TryParseHtmlString(normalized, out _)
            ? normalized.ToUpperInvariant()
            : "#FFFFFF";
    }

    [Serializable]
    private sealed class SettingsJsonDocument
    {
        public string version = SchemaVersion;
        public GraphicsJson graphics = new();
        public AudioJson audio = new();
        public ControlsJson controls = new();
        public AccessibilityJson accessibility = new();
        public GameplayJson gameplay = new();
    }

    [Serializable]
    private sealed class GraphicsJson
    {
        public string resolution = "1920x1080";
        public string windowMode = "fullscreen";
        public string qualityPreset = "medium";
        public int fpsLimit = 60;
        public bool vSync = true;
        public string shadowQuality = "medium";
        public string particleQuality = "medium";
    }

    [Serializable]
    private sealed class AudioJson
    {
        public int masterVolume = 80;
        public int bgmVolume = 70;
        public int seVolume = 80;
        public int voiceChatVolume = 100;
        public int micGain = 100;
    }

    [Serializable]
    private sealed class ControlsJson
    {
        public float mouseSensitivity = 3f;
        public bool mouseYInvert;
        public string gamepadPreset = "default";
        public KeyBindingsJson keyBindings = new();
    }

    [Serializable]
    private sealed class KeyBindingsJson
    {
    }

    [Serializable]
    private sealed class AccessibilityJson
    {
        public bool subtitles;
        public int uiScale = 100;
        public string colorBlindMode = "off";
        public string crosshairColor = "#FFFFFF";
        public bool cameraShakeReduction;
    }

    [Serializable]
    private sealed class GameplayJson
    {
        public string playerDisplayName = string.Empty;
        public bool tutorialHintsEnabled = true;
    }
}
