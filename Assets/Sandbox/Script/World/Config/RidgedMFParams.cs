using UnityEngine;

namespace Sandbox.World.Config
{
    /// <summary>
    /// Step 2 で確定した Ridged Multifractal 全パラメータ。
    /// </summary>
    [CreateAssetMenu(menuName = "PJ-Revolution/World/Ridged Multifractal Params",
                     fileName = "RidgedMFParams")]
    public sealed class RidgedMFParams : ScriptableObject
    {
        [Header("Base FBM")]
        [Min(0.0001f)] public float baseFrequency = 0.0035f;
        [Range(3, 12)] public int octaves = 8;
        [Range(1.5f, 3.0f)] public float lacunarity = 2.07f;
        [Range(0.5f, 1.2f)] public float H = 0.95f;

        [Header("Ridge")]
        [Range(0.5f, 1.5f)] public float ridgeOffset = 1.0f;
        [Range(0.5f, 3.0f)] public float gain = 2.0f;

        [Header("Mountain Mask")]
        [Min(0.0001f)] public float mountainMaskFreq = 0.0004f;
        public Vector2 mountainMaskThreshold = new Vector2(0.2f, 0.7f);

        [Header("Single Mountain (Open World)")]
        [Tooltip("ON のとき、複数の山ではなくワールド中心に巨大な単一の山を生成する。")]
        public bool singleMountainMode = true;
        [Tooltip("単一山の中心ワールド座標 (XZ)[m]。")]
        public Vector2 mountainCenter = Vector2.zero;
        [Tooltip("単一山の裾野までの半径 [m]。この距離で標高 0 に落ちる。")]
        [Min(1f)] public float mountainRadius = 1300f;
        [Tooltip("放射状フォールオフの形状。>1 で頂上が尖り裾野が広がる、<1 で台地状。")]
        [Range(0.5f, 4f)] public float mountainFalloff = 1.4f;
        [Tooltip("山の外周を不規則化する低周波ノイズの振幅 [m]。円錐臭さを消す。")]
        [Min(0f)] public float mountainEdgeNoise = 260f;
        [Tooltip("尾根レリーフの基底比率 (0〜1)。山体全体が単一の塊として見えるよう、ドーム標高に対し尾根が乗る割合を決める。")]
        [Range(0f, 1f)] public float mountainReliefBase = 0.55f;

        [Header("Island / Ocean")]
        [Tooltip("ON のとき、mountainRadius より外側の地形を海面下（海底）へ沈め、島が海に囲まれた形にする。OFF だと旧来どおり外周に無限の丘陵が続く。")]
        public bool islandMode = true;
        [Tooltip("海岸線（mountainRadius）より外で地形が沈む深さ [m]。海面(seaLevel)からの最大下降量。")]
        [Min(0f)] public float seabedDepth = 160f;
        [Tooltip("海岸線から海底の最深部へ到達するまでの距離 [m]。小さいほど急な岸壁、大きいほど遠浅。")]
        [Min(1f)] public float seabedFalloffDistance = 240f;
        [Tooltip("島の縁（海岸付近）で丘陵レリーフを減衰させる強さ。1=海岸でほぼ平坦なビーチ、0=減衰なし。")]
        [Range(0f, 1f)] public float shoreFlatten = 1f;

        [Header("Domain Warp")]
        [Min(0f)] public float domainWarpFreq = 0.002f;
        [Min(0f)] public float domainWarpAmp = 30f;

        [Header("Height Profile")]
        public AnimationCurve heightProfileCurve = new AnimationCurve(
            new Keyframe(0f, 0f, 0f, 1f),
            new Keyframe(0.6f, 0.3f, 1f, 1f),
            new Keyframe(1f, 1f, 2f, 0f)
        );
        public float peakAltitude = 600f;
        public float seaLevel = 0f;

        [Header("Micro Detail")]
        [Min(0f)] public float microFreq = 0.25f;
        [Min(0f)] public float microAmp = 0.4f;

        [Header("Seed / Origin")]
        public uint worldSeed = 0x9E37u;
        [Tooltip("ノイズ入力座標のスナップ単位 [m]。float 精度対策。")]
        public float generationGridSize = 1024f;

        [Header("LOD")]
        [Range(0, 4)] public int octaveDropPerLod = 2;
        [Range(3, 12)] public int maxOctavesAtLod0 = 10;

        [System.NonSerialized] private Texture2D _profileLut;
        private const int LutSize = 256;

        public int EffectiveOctaves(int lod)
        {
            int oct = octaves - lod * octaveDropPerLod;
            return Mathf.Clamp(oct, 3, maxOctavesAtLod0);
        }

        public Texture2D GetOrBuildHeightProfileLut()
        {
            if (_profileLut == null || _profileLut.width != LutSize)
            {
                _profileLut = new Texture2D(LutSize, 1, TextureFormat.RHalf, false, true)
                {
                    name = "HeightProfileLUT",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            var colors = new Color[LutSize];
            for (int i = 0; i < LutSize; i++)
            {
                float t = i / (float)(LutSize - 1);
                colors[i] = new Color(heightProfileCurve.Evaluate(t), 0f, 0f, 1f);
            }
            _profileLut.SetPixels(colors);
            _profileLut.Apply(false, false);
            return _profileLut;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (domainWarpAmp < 0f) domainWarpAmp = 0f;
            _profileLut = null; // 強制再ベイク
        }
#endif
    }
}
