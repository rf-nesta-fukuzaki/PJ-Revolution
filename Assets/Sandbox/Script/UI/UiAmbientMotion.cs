using UnityEngine;
using UnityEngine.UI;

namespace Sandbox.UI
{
    /// <summary>
    /// 手続き UI 背景の微動（PEAK 夕日パルス / 山パララックス / MIMESIS スキャンライン）。
    /// FlowUiTheme.CreateSceneBackdrop 後に Attach する。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiAmbientMotion : MonoBehaviour
    {
        public enum Profile { TitlePeak, ShopRepo, LoadingMimesis, ResultPeak }

        [SerializeField] private Profile _profile = Profile.TitlePeak;

        private Image _sunGlow;
        private RectTransform _mountainFar;
        private RectTransform _mountainNear;
        private RectTransform _scanlineRoot;
        private Image _tealWash;
        private Image _vignettePulse;

        public static UiAmbientMotion Attach(Transform backdropRoot, Profile profile)
        {
            if (backdropRoot == null) return null;
            var motion = backdropRoot.GetComponent<UiAmbientMotion>();
            if (motion == null) motion = backdropRoot.gameObject.AddComponent<UiAmbientMotion>();
            motion._profile = profile;
            motion.CacheRefs(backdropRoot);
            return motion;
        }

        private void CacheRefs(Transform root)
        {
            _sunGlow = FindImage(root, "PeakSunGlow");
            _mountainFar = FindRect(root, "MountainSilhouette/Peak");
            _mountainNear = FindRect(root, "MountainSilhouette");
            _scanlineRoot = FindRect(root, "Scanlines");
            _tealWash = FindImage(root, "MimicWash");
            _vignettePulse = FindImage(root, "Vignette");
        }

        private void Update()
        {
            float t = Time.unscaledTime;

            if (_sunGlow != null && (_profile == Profile.TitlePeak || _profile == Profile.ResultPeak))
            {
                float pulse = 0.10f + 0.04f * Mathf.Sin(t * 0.85f);
                var c = _sunGlow.color;
                _sunGlow.color = new Color(c.r, c.g, c.b, pulse);
            }

            if (_mountainFar != null)
            {
                float dx = Mathf.Sin(t * 0.22f) * 6f;
                _mountainFar.anchoredPosition = new Vector2(dx * 0.4f, 0f);
            }
            if (_mountainNear != null && _mountainNear != _mountainFar)
            {
                float dx = Mathf.Sin(t * 0.18f + 1.2f) * 10f;
                _mountainNear.anchoredPosition = new Vector2(dx, 0f);
            }

            if (_scanlineRoot != null && _profile == Profile.LoadingMimesis)
            {
                float scroll = (t * 28f) % 40f;
                _scanlineRoot.anchoredPosition = new Vector2(0f, scroll);
            }

            if (_tealWash != null && _profile == Profile.LoadingMimesis)
            {
                float flicker = 0.06f + 0.04f * Mathf.PerlinNoise(t * 1.8f, 0.4f);
                var c = _tealWash.color;
                _tealWash.color = new Color(c.r, c.g, c.b, flicker);
            }

            if (_vignettePulse != null && _profile == Profile.LoadingMimesis)
            {
                var edges = _vignettePulse.GetComponentsInChildren<Image>(true);
                float vig = 0.48f + 0.08f * Mathf.Sin(t * 2.1f);
                foreach (var img in edges)
                {
                    if (img.gameObject.name.StartsWith("VigEdge"))
                        img.color = new Color(0f, 0f, 0f, vig);
                }
            }
        }

        private static Image FindImage(Transform root, string path)
        {
            var t = root.Find(path);
            return t != null ? t.GetComponent<Image>() : null;
        }

        private static RectTransform FindRect(Transform root, string path)
        {
            var t = root.Find(path);
            return t != null ? t as RectTransform : null;
        }
    }
}
