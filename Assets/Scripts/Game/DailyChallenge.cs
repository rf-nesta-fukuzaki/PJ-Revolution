using System;
using UnityEngine;

/// <summary>
/// デイリーチャレンジ基盤。static クラス（MonoBehaviour 不要）。
///
/// [シード]
///   GetDailySeed() は DateTime.UtcNow.ToString("yyyyMMdd").GetHashCode() を返す。
///   同じ日は同じシードで洞窟が生成され、全プレイヤーが同一マップをプレイできる。
///
/// [スコア保存]
///   PlayerPrefs キー: "DailyBest_{yyyyMMdd}_Time" / "DailyBest_{yyyyMMdd}_Gems"
///   「宝石が多い、または同数で時間が短い場合」のみ更新する。
///
/// [IsDailyMode]
///   TitleScreenUI のデイリーチャレンジボタンが true に設定する。
///   GameManager が EscapeSuccess / AllDowned 時に SaveScore() を呼ぶ。
/// </summary>
public static class DailyChallenge
{
    // ─────────────── プロパティ ───────────────

    /// <summary>デイリーチャレンジモード中か。TitleScreenUI からセットする。</summary>
    public static bool IsDailyMode { get; set; } = false;

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// 本日の洞窟シードを返す。
    /// DateTime.UtcNow.ToString("yyyyMMdd").GetHashCode() で算出するため
    /// 同日中は常に同じ値が返る。
    /// </summary>
    public static int GetDailySeed()
        => DateTime.UtcNow.ToString("yyyyMMdd").GetHashCode();

    /// <summary>本日の日付文字列 "yyyy/MM/dd" を返す。UI 表示用。</summary>
    public static string GetDailyDateString()
        => DateTime.UtcNow.ToString("yyyy/MM/dd");

    /// <summary>
    /// 本日のベストスコアを更新する。
    /// 「宝石が多い or 同数で時間が短い」場合のみ PlayerPrefs を上書きする。
    /// </summary>
    /// <param name="elapsedTime">クリア / ゲームオーバーまでの経過時間（秒）</param>
    /// <param name="gems">収集した宝石数</param>
    public static void SaveScore(float elapsedTime, int gems)
    {
        string date     = DateTime.UtcNow.ToString("yyyyMMdd");
        string keyTime  = $"DailyBest_{date}_Time";
        string keyGems  = $"DailyBest_{date}_Gems";

        float bestTime  = PlayerPrefs.GetFloat(keyTime,  float.MaxValue);
        int   bestGems  = PlayerPrefs.GetInt  (keyGems,  0);

        bool update = gems > bestGems || (gems == bestGems && elapsedTime < bestTime);

        if (update)
        {
            PlayerPrefs.SetFloat(keyTime,  elapsedTime);
            PlayerPrefs.SetInt  (keyGems,  gems);
            PlayerPrefs.Save();
            Debug.Log($"[DailyChallenge] ベストスコア更新: 時間={elapsedTime:F1}s, 宝石={gems}個");
        }
        else
        {
            Debug.Log($"[DailyChallenge] スコア未更新（既存ベスト: 時間={bestTime:F1}s, 宝石={bestGems}個）");
        }
    }

    /// <summary>
    /// 本日のベストスコアを返す。記録がない場合は time=0, gems=0 を返す。
    /// </summary>
    public static (float time, int gems) GetBestScore()
    {
        string date    = DateTime.UtcNow.ToString("yyyyMMdd");
        float  time    = PlayerPrefs.GetFloat($"DailyBest_{date}_Time",  0f);
        int    gems    = PlayerPrefs.GetInt  ($"DailyBest_{date}_Gems",  0);
        return (time, gems);
    }
}
