using System;
using UnityEngine;
using UnityEngine.Rendering;
using Sandbox.World.Config;

namespace Sandbox.World.Generation.Base
{
    /// <summary>
    /// GPU 初期地形ビルダー (Step 2 実装).
    /// Compute Shader で Ridged Multifractal + 山岳マスク + ドメインワープ + プロファイル LUT を 1 パスで生成。
    /// </summary>
    public sealed class RidgedMultifractalBuilder : IHeightfieldBuilder
    {
        private static readonly int IdHeightTex        = Shader.PropertyToID("_HeightTex");
        private static readonly int IdNormalSlopeTex   = Shader.PropertyToID("_NormalSlopeTex");
        private static readonly int IdHeightProfileLUT = Shader.PropertyToID("_HeightProfileLUT");

        private static readonly int IdWorldOrigin    = Shader.PropertyToID("_WorldOrigin");
        private static readonly int IdGenOrigin      = Shader.PropertyToID("_GenerationOriginXZ");
        private static readonly int IdMisc           = Shader.PropertyToID("_Misc");
        private static readonly int IdBaseParams     = Shader.PropertyToID("_BaseParams");
        private static readonly int IdRidgeParams    = Shader.PropertyToID("_RidgeParams");
        private static readonly int IdMaskParams     = Shader.PropertyToID("_MaskParams");
        private static readonly int IdWarpParams     = Shader.PropertyToID("_WarpParams");
        private static readonly int IdProfileParams  = Shader.PropertyToID("_ProfileParams");
        private static readonly int IdMicroParams    = Shader.PropertyToID("_MicroParams");
        private static readonly int IdMountainParams = Shader.PropertyToID("_MountainParams");
        private static readonly int IdOceanParams    = Shader.PropertyToID("_OceanParams");

        private readonly ComputeShader _shader;
        private readonly RidgedMFParams _params;
        private readonly int _kernelBuild;
        private readonly int _kernelNormal;

        public BuilderCaps Caps => new BuilderCaps(true, true, 33);

        public RidgedMultifractalBuilder(ComputeShader shader, RidgedMFParams parameters)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            _shader = shader;
            _params = parameters;
            _kernelBuild  = shader.FindKernel("RidgedMF_Build");
            _kernelNormal = shader.FindKernel("RidgedMF_FillNormalSlope");
        }

        public void Schedule(CommandBuffer cmd, in ChunkContext ctx, ChunkBufferSet target)
        {
            int fullRes = ctx.FullResolution;
            // 高さは LOD 非依存（全チャンク同一オクターブ）で生成する。LOD ごとに octave を
            // 減らすと、隣接チャンクの LOD が違うとき共有辺で高さ値がズレて継ぎ目になる。
            // 常に LOD0 相当のオクターブを使えば、どの LOD 組み合わせでも辺の値が一致する。
            int oct     = _params.EffectiveOctaves(0);

            // 生成原点 (ノイズ評価のための座標基準) は **全チャンク共通の固定値** にする。
            // 以前は ctx.WorldOrigin をチャンク毎に grid スナップしていたため、grid 境界
            // (1024m) を跨ぐ隣接チャンクで genOrigin が grid 分ジャンプし、同一ワールド
            // 座標でもノイズ評価点がズレて継ぎ目になっていた。
            // 山岳中心を一度だけスナップした固定アンカーを使えば、全チャンクで src が連続し
            // 継ぎ目が消える。世界規模 (〜数km) では float 精度も十分。
            float grid = Mathf.Max(1f, _params.generationGridSize);
            float ox   = Mathf.Floor(_params.mountainCenter.x / grid) * grid;
            float oz   = Mathf.Floor(_params.mountainCenter.y / grid) * grid;

            var lut = _params.GetOrBuildHeightProfileLut();

            cmd.BeginSample("RidgedMF.Build");

            // Globals (per-shader, shared across kernels)
            cmd.SetComputeVectorParam(_shader, IdWorldOrigin,
                new Vector4(ctx.WorldOrigin.x, ctx.WorldOrigin.y, ctx.WorldOrigin.z, 0f));
            cmd.SetComputeVectorParam(_shader, IdGenOrigin, new Vector4(ox, oz, 0f, 0f));
            cmd.SetComputeVectorParam(_shader, IdMisc,
                new Vector4(ctx.CellSize, ctx.Resolution, ctx.Apron, ctx.Lod));

            cmd.SetComputeVectorParam(_shader, IdBaseParams,
                new Vector4(_params.baseFrequency, _params.lacunarity, _params.H, oct));
            cmd.SetComputeVectorParam(_shader, IdRidgeParams,
                new Vector4(_params.ridgeOffset, _params.gain,
                            _params.singleMountainMode ? 1f : 0f, 0f));
            cmd.SetComputeVectorParam(_shader, IdMaskParams,
                new Vector4(_params.mountainMaskFreq,
                            _params.mountainMaskThreshold.x,
                            _params.mountainMaskThreshold.y,
                            _params.mountainEdgeNoise));
            cmd.SetComputeVectorParam(_shader, IdMountainParams,
                new Vector4(_params.mountainCenter.x, _params.mountainCenter.y,
                            Mathf.Max(1f, _params.mountainRadius), _params.mountainFalloff));

            // 島/海底: x=有効(0/1), y=海底深さ[m], z=海岸からの沈降距離[m], w=海岸レリーフ減衰(0..1)
            cmd.SetComputeVectorParam(_shader, IdOceanParams,
                new Vector4(_params.islandMode ? 1f : 0f,
                            Mathf.Max(0f, _params.seabedDepth),
                            Mathf.Max(1f, _params.seabedFalloffDistance),
                            Mathf.Clamp01(_params.shoreFlatten)));

            // Domain warp amplitude: apron 0.3 制約 (Step 2)
            float warpAmpMax = ctx.Apron * ctx.CellSize * 0.3f;
            float warpAmp    = Mathf.Min(_params.domainWarpAmp, warpAmpMax);
            cmd.SetComputeVectorParam(_shader, IdWarpParams,
                new Vector4(_params.domainWarpFreq, warpAmp, 0f, 0f));

            cmd.SetComputeVectorParam(_shader, IdProfileParams,
                new Vector4(_params.peakAltitude, _params.seaLevel,
                            _params.mountainReliefBase, 0f));
            cmd.SetComputeVectorParam(_shader, IdMicroParams,
                new Vector4(_params.microFreq, _params.microAmp, 0f, 0f));

            // ── Kernel 1: Build height
            cmd.SetComputeTextureParam(_shader, _kernelBuild, IdHeightTex,        target.HeightTex);
            cmd.SetComputeTextureParam(_shader, _kernelBuild, IdHeightProfileLUT, lut);

            int gx = Mathf.CeilToInt(fullRes / 8f);
            cmd.DispatchCompute(_shader, _kernelBuild, gx, gx, 1);

            // ── Kernel 2: Fill normal / slope
            cmd.SetComputeTextureParam(_shader, _kernelNormal, IdHeightTex,      target.HeightTex);
            cmd.SetComputeTextureParam(_shader, _kernelNormal, IdNormalSlopeTex, target.NormalSlopeTex);
            cmd.DispatchCompute(_shader, _kernelNormal, gx, gx, 1);

            cmd.EndSample("RidgedMF.Build");
        }
    }
}
