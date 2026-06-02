using UnityEngine;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// 山腰の中腹高度に水平な半透明 plane を配置し、雲海風の演出を出す。
    /// 高度は SandboxBootstrap.ColliderBaker.GlobalMaxY * altitudeFraction で動的決定（観測完了後に置き直し）。
    /// 1 枚 plane で済む軽量実装。Step 5 で Volumetric/board cluster 化検討。
    /// </summary>
    public sealed class CloudSeaLayer : MonoBehaviour
    {
        [SerializeField] private float planeSize = 4000f;
        [Tooltip("summit Y * これ の高度に置く。高めにして山頂付近の雲海とし、裾野の登攀視界を塞がない。")]
        [Range(0f, 1f)] [SerializeField] private float altitudeFraction = 0.62f;
        [SerializeField] private Color color = new Color(0.95f, 0.97f, 1.00f, 0.40f);
        [SerializeField] private float radiusFalloff = 0.62f;
        [SerializeField] private float noiseScale = 0.4f;
        [SerializeField] private float noiseStrength = 0.55f;
        [SerializeField] private float scrollSpeed = 0.02f;

        [Header("Camera Follow")]
        [Tooltip("ON ならカメラ XZ に追従し常に視界中央に雲海を維持する。")]
        [SerializeField] private bool followCameraXZ = true;
        [Tooltip("カメラが planeSize の何割移動したら再センタリングするか（小刻みに毎フレーム書換えないため）。")]
        [Range(0f, 0.5f)] [SerializeField] private float recenterThreshold = 0.10f;

        [Header("Fallback（SandboxSummitGoal 不在シーン: combined offline 等）")]
        [Tooltip("地形最高点(GlobalMaxY)がこの秒数 伸び止まったら『山頂確定』とみなして即配置する。" +
                 "固定待ち(fallbackAfterSeconds)に頼らず、世界が実際に整い次第すぐ雲海を出すための主トリガー。")]
        [SerializeField] private float summitStableSeconds = 0.75f;
        [Tooltip("配置開始に必要な最小ベイク済みチャンク数。早すぎる GlobalMaxY での谷底誤配置を防ぐ。")]
        [SerializeField] private int minBakedChunksForPlacement = 8;
        [Tooltip("安全弁。山頂が一向に伸び止まらなくても、この秒数 経過したら現在の GlobalMaxY で強制配置する（旧・固定待ち値）。")]
        [SerializeField] private float fallbackAfterSeconds = 8f;
        [Tooltip("フォールバック時の絶対高度[m]。0 以下なら GlobalMaxY * altitudeFraction を使う。")]
        [SerializeField] private float fallbackAbsoluteAltitude = 0f;
        [Tooltip("配置後に後から焼ける高所チャンクで GlobalMaxY が伸びた際、雲海高度を追従させる速度[m/s]。" +
                 "早期配置でも最終的に正しい高度へ滑らかに補正され、ぴょこっと跳ねない。")]
        [SerializeField] private float altitudeFollowSpeed = 80f;

        // 配置タイミング計測用（検証・デバッグ）。-1 = 未配置。
        public float PlacedAtTime { get; private set; } = -1f;
        public int PlacedAtFrame { get; private set; } = -1;
        public bool HasPlaced => _placed;

        private GameObject _plane;
        private Sandbox.World.Integration.SandboxBootstrap _bootstrap;
        private Sandbox.World.Integration.SandboxSummitGoal _summitGoal;
        private bool _placed;
        private float _placedY;
        private Vector2 _lastCenterXZ;
        // 山頂(GlobalMaxY)安定検出用。
        private float _maxYObserved = -1f;
        private float _maxYStableTime;

        private void Awake()
        {
            _bootstrap  = GetComponent<Sandbox.World.Integration.SandboxBootstrap>();
            _summitGoal = GetComponent<Sandbox.World.Integration.SandboxSummitGoal>();
        }

        private void Update()
        {
            if (_summitGoal == null)
                _summitGoal = GetComponent<Sandbox.World.Integration.SandboxSummitGoal>();

            if (!_placed)
            {
                if (_bootstrap == null) return;

                bool haveSummit = _summitGoal != null && _summitGoal.HasSummit;
                float placeY;
                string src;
                if (haveSummit)
                {
                    // 高度は確定済み山頂 Y を基準にする（旧実装は早期 GlobalMaxY≈0 を採用し谷底に置かれていた）。
                    placeY = _summitGoal.SummitPosition.y * altitudeFraction;
                    src = $"summit*{altitudeFraction:F2}";
                }
                else
                {
                    // SandboxSummitGoal 不在シーン（combined offline 等）。
                    // 旧実装は固定 8 秒待ってから置いていたため、地形が一瞬で焼けても雲海が 8 秒間出なかった。
                    // 改善: 地形最高点(GlobalMaxY)が伸び止まった瞬間（＝山頂チャンクまで焼けて確定）に即配置する。
                    // これで世界が整い次第すぐ雲海が出る。fallbackAfterSeconds は伸び止まらない場合の安全弁のみ。
                    bool capReached = Time.time >= fallbackAfterSeconds;
                    if (fallbackAbsoluteAltitude > 0f)
                    {
                        placeY = fallbackAbsoluteAltitude;
                        src = "fallbackAbs";
                    }
                    else
                    {
                        var baker = _bootstrap.ColliderBaker;
                        if (baker == null) return;
                        if (!capReached)
                        {
                            // 最低限の地形が焼けるまで待つ（早すぎる GlobalMaxY での谷底配置を防ぐ）。
                            if (baker.GlobalMaxY < 1f || baker.BakedCount < minBakedChunksForPlacement) return;
                            // GlobalMaxY が伸びている間は山頂未確定。伸び止まって summitStableSeconds 続いたら確定。
                            if (baker.GlobalMaxY > _maxYObserved + 1f)
                            {
                                _maxYObserved = baker.GlobalMaxY;
                                _maxYStableTime = 0f;
                                return;
                            }
                            _maxYStableTime += Time.deltaTime;
                            if (_maxYStableTime < summitStableSeconds) return;
                        }
                        else if (baker.GlobalMaxY < 1f)
                        {
                            return; // 安全弁時刻でも地形ゼロなら置けない（ごく稀）
                        }
                        placeY = baker.GlobalMaxY * altitudeFraction;
                        src = capReached
                            ? $"terrainMax({baker.GlobalMaxY:F0})*{altitudeFraction:F2} [cap@{fallbackAfterSeconds:F0}s]"
                            : $"terrainMax({baker.GlobalMaxY:F0})*{altitudeFraction:F2} [stable@{Time.time:F1}s]";
                    }
                }

                _placedY = placeY;
                _plane = CreatePlane(_placedY);
                _placed = true;
                PlacedAtTime = Time.time;
                PlacedAtFrame = Time.frameCount;
                _lastCenterXZ = _plane != null
                    ? new Vector2(_plane.transform.position.x, _plane.transform.position.z)
                    : Vector2.zero;
                Debug.Log($"[CloudSeaLayer] placed at Y={_placedY:F1} ({src}) t={PlacedAtTime:F2}s frame={PlacedAtFrame}");
                return;
            }

            // 山頂が後から更新されたら雲海高度も追従。
            if (_summitGoal != null && _summitGoal.HasSummit && _plane != null)
            {
                float targetY = _summitGoal.SummitPosition.y * altitudeFraction;
                if (Mathf.Abs(targetY - _placedY) > 1f)
                {
                    _placedY = targetY;
                    var pp = _plane.transform.position;
                    _plane.transform.position = new Vector3(pp.x, _placedY, pp.z);
                }
            }
            // SummitGoal 不在シーン: 後から焼ける高所チャンクで GlobalMaxY が伸びたら高度を滑らかに追従。
            // （早期配置でも最終的に正しい高度へ補正され、瞬間移動でなく緩やかに上がるため自然。）
            else if (_summitGoal == null && _plane != null && fallbackAbsoluteAltitude <= 0f
                     && _bootstrap.ColliderBaker != null)
            {
                float targetY = _bootstrap.ColliderBaker.GlobalMaxY * altitudeFraction;
                if (targetY > 1f && Mathf.Abs(targetY - _placedY) > 0.5f)
                {
                    _placedY = Mathf.MoveTowards(_placedY, targetY, altitudeFollowSpeed * Time.deltaTime);
                    var pp = _plane.transform.position;
                    _plane.transform.position = new Vector3(pp.x, _placedY, pp.z);
                }
            }

            if (!followCameraXZ || _plane == null) return;
            var cam = Camera.main; if (cam == null) return;
            var camXZ = new Vector2(cam.transform.position.x, cam.transform.position.z);
            float thr = planeSize * recenterThreshold;
            if ((camXZ - _lastCenterXZ).sqrMagnitude < thr * thr) return;
            _plane.transform.position = new Vector3(camXZ.x, _placedY, camXZ.y);
            _lastCenterXZ = camXZ;
        }

        private GameObject CreatePlane(float y)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "SandboxCloudSea";
            go.transform.SetParent(_bootstrap.TerrainGenerator.transform, false);
            // Quad は XY 平面を向くので 90° 倒して XZ に
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            // カメラ中心付近に置く（簡易）。Step 5 でカメラ追従/分割を検討。
            var cam = Camera.main;
            float cx = cam != null ? cam.transform.position.x : 0f;
            float cz = cam != null ? cam.transform.position.z : 0f;
            go.transform.position = new Vector3(cx, y, cz);
            go.transform.localScale = new Vector3(planeSize, planeSize, 1f);

            // 衝突しないように collider 削除
            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var sh = Shader.Find("Sandbox/CloudSeaSoft");
            var mat = new Material(sh) { name = "CloudSeaSoftMat" };
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_RadiusFalloff", radiusFalloff);
            mat.SetFloat("_NoiseScale", noiseScale);
            mat.SetFloat("_NoiseStrength", noiseStrength);
            mat.SetFloat("_ScrollSpeed", scrollSpeed);
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return go;
        }
    }
}
