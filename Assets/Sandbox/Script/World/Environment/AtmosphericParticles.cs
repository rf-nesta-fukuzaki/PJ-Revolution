using UnityEngine;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// AAA オープンワールド級の「大気の生命感」を出す環境パーティクル。完全手続き的（外部アセット不要）。
    ///
    /// - メインカメラに追従する world-simulation のパーティクルボックスを生成し、登坂しても常に
    ///   プレイヤー周囲に微細な浮遊物が舞う。
    /// - 高度（MountainProfile.Fraction）で表情を切替える:
    ///     低地  : 暖色のほこり/花粉がゆっくり漂う（穏やかな日向の空気感）
    ///     中腹〜高地: 風に流される白い雪片へ連続ブレンド（風速・量も増す）
    /// - ノイズモジュールで自然な渦・ゆらぎ、velocity で風の流れを与える。
    /// - 描画は URP Particles/Unlit + 手続き soft-dot テクスチャ。シェーダ未検出時は静かに無効化。
    ///
    /// SandboxBootstrap(autoAttachAtmosphere) が実行時 AddComponent する。値はフィールド初期化子が真。
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class AtmosphericParticles : MonoBehaviour
    {
        [Header("Box around camera (world units)")]
        [SerializeField] private float boxXZ = 46f;
        [SerializeField] private float boxHeight = 30f;
        [SerializeField] private float followLead = 6f;     // 視線方向へ少し先回りして配置

        [Header("Dust (low altitude)")]
        [SerializeField] private float dustRate = 14f;      // particles/sec
        [SerializeField] private Color dustColor = new Color(0.96f, 0.90f, 0.72f, 0.18f);
        [SerializeField] private float dustSize = 0.05f;

        [Header("Snow (high altitude)")]
        [SerializeField] private float snowRate = 150f;     // particles/sec at full altitude
        [SerializeField] private Color snowColor = new Color(0.95f, 0.97f, 1.00f, 0.55f);
        [SerializeField] private float snowSize = 0.11f;

        [Header("Altitude blend (MountainProfile.Fraction)")]
        [Tooltip("この割合から雪へブレンド開始。")]
        [SerializeField] private float snowStartFraction = 0.40f;
        [Tooltip("この割合で完全に雪。")]
        [SerializeField] private float snowFullFraction = 0.80f;

        [Header("Fallback altitude (MountainProfile 未準備時)")]
        [SerializeField] private float fallbackBaseY = 50f;
        [SerializeField] private float fallbackSummitY = 760f;

        [Header("Wind")]
        [SerializeField] private float windLow = 0.6f;      // dust の弱い流れ
        [SerializeField] private float windHigh = 4.5f;     // snow の強い流れ
        [SerializeField] private Vector3 windDir = new Vector3(1f, -0.35f, 0.4f);

        private ParticleSystem _ps;
        private ParticleSystem.EmissionModule _emission;
        private ParticleSystem.MainModule _main;
        private ParticleSystem.VelocityOverLifetimeModule _vel;
        private Material _mat;
        private Texture2D _dot;
        private Camera _cam;
        private bool _ready;

        private void OnEnable()
        {
            Build();
        }

        private void OnDisable()
        {
            if (_ps != null) { Object.Destroy(_ps.gameObject); _ps = null; }
            if (_mat != null) { Object.Destroy(_mat); _mat = null; }
            if (_dot != null) { Object.Destroy(_dot); _dot = null; }
            _ready = false;
        }

        private void Build()
        {
            if (_ready) return;
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            if (shader == null)
            {
                Debug.LogWarning("[AtmosphericParticles] particle shader not found; disabling.");
                enabled = false;
                return;
            }

            _dot = BuildSoftDot();
            _mat = new Material(shader) { name = "AtmosphericParticleMat" };
            if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", _dot);
            if (_mat.HasProperty("_MainTex")) _mat.SetTexture("_MainTex", _dot);
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", Color.white);
            // 透明アルファブレンドへ（URP Particles/Unlit）。
            if (_mat.HasProperty("_Surface")) _mat.SetFloat("_Surface", 1f);
            if (_mat.HasProperty("_Blend")) _mat.SetFloat("_Blend", 0f);
            if (_mat.HasProperty("_ZWrite")) _mat.SetFloat("_ZWrite", 0f);
            _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _mat.renderQueue = 3100;

            var go = new GameObject("Sandbox_AtmosphericParticles");
            go.transform.SetParent(transform, false);
            _ps = go.AddComponent<ParticleSystem>();
            _ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            _main = _ps.main;
            _main.simulationSpace = ParticleSystemSimulationSpace.World;
            _main.startLifetime = 7f;
            _main.startSpeed = 0f;
            _main.startSize = dustSize;
            _main.startColor = dustColor;
            _main.gravityModifier = 0.01f;
            _main.maxParticles = 900;

            _emission = _ps.emission;
            _emission.rateOverTime = 0f;

            var shape = _ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(boxXZ, boxHeight, boxXZ);

            _vel = _ps.velocityOverLifetime;
            _vel.enabled = true;
            _vel.space = ParticleSystemSimulationSpace.World;
            _vel.x = new ParticleSystem.MinMaxCurve(windDir.x * windLow);
            _vel.y = new ParticleSystem.MinMaxCurve(windDir.y * windLow);
            _vel.z = new ParticleSystem.MinMaxCurve(windDir.z * windLow);

            var noise = _ps.noise;
            noise.enabled = true;
            noise.strength = 0.55f;
            noise.frequency = 0.22f;
            noise.scrollSpeed = 0.35f;
            noise.quality = ParticleSystemNoiseQuality.Medium;

            var renderer = _ps.GetComponent<ParticleSystemRenderer>();
            renderer.material = _mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortMode = ParticleSystemSortMode.None;
            renderer.alignment = ParticleSystemRenderSpace.View;

            _ps.Play();
            _ready = true;
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) return;

            // カメラに追従（視線方向へ少し先回り、ボックス中心が頭上やや前方）
            Vector3 fwd = _cam.transform.forward; fwd.y = 0f; fwd = fwd.sqrMagnitude > 1e-3f ? fwd.normalized : Vector3.forward;
            transform.position = _cam.transform.position + fwd * followLead + Vector3.up * (boxHeight * 0.25f);

            // 高度割合で dust→snow をブレンド
            float y = _cam.transform.position.y;
            float frac = MountainProfile.IsReady
                ? MountainProfile.Fraction(y)
                : Mathf.InverseLerp(fallbackBaseY, fallbackSummitY, y);
            float snow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(snowStartFraction, snowFullFraction, frac));

            _emission.rateOverTime = Mathf.Lerp(dustRate, snowRate, snow);
            _main.startColor = Color.Lerp(dustColor, snowColor, snow);
            _main.startSize = Mathf.Lerp(dustSize, snowSize, snow);

            float wind = Mathf.Lerp(windLow, windHigh, snow);
            // 緩いガスト（時間で微変動）
            float gust = 1f + 0.35f * Mathf.Sin(Time.time * 0.4f);
            _vel.x = new ParticleSystem.MinMaxCurve(windDir.x * wind * gust);
            _vel.y = new ParticleSystem.MinMaxCurve(windDir.y * wind);
            _vel.z = new ParticleSystem.MinMaxCurve(windDir.z * wind * gust);
        }

        private static Texture2D BuildSoftDot()
        {
            const int N = 32;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { name = "SoftDot", wrapMode = TextureWrapMode.Clamp };
            float c = (N - 1) * 0.5f;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x - c) / c, dy = (y - c) / c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // smoothstep falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply(false, false);
            return tex;
        }
    }
}
