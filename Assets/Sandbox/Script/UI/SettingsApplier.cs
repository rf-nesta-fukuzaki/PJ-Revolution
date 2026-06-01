using UnityEngine;

/// <summary>
/// 設定値のランタイム適用ロジック。MonoBehaviour / UI から分離したドメイン層。
/// </summary>
public static class SettingsApplier
{
    private static readonly int[] FpsOptions = { 30, 60, 120, 0 };

    public static void ApplyGraphics(SettingsData s)
    {
        var resolutions = Screen.resolutions;
        int idx = Mathf.Clamp(s.resolutionIndex, 0, Mathf.Max(0, resolutions.Length - 1));
        if (resolutions.Length > 0)
        {
            FullScreenMode mode = s.windowMode switch
            {
                1 => FullScreenMode.Windowed,
                2 => FullScreenMode.FullScreenWindow,
                _ => FullScreenMode.ExclusiveFullScreen,
            };
            Screen.SetResolution(resolutions[idx].width, resolutions[idx].height, mode);
        }

        QualitySettings.SetQualityLevel(s.qualityLevel, true);

        int fps = (s.fpsCap >= 0 && s.fpsCap < FpsOptions.Length) ? FpsOptions[s.fpsCap] : 60;
        Application.targetFrameRate = fps == 0 ? -1 : fps;
        QualitySettings.vSyncCount = s.vSync ? 1 : 0;
        ApplyShadowQuality(s.shadowQuality);
    }

    public static void ApplyShadowQuality(int level)
    {
        switch (level)
        {
            case 0:
                QualitySettings.shadows = ShadowQuality.Disable;
                break;
            case 1:
                QualitySettings.shadows = ShadowQuality.HardOnly;
                QualitySettings.shadowDistance = 30f;
                break;
            case 2:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 80f;
                break;
            case 3:
                QualitySettings.shadows = ShadowQuality.All;
                QualitySettings.shadowDistance = 150f;
                break;
        }
    }

    public static float VolumeToDb(int volume0To100)
    {
        float t = Mathf.Clamp(volume0To100, 0, 100) / 100f;
        return t < 0.001f ? -80f : 20f * Mathf.Log10(t);
    }

    public static void ApplyUiScale(int uiScalePercent)
    {
        float scale = uiScalePercent / 100f;
        foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
        {
            if (canvas.renderMode != RenderMode.WorldSpace)
                canvas.scaleFactor = scale;
        }
    }
}
