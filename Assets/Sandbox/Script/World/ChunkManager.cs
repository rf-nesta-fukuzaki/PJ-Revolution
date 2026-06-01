using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sandbox.World
{
    /// <summary>
    /// チャンク管理（Step 1 設計 + Module 3 time-slicing）。
    /// Base (IHeightfieldBuilder) → Erosion (IErosionPass) を、フレーム予算
    /// (dispatch 数) 内で前進させる状態機械。重い浸食を複数フレームに分散する。
    /// Biome / Placement ステージは後続フェーズで挿入。
    /// </summary>
    public sealed class ChunkManager : IDisposable
    {
        private enum BuildStage { Base, Erosion, Biome, Placement, Done }

        private sealed class BuildJob
        {
            public ChunkHandle Handle;
            public BuildStage Stage;
            public IErosionJob ErosionJob;
        }

        private const int BaseDispatchCost = 2; // RidgedMF build + normal

        private readonly Dictionary<ChunkCoord, ChunkHandle> _active = new Dictionary<ChunkCoord, ChunkHandle>();
        private readonly List<BuildJob> _building = new List<BuildJob>();
        private readonly List<ChunkCoord> _evictScratch = new List<ChunkCoord>();

        private readonly IChunkLifecyclePolicy _policy;
        private readonly IHeightfieldBuilder _heightBuilder;
        private readonly IErosionPass _erosionPass;      // null 可
        private readonly IBiomePass _biomePass;          // null 可
        private readonly IPlacementPass _placementPass;  // null 可

        private readonly int _resolution;
        private readonly int _apron;
        private readonly float _cellSize;
        private readonly float _chunkWorldSize;
        private readonly uint _worldSeed;
        private readonly int _maxInstances;
        private readonly int _maxChunksInFlight;
        private readonly int _perFrameBudget;

        public ChunkManager(IHeightfieldBuilder heightBuilder,
                            IErosionPass erosionPass,
                            IBiomePass biomePass,
                            IPlacementPass placementPass,
                            IChunkLifecyclePolicy policy,
                            int resolution, int apron, float cellSize, uint worldSeed,
                            int maxInstances, int maxChunksInFlight, int perFrameBudget)
        {
            _heightBuilder     = heightBuilder ?? throw new ArgumentNullException(nameof(heightBuilder));
            _policy            = policy        ?? throw new ArgumentNullException(nameof(policy));
            _erosionPass       = erosionPass;   // 任意
            _biomePass         = biomePass;     // 任意
            _placementPass     = placementPass; // 任意
            _resolution        = resolution;
            _apron             = apron;
            _cellSize          = cellSize;
            // 共有エッジタイリング: 間隔は (resolution-1)*cellSize。
            // メッシュ/コライダーの span と一致させ、隣接チャンクが境界頂点を
            // 共有して隙間ゼロにする（off-by-one だとグリッド継ぎ目 + すり抜け）。
            _chunkWorldSize    = (resolution - 1) * cellSize;
            _worldSeed         = worldSeed;
            _maxInstances      = Mathf.Max(1, maxInstances);
            _maxChunksInFlight = Mathf.Max(1, maxChunksInFlight);
            _perFrameBudget    = Mathf.Max(1, perFrameBudget);
        }

        public float ChunkWorldSize => _chunkWorldSize;
        public IReadOnlyDictionary<ChunkCoord, ChunkHandle> Active => _active;
        public bool HasErosion => _erosionPass != null;
        public int BuildingCount => _building.Count;

        public void Tick(Vector3 viewer, int loadRadius, CommandBuffer cmd)
        {
            DiscoverNewChunks(viewer, loadRadius);
            AdvanceBuilds(cmd);
            EvictStale(viewer);
        }

        private void DiscoverNewChunks(Vector3 viewer, int loadRadius)
        {
            int vcx = Mathf.FloorToInt(viewer.x / _chunkWorldSize);
            int vcz = Mathf.FloorToInt(viewer.z / _chunkWorldSize);

            for (int dz = -loadRadius; dz <= loadRadius; dz++)
            {
                for (int dx = -loadRadius; dx <= loadRadius; dx++)
                {
                    var coord = new ChunkCoord(vcx + dx, vcz + dz);
                    if (_active.ContainsKey(coord)) continue;
                    if (!_policy.ShouldLoad(coord, viewer)) continue;

                    Reserve(coord, _policy.TargetLOD(coord, viewer));
                }
            }
        }

        private void Reserve(ChunkCoord coord, int lod)
        {
            int fullRes = _resolution + 2 * _apron;
            var buffers = new ChunkBufferSet(fullRes, _maxInstances);

            var origin = new float3(coord.x * _chunkWorldSize, 0f, coord.z * _chunkWorldSize);
            uint seed  = HashSeed(_worldSeed, coord);
            var ctx    = new ChunkContext(coord, origin, _cellSize, _resolution, _apron, lod, seed);

            var handle = new ChunkHandle(coord, buffers, ctx); // State = Reserved
            _active[coord] = handle;
            _building.Add(new BuildJob { Handle = handle, Stage = BuildStage.Base });
        }

        private void AdvanceBuilds(CommandBuffer cmd)
        {
            if (_building.Count == 0) return;

            int budget  = _perFrameBudget;
            int touched = 0;
            for (int i = 0; i < _building.Count && budget > 0 && touched < _maxChunksInFlight; i++)
            {
                budget -= Advance(_building[i], cmd, budget);
                touched++;
            }

            for (int i = _building.Count - 1; i >= 0; i--)
                if (_building[i].Stage == BuildStage.Done)
                    _building.RemoveAt(i);
        }

        private int Advance(BuildJob job, CommandBuffer cmd, int budget)
        {
            var handle = job.Handle;
            switch (job.Stage)
            {
                case BuildStage.Base:
                    _heightBuilder.Schedule(cmd, handle.Context, handle.Buffers);
                    handle.State = ChunkState.BaseBuilt;
                    if (_erosionPass != null)
                        job.Stage = BuildStage.Erosion;
                    else
                        EnterPostTerrainStage(job);
                    return BaseDispatchCost;

                case BuildStage.Erosion:
                    if (!_erosionPass.Caps.SupportsTimeSlicing)
                    {
                        _erosionPass.Schedule(cmd, handle.Context, handle.Buffers);
                        handle.State = ChunkState.Eroded;
                        EnterPostTerrainStage(job);
                        return budget;
                    }

                    if (job.ErosionJob == null)
                        job.ErosionJob = _erosionPass.CreateJob(handle.Context, handle.Buffers);

                    int used = job.ErosionJob.Step(cmd, budget);
                    if (job.ErosionJob.IsComplete)
                    {
                        handle.State   = ChunkState.Eroded;
                        job.ErosionJob = null;
                        EnterPostTerrainStage(job);
                    }
                    return used;

                case BuildStage.Biome:
                    _biomePass.Schedule(cmd, handle.Context, handle.Buffers);
                    handle.State = ChunkState.BiomeReady;
                    EnterPostBiomeStage(job);
                    return 1;

                case BuildStage.Placement:
                    _placementPass.Schedule(cmd, handle.Context, handle.Buffers);
                    job.Stage    = BuildStage.Done;
                    handle.State = ChunkState.Ready;
                    return 1;

                default:
                    return 0;
            }
        }

        // 地形（Base + Erosion）完了後の次ステージへ遷移。
        private void EnterPostTerrainStage(BuildJob job)
        {
            if (_biomePass != null)
                job.Stage = BuildStage.Biome;
            else
                EnterPostBiomeStage(job);
        }

        // Biome 完了後（または Biome 無し時）の次ステージへ遷移。
        private void EnterPostBiomeStage(BuildJob job)
        {
            if (_placementPass != null)
            {
                job.Stage = BuildStage.Placement;
            }
            else
            {
                job.Stage = BuildStage.Done;
                job.Handle.State = ChunkState.Ready;
            }
        }

        private void EvictStale(Vector3 viewer)
        {
            _evictScratch.Clear();
            foreach (var kv in _active)
                if (_policy.ShouldEvict(kv.Key, viewer))
                    _evictScratch.Add(kv.Key);

            for (int i = 0; i < _evictScratch.Count; i++)
                Evict(_evictScratch[i]);
        }

        private void Evict(ChunkCoord coord)
        {
            if (!_active.TryGetValue(coord, out var handle)) return;
            handle.State = ChunkState.Evicting;
            handle.Buffers.Dispose();
            _active.Remove(coord);

            for (int i = _building.Count - 1; i >= 0; i--)
                if (_building[i].Handle.Coord.Equals(coord))
                    _building.RemoveAt(i);
        }

        public void Dispose()
        {
            foreach (var kv in _active)
                kv.Value.Buffers.Dispose();
            _active.Clear();
            _building.Clear();
        }

        private static uint HashSeed(uint worldSeed, ChunkCoord coord)
        {
            unchecked
            {
                uint h = worldSeed;
                h = (h * 374761393u) + (uint)coord.x;
                h = ((h << 17) | (h >> 15)) * 668265263u;
                h ^= h >> 13;
                h = (h * 1274126177u) + (uint)coord.z;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
