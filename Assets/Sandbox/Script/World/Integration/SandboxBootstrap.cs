using System.Collections.Generic;
using UnityEngine;
using Sandbox.World.Generation.Route;
using Sandbox.World.Environment;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// Sandbox.unity 用の統合エントリポイント。TerrainGenerator と同 GameObject に配置。
    /// 役割:
    ///  - ChunkColliderBaker を保持し、毎フレームポーリング駆動でコライダーをベイク
    ///  - 同 GameObject に Player/Summit/Checkpoint の Spawner を必要なら自動追加
    ///  - Spawner 群へ TerrainGenerator / ChunkColliderBaker 参照を配布
    /// 既存ゲームプレイ層（PlayerMovement・SummitGoal・CheckpointSystem）を A 系統地形上で動かすための薄い接着剤。
    /// </summary>
    [DefaultExecutionOrder(-30)] // TerrainGenerator(-50) の後・MeshBaker などより前
    public sealed class SandboxBootstrap : MonoBehaviour
    {
        [SerializeField] private TerrainGenerator terrainGenerator;
        [Tooltip("ON なら Player/Summit/Checkpoint/RouteVisualizer/RigUpgrade/GrappableHints などゲームプレイ系を同 GameObject に自動 AddComponent する。")]
        [SerializeField] private bool autoAttachSpawners = true;
        [Tooltip("ON なら 大気/空/雲/時刻サイクル/サミットFX などビジュアル系を同 GameObject に自動 AddComponent する。OFF にすると地形＋コライダーのみ（他シーンのゲームプレイ層へ地形だけ載せる用途）。")]
        [SerializeField] private bool autoAttachAtmosphere = true;

        [Header("Route Graph")]
        [SerializeField] private bool generateRoute = true;
        [SerializeField] private int routeGridResolution = 48;
        [SerializeField] private float routeSlopeFactor = 0.3f;
        [SerializeField] private float routeMaxClimbableSlope = 70f;

        private ChunkColliderBaker _colliderBaker;
        private SandboxRoutePath _routePath;
        private SandboxExplorerPositioner _explorerPositioner;
        private bool _routeGenerated;
        private bool _worldReadyLogged;

        public TerrainGenerator TerrainGenerator => terrainGenerator;
        public ChunkColliderBaker ColliderBaker => _colliderBaker;
        public SandboxRoutePath RoutePath => _routePath;
        public SandboxExplorerPositioner ExplorerPositioner => _explorerPositioner;

        private void Awake()
        {
            if (terrainGenerator == null)
                terrainGenerator = GetComponent<TerrainGenerator>() ?? FindFirstObjectByType<TerrainGenerator>();
            if (terrainGenerator == null)
            {
                Debug.LogError("[SandboxBootstrap] TerrainGenerator が見つかりません。", this);
                enabled = false;
                return;
            }

            _colliderBaker = new ChunkColliderBaker(terrainGenerator.transform);

            if (autoAttachSpawners)
            {
                if (GetComponent<SandboxPerformanceConfig>()    == null) gameObject.AddComponent<SandboxPerformanceConfig>();
                // P プレイヤー(Explorer)をシーン上で位置決めする。L 系 SandboxPlayerSpawner/RigUpgrade は注入しない。
                if (GetComponent<SandboxExplorerPositioner>()   == null) gameObject.AddComponent<SandboxExplorerPositioner>();
                if (GetComponent<SandboxSummitGoal>()           == null) gameObject.AddComponent<SandboxSummitGoal>();
                if (GetComponent<SandboxCheckpointBaker>()      == null) gameObject.AddComponent<SandboxCheckpointBaker>();
                if (GetComponent<SandboxRoutePath>()            == null) gameObject.AddComponent<SandboxRoutePath>();
                if (GetComponent<SandboxGrappableHints>()       == null) gameObject.AddComponent<SandboxGrappableHints>();
                if (GetComponent<SandboxGameplayDirector>()     == null) gameObject.AddComponent<SandboxGameplayDirector>();
                // スコア計算サービス（IScoreService）。Awake で自己登録するので SceneServiceInstaller より前に付ける。
                if (GetComponent<ScoreTracker>()                == null) gameObject.AddComponent<ScoreTracker>();
                if (GetComponent<SceneServiceInstaller>()       == null) gameObject.AddComponent<SceneServiceInstaller>();
            }
            if (autoAttachAtmosphere)
            {
                // ビジュアル系（地形だけを他シーンへ載せる用途では autoAttachAtmosphere=false でこのブロックを抑制）。
                // AtmosphericProfileController を DayNightCycle より先に付ける（DayNightCycle.Awake が GetComponent する）。
                if (GetComponent<AtmosphericProfileController>() == null) gameObject.AddComponent<AtmosphericProfileController>();
                if (GetComponent<DayNightCycle>()               == null) gameObject.AddComponent<DayNightCycle>();
                if (GetComponent<ProceduralSky>()               == null) gameObject.AddComponent<ProceduralSky>();
                if (GetComponent<VolumeProfileSetup>()          == null) gameObject.AddComponent<VolumeProfileSetup>();
                if (GetComponent<CloudSeaLayer>()               == null) gameObject.AddComponent<CloudSeaLayer>();
                if (GetComponent<OceanLayer>()                  == null) gameObject.AddComponent<OceanLayer>();
                if (GetComponent<SummitVisualEffects>()         == null) gameObject.AddComponent<SummitVisualEffects>();
                if (GetComponent<SummitConfettiCelebration>()   == null) gameObject.AddComponent<SummitConfettiCelebration>();
                if (GetComponent<AtmosphericParticles>()        == null) gameObject.AddComponent<AtmosphericParticles>();
            }
            _routePath = GetComponent<SandboxRoutePath>();
            _explorerPositioner = GetComponent<SandboxExplorerPositioner>();
        }

        private void Update()
        {
            if (terrainGenerator == null || terrainGenerator.Manager == null || _colliderBaker == null) return;
            _colliderBaker.UpdateAll(terrainGenerator.Manager);

            // 初期ロード完了の計測ログ（全アクティブチャンクが生成＋ベイク完了した最初の瞬間）。
            if (!_worldReadyLogged
                && terrainGenerator.Manager.BuildingCount == 0
                && _colliderBaker.BakedCount > 0
                && _colliderBaker.BakedCount == terrainGenerator.Manager.Active.Count)
            {
                _worldReadyLogged = true;
                Debug.Log($"[SandboxBootstrap] 初期地形ロード完了: {_colliderBaker.BakedCount} チャンク bake 済 / 経過 {Time.realtimeSinceStartup:F2}s (frame {Time.frameCount})");
            }

            // 全コライダーがベイク完了かつ summit 観測済みになったらルートを 1 度だけ生成
            if (generateRoute && !_routeGenerated && _colliderBaker.IsAllBaked(1) && _colliderBaker.GlobalMaxY != float.MinValue)
            {
                GenerateRoute();
                _routeGenerated = true;
            }
        }

        private void GenerateRoute()
        {
            var summit = _colliderBaker.GlobalMaxPos;
            Vector3 spawn = _explorerPositioner != null
                ? _explorerPositioner.SpawnXZ
                : new Vector3(128f, 0f, 128f);

            // bounds: loaded chunks 全体の bbox
            var mgr = terrainGenerator.Manager;
            float cw = terrainGenerator.ChunkWorldSize;
            int minCx = int.MaxValue, maxCx = int.MinValue, minCz = int.MaxValue, maxCz = int.MinValue;
            foreach (var kv in mgr.Active)
            {
                var c = kv.Key;
                if (c.x < minCx) minCx = c.x; if (c.x > maxCx) maxCx = c.x;
                if (c.z < minCz) minCz = c.z; if (c.z > maxCz) maxCz = c.z;
            }
            var cfg = new RouteGraphGenerator.Config
            {
                BoundsMin       = new Vector2(minCx * cw, minCz * cw),
                BoundsMax       = new Vector2((maxCx + 1) * cw, (maxCz + 1) * cw),
                GridResolution  = routeGridResolution,
                SlopeFactor     = routeSlopeFactor,
                MaxClimbableSlope = routeMaxClimbableSlope
            };
            var route = RouteGraphGenerator.Generate(spawn, summit, cfg);
            if (_routePath != null && route != null && route.Count >= 2)
            {
                _routePath.SetRoute(route);
                Debug.Log($"[SandboxBootstrap] route generated: nodes={route.Count}");
            }
            else
            {
                Debug.LogWarning($"[SandboxBootstrap] route generation failed (nodes={route?.Count ?? 0})");
            }
        }

        private void OnDestroy()
        {
            _colliderBaker?.Dispose();
            _colliderBaker = null;
        }
    }
}
