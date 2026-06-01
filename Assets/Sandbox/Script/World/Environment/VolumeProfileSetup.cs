using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// URP の Volume System を runtime セットアップする。
    /// - Global Volume を 1 つ作り、VolumeProfile に Bloom / Vignette / Color Adjustments / Tonemapping を追加
    /// - 既定値は山岳シーンの「澄んだ大気」「自然な発光」に寄せた値
    /// - パッケージ未導入の環境では `using UnityEngine.Rendering.Universal;` でビルドエラーになるが、
    ///   このプロジェクトは URP 前提（CLAUDE.md「Unity 6.3 URP」）なので問題なし
    /// </summary>
    public sealed class VolumeProfileSetup : MonoBehaviour
    {
        [Header("Bloom")]
        [SerializeField] private bool enableBloom = true;
        [SerializeField] private float bloomIntensity = 0.45f;
        [SerializeField] private float bloomThreshold = 1.05f;
        [SerializeField] private float bloomScatter = 0.65f;

        [Header("Vignette")]
        [SerializeField] private bool enableVignette = true;
        [SerializeField] private Color vignetteColor = new Color(0.02f, 0.02f, 0.04f, 1f);
        [Range(0f, 1f)] [SerializeField] private float vignetteIntensity = 0.18f;
        [Range(0.01f, 1f)] [SerializeField] private float vignetteSmoothness = 0.35f;

        [Header("Color Adjustments")]
        [SerializeField] private bool enableColorAdjust = true;
        [Range(-100f, 100f)] [SerializeField] private float postExposure = 0.05f;
        [Range(-100f, 100f)] [SerializeField] private float contrast = 16f;
        [SerializeField] private Color colorFilter = new Color(1.00f, 0.98f, 0.94f, 1f);
        // +32 は強い日射 + ACES と相まって草地がネオン蛍光色に転んだ（実写キャプチャで確認）。
        // PEAK 風の鮮やかさは保ちつつ毒々しさを避けるため +18 に抑える。
        [Range(-100f, 100f)] [SerializeField] private float saturation = 18f;

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
            if (enableColorAdjust)
            {
                var ca = _profile.Add<ColorAdjustments>(true);
                ca.postExposure.Override(postExposure);
                ca.contrast.Override(contrast);
                ca.colorFilter.Override(colorFilter);
                ca.saturation.Override(saturation);
            }
            if (enableBloom)
            {
                var bl = _profile.Add<Bloom>(true);
                bl.intensity.Override(bloomIntensity);
                bl.threshold.Override(bloomThreshold);
                bl.scatter.Override(bloomScatter);
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
