using UnityEngine;

namespace Sandbox.World
{
    public interface IChunkLifecyclePolicy
    {
        bool ShouldLoad(in ChunkCoord coord, in Vector3 viewer);
        bool ShouldEvict(in ChunkCoord coord, in Vector3 viewer);
        int TargetLOD(in ChunkCoord coord, in Vector3 viewer);
    }
}
