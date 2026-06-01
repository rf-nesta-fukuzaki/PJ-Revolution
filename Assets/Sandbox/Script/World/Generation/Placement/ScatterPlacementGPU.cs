using System;
using UnityEngine;
using UnityEngine.Rendering;
using Sandbox.World.Config;

namespace Sandbox.World.Generation.Placement
{
    /// <summary>
    /// GPU scatter 配置パス (Module 3)。候補格子を走査し、バイオーム/斜度に応じて
    /// Tree/Rock インスタンスを PlacementBuffer (Append) に追加する。単一 dispatch。
    /// </summary>
    public sealed class ScatterPlacementGPU : IPlacementPass
    {
        private static readonly int IdHeightTex      = Shader.PropertyToID("_HeightTex");
        private static readonly int IdNormalSlopeTex = Shader.PropertyToID("_NormalSlopeTex");
        private static readonly int IdBiomeMaskTex   = Shader.PropertyToID("_BiomeMaskTex");
        private static readonly int IdTreeInstances  = Shader.PropertyToID("_TreeInstances");
        private static readonly int IdRockInstances  = Shader.PropertyToID("_RockInstances");

        private static readonly int IdPMisc        = Shader.PropertyToID("_PMisc");
        private static readonly int IdWorldOrigin  = Shader.PropertyToID("_WorldOriginXZ");
        private static readonly int IdTreeParams   = Shader.PropertyToID("_TreeParams");
        private static readonly int IdRockParams   = Shader.PropertyToID("_RockParams");
        private static readonly int IdPSeed        = Shader.PropertyToID("_PSeed");

        private readonly ComputeShader _shader;
        private readonly PlacementParams _params;
        private readonly int _kernel;

        public PlacementCaps Caps => new PlacementCaps(true, 33);

        public ScatterPlacementGPU(ComputeShader shader, PlacementParams parameters)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            _shader = shader;
            _params = parameters;
            _kernel = shader.FindKernel("Scatter");
        }

        public void Schedule(CommandBuffer cmd, in ChunkContext ctx, ChunkBufferSet target)
        {
            int fullRes = ctx.FullResolution;

            // Append オーバーフロー防止: candDim^2 <= MaxInstances に収める
            int maxDim = Mathf.FloorToInt(Mathf.Sqrt(target.MaxInstances));
            int candDim = Mathf.Clamp(_params.candidateGridDim, 1, Mathf.Max(1, maxDim));
            int gx = Mathf.CeilToInt(candDim / 8f);

            cmd.BeginSample("Placement.Scatter");

            cmd.SetBufferCounterValue(target.PlacementTreeBuffer, 0);
            cmd.SetBufferCounterValue(target.PlacementRockBuffer, 0);

            cmd.SetComputeVectorParam(_shader, IdPMisc,
                new Vector4(fullRes, ctx.Apron, ctx.CellSize, candDim));
            cmd.SetComputeVectorParam(_shader, IdWorldOrigin,
                new Vector4(ctx.WorldOrigin.x, ctx.WorldOrigin.z, 0f, 0f));
            cmd.SetComputeVectorParam(_shader, IdTreeParams,
                new Vector4(_params.treeDensity, _params.treeMaxSlopeDeg,
                            _params.treeScaleMin, _params.treeScaleMax));
            cmd.SetComputeVectorParam(_shader, IdRockParams,
                new Vector4(_params.rockDensity, _params.rockScaleMin, _params.rockScaleMax, 0f));
            cmd.SetComputeIntParams(_shader, IdPSeed, unchecked((int)ctx.Seed), 0, 0, 0);

            cmd.SetComputeTextureParam(_shader, _kernel, IdHeightTex,      target.HeightTex);
            cmd.SetComputeTextureParam(_shader, _kernel, IdNormalSlopeTex, target.NormalSlopeTex);
            cmd.SetComputeTextureParam(_shader, _kernel, IdBiomeMaskTex,   target.BiomeMaskTex);
            cmd.SetComputeBufferParam(_shader, _kernel, IdTreeInstances,   target.PlacementTreeBuffer);
            cmd.SetComputeBufferParam(_shader, _kernel, IdRockInstances,   target.PlacementRockBuffer);

            cmd.DispatchCompute(_shader, _kernel, gx, gx, 1);

            cmd.EndSample("Placement.Scatter");
        }
    }
}
