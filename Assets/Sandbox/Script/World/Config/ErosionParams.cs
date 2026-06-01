using UnityEngine;

namespace Sandbox.World.Config
{
    /// <summary>
    /// Step 3 設計に基づく浸食パラメータ。Hydraulic + Thermal 共通保持。
    /// </summary>
    [CreateAssetMenu(menuName = "PJ-Revolution/World/Erosion Params", fileName = "ErosionParams")]
    public sealed class ErosionParams : ScriptableObject
    {
        [Header("Hydraulic Droplet")]
        [Min(0)] public int dropletsPerChunk = 50000;
        [Range(256, 16000)] [Tooltip("1 dispatch(=time-slice 1 op) あたりの droplet 数。小さいほど時分割が細かくフレームスパイクが減る。")]
        public int dropletsPerBatch = 2000;
        [Range(8, 256)] public int maxLifetime = 30;
        [Range(0f, 1f)] public float inertia = 0.05f;
        [Min(0f)] public float minSlope = 0.01f;
        [Min(0f)] public float capacityFactor = 4.0f;
        [Min(0f)] public float gravity = 4.0f;
        [Range(0f, 0.1f)] public float evapRate = 0.012f;
        [Range(0f, 1f)] public float erodeSpeed = 0.3f;
        [Range(0f, 1f)] public float depositSpeed = 0.3f;
        [Range(1, 6)] public int brushRadius = 3;
        [Min(1f)] [Tooltip("速度上限。capacity 暴走を防ぐ。")]
        public float maxSpeed = 6f;

        [Header("Normalization")]
        [Min(1f)] [Tooltip("高さ正規化スケール [m]。隣接セル差を小さく保ち浸食を安定化。≒ peakAltitude 以上。")]
        public float heightNormalization = 256f;

        [Header("Thermal Relaxation")]
        [Range(10f, 60f)] public float talusBaseDeg = 33f;
        [Range(0f, 10f)] public float talusJitterDeg = 1.5f;
        [Range(0f, 1f)] public float relaxFactor = 0.5f;
        [Range(0, 20)] public int finalRelaxIterations = 4;

        [Header("Seam (cross-chunk continuity)")]
        [Range(0, 24)]
        [Tooltip("チャンク境界からこのテクセル数ぶん、侵食結果をシームレスなベース高さへ" +
                 "smoothstep ブレンド復帰させ、隣接チャンクとの段差(穴/継ぎ目)を消す。" +
                 "境界とアプロン帯はベース高さに固定。0 で無効(=継ぎ目が出る)。")]
        public int boundaryBlendWidth = 8;

        [Header("Safety (informational)")]
        [Min(0f)] public float heightSoftLimit = 1000f;

        public float ComputeBrushNormFactor()
        {
            int R = brushRadius;
            float fR = R;
            float sum = 0f;
            for (int by = -R; by <= R; by++)
            {
                for (int bx = -R; bx <= R; bx++)
                {
                    float r = Mathf.Sqrt(bx * bx + by * by);
                    if (r > fR) continue;
                    sum += 1f - r / fR;
                }
            }
            return sum > 1e-6f ? 1f / sum : 0f;
        }
    }
}
