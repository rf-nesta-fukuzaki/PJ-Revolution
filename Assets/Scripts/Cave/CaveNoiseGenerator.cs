using UnityEngine;

/// <summary>
/// 3D Perlin Noise をオクターブ合成してスカラー値を返す static クラス。
/// Unity の Mathf.PerlinNoise (2D) を XY/YZ/XZ の3面合成で3D化する。
/// Y方向に重力バイアスを加算して天井・床が形成されやすくする。
/// </summary>
public static class CaveNoiseGenerator
{
    /// <summary>
    /// Return scalar value at specified world coordinates.
    /// If value < settings.isoLevel, it's treated as "cavity". Otherwise "rock".
    /// </summary>
    /// <param name="x">World X coordinate</param>
    /// <param name="y">World Y coordinate</param>
    /// <param name="z">World Z coordinate</param>
    /// <param name="settings">Noise parameters</param>
    /// <param name="seed">Seed value (used as offset)</param>
    /// <param name="worldHeight">World height (for gravity bias normalization)</param>
    public static float Sample(float x, float y, float z,
                               NoiseSettings settings, int seed,
                               float worldHeight = 64f)
    {
        float scale       = settings.scale;
        int   octaves     = settings.octaveCount;
        float persistence = settings.persistence;

        // Use seed as offset (Mathf.PerlinNoise doesn't support seed parameter)
        float seedOffset = seed * 0.1f;

        float amplitude = 1f;
        float frequency = 1f;
        float noiseSum  = 0f;
        float maxValue  = 0f;

        for (int i = 0; i < octaves; i++)
        {
            float sx = x * scale * frequency + seedOffset;
            float sy = y * scale * frequency + seedOffset + 31.41f; // Y offset
            float sz = z * scale * frequency + seedOffset + 62.83f; // Z offset

            // Approximate 3D Perlin Noise by compositing 3 planes: XY / YZ / XZ
            float xy = Mathf.PerlinNoise(sx, sy);
            float yz = Mathf.PerlinNoise(sy, sz);
            float xz = Mathf.PerlinNoise(sx, sz);

            noiseSum += (xy + yz + xz) / 3f * amplitude;
            maxValue += amplitude;

            amplitude *= persistence;
            frequency *= 2f;
        }

        // Normalize to 0-1
        float normalized = noiseSum / maxValue;

        // Gravity bias: Higher Y = higher value (forms ceiling)
        // Lower Y = lower value (forms floor)
        float gravityBias = settings.gravityBias;
        float yRatio = Mathf.Clamp01(y / worldHeight);
        // At center (yRatio=0.5): 0, At top (1.0): +bias, At bottom (0.0): -bias
        float biasValue = (yRatio - 0.5f) * 2f * gravityBias;

        return Mathf.Clamp01(normalized + biasValue);
    }
}
