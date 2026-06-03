using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;

namespace Sandbox.World.Zipline
{
    /// <summary>
    /// 拠点（ベースキャンプ）と到達済みチェックポイントを結ぶジップライン網の管理者（シーン単一）。
    /// チェックポイント到達時に <see cref="InstallLine"/> が呼ばれ、拠点⇄当該チェックポイントの
    /// <see cref="Zipline"/> を 1 本だけ設置する（再到達では再設置しない・冪等）。
    ///
    /// 拠点側の支柱は、各チェックポイント方向へ放射状にずらして並べ、複数ラインが重ならないようにする。
    /// 走行は各プレイヤーの <see cref="ZiplineRider"/> が本ネットワークの <see cref="Lines"/> を参照して行う。
    /// </summary>
    public sealed class ZiplineNetwork : MonoBehaviour
    {
        public static ZiplineNetwork Instance { get; private set; }

        [Header("拠点アンカー")]
        [Tooltip("拠点中心（XZ）と地表 Y。Configure で設定。")]
        [SerializeField] private Vector3 _baseCenter;
        [Tooltip("拠点側支柱の高さ[m]（ケーブル端点＝支柱頭）。全ステーションで統一。")]
        [SerializeField] private float _baseTowerHeight = 6.5f;

        [Header("拠点ステーション配置（4隅）")]
        [Tooltip("ON で 拠点側ステーションを拠点の4隅へ配置する（各ラインを別の隅に割り当て、キャンプ構造物と被らせない）。")]
        [SerializeField] private bool _useCorners = true;
        [Tooltip("拠点中心から各隅までの片軸距離[m]。柵（半辺 ~15.5m）の内側に収めるため既定はやや内側。隅は (±この値, ±この値)。")]
        [SerializeField] private float _cornerExtent = 13f;

        // 4隅モード非使用時の扇状フォールバック設定。
        [SerializeField] private float _baseStandoff = 22f;
        [SerializeField] private float _fanSpacing = 4.5f;

        [Header("チェックポイント側")]
        [Tooltip("チェックポイント側支柱の高さ[m]。拠点側と揃えて全ステーションを同寸にする。")]
        [SerializeField] private float _checkpointTowerHeight = 6.5f;

        [Header("ケーブル地形クリアランス")]
        [Tooltip("ケーブルが地形から確保する最小クリアランス[m]（ライダーのぶら下がり ~1.9m + 体 + 余裕）。走行中に山へ潜らないよう両端を持ち上げる基準。")]
        [SerializeField] private float _riderClearance = 5f;
        [Tooltip("地形を越えるためにケーブル両端を持ち上げる最大量[m]。")]
        [SerializeField] private float _maxClearanceRaise = 60f;
        [Tooltip("ルート沿いの地形クリアランス判定のサンプル数（多いほど鋭い尾根も拾う）。")]
        [SerializeField] private int _clearanceSamples = 28;

        // 全ラインで統一する持ち上げ量[m]。設定されると各ラインはこの値を使い、延長ポール長が全て同一になる。
        private float _uniformRaise;
        private bool _useUniformRaise;

        /// <summary>全ジップラインで共通の持ち上げ量[m]を設定する（延長ポールの長さを全ライン統一するため）。</summary>
        public void SetUniformRaise(float raise)
        {
            _uniformRaise = Mathf.Clamp(raise, 0f, _maxClearanceRaise);
            _useUniformRaise = true;
        }

        // 拠点ステーションを左右へ扇状に分離する際の中央寄せ用レーン総数（扇状フォールバック時のみ使用）。
        private int _laneCount = 3;
        // 4隅の割り当て順を決めるための山頂方向（XZ）。CombinedTerrainConformer が設置前に与える。未設定なら初回設置の方向を採用。
        private Vector2 _summitDir2 = Vector2.zero;
        private bool _summitDirSet;

        /// <summary>拠点ステーションの左右扇状配置を中央寄せするためのレーン総数を設定する（扇状フォールバック時のみ）。</summary>
        public void SetLaneCount(int count) => _laneCount = Mathf.Max(1, count);

        /// <summary>4隅の割り当て順を決める山頂方向（XZ）を設定する（設置前に呼ぶ）。</summary>
        public void SetSummitDirection(Vector2 dirXZ)
        {
            if (dirXZ.sqrMagnitude < 0.0001f) return;
            _summitDir2 = dirXZ.normalized;
            _summitDirSet = true;
        }

        /// <summary>チェックポイント番号で循環する既定の演出色。ビーコンとラインで共有する。</summary>
        public static readonly Color[] DefaultColors =
        {
            new Color(0.35f, 0.80f, 1.00f),
            new Color(1.00f, 0.72f, 0.28f),
            new Color(0.55f, 1.00f, 0.55f),
            new Color(1.00f, 0.45f, 0.55f),
            new Color(0.80f, 0.60f, 1.00f),
        };

        /// <summary>チェックポイント番号に対応する既定色を返す。</summary>
        public static Color ColorFor(int index) => DefaultColors[Mathf.Abs(index) % DefaultColors.Length];

        [Header("演出色（チェックポイント番号で循環）")]
        [SerializeField] private Color[] _lineColors = (Color[])DefaultColors.Clone();

        private bool _baseConfigured;
        private readonly Dictionary<int, Zipline> _lines = new();

        /// <summary>設置済みライン（チェックポイント番号 → ライン）。</summary>
        public IReadOnlyDictionary<int, Zipline> Lines => _lines;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>拠点アンカーの中心と地表 Y を設定する（地形整合後に呼ぶ）。</summary>
        public void ConfigureBase(Vector3 center, float groundY)
        {
            _baseCenter = new Vector3(center.x, groundY, center.z);
            _baseConfigured = true;
        }

        /// <summary>
        /// シーンに <see cref="ZiplineNetwork"/> が無ければ生成して返す。拠点中心は引数優先、無ければ
        /// "Basecamp" / "DepartureGate" / 原点 から推定する。チェックポイント側から遅延起動する用途。
        /// </summary>
        public static ZiplineNetwork Ensure(Vector3? baseCenter = null, float? baseGroundY = null)
        {
            if (Instance != null)
            {
                if (baseCenter.HasValue && baseGroundY.HasValue && !Instance._baseConfigured)
                    Instance.ConfigureBase(baseCenter.Value, baseGroundY.Value);
                return Instance;
            }

            var go = new GameObject("ZiplineNetwork");
            var net = go.AddComponent<ZiplineNetwork>();

            if (baseCenter.HasValue && baseGroundY.HasValue)
            {
                net.ConfigureBase(baseCenter.Value, baseGroundY.Value);
            }
            else
            {
                Vector3 c = ResolveBaseCenterFromScene();
                net.ConfigureBase(c, c.y);
            }
            return net;
        }

        /// <summary>
        /// チェックポイント番号に対応する拠点の隅（中心からのオフセット XZ）を返す。4隅を山頂方向への
        /// 近さ順に並べ、index で巡回割り当てする（山側の隅から先に使い、ケーブルがキャンプを跨ぎにくくする）。
        /// </summary>
        private Vector2 ResolveCorner(int checkpointIndex)
        {
            float e = _cornerExtent;
            // NE, NW, SW, SE
            var corners = new[]
            {
                new Vector2(+e, +e), new Vector2(-e, +e), new Vector2(-e, -e), new Vector2(+e, -e),
            };

            Vector2 sdir = _summitDirSet ? _summitDir2 : Vector2.up;
            // 山頂方向への整列度（dot）で降順ソート（山側の隅が先頭）。
            System.Array.Sort(corners, (a, b) =>
                Vector2.Dot(b.normalized, sdir).CompareTo(Vector2.Dot(a.normalized, sdir)));

            return corners[Mathf.Abs(checkpointIndex) % corners.Length];
        }

        /// <summary>
        /// ルート沿いに接地高を測り、放物線たわみを考慮してもケーブルが地形 + <see cref="_riderClearance"/> を
        /// 下回らないよう、両端アンカーを等量持ち上げる量[m]を返す（0〜<see cref="_maxClearanceRaise"/>）。
        /// 両端を同量上げると弦全体が平行移動し、最悪点が基準を満たせば全区間でクリアできる。
        /// </summary>
        private float ComputeClearanceRaise(Vector3 groundA, Vector3 anchorA, Vector3 groundB, Vector3 anchorB, float sag)
        {
            int samples = Mathf.Max(6, _clearanceSamples);
            float maxDeficit = 0f;
            Vector2 a = new Vector2(anchorA.x, anchorA.z);
            Vector2 b = new Vector2(anchorB.x, anchorB.z);
            for (int k = 1; k < samples; k++)
            {
                float t = k / (float)samples;
                Vector2 xz = Vector2.Lerp(a, b, t);
                float chordY = Mathf.Lerp(anchorA.y, anchorB.y, t);
                float cableY = chordY - sag * 4f * t * (1f - t);
                if (!TrySampleGroundY(xz.x, xz.y, out float gy))
                    gy = Mathf.Lerp(groundA.y, groundB.y, t); // 未ベイクチャンクは接地弦で代替
                float deficit = (gy + _riderClearance) - cableY;
                if (deficit > maxDeficit) maxDeficit = deficit;
            }
            return Mathf.Clamp(maxDeficit, 0f, _maxClearanceRaise);
        }

        /// <summary>(x,z) 真下の接地面 Y を返す（手続き地形チャンク / 拠点台座のみを対象）。</summary>
        private static bool TrySampleGroundY(float x, float z, out float y)
        {
            var origin = new Vector3(x, 2000f, z);
            var hits = Physics.RaycastAll(origin, Vector3.down, 4000f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MinValue;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                string n = hits[i].collider.gameObject.name;
                if (!(n.StartsWith("ChunkCollider_") || n == "BasecampPad")) continue;
                if (hits[i].point.y > best) { best = hits[i].point.y; found = true; }
            }
            y = found ? best : 0f;
            return found;
        }

        /// <summary>HUD へ「ジップライン開通」のトースト通知を出す（ExpeditionHUD があれば）。</summary>
        private static void NotifyOpened(int checkpointIndex)
        {
            var hud = ExpeditionHUD.Instance;
            if (hud != null)
                hud.ShowWarning($"ジップライン開通！ 拠点 ⇆ CP{checkpointIndex + 1}");
        }

        /// <summary>"Player" タグの Rigidbody を持つ各オブジェクトに <see cref="ZiplineRider"/> を保証する（冪等）。</summary>
        private static void EnsureRidersOnPlayers()
        {
            GameObject[] players;
            try { players = GameObject.FindGameObjectsWithTag("Player"); }
            catch (UnityException) { return; } // "Player" タグ未定義シーンでは何もしない
            foreach (var p in players)
            {
                if (p == null || p.GetComponent<Rigidbody>() == null) continue;
                if (p.GetComponent<ZiplineRider>() == null)
                    p.AddComponent<ZiplineRider>();
            }
        }

        private static Vector3 ResolveBaseCenterFromScene()
        {
            var basecamp = GameObject.Find("Basecamp");
            if (basecamp != null) return basecamp.transform.position;
            var gate = GameObject.Find("DepartureGate");
            if (gate != null) return gate.transform.position;
            return Vector3.zero;
        }

        public bool HasLine(int checkpointIndex) => _lines.ContainsKey(checkpointIndex);

        /// <summary>
        /// 指定チェックポイント位置に対し、実際に設置されるのと同じ拠点側コーナー・タワー高・たわみで
        /// 地形を貫通しないために必要な持ち上げ量[m]を見積もる。0 ならその位置は無調整でクリアできる。
        /// 配置側（CombinedTerrainConformer）がチェックポイントを散らして探索する際に使用する。
        /// </summary>
        public float EstimateRaiseFor(int checkpointIndex, Vector3 checkpointGround)
        {
            if (!_baseConfigured)
            {
                Vector3 c = ResolveBaseCenterFromScene();
                ConfigureBase(c, c.y);
            }

            Vector3 toCp = checkpointGround - _baseCenter; toCp.y = 0f;
            Vector3 dir = toCp.sqrMagnitude > 0.01f ? toCp.normalized : Vector3.forward;

            Vector3 baseStationGround;
            if (_useCorners)
            {
                Vector2 corner = ResolveCorner(checkpointIndex);
                baseStationGround = new Vector3(_baseCenter.x + corner.x, 0f, _baseCenter.z + corner.y);
            }
            else
            {
                Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;
                float center = (_laneCount - 1) * 0.5f;
                float lateral = (checkpointIndex - center) * _fanSpacing;
                baseStationGround = _baseCenter + dir * _baseStandoff + perp * lateral;
            }
            baseStationGround.y = TrySampleGroundY(baseStationGround.x, baseStationGround.z, out float bgy)
                ? bgy : _baseCenter.y;

            Vector3 anchorA0 = baseStationGround + Vector3.up * _baseTowerHeight;
            Vector3 anchorB0 = checkpointGround + Vector3.up * _checkpointTowerHeight;
            Vector3 spanH = anchorB0 - anchorA0;
            float horiz = new Vector2(spanH.x, spanH.z).magnitude;
            float sag = Mathf.Clamp(horiz * 0.06f, 0.6f, 6f);
            return ComputeClearanceRaise(baseStationGround, anchorA0, checkpointGround, anchorB0, sag);
        }

        /// <summary>
        /// 拠点⇄チェックポイント のラインを設置する。既に同番号があれば何もしない。
        /// </summary>
        /// <param name="checkpointIndex">チェックポイント番号（0 始まり）。</param>
        /// <param name="checkpointGround">チェックポイント足元のワールド座標。</param>
        public Zipline InstallLine(int checkpointIndex, Vector3 checkpointGround)
        {
            if (_lines.TryGetValue(checkpointIndex, out var existing) && existing != null)
                return existing;

            if (!_baseConfigured)
            {
                Vector3 c = ResolveBaseCenterFromScene();
                ConfigureBase(c, c.y);
            }

            Color color = _lineColors.Length > 0
                ? _lineColors[Mathf.Abs(checkpointIndex) % _lineColors.Length]
                : Color.cyan;

            // 拠点 → チェックポイントの水平方向（山頂方向の既定にも使う）。
            Vector3 toCp = checkpointGround - _baseCenter; toCp.y = 0f;
            Vector3 dir = toCp.sqrMagnitude > 0.01f ? toCp.normalized : Vector3.forward;
            if (!_summitDirSet) SetSummitDirection(new Vector2(dir.x, dir.z));

            // 拠点ステーションの XZ を決める。既定は拠点の4隅（ライン毎に別の隅）。
            Vector3 baseStationGround;
            if (_useCorners)
            {
                Vector2 corner = ResolveCorner(checkpointIndex);
                baseStationGround = new Vector3(_baseCenter.x + corner.x, 0f, _baseCenter.z + corner.y);
            }
            else
            {
                // 扇状フォールバック：山側外周に中央寄せで並べる。
                Vector3 perp = Vector3.Cross(Vector3.up, dir).normalized;
                float center = (_laneCount - 1) * 0.5f;
                float lateral = (checkpointIndex - center) * _fanSpacing;
                baseStationGround = _baseCenter + dir * _baseStandoff + perp * lateral;
            }
            // 外周の地形（pad スカート/自然地形）に正しく接地させる。取れなければ pad 天面高さで代替。
            baseStationGround.y = TrySampleGroundY(baseStationGround.x, baseStationGround.z, out float bgy)
                ? bgy : _baseCenter.y;

            Vector3 anchorA0 = baseStationGround + Vector3.up * _baseTowerHeight;
            Vector3 anchorB0 = checkpointGround + Vector3.up * _checkpointTowerHeight;

            // 地形クリアランス：ルート沿いの接地高を測り、たわんでも地形へ潜らないよう両端を等量持ち上げる。
            const float sagFactor = 0.06f; // Zipline.Build の既定たわみ係数と一致させる。
            Vector3 spanH = anchorB0 - anchorA0;
            float horiz = new Vector2(spanH.x, spanH.z).magnitude;
            float sag = Mathf.Clamp(horiz * sagFactor, 0.6f, 6f);
            // 統一レイズが設定されていれば全ライン共通値を使い、延長ポール長を全ライン同一にする。
            // 未設定（Stage01 等の個別開通）の場合のみライン毎にクリアランスを算出する。
            float raise = _useUniformRaise
                ? _uniformRaise
                : ComputeClearanceRaise(baseStationGround, anchorA0, checkpointGround, anchorB0, sag);

            Vector3 anchorA = anchorA0 + Vector3.up * raise;
            Vector3 anchorB = anchorB0 + Vector3.up * raise;

            var go = new GameObject($"Zipline_CP{checkpointIndex + 1}");
            go.transform.SetParent(transform, false);
            var zip = go.AddComponent<Zipline>();
            zip.Build(checkpointIndex, baseStationGround, anchorA, checkpointGround, anchorB, color, sagFactor);

            _lines[checkpointIndex] = zip;

            // ExplorerController 以外のプレイヤー実装でも乗れるよう、設置時に全プレイヤーへライダーを保証する。
            EnsureRidersOnPlayers();

            GameServices.Audio?.PlaySE(SoundId.RopeConnect, baseStationGround);
            NotifyOpened(checkpointIndex);
            Debug.Log($"[Zipline] CP{checkpointIndex + 1} へのジップラインを設置（拠点⇄CP, 距離 {Vector3.Distance(baseStationGround, checkpointGround):F0}m）");
            return zip;
        }
    }
}
