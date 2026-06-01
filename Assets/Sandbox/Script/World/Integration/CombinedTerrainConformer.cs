using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Sandbox.World.Environment;

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
        [SerializeField] private Vector2 probeXZ = new Vector2(0f, 0f);
        [SerializeField] private float rayFromAltitude = 1200f;
        [SerializeField] private int minBakedChunks = 4;

        [Header("Basecamp Pad（平坦化＋丸角スロープ）")]
        [SerializeField] private string basecampContainerName = "Basecamp";
        [SerializeField] private float footprintMargin = 6f;
        [SerializeField] private float fallbackFootprintHalf = 20f;
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

        [Header("Atmosphere Fog（低高度の白飛び緩和・高度連動）")]
        [SerializeField] private bool tuneFog = true;
        [Tooltip("谷（低高度）のフォグ開始距離。白飛びを抑えるため遠ざける。")]
        [SerializeField] private float fogStartOverride = 700f;  // = low start
        [Tooltip("谷（低高度）のフォグ終了距離。")]
        [SerializeField] private float fogEndOverride = 6000f;    // = low end
        [Tooltip("高所のフォグ開始距離。空気遠近の奥行きを出すため近づける。")]
        [SerializeField] private float fogStartHigh = 500f;
        [Tooltip("高所のフォグ終了距離。")]
        [SerializeField] private float fogEndHigh = 4500f;
        [Tooltip("距離/色の補間が high 側へ到達する基準カメラ高度（山頂相当 ≈485m）。")]
        [SerializeField] private float fogAltitudeForFullClear = 480f;
        [SerializeField] private Color fogLowOverride = new Color(0.56f, 0.69f, 0.86f, 1f);
        [SerializeField] private Color fogHighOverride = new Color(0.50f, 0.66f, 0.92f, 1f);

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

        [SerializeField] private string[] containerNames =
            { "Basecamp", "Mountain", "Relics", "ReviveShrines", "Hazards", "ClimbingPoints", "NetworkPlayerSpawner" };

        private const string PadName = "BasecampPad";

        private SandboxBootstrap _bootstrap;
        private GameObject _safetyFloor;
        private GameObject _pad;
        private Vector2 _footMin, _footMax; // x=ワールドX / y=ワールドZ
        private float _padTopY;
        private readonly List<Transform> _pending = new List<Transform>();
        private readonly List<Transform> _climbObjects = new List<Transform>();
        private bool _climbDistributed;
        private float _climbMaxY = -1f;
        private float _climbStableTime;
        private readonly HashSet<int> _anchored = new HashSet<int>();
        private readonly Dictionary<int, AgentTrack> _tracks = new Dictionary<int, AgentTrack>();
        private bool _padBuilt;
        private bool _fogTuned;
        private bool _mountainSoftened;
        private bool _fogCamLinked;
        private bool _camerasResolved;
        private bool _playerPlaced;
        private bool _initialDone;
        private float _startTime;
        private float _maintTimer;
        private int _rescuedCount;

        public bool Done => _initialDone;

        private struct AgentTrack { public Vector3 lastPos; public float still; }

        private void Awake()
        {
            _bootstrap = GetComponent<SandboxBootstrap>();
            if (_bootstrap == null) _bootstrap = FindFirstObjectByType<SandboxBootstrap>();
            if (_bootstrap == null)
            {
                Debug.LogError("[CombinedTerrainConformer] SandboxBootstrap が見つかりません。地形整合をスキップします。", this);
                enabled = false;
                return;
            }
            CreateSafetyFloor();
        }

        private void Update()
        {
            if (_bootstrap == null || _bootstrap.ColliderBaker == null) return;
            if (_bootstrap.ColliderBaker.BakedCount < minBakedChunks) return;
            if (!_padBuilt && !TrySampleGround(probeXZ.x, probeXZ.y, out _)) return; // 基地中心に地形が出てから

            if (!_padBuilt)
            {
                if (!TryBuildBasecampPad()) return;
                TuneFog();
                CollectPending();
                SoftenMountainMaterials();
                _startTime = Time.time;
            }

            LinkFogCamera();          // プレイヤー出現後、高度連動フォグの参照を追従カメラへ（一度成功で固定）
            ResolveDuplicateCameras(); // 残置 MainCamera を無効化し追従カメラを唯一の MainCamera に（同上）
            DistributeClimbCourse();   // 山頂ベイク確定後、登攀コースを手続き山へ標高順に再配置（一度成功で固定）

            if (!_initialDone)
            {
                SnapPending();
                if (!_playerPlaced) _playerPlaced = PlacePlayers();
                RescueBuriedBodies();
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
            }
        }

        // ── 平坦台座（丸角プラトー） ───────────────────────────────
        private bool TryBuildBasecampPad()
        {
            Vector2 min, max;
            var camp = GameObject.Find(basecampContainerName);
            if (camp != null && camp.transform.childCount > 0)
            {
                min = new Vector2(float.MaxValue, float.MaxValue);
                max = new Vector2(float.MinValue, float.MinValue);
                foreach (Transform c in camp.transform)
                {
                    var p = c.position;
                    if (p.x < min.x) min.x = p.x;
                    if (p.x > max.x) max.x = p.x;
                    if (p.z < min.y) min.y = p.z;
                    if (p.z > max.y) max.y = p.z;
                }
                min -= Vector2.one * footprintMargin;
                max += Vector2.one * footprintMargin;
            }
            else
            {
                min = probeXZ - Vector2.one * fallbackFootprintHalf;
                max = probeXZ + Vector2.one * fallbackFootprintHalf;
            }

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

            Vector2 dir = summitXZ.normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            int moved = 0, movedShrines = 0;

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

                Vector2 xz = Vector2.Lerp(Vector2.zero, summitXZ, f) + perp * lat + dir * along;
                if (!TrySampleGround(xz.x, xz.y, out float gy))
                    gy = Mathf.Lerp(summit.y * 0.12f, summit.y, f); // 念のためのルート標高補間
                t.position = new Vector3(xz.x, gy + climbLift, xz.y);
                moved++;
                if (isShrine) movedShrines++;
            }

            _climbDistributed = true;
            Debug.Log($"[CombinedTerrainConformer] 登攀コースを手続き山へ再配置: {moved} 個（うち祠 {movedShrines}）(summit={summit}, dist={summitXZ.magnitude:F0}m, baked={baker.BakedCount})");
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
                foreach (Transform c in container.transform) dest.Add(c);
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
                var t = pc.transform;
                var pos = t.position;
                if (!TrySampleGround(pos.x, pos.z, out float gy)) continue;
                pos.y = gy + playerLift;
                var rb = pc.GetComponent<Rigidbody>();
                if (rb != null) { rb.position = pos; rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
                t.position = pos;
                placed++;
            }
            if (placed > 0) Physics.SyncTransforms();
            return placed > 0;
        }

        private void RescueBuriedBodies()
        {
            var bodies = FindObjectsByType<Rigidbody>(FindObjectsSortMode.None);
            for (int i = 0; i < bodies.Length; i++)
            {
                var rb = bodies[i];
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
            var npcs = FindObjectsByType<NPCController>(FindObjectsSortMode.None);
            for (int i = 0; i < npcs.Length; i++) yield return npcs[i].gameObject;
            var enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++) yield return enemies[i].gameObject;
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
