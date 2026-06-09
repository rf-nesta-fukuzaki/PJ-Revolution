using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sandbox.UI
{
    /// <summary>
    /// ゲームループ UI の統合ビジュアルテーマ。
    ///   PEAK    … 暖色・山岳遠征・クリーム/アンバー・読みやすいソフトシャドウ
    ///   R.E.P.O … ベースキャンプ端末・太枠パネル・チーム共有/ルームコード表示
    ///   MIMESIS … ローディング/遷移時の微かな不穏（スキャンライン・ティールグリッチ）
    /// </summary>
    public static class FlowUiTheme
    {
        // ── R.E.P.O. 端末 / ショップ ─────────────────────────────
        public static readonly Color TerminalBg     = new(0.05f, 0.06f, 0.08f, 0.92f);
        public static readonly Color TerminalBorder = new(0.22f, 0.24f, 0.28f, 1f);
        public static readonly Color TerminalAccent = new(0.35f, 0.88f, 0.62f, 1f);   // 緑系 HUD
        public static readonly Color TerminalWarn   = new(0.98f, 0.55f, 0.22f, 1f);

        // ── MIMESIS 遷移 / ローディング ────────────────────────────
        public static readonly Color MimicTeal      = new(0.28f, 0.78f, 0.72f, 0.85f);
        public static readonly Color MimicScanline    = new(0f, 0f, 0f, 0.12f);
        public static readonly Color MimicVignette    = new(0f, 0f, 0f, 0.55f);

        // ── PEAK タイトル / リザルト ───────────────────────────────
        public static readonly Color PeakSunsetTop    = new(0.18f, 0.14f, 0.22f, 1f);
        public static readonly Color PeakSunsetMid    = new(0.42f, 0.22f, 0.14f, 0.65f);
        public static readonly Color PeakMountainSil  = new(0.08f, 0.10f, 0.14f, 0.85f);

        public enum SceneFlavor { TitlePeak, ShopRepo, CoopRepo, LoadingMimesis, ResultPeak }

        /// <summary>TMP に PEAK 可読性（アウトライン+シャドウ）を適用。</summary>
        public static void StyleReadable(TMP_Text text, float outline = 0.16f)
        {
            if (text == null) return;
            UiReadability.MakeReadable(text, outline);
        }

        /// <summary>R.E.P.O. 風の端末カード（角丸・縦グラデ面・発光枠・上端ハイライト・接地影）。</summary>
        public static RectTransform CreateTerminalPanel(Transform parent, string name,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax,
            Color? bg = null, Color? border = null)
        {
            var outer = NewRect(name, parent);
            outer.anchorMin = anchorMin;
            outer.anchorMax = anchorMax;
            outer.offsetMin = offsetMin;
            outer.offsetMax = offsetMax;

            // 接地影（パネルを浮かせる）
            var shadow = NewRect("Shadow", outer);
            Stretch(shadow, -10f);
            shadow.anchoredPosition = new Vector2(0f, -8f);
            AddSprite(shadow, UiSprite.RoundedRect(22), new Color(0f, 0f, 0f, 0.45f));

            // 面（縦グラデで上を僅かに明るく）
            Color baseBg = bg ?? TerminalBg;
            Color topBg = new(Mathf.Min(1f, baseBg.r + 0.05f), Mathf.Min(1f, baseBg.g + 0.05f), Mathf.Min(1f, baseBg.b + 0.06f), baseBg.a);
            var fill = AddSprite(outer, UiSprite.VerticalGradient(baseBg, topBg), Color.white);
            fill.type = Image.Type.Sliced;

            // 発光枠
            var frame = NewRect("Frame", outer);
            Stretch(frame);
            var frameImg = AddSprite(frame, UiSprite.RoundedFrame(22, 2), border ?? TerminalBorder);
            frameImg.type = Image.Type.Sliced;

            // 上端アクセントハイライト
            var highlight = NewRect("TopHighlight", outer);
            highlight.anchorMin = new Vector2(0.04f, 1f);
            highlight.anchorMax = new Vector2(0.96f, 1f);
            highlight.pivot = new Vector2(0.5f, 1f);
            highlight.sizeDelta = new Vector2(0f, 2f);
            highlight.anchoredPosition = new Vector2(0f, -6f);
            AddSprite(highlight, UiSprite.RoundedRect(2), new Color(1f, 1f, 1f, 0.10f));

            return outer;
        }

        /// <summary>RectTransform に Sprite 付き Image を足すヘルパー。</summary>
        public static Image AddSprite(RectTransform rt, Sprite sprite, Color color, Image.Type type = Image.Type.Sliced)
        {
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.type = type;
            if (type == Image.Type.Sliced) img.pixelsPerUnitMultiplier = 1f;
            return img;
        }

        /// <summary>PEAK 風の山岳シルエット（本物の三角稜線・多層パララックス）。遠→近で濃く。</summary>
        public static void CreateMountainSilhouette(Transform parent)
        {
            var root = NewRect("MountainSilhouette", parent);
            Stretch(root);

            // 遠景（霞んだ薄い稜線）
            AddRidge(root, "RidgeFar", 0f, 0.46f,
                new Color(0.16f, 0.16f, 0.24f, 0.7f), baseHeight: 0.62f, peaks: 5, jaggedness: 0.10f, seed: 11);
            // 中景
            AddRidge(root, "RidgeMid", 0f, 0.40f,
                new Color(0.10f, 0.11f, 0.17f, 0.92f), baseHeight: 0.66f, peaks: 4, jaggedness: 0.20f, seed: 7);
            // 近景（最も濃い前山）
            AddRidge(root, "RidgeNear", 0f, 0.30f,
                new Color(0.04f, 0.05f, 0.08f, 1f), baseHeight: 0.7f, peaks: 3, jaggedness: 0.26f, seed: 23);
        }

        private static void AddRidge(RectTransform parent, string name, float yMin, float yMax,
            Color color, float baseHeight, int peaks, float jaggedness, int seed)
        {
            var rt = NewRect(name, parent);
            rt.anchorMin = new Vector2(0f, yMin);
            rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = AddSprite(rt, UiSprite.MountainRidge(Color.white, baseHeight, peaks, jaggedness, seed), color, Image.Type.Simple);
            img.preserveAspect = false;
        }

        /// <summary>MIMESIS 風スキャンライン（タイル化 1 枚）。</summary>
        public static void CreateScanlineOverlay(Transform parent, int period = 4, float alpha = 0.16f)
        {
            var root = NewRect("Scanlines", parent);
            Stretch(root);
            var img = AddSprite(root, UiSprite.Scanlines(period, 1f), new Color(0f, 0f, 0f, alpha), Image.Type.Tiled);
            img.pixelsPerUnitMultiplier = 3.5f;
            img.raycastTarget = false;
        }

        /// <summary>放射ビネット（本物のアルファ減衰・1 枚）。</summary>
        public static void CreateVignette(Transform parent, float strength = 0.45f)
        {
            var vig = NewRect("Vignette", parent);
            Stretch(vig);
            var img = AddSprite(vig, UiSprite.Vignette(0.42f, 1.08f, 1f), new Color(0f, 0f, 0f, strength), Image.Type.Simple);
            img.raycastTarget = false;
        }

        /// <summary>
        /// diegetic 3D を背後に見せる R.E.P.O. 端末オーバーレイ。
        /// 上下を暗く落として UI 文字を読ませつつ、中央は透過して 3D 基地を覗かせる。
        /// </summary>
        public static void CreateDiegeticTerminalOverlay(Transform parent)
        {
            var shade = NewRect("DiegeticShade", parent);
            Stretch(shade);
            // bottom→mid→top のアルファ勾配（中央クリア・上下暗め）
            var grad = AddSprite(shade, UiSprite.VerticalGradient3(
                new Color(0.02f, 0.03f, 0.05f, 0.55f),   // 下: ヒント帯
                new Color(0.02f, 0.03f, 0.05f, 0.04f),   // 中: 透過（基地を見せる）
                new Color(0.02f, 0.03f, 0.06f, 0.82f),   // 上: ヘッダ/掲示板
                0.5f), Color.white, Image.Type.Simple);
            grad.raycastTarget = false;

            // 端末の緑がかった上方グロー
            var monitorGlow = NewRect("RepoMonitorGlow", parent);
            monitorGlow.anchorMin = monitorGlow.anchorMax = new Vector2(0.5f, 0.96f);
            monitorGlow.sizeDelta = new Vector2(1700f, 760f);
            var mg = AddSprite(monitorGlow, UiSprite.RadialGlow(2.4f),
                new Color(TerminalAccent.r, TerminalAccent.g, TerminalAccent.b, 0.06f), Image.Type.Simple);
            mg.raycastTarget = false;

            CreateScanlineOverlay(parent, 5, 0.06f);
            CreateVignette(parent, 0.5f);
            CreateGrain(parent, 0.018f);
        }

        /// <summary>極薄フィルムグレイン（タイル化）。質感ノイズ。</summary>
        public static void CreateGrain(Transform parent, float alpha = 0.035f)
        {
            var g = NewRect("Grain", parent);
            Stretch(g);
            var img = AddSprite(g, UiSprite.Grain(128, 1f), new Color(1f, 1f, 1f, alpha), Image.Type.Tiled);
            img.pixelsPerUnitMultiplier = 1.6f;
            img.raycastTarget = false;
        }

        public static void CreateSceneBackdrop(Transform parent, SceneFlavor flavor)
        {
            switch (flavor)
            {
                case SceneFlavor.TitlePeak:
                case SceneFlavor.ResultPeak:
                    CreatePeakBackdrop(parent);
                    UiAmbientMotion.Attach(parent, flavor == SceneFlavor.ResultPeak
                        ? UiAmbientMotion.Profile.ResultPeak
                        : UiAmbientMotion.Profile.TitlePeak);
                    break;
                case SceneFlavor.ShopRepo:
                case SceneFlavor.CoopRepo:
                    CreateRepoBackdrop(parent);
                    UiAmbientMotion.Attach(parent, UiAmbientMotion.Profile.ShopRepo);
                    break;
                case SceneFlavor.LoadingMimesis:
                    CreateMimesisBackdrop(parent);
                    UiAmbientMotion.Attach(parent, UiAmbientMotion.Profile.LoadingMimesis);
                    break;
            }
        }

        /// <summary>
        /// ポーズメニュー用の暗い山岳オーバーレイ（PEAK 夕景を抑えたトーン）。
        /// ゲーム画面の上に重ねても読みやすく、没入感を保つ。
        /// </summary>
        public static void CreatePauseBackdrop(Transform parent)
        {
            var bg = NewRect("PauseBG", parent);
            Stretch(bg);
            AddSprite(bg, UiSprite.VerticalGradient3(
                new Color(0.03f, 0.04f, 0.07f, 0.96f),
                new Color(0.05f, 0.06f, 0.09f, 0.92f),
                new Color(0.02f, 0.03f, 0.05f, 0.98f),
                0.48f), Color.white, Image.Type.Simple);

            var glow = NewRect("PauseGlow", parent);
            glow.anchorMin = glow.anchorMax = new Vector2(0.5f, 0.72f);
            glow.sizeDelta = new Vector2(1400f, 900f);
            AddSprite(glow, UiSprite.RadialGlow(2.4f),
                new Color(PeakSunsetMid.r, PeakSunsetMid.g, PeakSunsetMid.b, 0.14f), Image.Type.Simple);

            CreateMountainSilhouette(parent);
            CreateVignette(parent, 0.62f);
            CreateScanlineOverlay(parent, 6, 0.05f);
            CreateGrain(parent, 0.022f);
            UiAmbientMotion.Attach(parent, UiAmbientMotion.Profile.ResultPeak);
        }

        private static void CreatePeakBackdrop(Transform parent)
        {
            // 夕景の縦グラデ空（地平の暖色 → 中段 → 上部の冷たい紫紺）
            var sky = NewRect("PeakSky", parent);
            Stretch(sky);
            AddSprite(sky, UiSprite.VerticalGradient3(
                new Color(0.50f, 0.27f, 0.18f, 1f),   // 地平の橙
                new Color(0.30f, 0.18f, 0.22f, 1f),   // 中段の赤紫
                new Color(0.10f, 0.09f, 0.16f, 1f),   // 上部の紫紺
                0.42f), Color.white, Image.Type.Simple);

            // 太陽の光だまり（本物の放射グロー・2 重で芯を強く）。テキスト帯を避け上方右へ。
            var sunGlow = NewRect("PeakSunHalo", parent);
            sunGlow.anchorMin = sunGlow.anchorMax = new Vector2(0.78f, 0.70f);
            sunGlow.sizeDelta = new Vector2(1180f, 1180f);
            AddSprite(sunGlow, UiSprite.RadialGlow(2.0f), new Color(0.98f, 0.60f, 0.32f, 0.32f), Image.Type.Simple);

            var sunCore = NewRect("PeakSunCore", parent);
            sunCore.anchorMin = sunCore.anchorMax = new Vector2(0.78f, 0.70f);
            sunCore.sizeDelta = new Vector2(300f, 300f);
            AddSprite(sunCore, UiSprite.RadialGlow(2.8f), new Color(1f, 0.88f, 0.64f, 0.6f), Image.Type.Simple);

            CreateMountainSilhouette(parent);
            CreateVignette(parent, 0.40f);
            CreateGrain(parent, 0.03f);
        }

        private static void CreateRepoBackdrop(Transform parent)
        {
            // 倉庫/基地の冷たい縦グラデ
            var bg = NewRect("RepoBG", parent);
            Stretch(bg);
            AddSprite(bg, UiSprite.VerticalGradient(
                new Color(0.03f, 0.04f, 0.06f, 1f),
                new Color(0.07f, 0.09f, 0.12f, 1f)), Color.white, Image.Type.Simple);

            // 端末の緑がかった上方グロー（モニタ光）
            var monitorGlow = NewRect("RepoMonitorGlow", parent);
            monitorGlow.anchorMin = monitorGlow.anchorMax = new Vector2(0.5f, 0.95f);
            monitorGlow.sizeDelta = new Vector2(1600f, 900f);
            AddSprite(monitorGlow, UiSprite.RadialGlow(2.4f), new Color(TerminalAccent.r, TerminalAccent.g, TerminalAccent.b, 0.08f), Image.Type.Simple);

            CreateScanlineOverlay(parent, 5, 0.10f);
            CreateVignette(parent, 0.55f);
            CreateGrain(parent, 0.04f);
        }

        private static void CreateMimesisBackdrop(Transform parent)
        {
            var bg = NewRect("MimicBG", parent);
            Stretch(bg);
            AddSprite(bg, UiSprite.VerticalGradient(
                new Color(0.01f, 0.04f, 0.05f, 1f),
                new Color(0.03f, 0.08f, 0.09f, 1f)), Color.white, Image.Type.Simple);

            var tealGlow = NewRect("MimicGlow", parent);
            tealGlow.anchorMin = tealGlow.anchorMax = new Vector2(0.5f, 0.45f);
            tealGlow.sizeDelta = new Vector2(1500f, 1500f);
            AddSprite(tealGlow, UiSprite.RadialGlow(2.2f), new Color(MimicTeal.r, MimicTeal.g, MimicTeal.b, 0.10f), Image.Type.Simple);

            CreateScanlineOverlay(parent, 4, 0.16f);
            CreateVignette(parent, 0.6f);
            CreateGrain(parent, 0.05f);
        }

        public static RectTransform NewRect(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rt, float inset = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
        }
    }
}
