using System;
using UnityEngine;
using Sandbox.World.Generation.Placement;

namespace Sandbox.World
{
    /// <summary>
    /// Per-chunk GPU resources (Step 1 contract).
    /// HeightTex / NormalSlopeTex / HeightFixed (Erosion atomic) /
    /// BiomeMaskTex (Module 3 分類) / PlacementBuffer (Module 3 scatter).
    /// </summary>
    public sealed class ChunkBufferSet : IDisposable
    {
        public RenderTexture HeightTex { get; private set; }
        public RenderTexture BaseHeightTex { get; private set; }     // RFloat — 侵食前のベース高さ(継ぎ目修復のブレンド基準)
        public RenderTexture NormalSlopeTex { get; private set; }
        public GraphicsBuffer HeightFixed { get; private set; }     // int[FullResolution^2] — Erosion 原子加算用
        public RenderTexture BiomeMaskTex { get; private set; }      // RFloat — 支配的バイオーム index
        public RenderTexture BiomeColorTex { get; private set; }     // ARGBHalf — 重みブレンド済みバイオーム色
        // prototype 別の Append バッファ（indirect instancing 用。1 mesh = 1 buffer）
        public GraphicsBuffer PlacementTreeBuffer { get; private set; } // Append<PlacementInstance>
        public GraphicsBuffer PlacementRockBuffer { get; private set; } // Append<PlacementInstance>

        public int FullResolution { get; }
        public int MaxInstances { get; }

        public ChunkBufferSet(int fullResolution, int maxInstances)
        {
            FullResolution = fullResolution;
            MaxInstances   = Mathf.Max(1, maxInstances);

            HeightTex      = CreateRT(fullResolution, RenderTextureFormat.RFloat,   "HeightTex");
            BaseHeightTex  = CreateRT(fullResolution, RenderTextureFormat.RFloat,   "BaseHeightTex");
            NormalSlopeTex = CreateRT(fullResolution, RenderTextureFormat.ARGBHalf, "NormalSlopeTex");
            BiomeMaskTex   = CreateRT(fullResolution, RenderTextureFormat.RFloat,   "BiomeMaskTex");
            BiomeColorTex  = CreateRT(fullResolution, RenderTextureFormat.ARGBHalf, "BiomeColorTex");

            HeightFixed = new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                fullResolution * fullResolution,
                sizeof(int))
            {
                name = "HeightFixed"
            };

            PlacementTreeBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Append, MaxInstances, PlacementInstance.Stride)
            {
                name = "PlacementTreeBuffer"
            };
            PlacementRockBuffer = new GraphicsBuffer(
                GraphicsBuffer.Target.Append, MaxInstances, PlacementInstance.Stride)
            {
                name = "PlacementRockBuffer"
            };
            PlacementTreeBuffer.SetCounterValue(0);
            PlacementRockBuffer.SetCounterValue(0);
        }

        private static RenderTexture CreateRT(int size, RenderTextureFormat format, string name)
        {
            var rt = new RenderTexture(size, size, 0, format)
            {
                enableRandomWrite = true,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = name
            };
            rt.Create();
            return rt;
        }

        public void Dispose()
        {
            ReleaseRT(HeightTex);      HeightTex = null;
            ReleaseRT(BaseHeightTex);  BaseHeightTex = null;
            ReleaseRT(NormalSlopeTex); NormalSlopeTex = null;
            ReleaseRT(BiomeMaskTex);   BiomeMaskTex = null;
            ReleaseRT(BiomeColorTex);  BiomeColorTex = null;

            if (HeightFixed != null)         { HeightFixed.Dispose();         HeightFixed = null; }
            if (PlacementTreeBuffer != null) { PlacementTreeBuffer.Dispose(); PlacementTreeBuffer = null; }
            if (PlacementRockBuffer != null) { PlacementRockBuffer.Dispose(); PlacementRockBuffer = null; }
        }

        private static void ReleaseRT(RenderTexture rt)
        {
            if (rt == null) return;
            rt.Release();
            // エディタ（非 Play）では Destroy が使えないため DestroyImmediate にフォールバック。
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(rt);
            else
                UnityEngine.Object.DestroyImmediate(rt);
        }
    }
}
