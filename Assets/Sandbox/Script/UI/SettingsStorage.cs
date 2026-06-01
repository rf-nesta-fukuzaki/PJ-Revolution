using UnityEngine;

/// <summary>
/// 設定データの永続化（PlayerPrefs）。UI から独立した純粋 C# ストレージ層。
/// </summary>
public static class SettingsStorage
{
    private const string KeyResolution = "cfg_resolution";
    private const string KeyWindowMode = "cfg_window";
    private const string KeyQuality = "cfg_quality";
    private const string KeyFpsCap = "cfg_fps";
    private const string KeyVSync = "cfg_vsync";
    private const string KeyShadow = "cfg_shadow";
    private const string KeyParticle = "cfg_particle";
    private const string KeyMasterVol = "cfg_vol_master";
    private const string KeyBgmVol = "cfg_vol_bgm";
    private const string KeySeVol = "cfg_vol_se";
    private const string KeyVoiceVol = "cfg_vol_voice";
    private const string KeyMicGain = "cfg_mic_gain";
    private const string KeyMouseSens = "cfg_mouse_sens";
    private const string KeyInvertY = "cfg_invert_y";
    private const string KeyGamepadPreset = "cfg_gamepad";
    private const string KeySubtitles = "cfg_subtitles";
    private const string KeyUiScale = "cfg_ui_scale";
    private const string KeyColorBlind = "cfg_color_blind";
    private const string KeyCameraShake = "cfg_camera_shake";
    private const string KeyCrosshairHex = "cfg_crosshair";

    public static void SaveToPlayerPrefs(SettingsData s)
    {
        PlayerPrefs.SetInt(KeyResolution, s.resolutionIndex);
        PlayerPrefs.SetInt(KeyWindowMode, s.windowMode);
        PlayerPrefs.SetInt(KeyQuality, s.qualityLevel);
        PlayerPrefs.SetInt(KeyFpsCap, s.fpsCap);
        PlayerPrefs.SetInt(KeyVSync, s.vSync ? 1 : 0);
        PlayerPrefs.SetInt(KeyShadow, s.shadowQuality);
        PlayerPrefs.SetInt(KeyParticle, s.particleQuality);
        PlayerPrefs.SetInt(KeyMasterVol, s.masterVolume);
        PlayerPrefs.SetInt(KeyBgmVol, s.bgmVolume);
        PlayerPrefs.SetInt(KeySeVol, s.seVolume);
        PlayerPrefs.SetInt(KeyVoiceVol, s.voiceVolume);
        PlayerPrefs.SetInt(KeyMicGain, s.micGain);
        PlayerPrefs.SetFloat(KeyMouseSens, s.mouseSensitivity);
        PlayerPrefs.SetInt(KeyInvertY, s.invertY ? 1 : 0);
        PlayerPrefs.SetInt(KeyGamepadPreset, s.gamepadPreset);
        PlayerPrefs.SetInt(KeySubtitles, s.subtitles ? 1 : 0);
        PlayerPrefs.SetInt(KeyUiScale, s.uiScale);
        PlayerPrefs.SetInt(KeyColorBlind, s.colorBlindMode);
        PlayerPrefs.SetInt(KeyCameraShake, s.reduceCameraShake ? 1 : 0);
        PlayerPrefs.SetString(KeyCrosshairHex, s.crosshairColorHex);
        PlayerPrefs.Save();
    }

    public static SettingsData LoadFromPlayerPrefs()
    {
        return new SettingsData(
            resolutionIndex: PlayerPrefs.GetInt(KeyResolution, Screen.resolutions.Length - 1),
            windowMode: PlayerPrefs.GetInt(KeyWindowMode, 0),
            qualityLevel: PlayerPrefs.GetInt(KeyQuality, 2),
            fpsCap: PlayerPrefs.GetInt(KeyFpsCap, 1),
            vSync: PlayerPrefs.GetInt(KeyVSync, 1) == 1,
            shadowQuality: PlayerPrefs.GetInt(KeyShadow, 2),
            particleQuality: PlayerPrefs.GetInt(KeyParticle, 1),
            masterVolume: PlayerPrefs.GetInt(KeyMasterVol, 80),
            bgmVolume: PlayerPrefs.GetInt(KeyBgmVol, 70),
            seVolume: PlayerPrefs.GetInt(KeySeVol, 80),
            voiceVolume: PlayerPrefs.GetInt(KeyVoiceVol, 100),
            micGain: PlayerPrefs.GetInt(KeyMicGain, 100),
            mouseSensitivity: PlayerPrefs.GetFloat(KeyMouseSens, 3.0f),
            invertY: PlayerPrefs.GetInt(KeyInvertY, 0) == 1,
            gamepadPreset: PlayerPrefs.GetInt(KeyGamepadPreset, 0),
            subtitles: PlayerPrefs.GetInt(KeySubtitles, 0) == 1,
            uiScale: PlayerPrefs.GetInt(KeyUiScale, 100),
            colorBlindMode: PlayerPrefs.GetInt(KeyColorBlind, 0),
            reduceCameraShake: PlayerPrefs.GetInt(KeyCameraShake, 0) == 1,
            crosshairColorHex: PlayerPrefs.GetString(KeyCrosshairHex, "#FFFFFF"));
    }
}
