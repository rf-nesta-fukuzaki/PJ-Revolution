using System.Collections.Generic;

/// <summary>
/// 遠征リザルトのデータ。ScoreTracker が生成し ResultScreen が表示する。
/// ResultScreen.cs から分離してドメインデータと UI を切り離す。
/// </summary>
[System.Serializable]
public class ScoreData
{
    public int               TeamScore;
    public float             ClearTimeSeconds;
    public List<RelicBase>   Relics       = new();
    public List<PlayerScore> PlayerScores = new();
}

/// <summary>個人スコアおよびコメディ称号判定用の統計（GDD §12.5）。</summary>
[System.Serializable]
public class PlayerScore
{
    public string PlayerName;
    public int    IndividualScore;
    public int    FallCount;
    public int    ItemsLost;
    public float  RelicDamageDealt;
    public int    GhostContributions;
    public int    RopePlacementCount;
    public int    ShoutCount;             // 歌う壺ボイチャ妨害による「叫び」回数
    public int    RelicsFoundCount;       // GDD §12.5「遺物マスター」用: 発見/運搬した遺物数
    public bool   Survived;               // GDD §12.5「鉄人ハンター」用: 一度も死亡せずに帰還
}
