using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Sandbox.World.Config;

namespace Sandbox.World.Generation.Erosion
{
    /// <summary>
    /// GPU 水理 + 熱的浸食パス (Step 3 設計 A).
    /// Pack → Droplet × N batches → Thermal × M iter → Unpack → 法線再計算。
    /// v2: CreateJob でフレーム予算による time-slicing 実行に対応。
    /// </summary>
    public sealed class HydraulicThermalErosionGPU : IErosionPass
    {
        private static readonly int IdHeightTex      = Shader.PropertyToID("_HeightTex");
        private static readonly int IdBaseHeightTex  = Shader.PropertyToID("_BaseHeightTex");
        private static readonly int IdHeightFixed    = Shader.PropertyToID("_HeightFixed");
        private static readonly int IdNormalSlopeTex = Shader.PropertyToID("_NormalSlopeTex");

        private static readonly int IdMisc           = Shader.PropertyToID("_Misc");
        private static readonly int IdDropletParams  = Shader.PropertyToID("_DropletParams");
        private static readonly int IdErodeParams    = Shader.PropertyToID("_ErodeParams");
        private static readonly int IdErodeMisc      = Shader.PropertyToID("_ErodeMisc");
        private static readonly int IdThermalParams  = Shader.PropertyToID("_ThermalParams");
        private static readonly int IdBatchInfo      = Shader.PropertyToID("_BatchInfo");

        private readonly ComputeShader _shader;
        private readonly ErosionParams _params;
        private readonly int _kPack;
        private readonly int _kDroplet;
        private readonly int _kThermal;
        private readonly int _kUnpack;
        private readonly int _kBlend;
        private readonly int _kNormal;

        public ErosionCaps Caps => new ErosionCaps(true, true, 33);

        public HydraulicThermalErosionGPU(ComputeShader shader, ErosionParams parameters)
        {
            if (shader == null) throw new ArgumentNullException(nameof(shader));
            if (parameters == null) throw new ArgumentNullException(nameof(parameters));

            _shader   = shader;
            _params   = parameters;
            _kPack    = shader.FindKernel("Height_PackToFixed");
            _kDroplet = shader.FindKernel("Droplet_Simulate");
            _kThermal = shader.FindKernel("Thermal_Relax");
            _kUnpack  = shader.FindKernel("Height_UnpackFromFixed");
            _kBlend   = shader.FindKernel("Height_BlendBoundaryToBase");
            _kNormal  = shader.FindKernel("RecomputeNormalSlope");
        }

        /// <summary>全工程を 1 コマンドバッファに一括記録（time-slicing なし）。</summary>
        public void Schedule(CommandBuffer cmd, in ChunkContext ctx, ChunkBufferSet target)
        {
            var job = new Job(this, ctx, target);
            cmd.BeginSample("Erosion.Hydraulic");
            job.Step(cmd, int.MaxValue);
            cmd.EndSample("Erosion.Hydraulic");
        }

        public IErosionJob CreateJob(in ChunkContext ctx, ChunkBufferSet target)
            => new Job(this, ctx, target);

        // ── 分割実行ジョブ ────────────────────────────────────────────
        private enum Kind { SnapshotBase, Pack, Droplet, Thermal, Unpack, Blend, Normal }

        private readonly struct Op
        {
            public readonly Kind kind;
            public readonly int gx, gy;
            public readonly int bi0, bi1, bi2, bi3; // _BatchInfo x,y,z,w

            public Op(Kind kind, int gx, int gy, int bi0 = 0, int bi1 = 0, int bi2 = 0, int bi3 = 0)
            {
                this.kind = kind; this.gx = gx; this.gy = gy;
                this.bi0 = bi0; this.bi1 = bi1; this.bi2 = bi2; this.bi3 = bi3;
            }
        }

        private sealed class Job : IErosionJob
        {
            private readonly HydraulicThermalErosionGPU _o;
            private readonly ChunkContext _ctx;
            private readonly ChunkBufferSet _target;
            private readonly List<Op> _ops = new List<Op>();
            private int _cursor;

            public bool IsComplete => _cursor >= _ops.Count;

            public Job(HydraulicThermalErosionGPU owner, in ChunkContext ctx, ChunkBufferSet target)
            {
                _o = owner; _ctx = ctx; _target = target;
                BuildPlan();
            }

            private void BuildPlan()
            {
                var p = _o._params;
                int fullRes = _ctx.FullResolution;
                int gxFull  = Mathf.CeilToInt(fullRes / 8f);

                // 侵食で書き換わる前のベース高さ(=隣接チャンクと一致する純粋関数値)を保存。
                // 末尾の Blend で境界バンドをこのベースへ戻し、継ぎ目/穴を解消する。
                _ops.Add(new Op(Kind.SnapshotBase, gxFull, gxFull));

                _ops.Add(new Op(Kind.Pack, gxFull, gxFull));

                int dropletsTotal = Mathf.Max(0, p.dropletsPerChunk);
                int batchSize = Mathf.Clamp(p.dropletsPerBatch, 256, 16000);
                uint baseSeed = _ctx.Seed;
                int processed = 0, batchIdx = 0;
                while (processed < dropletsTotal)
                {
                    int batchCount = Mathf.Min(batchSize, dropletsTotal - processed);
                    int seed = unchecked((int)(baseSeed + (uint)batchIdx * 2654435761u));
                    _ops.Add(new Op(Kind.Droplet, Mathf.CeilToInt(batchCount / 64f), 1,
                                    seed, batchIdx, p.maxLifetime, batchCount));
                    processed += batchCount;
                    batchIdx++;
                }

                int relaxIter = Mathf.Max(0, p.finalRelaxIterations);
                for (int i = 0; i < relaxIter; i++)
                {
                    int seed2 = unchecked((int)(baseSeed + 0xA5A5A5A5u + (uint)i));
                    _ops.Add(new Op(Kind.Thermal, gxFull, gxFull, seed2, batchIdx + i));
                }

                _ops.Add(new Op(Kind.Unpack, gxFull, gxFull));
                if (p.boundaryBlendWidth > 0)
                    _ops.Add(new Op(Kind.Blend, gxFull, gxFull));
                _ops.Add(new Op(Kind.Normal, gxFull, gxFull));
            }

            public int Step(CommandBuffer cmd, int budget)
            {
                if (IsComplete || budget <= 0) return 0;

                BindFrame(cmd); // cmd は毎フレームクリアされるため globals/バインドを再記録

                int used = 0;
                while (_cursor < _ops.Count && used < budget)
                {
                    Dispatch(cmd, _ops[_cursor]);
                    _cursor++;
                    used++;
                }
                return used;
            }

            private void BindFrame(CommandBuffer cmd)
            {
                var s = _o._shader;
                var p = _o._params;

                cmd.SetComputeVectorParam(s, IdMisc,
                    new Vector4(_ctx.CellSize, _ctx.FullResolution, _ctx.Apron, p.boundaryBlendWidth));
                cmd.SetComputeVectorParam(s, IdDropletParams,
                    new Vector4(p.inertia, p.minSlope, p.capacityFactor, p.gravity));
                cmd.SetComputeVectorParam(s, IdErodeParams,
                    new Vector4(p.erodeSpeed, p.depositSpeed, p.evapRate, p.brushRadius));
                cmd.SetComputeVectorParam(s, IdErodeMisc,
                    new Vector4(p.ComputeBrushNormFactor(),
                                Mathf.Max(1f, p.heightNormalization),
                                Mathf.Max(1f, p.maxSpeed), 0f));
                cmd.SetComputeVectorParam(s, IdThermalParams,
                    new Vector4(p.talusBaseDeg, p.talusJitterDeg, p.relaxFactor, 0f));

                cmd.SetComputeTextureParam(s, _o._kPack,   IdHeightTex,      _target.HeightTex);
                cmd.SetComputeBufferParam (s, _o._kPack,   IdHeightFixed,    _target.HeightFixed);
                cmd.SetComputeBufferParam (s, _o._kDroplet, IdHeightFixed,   _target.HeightFixed);
                cmd.SetComputeBufferParam (s, _o._kThermal, IdHeightFixed,   _target.HeightFixed);
                cmd.SetComputeTextureParam(s, _o._kUnpack, IdHeightTex,      _target.HeightTex);
                cmd.SetComputeBufferParam (s, _o._kUnpack, IdHeightFixed,    _target.HeightFixed);
                cmd.SetComputeTextureParam(s, _o._kBlend,  IdHeightTex,      _target.HeightTex);
                cmd.SetComputeTextureParam(s, _o._kBlend,  IdBaseHeightTex,  _target.BaseHeightTex);
                cmd.SetComputeTextureParam(s, _o._kNormal, IdHeightTex,      _target.HeightTex);
                cmd.SetComputeTextureParam(s, _o._kNormal, IdNormalSlopeTex, _target.NormalSlopeTex);
            }

            private void Dispatch(CommandBuffer cmd, in Op op)
            {
                var s = _o._shader;
                switch (op.kind)
                {
                    case Kind.SnapshotBase:
                        // 侵食前のベース高さを退避（GPU 間コピー、同フォーマット/サイズ）。
                        cmd.CopyTexture(_target.HeightTex, _target.BaseHeightTex);
                        break;
                    case Kind.Pack:
                        cmd.DispatchCompute(s, _o._kPack, op.gx, op.gy, 1);
                        break;
                    case Kind.Droplet:
                        cmd.SetComputeIntParams(s, IdBatchInfo, op.bi0, op.bi1, op.bi2, op.bi3);
                        cmd.DispatchCompute(s, _o._kDroplet, op.gx, op.gy, 1);
                        break;
                    case Kind.Thermal:
                        cmd.SetComputeIntParams(s, IdBatchInfo, op.bi0, op.bi1, op.bi2, op.bi3);
                        cmd.DispatchCompute(s, _o._kThermal, op.gx, op.gy, 1);
                        break;
                    case Kind.Unpack:
                        cmd.DispatchCompute(s, _o._kUnpack, op.gx, op.gy, 1);
                        break;
                    case Kind.Blend:
                        cmd.DispatchCompute(s, _o._kBlend, op.gx, op.gy, 1);
                        break;
                    case Kind.Normal:
                        cmd.DispatchCompute(s, _o._kNormal, op.gx, op.gy, 1);
                        break;
                }
            }
        }
    }
}
