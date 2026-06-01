using UnityEngine;
using UnityEngine.Rendering;
using Sandbox.World.Config;
using Sandbox.World.Generation.Base;
using Sandbox.World.Generation.Erosion;
using Sandbox.World.Generation.Biome;
using Sandbox.World.Generation.Placement;

namespace Sandbox.World
{
    /// <summary>
    /// 山岳地形生成のシーン側エントリーポイント (Step 1 設計の orchestrator).
    /// Base (Ridged Multifractal) + Erosion (Hydraulic + Thermal) を稼働。
    /// Biome / Placement パスは後続フェーズで接続。
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class TerrainGenerator : MonoBehaviour
    {
        [Header("Base (Ridged Multifractal)")]
        [SerializeField] private ComputeShader ridgedMFShader;
        [SerializeField] private RidgedMFParams ridgedMFParams;

        [Header("Erosion (Hydraulic + Thermal, optional)")]
        [SerializeField] private bool enableErosion = true;
        [SerializeField] private ComputeShader erosionShader;
        [SerializeField] private ErosionParams erosionParams;

        [Header("Biome (optional)")]
        [SerializeField] private bool enableBiome = true;
        [SerializeField] private ComputeShader biomeShader;
        [SerializeField] private BiomeParams biomeParams;

        [Header("Chunk Grid")]
        [Min(33)] [SerializeField] private int chunkResolution = 129;
        [Range(1, 8)] [SerializeField] private int apron = 2;
        [Min(0.1f)] [SerializeField] private float cellSize = 2f;
        [Range(1, 8)] [SerializeField] private int loadRadius = 2;

        [Header("LOD")]
        [SerializeField] private float lod1Distance = 600f;
        [SerializeField] private float lod2Distance = 1400f;

        [Header("Time-Slicing")]
        [Min(1)] [SerializeField] private int perFrameDispatchBudget = 8;
        [Min(1)] [SerializeField] private int maxChunksInFlight = 4;

        [Header("Placement")]
        [Min(1)] [SerializeField] private int maxInstancesPerChunk = 4096;
        [SerializeField] private bool enablePlacement = true;
        [SerializeField] private ComputeShader placementShader;
        [SerializeField] private PlacementParams placementParams;
        [SerializeField] private bool drawPlacement = true;
        [Tooltip("未指定なら Sandbox/PlacementInstancedIndirect のフォールバック材質を生成。")]
        [SerializeField] private Material placementTreeMaterial;
        [SerializeField] private Material placementRockMaterial;
        [Tooltip("未指定なら Cylinder(木)/Cube(岩) の標準プリミティブを使用。")]
        [SerializeField] private Mesh placementTreeMesh;
        [SerializeField] private Mesh placementRockMesh;
        [Tooltip("個別インスタンスがカメラからこの距離を超えると描画しない。")]
        [Min(1f)] [SerializeField] private float placementCullDistance = 300f;
        [Tooltip("カリング手前でディザフェードを開始する距離。")]
        [Min(0f)] [SerializeField] private float placementFadeStart = 220f;

        [Header("Networking (NGO)")]
        [Tooltip("ON のとき OnEnable では生成せず、NetworkedTerrainSeed.ApplyWorldSeed で同期シードを受けてから生成する。")]
        [SerializeField] private bool deferBuildToNetworkSeed = false;

        [Header("Viewer")]
        [SerializeField] private Transform viewer;
        [SerializeField] private bool autoUseMainCamera = true;

        [Header("Debug Visualization")]
        [SerializeField] private bool buildDebugMesh = true;
        [SerializeField] private Material debugMeshMaterial;
        [Tooltip("地形可視メッシュがシャドウを落とすか。既定 OFF（地形はシーン三角形の9割超を占め、" +
                 "4カスケードのシャドウパス再描画が GPU 律速の主因。受光は維持）。稜線の自己影が要るなら ON。")]
        [SerializeField] private bool castTerrainShadows = false;

        private ChunkManager _chunkManager;

        /// <summary>外部（Bootstrap / Collider baker など）から ChunkManager を参照するためのアクセサ。未生成時 null。</summary>
        public ChunkManager Manager => _chunkManager;
        /// <summary>外部から1チャンクのワールド辺長を参照する（チェックポイント配置等で利用）。</summary>
        public float ChunkWorldSize => (chunkResolution - 1) * cellSize;

        private RidgedMultifractalBuilder _builder;
        private HydraulicThermalErosionGPU _erosion;
        private BiomeClassifierGPU _biome;
        private ScatterPlacementGPU _placement;
        private PlacementIndirectRenderer _placementRenderer;
        private CommandBuffer _cmd;
        private TerrainDebugMeshBaker _meshBaker;
        private bool _built;
        private uint _currentSeed;

        private void OnEnable()
        {
            if (ridgedMFShader == null || ridgedMFParams == null)
            {
                Debug.LogError("[TerrainGenerator] Base ComputeShader / RidgedMFParams が未設定です。", this);
                enabled = false;
                return;
            }

            // ネットワーク時は同期シードを待つ（NetworkedTerrainSeed.ApplyWorldSeed で生成）。
            if (deferBuildToNetworkSeed) return;

            BuildPipeline(ridgedMFParams.worldSeed);
        }

        /// <summary>
        /// 指定シードでパイプラインを (再)構築する。NGO の同期シード適用にも使用。
        /// 既に同一シードで構築済みなら何もしない。
        /// </summary>
        public void ApplyWorldSeed(uint worldSeed)
        {
            if (ridgedMFShader == null || ridgedMFParams == null) return;
            if (_built && _currentSeed == worldSeed) return;
            if (_built) DisposePipeline();
            BuildPipeline(worldSeed);
        }

        private void BuildPipeline(uint worldSeed)
        {
            _currentSeed = worldSeed;
            _builder = new RidgedMultifractalBuilder(ridgedMFShader, ridgedMFParams);

            if (enableErosion)
            {
                if (erosionShader != null && erosionParams != null)
                {
                    _erosion = new HydraulicThermalErosionGPU(erosionShader, erosionParams);
                }
                else
                {
                    Debug.LogWarning("[TerrainGenerator] Erosion が有効ですが Shader/Params 未設定のため無効化。", this);
                }
            }

            if (enableBiome)
            {
                if (biomeShader != null && biomeParams != null)
                {
                    _biome = new BiomeClassifierGPU(biomeShader, biomeParams);
                }
                else
                {
                    Debug.LogWarning("[TerrainGenerator] Biome が有効ですが Shader/Params 未設定のため無効化。", this);
                }
            }

            if (enablePlacement)
            {
                if (placementShader != null && placementParams != null)
                {
                    _placement = new ScatterPlacementGPU(placementShader, placementParams);
                    if (drawPlacement)
                        _placementRenderer = new PlacementIndirectRenderer(
                            placementTreeMaterial, placementRockMaterial,
                            placementTreeMesh, placementRockMesh)
                        {
                            CullDistance = placementCullDistance,
                            FadeStart    = placementFadeStart
                        };
                }
                else
                {
                    Debug.LogWarning("[TerrainGenerator] Placement が有効ですが Shader/Params 未設定のため無効化。", this);
                }
            }

            // 共有エッジタイリング: ChunkManager と同じ (resolution-1)*cellSize を使う。
            float chunkWorldSize = (chunkResolution - 1) * cellSize;
            var policy = new DistanceBasedLifecyclePolicy(
                chunkWorldSize, loadRadius, loadRadius + 1, lod1Distance, lod2Distance);

            _chunkManager = new ChunkManager(_builder, _erosion, _biome, _placement, policy,
                chunkResolution, apron, cellSize, worldSeed,
                maxInstancesPerChunk, maxChunksInFlight, perFrameDispatchBudget);

            _cmd = new CommandBuffer { name = "TerrainGenerator.Build" };

            if (buildDebugMesh)
                _meshBaker = new TerrainDebugMeshBaker(transform, debugMeshMaterial, castTerrainShadows);

            if (viewer == null && autoUseMainCamera)
                viewer = Camera.main != null ? Camera.main.transform : transform;

            _built = true;
        }

        private void OnDisable()
        {
            DisposePipeline();
        }

        private void DisposePipeline()
        {
            _placementRenderer?.Dispose();
            _placementRenderer = null;
            _meshBaker?.Dispose();
            _meshBaker = null;
            _chunkManager?.Dispose();
            _chunkManager = null;
            if (_cmd != null)
            {
                _cmd.Release();
                _cmd = null;
            }
            _built = false;
        }

        private void Update()
        {
            if (_chunkManager == null || _cmd == null) return;

            Vector3 v = viewer != null ? viewer.position : transform.position;

            _cmd.Clear();
            _chunkManager.Tick(v, loadRadius, _cmd);
            Graphics.ExecuteCommandBuffer(_cmd);

            _meshBaker?.UpdateAll(_chunkManager);

            _placementRenderer?.Render(_chunkManager);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_chunkManager == null) return;
            float size = _chunkManager.ChunkWorldSize;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.5f);
            foreach (var kv in _chunkManager.Active)
            {
                var c = kv.Key;
                var center = new Vector3(
                    (c.x + 0.5f) * size,
                    0f,
                    (c.z + 0.5f) * size);
                Gizmos.DrawWireCube(center, new Vector3(size, 1f, size));
            }
        }
#endif
    }
}
