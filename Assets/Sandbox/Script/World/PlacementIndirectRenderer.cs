using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Sandbox.World
{
    /// <summary>
    /// PlacementBuffer (Append) を CPU 読み戻しせず、GraphicsBuffer.CopyCount で得た
    /// instanceCount を使って DrawMeshInstancedIndirect で GPU 直描画するレンダラ。
    /// prototype 別（Tree=Cylinder / Rock=Cube）に 1 draw ずつ。
    /// </summary>
    internal sealed class PlacementIndirectRenderer : IDisposable
    {
        private const string ShaderName = "Sandbox/PlacementInstancedIndirect";
        private static readonly int IdInstances = Shader.PropertyToID("_Instances");
        private static readonly int IdBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int IdCullDistance = Shader.PropertyToID("_CullDistance");
        private static readonly int IdFadeStart = Shader.PropertyToID("_FadeStart");

        // 個別インスタンスの距離カリング/フェード（シェーダーの setup/frag が参照）
        public float CullDistance { get; set; } = 300f;
        public float FadeStart { get; set; } = 220f;

        private readonly Mesh _treeMesh;
        private readonly Mesh _rockMesh;
        private readonly Material _treeMat;
        private readonly Material _rockMat;
        private readonly bool _ownsTreeMat;
        private readonly bool _ownsRockMat;
        private readonly Bounds _bounds = new Bounds(Vector3.zero, Vector3.one * 100000f);
        private readonly Dictionary<ChunkCoord, ChunkArgs> _chunks = new Dictionary<ChunkCoord, ChunkArgs>();
        private readonly List<ChunkCoord> _stale = new List<ChunkCoord>();
        private readonly Plane[] _frustum = new Plane[6];

        public int LastDrawnChunks { get; private set; }
        public int LastCulledChunks { get; private set; }

        public PlacementIndirectRenderer(Material treeMat, Material rockMat, Mesh treeMesh, Mesh rockMesh)
        {
            _treeMesh = treeMesh != null ? treeMesh : GrabPrimitiveMesh(PrimitiveType.Cylinder);
            _rockMesh = rockMesh != null ? rockMesh : GrabPrimitiveMesh(PrimitiveType.Cube);

            if (treeMat != null) _treeMat = treeMat;
            else { _treeMat = MakeFallback(new Color(0.15f, 0.45f, 0.18f)); _ownsTreeMat = true; }

            if (rockMat != null) _rockMat = rockMat;
            else { _rockMat = MakeFallback(new Color(0.45f, 0.42f, 0.40f)); _ownsRockMat = true; }
        }

        public void Render(ChunkManager mgr)
        {
            // 新規 Ready チャンクの args を確保
            foreach (var kv in mgr.Active)
            {
                if (kv.Value.State < ChunkState.Ready) continue;
                if (_chunks.ContainsKey(kv.Key)) continue;
                _chunks[kv.Key] = ChunkArgs.Create(_treeMesh, _rockMesh);
            }

            // エビクト済みチャンクの args を破棄
            _stale.Clear();
            foreach (var kv in _chunks)
                if (!mgr.Active.ContainsKey(kv.Key)) _stale.Add(kv.Key);
            for (int i = 0; i < _stale.Count; i++)
            {
                _chunks[_stale[i]].Dispose();
                _chunks.Remove(_stale[i]);
            }

            // フラスタムカリング準備（main camera 基準。無ければ全描画）
            var cam = Camera.main;
            bool doCull = cam != null;
            if (doCull) GeometryUtility.CalculateFrustumPlanes(cam, _frustum);

            int drawn = 0, culled = 0;

            // 描画
            foreach (var kv in _chunks)
            {
                if (!mgr.Active.TryGetValue(kv.Key, out var handle)) continue;
                if (handle.State < ChunkState.Ready) continue;

                if (doCull && !GeometryUtility.TestPlanesAABB(_frustum, ChunkBounds(handle.Context)))
                {
                    culled++;
                    continue;
                }

                var buf = handle.Buffers;
                var ca = kv.Value;
                DrawProto(_treeMesh, _treeMat, buf.PlacementTreeBuffer, ca.TreeArgs, ca.TreeMpb);
                DrawProto(_rockMesh, _rockMat, buf.PlacementRockBuffer, ca.RockArgs, ca.RockMpb);
                drawn++;
            }

            LastDrawnChunks = drawn;
            LastCulledChunks = culled;
        }

        private static Bounds ChunkBounds(in ChunkContext ctx)
        {
            // フラスタムカリング用 AABB。Y はフル標高（海面〜山頂 + 余裕）を覆う高さにする。
            // 以前は y=125±200(=-75..325) で、見上げ時に山頂(〜550m)の岩/木が誤カリングで消えていた。
            float cw = ctx.Resolution * ctx.CellSize;
            var center = new Vector3(ctx.WorldOrigin.x + cw * 0.5f, 300f, ctx.WorldOrigin.z + cw * 0.5f);
            return new Bounds(center, new Vector3(cw, 1000f, cw));
        }

        private void DrawProto(Mesh mesh, Material mat, GraphicsBuffer data,
                               GraphicsBuffer args, MaterialPropertyBlock mpb)
        {
            if (mesh == null || mat == null || data == null || args == null) return;
            GraphicsBuffer.CopyCount(data, args, sizeof(uint)); // instanceCount = args[1] (byte offset 4)
            mpb.SetBuffer(IdInstances, data);
            mpb.SetFloat(IdCullDistance, CullDistance);
            mpb.SetFloat(IdFadeStart, FadeStart);
            Graphics.DrawMeshInstancedIndirect(mesh, 0, mat, _bounds, args, 0, mpb);
        }

        public void Dispose()
        {
            foreach (var kv in _chunks) kv.Value.Dispose();
            _chunks.Clear();
            if (_ownsTreeMat && _treeMat != null) UnityEngine.Object.Destroy(_treeMat);
            if (_ownsRockMat && _rockMat != null) UnityEngine.Object.Destroy(_rockMat);
        }

        private static Material MakeFallback(Color c)
        {
            var sh = Shader.Find(ShaderName);
            var m = new Material(sh) { name = "PlacementFallbackMat", hideFlags = HideFlags.HideAndDontSave };
            m.enableInstancing = true;
            m.SetColor(IdBaseColor, c);
            return m;
        }

        private static Mesh GrabPrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.Destroy(go);
            return mesh;
        }

        private sealed class ChunkArgs
        {
            public GraphicsBuffer TreeArgs;
            public GraphicsBuffer RockArgs;
            public MaterialPropertyBlock TreeMpb;
            public MaterialPropertyBlock RockMpb;

            public static ChunkArgs Create(Mesh treeMesh, Mesh rockMesh)
            {
                return new ChunkArgs
                {
                    TreeArgs = MakeArgs(treeMesh),
                    RockArgs = MakeArgs(rockMesh),
                    TreeMpb  = new MaterialPropertyBlock(),
                    RockMpb  = new MaterialPropertyBlock()
                };
            }

            private static GraphicsBuffer MakeArgs(Mesh m)
            {
                var args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
                uint[] a = new uint[5];
                if (m != null)
                {
                    a[0] = m.GetIndexCount(0);
                    a[2] = m.GetIndexStart(0);
                    a[3] = m.GetBaseVertex(0);
                }
                args.SetData(a);
                return args;
            }

            public void Dispose()
            {
                TreeArgs?.Dispose(); TreeArgs = null;
                RockArgs?.Dispose(); RockArgs = null;
            }
        }
    }
}
