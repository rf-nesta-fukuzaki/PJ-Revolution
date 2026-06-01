using UnityEngine;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// 山頂演出。SandboxSummitGoal が `SummitGoal` Trigger に AddComponent された後、
    /// 同 GameObject に光柱 + 紙吹雪 Particle を追加配置する。
    /// 山頂位置は SandboxBootstrap.ColliderBaker.GlobalMaxPos を参照。
    /// 演出は常時再生（環境演出として）。Trigger 時の更なる演出（カメラリフトなど）は別 step。
    /// </summary>
    public sealed class SummitVisualEffects : MonoBehaviour
    {
        [Header("Light Beam")]
        [SerializeField] private bool enableLightBeam = true;
        [SerializeField] private float beamHeight = 80f;
        [SerializeField] private float beamRadiusBottom = 1.5f;
        [SerializeField] private float beamRadiusTop = 6.0f;
        [SerializeField] private Color beamColor = new Color(1.0f, 0.95f, 0.65f, 0.35f);

        [Header("Particles")]
        [SerializeField] private bool enableParticles = true;
        [SerializeField] private int particleMaxCount = 80;
        [SerializeField] private float particleEmissionRate = 12f;
        [SerializeField] private float particleLifetime = 8f;
        [SerializeField] private float particleStartSpeed = 2f;
        [SerializeField] private Color particleColor = new Color(1.0f, 0.95f, 0.65f, 1f);

        private Sandbox.World.Integration.SandboxSummitGoal _summitGoal;
        private GameObject _beam;
        private GameObject _ps;
        private bool _placed;
        private Vector3 _lastSummit = new Vector3(float.MinValue, 0f, 0f);

        private void Awake() { _summitGoal = GetComponent<Sandbox.World.Integration.SandboxSummitGoal>(); }

        private void Update()
        {
            // 山頂位置は SandboxSummitGoal（唯一の真実）に追従する。
            // 旧実装は ColliderBaker.IsAllBaked(1) の早期に GlobalMaxPos を 1 度だけ採用していたため、
            // 山体チャンクが焼ける前の外周コーナー(Y≈0)に演出が取り残されていた。
            if (_summitGoal == null)
            {
                _summitGoal = GetComponent<Sandbox.World.Integration.SandboxSummitGoal>();
                if (_summitGoal == null) return;
            }
            if (!_summitGoal.HasSummit) return;

            var summit = _summitGoal.SummitPosition;
            if (!_placed)
            {
                if (enableLightBeam) _beam = CreateBeam(summit);
                if (enableParticles) _ps = CreateParticles(summit);
                _placed = true;
                _lastSummit = summit;
                Debug.Log($"[SummitVisualEffects] placed at {summit}");
            }
            else if ((summit - _lastSummit).sqrMagnitude > 0.25f)
            {
                // より高いピークが観測されたら演出も追従。
                if (_beam != null) _beam.transform.position = summit;
                if (_ps != null)   _ps.transform.position   = summit + Vector3.up * 2f;
                _lastSummit = summit;
            }
        }

        private GameObject CreateBeam(Vector3 summit)
        {
            // 円錐の Cylinder Primitive を逆さで使う（下細・上太）。
            // ベース円 = radiusBottom, 上 = radiusTop。Cylinder の default scale は (1,1,1) で直径 1、高さ 2。
            var go = new GameObject("Sandbox_SummitBeam");
            go.transform.SetParent(transform, false);
            var mesh = BuildConeMesh(beamRadiusBottom, beamRadiusTop, beamHeight, 24);
            var mf = go.AddComponent<MeshFilter>(); mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.sharedMaterial = BuildUnlitTransparentMat(beamColor);
            go.transform.position = summit;
            return go;
        }

        private static Mesh BuildConeMesh(float rBot, float rTop, float h, int seg)
        {
            seg = Mathf.Max(8, seg);
            var verts = new Vector3[seg * 2];
            var uvs = new Vector2[seg * 2];
            var tris = new int[seg * 6];
            for (int i = 0; i < seg; i++)
            {
                float t = i / (float)seg;
                float ang = t * Mathf.PI * 2f;
                float cx = Mathf.Cos(ang), cz = Mathf.Sin(ang);
                verts[i]       = new Vector3(cx * rBot, 0f, cz * rBot);
                verts[i + seg] = new Vector3(cx * rTop, h,  cz * rTop);
                uvs[i]       = new Vector2(t, 0f);
                uvs[i + seg] = new Vector2(t, 1f);
            }
            for (int i = 0; i < seg; i++)
            {
                int ni = (i + 1) % seg;
                int a = i, b = ni, c = i + seg, d = ni + seg;
                int o = i * 6;
                tris[o + 0] = a; tris[o + 1] = c; tris[o + 2] = b;
                tris[o + 3] = b; tris[o + 4] = c; tris[o + 5] = d;
            }
            var m = new Mesh { name = "SummitBeamCone" };
            m.vertices = verts;
            m.uv = uvs;
            m.triangles = tris;
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        private static Material BuildUnlitTransparentMat(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(sh) { name = "SummitBeamMat" };
            mat.SetColor("_BaseColor", c);
            // URP Unlit を Transparent に切替（Surface=1=Transparent, Blend=0=Alpha）
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHATEST_ON");
            return mat;
        }

        private GameObject CreateParticles(Vector3 summit)
        {
            var go = new GameObject("Sandbox_SummitParticles");
            go.transform.SetParent(transform, false);
            go.transform.position = summit + Vector3.up * 2f;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = particleLifetime;
            main.startSpeed = particleStartSpeed;
            main.startSize = 0.20f;
            main.startColor = particleColor;
            main.maxParticles = particleMaxCount;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = particleEmissionRate;
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 22f;
            sh.radius = 0.5f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(particleColor, 0f), new GradientColorKey(particleColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = grad;
            var sz = ps.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = BuildUnlitTransparentMat(particleColor);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return go;
        }
    }
}
