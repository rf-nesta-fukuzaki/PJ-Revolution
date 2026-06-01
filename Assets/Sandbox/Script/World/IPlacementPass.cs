using UnityEngine.Rendering;

namespace Sandbox.World
{
    public readonly struct PlacementCaps
    {
        public readonly bool RunsOnGpu;
        public readonly int MinResolution;

        public PlacementCaps(bool runsOnGpu, int minResolution)
        {
            RunsOnGpu = runsOnGpu;
            MinResolution = minResolution;
        }
    }

    /// <summary>
    /// オブジェクト配置パス (Module 3)。BiomeMaskTex/HeightTex/NormalSlopeTex を読み、
    /// 採用インスタンスを ChunkBufferSet.PlacementBuffer (Append) に書く。単一 dispatch。
    /// </summary>
    public interface IPlacementPass
    {
        PlacementCaps Caps { get; }
        void Schedule(CommandBuffer cmd, in ChunkContext ctx, ChunkBufferSet target);
    }
}
