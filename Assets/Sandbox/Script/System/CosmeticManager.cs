using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §9.5 — コスメティック報酬システム（骨格実装）。
/// PlayerPrefs でアンロック状態を永続化する。
/// リザルト画面から ProcessResultRewards() を呼ぶことで、称号・スコアに応じてコスメを解放する。
/// </summary>
public class CosmeticManager : MonoBehaviour
{
    public static CosmeticManager Instance { get; private set; }

    private const string KEY_PREFIX = "Cosmetic_";

    // GDD §9.5 定義のコスメティック ID リスト
    public static readonly string[] ALL_COSMETICS =
    {
        "skin_default",    // 最初から解放
        "skin_snowman",    // チームスコア 500pt 以上
        "skin_ninja",      // 吹雪の中で遺物を完品回収
        "hat_beanie",      // 最初から解放
        "hat_hardhat",     // 5 分以内クリア
        "hat_explorer",    // 遺物を完品で 1 個以上回収
        "pack_standard",   // 最初から解放
        "pack_large",      // 3 遺物以上回収
        "pack_expedition", // 全遺物完品回収

        // ── GDD §12.6 メタ進行（累積チームスコア閾値による解放）──
        "hat_adventurer_brown",       //    500pt — 冒険者キャップ（茶）
        "pack_duck_pattern",          //  1,500pt — アヒル柄バックパック
        "skin_ancient_robe_blue",     //  3,000pt — 古代文明ローブ（青）
        "hat_priest_mask",            //  5,000pt — 祭司の仮面
        "pack_golden_duck",           //  8,000pt — 黄金あひるバックパック
        "hat_crystal_crown",          // 12,000pt — クリスタルの冠
        "skin_ruin_explorer_suit",    // 18,000pt — 遺跡探検家スーツ
        "pack_ancient_wings",         // 25,000pt — 古代文明の翼（装飾背負い物）
        "skin_legend_hunter_gold",    // 35,000pt — 伝説のハンタースーツ（金）
        "frame_ancient_master"        // 50,000pt — 「古代文明マスター」称号フレーム
    };

    /// <summary>GDD §12.6 表: 累積チームスコア閾値と解放コスメ ID の対応。</summary>
    public static readonly (int scoreThreshold, string cosmeticId)[] CUMULATIVE_UNLOCKS =
    {
        (   500, "hat_adventurer_brown"),
        ( 1_500, "pack_duck_pattern"),
        ( 3_000, "skin_ancient_robe_blue"),
        ( 5_000, "hat_priest_mask"),
        ( 8_000, "pack_golden_duck"),
        (12_000, "hat_crystal_crown"),
        (18_000, "skin_ruin_explorer_suit"),
        (25_000, "pack_ancient_wings"),
        (35_000, "skin_legend_hunter_gold"),
        (50_000, "frame_ancient_master")
    };

    public event System.Action<string> OnCosmeticUnlocked;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        transform.SetParent(null);   // ルートに移動してから DDOL を呼ぶ
        DontDestroyOnLoad(gameObject);

        // デフォルト解放済みコスメを初期化
        Unlock("skin_default");
        Unlock("hat_beanie");
        Unlock("pack_standard");
    }

    // ── クエリ ────────────────────────────────────────────────
    public bool IsUnlocked(string id) =>
        PlayerPrefs.GetInt(KEY_PREFIX + id, 0) == 1;

    public List<string> GetAllUnlocked()
    {
        var list = new List<string>();
        foreach (var id in ALL_COSMETICS)
            if (IsUnlocked(id)) list.Add(id);
        return list;
    }

    // ── アンロック ────────────────────────────────────────────
    public void Unlock(string id)
    {
        if (IsUnlocked(id)) return;

        PlayerPrefs.SetInt(KEY_PREFIX + id, 1);
        PlayerPrefs.Save();
        OnCosmeticUnlocked?.Invoke(id);
        Debug.Log($"[Cosmetic] アンロック: {id}");
    }

    // ── リザルト連動解放（ResultScreen.Show から呼ぶ）────────
    /// <summary>リザルト称号・スコアに基づいてコスメティックを解放する。</summary>
    public void ProcessResultRewards(ScoreData score)
    {
        if (score == null) return;

        // 5 分以内クリア → ハードハット
        if (score.ClearTimeSeconds > 0f && score.ClearTimeSeconds < 300f)
            Unlock("hat_hardhat");

        // チームスコア 500pt 以上 → スノーマンスキン
        if (score.TeamScore >= 500)
            Unlock("skin_snowman");

        // 遺物を 1 個以上完品で回収 → エクスプローラーハット
        foreach (var relic in score.Relics)
        {
            if (relic != null && relic.Condition == RelicCondition.Perfect)
            {
                Unlock("hat_explorer");
                break;
            }
        }

        // 遺物を 3 個以上回収 → ラージパック
        if (score.Relics.Count >= 3)
            Unlock("pack_large");

        // 全遺物完品回収 → エクスペディションパック
        bool allPerfect = score.Relics.Count > 0;
        foreach (var relic in score.Relics)
        {
            if (relic == null || relic.Condition != RelicCondition.Perfect)
            {
                allPerfect = false;
                break;
            }
        }
        if (allPerfect)
            Unlock("pack_expedition");
    }

    /// <summary>
    /// GDD §12.6 メタ進行: 累積チームスコアに応じて対応するコスメティックを解放する。
    /// SaveManager.UpdateFromResult 直後に呼び、しきい値を超えたアイテムを一度に解放する。
    /// </summary>
    public void ProcessCumulativeRewards(int cumulativeTeamScore)
    {
        if (cumulativeTeamScore <= 0) return;

        foreach (var (threshold, id) in CUMULATIVE_UNLOCKS)
        {
            if (cumulativeTeamScore >= threshold)
                Unlock(id);
        }
    }
}
