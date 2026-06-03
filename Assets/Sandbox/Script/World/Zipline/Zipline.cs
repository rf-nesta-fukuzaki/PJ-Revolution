using UnityEngine;
using K = Sandbox.World.Integration.BasecampPropKit;

namespace Sandbox.World.Zipline
{
    /// <summary>
    /// 拠点(StationA)と到達済みチェックポイント(StationB)を結ぶ 1 本のジップライン。
    /// 両端に木製の支柱＋滑車＋足場ステーションを手続き的に組み、たわんだケーブルを LineRenderer で描画する。
    /// 物理は持たず「乗車線」としてのデータ（曲線サンプリング・乗車位置・トロリー演出）のみを提供し、
    /// 実際の搭乗・走行は <see cref="ZiplineRider"/> が担う。
    /// すべて実行時生成・実行時マテリアル（アセットや他シーンを汚さない）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Zipline : MonoBehaviour
    {
        // ── 公開状態 ─────────────────────────────────────────────
        /// <summary>拠点側ステーションの足場（搭乗位置）。</summary>
        public Vector3 StationA { get; private set; }
        /// <summary>チェックポイント側ステーションの足場（搭乗位置）。</summary>
        public Vector3 StationB { get; private set; }
        /// <summary>ケーブル端点（拠点側支柱頭）。</summary>
        public Vector3 AnchorA { get; private set; }
        /// <summary>ケーブル端点（チェックポイント側支柱頭）。</summary>
        public Vector3 AnchorB { get; private set; }
        /// <summary>このラインが結ぶチェックポイント番号（0 始まり）。</summary>
        public int CheckpointIndex { get; private set; }

        private float _sag;
        private float _cableLength = 1f;
        private LineRenderer _cable;
        private Transform _trolley;
        private float _trolleyT;
        private float _trolleyParkT;
        private float _lastDrivenTime = -999f;
        private Color _color = new Color(0.35f, 0.78f, 1f);

        private const int CableSegments = 24;
        private const float TrolleyParkParamSpeed = 0.6f; // 駐機端へ戻る param 速度 (1/s)
        // 全ステーション共通の「見た目の支柱高さ」。地形クリアランスでケーブル端点(anchor)が上がっても、
        // 主構造（支柱・足場・滑車）はこの一定高さで作り、anchor まではその上に細い延長ポールで繋ぐ。
        // これにより支柱の太い見た目が一定の大きさに揃う。
        private const float StationVisualHeight = 6.5f;

        // ── 配色（拠点キャンプと馴染む木/鉄＋アクセント色） ──
        private static readonly Color Timber     = new Color(0.38f, 0.27f, 0.17f);
        private static readonly Color TimberDark = new Color(0.26f, 0.18f, 0.12f);
        private static readonly Color Iron       = new Color(0.20f, 0.21f, 0.23f);
        private static readonly Color Cable      = new Color(0.46f, 0.48f, 0.52f); // 明るめスチール（遠目でも視認できる鋼索）

        /// <summary>ケーブルの近似全長（ライダーの param 速度換算に使用）。</summary>
        public float CableLength => _cableLength;

        /// <summary>
        /// ライン本体を組み立てる。stationA/stationB は搭乗用の足場（地表付近）、anchorA/anchorB は
        /// 支柱頭＝ケーブル端点。color はステーションの発光アクセント＆ HUD 用。
        /// </summary>
        public void Build(int checkpointIndex,
                          Vector3 stationA, Vector3 anchorA,
                          Vector3 stationB, Vector3 anchorB,
                          Color color, float sagFactor = 0.06f)
        {
            CheckpointIndex = checkpointIndex;
            StationA = stationA; AnchorA = anchorA;
            StationB = stationB; AnchorB = anchorB;
            _color = color;

            Vector3 span = anchorB - anchorA;
            float horiz = new Vector2(span.x, span.z).magnitude;
            _sag = Mathf.Clamp(horiz * sagFactor, 0.6f, 6f);
            _cableLength = ApproximateCableLength();

            BuildStation(stationA, anchorA, anchorB, "Station_Base");
            BuildStation(stationB, anchorB, anchorA, "Station_Checkpoint");
            BuildCable();
            BuildTrolley();

            // 既定は拠点側に駐機（拠点から登れる「ショートカット」を視覚的に示す）。
            _trolleyParkT = 0f;
            _trolleyT = 0f;
            UpdateTrolleyVisual(0f);
        }

        // ── 曲線サンプリング（放物線近似のたわみ） ──────────────────
        /// <summary>ケーブル上の点を返す（t: 0=AnchorA, 1=AnchorB）。中央が重力方向へたわむ。</summary>
        public Vector3 SampleCable(float t)
        {
            t = Mathf.Clamp01(t);
            Vector3 p = Vector3.Lerp(AnchorA, AnchorB, t);
            p.y -= _sag * 4f * t * (1f - t);
            return p;
        }

        /// <summary>搭乗側の足場（end=false: A側 / true: B側）。</summary>
        public Vector3 StationGround(bool endB) => endB ? StationB : StationA;

        /// <summary>トロリーを param 位置 t へ移動（ライダー走行中の演出）。毎フレーム呼ばれると駐機ロジックより優先される。</summary>
        public void SetTrolley(float t)
        {
            _trolleyT = Mathf.Clamp01(t);
            _lastDrivenTime = Time.time;
            UpdateTrolleyVisual(_trolleyT);
        }

        /// <summary>誰も乗っていないときの駐機端（0 or 1）。ライダー降車時に呼ぶ。</summary>
        public void ParkTrolley(float t)
        {
            _trolleyParkT = Mathf.Clamp01(t);
        }

        private void Update()
        {
            // 乗車中（直近に SetTrolley された）は駐機イーズを行わない。
            if (Time.time - _lastDrivenTime < 0.15f) return;

            float next = Mathf.MoveTowards(_trolleyT, _trolleyParkT, TrolleyParkParamSpeed * Time.deltaTime);
            if (!Mathf.Approximately(next, _trolleyT))
            {
                _trolleyT = next;
                UpdateTrolleyVisual(_trolleyT);
            }
        }

        private float ApproximateCableLength()
        {
            float len = 0f;
            Vector3 prev = SampleCableRaw(0f);
            for (int i = 1; i <= CableSegments; i++)
            {
                Vector3 cur = SampleCableRaw(i / (float)CableSegments);
                len += Vector3.Distance(prev, cur);
                prev = cur;
            }
            return Mathf.Max(0.5f, len);
        }

        // Build 時点（_sag 確定後）にも使える素のサンプリング。
        private Vector3 SampleCableRaw(float t)
        {
            Vector3 p = Vector3.Lerp(AnchorA, AnchorB, t);
            p.y -= _sag * 4f * t * (1f - t);
            return p;
        }

        // ── ステーション（支柱＋筋交い＋滑車＋足場＋アクセント灯） ──
        private void BuildStation(Vector3 ground, Vector3 anchorTop, Vector3 facing, string name)
        {
            var root = K.Group(name, transform, Vector3.zero);
            root.position = ground;

            // 水平の向き（相手ステーション方向）へ正対させる。
            Vector3 dir = facing - ground; dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                root.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);

            // ケーブル端点(anchor)のローカル高さ。地形クリアランスでこれが上がっても、主構造は一定高さで作る。
            float cableLocalY = Mathf.Max(2.5f, anchorTop.y - ground.y);
            float vis = Mathf.Min(StationVisualHeight, cableLocalY); // 太い支柱は一定の見た目高さ

            var matT = K.Mat(Timber);
            var matD = K.Mat(TimberDark);
            var matIron = K.Mat(Iron, 0.3f, 0.6f);
            var matAccent = K.Mat(_color, 0.2f, 0.1f, emission: _color * 1.4f);

            // 主柱（一定高さ）＋足元の土台。
            K.Cyl("Mast", root, new Vector3(0f, vis * 0.5f, 0f), 0.22f, vis, matT);
            K.Box("MastBase", root, new Vector3(0f, 0.25f, 0f), new Vector3(1.2f, 0.5f, 1.2f), matD);

            // 前後の控え（A フレーム筋交い）。一定高さ基準。
            for (int s = -1; s <= 1; s += 2)
            {
                K.Cyl($"Brace_{s}", root, new Vector3(0f, vis * 0.45f, s * 1.0f), 0.12f, vis * 0.95f, matD,
                      eulerLocal: new Vector3(s * 26f, 0f, 0f));
            }

            // 地形クリアランスで anchor が主柱より高い場合だけ、頭頂からケーブル端点まで細い延長ポールを足す。
            if (cableLocalY > vis + 0.1f)
            {
                float extLen = cableLocalY - vis;
                K.Cyl("MastExt", root, new Vector3(0f, vis + extLen * 0.5f, 0f), 0.10f, extLen, matIron);
            }

            // 滑車（ケーブル端点の鉄輪）。ケーブルがここを通る＝乗り口がひと目で分かる。
            float headLocalY = cableLocalY - 0.1f;
            K.Cyl("PulleyHub", root, new Vector3(0f, headLocalY, 0.18f), 0.16f, 0.32f, matIron,
                  solid: false, eulerLocal: new Vector3(90f, 0f, 0f));
            K.Cyl("PulleyRim", root, new Vector3(0f, headLocalY, 0.18f), 0.40f, 0.14f, matIron,
                  solid: false, eulerLocal: new Vector3(90f, 0f, 0f));

            // 乗降の足場（一段高い板。乗れる＝当たり判定あり）。
            K.Box("Deck", root, new Vector3(0f, 0.16f, 0.0f), new Vector3(2.6f, 0.3f, 2.6f), K.Mat(new Color(0.24f, 0.25f, 0.27f), 0.1f));

            // 行き先を示すアクセント帯（発光）＋誘導灯（主柱頭・一定位置）。
            K.Box("Marker", root, new Vector3(0f, vis - 0.6f, 0.24f), new Vector3(0.8f, 0.8f, 0.08f), matAccent, solid: false);
            var light = K.PointLight("StationLight", root, new Vector3(0f, vis, 0.2f), _color, 10f, 2.6f);
            light.shadows = LightShadows.None;

            // ジップラインだと分かる方向矢印（足場手前→ケーブル方向）。発光アクセント。
            K.Box("ArrowShaft", root, new Vector3(0f, vis + 0.55f, 0.0f), new Vector3(0.14f, 0.14f, 1.0f), matAccent, solid: false);
            K.Cyl("ArrowHead", root, new Vector3(0f, vis + 0.55f, 0.62f), 0.22f, 0.34f, matAccent, solid: false,
                  eulerLocal: new Vector3(90f, 0f, 0f));

            // 乗車目印（足場中央の発光リング）。
            K.Cyl("MountPad", root, new Vector3(0f, 0.33f, 0f), 0.9f, 0.05f, matAccent, solid: false);
        }

        // ── ケーブル（たわんだ撚り線。LineRenderer） ────────────────
        private LineRenderer _guide; // ケーブル上のアクセント色ガイド（ジップラインらしさ）

        private void BuildCable()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");

            // ① 太い鋼索（やや明るいスチール）。遠目でもはっきり 1 本の線として見える太さに。
            var go = new GameObject("Cable");
            go.transform.SetParent(transform, false);
            _cable = ConfigureCableLine(go, shader, 0.14f, Cable, "ZiplineCableMat");

            // ② 鋼索の少し上に重ねるアクセント色ガイドライン。各ジップラインの色で「乗れる線」を主張する。
            var ggo = new GameObject("CableGuide");
            ggo.transform.SetParent(transform, false);
            Color guideColor = Color.Lerp(_color, Color.white, 0.15f);
            _guide = ConfigureCableLine(ggo, shader, 0.06f, guideColor, "ZiplineGuideMat");

            RefreshCablePositions();
        }

        private static LineRenderer ConfigureCableLine(GameObject go, Shader shader, float width, Color color, string matName)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCapVertices = 4;
            lr.numCornerVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.startWidth = width;
            lr.endWidth = width;

            var mat = new Material(shader) { name = matName };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;
            lr.sharedMaterial = mat;
            return lr;
        }

        private void RefreshCablePositions()
        {
            _cable.positionCount = CableSegments + 1;
            for (int i = 0; i <= CableSegments; i++)
                _cable.SetPosition(i, SampleCableRaw(i / (float)CableSegments));

            if (_guide != null)
            {
                _guide.positionCount = CableSegments + 1;
                for (int i = 0; i <= CableSegments; i++)
                    _guide.SetPosition(i, SampleCableRaw(i / (float)CableSegments) + Vector3.up * 0.06f);
            }
        }

        // ── トロリー（滑車ハンドル。乗車中はライダーがぶら下がる） ──
        private void BuildTrolley()
        {
            var root = new GameObject("Trolley").transform;
            root.SetParent(transform, false);

            var matIron = K.Mat(Iron, 0.45f, 0.75f);
            var matAccent = K.Mat(_color, 0.2f, 0.1f, emission: _color * 1.3f);
            var matGrip = K.Mat(new Color(0.5f, 0.34f, 0.16f));

            // ケーブルに乗る大きめの二輪（はっきり分かるサイズに）。
            for (int s = -1; s <= 1; s += 2)
                K.Cyl($"Wheel_{s}", root, new Vector3(0f, 0f, s * 0.14f), 0.26f, 0.12f, matIron, solid: false,
                      eulerLocal: new Vector3(0f, 0f, 90f));
            // 車軸。
            K.Cyl("Axle", root, new Vector3(0f, 0f, 0f), 0.05f, 0.4f, matIron, solid: false,
                  eulerLocal: new Vector3(90f, 0f, 0f));

            // アクセント色のフレーム（ヨーク）＝色でジップラインのトロリーと分かる。
            K.Box("Yoke", root, new Vector3(0f, -0.28f, 0f), new Vector3(0.16f, 0.56f, 0.16f), matAccent, solid: false);
            // 吊り下げハーネス（座板）。
            K.Box("Seat", root, new Vector3(0f, -0.62f, 0f), new Vector3(0.5f, 0.1f, 0.42f), matAccent, solid: false);
            // 握りバー（T 字ハンドル）。
            K.Cyl("Handle", root, new Vector3(0f, -0.5f, 0f), 0.06f, 0.8f, matGrip, solid: false,
                  eulerLocal: new Vector3(90f, 0f, 0f));

            _trolley = root;
        }

        private void UpdateTrolleyVisual(float t)
        {
            if (_trolley == null) return;
            Vector3 pos = SampleCableRaw(t);
            _trolley.position = pos;

            // 進行方向へ向ける（端では隣点との差分で算出）。
            float t2 = Mathf.Clamp01(t + 0.01f);
            Vector3 fwd = SampleCableRaw(t2) - pos;
            if (fwd.sqrMagnitude > 0.0001f)
                _trolley.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        private void OnDestroy()
        {
            if (_cable != null && _cable.sharedMaterial != null)
                Destroy(_cable.sharedMaterial);
            if (_guide != null && _guide.sharedMaterial != null)
                Destroy(_guide.sharedMaterial);
        }
    }
}
