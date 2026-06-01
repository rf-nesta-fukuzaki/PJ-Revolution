using UnityEngine;

namespace Sandbox.World.Config
{
    /// <summary>
    /// バイオーム分類しきい値 (Module 3)。高度 [m] と斜度 [deg] で
    /// 支配的バイオーム index を決める。index は BiomeId と一致させる。
    /// </summary>
    [CreateAssetMenu(menuName = "PJ-Revolution/World/Biome Params", fileName = "BiomeParams")]
    public sealed class BiomeParams : ScriptableObject
    {
        [Header("Altitude thresholds [m]")]
        public float seaLevel = 0f;
        [Min(0f)] public float beachMaxAltitude = 2f;
        [Min(0f)] public float grassMaxAltitude = 16f;
        [Min(0f)] public float forestMaxAltitude = 50f;
        [Min(0f)] public float snowMinAltitude = 70f;

        [Header("Slope thresholds [deg]")]
        [Range(0f, 90f)] public float rockMinSlopeDeg = 35f;

        [Header("Blend (重みブレンド)")]
        [Tooltip("高度しきい値まわりのブレンド幅 [m]。大きいほど境界がなめらか。")]
        [Min(0.01f)] public float altitudeBlend = 8f;
        [Tooltip("斜度しきい値まわりのブレンド幅 [deg]。")]
        [Range(0.01f, 20f)] public float slopeBlend = 6f;

        [Header("Debug colors (index 順: Water/Sand/Grass/Forest/Rock/Snow)")]
        public Color[] debugColors =
        {
            new Color(0.15f, 0.35f, 0.75f), // Water
            new Color(0.85f, 0.80f, 0.55f), // Sand
            new Color(0.30f, 0.65f, 0.25f), // Grass
            new Color(0.12f, 0.40f, 0.18f), // Forest
            new Color(0.45f, 0.42f, 0.40f), // Rock
            new Color(0.95f, 0.96f, 1.00f), // Snow
        };
    }

    /// <summary>BiomeMaskTex に格納される index の意味。HLSL 側と一致させる。</summary>
    public enum BiomeId
    {
        Water = 0,
        Sand = 1,
        Grass = 2,
        Forest = 3,
        Rock = 4,
        Snow = 5,
    }
}
