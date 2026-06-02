using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sandbox.World
{
    /// <summary>
    /// HeightTex を AsyncGPUReadback で取得してグリッドメッシュを生成し、
    /// BiomeColorTex を MaterialPropertyBlock で per-chunk バインドして
    /// 専用シェーダー(Sandbox/TerrainBiomeSampled)で UV サンプル着色する確認用ベイカー。
    /// LOD に応じて頂点を間引く。Step 4 で本実装に置き換える前提。
    /// </summary>
    internal sealed class TerrainDebugMeshBaker : IDisposable
    {
        private readonly Transform _root;
        private readonly Material _material;
        private readonly Dictionary<ChunkCoord, MeshBake> _bakes = new Dictionary<ChunkCoord, MeshBake>();
        private readonly List<ChunkCoord> _stale = new List<ChunkCoord>();
        private readonly bool _ownsMaterial;
        private readonly ShadowCastingMode _shadowMode;

        public TerrainDebugMeshBaker(Transform root, Material material, bool castShadows = false)
        {
            _root = root;
            // 地形可視メッシュはコライダー密度(129^2≈3.5万tri/chunk)で焼かれ、シーン総三角形の
            // 9 割超を占める。これを 4 カスケードのシャドウパスで毎フレーム再描画すると GPU が
            // 飽和する（プロファイル実測: CPU は遊休 1.5ms、フレームはシャドウ/ジオメトリ律速）。
            // 既定では地形のシャドウキャストを無効化（受光は維持）。稜線の自己影が必要なら true に。
            _shadowMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (material != null)
            {
                _material     = material;
                _ownsMaterial = false;
            }
            else
            {
                var shader = Shader.Find("Sandbox/TerrainBiomeSampled");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                _material  = new Material(shader) { name = "DebugTerrainMat", hideFlags = HideFlags.HideAndDontSave };
                _ownsMaterial = true;
            }
        }

        // 1 フレームで新規生成する可視メッシュベイクの上限。ダッシュでチャンク境界
        // (256m 毎) を跨ぐと複数チャンクが同フレームで Ready になり、BuildMesh の
        // メインスレッド処理（頂点配列構築 + RecalculateNormals, 約 1.7 万頂点/chunk）が
        // 一斉に走ってフレームが詰まる（ガクつき/瞬間停止）。1 フレーム 1 件に制限して
        // 数フレームへ分散する。可視メッシュは数フレーム遅延で出現しても問題ない
        // （コライダーは ChunkColliderBaker 側で即時ベイクするため接地は保たれる）。
        private const int MaxNewBakesPerFrame = 1;

        public void UpdateAll(ChunkManager mgr)
        {
            int created = 0;
            foreach (var kv in mgr.Active)
            {
                if (_bakes.ContainsKey(kv.Key)) continue;
                if (kv.Value.State < ChunkState.Ready) continue;
                if (created >= MaxNewBakesPerFrame) break;

                var bake = new MeshBake(_root, _material, kv.Value, _shadowMode);
                _bakes[kv.Key] = bake;
                bake.RequestReadback(kv.Value.Buffers.HeightTex, kv.Value.Buffers.BiomeColorTex);
                created++;
            }

            // 退避済みチャンクを破棄（毎フレームの List 新規確保を避け、再利用バッファで走査）。
            _stale.Clear();
            foreach (var kv in _bakes)
                if (!mgr.Active.ContainsKey(kv.Key)) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
            {
                _bakes[_stale[i]].Dispose();
                _bakes.Remove(_stale[i]);
            }
        }

        public void Dispose()
        {
            foreach (var kv in _bakes) kv.Value.Dispose();
            _bakes.Clear();
            if (_ownsMaterial && _material != null)
                UnityEngine.Object.Destroy(_material);
        }

        private sealed class MeshBake : IDisposable
        {
            private static readonly int IdBiomeColorTex  = Shader.PropertyToID("_BiomeColorTex");
            private static readonly int IdNormalSlopeTex = Shader.PropertyToID("_NormalSlopeTex");
            private static readonly int IdUVScale        = Shader.PropertyToID("_UVScale");
            private static readonly int IdUVOffset       = Shader.PropertyToID("_UVOffset");

            private readonly GameObject _go;
            private readonly MeshFilter _mf;
            private readonly MeshRenderer _mr;
            private readonly Mesh _mesh;
            private readonly ChunkContext _ctx;
            private readonly RenderTexture _biomeColorTex;
            private readonly RenderTexture _normalSlopeTex;
            private readonly int _fullRes;
            private bool _disposed;
            private bool _requested;

            public MeshBake(Transform parent, Material mat, ChunkHandle handle, ShadowCastingMode shadowMode)
            {
                _ctx            = handle.Context;
                _fullRes        = _ctx.FullResolution;
                _biomeColorTex  = handle.Buffers.BiomeColorTex;
                _normalSlopeTex = handle.Buffers.NormalSlopeTex;

                _go = new GameObject($"Chunk_{handle.Coord.x}_{handle.Coord.z}");
                _go.transform.SetParent(parent, false);
                _go.transform.position = new Vector3(_ctx.WorldOrigin.x, 0f, _ctx.WorldOrigin.z);

                _mf = _go.AddComponent<MeshFilter>();
                _mr = _go.AddComponent<MeshRenderer>();
                _mr.sharedMaterial = mat;
                // 既定では地形はシャドウを「受ける」が「落とさない」（4 カスケード×巨大メッシュの再描画を回避）。
                _mr.shadowCastingMode = shadowMode;
                _mr.receiveShadows    = true;

                // per-chunk: BiomeColorTex + apron 除外 UV リマップ係数
                var mpb = new MaterialPropertyBlock();
                if (_biomeColorTex != null)  mpb.SetTexture(IdBiomeColorTex,  _biomeColorTex);
                if (_normalSlopeTex != null) mpb.SetTexture(IdNormalSlopeTex, _normalSlopeTex);
                int n = _ctx.Resolution;
                mpb.SetFloat(IdUVScale,  (n - 1) / (float)_fullRes);
                mpb.SetFloat(IdUVOffset, (_ctx.Apron + 0.5f) / _fullRes);
                _mr.SetPropertyBlock(mpb);

                _mesh = new Mesh { name = "ChunkMesh", indexFormat = IndexFormat.UInt32 };
                _mf.sharedMesh = _mesh;
            }

            public void RequestReadback(RenderTexture heightRt, RenderTexture _unused)
            {
                if (_requested) return;
                _requested = true;
                AsyncGPUReadback.Request(heightRt, 0, TextureFormat.RFloat, OnHeights);
            }

            private void OnHeights(AsyncGPUReadbackRequest req)
            {
                if (_disposed) return;
                if (req.hasError) { Debug.LogWarning($"[MeshBaker] height readback error {_ctx.Coord}"); return; }
                BuildMesh(req.GetData<float>());
            }

            // チャンク境界の隙間（LOD 差の T 字接合や world 端の grazing 角クラック）を
            // 埋めるためのスカート下げ幅(m)。隣接チャンクと十分重なる深さにする。
            private const float SkirtDepth = 10f;

            private void BuildMesh(Unity.Collections.NativeArray<float> heights)
            {
                int M     = _fullRes;
                int N     = _ctx.Resolution;
                int apron = _ctx.Apron;
                float ds  = _ctx.CellSize;

                // 可視メッシュは常にフル解像度で焼く（コライダーと同一密度）。
                // LOD で間引くと隣接チャンクの LOD 差で共有辺の頂点密度がズレ、T 字接合の
                // クラック/穴になる（skirt では塞ぎきれない）。フル解像度なら全チャンクの
                // 共有辺の頂点が一致し、構造的に継ぎ目が出ない。LOD は GPU 生成側の将来
                // 最適化用に温存し、メッシュ密度には使わない。
                const int step = 1;
                int gn = N;

                var vertices  = new List<Vector3>(gn * gn + gn * 4);
                var uvs        = new List<Vector2>(gn * gn + gn * 4);
                var triangles = new List<int>((gn - 1) * (gn - 1) * 6 + (gn - 1) * 4 * 12);

                for (int z = 0; z < gn; z++)
                {
                    int sz = Mathf.Min(z * step, N - 1);
                    for (int x = 0; x < gn; x++)
                    {
                        int sx = Mathf.Min(x * step, N - 1);
                        int src = (sz + apron) * M + (sx + apron);
                        vertices.Add(new Vector3(sx * ds, heights[src], sz * ds));
                        uvs.Add(new Vector2(sx / (float)(N - 1), sz / (float)(N - 1)));
                    }
                }

                for (int z = 0; z < gn - 1; z++)
                {
                    for (int x = 0; x < gn - 1; x++)
                    {
                        int a = z * gn + x;
                        int b = z * gn + (x + 1);
                        int c = (z + 1) * gn + x;
                        int d = (z + 1) * gn + (x + 1);
                        triangles.Add(a); triangles.Add(c); triangles.Add(b);
                        triangles.Add(b); triangles.Add(c); triangles.Add(d);
                    }
                }

                // 周縁 4 辺にスカート（両面）を生やす。両面なので winding を気にせず常に隙間を覆う。
                void Skirt(System.Func<int, int> gridIdx, int count)
                {
                    int sBase = vertices.Count;
                    for (int k = 0; k < count; k++)
                    {
                        int g = gridIdx(k);
                        Vector3 v = vertices[g];
                        vertices.Add(new Vector3(v.x, v.y - SkirtDepth, v.z));
                        uvs.Add(uvs[g]);
                    }
                    for (int k = 0; k < count - 1; k++)
                    {
                        int t0 = gridIdx(k), t1 = gridIdx(k + 1);
                        int s0 = sBase + k, s1 = sBase + k + 1;
                        triangles.Add(t0); triangles.Add(t1); triangles.Add(s1);
                        triangles.Add(t0); triangles.Add(s1); triangles.Add(s0);
                        triangles.Add(t0); triangles.Add(s1); triangles.Add(t1); // 裏面
                        triangles.Add(t0); triangles.Add(s0); triangles.Add(s1);
                    }
                }
                Skirt(k => k, gn);                     // -Z 辺
                Skirt(k => (gn - 1) * gn + k, gn);     // +Z 辺
                Skirt(k => k * gn, gn);                // -X 辺
                Skirt(k => k * gn + (gn - 1), gn);     // +X 辺

                _mesh.Clear();
                _mesh.SetVertices(vertices);
                _mesh.SetUVs(0, uvs);
                _mesh.SetTriangles(triangles, 0);
                _mesh.RecalculateNormals();
                _mesh.RecalculateBounds();
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
