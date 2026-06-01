using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// SandboxOfflineCombined 向けローカル Co-op 設定（最大4人パーティ、不足分は NPC 補充）。
/// </summary>
public static class LocalCoopSettings
{
    public const int MaxPartySize = 4;
    public const string HumanCountPlayerPrefsKey = "SandboxLocalCoopHumanCount";

    /// <summary>パーティシステム（4スロット）が有効なシーンで true。</summary>
    public static bool IsActive { get; internal set; }

    /// <summary>オンラインマルチ（NGO）モード。</summary>
    public static bool IsOnline => PlayMode == PartyPlayMode.Online;

    public static PartyPlayMode PlayMode { get; internal set; } = PartyPlayMode.OfflineLocal;

    /// <summary>人間プレイヤー数（1〜4）。</summary>
    public static int HumanCount { get; internal set; } = 1;

    public static int NpcFillCount => Mathf.Clamp(MaxPartySize - HumanCount, 0, MaxPartySize);

    public static void Configure(PartyPlayMode mode)
    {
        PlayMode = mode;
        IsActive = true;
    }

    public static int ResolveHumanCount(int configured, bool autoDetectGamepads)
    {
        int count = Mathf.Clamp(configured, 1, MaxPartySize);
        if (!autoDetectGamepads) return count;

        int detected = 1;
        if (Gamepad.all.Count > 0)
            detected = Mathf.Min(MaxPartySize, 1 + Gamepad.all.Count);

        return Mathf.Clamp(Mathf.Max(count, detected), 1, MaxPartySize);
    }

    public static int LoadHumanCountFromPrefs(int fallback)
    {
        if (!PlayerPrefs.HasKey(HumanCountPlayerPrefsKey))
            return fallback;
        return Mathf.Clamp(PlayerPrefs.GetInt(HumanCountPlayerPrefsKey, fallback), 1, MaxPartySize);
    }

    public static void SaveHumanCountToPrefs(int count)
    {
        PlayerPrefs.SetInt(HumanCountPlayerPrefsKey, Mathf.Clamp(count, 1, MaxPartySize));
        PlayerPrefs.Save();
    }
}
