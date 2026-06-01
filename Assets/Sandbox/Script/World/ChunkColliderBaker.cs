using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sandbox.World
{
    /// <summary>
    /// Ready チャンクから HeightTex を AsyncGPUReadback で取得し、
    /// バックグラウンドスレッドで Physics.BakeMesh → MeshCollider に流し込む。
    /// プレイヤー/物理用の足場。退避時に破棄。
    /// 既存の TerrainDebugMeshBaker と同じポーリング駆動パターン（イベント不要）。
    /// </summary>
    public sealed class ChunkColliderBaker : IDisposable
    {
        private readonly Transform _root;
        private readonly Dictionary<ChunkCoord, ColliderEntry> _entries = new Dictionary<ChunkCoord, ColliderEntry>();
        private readonly List<ChunkCoord> _stale = new List<ChunkCoord>();

        /// <summary>現フレームでエントリ作成済みのチャンク数（コライダーメッシュは bake 中の可能性あり）。</summary>
        public int LastReadyCount { get; private set; }
        /// <summary>現フレームで MeshCollider にメッシュ割当て済みの数（async bake 完了済み）。</summary>
        public int BakedCount { get; private set; }
        /// <summary>LastReadyCount==BakedCount かつ minRequired を満たすと true。Spawn/CP/Summit が安全に走るタイミング。</summary>
        public bool IsAllBaked(int minRequired) => BakedCount >= minRequired && BakedCount == LastReadyCount;
        /// <summary>これまでに観測した全コライダーメッシュのワールド最高 Y。</summary>
        public float GlobalMaxY { get; private set; } = float.MinValue;
        /// <summary>GlobalMaxY を観測したワールド座標。</summary>
        public Vector3 GlobalMaxPos { get; private set; }

        public ChunkColliderBaker(Transform root) { _root = root; }

        public void UpdateAll(ChunkManager mgr)
        {
            foreach (var kv in mgr.Active)
            {
                if (kv.Value.State < ChunkState.Ready) continue;
                if (_entries.ContainsKey(kv.Key)) continue;
                var e = new ColliderEntry(_root, kv.Value, this);
                _entries[kv.Key] = e;
                e.RequestReadback();
            }

            _stale.Clear();
            foreach (var kv in _entries)
                if (!mgr.Active.ContainsKey(kv.Key)) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
            {
                _entries[_stale[i]].Dispose();
                _entries.Remove(_stale[i]);
            }
            LastReadyCount = _entries.Count;
            int baked = 0;
            foreach (var kv in _entries) if (kv.Value.IsBaked) baked++;
            BakedCount = baked;
        }

        internal void RegisterPeak(Vector3 worldPos)
        {
            if (worldPos.y > GlobalMaxY)
            {
                GlobalMaxY = worldPos.y;
                GlobalMaxPos = worldPos;
            }
        }

        public void Dispose()
        {
            foreach (var kv in _entries) kv.Value.Dispose();
            _entries.Clear();
        }

        private sealed class ColliderEntry : IDisposable
        {
            private readonly GameObject _go;
            private readonly MeshCollider _collider;
            private readonly ChunkContext _ctx;
            private readonly RenderTexture _heightTex;
            private readonly int _fullRes;
            private readonly ChunkColliderBaker _owner;
            private Mesh _mesh;
            private bool _disposed;
            private bool _requested;
            public bool IsBaked { get; private set; }

            public ColliderEntry(Transform parent, ChunkHandle handle, ChunkColliderBaker owner)
            {
                _ctx = handle.Context;
                _heightTex = handle.Buffers.HeightTex;
                _fullRes = _ctx.FullResolution;
                _owner = owner;

                _go = new GameObject($"ChunkCollider_{handle.Coord.x}_{handle.Coord.z}");
                _go.transform.SetParent(parent, false);
                _go.transform.position = new Vector3(_ctx.WorldOrigin.x, 0f, _ctx.WorldOrigin.z);
                _collider = _go.AddComponent<MeshCollider>();
            }

            public void RequestReadback()
            {
                if (_requested) return;
                _requested = true;
                AsyncGPUReadback.Request(_heightTex, 0, TextureFormat.RFloat, OnHeights);
            }

            private async void OnHeights(AsyncGPUReadbackRequest req)
            {
                if (_disposed) return;
                if (req.hasError)
                {
                    Debug.LogWarning($"[ChunkColliderBaker] height readback error {_ctx.Coord.x},{_ctx.Coord.z}");
                    return;
                }

                int M = _fullRes;
                int N = _ctx.Resolution;
                int apron = _ctx.Apron;
                float ds = _ctx.CellSize;
                var heights = req.GetData<float>();

                var vertices = new Vector3[N * N];
                var triangles = new int[(N - 1) * (N - 1) * 6];
                float localMaxY = float.MinValue;
                int peakIdx = 0;

                for (int z = 0; z < N; z++)
                {
                    for (int x = 0; x < N; x++)
                    {
                        int src = (z + apron) * M + (x + apron);
                        float y = heights[src];
                        int idx = z * N + x;
                        vertices[idx] = new Vector3(x * ds, y, z * ds);
                        if (y > localMaxY) { localMaxY = y; peakIdx = idx; }
                    }
                }

                int t = 0;
                for (int z = 0; z < N - 1; z++)
                {
                    for (int x = 0; x < N - 1; x++)
                    {
                        int a = z * N + x;
                        int b = z * N + (x + 1);
                        int c = (z + 1) * N + x;
                        int d = (z + 1) * N + (x + 1);
                        triangles[t++] = a; triangles[t++] = c; triangles[t++] = b;
                        triangles[t++] = b; triangles[t++] = c; triangles[t++] = d;
                    }
                }

                _mesh = new Mesh
                {
                    name = $"ChunkColliderMesh_{_ctx.Coord.x}_{_ctx.Coord.z}",
                    indexFormat = IndexFormat.UInt32
                };
                _mesh.vertices = vertices;
                _mesh.triangles = triangles;
                _mesh.RecalculateBounds();

                int meshId = _mesh.GetInstanceID();
                await Awaitable.BackgroundThreadAsync();
                // Physics.BakeMesh(int, bool) は Unity 6 で obsolete だが、EntityId 版は DOTS 依存。
                // 機能は同一のため警告のみ抑止。
                #pragma warning disable 0618
                Physics.BakeMesh(meshId, false);
                #pragma warning restore 0618
                await Awaitable.MainThreadAsync();
                if (_disposed) { UnityEngine.Object.Destroy(_mesh); return; }

                _collider.sharedMesh = _mesh;
                IsBaked = true;

                // peak をワールド座標に変換して baker に登録
                var peakLocal = vertices[peakIdx];
                var peakWorld = new Vector3(_ctx.WorldOrigin.x + peakLocal.x, peakLocal.y, _ctx.WorldOrigin.z + peakLocal.z);
                _owner.RegisterPeak(peakWorld);
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
                if (_go != null) UnityEngine.Object.Destroy(_go);
            }
        }
    }
}
