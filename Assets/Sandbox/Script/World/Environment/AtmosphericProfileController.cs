using UnityEngine;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// PEAK 級バーティカリティの環境制御。
    /// - 標高帯（雪線/岩/草/浅瀬）の色・しきい値を Shader Global として配布（TerrainBiomeSampled.shader が読む）
    /// - URP 標準フォグ（RenderSettings.fog*）をカメラ高度・時刻で連続変化
    /// - Sun（main directional light）の色温度・強度・方向を「夜明け→朝→正午→夕方→黄昏」のサイクルで補間
    /// - Skybox 用に天頂/水平/夕焼け色を Shader Global として配布（ProceduralGradientSky.shader が読む）
    ///
    /// Step 5: TimeOfDay を public プロパティ化し、DayNightCycle から駆動できるよう refactor。
    /// </summary>
    [DefaultExecutionOrder(-25)] // Bootstrap(-30) の後、レンダリングより前に走る
    public sealed class AtmosphericProfileController : MonoBehaviour
    {
        // ───── 標高帯（terrain shader が参照する shader global） ─────
        // しきい値は実際の山高（裾野 ~0-66m の転がる丘陵 / 中腹 ~277m / 山頂 ~485m）に合わせる。
        // 重要: バンド選択は _GrassLine/_RockLine/_SnowLine で行い、shore は「草地より下」。
        // 旧 grassLine=95 だと裾野(~24m)が shore(土色) 判定 → 砂漠の様に見えていた。
        // grassLine を水際近くまで下げ、裾野〜中腹を緑の高山草地にする（高山らしい tiered 配色）。
        [Header("Elevation Bands (terrain shader globals)")]
        [SerializeField] private float shoreLine = 4f;   // grass パッチ下限のみに使用（水際の土）
        [SerializeField] private float shoreBlend = 6f;
        [SerializeField] private float grassLine = 10f;  // 水際から上は緑の草地
        [SerializeField] private float grassBlend = 16f;
        [SerializeField] private float rockLine  = 200f; // 中腹から上が岩肌
        [SerializeField] private float rockBlend = 60f;
        [SerializeField] private float snowLine  = 330f; // 山頂部の冠雪
        [SerializeField] private float snowBlend = 48f;
        // biome テクスチャは青寄りでくすむため band 主体（92%）にして tiered な配色をはっきり出す。
        [Range(0f, 1f)] [SerializeField] private float bandStrength = 0.92f;

        [SerializeField] private Color colShore = new Color(0.42f, 0.39f, 0.30f, 1f); // 水際の土（稀にしか見えない）
        // (0.29,0.60,0.17) は彩度が高すぎ、+32 サチュレーションと相まってネオン緑に見えていた。
        // やや黄緑寄り・彩度控えめの自然な高山草地へ（スタイライズドの鮮やかさは残しつつ毒々しさを除く）。
        [SerializeField] private Color colGrass = new Color(0.34f, 0.52f, 0.22f, 1f); // 自然な高山草地
        [SerializeField] private Color colRock  = new Color(0.48f, 0.47f, 0.43f, 1f); // 寒色グレーの岩肌
        [SerializeField] private Color colSnow  = new Color(0.97f, 0.98f, 1.00f, 1f); // 雪白

        // ───── Fog ─────
        [Header("URP Fog")]
        [SerializeField] private bool enableFog = true;
        [SerializeField] private FogMode fogMode = FogMode.Linear;
        [SerializeField] private float fogStart = 320f;
        [SerializeField] private float fogEnd = 2800f;
        [SerializeField] private Color fogColorLow  = new Color(0.76f, 0.81f, 0.89f, 1f);
        [SerializeField] private Color fogColorHigh = new Color(0.58f, 0.71f, 0.91f, 1f);
        [SerializeField] private Color fogColorDusk = new Color(0.78f, 0.62f, 0.52f, 1f);
        [SerializeField] private float fogAltitudeMax = 120f;

        // ───── Sun（時刻別キーカラー） ─────
        [Header("Sun (main directional light)")]
        [SerializeField] private Light sun;
        [SerializeField] private Color sunDawn    = new Color(1.00f, 0.60f, 0.45f, 1f); // 0.00
        [SerializeField] private Color sunMorning = new Color(1.00f, 0.85f, 0.65f, 1f); // 0.25
        [SerializeField] private Color sunNoon    = new Color(1.00f, 0.97f, 0.92f, 1f); // 0.50
        [SerializeField] private Color sunDusk    = new Color(1.00f, 0.70f, 0.45f, 1f); // 0.75
        [SerializeField] private Color sunNight   = new Color(0.20f, 0.25f, 0.40f, 1f); // 1.00
        [SerializeField] private Vector3 sunEulerDawn    = new Vector3(  2f, 30f, 0f);
        [SerializeField] private Vector3 sunEulerMorning = new Vector3( 30f, 30f, 0f);
        [SerializeField] private Vector3 sunEulerNoon    = new Vector3( 75f, 30f, 0f);
        [SerializeField] private Vector3 sunEulerDusk    = new Vector3( 30f,210f, 0f);
        [SerializeField] private Vector3 sunEulerNight   = new Vector3(  2f,210f, 0f);
        [Range(0f, 1f)] [SerializeField] private float timeOfDay = 0.40f; // 0=夜明け 0.5=正午 1=夜
        [SerializeField] private float sunIntensityDay = 1.20f;
        [SerializeField] private float sunIntensityNight = 0.05f;
        [Tooltip("やわらかいスタイライズド影。ハード影 + strength=1 だと明るい基地台座に真っ黒な紺色の塊が出る。")]
        [SerializeField] private bool softShadows = true;
        [Tooltip("影の濃さ。1.0 は黒すぎるため 0.6 前後でやわらかい陰影にする。")]
        [Range(0f, 1f)] [SerializeField] private float shadowStrength = 0.6f;

        // ───── Ambient ─────
        [Header("Ambient")]
        // 旧 0.88 は青いフィルライトが強すぎて陰影が消え一様にくすんでいた。
        // 0.60 に下げて主光源（太陽）でコントラストと彩度を出す。
        [SerializeField] private Color ambientColorDay   = new Color(0.50f, 0.55f, 0.63f, 1f);
        [SerializeField] private Color ambientColorNight = new Color(0.10f, 0.12f, 0.20f, 1f);
        [SerializeField] private float ambientIntensity = 0.60f;

        // ───── Reflection（環境鏡面の時刻追従） ─────
        [Header("Reflection")]
        [Tooltip("空の色変化に環境反射（雪/水の鏡面・反射プローブ）を追従させる。")]
        [SerializeField] private bool refreshReflectionOverTime = true;
        [Tooltip("環境反射の再ベイク間隔[s]。空はゆっくり変化するので数秒で十分。0以下で無効。")]
        [SerializeField] private float reflectionRefreshInterval = 4f;

        // ───── Sky (procedural sky の shader global) ─────
        [Header("Sky Globals (ProceduralGradientSky reads these)")]
        [SerializeField] private bool driveSkyGlobals = true;
        // 日中の空はやや色あせて見えるため、天頂をより濃い「PEAK 級の鮮やかな青」へ、
        // 地平を白寄りの明るさへ振り、コントラストの強いスタイライズドな空にする。
        [SerializeField] private Color skyZenithDay    = new Color(0.15f, 0.45f, 0.85f, 1f);
        [SerializeField] private Color skyHorizonDay   = new Color(0.85f, 0.95f, 1.00f, 1f);
        [SerializeField] private Color skyZenithDusk   = new Color(0.10f, 0.10f, 0.35f, 1f);
        [SerializeField] private Color skyHorizonDusk  = new Color(1.00f, 0.55f, 0.30f, 1f);
        [SerializeField] private Color skyZenithNight  = new Color(0.02f, 0.03f, 0.08f, 1f);
        [SerializeField] private Color skyHorizonNight = new Color(0.06f, 0.08f, 0.16f, 1f);
        [SerializeField] private Color skyGroundColor  = new Color(0.18f, 0.20f, 0.22f, 1f);
        [SerializeField] private float skySunSize = 0.04f;
        [SerializeField] private Color skySunColor = new Color(1.0f, 0.95f, 0.80f, 1f);

        [Header("Camera Far Clip")]
        [SerializeField] private bool overrideMainCamFarClip = true;
        [SerializeField] private float farClipPlane = 1800f;

        private static readonly int IdShoreLine  = Shader.PropertyToID("_ShoreLine");
        private static readonly int IdShoreBlend = Shader.PropertyToID("_ShoreBlend");
        private static readonly int IdGrassLine  = Shader.PropertyToID("_GrassLine");
        private static readonly int IdGrassBlend = Shader.PropertyToID("_GrassBlend");
        private static readonly int IdRockLine   = Shader.PropertyToID("_RockLine");
        private static readonly int IdRockBlend  = Shader.PropertyToID("_RockBlend");
        private static readonly int IdSnowLine   = Shader.PropertyToID("_SnowLine");
        private static readonly int IdSnowBlend  = Shader.PropertyToID("_SnowBlend");
        private static readonly int IdColShore   = Shader.PropertyToID("_ColShore");
        private static readonly int IdColGrass   = Shader.PropertyToID("_ColGrass");
        private static readonly int IdColRock    = Shader.PropertyToID("_ColRock");
        private static readonly int IdColSnow    = Shader.PropertyToID("_ColSnow");
        private static readonly int IdBandStrength = Shader.PropertyToID("_BandStrength");

        // Sky globals
        private static readonly int IdSkyZenith    = Shader.PropertyToID("_SkyZenith");
        private static readonly int IdSkyHorizon   = Shader.PropertyToID("_SkyHorizon");
        private static readonly int IdSkyGround    = Shader.PropertyToID("_SkyGround");
        private static readonly int IdSkySunDir    = Shader.PropertyToID("_SkySunDir");
        private static readonly int IdSkySunColor  = Shader.PropertyToID("_SkySunColor");
        private static readonly int IdSkySunSize   = Shader.PropertyToID("_SkySunSize");

        private float _reflectionTimer;

        // 高度連動フォグ（combined シーンの CombinedTerrainConformer が有効化）。
        // 既定は無効＝Sandbox.unity 等では従来どおり固定距離で振る舞う。
        private bool _fogAltitudeDynamic;
        private float _fogStartLow, _fogEndLow, _fogStartHigh, _fogEndHigh;

        // フォグ高度の基準カメラ。null なら Camera.main（既定）。combined シーンでは残置 MainCamera ではなく
        // プレイヤー追従の CameraRig を指すよう conformer が差し込み、登坂に応じてフォグが連続変化する。
        private Camera _fogRefCam;

        public float TimeOfDay
        {
            get => timeOfDay;
            set { timeOfDay = Mathf.Clamp01(value); ApplyTimeDependent(); }
        }

        private void OnEnable()
        {
            ApplyBands();
            ApplyFogStatic();
            ApplyTimeDependent();
            if (overrideMainCamFarClip && Camera.main != null) Camera.main.farClipPlane = farClipPlane;
        }

        private void Update()
        {
            if (enableFog)
            {
                var cam = _fogRefCam != null ? _fogRefCam : Camera.main;
                float y = cam != null ? cam.transform.position.y : 0f;
                float altT = Mathf.Clamp01(y / Mathf.Max(1f, fogAltitudeMax));
                // 高度: low→high の縦補間、時刻: 0.6 以降で dusk 寄せ
                var byAlt = Color.Lerp(fogColorLow, fogColorHigh, altT);
                float duskT = Mathf.Clamp01((timeOfDay - 0.6f) * 4f); // 0.6〜0.85 で dusk へ
                RenderSettings.fogColor = Color.Lerp(byAlt, fogColorDusk, duskT);

                // 高度連動で距離も補間: 谷=遠ざけて白飛び回避 / 高所=近づけて空気遠近の奥行き。
                if (_fogAltitudeDynamic)
                {
                    RenderSettings.fogStartDistance = Mathf.Lerp(_fogStartLow, _fogStartHigh, altT);
                    RenderSettings.fogEndDistance   = Mathf.Lerp(_fogEndLow,   _fogEndHigh,   altT);
                }
            }

            // 環境反射を空の色変化に追従（プロシージャル空のみ・間引き再ベイク）。
            // ApplySkyGlobals が skybox の色 global を更新済みなので、ここで cubemap を焼き直す。
            if (refreshReflectionOverTime && driveSkyGlobals && reflectionRefreshInterval > 0f)
            {
                _reflectionTimer += Time.deltaTime;
                if (_reflectionTimer >= reflectionRefreshInterval)
                {
                    _reflectionTimer = 0f;
                    DynamicGI.UpdateEnvironment();
                }
            }
        }

        /// <summary>TimeOfDay 依存（Sun/Ambient/Sky globals）を再計算する。</summary>
        public void ApplyTimeDependent()
        {
            ApplySun();
            ApplyAmbient();
            if (driveSkyGlobals) ApplySkyGlobals();
        }

        private void ApplyBands()
        {
            Shader.SetGlobalFloat(IdShoreLine,  shoreLine);
            Shader.SetGlobalFloat(IdShoreBlend, shoreBlend);
            Shader.SetGlobalFloat(IdGrassLine,  grassLine);
            Shader.SetGlobalFloat(IdGrassBlend, grassBlend);
            Shader.SetGlobalFloat(IdRockLine,   rockLine);
            Shader.SetGlobalFloat(IdRockBlend,  rockBlend);
            Shader.SetGlobalFloat(IdSnowLine,   snowLine);
            Shader.SetGlobalFloat(IdSnowBlend,  snowBlend);
            Shader.SetGlobalColor(IdColShore,   colShore);
            Shader.SetGlobalColor(IdColGrass,   colGrass);
            Shader.SetGlobalColor(IdColRock,    colRock);
            Shader.SetGlobalColor(IdColSnow,    colSnow);
            Shader.SetGlobalFloat(IdBandStrength, bandStrength);
        }

        private void ApplyFogStatic()
        {
            RenderSettings.fog = enableFog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.fogColor = fogColorLow;
        }

        /// <summary>
        /// 外部からフォグの距離・色を上書きする（CombinedTerrainConformer が低高度の白飛びを緩和するために使用）。
        /// 毎フレーム RenderSettings へ再適用されるため、フィールド側を書き換える必要がある。
        /// </summary>
        public void OverrideFog(float start, float end, Color low, Color high)
        {
            fogStart = start;
            fogEnd = end;
            fogColorLow = low;
            fogColorHigh = high;
            _fogAltitudeDynamic = false;
            ApplyFogStatic();
        }

        /// <summary>
        /// 高度連動でフォグ距離を動的に上書きする（低高度白飛び対策の発展版）。
        /// 谷（低高度 y≈0）では距離を遠ざけて白飛びを抑え、高所（y≈altitudeMax）では近づけて空気遠近の奥行きを出す。
        /// 色の low→high ブレンド基準高度(fogAltitudeMax)も山全体を覆う値へ広げ、標高に応じて連続変化させる。
        /// 以降は毎フレーム Update でカメラ高度に応じて距離・色が再計算される。
        /// </summary>
        public void OverrideFogAltitudeAware(float startLow, float endLow, float startHigh, float endHigh,
                                             Color low, Color high, float altitudeMax)
        {
            _fogStartLow = startLow; _fogEndLow = endLow;
            _fogStartHigh = startHigh; _fogEndHigh = endHigh;
            fogColorLow = low; fogColorHigh = high;
            fogAltitudeMax = Mathf.Max(1f, altitudeMax);
            _fogAltitudeDynamic = true;
            fogStart = startLow; fogEnd = endLow; // 初期（谷）値を即適用
            ApplyFogStatic();
        }

        /// <summary>
        /// フォグ高度の基準カメラを差し替える（combined シーンで残置 MainCamera ではなくプレイヤー追従カメラを
        /// 参照させる）。null で Camera.main に戻る。Sandbox.unity 等は呼ばないため従来挙動のまま。
        /// </summary>
        public void SetFogReferenceCamera(Camera cam) => _fogRefCam = cam;

        // timeOfDay [0,1] を 5 キー（dawn / morning / noon / dusk / night）で補間。
        private static Color LerpFiveKeys(float t, Color a, Color b, Color c, Color d, Color e)
        {
            // セグメント: 0.00→0.25→0.50→0.75→1.00
            if (t < 0.25f) return Color.Lerp(a, b, t / 0.25f);
            if (t < 0.50f) return Color.Lerp(b, c, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Color.Lerp(c, d, (t - 0.50f) / 0.25f);
            return Color.Lerp(d, e, (t - 0.75f) / 0.25f);
        }
        private static Vector3 LerpFiveKeysV(float t, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e)
        {
            if (t < 0.25f) return Vector3.Lerp(a, b, t / 0.25f);
            if (t < 0.50f) return Vector3.Lerp(b, c, (t - 0.25f) / 0.25f);
            if (t < 0.75f) return Vector3.Lerp(c, d, (t - 0.50f) / 0.25f);
            return Vector3.Lerp(d, e, (t - 0.75f) / 0.25f);
        }

        private void ApplySun()
        {
            if (sun == null) sun = FindMainDirectional();
            if (sun == null) return;
            // やわらかいスタイライズド影に統一（ハード影 strength=1 の真っ黒な塊を避ける）。
            sun.shadows = softShadows ? LightShadows.Soft : LightShadows.Hard;
            sun.shadowStrength = shadowStrength;
            sun.color = LerpFiveKeys(timeOfDay, sunDawn, sunMorning, sunNoon, sunDusk, sunNight);
            // 夜帯（0.85〜1.0）で減衰
            float nightT = Mathf.Clamp01((timeOfDay - 0.85f) / 0.15f);
            sun.intensity = Mathf.Lerp(sunIntensityDay, sunIntensityNight, nightT);
            var euler = LerpFiveKeysV(timeOfDay, sunEulerDawn, sunEulerMorning, sunEulerNoon, sunEulerDusk, sunEulerNight);
            sun.transform.rotation = Quaternion.Euler(euler);
        }

        private void ApplyAmbient()
        {
            float nightT = Mathf.Clamp01((timeOfDay - 0.7f) / 0.3f);
            var ambient = Color.Lerp(ambientColorDay, ambientColorNight, nightT) * ambientIntensity;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientLight = ambient;
            RenderSettings.ambientSkyColor = ambient * 1.1f;
            RenderSettings.ambientEquatorColor = ambient;
            RenderSettings.ambientGroundColor = ambient * 0.6f;
        }

        private void ApplySkyGlobals()
        {
            // Day(0.25〜0.6) → Dusk(0.75) → Night(0.95+)
            Color zenith, horizon;
            if (timeOfDay < 0.60f)
            {
                float t = Mathf.Clamp01(timeOfDay / 0.60f);
                zenith  = Color.Lerp(skyZenithDusk * 0.6f, skyZenithDay, t);
                horizon = Color.Lerp(skyHorizonDusk * 0.6f, skyHorizonDay, t);
            }
            else if (timeOfDay < 0.85f)
            {
                float t = Mathf.Clamp01((timeOfDay - 0.60f) / 0.25f);
                zenith  = Color.Lerp(skyZenithDay, skyZenithDusk, t);
                horizon = Color.Lerp(skyHorizonDay, skyHorizonDusk, t);
            }
            else
            {
                float t = Mathf.Clamp01((timeOfDay - 0.85f) / 0.15f);
                zenith  = Color.Lerp(skyZenithDusk, skyZenithNight, t);
                horizon = Color.Lerp(skyHorizonDusk, skyHorizonNight, t);
            }
            Shader.SetGlobalColor(IdSkyZenith,   zenith);
            Shader.SetGlobalColor(IdSkyHorizon,  horizon);
            Shader.SetGlobalColor(IdSkyGround,   skyGroundColor);
            Shader.SetGlobalColor(IdSkySunColor, skySunColor);
            Shader.SetGlobalFloat(IdSkySunSize,  skySunSize);
            if (sun != null)
            {
                // Sun direction = sun が照らす方向の逆（光源位置方向）
                var dir = -sun.transform.forward;
                Shader.SetGlobalVector(IdSkySunDir, new Vector4(dir.x, dir.y, dir.z, 0f));
            }
        }

        private static Light FindMainDirectional()
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            foreach (var l in lights) if (l.type == LightType.Directional) return l;
            return null;
        }
    }
}
