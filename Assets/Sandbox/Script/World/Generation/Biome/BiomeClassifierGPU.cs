using System;
using UnityEngine;
using UnityEngine.Rendering;
using Sandbox.World.Config;

namespace Sandbox.World.Generation.Biome
{
    /// <summary>
    /// GPU バイオーム分類パス (Module 3)。HeightTex + NormalSlopeTex(.w=slope) を読み、
    /// 支配的バイオーム index を BiomeMaskTex(RFloat) に書く。単一 dispatch。
    /// </summary>
    public sealed class BiomeClassifierGPU : IBiomePass
    {
        private static readonly int IdHeightTex      = Shader.PropertyToID("_HeightTex");
        private static readonly int IdNormalSlopeTex = Shader.PropertyToID("_NormalSlopeTex");
        private static readonly int IdBiomeMaskTex   = Shader.PropertyToID("_BiomeMaskTex");
        private static readonly int IdBiomeColorTex  = Shader.PropertyToID("_BiomeColorTex");

        private static readonly int IdBMisc      = Shader.PropertyToID("_BMisc");
        private static readonly int IdAltitudes  = Shader.PropertyToID("_Altitudes");
        private static readonly int IdAltitudes2 = Shader.PropertyToID("_Altitudes2");
        private static readonly int IdSlopes     = Shader.PropertyToID("_Slopes");
        private static readonly int IdBlend      = Shader.PropertyToID("_Blend");
        private static readonly int[] IdCol =
        {
            Shader.PropertyToID("_Col0"), Shader.PropertyToID("_Col1"),
            Shader.PropertyToID("_Col2"), Shader.PropertyToID("_Col3"),
            Shader.PropertyToID("_Col4"), Shader.PropertyToID("_Col5"),
        };

        private readonly ComputeShader _shader;
        private readonly BiomeParams _params;
        private readonly int _kernel;

        public BiomeCaps Caps => new BiomeCaps(true, 33);

        public BiomeClassifierGPU(ComputeShader shader, BiomeParams parameters)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            _shader = shader;
            _params = parameters;
            _kernel = shader.FindKernel("Biome_Classify");
        }

        public void Schedule(CommandBuffer cmd, in ChunkContext ctx, ChunkBufferSet target)
        {
            int fullRes = ctx.FullResolution;
            int gxFull  = Mathf.CeilToInt(fullRes / 8f);

            cmd.BeginSample("Biome.Classify");

            cmd.SetComputeVectorParam(_shader, IdBMisc, new Vector4(fullRes, 0f, 0f, 0f));
            cmd.SetComputeVectorParam(_shader, IdAltitudes,
                new Vector4(_params.seaLevel, _params.beachMaxAltitude,
                            _params.grassMaxAltitude, _params.forestMaxAltitude));
            cmd.SetComputeVectorParam(_shader, IdAltitudes2,
                new Vector4(_params.snowMinAltitude, 0f, 0f, 0f));
            cmd.SetComputeVectorParam(_shader, IdSlopes,
                new Vector4(_params.rockMinSlopeDeg, 0f, 0f, 0f));
            cmd.SetComputeVectorParam(_shader, IdBlend,
                new Vector4(_params.altitudeBlend, _params.slopeBlend, 0f, 0f));

            var colors = _params.debugColors;
            for (int i = 0; i < IdCol.Length; i++)
            {
                Color c = (colors != null && i < colors.Length) ? colors[i] : Color.magenta;
                cmd.SetComputeVectorParam(_shader, IdCol[i], c);
            }

            cmd.SetComputeTextureParam(_shader, _kernel, IdHeightTex,      target.HeightTex);
            cmd.SetComputeTextureParam(_shader, _kernel, IdNormalSlopeTex, target.NormalSlopeTex);
            cmd.SetComputeTextureParam(_shader, _kernel, IdBiomeMaskTex,   target.BiomeMaskTex);
            cmd.SetComputeTextureParam(_shader, _kernel, IdBiomeColorTex,  target.BiomeColorTex);

            cmd.DispatchCompute(_shader, _kernel, gxFull, gxFull, 1);

            cmd.EndSample("Biome.Classify");
        }
    }
}
