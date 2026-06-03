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
    // 標高ハザードの閾値は「山高に対する割合 (0=基地, 1=山頂)」で持ち、実行時に MountainProfile
    // （CombinedTerrainConformer が地形ベイク後に公開）で実 World Y へ変換する。
    // 旧来は絶対メートル(1600m/2000m)固定だったが、手続き山の実山高(~460m)に永遠に届かず凍傷・
    // 高山病が死蔵していた。割合化により、低地→中腹(凍傷)→高地(高山病) が実山高に必ず追従する。
    [Header("標高ハザード — 山高に対する割合 (0=基地, 1=山頂)。MountainProfile に連動")]
    [Tooltip("吹雪による凍傷が発生し始める高度（山高に対する割合）。中腹 0.5 で発火開始。")]
    [SerializeField, Range(0f, 1f)] private float _blizzardFraction = 0.5f;
    [Tooltip("高山病が発生し始める高度（山高に対する割合）。高地 0.75 で発火開始。")]
    [SerializeField, Range(0f, 1f)] private float _altitudeSicknessFraction = 0.75f;

    [Header("フォールバック絶対標高（MountainProfile 未準備時のみ使用）")]
    [SerializeField, Min(0f)] private float _blizzardAltitudeFallback = 1600f;
    [SerializeField, Min(0f)] private float _altitudeSicknessFallback = 2000f;

    /// <summary>吹雪凍傷の発生高度（World Y）。実行時は実山高に連動、未準備時は絶対フォールバック。</summary>
    public float BlizzardAltitudeMin => MountainProfile.IsReady
        ? MountainProfile.WorldYAtFraction(_blizzardFraction)
        : _blizzardAltitudeFallback;

    /// <summary>高山病の発生高度（World Y）。実行時は実山高に連動、未準備時は絶対フォールバック。</summary>
    public float AltitudeSicknessMin => MountainProfile.IsReady
        ? MountainProfile.WorldYAtFraction(_altitudeSicknessFraction)
        : _altitudeSicknessFallback;

    [Header("凍傷ダメージ（プレイヤー / GDD §3.4）")]
    [field: SerializeField, Min(0f)] public float FrostDamagePerSec    { get; private set; } = 5f;

    [Header("遺物凍結ダメージ（GDD §5.2）")]
    [field: SerializeField, Min(0f)] public float RelicFreezeDamagePerSec { get; private set; } = 2f;

    [Header("高山病（GDD §3.4）")]
    [field: SerializeField, Range(0f, 1f)] public float AltitudeSpeedPenalty  { get; private set; } = 0.30f;
    [field: SerializeField, Min(0f)]       public float AltitudeEffectLerp    { get; private set; } = 2f;
}
