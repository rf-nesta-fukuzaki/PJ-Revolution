using UnityEngine;

/// <summary>
/// 3D Perlin Noise のパラメータ設定。
/// CaveNoiseGenerator.Sample() と CaveWorldGenerator の Inspector で共有する。
/// </summary>
[System.Serializable]
public struct NoiseSettings
{
    [Tooltip("Perlin noise frequency scale. Smaller = larger caves, Larger = finer terrain. Recommended: 0.03-0.06")]
    [Range(0.005f, 0.2f)]
    public float scale;

    [Tooltip("Number of noise layers. More = more natural but heavier. Recommended: 3-5")]
    [Range(1, 6)]
    public int octaveCount;

    [Tooltip("Amplitude multiplier for each octave. Small = smooth, Large = complex shape. Recommended: 0.4-0.6")]
    [Range(0f, 1f)]
    public float persistence;

    [Tooltip("Isosurface threshold. Lower = more cavities, Higher = more rock. Recommended: 0.45-0.55")]
    [Range(0f, 1f)]
    public float isoLevel;

    [Tooltip("Y-axis bias. Higher = clearer floor/ceiling. Recommended: 0.2-0.4")]
    [Range(0f, 1f)]
    public float gravityBias;

    [Tooltip("Random seed. 0 = random each time. Fixed value = reproducible cave (for NGO sync)")]
    public int seed;

    /// <summary>Default values (recommended initial values)</summary>
    public static NoiseSettings Default => new NoiseSettings
    {
        scale = 0.04f,
        octaveCount = 4,
        persistence = 0.5f,
        isoLevel = 0.5f,
        gravityBias = 0.3f,
        seed = 0,
    };
}
