using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Sandbox.UI;
using Sandbox.World.Environment;
using Sandbox.World.Generation.Placement;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// SandboxOfflineCombined.unity 専用。Sandbox 手続き地形（チャンクコライダー）がベイクされた後に、
    /// OfflineTestScene 由来の「フラット地面前提」ゲームプレイ層を地形面へ整合し、Sandbox 地形を唯一の
    /// 地面として使う状態にする。
    ///
    /// 解決する点:
    ///   (1) 基地キャンプの平坦化 …… footprint（基地占有 XZ）に「平坦な天面＋角を丸めた傾斜スカート」の
    ///       台座(BasecampPad) を生成。footprint 内は天面高さ(_padTopY)へ解析的に揃え完全に平坦、周囲地形へは
    ///       放射状に広がる滑らかなスロープで繋ぐ（四角錐台の角の交差を回避）。
    ///   (2) NPC/敵の徘徊 …… NavMesh 不使用・Rigidbody ステアリング AI。整合後に各エージェントの基点を
    ///       地表へ ReanchorHome（死亡リスポーンの地形下湧き防止）。長時間スタック時は「前方/到達可能な
    ///       近傍地点」へ退避させる（基地へ引き戻すと往復しがちなので、進行方向を優先する）。
    ///   (3) 大気フォグ …… 低高度の白飛びを抑えるため AtmosphericProfileController.OverrideFog で
    ///       フォグ距離を遠ざけ色を澄ませる（combined シーン限定）。
    ///   (4) 遅延スポーン …… 初期整合の後も維持フェーズで接地面より埋没した Rigidbody を継続救出。
    /// </summary>
    [DefaultExecutionOrder(-20)] // SandboxBootstrap(-30) の後
    public sealed class CombinedTerrainConformer : MonoBehaviour
    {
        [Header("Sampling")]
        // 拠点（リスポーン/出撃地点）の中心 XZ。山(center=(128,688))の麓を、山頂から放射状に最も外側＝
        // 海際の低い渚(y≈10-30m)へ置く。これにより『海辺の遠征基地から、緩やかな裾野を経て徐々に勾配が
        // 上がる自然な山を登り、山頂(≈y760)を目指す』開始構図になる。山頂とは反対側(−Z 方向)の海岸線
        // 直前に固定（決定論的地形のため安全。pad スカートが周囲地形へ滑らかに接続。Awake の植生除外円も
        // この XZ を使うので pad/植生/ジップライン/登攀コースが一貫追従）。海を見下ろす乾いた見晴らしベンチへ微調整
        // （Z=-540≒海抜24m。waterline は Z≈-723、手前に砂浜＋緩斜面を約180m挟み、台座は海に入らず海を一望）。
        [SerializeField] private Vector2 probeXZ = new Vector2(128f, -540f);
        [SerializeField] private float rayFromAltitude = 1200f;
        [SerializeField] private int minBakedChunks = 4;

        [Header("Basecamp Pad（平坦化＋丸角スロープ）")]
        [Tooltip("拠点（リスポーン地点）の設計上の半辺[m]。BasecampBuilder の手続きキャンプに合わせ、台座を spawn 中心の対称な正方形にする（旧・散在オブジェクト由来の非対称 footprint を廃止）。")]
        [SerializeField] private float campHalfExtent = 17f;
        [Tooltip("拠点中心から半径[m]の円内には手続き植生（木/岩）を生やさない。台座の上に木が突き抜けるのを防ぐ。生成開始前(Awake)に ScatterPlacementGPU へ設定する。")]
        [SerializeField] private float scatterExcludeRadius = 20f;
        [SerializeField] private float padClearance = 0.3f;
        [SerializeField] private int padSampleGrid = 7;
        [Range(0.1f, 1f)] [SerializeField] private float padReadyFraction = 0.7f;
        [SerializeField] private float padSkirtMinWidth = 14f;
        [SerializeField] private float padSkirtSlopeFactor = 1.4f;
        [Tooltip("天面コーナーの丸め半径。")]
        [SerializeField] private float padCornerRadius = 6f;
        [Tooltip("コーナー1つあたりの円弧分割数（多いほど滑らか）。")]
        [SerializeField] private int padCornerSegments = 6;

        [Header("Placement")]
        [SerializeField] private float playerLift = 1.2f;
        [Tooltip("スポーン時に各エージェント（プレイヤー/NPC）を重ねないための最小間隔[m]。拠点まわりの同心リング上の空きスロットへ1人ずつ割り当てる。")]
        [SerializeField] private float partySpawnSpacing = 3f;
        [SerializeField] private float objectLift = 0f;
        [SerializeField] private float buriedThreshold = 1.5f;
        [SerializeField] private float maxConformSeconds = 12f;

        [Header("Maintenance（維持フェーズ）")]
        [SerializeField] private float maintenanceInterval = 0.4f;
        [SerializeField] private float stuckEscapeSeconds = 9f;
        [SerializeField] private float stuckMoveEpsilon = 0.5f;
        [Tooltip("スタック脱出時に前方/近傍へ動かす距離。")]
        [SerializeField] private float escapeNudgeDist = 3.5f;
        [Tooltip("脱出先として許容する上下段差（これを超える崖は不可とみなす）。")]
        [SerializeField] private float escapeClimbStep = 2.5f;
        [Tooltip("前方・近傍に脱出先が無いときだけ基地中心方向へ引き戻す割合（最終手段）。")]
        [Range(0.05f, 1f)] [SerializeField] private float escapeFallbackPull = 0.4f;

        [Header("Agent Containment（海・島外への徘徊脱出を防ぐ）")]
        [Tooltip("ON で、海上（baked 島の外）や渚に踏み出した NPC/敵を拠点へ引き戻し、AI ホームも拠点へ付け替える。プレイヤーは対象外。")]
        [SerializeField] private bool containAgents = true;
        [Tooltip("接地高度がこの値[m]未満（＝海面プレーン y≈4 付近の渚）に出た NPC/敵を拠点へ引き戻す。海岸は島で最も低い land なので、内陸の高所追跡中の敵には誤発火しない。")]
        [SerializeField] private float agentSeaContainY = 8f;

        [Header("Sea Barrier（海への侵入を防ぐ見えない壁）")]
        [Tooltip("ON で、山頂中心の円筒メッシュコライダー（不可視）を海岸線手前に設置し、プレイヤー/NPC が海へ歩いて出られないようにする。")]
        [SerializeField] private bool seaBarrier = true;
        [Tooltip("山頂から海側へ進んで接地高度がこの値[m]を下回った地点を海岸線とみなし、その半径に壁を立てる（放射状なので湾の曲がりに追従）。")]
        [SerializeField] private float seaBarrierGroundY = 8f;
        [Tooltip("壁の下端 Y[m]（海底まで覆う）。")]
        [SerializeField] private float seaBarrierBottomY = -40f;
        [Tooltip("壁の上端 Y[m]（渚の低地より十分高く＝飛び越え不可。内陸では地中に埋まり無害）。")]
        [SerializeField] private float seaBarrierTopY = 50f;
        [Tooltip("円筒の分割数（多いほど海岸線に滑らかに追従）。")]
        [SerializeField] private int seaBarrierSegments = 128;

        [Header("Atmosphere Fog（低高度の白飛び緩和・高度連動）")]
        [SerializeField] private bool tuneFog = true;
        // 空気遠近を効かせて山岳のスケールを出す。谷は白飛び回避で距離を保ちつつ、登るほど
        // フォグが近づき遠景の尾根が層状に大気へ溶ける（高所ほど叙景的に）。
        [Tooltip("谷（低高度）のフォグ開始距離。白飛びを抑えるため遠ざける。")]
        [SerializeField] private float fogStartOverride = 420f;  // = low start
        [Tooltip("谷（低高度）のフォグ終了距離。")]
        [SerializeField] private float fogEndOverride = 3400f;    // = low end
        [Tooltip("高所のフォグ開始距離。空気遠近の奥行きを出すため近づける。")]
        [SerializeField] private float fogStartHigh = 260f;
        [Tooltip("高所のフォグ終了距離。遠景の尾根を大気色へ層状に溶かす。")]
        [SerializeField] private float fogEndHigh = 2050f;
        [Tooltip("距離/色の補間が high 側へ到達する基準カメラ高度（山頂相当 ≈485m）。")]
        [SerializeField] private float fogAltitudeForFullClear = 460f;
        [SerializeField] private Color fogLowOverride = new Color(0.70f, 0.78f, 0.88f, 1f);
        [SerializeField] private Color fogHighOverride = new Color(0.64f, 0.74f, 0.90f, 1f);

        [Header("Duplicate Camera（残置 MainCamera の整理）")]
        [Tooltip("実行時にプレイヤー追従カメラ以外の残置 MainCamera を無効化し、追従カメラを唯一の MainCamera にする。")]
        [SerializeField] private bool resolveDuplicateCameras = true;
        [Tooltip("ゲームプレイカメラの遠クリップ面。遠景の岩峰を描くため広げる。")]
        [SerializeField] private float gameplayCamFarClip = 1800f;
        [Tooltip("追従カメラに UniversalAdditionalCameraData を付与し renderPostProcessing/SMAA を有効化する。Global Volume の Bloom/Tonemapping/ColorAdjustments を画面へ反映させるために必須。")]
        [SerializeField] private bool enablePostProcessing = true;

        [Header("Climb Course Distribution（手続き山へ登攀コースを再配置）")]
        [Tooltip("ON で Mountain 岩 + ClimbingPoints を基地へ平坦化せず、基地→山頂ルート沿いに標高順で手続き山へ再配置する。")]
        [SerializeField] private bool distributeClimbCourse = true;
        // 遺物(Relics)もルート沿いへ再配置する。安全性は調査済（2026-05-31）:
        //  - 発見=RelicDiscoveryTrigger の 20m 球、得点=RelicBase 参照、NPC=FindObjectsByType<RelicBase>、
        //    SingingVase の妨害=実 transform 距離 ＝ いずれも位置キャッシュ無しで再配置に追従する。
        //  - 二重生成なし: SpawnManager は ZoneRuntime の SpawnPoint プレハブを Instantiate するだけで、
        //    手作り Relics オブジェクトには触れない（authored relics は別個の収集物）。
        //  ※ Hazards は CollapsiblePlatform/FallingCeiling が Awake/Start で位置をキャッシュするため runtime 移動は不可
        //    （conformer は exec order -20 で後から動くので崩落/落下判定が壊れる）。本フラグの対象外。
        [Tooltip("ON で 遺物(Relics) も登攀ルート沿いへゾーン順に再配置する。Hazards は位置キャッシュのため対象外。")]
        [SerializeField] private bool distributeRelics = true;
        // 遺物の供給を一系統（authored の手作り Relics コンテナ＝conformer がルート沿いへ接地配置する正規収集物）に
        // 統一する。SandboxOfflineCombined は OfflineTest 由来の SpawnManager L3 が SpawnPoint(Relic) からも
        // 3〜5個の遺物クローンを基地/原点付近に生成しており（遺物供給二系統）、それらは適切に接地されず
        // NPC に拾われて拠点構造物で破壊される・あるいは ReturnZone 内に湧いて「無料ノルマ」になる等の不具合源。
        // ON で SpawnManager.Start（=L3 生成）より前（Awake）に SpawnPoint(Relic) を無効化し、クローン供給を断つ。
        // 抽出ノルマは価値ベース（ReturnZone 内遺物価値の合計, レベル1=120pt）で、authored 8種(各~100-130pt,計~880pt)
        // のみで十分達成可能なため安全。Hazard/Item/Route 層の SpawnPoint には触れない。
        [Tooltip("ON で SpawnPoint(Relic) を無効化し、遺物供給を authored の Relics コンテナ一系統に統一する（基地に湧く重複クローンを断つ）。")]
        [SerializeField] private bool disableSpawnPointRelicSupply = true;
        // 復活の祠(ReviveShrines)もルート沿いへ再配置する。安全性は調査済（2026-06-01）:
        //  - ReviveShrine は static s_registeredShrines に OnEnable で登録し、検出は GhostSystem が
        //    shrine.transform.position を実距離で参照する（位置キャッシュ無し）＝ runtime 再配置に追従する。
        //  - GhostSystem に基地フォールバックは無いが、祠は Shrine_Zone1/2/4 とゾーン名を持つため、
        //    ParseZone でルート上の標高順（Zone1=最下 fraction 0.08 ≒ 基地近傍）へ写像される。これにより
        //    「ゾーン到達保証」と「最下ゾーンに到達容易な祠が必ず1つ」が同時に満たされる（旧・対象外の懸念を解消）。
        //  - 祠は復活の安全拠点。幽霊は登攀ルートを辿って戻るため、横/前後ばらつきを半減してルート線へ寄せ発見性を上げる。
        [Tooltip("ON で 復活の祠(ReviveShrines) も登攀ルート沿いへゾーン順に再配置する（ルート線寄せ・低ゾーンに必ず1つ）。")]
        [SerializeField] private bool distributeShrines = true;
        [Tooltip("再配置を始めるのに必要な最小ベイク済チャンク数。")]
        [SerializeField] private int climbDistributeMinChunks = 16;
        [Tooltip("遠い山頂チャンクは後から焼けるため、GlobalMaxY がこの秒数 伸びなくなる（＝山頂確定）まで再配置を待つ。")]
        [SerializeField] private float climbSummitStableSeconds = 1.5f;
        [Tooltip("ルート上の最下ゾーン(Zone1)の位置（0=基地, 1=山頂）。")]
        [Range(0f, 0.5f)] [SerializeField] private float climbStartFraction = 0.08f;
        [Tooltip("ルート上の最上ゾーン(Zone5)の位置。Zone6_Summit は常に山頂(1.0)へ。")]
        [Range(0.5f, 1f)] [SerializeField] private float climbTopFraction = 0.9f;
        [Tooltip("ゾーン内のルート横方向のばらつき[m]。")]
        [SerializeField] private float climbLateralSpread = 24f;
        [Tooltip("ルートに沿う前後ばらつき[m]。")]
        [SerializeField] private float climbAlongSpread = 18f;
        [Tooltip("接地面からの持ち上げ[m]（岩棚が地表に乗る）。")]
        [SerializeField] private float climbLift = 0.5f;

        [Header("Zipline Checkpoints（拠点⇄チェックポイントのジップライン）")]
        [Tooltip("ON で 基地→山頂ルート沿いにジップライン用チェックポイントを配置し、到達で拠点へのジップラインを開通させる。")]
        [SerializeField] private bool placeZiplineCheckpoints = true;
        [Tooltip("チェックポイントを置く『ステージ(ゾーン)』番号。各ゾーンの始まり（最下端）に 1 本ずつジップラインを開通させる。1=基地隣接, 6=山頂。既定は中間ステージ Zone2〜5。")]
        [SerializeField] private int[] ziplineCheckpointZones = { 2, 3, 4, 5 };
        [Tooltip("（任意）ゾーン指定の代わりに使うルート割合（0=基地, 1=山頂）。ziplineCheckpointZones が空のときのみ使用。")]
        [SerializeField] private float[] ziplineCheckpointFractions = { 0.34f, 0.58f, 0.82f };
        [Tooltip("チェックポイント到達トリガーの半径[m]。")]
        [SerializeField] private float ziplineCheckpointRadius = 5f;
        [Tooltip("登攀コースの岩/足場をチェックポイント中心からこの半径[m]内に置かない（ジップラインのステーションと被らないよう確保）。")]
        [SerializeField] private float checkpointClearRadius = 7f;
        [Tooltip("チェックポイントをルート中心線から左右へ振る量[m]。直線上に密集して見えるのを避け、各ステージで散らす。")]
        [SerializeField] private float ziplineCheckpointLateralSpread = 26f;

        // ※ "Basecamp" は含めない：BasecampBuilder が pad 天面へ精密に配置するため、ここで再スナップしない。
        [SerializeField] private string[] containerNames =
            { "Mountain", "Relics", "ReviveShrines", "Hazards", "ClimbingPoints", "NetworkPlayerSpawner" };

        private const string PadName = "BasecampPad";

        private SandboxBootstrap _bootstrap;
        private GameObject _safetyFloor;
        private GameObject _pad;
        private Vector2 _footMin, _footMax; // x=ワールドX / y=ワールドZ
        private float _padTopY;
        private readonly List<Transform> _pending = new List<Transform>();
        private readonly List<Transform> _climbObjects = new List<Transform>();
        private bool _climbDistributed;
        private bool _ziplineCheckpointsPlaced;
        // ジップライン用チェックポイントの確定 XZ と、全ライン共通の持ち上げ量[m]（探索で算出して一度だけ確定）。
        private bool _ziplineChosen;
        private readonly List<Vector2> _ziplineXZ = new List<Vector2>();
        private float _ziplineUniformRaise;
        private float _climbMaxY = -1f;
        private float _climbStableTime;
        private readonly HashSet<int> _anchored = new HashSet<int>();
        private readonly Dictionary<int, AgentTrack> _tracks = new Dictionary<int, AgentTrack>();
        private bool _padBuilt;
        private bool _fogTuned;
        // 標高バンドを最後に適用したときの観測山頂[m]。-1=未適用。山頂が後から伸びる（遠い
        // 高所チャンクが後着でベイク）たびに再適用するため、一度きりではなくこの値で差分判定する。
        private float _lastBandSummitY = -1f;
        private bool _mountainSoftened;
        private bool _legacyClimbPlaceholdersRemoved;
        private bool _fogCamLinked;
        private bool _camerasResolved;
        private bool _playerPlaced;
        private bool _partyPlaced;
        private bool _seaBarrierBuilt;
        // スポーン済みエージェント(プレイヤー+NPC)の XZ。次のスポーンが重ならない空きスロットを選ぶのに使う。
        private readonly List<Vector2> _spawnOccupied = new List<Vector2>();
        private bool _initialDone;
        private float _startTime;
        private float _maintTimer;
        private int _rescuedCount;

        // 維持フェーズの全シーン走査キャッシュ。RescueBuriedBodies / StuckWatchdog は従来
        // 毎回（初期化中は毎フレーム、維持中は maintenanceInterval=0.4s 毎）に
        // FindObjectsByType<Rigidbody/NPCController/EnemyController> を呼んでおり、フレーム
        // スパイクの一因だった。長間隔で 1 回だけ走査し、配列を使い回す（破棄済みは null チェック）。
        private const float AgentCacheInterval = 2f;
        private float _agentCacheRefresh = float.MinValue;
        private Rigidbody[] _cachedBodies;
        private NPCController[] _cachedNpcs;
        private EnemyController[] _cachedEnemies;

        public bool Done => _initialDone;

        private struct AgentTrack { public Vector3 lastPos; public float still; }

        private void Awake()
        {
            // 既存の SandboxOfflineCombined シーンには probeXZ=(0,0)（島中央＝中腹 y≈290）が直列化されており、
            // これが「スタート地点が高すぎる」原因。(0,0) と旧自動既定 (-52,-173)（まだ中腹寄りで背後に急斜面が
            // 迫る）はいずれも「未設定＝自動」とみなし、海際の低い渚へ移行する（シーンアセットを編集せずコードで
            // 一貫させる。Inspector で別値を明示設定済みならそれを尊重）。
            var coastalDefault = new Vector2(128f, -540f);
            if (probeXZ == Vector2.zero || probeXZ == new Vector2(-52f, -173f))
                probeXZ = coastalDefault;

            // 生成（TerrainGenerator.Start → 各チャンクの scatter dispatch は Update 以降）より前に
            // 拠点の植生除外円を設定する。Awake は全 Start/Update より先に走るため確実に間に合う。
            ScatterPlacementGPU.ExcludeXZRadius = new Vector4(probeXZ.x, probeXZ.y, scatterExcludeRadius, 0f);

            _bootstrap = GetComponent<SandboxBootstrap>();
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<SandboxBootstrap>();
            if (_bootstrap == null)
            {
                Debug.LogError("[CombinedTerrainConformer] SandboxBootstrap が見つかりません。地形整合をスキップします。", this);
                enabled = false;
                return;
            }
            CreateSafetyFloor();
            EnsureWireRopeHud();
            EnsureUiFontUnified();
            DisableSpawnPointRelicSupply();
        }

        /// <summary>
        /// 遺物供給を authored の Relics コンテナ一系統に統一するため、SpawnPoint(Relic) を Awake で無効化する。
        /// SpawnManager.Start（L3 遺物生成）は全 Awake の後に走り、かつ FindObjectsByType は既定で inactive を
        /// 除外するため、ここで SetActive(false) にした Relic 用 SpawnPoint からはクローンが生成されない。
        /// 万一 既に生成済みなら Deactivate() で取り除く（防御的）。Hazard/Item/Route 層には触れない。
        /// </summary>
        private void DisableSpawnPointRelicSupply()
        {
            if (!disableSpawnPointRelicSupply) return;

            var points = FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
            int disabled = 0;
            foreach (var sp in points)
            {
                if (sp == null || sp.Layer != SpawnLayer.Relic) continue;
                sp.Deactivate();                 // 既に湧いていれば除去（通常は未生成で no-op）
                sp.gameObject.SetActive(false);  // SpawnManager.Start の走査対象から外す
                disabled++;
            }
            if (disabled > 0)
                Debug.Log($"[CombinedTerrainConformer] 遺物供給を一系統化: SpawnPoint(Relic) を {disabled} 個無効化（authored Relics のみ使用）。");
        }

        /// <summary>
        /// SandboxOfflineCombined は autoAttachSpawners=false のため SandboxGameplayDirector が付かず
        /// HudManager が無い。R キー ワイヤーロープの力ゲージ表示用に 1 つだけ生成する。
        /// </summary>
        private static void EnsureWireRopeHud()
        {
            if (Object.FindFirstObjectByType<HudManager>() != null) return;
            var hudGo = new GameObject("HudManager");
            // 力ゲージ + クロスヘアのみ。タイマー/CP/高度は ExpeditionHUD が担うので
            // gauge-only モードにして HUD の二重表示を防ぐ。
            hudGo.AddComponent<HudManager>().WireRopeGaugeOnly = true;
            Debug.Log("[CombinedTerrainConformer] WireRope 用 HudManager (gauge-only) を生成しました。");
        }

        /// <summary>
        /// ベイク済み UI に混在する LiberationSans(TMP 既定) を排し、TMP テキストを
        /// プロジェクト標準の NotoSansJP へ統一する。シーンアセットは書き換えず実行時のみ適用（非破壊）。
        /// </summary>
        private static void EnsureUiFontUnified()
        {
            int changed = UiFontUnifier.UnifySceneToProjectFont();
            if (changed > 0)
                Debug.Log($"[CombinedTerrainConformer] TMP フォントを NotoSansJP へ統一しました（{changed} 件）。");
        }

        private void Update()
        {
            if (_bootstrap == null || _bootstrap.ColliderBaker == null) return;

            // フォグは地形ベイク完了を待たず、最初の Update（＝最初の描画前）から整える。
            // pad ビルド待ちにしていると、その数フレーム〜数秒間だけ初期の濃いフォグ(320-2800m)で遠景が白く霞み、
            // 整合後に急にクリアになる『フォグの後出し』が見えていた。TuneFog は _fogTuned で冪等。
            TuneFog();

            if (_bootstrap.ColliderBaker.BakedCount < minBakedChunks) return;
            if (!_padBuilt && !TrySampleGround(probeXZ.x, probeXZ.y, out _)) return; // 基地中心に地形が出てから

            if (!_padBuilt)
            {
                if (!TryBuildBasecampPad()) return;
                // 平坦化した pad 天面の上に、AAA 級の遠征キャンプを手続き的に組み上げる
                // （仮の Cube 5 個 → 整理された拠点へ）。機能コンポーネントは温存・見た目のみ作り直す。
                BasecampBuilder.Build(new Vector3(probeXZ.x, _padTopY, probeXZ.y), _padTopY);
                RemoveLegacyClimbPlaceholders(); // 拠点直下の茶色い板(Ramp_* + Zone1_Rock*)を除去。Zone2〜山頂は温存
                CollectPending();
                SoftenMountainMaterials();
                _startTime = Time.time;
            }

            LinkFogCamera();          // プレイヤー出現後、高度連動フォグの参照を追従カメラへ（一度成功で固定）
            ResolveDuplicateCameras(); // 残置 MainCamera を無効化し追従カメラを唯一の MainCamera に（同上）
            DistributeClimbCourse();   // 山頂ベイク確定後、登攀コースを手続き山へ標高順に再配置（一度成功で固定）
            PlaceZiplineCheckpoints(); // 登攀コース確定後、ルート沿いにジップライン用チェックポイントを設置（一度成功で固定）
            PublishAltitudeProfile();  // 実山高(基地→山頂)を MountainProfile へ公開（天候/凍傷/高山病/高度計が連動）
            TuneElevationBands();      // 観測山頂が確定したら地形の標高バンド(草/岩/雪線)を実山高へ比例追従（一度成功で固定）
            BuildSeaBarrier();         // 海岸線手前に不可視の円筒壁を設置し、海への侵入を防ぐ（一度成功で固定）

            if (!_initialDone)
            {
                SnapPending();
                if (!_playerPlaced) _playerPlaced = PlacePlayers();
                if (!_partyPlaced)  _partyPlaced  = PlaceParty();
                RescueBuriedBodies();
                ContainStrayAgents();
                if (Time.time - _startTime > maxConformSeconds)
                {
                    if (_safetyFloor != null) Destroy(_safetyFloor);
                    _initialDone = true;
                    Debug.Log($"[CombinedTerrainConformer] 初期整合完了: padTop={_padTopY:F1}, 残 pending={_pending.Count}, playerPlaced={_playerPlaced}, rescued={_rescuedCount} → 維持フェーズへ");
                }
                return;
            }

            _maintTimer -= Time.deltaTime;
            if (_maintTimer <= 0f)
            {
                _maintTimer = maintenanceInterval;
                RescueBuriedBodies();
                StuckWatchdog();
                ContainStrayAgents();
            }
        }

        // ── 平坦台座（丸角プラトー） ───────────────────────────────
        private bool TryBuildBasecampPad()
        {
            // 拠点は BasecampBuilder が手続き的に組むため、台座は spawn 中心の対称な正方形にする
            // （旧来は散在 Cube の座標から footprint を取っていたため非対称＝散らかった印象だった）。
            Vector2 min = probeXZ - Vector2.one * campHalfExtent;
            Vector2 max = probeXZ + Vector2.one * campHalfExtent;

            if (!SampleGridHiLo(min, max, out float hi, out _)) return false;
            float topY = hi + padClearance;

            float provisional = 25f;
            SampleGridHiLo(min - Vector2.one * provisional, max + Vector2.one * provisional, out _, out float outerLo);
            if (outerLo == float.MaxValue) outerLo = hi - 10f;
            float bottomY = outerLo - 2f;
            float skirt = Mathf.Clamp((topY - bottomY) * padSkirtSlopeFactor, padSkirtMinWidth, 80f);

            // footprint と天面高さを保存（TrySampleGround が footprint 内で解析的に _padTopY を返す）。
            _footMin = min; _footMax = max; _padTopY = topY;

            var mesh = BuildRoundedPlateauMesh(min, max, topY, skirt, bottomY, padCornerRadius, Mathf.Max(1, padCornerSegments));

            _pad = new GameObject(PadName);
            var mf = _pad.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = _pad.AddComponent<MeshRenderer>();
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            if (sh != null)
            {
                var mat = new Material(sh);
                // 旧 (0.42,0.37,0.30) は強い日射 + ACES + 露出補正で天面が白飛びし、台座が
                // 真っ白なスラブに見えていた。締まった土色(packed earth)へ下げて白飛びを抑え、
                // 木製ゲート/ショップ(0.5-0.6 系)の下に馴染む基地の地面として読ませる。
                mat.SetColor("_BaseColor", new Color(0.32f, 0.27f, 0.20f, 1f));
                mat.SetFloat("_Smoothness", 0.0f); // フラットなマット土（光沢で白飛びさせない）
                mat.SetFloat("_Cull", 0f); // 両面
                mr.sharedMaterial = mat;
            }
            var mc = _pad.AddComponent<MeshCollider>(); mc.sharedMesh = mesh;

            Physics.SyncTransforms(); // autoSyncTransforms=false 環境のため明示同期
            _padBuilt = true;
            return true;
        }

        private bool SampleGridHiLo(Vector2 min, Vector2 max, out float hi, out float lo)
        {
            int n = Mathf.Max(2, padSampleGrid);
            int hits = 0, total = 0;
            hi = float.MinValue; lo = float.MaxValue;
            for (int ix = 0; ix < n; ix++)
                for (int iz = 0; iz < n; iz++)
                {
                    float x = Mathf.Lerp(min.x, max.x, ix / (float)(n - 1));
                    float z = Mathf.Lerp(min.y, max.y, iz / (float)(n - 1));
                    total++;
                    if (!TrySampleGround(x, z, out float gy)) continue;
                    hits++;
                    if (gy > hi) hi = gy;
                    if (gy < lo) lo = gy;
                }
            return total > 0 && hits / (float)total >= padReadyFraction;
        }

        /// <summary>
        /// 平坦な天面＋角を丸めた傾斜スカートを持つプラトーメッシュ。
        /// スカートは「角丸矩形の外側オフセット」（各コーナー中心から半径 r+skirt へ等距離オフセット）で作るため、
        /// 外周の各点が稜線に対して常に水平 skirt だけ移動し、垂直落差は一定 → 勾配が周囲で完全に均一になる。
        /// （中心からの放射オフセットだと矩形 footprint では辺・角ごとに勾配がばらつくため、それを是正したもの）
        /// </summary>
        private static Mesh BuildRoundedPlateauMesh(Vector2 min, Vector2 max, float topY, float skirt, float bottomY, float radius, int seg)
        {
            float cx = (min.x + max.x) * 0.5f, cz = (min.y + max.y) * 0.5f;
            float r = Mathf.Min(radius, (max.x - min.x) * 0.5f, (max.y - min.y) * 0.5f);
            r = Mathf.Max(0.01f, r);

            // (centerX, centerZ, startDeg) を 4 隅ぶん。θ:0=+X,90=+Z
            var corners = new (float ccx, float ccz, float a0)[]
            {
                (max.x - r, max.y - r, 0f),   // NE
                (min.x + r, max.y - r, 90f),  // NW
                (min.x + r, min.y + r, 180f), // SW
                (max.x - r, min.y + r, 270f), // SE
            };

            // 天面の丸角矩形ループと、各頂点に対応する外周リング点を同時に構築。
            // 外周点 = コーナー中心 + 同じ方向 × (r+skirt) なので、稜線から見て常に skirt だけ外へ（等距離オフセット）。
            var loop = new List<Vector3>();
            var ring = new List<Vector3>();
            foreach (var c in corners)
                for (int s = 0; s <= seg; s++)
                {
                    float rad = (c.a0 + 90f * s / seg) * Mathf.Deg2Rad;
                    float dx = Mathf.Cos(rad), dz = Mathf.Sin(rad);
                    loop.Add(new Vector3(c.ccx + dx * r, topY, c.ccz + dz * r));
                    ring.Add(new Vector3(c.ccx + dx * (r + skirt), bottomY, c.ccz + dz * (r + skirt)));
                }

            int m = loop.Count;
            var verts = new List<Vector3>(m * 2 + 1);
            var tris = new List<int>(m * 9);

            // 天面（中心からのファン）
            // 巻き順は (center, b, a) = CW（上から見て時計回り）にして法線を +Y（上向き）にする。
            // ここを (center, a, b) にすると法線が -Y（下向き）になり、queriesHitBackfaces=false 環境で
            // ① 上からの Raycast が天面を貫通（接地判定が抜ける）② 上から落ちてくる Rigidbody がすり抜けて
            // 下の地形へ落下する。基地台座を「上から乗れる床」にするため必ず上向きにすること。
            int centerIdx = verts.Count;
            verts.Add(new Vector3(cx, topY, cz));
            int topStart = verts.Count;
            for (int i = 0; i < m; i++) verts.Add(loop[i]);
            for (int i = 0; i < m; i++)
            {
                int a = topStart + i, b = topStart + (i + 1) % m;
                tris.Add(centerIdx); tris.Add(b); tris.Add(a);
            }

            // 外周リング（等距離オフセット・底面高さ）
            int outStart = verts.Count;
            for (int i = 0; i < m; i++) verts.Add(ring[i]);
            // スカート（天面外周→外周リング）。天面と整合する巻き順で法線を外向きにする。
            for (int i = 0; i < m; i++)
            {
                int i2 = (i + 1) % m;
                int t0 = topStart + i, t1 = topStart + i2;
                int o0 = outStart + i, o1 = outStart + i2;
                tris.Add(t0); tris.Add(o1); tris.Add(o0);
                tris.Add(t0); tris.Add(t1); tris.Add(o1);
            }

            var mesh = new Mesh { name = "BasecampPadPlateau", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 登攀コース（Mountain の Grappable 岩 + ClimbingPoints）を、基地へ平坦化する代わりに
        /// 「基地(原点) → 手続き山頂(ColliderBaker.GlobalMaxPos)」のルート沿いへ標高順に再配置する。
        /// ゾーン番号(Zone1〜6)をルート上の割合(climbStartFraction〜climbTopFraction、Zone6_Summit は山頂)へ
        /// 写像し、各オブジェクトを地表に接地させる。山頂チャンクが焼けて GlobalMaxPos が確定するまで待つ。
        /// 一度成功したら固定（その後プレイヤーが登っても再配置しない）。
        /// </summary>
        /// <summary>
        /// 実山高（基地 pad 天面 → 観測山頂）を MountainProfile へ公開する。GlobalMaxY は地形ベイク
        /// 進行に伴い単調増加で真の山頂へ収束するため、毎フレーム最新値を渡して追従させる。
        /// これにより 天候悪化・凍傷・高山病・高度計 が実山高(~50→460m)に連動する
        /// （絶対メートル固定 1600m/2000m が実山高に永遠に届かず死蔵していた問題を解消）。
        /// </summary>
        private void PublishAltitudeProfile()
        {
            float summitY = _bootstrap.ColliderBaker.GlobalMaxY;
            if (summitY < _padTopY + 1f) return; // まだ基地より高い山頂を観測できていない
            MountainProfile.Publish(_padTopY, summitY);

            // 登攀ルートの XZ（拠点 → 観測山頂）も公開する。DistributeClimbCourse と同じ起点・終点を
            // 使い、EnemySpawner 等が「ゾーン(標高ステージ)に対応した位置」を実山に沿って解決できる。
            // 山頂チャンクが原点近傍の低ピークの間は magnitude が小さいので確定させない。
            Vector3 summit = _bootstrap.ColliderBaker.GlobalMaxPos;
            Vector2 summitXZ = new Vector2(summit.x, summit.z);
            if (summitXZ.magnitude >= 50f)
                MountainProfile.PublishRoute(probeXZ, summitXZ);
        }

        /// <summary>
        /// 双子像（TwinStatueRelic）のペアを、鎖が張らない近距離へ寄せて co-location する。
        /// DistributeClimbCourse でゾーン別に独立配置されると authored 像と runtime クローンが
        /// 鎖長を遥かに超えて離れ、開幕の張力ダメージで両者が自壊するため、配置確定直後に補正する。
        /// authored 側（"(Clone)" を含まない）をアンカーにし、もう片方を隣へ接地させる。
        /// </summary>
        private void CoLocateTwinStatues()
        {
            var twins = FindObjectsByType<TwinStatueRelic>(FindObjectsSortMode.None);
            if (twins.Length < 2) return;

            var handled = new HashSet<TwinStatueRelic>();
            foreach (var a in twins)
            {
                if (a == null || handled.Contains(a)) continue;
                var b = a.Partner;
                if (b == null || handled.Contains(b)) continue;
                handled.Add(a);
                handled.Add(b);

                // authored(Zone付き=ルートの正規位置に置かれた側)をアンカーにする。
                bool aIsClone = a.name.Contains("(Clone)");
                bool bIsClone = b.name.Contains("(Clone)");
                TwinStatueRelic anchor   = (aIsClone && !bIsClone) ? b : a;
                TwinStatueRelic follower = anchor == a ? b : a;

                // 鎖が張らない距離（自然長の 1/3 程度）でアンカー横へ。地表へ接地・静止させる。
                float offset = Mathf.Max(0.8f, anchor.ChainLength * 0.33f);
                Vector3 p = anchor.transform.position + new Vector3(offset, 0f, 0f);
                if (TrySampleGround(p.x, p.z, out float gy))
                {
                    follower.transform.position = new Vector3(p.x, gy + climbLift, p.z);
                    follower.SettleOntoGround(gy); // 接地＋静止（双子像は RestsKinematic=true）で速度0化も行う
                }
                else
                {
                    follower.transform.position = p;
                    var rb = follower.GetComponent<Rigidbody>();
                    if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                }

                Debug.Log($"[CombinedTerrainConformer] 双子像 co-location: {follower.name} → {anchor.name} の隣 (offset={offset:F1}m)");
            }
        }

        private void DistributeClimbCourse()
        {
            // distributeClimbCourse / distributeRelics / distributeShrines のいずれかが ON なら走る（_climbObjects に集めた対象を配る）。
            if (_climbDistributed || !(distributeClimbCourse || distributeRelics || distributeShrines)) return;
            if (_bootstrap == null || _bootstrap.ColliderBaker == null) return;
            var baker = _bootstrap.ColliderBaker;
            if (baker.BakedCount < climbDistributeMinChunks) return;

            // 遠い山頂チャンクは後から焼けるため、GlobalMaxY が伸び止まる（＝山頂確定）まで待つ。
            // これをしないと近傍の低いピークを「山頂」と誤認し、コースが山の下部にしか展開されない。
            if (baker.GlobalMaxY > _climbMaxY + 1f)
            {
                _climbMaxY = baker.GlobalMaxY;
                _climbStableTime = 0f;
                return;
            }
            _climbStableTime += Time.deltaTime;
            if (_climbStableTime < climbSummitStableSeconds) return;

            Vector3 summit = baker.GlobalMaxPos;
            Vector2 summitXZ = new Vector2(summit.x, summit.z);
            if (summitXZ.magnitude < 50f) return; // まだ原点近傍の低ピークしか観測できていない

            // ルートは「拠点(probeXZ) → 観測山頂(summitXZ)」を基準にする。以前は世界原点(0,0)起点で
            // lerp していたため、拠点を海際へ大きく動かすと登攀コース/遺物/祠が拠点から外れてしまった。
            // 拠点起点にすることで、どの拠点位置でも『麓→山頂』の一直線にコースが並ぶ。
            Vector2 routeBase = probeXZ;
            Vector2 toSummit = summitXZ - routeBase;
            Vector2 dir = toSummit.sqrMagnitude > 1e-4f ? toSummit.normalized : Vector2.up;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            int moved = 0, movedShrines = 0;

            // ジップラインのステーションが立つチェックポイント位置（左右スキャッタ込み）。岩/足場がここに
            // 被るとステーション（支柱・足場）と重なるため、後で半径内のオブジェクトを外側へ退避させる。
            var cpPositions = (placeZiplineCheckpoints && checkpointClearRadius > 0f)
                ? EnsureZiplineCheckpointsChosen(summitXZ)
                : new System.Collections.Generic.List<Vector2>();

            foreach (var t in _climbObjects)
            {
                if (t == null) continue;
                int zone = ParseZone(t.name); // 1..6（不明は 1）
                bool isSummit = zone >= 6;
                bool isShrine = t.name.StartsWith("Shrine", System.StringComparison.Ordinal);
                float f = isSummit
                    ? 1.0f
                    : Mathf.Lerp(climbStartFraction, climbTopFraction, (zone - 1) / 5f);

                // 名前ハッシュで決定的に横/前後へばらつかせ、同ゾーン内の重なりを避ける。
                // 山頂は密集を避けて強く絞り、祠は復活拠点なのでルート線へ寄せて（半減）発見性を確保する。
                int h = StableHash(t.name);
                float latScale, alongScale;
                if (isSummit)      { latScale = 0.35f; alongScale = 0.25f; }
                else if (isShrine) { latScale = 0.5f;  alongScale = 0.5f;  }
                else               { latScale = 1f;    alongScale = 1f;    }
                float lat   = (((h        & 0xFF) / 255f) * 2f - 1f) * climbLateralSpread * latScale;
                float along = ((((h >> 8) & 0xFF) / 255f) * 2f - 1f) * climbAlongSpread   * alongScale;

                Vector2 xz = Vector2.Lerp(routeBase, summitXZ, f) + perp * lat + dir * along;

                // ジップラインのステーション設置点（チェックポイント位置）に被るオブジェクトは外側へ退避させる。
                for (int ci = 0; ci < cpPositions.Count; ci++)
                {
                    Vector2 cpXZ = cpPositions[ci];
                    Vector2 away = xz - cpXZ;
                    float d = away.magnitude;
                    if (d >= checkpointClearRadius) continue;
                    Vector2 outDir = d > 0.01f ? away / d : perp; // 位置と一致する縮退時は横へ逃がす
                    xz = cpXZ + outDir * (checkpointClearRadius + 1.5f);
                }

                if (!TrySampleGround(xz.x, xz.y, out float gy))
                    gy = Mathf.Lerp(summit.y * 0.12f, summit.y, f); // 念のためのルート標高補間
                t.position = new Vector3(xz.x, gy + climbLift, xz.y);
                // 遺物はコライダー底面を地表へ正確に接地し、kinematic で静止させる（落下/タンブリングによる
                // 回収前破壊を防ぐ）。岩棚/足場（非 RelicBase）は従来どおり climbLift 持ち上げで配置。
                if (t.TryGetComponent<RelicBase>(out var relic)) relic.SettleOntoGround(gy);
                moved++;
                if (isShrine) movedShrines++;
            }

            // 双子像ペアの co-location。ゾーン別配置だと authored(Zone付) と runtime(Clone) が
            // 別ゾーンへ撒かれて鎖長(3m)を遥かに超えて離れ、開幕の張力で自壊する。配置確定後に
            // 相方を鎖が張らない距離まで寄せて、双子ギミックが正しく成立するようにする。
            CoLocateTwinStatues();

            _climbDistributed = true;
            Debug.Log($"[CombinedTerrainConformer] 登攀コースを手続き山へ再配置: {moved} 個（うち祠 {movedShrines}）(summit={summit}, dist={summitXZ.magnitude:F0}m, baked={baker.BakedCount})");
        }

        /// <summary>
        /// 基地→山頂ルート沿いに <see cref="Sandbox.World.Zipline.ZiplineCheckpoint"/> を配置する。各点に到達すると
        /// 拠点⇄当該地点のジップラインが開通し、以後は拠点から登りをショートカットできる。登攀コースの再配置が
        /// 確定（＝山頂が安定）してから一度だけ実行する。<see cref="Sandbox.World.Zipline.ZiplineNetwork"/> も
        /// 拠点中心(pad 天面)で初期化する。
        /// </summary>
        private void PlaceZiplineCheckpoints()
        {
            if (_ziplineCheckpointsPlaced || !placeZiplineCheckpoints) return;
            // 登攀コース確定（山頂安定）を待つ。山頂が原点近傍の低ピークの間は置かない。
            if (!_climbDistributed) return;
            if (_bootstrap == null || _bootstrap.ColliderBaker == null) return;

            Vector3 summit = _bootstrap.ColliderBaker.GlobalMaxPos;
            Vector2 summitXZ = new Vector2(summit.x, summit.z);
            if (summitXZ.magnitude < 50f) return;

            // 拠点アンカー（pad 天面中心）でネットワークを初期化。
            var net = Sandbox.World.Zipline.ZiplineNetwork.Ensure(
                new Vector3(probeXZ.x, _padTopY, probeXZ.y), _padTopY);

            // チェックポイント位置（地形を貫通しない探索済み）＋全ライン共通レイズを確定（SetLaneCount/SetSummitDirection/
            // SetUniformRaise は EnsureZiplineCheckpointsChosen 内で実施済み）。
            var positions = EnsureZiplineCheckpointsChosen(summitXZ);
            float[] fractions = ResolveZiplineFractions();
            int total = fractions.Length;
            int placed = 0;

            for (int i = 0; i < total; i++)
            {
                Vector2 xz = positions[i];
                float f = Mathf.Clamp01(fractions[i]);
                // 接地が取れない遠方チャンクは、ルート標高補間で代替して必ず配置する（登攀コース再配置と同方針）。
                if (!TrySampleGround(xz.x, xz.y, out float gy))
                    gy = Mathf.Lerp(summit.y * 0.12f, summit.y, f);

                var go = new GameObject($"ZiplineCheckpoint_{i + 1}");
                go.transform.position = new Vector3(xz.x, gy, xz.y);
                var cp = go.AddComponent<Sandbox.World.Zipline.ZiplineCheckpoint>();
                cp.Configure(i, total, Sandbox.World.Zipline.ZiplineNetwork.ColorFor(i), ziplineCheckpointRadius);
                placed++;
            }

            _ziplineCheckpointsPlaced = true;
            Debug.Log($"[CombinedTerrainConformer] ジップライン用チェックポイントを配置: {placed}/{total} 個（各ステージ始端＋左右スキャッタ, base={net.name}）");
        }

        /// <summary>
        /// ジップライン用チェックポイントの最終 XZ 位置を「探索」で確定する（一度だけ）。各ステージ(ゾーン)で
        /// 左右スキャッタ＋前後オフセットの候補を走査し、<see cref="Sandbox.World.Zipline.ZiplineNetwork.EstimateRaiseFor"/>
        /// で『拠点⇄その地点のケーブルが地形を貫通しないために必要な持ち上げ量』が最小になる位置を選ぶ。
        /// さらに全ステージの必要レイズの最大値を共通レイズとしてネットワークに設定する。これにより
        ///   ・全ラインの延長ポール長が同一（共通レイズ）になる
        ///   ・どのラインも地形を貫通しない（共通レイズ ≧ 各ラインの必要量）
        ///   ・各ステージ 1 本（ゾーン毎に 1 点）
        /// を同時に満たす。<see cref="PlaceZiplineCheckpoints"/> と <see cref="DistributeClimbCourse"/> の双方で同結果を使う。
        /// </summary>
        private System.Collections.Generic.List<Vector2> EnsureZiplineCheckpointsChosen(Vector2 summitXZ)
        {
            if (_ziplineChosen) return _ziplineXZ;
            if (!placeZiplineCheckpoints) { _ziplineChosen = true; return _ziplineXZ; }

            var net = Sandbox.World.Zipline.ZiplineNetwork.Ensure(
                new Vector3(probeXZ.x, _padTopY, probeXZ.y), _padTopY);

            float[] fr = ResolveZiplineFractions();
            int total = fr.Length;
            net.SetLaneCount(total);
            // 4隅割り当て順を確定させるため、探索より前に山頂方向を設定する（EstimateRaiseFor が同じコーナーを使う）。
            net.SetSummitDirection(summitXZ - probeXZ);

            Vector2 baseXZ = probeXZ;
            Vector2 d = summitXZ - baseXZ;
            Vector2 dir = d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.up;
            Vector2 perp = new Vector2(-dir.y, dir.x);

            // 散らし候補：左右オフセット倍率（直線密集を避けつつ、山を回避できる位置を探す）。
            float[] lateralMul = { 1f, -1f, 0.6f, -0.6f, 1.4f, -1.4f, 0.25f, -0.25f, 0f };
            // ステージ帯内の前後オフセット（ルート割合）。
            float[] alongOff = { 0f, 0.05f, -0.04f, 0.09f, -0.07f, 0.13f };

            _ziplineXZ.Clear();
            float maxRaise = 0f;

            for (int i = 0; i < total; i++)
            {
                // 既定（散らしパターン）を初期候補に。探索で貫通しない位置が見つかればそれを優先。
                float[] pattern = { 1f, -0.7f, 0.65f, -1f, 0.85f, -0.55f };
                Vector2 bestXZ = Vector2.Lerp(baseXZ, summitXZ, Mathf.Clamp01(fr[i]))
                                 + perp * (ziplineCheckpointLateralSpread * pattern[i % pattern.Length]);
                float bestRaise = float.MaxValue;

                for (int li = 0; li < lateralMul.Length && bestRaise > 0.05f; li++)
                {
                    for (int ai = 0; ai < alongOff.Length && bestRaise > 0.05f; ai++)
                    {
                        float f = Mathf.Clamp01(fr[i] + alongOff[ai]);
                        float lat = ziplineCheckpointLateralSpread * lateralMul[li];
                        Vector2 xz = Vector2.Lerp(baseXZ, summitXZ, f) + perp * lat;

                        // 接地が取れない候補はクリアランス判定不能なので採らない。
                        if (!TrySampleGround(xz.x, xz.y, out float gy)) continue;

                        float raise = net.EstimateRaiseFor(i, new Vector3(xz.x, gy, xz.y));
                        if (raise < bestRaise)
                        {
                            bestRaise = raise;
                            bestXZ = xz;
                        }
                    }
                }

                if (bestRaise == float.MaxValue) bestRaise = 0f; // 全候補で接地不能 → 後段の標高補間配置に委ねる
                _ziplineXZ.Add(bestXZ);
                maxRaise = Mathf.Max(maxRaise, bestRaise);
            }

            _ziplineUniformRaise = maxRaise;
            net.SetUniformRaise(maxRaise); // 全ライン共通の持ち上げ量（＝延長ポール長を統一）。
            _ziplineChosen = true;
            Debug.Log($"[CombinedTerrainConformer] ジップラインCP選定: {_ziplineXZ.Count} 点 / 共通レイズ {maxRaise:F1}m（全ライン延長ポール統一）");
            return _ziplineXZ;
        }

        /// <summary>ゾーン番号(1..6)を、登攀コースと同じ写像でルート上の『始端』割合(0=基地..1=山頂)へ変換する。</summary>
        private float ZoneStartFraction(int zone)
        {
            if (zone >= 6) return 1f;                // Zone6_Summit は山頂
            if (zone <= 1) return climbStartFraction; // Zone1 は最下段（基地隣接）
            return Mathf.Lerp(climbStartFraction, climbTopFraction, (zone - 1) / 5f);
        }

        /// <summary>
        /// ジップライン用チェックポイントのルート割合を解決する。<see cref="ziplineCheckpointZones"/> が指定されていれば
        /// 各ゾーンの始端割合（登攀コースの配置と一致）を返し、無ければ <see cref="ziplineCheckpointFractions"/> を使う。
        /// </summary>
        private float[] ResolveZiplineFractions()
        {
            if (ziplineCheckpointZones != null && ziplineCheckpointZones.Length > 0)
            {
                var arr = new float[ziplineCheckpointZones.Length];
                for (int i = 0; i < arr.Length; i++)
                    arr[i] = Mathf.Clamp01(ZoneStartFraction(ziplineCheckpointZones[i]));
                return arr;
            }
            return ziplineCheckpointFractions != null && ziplineCheckpointFractions.Length > 0
                ? ziplineCheckpointFractions
                : new[] { 0.34f, 0.58f, 0.82f };
        }

        private static int ParseZone(string objName)
        {
            int idx = objName.IndexOf("Zone", System.StringComparison.Ordinal);
            if (idx >= 0 && idx + 4 < objName.Length && char.IsDigit(objName[idx + 4]))
                return objName[idx + 4] - '0';
            return 1; // Ramp 等ゾーン無しは最下段へ
        }

        private static int StableHash(string s)
        {
            int h = 17;
            foreach (var c in s) h = unchecked(h * 31 + c);
            return h & 0x7FFFFFFF;
        }

        /// <summary>
        /// 地形の標高バンド（草地/岩肌/冠雪線）を、観測された実山高へ比例追従させる。
        /// バンドは元々「山頂 ~485m」前提の固定メートルで tuning されていたため、島リスケールで
        /// 山頂が高くなると中腹〜上部がほぼ雪・岩に潰れ「白い山」に見えていた。MountainProfile が
        /// 実測山頂を得たら（IsReady）、海面0→山頂の割合で grass/rock/snow 線を置き直し、
        /// どの手続き山高でも tiered な配色（緑の裾野→灰の岩→冠雪）を保つ。一度成功で固定。
        /// </summary>
        private void TuneElevationBands()
        {
            if (!MountainProfile.IsReady) return; // 実測山頂が確定するまで待つ（既定の絶対バンドのまま）
            float summitY = MountainProfile.SummitY;
            // 遠い高所チャンクは後着でベイクされ GlobalMaxY＝山頂が徐々に伸びる。最初の IsReady で
            // 一度だけ固定すると、暫定の低い山頂(例:561m)で雪/岩線が決まり、最終山頂(例:867m)では
            // 雪が下がりすぎる。山頂が一定以上伸びるたびに再適用して tiered 配色を最終山高へ収束させる。
            if (_lastBandSummitY > 0f && summitY - _lastBandSummitY < 15f) return;
            var atmos = GetComponent<AtmosphericProfileController>();
            if (atmos == null) atmos = FindFirstObjectByType<AtmosphericProfileController>();
            if (atmos == null) return;
            // バンドの基準は「海面(0)→山頂」。裾野の緑/岩/雪が海抜割合で並ぶようにする。
            atmos.ApplyBandsForElevation(0f, summitY);
            _lastBandSummitY = summitY;
            Debug.Log($"[CombinedTerrainConformer] 標高バンドを実山高へ追従: summitY={summitY:F0}m");
        }

        private void TuneFog()
        {
            if (!tuneFog || _fogTuned) return;
            var atmos = GetComponent<AtmosphericProfileController>();
            if (atmos == null) atmos = FindFirstObjectByType<AtmosphericProfileController>();
            if (atmos == null) return; // まだ未付与なら次フレーム以降に再試行
            atmos.OverrideFogAltitudeAware(fogStartOverride, fogEndOverride, fogStartHigh, fogEndHigh,
                                           fogLowOverride, fogHighOverride, fogAltitudeForFullClear);
            _fogTuned = true;
        }

        /// <summary>
        /// 拠点直下（坂下）に見える茶色い板を除去する。Mountain 直下には
        ///   ・旧テスト用のプレーンなランプ "Ramp_*"（Untagged・ゾーン無し）
        ///   ・登攀コース最下段の掴める足場 "Zone1_Rock1/2/3"（Grappable）
        /// があり、いずれも DistributeClimbCourse でルート起点(climbStartFraction)＝拠点直下へ撒かれるため、
        /// 「坂を降りた先に茶色い長方形の板がある」状態の原因になっていた。ユーザー要望によりこの最下段の
        /// 板（ランプ＋Zone1 足場）を一掃する。Zone2〜Zone6_Summit の上段コース／山頂ゴールには触れない
        /// （登攀は Zone2 以上＋ClimbingPoints＋手続き Grappable 岩で継続）。CollectPending より前に消して
        /// _climbObjects へ拾われないようにする（シーンアセットは編集しない非破壊）。
        /// </summary>
        private void RemoveLegacyClimbPlaceholders()
        {
            if (_legacyClimbPlaceholdersRemoved) return;
            _legacyClimbPlaceholdersRemoved = true;

            var mountain = GameObject.Find("Mountain");
            if (mountain == null) return;

            var doomed = new List<Transform>();
            foreach (Transform c in mountain.transform)
            {
                if (c == null) continue;
                // 最下段の板のみを対象。Zone2〜Zone6（山頂含む）の上段コースには絶対に触れない。
                bool isRamp  = c.name.StartsWith("Ramp",  System.StringComparison.Ordinal);
                bool isZone1 = c.name.StartsWith("Zone1", System.StringComparison.Ordinal);
                if (isRamp || isZone1) doomed.Add(c);
            }

            foreach (var d in doomed)
                if (d != null) Destroy(d.gameObject);

            if (doomed.Count > 0)
                Debug.Log($"[CombinedTerrainConformer] 拠点直下の茶色い板を除去: {doomed.Count} 個（Ramp_* + Zone1_Rock*。Zone2〜山頂コースは温存）");
        }

        /// <summary>
        /// Mountain コンテナの Grappable 岩（Zone*_Rock / Zone6_Summit 等）のマテリアルをスタイライズド向けに整える。
        /// これらは近白色(0.9)＋Smoothness 0.5 の半光沢で、強い日射 + ACES + 露出補正により基地前景で真っ白に
        /// 白飛びしていた。フラットなマット(Smoothness 低・Metallic 0)へ統一し、明るすぎる近白色アルベドは
        /// 上限 0.80 に丸めて白飛びを抑える（雪/岩の質感は残しつつ前景の白スラブ感を解消）。
        /// runtime インスタンス material を編集するためアセットや他シーンには影響しない。
        /// </summary>
        private void SoftenMountainMaterials()
        {
            if (_mountainSoftened) return;
            var mountain = GameObject.Find("Mountain");
            if (mountain == null) return;
            // 0.80 では強い日射 + ACES + 露出で依然として白飛びした。光石(light stone)程度の 0.62 まで落とすと
            // トーンマップ後も白飛びせず読める明るさになる（雪/氷はやや明るめのライトグレーになるが許容）。
            const float maxBrightness = 0.62f;
            foreach (var r in mountain.GetComponentsInChildren<MeshRenderer>(true))
            {
                var m = r.material; // runtime インスタンス（共有アセットを汚さない）
                if (m == null) continue;
                if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
                if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", 0f);
                if (m.HasProperty("_BaseColor"))
                {
                    var c = m.GetColor("_BaseColor");
                    float peak = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    if (peak > maxBrightness)
                    {
                        float k = maxBrightness / peak;
                        m.SetColor("_BaseColor", new Color(c.r * k, c.g * k, c.b * k, c.a));
                    }
                }
            }
            _mountainSoftened = true;
        }

        /// <summary>
        /// 高度連動フォグの基準カメラをプレイヤー追従カメラへ差し替える。Camera.main は y≈2 の残置 MainCamera を
        /// 指すため、そのままだと登坂してもフォグ高度が変わらない。プレイヤー(ExplorerController)配下のカメラを
        /// 参照させ、昇るほど距離・色が連続変化するようにする（一度成功したら固定）。
        /// </summary>
        private void LinkFogCamera()
        {
            if (_fogCamLinked || !tuneFog) return;
            var atmos = GetComponent<AtmosphericProfileController>();
            if (atmos == null) atmos = FindFirstObjectByType<AtmosphericProfileController>();
            if (atmos == null) return;
            var players = FindObjectsByType<ExplorerController>(FindObjectsSortMode.None);
            if (players == null || players.Length == 0) return;
            var cam = players[0].GetComponentInChildren<Camera>();
            if (cam == null) return;
            atmos.SetFogReferenceCamera(cam);
            _fogCamLinked = true;
            Debug.Log($"[CombinedTerrainConformer] 高度連動フォグの参照カメラ = {cam.name}（プレイヤー追従）");
        }

        /// <summary>
        /// 実行時の二重カメラ描画を解消する。OfflineTest 由来の残置 MainCamera（y≈2・地中）が
        /// プレイヤー追従カメラと同 depth で同じ画面に重ね描きされるため、残置側（MainCamera タグ・追従カメラ以外）を
        /// 無効化し、追従カメラを唯一の MainCamera にする。UI は ScreenSpaceOverlay / WorldSpace(worldCam=null) で
        /// カメラ非依存のため安全。一度成功したら固定。
        /// </summary>
        private void ResolveDuplicateCameras()
        {
            if (_camerasResolved || !resolveDuplicateCameras) return;
            var players = FindObjectsByType<ExplorerController>(FindObjectsSortMode.None);
            if (players == null || players.Length == 0) return;
            var rigCam = players[0].GetComponentInChildren<Camera>();
            if (rigCam == null) return;

            var cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            int disabled = 0;
            foreach (var c in cams)
            {
                if (c == rigCam) continue;
                if (c.transform.IsChildOf(players[0].transform)) continue; // プレイヤー配下サブカメラは温存
                if (!c.CompareTag("MainCamera")) continue;                 // 残置 MainCamera のみ対象
                c.enabled = false;
                c.tag = "Untagged";
                disabled++;
            }

            rigCam.tag = "MainCamera"; // Camera.main を追従カメラへ解決させる
            if (gameplayCamFarClip > rigCam.farClipPlane) rigCam.farClipPlane = gameplayCamFarClip;

            // ── ポストプロセス有効化 ───────────────────────────────────
            // VolumeProfileSetup が Global Volume（Bloom/Vignette/ACES Tonemapping/ColorAdjustments）を生成しても、
            // プレイヤー追従カメラ（PlayerPrefab/CameraRig）に UniversalAdditionalCameraData が無く
            // renderPostProcessing=false のままだと一切描画されない。ここで明示的に有効化し、スタイライズドな
            // 発光・色調補正を画面へ反映する。SMAA はローポリのエッジを安価にクリーンに保つ（TAA のような滲み無し）。
            if (enablePostProcessing)
            {
                var urpData = rigCam.GetUniversalAdditionalCameraData();
                if (urpData != null)
                {
                    urpData.renderPostProcessing = true;
                    urpData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    urpData.antialiasingQuality = AntialiasingQuality.High;
                    urpData.dithering = true; // 空/フォグのグラデーションバンディングを抑える
                }
            }

            _camerasResolved = true;
            Debug.Log($"[CombinedTerrainConformer] 重複カメラ整理: 残置 {disabled} 個を無効化、{rigCam.name} を MainCamera 化（far={rigCam.farClipPlane:F0}）");
        }

        // ── 配置 / 救出 ───────────────────────────────────────────
        private void CreateSafetyFloor()
        {
            _safetyFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _safetyFloor.name = "TempSafetyFloor";
            var mr = _safetyFloor.GetComponent<MeshRenderer>();
            if (mr != null) Destroy(mr);
            _safetyFloor.transform.position = new Vector3(probeXZ.x, -2f, probeXZ.y);
            _safetyFloor.transform.localScale = new Vector3(800f, 4f, 800f);
        }

        private void CollectPending()
        {
            _pending.Clear();
            _climbObjects.Clear();
            foreach (var name in containerNames)
            {
                var container = GameObject.Find(name);
                if (container == null) continue;
                // 登攀コース（Mountain 岩 + ClimbingPoints）と、任意で遺物(Relics)・復活の祠(ReviveShrines)を
                // 基地へ平坦化せず、後で手続き山へ標高順に再配置する（_climbObjects へ）。
                // Hazards は Awake/Start で位置をキャッシュするため runtime 移動不可 ＝ _pending 側に残し接地のみ。
                bool isClimb = (distributeClimbCourse && (name == "Mountain" || name == "ClimbingPoints"))
                            || (distributeRelics && name == "Relics")
                            || (distributeShrines && name == "ReviveShrines");
                var dest = isClimb ? _climbObjects : _pending;
                foreach (Transform c in container.transform)
                {
                    dest.Add(c);
                    // _climbObjects 側の遺物は DistributeClimbCourse の SettleOntoGround で接地配置するため、
                    // 自動接地（RelicBase.AutoSettleRoutine）を抑止して二重配置・落下を防ぐ。
                    // _pending 側（distributeRelics=false 等）は自動接地に委ねる。
                    if (dest == _climbObjects && c.TryGetComponent<RelicBase>(out var relic))
                        relic.ExternalManaged();
                }
            }
        }

        private void SnapPending()
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var c = _pending[i];
                if (c == null) { _pending.RemoveAt(i); continue; }
                var pos = c.position;
                if (!TrySampleGround(pos.x, pos.z, out float gy)) continue;
                pos.y = gy + objectLift;
                c.position = pos;
                _pending.RemoveAt(i);
            }
        }

        private bool PlacePlayers()
        {
            var players = FindObjectsByType<ExplorerController>(FindObjectsSortMode.None);
            if (players == null || players.Length == 0) return false;
            int placed = 0;
            foreach (var pc in players)
            {
                // 出現位置を NGO 初期スポーン地点(≈原点・中腹)ではなく拠点へ。既に置いた他プレイヤー/NPC や
                // 中央の焚き火と重ならない、拠点まわりの空きスロットを割り当てる（非重複スポーン）。
                if (!AllocateSpawnPoint(out var pos)) continue;
                var rb = pc.GetComponent<Rigidbody>();
                if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                pc.transform.position = pos;
                placed++;
            }
            if (placed > 0) Physics.SyncTransforms();
            return placed > 0;
        }

        /// <summary>パーティ NPC を拠点(probeXZ)まわりの小さなリングへ配置し、プレイヤーと一緒に麓から出撃させる。</summary>
        private bool PlaceParty()
        {
            var npcs = FindObjectsByType<NPCController>(FindObjectsSortMode.None);
            if (npcs == null || npcs.Length == 0) return false;
            int placed = 0;
            for (int i = 0; i < npcs.Length; i++)
            {
                var n = npcs[i];
                if (n == null) continue;
                // プレイヤー含む既配置エージェントと重ならない拠点まわりの空きスロットへ（非重複スポーン）。
                if (!AllocateSpawnPoint(out var pos)) continue;
                var rb = n.GetComponent<Rigidbody>();
                if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                n.transform.position = pos;
                TryReanchorAgentOnce(n.gameObject, pos);  // AI のホーム/アンカーを拠点へ
                placed++;
            }
            if (placed > 0) Physics.SyncTransforms();
            return placed >= npcs.Length;
        }

        /// <summary>
        /// 拠点まわりの「同心リング上の空きスロット」から、既配置エージェント(_spawnOccupied)と
        /// partySpawnSpacing 以上離れ、かつ接地している最初の地点を割り当てる（重複スポーン防止）。
        /// プレイヤーと NPC で _spawnOccupied を共有するため、両者・全員が互いに重ならない。
        /// 中央(スロット無し)は焚き火等の中心物を避けてリング1から始める。
        /// </summary>
        private bool AllocateSpawnPoint(out Vector3 dest)
        {
            float minSqr = partySpawnSpacing * partySpawnSpacing * 0.81f; // 0.9×間隔を許容下限に
            for (int slot = 0; slot < 120; slot++)
            {
                Vector2 xz = probeXZ + RingSlotOffset(slot);
                bool clear = true;
                for (int i = 0; i < _spawnOccupied.Count; i++)
                    if ((_spawnOccupied[i] - xz).sqrMagnitude < minSqr) { clear = false; break; }
                if (!clear) continue;
                if (!TrySampleGround(xz.x, xz.y, out float gy)) continue;
                _spawnOccupied.Add(xz);
                dest = new Vector3(xz.x, gy + playerLift, xz.y);
                return true;
            }
            // 全スロット埋まり/接地不能の保険（通常到達しない）。
            dest = new Vector3(probeXZ.x, _padTopY + playerLift, probeXZ.y);
            return true;
        }

        /// <summary>拠点中心まわりの同心リング上の固定スロット位置（slot 番号→拠点相対 XZ）。総数に依存しないため順序/タイミングに非依存。</summary>
        private Vector2 RingSlotOffset(int slot)
        {
            int s = Mathf.Max(0, slot), ring = 1, cap = 6;
            while (s >= cap) { s -= cap; ring++; cap = 6 * ring; }
            float ang = ((s + 0.5f) / cap) * Mathf.PI * 2f + ring * 0.7f; // リングごとに角度をずらして千鳥に
            float radius = ring * partySpawnSpacing;
            return new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
        }

        /// <summary>
        /// 海への侵入を防ぐ見えない円筒壁を、山頂(GlobalMaxPos)中心の放射状に設置する。山頂から海側
        /// (拠点方向)へ進んで接地高度が seaBarrierGroundY を下回る地点＝海岸線の半径に壁を立てるため、
        /// 放射状の海岸線（湾の曲がり）に追従して内側(島)に閉じ込める。上端は渚の低地より高く飛び越え不可、
        /// 内陸側では地中に埋まり無害（登坂は壁から内側へ向かうので干渉しない）。一度成功で固定。
        /// </summary>
        private void BuildSeaBarrier()
        {
            if (_seaBarrierBuilt || !seaBarrier) return;
            if (_bootstrap == null || _bootstrap.ColliderBaker == null) return;
            if (_bootstrap.ColliderBaker.BakedCount < climbDistributeMinChunks) return;
            // 山頂(GlobalMaxPos)が安定＝登攀コース確定後に建てる。早期の低ピーク誤認で壁が誤った中心/半径に
            // できるのを防ぐ（DistributeClimbCourse と同じ summit-stable ゲートを共有）。
            if (!_climbDistributed) return;

            Vector3 summit = _bootstrap.ColliderBaker.GlobalMaxPos;
            Vector2 summitXZ = new Vector2(summit.x, summit.z);
            if (summitXZ.magnitude < 50f) return; // まだ原点近傍の低ピークしか観測できていない

            Vector2 seaward = probeXZ - summitXZ; // 山頂→拠点＝海側
            if (seaward.sqrMagnitude < 1f) return;
            seaward.Normalize();

            // 山頂から海側へ前進し、接地が無い（海上）か渚高度を下回った最初の距離＝海岸線半径。
            float radius = -1f;
            for (float d = 100f; d <= 2400f; d += 8f)
            {
                Vector2 p = summitXZ + seaward * d;
                bool hasGround = TrySampleGround(p.x, p.y, out float gy);
                if (!hasGround || gy < seaBarrierGroundY) { radius = d; break; }
            }
            if (radius < 50f) return; // 海岸チャンクがまだ焼けていない → 次フレーム再試行

            // 厚い凸ボックスコライダのリングで壁を構成する。ゼロ厚のコンケーブ trimesh は高速侵入で
            // トンネル抜けする（実測で 12m/s の剛体が貫通）ため、放射方向に厚み(6m)を持つ凸ボックスを
            // 円周に沿って隣接重ねで並べる。凸ボックスは CCD と相性が良く、内外どちらからも確実に弾く。
            int segs = Mathf.Clamp(seaBarrierSegments, 48, 256);
            var go = new GameObject("SeaBarrier");
            go.transform.position = new Vector3(summitXZ.x, 0f, summitXZ.y);

            float midY    = (seaBarrierBottomY + seaBarrierTopY) * 0.5f;
            float height  = Mathf.Max(1f, seaBarrierTopY - seaBarrierBottomY);
            float chord   = 2f * radius * Mathf.Sin(Mathf.PI / segs);
            float width   = chord * 1.4f;   // 隣接ボックスと十分に重ねて継ぎ目の隙間をゼロにする
            const float thickness = 6f;      // 放射方向の厚み（高速侵入でもトンネルしない）

            for (int i = 0; i < segs; i++)
            {
                float a = i / (float)segs * Mathf.PI * 2f;
                Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                var seg = new GameObject("SeaBarrierSeg");
                seg.transform.SetParent(go.transform, false);
                seg.transform.localPosition = radial * radius + Vector3.up * midY;
                seg.transform.localRotation = Quaternion.LookRotation(radial, Vector3.up); // +Z=放射方向(厚み)
                var bc = seg.AddComponent<BoxCollider>();
                bc.size = new Vector3(width, height, thickness);
            }
            _seaBarrierBuilt = true;
            Debug.Log($"[CombinedTerrainConformer] 海バリア（不可視の凸ボックス壁 x{segs}）を設置: center={summitXZ} radius={radius:F0}m");
        }

        // 維持フェーズの重い全シーン走査をまとめてキャッシュ更新する（間隔内は再利用）。
        private void RefreshAgentCaches()
        {
            if (_cachedBodies != null && Time.time < _agentCacheRefresh) return;
            _agentCacheRefresh = Time.time + AgentCacheInterval;
            _cachedBodies  = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            _cachedNpcs    = FindObjectsByType<NPCController>(FindObjectsSortMode.None);
            _cachedEnemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        }

        private void RescueBuriedBodies()
        {
            RefreshAgentCaches();
            var bodies = _cachedBodies;
            for (int i = 0; i < bodies.Length; i++)
            {
                var rb = bodies[i];
                if (rb == null) continue; // キャッシュ後に破棄された Rigidbody
                var t = rb.transform;
                if (t.parent != null) continue;
                if (rb.GetComponent<ExplorerController>() != null) continue;
                var pos = t.position;
                if (!TrySampleGround(pos.x, pos.z, out float gy)) continue;
                if (pos.y >= gy - buriedThreshold) continue;
                pos.y = gy + objectLift;
                rb.position = pos; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
                t.position = pos;
                _rescuedCount++;
                TryReanchorAgentOnce(rb.gameObject, pos);
            }
        }

        /// <summary>
        /// 海・島外へ徘徊脱出（または物理で吹き飛ばされた）NPC/敵を拠点へ引き戻す封じ込め。
        /// 接地面が無い（baked 島の外＝海/虚空）か、渚高度(agentSeaContainY)未満に踏み出した個体を拠点まわりの
        /// 安全地点へ戻し、AI ホーム(_homePos)も拠点へ付け替えて再び海へ向かわないようにする。地表下への落下は
        /// RescueBuriedBodies が担当（接地ありの埋没）。プレイヤーは EnumerateAgents の対象外なので動かさない。
        /// 海岸は島で最も低い land なので、高所をプレイヤー追跡中の敵には誤発火しない（距離リーシュは使わない）。
        /// </summary>
        private void ContainStrayAgents()
        {
            if (!containAgents) return;
            bool any = false;
            foreach (var go in EnumerateAgents())
            {
                var pos = go.transform.position;
                bool hasGround = TrySampleGround(pos.x, pos.z, out float gy);
                bool offIsland = !hasGround;                          // baked 島の外（海上/虚空）
                bool atShore   = hasGround && gy < agentSeaContainY;  // 渚（海面 y≈4）へ踏み出した
                if (!(offIsland || atShore)) continue;

                // 拠点まわりの安全地点へ戻す（PlaceParty と同じ要領。footprint 内は _padTopY が返る）。
                Vector2 xz = probeXZ + Random.insideUnitCircle * 4f;
                if (!TrySampleGround(xz.x, xz.y, out float sy)) { xz = probeXZ; sy = _padTopY; }
                var dest = new Vector3(xz.x, sy + playerLift, xz.y);

                var rb = go.GetComponent<Rigidbody>();
                if (rb != null) { rb.position = dest; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                go.transform.position = dest;

                // ホームを拠点へ付け替え（_anchored を無視して毎回更新）、徘徊先を内陸に固定する。
                var npc = go.GetComponent<NPCController>();
                if (npc != null) npc.ReanchorHome(dest);
                var enemy = go.GetComponent<EnemyController>();
                if (enemy != null) enemy.ReanchorHome(dest);
                any = true;
            }
            if (any) Physics.SyncTransforms();
        }

        /// <summary>長時間静止した NPC/敵を、進行方向優先の到達可能な近傍へ退避させて詰まりを解消する。</summary>
        private void StuckWatchdog()
        {
            foreach (var go in EnumerateAgents())
            {
                int id = go.GetInstanceID();
                var pos = go.transform.position;
                if (!_tracks.TryGetValue(id, out var tr)) { _tracks[id] = new AgentTrack { lastPos = pos, still = 0f }; continue; }

                float moved = Vector3.Distance(pos, tr.lastPos);
                tr.still = moved < stuckMoveEpsilon ? tr.still + maintenanceInterval : 0f;
                tr.lastPos = pos;

                if (tr.still >= stuckEscapeSeconds)
                {
                    Vector3 dest;
                    if (!TryFindEscape(go, out dest))
                    {
                        // 最終手段: 基地中心方向へ少し引き戻す。
                        var c = new Vector3(probeXZ.x, pos.y, probeXZ.y);
                        var t2 = Vector3.Lerp(pos, c, escapeFallbackPull);
                        if (TrySampleGround(t2.x, t2.z, out float gy)) dest = new Vector3(t2.x, gy + playerLift, t2.z);
                        else { _tracks[id] = tr; continue; }
                    }
                    var rb = go.GetComponent<Rigidbody>();
                    if (rb != null) { rb.position = dest; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                    go.transform.position = dest;
                    tr.still = 0f; tr.lastPos = dest;
                }
                _tracks[id] = tr;
            }
        }

        /// <summary>進行方向を優先しつつ、段差が小さく到達可能な近傍地点を探す。</summary>
        private bool TryFindEscape(GameObject agent, out Vector3 dest)
        {
            dest = default;
            Vector3 pos = agent.transform.position;
            Vector3 fwd = agent.transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            fwd.Normalize();
            float[] angles = { 0f, 40f, -40f, 85f, -85f, 135f, -135f, 180f };
            foreach (var a in angles)
            {
                Vector3 dir = Quaternion.Euler(0f, a, 0f) * fwd;
                Vector3 p = pos + dir * escapeNudgeDist;
                if (!TrySampleGround(p.x, p.z, out float gy)) continue;
                if (Mathf.Abs(gy - pos.y) > escapeClimbStep) continue; // 大きな崖は不可
                dest = new Vector3(p.x, gy + playerLift, p.z);
                return true;
            }
            return false;
        }

        private IEnumerable<GameObject> EnumerateAgents()
        {
            RefreshAgentCaches();
            var npcs = _cachedNpcs;
            for (int i = 0; i < npcs.Length; i++)
                if (npcs[i] != null) yield return npcs[i].gameObject;
            var enemies = _cachedEnemies;
            for (int i = 0; i < enemies.Length; i++)
                if (enemies[i] != null) yield return enemies[i].gameObject;
        }

        private void TryReanchorAgentOnce(GameObject go, Vector3 groundPos)
        {
            int id = go.GetInstanceID();
            if (_anchored.Contains(id)) return;
            bool any = false;
            var npc = go.GetComponent<NPCController>();
            if (npc != null) { npc.ReanchorHome(groundPos); any = true; }
            var enemy = go.GetComponent<EnemyController>();
            if (enemy != null) { enemy.ReanchorHome(groundPos); any = true; }
            if (any) _anchored.Add(id);
        }

        /// <summary>
        /// (x,z) 真下の接地面の Y を返す。footprint 内は平坦な台座天面 _padTopY を解析的に返し（巻き順に
        /// 依存せず確実に平坦）、それ以外は地形チャンクへ Raycast する。
        /// </summary>
        private bool TrySampleGround(float x, float z, out float y)
        {
            if (_padBuilt && x >= _footMin.x && x <= _footMax.x && z >= _footMin.y && z <= _footMax.y)
            {
                y = _padTopY;
                return true;
            }
            var origin = new Vector3(x, rayFromAltitude, z);
            var hits = Physics.RaycastAll(origin, Vector3.down, rayFromAltitude * 2f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                var go = hits[i].collider.gameObject;
                if (_safetyFloor != null && go == _safetyFloor) continue;
                if (!go.name.StartsWith("ChunkCollider_")) continue;
                if (hits[i].point.y > best) { best = hits[i].point.y; found = true; }
            }
            y = found ? best : 0f;
            return found;
        }
    }
}
