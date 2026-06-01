using UnityEngine.Rendering;

namespace Sandbox.World
{
    public readonly struct BuilderCaps
    {
        public readonly bool RequiresApron;
        public readonly bool RunsOnGpu;
        public readonly int MinResolution;

        public BuilderCaps(bool requiresApron, bool runsOnGpu, int minResolution)
        {
            RequiresApron = requiresApron;
            RunsOnGpu = runsOnGpu;
            MinResolution = minResolution;
        }
    }

    public interface IHeightfieldBuilder
    {
        BuilderCaps Caps { get; }
        void Schedule(CommandBuffer cmd, in ChunkContext ctx, ChunkBufferSet target);
    }
}
