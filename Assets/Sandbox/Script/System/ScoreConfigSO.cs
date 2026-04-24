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
    [field: SerializeField, Min(0)] public int RelicIntactBonus  { get; private set; } = 50;

    [Header("チームボーナス")]
    [field: SerializeField, Min(0)] public int TeamSurvivalBonus { get; private set; } = 200;

    [Header("個人スコア")]
    [field: SerializeField, Min(0)] public int RopePlaceBonus  { get; private set; } = 5;
    [field: SerializeField, Min(0)] public int RelicFindBonus  { get; private set; } = 30;
    [field: SerializeField, Min(0)] public int RelicCarryBonus { get; private set; } = 2;   // per meter
    [field: SerializeField, Min(0)] public int GhostPinBonus   { get; private set; } = 10;

    [Header("ペナルティ")]
    [field: SerializeField, Range(0f, 1f)] public float RelicDamagePenaltyRate { get; private set; } = 0.5f;
    [field: SerializeField, Min(0)]        public int   ItemLostPenalty        { get; private set; } = 20;
}
