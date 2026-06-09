using System.Collections.Generic;
using UnityEngine;

namespace Sandbox.UI
{
    /// <summary>
    /// ランタイム生成 UI 用の手続きスプライト工房。
    /// 単色矩形の重ね合わせから脱却し、グラデーション・放射グロー・角丸パネル・
    /// 本物のビネット・三角の山稜・スキャンライン・フィルムグレインを供給する。
    ///
    /// 設計方針:
    ///   - 形（アルファ）と色を分離する。白系マスクを焼き、<see cref="UnityEngine.UI.Image.color"/> で着色する。
    ///   - 角丸/枠は 9-slice 境界付きで生成し、どのサイズでも角半径が崩れないようにする。
    ///   - 生成結果はキーでキャッシュし、シーンをまたいでも再利用する。
    /// </summary>
    public static class UiSprite
    {
        private static readonly Dictionary<string, Sprite> Cache = new();

        private static Sprite GetOrAdd(string key, System.Func<Sprite> factory)
        {
            if (Cache.TryGetValue(key, out var s) && s != null) return s;
            s = factory();
            Cache[key] = s;
            return s;
        }

        private static Texture2D NewTex(int w, int h, TextureWrapMode wrap = TextureWrapMode.Clamp)
            => new(w, h, TextureFormat.RGBA32, false) { wrapMode = wrap, filterMode = FilterMode.Bilinear };

        // ─────────────────────────────────────────────────────────────
        // 角丸（塗り）— 白マスク。Image.color で着色。9-slice 境界付き。
        // ─────────────────────────────────────────────────────────────
        public static Sprite RoundedRect(int radius = 24)
        {
            radius = Mathf.Clamp(radius, 2, 64);
            return GetOrAdd($"round_{radius}", () =>
            {
                int size = radius * 2 + 8;
                const float aa = 1.3f;
                var tex = NewTex(size, size);
                var px = new Color[size * size];
                float r = radius;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(r - x, x - (size - 1 - r), 0f);
                    float dy = Mathf.Max(r - y, y - (size - 1 - r), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((r - d) / aa + 1f);
                    a = Mathf.Clamp01(a);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            });
        }

        // ─────────────────────────────────────────────────────────────
        // 角丸（枠のみ）— 太さ borderPx の中空フレーム。
        // ─────────────────────────────────────────────────────────────
        public static Sprite RoundedFrame(int radius = 24, int borderPx = 3)
        {
            radius = Mathf.Clamp(radius, 2, 64);
            borderPx = Mathf.Clamp(borderPx, 1, radius);
            return GetOrAdd($"frame_{radius}_{borderPx}", () =>
            {
                int size = radius * 2 + 8;
                const float aa = 1.3f;
                var tex = NewTex(size, size);
                var px = new Color[size * size];
                float r = radius;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(r - x, x - (size - 1 - r), 0f);
                    float dy = Mathf.Max(r - y, y - (size - 1 - r), 0f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float outer = Mathf.Clamp01((r - d) / aa + 1f);
                    float inner = Mathf.Clamp01((r - borderPx - d) / aa + 1f);
                    float a = Mathf.Clamp01(outer - inner);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                    100f, 0, SpriteMeshType.FullRect, new Vector4(r + 2, r + 2, r + 2, r + 2));
            });
        }

        // ─────────────────────────────────────────────────────────────
        // 縦グラデーション（色を焼き込む）。Image はそのまま stretch。
        // ─────────────────────────────────────────────────────────────
        public static Sprite VerticalGradient(Color bottom, Color top, int steps = 256)
        {
            string key = $"vgrad_{bottom}_{top}_{steps}";
            return GetOrAdd(key, () =>
            {
                var tex = NewTex(4, steps);
                var px = new Color[4 * steps];
                for (int y = 0; y < steps; y++)
                {
                    float t = steps <= 1 ? 0f : y / (float)(steps - 1);
                    Color c = Color.Lerp(bottom, top, t);
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
                }
                tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, 4, steps), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        /// <summary>3 色の縦グラデ（地平線で暖色が灯る夕景など）。t は中段の位置 0..1。</summary>
        public static Sprite VerticalGradient3(Color bottom, Color mid, Color top, float midPos = 0.45f, int steps = 256)
        {
            string key = $"vgrad3_{bottom}_{mid}_{top}_{midPos}_{steps}";
            return GetOrAdd(key, () =>
            {
                var tex = NewTex(4, steps);
                var px = new Color[4 * steps];
                for (int y = 0; y < steps; y++)
                {
                    float t = steps <= 1 ? 0f : y / (float)(steps - 1);
                    Color c = t < midPos
                        ? Color.Lerp(bottom, mid, Mathf.InverseLerp(0f, midPos, t))
                        : Color.Lerp(mid, top, Mathf.InverseLerp(midPos, 1f, t));
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
                }
                tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, 4, steps), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        // ─────────────────────────────────────────────────────────────
        // 放射グロー（白マスク・中心 1 → 周縁 0）。太陽/光だまり/フォーカス用。
        // ─────────────────────────────────────────────────────────────
        public static Sprite RadialGlow(float falloff = 2.2f, int size = 256)
        {
            string key = $"glow_{falloff}_{size}";
            return GetOrAdd(key, () =>
            {
                var tex = NewTex(size, size);
                var px = new Color[size * size];
                float c = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                    float a = Mathf.Pow(1f - d, falloff);
                    px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(a));
                }
                tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        // ─────────────────────────────────────────────────────────────
        // ビネット（黒マスク・中心 0 → 周縁 strength）。全画面 stretch で使う。
        // ─────────────────────────────────────────────────────────────
        public static Sprite Vignette(float inner = 0.45f, float outer = 1.05f, float strength = 1f, int size = 256)
        {
            string key = $"vig_{inner}_{outer}_{strength}_{size}";
            return GetOrAdd(key, () =>
            {
                var tex = NewTex(size, size);
                var px = new Color[size * size];
                float c = (size - 1) * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - c) / c;
                    float dy = (y - c) / c;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, d)) * strength;
                    px[y * size + x] = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
                }
                tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        // ─────────────────────────────────────────────────────────────
        // スキャンライン（縦に Tiled）。MIMESIS のアナログ感。
        // ─────────────────────────────────────────────────────────────
        public static Sprite Scanlines(int period = 4, float lineAlpha = 0.16f)
        {
            string key = $"scan_{period}_{lineAlpha}";
            return GetOrAdd(key, () =>
            {
                period = Mathf.Max(2, period);
                var tex = NewTex(4, period, TextureWrapMode.Repeat);
                var px = new Color[4 * period];
                for (int y = 0; y < period; y++)
                {
                    float a = y < period / 2 ? lineAlpha : 0f;
                    for (int x = 0; x < 4; x++) px[y * 4 + x] = new Color(0f, 0f, 0f, a);
                }
                tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, 4, period), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        // ─────────────────────────────────────────────────────────────
        // フィルムグレイン（Tiled）。質感ノイズ。白系・Image.color でごく薄く。
        // ─────────────────────────────────────────────────────────────
        public static Sprite Grain(int size = 128, float intensity = 1f, int seed = 1337)
        {
            string key = $"grain_{size}_{intensity}_{seed}";
            return GetOrAdd(key, () =>
            {
                var rng = new System.Random(seed);
                var tex = NewTex(size, size, TextureWrapMode.Repeat);
                var px = new Color[size * size];
                for (int i = 0; i < px.Length; i++)
                {
                    float n = (float)rng.NextDouble();
                    px[i] = new Color(1f, 1f, 1f, n * intensity);
                }
                tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            });
        }

        // ─────────────────────────────────────────────────────────────
        // 山稜シルエット（本物の三角ベース）。色は焼き込み。横長で parallax 用。
        // jaggedness: 稜線の荒さ、peaks: 主峰数、seed で形を変える。
        // ─────────────────────────────────────────────────────────────
        public static Sprite MountainRidge(Color color, float baseHeight = 0.55f, int peaks = 4, float jaggedness = 0.22f, int seed = 7, int width = 512, int height = 256)
        {
            string key = $"ridge_{color}_{baseHeight}_{peaks}_{jaggedness}_{seed}_{width}_{height}";
            return GetOrAdd(key, () =>
            {
                var rng = new System.Random(seed);
                // 稜線の高さプロファイルを生成（複数サイン波 + 乱数オフセット）
                var ridge = new float[width];
                float phase1 = (float)rng.NextDouble() * 6.28f;
                float phase2 = (float)rng.NextDouble() * 6.28f;
                for (int x = 0; x < width; x++)
                {
                    float u = x / (float)(width - 1);
                    float h = baseHeight;
                    h += Mathf.Sin(u * Mathf.PI * peaks + phase1) * jaggedness;
                    h += Mathf.Sin(u * Mathf.PI * peaks * 2.7f + phase2) * jaggedness * 0.4f;
                    h += ((float)rng.NextDouble() - 0.5f) * jaggedness * 0.25f;
                    ridge[x] = Mathf.Clamp01(h);
                }
                // 軽く平滑化して尖りすぎを防ぐ
                for (int pass = 0; pass < 2; pass++)
                    for (int x = 1; x < width - 1; x++)
                        ridge[x] = (ridge[x - 1] + ridge[x] * 2f + ridge[x + 1]) * 0.25f;

                var tex = NewTex(width, height);
                var px = new Color[width * height];
                for (int x = 0; x < width; x++)
                {
                    float top = ridge[x] * (height - 1);
                    for (int y = 0; y < height; y++)
                    {
                        float a = Mathf.Clamp01(top - y + 1f); // 稜線で 1px AA
                        px[y * width + x] = new Color(color.r, color.g, color.b, a * color.a);
                    }
                }
                tex.SetPixels(px); tex.Apply();
                return Sprite.Create(tex, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 100f);
            });
        }
    }
}
