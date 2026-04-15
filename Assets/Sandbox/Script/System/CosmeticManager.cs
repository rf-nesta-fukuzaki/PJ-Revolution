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
        "pack_expedition"  // 全遺物完品回収
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
}
