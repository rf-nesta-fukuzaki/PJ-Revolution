using UnityEngine.Rendering;

namespace Sandbox.World
{
    public readonly struct ErosionCaps
    {
        public readonly bool RunsOnGpu;
        public readonly bool SupportsTimeSlicing;
        public readonly int MinResolution;

        public ErosionCaps(bool runsOnGpu, bool supportsTimeSlicing, int minResolution)
        {
            RunsOnGpu = runsOnGpu;
            SupportsTimeSlicing = supportsTimeSlicing;
            MinResolution = minResolution;
        }
    }

    /// <summary>
    /// Step 3 設計の浸食パス共通インターフェース。
    /// A) Hydraulic + Thermal GPU / B) Analytical Burst を同シグネチャで差し替え可能。
    /// Schedule は全 iteration を 1 回で実行（v1）。
    /// CreateJob は time-slicing 対応（v2）— フレーム予算で分割実行する。
    /// </summary>
    public interface IErosionPass
    {
        ErosionCaps Caps { get; }

        /// <summary>全工程を 1 コマンドバッファに一括記録する（time-slicing なし）。</summary>
        void Schedule(CommandBuffer cmd, in ChunkContext ctx, ChunkBufferSet target);

        /// <summary>
        /// time-slicing 用ジョブを生成。Caps.SupportsTimeSlicing=true のパスのみ意味を持つ。
        /// </summary>
        IErosionJob CreateJob(in ChunkContext ctx, ChunkBufferSet target);
    }

    /// <summary>
    /// 浸食工程をフレーム分割実行するためのジョブ。1 チャンクにつき 1 つ生成。
    /// </summary>
    public interface IErosionJob
    {
        /// <summary>全工程が完了したか。</summary>
        bool IsComplete { get; }

        /// <summary>
        /// 残工程を最大 <paramref name="budget"/> dispatch だけ cmd に記録する。
        /// 戻り値は実際に消費した dispatch 数。cmd は毎フレームクリアされる前提。
        /// </summary>
        int Step(CommandBuffer cmd, int budget);
    }
}
