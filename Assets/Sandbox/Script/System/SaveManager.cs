using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// GDD §18 — セーブ/データ永続化。
///
/// profile.json を <see cref="Application.persistentDataPath"/>/ccc/ に保存。
/// settings.json の保存は SettingsManager が PlayerPrefs で担当する（EA版）。
///
/// 呼び出しパターン:
///   SaveManager.Instance.HasSeenHint(3)         — HintManager から
///   SaveManager.Instance.AddSeenHint(3)          — HintManager から
///   SaveManager.Instance.UpdateFromResult(score) — ResultScreen から
///   SaveManager.Instance.Profile                 — 現在のプロフィールを取得
/// </summary>
public class SaveManager : MonoBehaviour
{
    public static SaveManager Instance { get; private set; }

    private const string SAVE_DIR      = "ccc";
    private const string PROFILE_FILE  = "profile.json";
    private const string SCHEMA_VERSION = "1.0";

    // ── 現在のプロフィール ────────────────────────────────────
    public ProfileData Profile { get; private set; }

    private string SavePath =>
        Path.Combine(Application.persistentDataPath, SAVE_DIR, PROFILE_FILE);

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // DontDestroyOnLoad はルートGameObjectにしか機能しないため、親から切り離す
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        Profile = Load();
    }

    private void OnApplicationQuit() => Save();

    // ── 読み込み ─────────────────────────────────────────────
    private ProfileData Load()
    {
        if (!File.Exists(SavePath))
            return new ProfileData();

        try
        {
            string json = File.ReadAllText(SavePath);
            var data = JsonUtility.FromJson<ProfileData>(json);
            if (data == null) return new ProfileData();
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[SaveManager] profile.json 読み込み失敗: {e.Message}");
            return new ProfileData();
        }
    }

    // ── 保存 ─────────────────────────────────────────────────
    public void Save()
    {
        try
        {
            string dir = Path.GetDirectoryName(SavePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir!);

            Profile.version = SCHEMA_VERSION;
            string json = JsonUtility.ToJson(Profile, prettyPrint: true);
            File.WriteAllText(SavePath, json);
        }
        catch (Exception e)
        {
            Debug.LogError($"[SaveManager] profile.json 保存失敗: {e.Message}");
        }
    }

    // ── 遠征リザルト反映（GDD §18.2）────────────────────────
    /// <summary>リザルト画面完了時に呼び出し、累積統計を更新して保存する。</summary>
    public void UpdateFromResult(ScoreData score, bool allSurvived)
    {
        Debug.Assert(score != null, "[Contract] SaveManager.UpdateFromResult: score が null です");

        Profile.stats.totalExpeditions++;
        if (allSurvived)
            Profile.stats.totalSuccessExpeditions++;

        Profile.stats.cumulativeTeamScore += score.TeamScore;
        if (score.TeamScore > Profile.stats.bestExpeditionScore)
            Profile.stats.bestExpeditionScore = score.TeamScore;

        foreach (var relic in score.Relics)
        {
            Profile.stats.totalRelicsRecovered++;
            if (relic.Condition == RelicCondition.Perfect)
                Profile.stats.perfectRelicsRecovered++;
        }

        foreach (var ps in score.PlayerScores)
        {
            Profile.stats.totalDeaths      += ps.FallCount;
            Profile.stats.totalRelicDamage += ps.RelicDamageDealt;
        }

        Save();
        Debug.Log($"[SaveManager] 遠征結果を保存。累積スコア: {Profile.stats.cumulativeTeamScore}");
    }

    // ── ヒント管理（GDD §21.2）──────────────────────────────
    public bool HasSeenHint(int hintId) =>
        Profile.tutorial.seenHints.Contains(hintId);

    public void AddSeenHint(int hintId)
    {
        Debug.Assert(hintId >= 1, $"[Contract] SaveManager.AddSeenHint: hintId が無効です ({hintId})");
        if (HasSeenHint(hintId)) return;
        Profile.tutorial.seenHints.Add(hintId);
        Save();
    }

    public bool IsTutorialHintsEnabled() =>
        Profile.tutorial.hintsEnabled;

    public void SetTutorialHintsEnabled(bool enabled)
    {
        Profile.tutorial.hintsEnabled = enabled;
        Save();
    }

    // ── 表示名 ───────────────────────────────────────────────
    public string PlayerDisplayName
    {
        get => Profile.playerName;
        set { Profile.playerName = value; Save(); }
    }

    // ── 最高標高を更新 ────────────────────────────────────────
    public void UpdateHighestAltitude(float altitude)
    {
        if (altitude > Profile.stats.highestAltitude)
        {
            Profile.stats.highestAltitude = altitude;
            // 頻繁なファイル書き込みを避けるため遠征終了時のみ保存
        }
    }
}

// ── プロフィールデータ（JSON シリアライズ可能クラス）────────────
[Serializable]
public class ProfileData
{
    public string           version     = "1.0";
    public string           playerName  = "";
    public ProfileStats     stats       = new();
    public ProfileCosmetics cosmetics   = new();
    public ProfileTutorial  tutorial    = new();
}

[Serializable]
public class ProfileStats
{
    public int   totalExpeditions;
    public int   totalSuccessExpeditions;
    public int   totalRelicsRecovered;
    public int   perfectRelicsRecovered;
    public float totalDistanceKm;
    public int   cumulativeTeamScore;
    public int   bestExpeditionScore;
    public int   totalDeaths;
    public float highestAltitude;
    public float totalRelicDamage;
}

[Serializable]
public class ProfileCosmetics
{
    public List<string>      unlockedIds  = new() { "default" };
    public CosmeticEquipment equipped     = new();
}

[Serializable]
public class CosmeticEquipment
{
    public string hat      = "default";
    public string skin     = "default";
    public string backpack = "default";
    public string uiFrame  = "default";
}

[Serializable]
public class ProfileTutorial
{
    public List<int> seenHints          = new();
    public bool      shopGuideCompleted;
    public bool      hintsEnabled       = true;
}
