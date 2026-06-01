using UnityEngine.Rendering;

namespace Sandbox.World
{
    public readonly struct BiomeCaps
    {
        public readonly bool RunsOnGpu;
        public readonly int MinResolution;

        public BiomeCaps(bool runsOnGpu, int minResolution)
        {
            RunsOnGpu = runsOnGpu;
            MinResolution = minResolution;
        }
    }

    /// <summary>
    /// バイオーム分類パス (Module 3)。HeightTex + NormalSlopeTex を読み、
    /// 支配的バイオーム index を BiomeMaskTex に書く。単一 dispatch（time-slicing 不要）。
    /// </summary>
    public interface IBiomePass
    {
        BiomeCaps Caps { get; }
        void Schedule(CommandBuffer cmd, in ChunkContext ctx, ChunkBufferSet target);
    }
}
