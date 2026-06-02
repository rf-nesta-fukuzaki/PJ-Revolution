using System.Collections;
using UnityEngine;

namespace Sandbox.UI
{
    /// <summary>
    /// 軽量な UI ジュース（手触り）ヘルパー。
    /// DOTween 等の外部依存を増やさず、プロジェクト既存の
    /// 「コルーチン + Mathf.Lerp」慣習（例: RelicDiscoveryNotifier）に合わせる。
    /// すべて Time.unscaledDeltaTime 基準なので、ポーズ中でも UI は動く。
    /// 値はすべて数値で決定的なので、画面を見られなくても論理検証できる。
    /// </summary>
    public static class UiJuice
    {
        // バウンス量・速度の既定値（マジックナンバーを避けるため定数化）。
        public const float DefaultPopOvershoot = 1.12f;
        public const float DefaultPopDuration  = 0.28f;
        public const float DefaultPunchStrength = 0.18f;
        public const float DefaultPunchDuration = 0.22f;

        /// <summary>
        /// 0 → overshoot → 1.0 のスケールバウンスで「ポンッ」と出現させる。
        /// 任意で CanvasGroup を 0→1 でフェードインする。
        /// </summary>
        public static IEnumerator PopIn(RectTransform target, CanvasGroup group = null,
            float overshoot = DefaultPopOvershoot, float duration = DefaultPopDuration)
        {
            if (target == null) yield break;

            float half = Mathf.Max(0.0001f, duration * 0.5f);
            target.localScale = Vector3.zero;
            if (group != null) group.alpha = 0f;

            // 立ち上がり: 0 → overshoot（同時にフェードイン）
            yield return ScaleLerp(target, 0f, overshoot, half, group, 0f, 1f);
            // 収束: overshoot → 1.0
            yield return ScaleLerp(target, overshoot, 1f, half, null, 0f, 0f);

            target.localScale = Vector3.one;
            if (group != null) group.alpha = 1f;
        }

        /// <summary>
        /// 一瞬だけ拡大して戻る「パンチ」。値が更新された瞬間など注目を集めたいときに使う。
        /// sin(πp) の山を (1-p) で減衰させ、自然なオーバーシュート1回にする。
        /// </summary>
        public static IEnumerator Punch(RectTransform target,
            float strength = DefaultPunchStrength, float duration = DefaultPunchDuration)
        {
            if (target == null) yield break;

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float s = 1f + Mathf.Sin(p * Mathf.PI) * strength * (1f - p);
                target.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            target.localScale = Vector3.one;
        }

        private static IEnumerator ScaleLerp(RectTransform target, float from, float to,
            float duration, CanvasGroup group, float alphaFrom, float alphaTo)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = Mathf.SmoothStep(0f, 1f, p);
                float s = Mathf.Lerp(from, to, eased);
                target.localScale = new Vector3(s, s, 1f);
                if (group != null) group.alpha = Mathf.Lerp(alphaFrom, alphaTo, eased);
                yield return null;
            }

            target.localScale = new Vector3(to, to, 1f);
            if (group != null) group.alpha = alphaTo;
        }
    }
}
