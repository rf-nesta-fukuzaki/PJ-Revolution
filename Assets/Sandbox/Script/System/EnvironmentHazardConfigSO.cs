using UnityEngine;

/// <summary>
/// GDD §3.4 / §5.2 — 環境ハザードの全パラメータを管理する ScriptableObject。
///
/// 以下のシステムが参照する：
///   - FrostbiteDamage    (凍傷ダメージ)
///   - RelicFreezeDamage  (遺物凍結ダメージ)
///   - AltitudeSicknessEffect (高山病)
///
/// 生成: Assets > Create > PeakPlunder > EnvironmentHazardConfig
/// </summary>
[CreateAssetMenu(menuName = "PeakPlunder/EnvironmentHazardConfig", fileName = "EnvironmentHazardConfig")]
public class EnvironmentHazardConfigSO : ScriptableObject
{
    [Header("標高ゾーン（GDD §3.1 ゾーン境界）")]
    [field: SerializeField, Min(0f)] public float BlizzardAltitudeMin  { get; private set; } = 1600f;
    [field: SerializeField, Min(0f)] public float AltitudeSicknessMin  { get; private set; } = 2000f;

    [Header("凍傷ダメージ（プレイヤー / GDD §3.4）")]
    [field: SerializeField, Min(0f)] public float FrostDamagePerSec    { get; private set; } = 5f;

    [Header("遺物凍結ダメージ（GDD §5.2）")]
    [field: SerializeField, Min(0f)] public float RelicFreezeDamagePerSec { get; private set; } = 2f;

    [Header("高山病（GDD §3.4）")]
    [field: SerializeField, Range(0f, 1f)] public float AltitudeSpeedPenalty  { get; private set; } = 0.30f;
    [field: SerializeField, Min(0f)]       public float AltitudeEffectLerp    { get; private set; } = 2f;
}
