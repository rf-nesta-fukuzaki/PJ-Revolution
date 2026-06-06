using UnityEngine;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// 島（mountainRadius 内の地形）を取り囲む海面を表現する水平プレーン。
    ///
    /// 地形側（RidgedMFKernel.compute の islandMode）が海岸線より外を海面下（海底）へ沈めるため、
    /// このレイヤーは seaLevel 付近に大きな不透明プレーンを 1 枚置くだけで「島が海に囲まれた」見た目を作る。
    /// CloudSeaLayer と異なり水面は地形ベイク完了を待たず即配置でき（高度が seaLevel 固定で既知）、
    /// カメラ XZ へ追従して常に水平線まで水が続く。軽量・URP/Lit の不透明プレーンで実装する
    /// （半透明は描画順・URP 設定依存で破綻しやすいため、確実性優先で不透明）。
    /// </summary>
    public sealed class OceanLayer : MonoBehaviour
    {
        [Header("Sea Level")]
        [Tooltip("RidgedMFParams.seaLevel と一致させる海面の基準高度 [m]。")]
        [SerializeField] private float seaLevel = 0f;
        [Tooltip("海面を seaLevel からわずかに持ち上げる量 [m]。波打ち際（島の縁）を少しだけ水に浸して海岸線を自然にする。")]
        [SerializeField] private float waterLevelOffset = 4f;

        [Header("Plane")]
        [Tooltip("水面プレーンの一辺 [m]。遠クリップ面（〜1800m）を十分覆うよう大きめに。")]
        [SerializeField] private float planeSize = 24000f;

        [Header("Water Look（Sandbox/WaterSurface 使用時）")]
        [Tooltip("浅瀬の色（RGB）と透過度（A）。岸付近の色。砂浜の遠浅らしい明るいターコイズに。")]
        [SerializeField] private Color shallowColor = new Color(0.30f, 0.66f, 0.68f, 0.5f);
        [Tooltip("深場の色（RGB）と透過度（A）。沖の色。")]
        [SerializeField] private Color deepColor = new Color(0.02f, 0.11f, 0.24f, 0.95f);
        [Tooltip("この水深[m]で浅瀬色→深場色へ遷移しきる。大きいほど遠浅のターコイズ帯が広く＝砂浜らしい。")]
        [SerializeField] private float depthFadeMeters = 24f;
        [Tooltip("岸の泡が出る水深帯[m]。波打ち際の白波。")]
        [SerializeField] private float foamDistance = 5f;
        [Tooltip("波の細かさ。大きいほど細波。")]
        [SerializeField] private float waveScale = 0.08f;
        [Tooltip("波のスクロール速度。")]
        [SerializeField] private float waveSpeed = 0.6f;
        [Tooltip("波法線の強さ（鏡面のゆらぎ）。")]
        [SerializeField] private float waveStrength = 0.6f;
        [Tooltip("シェーダーが見つからない場合のフォールバック材質の光沢。")]
        [Range(0f, 1f)] [SerializeField] private float fallbackSmoothness = 0.85f;

        [Header("Camera Follow")]
        [Tooltip("ON ならカメラ XZ に追従し常に水平線まで海を維持する。")]
        [SerializeField] private bool followCameraXZ = true;
        [Tooltip("カメラが planeSize の何割移動したら再センタリングするか（毎フレーム書換えを避ける）。")]
        [Range(0f, 0.5f)] [SerializeField] private float recenterThreshold = 0.08f;

        private GameObject _plane;
        private float _surfaceY;
        private Vector2 _lastCenterXZ;

        private void Start()
        {
            _surfaceY = seaLevel + waterLevelOffset;
            _plane = CreatePlane(_surfaceY);
            _lastCenterXZ = _plane != null
                ? new Vector2(_plane.transform.position.x, _plane.transform.position.z)
                : Vector2.zero;
        }

        private void Update()
        {
            if (!followCameraXZ || _plane == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            var camXZ = new Vector2(cam.transform.position.x, cam.transform.position.z);
            float thr = planeSize * recenterThreshold;
            if ((camXZ - _lastCenterXZ).sqrMagnitude < thr * thr) return;

            _plane.transform.position = new Vector3(camXZ.x, _surfaceY, camXZ.y);
            _lastCenterXZ = camXZ;
        }

        private GameObject CreatePlane(float y)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "SandboxOcean";
            go.transform.SetParent(transform, false);
            // Quad は XY 平面を向くので 90° 倒して XZ 水平に。
            go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            var cam = Camera.main;
            float cx = cam != null ? cam.transform.position.x : 0f;
            float cz = cam != null ? cam.transform.position.z : 0f;
            go.transform.position = new Vector3(cx, y, cz);
            go.transform.localScale = new Vector3(planeSize, planeSize, 1f);

            // 海面は接地判定を持たせない（プレイヤーが水面に立たないように）。
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = BuildWaterMaterial();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return go;
        }

        private Material BuildWaterMaterial()
        {
            var water = Shader.Find("Sandbox/WaterSurface");
            if (water != null)
            {
                var mat = new Material(water) { name = "OceanWaterMat" };
                mat.SetColor("_ShallowColor", shallowColor);
                mat.SetColor("_DeepColor", deepColor);
                mat.SetFloat("_DepthMax", depthFadeMeters);
                mat.SetFloat("_FoamDistance", foamDistance);
                mat.SetFloat("_WaveScale", waveScale);
                mat.SetFloat("_WaveSpeed", waveSpeed);
                mat.SetFloat("_WaveStrength", waveStrength);
                return mat;
            }

            // フォールバック: 専用シェーダーが未コンパイル/未検出のときは URP/Lit の不透明青で代替。
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var fallback = new Material(lit != null ? lit : Shader.Find("Sprites/Default")) { name = "OceanMatFallback" };
            if (fallback.HasProperty("_BaseColor")) fallback.SetColor("_BaseColor", deepColor);
            if (fallback.HasProperty("_Smoothness")) fallback.SetFloat("_Smoothness", fallbackSmoothness);
            if (fallback.HasProperty("_Metallic")) fallback.SetFloat("_Metallic", 0f);
            return fallback;
        }
    }
}
