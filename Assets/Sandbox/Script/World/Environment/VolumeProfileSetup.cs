using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// URP の Volume System を runtime セットアップする。
    /// - Global Volume を 1 つ作り、VolumeProfile に映画的なポストプロセスのフルスタックを構築
    /// - Tonemapping(ACES) / ColorAdjustments / Bloom / Vignette に加え、AAA 級の色深度のため
    ///   White Balance（暖色の昼光）/ Split Toning（ハイライト暖・シャドウ寒）/ Film Grain /
    ///   Chromatic Aberration を控えめに追加。
    /// - 既定値は山岳シーンの「澄んだ大気」「自然な発光」「フィルミックな色域」に寄せた値
    /// - パッケージ未導入の環境では `using UnityEngine.Rendering.Universal;` でビルドエラーになるが、
    ///   このプロジェクトは URP 前提（CLAUDE.md「Unity 6.3 URP」）なので問題なし
    /// </summary>
    public sealed class VolumeProfileSetup : MonoBehaviour
    {
        [Header("Bloom")]
        [SerializeField] private bool enableBloom = true;
        // 雪/日向斜面の輝度は 1.0 を超えるため、低い閾値だと細い冠雪が『発光する幽霊シャード』に化ける。
        // 閾値を上げ強度を抑え、真のハイライト（太陽・水面のきらめき）だけがにじむようにする。
        [SerializeField] private float bloomIntensity = 0.34f;
        [SerializeField] private float bloomThreshold = 1.40f;
        [SerializeField] private float bloomScatter = 0.62f;
        [SerializeField] private Color bloomTint = new Color(1.0f, 0.97f, 0.90f, 1f); // ハイライトに僅かな暖色

        [Header("Vignette")]
        [SerializeField] private bool enableVignette = true;
        [SerializeField] private Color vignetteColor = new Color(0.02f, 0.02f, 0.04f, 1f);
        [Range(0f, 1f)] [SerializeField] private float vignetteIntensity = 0.20f;
        [Range(0.01f, 1f)] [SerializeField] private float vignetteSmoothness = 0.40f;

        [Header("Color Adjustments")]
        [SerializeField] private bool enableColorAdjust = true;
        [Range(-100f, 100f)] [SerializeField] private float postExposure = 0.05f;
        // Split Toning で奥行きを出すぶんコントラストはやや控えめに（潰れ防止）。
        [Range(-100f, 100f)] [SerializeField] private float contrast = 14f;
        [SerializeField] private Color colorFilter = new Color(1.00f, 0.98f, 0.94f, 1f);
        // +32 は強い日射 + ACES と相まって草地がネオン蛍光色に転んだ（実写キャプチャで確認）。
        // PEAK 風の鮮やかさは保ちつつ毒々しさを避けるため +18 に抑える。
        [Range(-100f, 100f)] [SerializeField] private float saturation = 18f;

        [Header("White Balance")]
        [SerializeField] private bool enableWhiteBalance = true;
        // 昼光をわずかに暖色へ（雪山の青被りを補正し、心地よい暖かさを与える）。
        [Range(-100f, 100f)] [SerializeField] private float temperature = 6f;
        [Range(-100f, 100f)] [SerializeField] private float tint = -2f;

        [Header("Split Toning")]
        [SerializeField] private bool enableSplitToning = true;
        // ハイライト＝暖色（日向）、シャドウ＝寒色（空のフィル）。映画的な色分離で立体感を底上げ。
        [SerializeField] private Color splitShadows    = new Color(0.42f, 0.52f, 0.68f, 1f);
        [SerializeField] private Color splitHighlights = new Color(1.00f, 0.86f, 0.62f, 1f);
        [Range(-100f, 100f)] [SerializeField] private float splitBalance = -8f;

        [Header("Film Grain")]
        [SerializeField] private bool enableFilmGrain = true;
        [Range(0f, 1f)] [SerializeField] private float filmGrainIntensity = 0.13f;
        [Range(0f, 1f)] [SerializeField] private float filmGrainResponse = 0.80f;

        [Header("Chromatic Aberration")]
        [SerializeField] private bool enableChromaticAberration = true;
        [Range(0f, 1f)] [SerializeField] private float chromaticAberration = 0.06f;

        [Header("Tonemapping")]
        [SerializeField] private bool enableTonemapping = true;
        // ACES はハイライトのロールオフがフィルミックで、雪/日向斜面の白飛びを抑え映画的に見える。
        [SerializeField] private TonemappingMode tonemappingMode = TonemappingMode.ACES;

        private GameObject _volumeGo;
        private Volume _volume;
        private VolumeProfile _profile;

        private void OnEnable() { Build(); }
        private void OnDisable() { Teardown(); }

        private void Build()
        {
            if (_volumeGo != null) return;
            _volumeGo = new GameObject("Sandbox_GlobalVolume");
            _volumeGo.transform.SetParent(transform, false);
            _volume = _volumeGo.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 10f;
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _profile.name = "SandboxVolumeProfile";
            _volume.sharedProfile = _profile;

            if (enableTonemapping)
            {
                var tm = _profile.Add<Tonemapping>(true);
                tm.mode.Override(tonemappingMode);
            }
            if (enableWhiteBalance)
            {
                var wb = _profile.Add<WhiteBalance>(true);
                wb.temperature.Override(temperature);
                wb.tint.Override(tint);
            }
            if (enableColorAdjust)
            {
                var ca = _profile.Add<ColorAdjustments>(true);
                ca.postExposure.Override(postExposure);
                ca.contrast.Override(contrast);
                ca.colorFilter.Override(colorFilter);
                ca.saturation.Override(saturation);
            }
            if (enableSplitToning)
            {
                var st = _profile.Add<SplitToning>(true);
                st.shadows.Override(splitShadows);
                st.highlights.Override(splitHighlights);
                st.balance.Override(splitBalance);
            }
            if (enableBloom)
            {
                var bl = _profile.Add<Bloom>(true);
                bl.intensity.Override(bloomIntensity);
                bl.threshold.Override(bloomThreshold);
                bl.scatter.Override(bloomScatter);
                bl.tint.Override(bloomTint);
            }
            if (enableChromaticAberration)
            {
                var cab = _profile.Add<ChromaticAberration>(true);
                cab.intensity.Override(chromaticAberration);
            }
            if (enableFilmGrain)
            {
                var fg = _profile.Add<FilmGrain>(true);
                fg.type.Override(FilmGrainLookup.Medium1);
                fg.intensity.Override(filmGrainIntensity);
                fg.response.Override(filmGrainResponse);
            }
            if (enableVignette)
            {
                var vg = _profile.Add<Vignette>(true);
                vg.color.Override(vignetteColor);
                vg.intensity.Override(vignetteIntensity);
                vg.smoothness.Override(vignetteSmoothness);
            }
        }

        private void Teardown()
        {
            if (_profile != null) { Object.Destroy(_profile); _profile = null; }
            if (_volumeGo != null) { Object.Destroy(_volumeGo); _volumeGo = null; }
            _volume = null;
        }
    }
}
