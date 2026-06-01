using UnityEngine;

namespace Sandbox.World
{
    public sealed class DistanceBasedLifecyclePolicy : IChunkLifecyclePolicy
    {
        private readonly float _chunkWorldSize;
        private readonly int _loadRadius;
        private readonly int _evictRadius;
        private readonly float _lod1Distance;
        private readonly float _lod2Distance;

        public DistanceBasedLifecyclePolicy(float chunkWorldSize, int loadRadius,
                                            int evictRadius, float lod1Distance, float lod2Distance)
        {
            _chunkWorldSize = chunkWorldSize;
            _loadRadius = loadRadius;
            _evictRadius = Mathf.Max(evictRadius, loadRadius + 1);
            _lod1Distance = lod1Distance;
            _lod2Distance = lod2Distance;
        }

        public bool ShouldLoad(in ChunkCoord coord, in Vector3 viewer)
            => ChebyshevDistance(coord, viewer) <= _loadRadius;

        public bool ShouldEvict(in ChunkCoord coord, in Vector3 viewer)
            => ChebyshevDistance(coord, viewer) > _evictRadius;

        public int TargetLOD(in ChunkCoord coord, in Vector3 viewer)
        {
            float d = EuclideanDistance(coord, viewer);
            if (d > _lod2Distance) return 2;
            if (d > _lod1Distance) return 1;
            return 0;
        }

        private int ChebyshevDistance(in ChunkCoord coord, in Vector3 viewer)
        {
            int vcx = Mathf.FloorToInt(viewer.x / _chunkWorldSize);
            int vcz = Mathf.FloorToInt(viewer.z / _chunkWorldSize);
            return Mathf.Max(Mathf.Abs(coord.x - vcx), Mathf.Abs(coord.z - vcz));
        }

        private float EuclideanDistance(in ChunkCoord coord, in Vector3 viewer)
        {
            float cx = (coord.x + 0.5f) * _chunkWorldSize;
            float cz = (coord.z + 0.5f) * _chunkWorldSize;
            float dx = cx - viewer.x;
            float dz = cz - viewer.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
