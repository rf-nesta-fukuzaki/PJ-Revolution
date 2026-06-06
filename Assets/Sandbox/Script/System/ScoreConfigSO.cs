using UnityEngine;

/// <summary>
/// GDD §9 — スコア計算の全設定値を一元管理する ScriptableObject。
/// ゲームバランスの調整をコード変更なしで行える（データ駆動設計）。
/// 生成: Assets > Create > PeakPlunder > ScoreConfig
/// </summary>
[CreateAssetMenu(menuName = "PeakPlunder/ScoreConfig", fileName = "ScoreConfig")]
public class ScoreConfigSO : ScriptableObject
{
    [Header("遺物報酬")]
    // (GDD のチームスコア式には含まれない補助ボーナス。後方互換のため残置・既定では未使用)
    [field: SerializeField, Min(0)] public int RelicIntactBonus  { get; private set; } = 50;

    [Header("チームボーナス (GDD §12.1)")]
    [field: SerializeField, Min(0)]  public int   TeamSurvivalBonus       { get; private set; } = 200; // 旧加算式(未使用)
    // GDD §12.1 — 全員生還で teamScore に乗算する係数(1.2)。1人以上死亡で 1.0。
    [field: SerializeField, Min(1f)] public float SurvivalBonusMultiplier { get; private set; } = 1.2f;

    [Header("報酬配分 (GDD §12.4)")]
    // 各プレイヤーが貢献0でも保証される最低配分比率（4人で各10%）。
    [field: SerializeField, Range(0f, 0.25f)] public float MinRewardShare { get; private set; } = 0.10f;

    [Header("個人スコア (GDD §12.2)")]
    [field: SerializeField, Min(0)] public int   RopePlaceBonus      { get; private set; } = 15;  // ロープ設置/アンカー 15pt/回
    [field: SerializeField, Min(0)] public int   RelicFindBonus      { get; private set; } = 50;  // 遺物発見 50pt/個
    [field: SerializeField, Min(0f)] public float RelicCarryBonus    { get; private set; } = 0.2f; // 運搬距離 1pt/5m = 0.2pt/m
    [field: SerializeField, Min(0)] public int   GhostPinBonus       { get; private set; } = 10;  // 偵察ピン 10pt/回

    [Header("ペナルティ (GDD §12.3)")]
    [field: SerializeField, Min(0f)] public float RelicDamagePenaltyRate { get; private set; } = 2.0f; // 遺物ダメージ -2pt/dmg
    [field: SerializeField, Min(0)]  public int   ItemLostPenalty        { get; private set; } = 10;  // 装備喪失 -10pt/個
    [field: SerializeField, Min(0)]  public int   RelicDestroyedPenalty  { get; private set; } = 50;  // 遺物破壊 -50pt/個
}
