using UnityEngine;
using K = Sandbox.World.Integration.BasecampPropKit;

namespace Sandbox.World.Integration
{
    /// <summary>
    /// SandboxOfflineCombined のリスポーン拠点（ベースキャンプ）を、平坦化された <c>BasecampPad</c> 天面の上へ
    /// 手続き的に組み上げる。従来は Cube 5 個（柱アーチ・緑床・茶箱・空ラベル・板）が散在した「仮」状態だったため、
    /// 1 つの登山遠征キャンプとして整理・再構築する：
    ///
    ///   ・中央 …… 焚き火（石組み＋丸太＋発光する炎＋ゆらぐ暖色ライト）＝リスポーン地点のランドマーク
    ///   ・前方(+Z, 山側) …… 出発ゲート（丸太の A フレーム＋横木＋ロープ＋吊り旗＋松明）
    ///   ・左 …… 物資テント（傾斜屋根のキャンバス＋カウンター＋看板）＝装備ショップの見た目
    ///   ・左奥 …… 天候/告知ボード（木枠＋小屋根、WeatherBoardManager の Canvas を読みやすい高さへ）
    ///   ・右 …… 寝床テント 2 張り＋装備ラック
    ///   ・後方(-Z) …… 帰還/搬出デッキ（H マーキングのヘリパッド・四隅ライト）
    ///   ・周囲 …… 物資の木箱・樽・ロープの束・旗竿・寝袋、外周の丸太柵（ゲート側だけ開口）とランタン
    ///
    /// 当たり判定：構造物（柱・梁・カウンター・木箱・樽・柵・デッキ・ボード支柱）は全て Collider 付き。
    /// 旗・幕・ロープ・炎・ランタンの光・地面デカールは Collider 無し（プレイヤーが引っかからない）。
    ///
    /// 機能オブジェクト（DepartureGate / ReturnZone / WeatherBoard / BasecampShop）は破棄せず、
    /// 位置を整えて見た目だけ作り直す（トリガーや WeatherBoardManager の参照は温存）。
    /// すべて実行時生成・実行時マテリアル（アセットや他シーンを汚さない）。<see cref="CombinedTerrainConformer"/>
    /// が pad ビルド直後に一度だけ呼ぶ。
    /// </summary>
    internal static class BasecampBuilder
    {
        // ── 配色（暖色・低彩度・マット。強い日射 + ACES での白飛びを避けアルベドは ~0.6 以下） ──
        private static readonly Color TimberDark  = new Color(0.26f, 0.18f, 0.12f);
        private static readonly Color Timber      = new Color(0.38f, 0.27f, 0.17f);
        private static readonly Color TimberLight = new Color(0.46f, 0.34f, 0.22f);
        private static readonly Color Rope        = new Color(0.50f, 0.42f, 0.27f);
        private static readonly Color CanvasCream = new Color(0.56f, 0.51f, 0.42f);
        private static readonly Color CanvasRed   = new Color(0.52f, 0.21f, 0.16f);
        private static readonly Color CanvasBlue  = new Color(0.22f, 0.33f, 0.44f);
        private static readonly Color TarpGreen   = new Color(0.25f, 0.33f, 0.22f);
        private static readonly Color Iron        = new Color(0.20f, 0.21f, 0.23f);
        private static readonly Color Stone       = new Color(0.30f, 0.30f, 0.32f);
        private static readonly Color StoneDark   = new Color(0.20f, 0.20f, 0.22f);
        private static readonly Color Gold        = new Color(0.62f, 0.50f, 0.18f);
        private static readonly Color Deck        = new Color(0.24f, 0.25f, 0.27f);
        private static readonly Color PadPaint    = new Color(0.72f, 0.62f, 0.28f); // ヘリパッド塗装（非発光）

        private static readonly Color FlameLow  = new Color(0.95f, 0.32f, 0.08f);
        private static readonly Color FlameMid  = new Color(1.00f, 0.55f, 0.12f);
        private static readonly Color FireLight = new Color(1.00f, 0.62f, 0.28f);
        private static readonly Color WarmLight = new Color(1.00f, 0.80f, 0.52f);
        private static readonly Color GlowAmber = new Color(1.00f, 0.66f, 0.22f);

        // ── レイアウト（camp サブルートのローカル座標。y=0 が pad 天面、+Z が山側） ──
        private const float GateDist    = 12f;   // 出発ゲート (+Z)
        private const float ReturnDist  = 12f;   // 帰還/搬出デッキ (-Z)
        private const float Perimeter   = 15.5f; // 外周柵の半辺
        private const float GateOpening = 5f;    // ゲート側の柵開口の半幅

        public static void Build(Vector3 center, float topY)
        {
            var basecamp = GameObject.Find("Basecamp")?.transform;
            if (basecamp == null) basecamp = new GameObject("Basecamp").transform;

            // 装飾サブルート（pad 天面 = ローカル y0）
            var campRoot = K.Group("CampDressing", basecamp, new Vector3(center.x, topY, center.z));

            BuildGate(basecamp, center, topY);
            BuildExtractionDeck(basecamp, center, topY);
            BuildShopTent(K.Group("ShopTent", campRoot, new Vector3(-10f, 0f, 4f)));
            BuildNoticeBoard(basecamp, center, topY);
            BuildSupplyTent(K.Group("SupplyTentA", campRoot, new Vector3(10f, 0f, 3f)), CanvasBlue, 18f);
            BuildSupplyTent(K.Group("SupplyTentB", campRoot, new Vector3(10.5f, 0f, -6f)), TarpGreen, -22f);
            BuildGearRack(K.Group("GearRack", campRoot, new Vector3(7.5f, 0f, -3f)));
            BuildCampfire(K.Group("Campfire", campRoot, new Vector3(-3.5f, 0f, -1f)));
            BuildCargo(K.Group("Cargo", campRoot, new Vector3(-8.5f, 0f, -7f)));
            BuildBarrels(K.Group("Barrels", campRoot, new Vector3(-6.5f, 0f, 6.5f)));
            BuildFlagPole(K.Group("FlagPole", campRoot, new Vector3(5f, 0f, -9f)));
            BuildBedrolls(K.Group("Bedrolls", campRoot, new Vector3(-1.5f, 0f, -3.5f)));
            BuildPerimeter(K.Group("Perimeter", campRoot, Vector3.zero));
            BuildGroundDecals(K.Group("GroundDecals", campRoot, Vector3.zero));

            // 既存の散在装飾を排除（純装飾。機能コンポーネント無し）
            DestroyIfExists(basecamp, "ShopCounter");
            DestroyIfExists(basecamp, "ShopSignLabel");
            // 旧 OfflineTest の埋没ヘリパッドマーカーを搬出デッキへ畳む
            FoldHelipadMarker(center, topY);
            // 安全地帯（拠点）に紛れ込んだ散在物を片付ける
            ClearCampFootprint(15.5f);

            Debug.Log($"[BasecampBuilder] 拠点を再構築しました（pad 天面 y={topY:F1}）。");
        }

        // ── 出発ゲート（丸太 A フレーム） ───────────────────────────
        private static void BuildGate(Transform basecamp, Vector3 center, float topY)
        {
            var gate = GameObject.Find("DepartureGate")?.transform;
            if (gate == null) return;
            gate.position = new Vector3(center.x, topY, center.z + GateDist);
            ClearChildrenExcept(gate, null);

            var t = gate; // ローカル y0 = pad
            var matT = K.Mat(Timber);
            var matD = K.Mat(TimberDark);
            const float postH = 4.4f, span = 4.4f;

            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * span;
                // 主柱（やや内傾の A フレーム）
                K.Cyl($"Post_{s}", t, new Vector3(x, postH * 0.5f, 0f), 0.28f, postH, matT,
                      eulerLocal: new Vector3(0f, 0f, s * 5f));
                // 控え柱（前後の筋交い）
                K.Cyl($"Brace_{s}_f", t, new Vector3(x, 1.6f, 1.4f), 0.16f, 3.6f, matD,
                      eulerLocal: new Vector3(38f, 0f, 0f));
                K.Cyl($"Brace_{s}_b", t, new Vector3(x, 1.6f, -1.4f), 0.16f, 3.6f, matD,
                      eulerLocal: new Vector3(-38f, 0f, 0f));
                K.Box($"PostBase_{s}", t, new Vector3(x, 0.25f, 0f), new Vector3(1.1f, 0.5f, 1.1f), matD);
                // 松明（柱頭の発光）
                BuildTorch(t, new Vector3(x, postH + 0.1f, 0f));
            }

            // 横木（2 段）と棟木
            K.Box("Lintel", t, new Vector3(0f, postH - 0.15f, 0f), new Vector3(span * 2f + 1f, 0.4f, 0.45f), matT);
            K.Box("Lintel2", t, new Vector3(0f, postH - 0.85f, 0f), new Vector3(span * 2f + 0.4f, 0.28f, 0.32f), matD);
            K.Cyl("Ridge", t, new Vector3(0f, postH + 0.35f, 0f), 0.14f, span * 2f, matD,
                  eulerLocal: new Vector3(0f, 0f, 90f));

            // 吊り旗（ゲート看板）。当たり判定なし。
            K.Box("Banner", t, new Vector3(0f, postH - 1.7f, 0.05f), new Vector3(3.0f, 1.4f, 0.06f), K.Mat(CanvasRed), solid: false);
            K.Box("BannerTrim", t, new Vector3(0f, postH - 1.0f, 0.04f), new Vector3(3.1f, 0.14f, 0.07f), K.Mat(Gold, emission: GlowAmber * 0.4f), solid: false);
            // ロープの飾り紐
            K.Cyl("Rope_L", t, new Vector3(-span, postH - 0.6f, 0f), 0.05f, span, K.Mat(Rope), solid: false, eulerLocal: new Vector3(0f, 0f, 70f));
        }

        private static void BuildTorch(Transform parent, Vector3 localTop)
        {
            K.Cyl("TorchHead", parent, localTop, 0.16f, 0.45f, K.Mat(Iron, 0.25f, 0.4f), solid: false);
            var flame = K.Cyl("TorchFlame", parent, localTop + Vector3.up * 0.35f, 0.13f, 0.5f, K.Mat(FlameMid, emission: FlameMid * 2.6f), solid: false);
            var l = K.PointLight("TorchLight", parent, localTop + Vector3.up * 0.4f, FireLight, 7f, 2.2f);
            l.gameObject.AddComponent<CampLightFlicker>().Init(l, flame.transform, 0.4f, 9f);
        }

        // ── 物資テント（傾斜屋根 ＝ ショップの見た目） ────────────────
        private static void BuildShopTent(Transform t)
        {
            t.localRotation = Quaternion.Euler(0f, 28f, 0f); // 入口を camp 中央へ向ける
            var pole = K.Mat(TimberLight);
            var canvas = K.Mat(CanvasCream);
            var stripe = K.Mat(CanvasRed);
            const float w = 5.2f, d = 4.2f, h = 2.6f, ridge = 3.7f;

            // 4 隅の支柱 + 棟柱
            float hw = w * 0.5f, hd = d * 0.5f;
            K.Cyl("Pole_FL", t, new Vector3(-hw, h * 0.5f, hd), 0.1f, h, pole);
            K.Cyl("Pole_FR", t, new Vector3(hw, h * 0.5f, hd), 0.1f, h, pole);
            K.Cyl("Pole_BL", t, new Vector3(-hw, h * 0.5f, -hd), 0.1f, h, pole);
            K.Cyl("Pole_BR", t, new Vector3(hw, h * 0.5f, -hd), 0.1f, h, pole);
            K.Cyl("RidgePole", t, new Vector3(0f, ridge, 0f), 0.08f, d + 0.6f, K.Mat(Timber), eulerLocal: new Vector3(90f, 0f, 0f));

            // 切妻の傾斜屋根（前後 2 枚）。屋根は当たり判定ありで貫通防止。
            float slopeLen = Mathf.Sqrt(hw * hw + (ridge - h) * (ridge - h)) + 0.2f;
            float ang = Mathf.Atan2(ridge - h, hw) * Mathf.Rad2Deg;
            K.Box("Roof_L", t, new Vector3(-hw * 0.5f, (h + ridge) * 0.5f, 0f), new Vector3(slopeLen, 0.08f, d + 0.6f), canvas, eulerLocal: new Vector3(0f, 0f, ang));
            K.Box("Roof_R", t, new Vector3(hw * 0.5f, (h + ridge) * 0.5f, 0f), new Vector3(slopeLen, 0.08f, d + 0.6f), canvas, eulerLocal: new Vector3(0f, 0f, -ang));
            // 屋根の縁取りストライプ
            K.Box("Eave_L", t, new Vector3(-hw, h - 0.06f, 0f), new Vector3(0.18f, 0.18f, d + 0.6f), stripe, solid: false);
            K.Box("Eave_R", t, new Vector3(hw, h - 0.06f, 0f), new Vector3(0.18f, 0.18f, d + 0.6f), stripe, solid: false);

            // 背面の幕（後ろだけ閉じ、前は開口）
            K.Box("BackWall", t, new Vector3(0f, h * 0.5f, -hd), new Vector3(w, h, 0.06f), canvas, solid: false);

            // カウンター（前方）と陳列の小箱
            K.Box("Counter", t, new Vector3(0f, 0.55f, hd - 0.3f), new Vector3(w - 0.4f, 1.1f, 0.7f), K.Mat(Timber));
            K.Box("CounterTop", t, new Vector3(0f, 1.16f, hd - 0.3f), new Vector3(w - 0.2f, 0.12f, 0.9f), K.Mat(TimberLight));
            K.Box("Crate_a", t, new Vector3(-1.6f, 1.42f, hd - 0.35f), new Vector3(0.5f, 0.5f, 0.5f), K.Mat(TimberDark), solid: false);
            K.Box("Crate_b", t, new Vector3(1.3f, 1.42f, hd - 0.4f), new Vector3(0.45f, 0.45f, 0.45f), K.Mat(TimberDark), solid: false);

            // 吊り看板（"SHOP"）。発光で視認性。
            K.Box("SignPanel", t, new Vector3(0f, h + 0.2f, hd + 0.1f), new Vector3(2.2f, 0.7f, 0.08f), K.Mat(TimberDark));
            K.Box("SignGlow", t, new Vector3(0f, h + 0.2f, hd + 0.16f), new Vector3(1.9f, 0.42f, 0.05f), K.Mat(Gold, emission: GlowAmber * 0.7f), solid: false);
            // 入口ランタン
            BuildLantern(t, new Vector3(hw + 0.2f, 2.0f, hd + 0.2f));
        }

        // ── 寝床/物資テント（小ぶり・キャンバス色違い） ─────────────
        private static void BuildSupplyTent(Transform t, Color tarp, float yaw)
        {
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var pole = K.Mat(Timber);
            var canvas = K.Mat(tarp);
            const float w = 3.4f, d = 4.0f, ridge = 2.4f;
            float hw = w * 0.5f, hd = d * 0.5f;

            // 棟柱（前後）＋棟木
            K.Cyl("Ridge_F", t, new Vector3(0f, ridge * 0.5f, hd), 0.09f, ridge, pole);
            K.Cyl("Ridge_B", t, new Vector3(0f, ridge * 0.5f, -hd), 0.09f, ridge, pole);
            K.Cyl("RidgePole", t, new Vector3(0f, ridge, 0f), 0.07f, d, K.Mat(TimberDark), eulerLocal: new Vector3(90f, 0f, 0f));

            // A 型の幕（左右の斜面）。屋根は当たり判定あり。
            float slope = Mathf.Sqrt(hw * hw + ridge * ridge) + 0.15f;
            float ang = Mathf.Atan2(ridge, hw) * Mathf.Rad2Deg;
            K.Box("Tarp_L", t, new Vector3(-hw * 0.5f, ridge * 0.5f, 0f), new Vector3(slope, 0.07f, d + 0.4f), canvas, eulerLocal: new Vector3(0f, 0f, ang));
            K.Box("Tarp_R", t, new Vector3(hw * 0.5f, ridge * 0.5f, 0f), new Vector3(slope, 0.07f, d + 0.4f), canvas, eulerLocal: new Vector3(0f, 0f, -ang));
            // 奥の三角閉じ（板で代用）
            K.Box("Back", t, new Vector3(0f, ridge * 0.35f, -hd), new Vector3(w * 0.7f, ridge * 0.7f, 0.05f), canvas, solid: false);
            // ペグ（ロープ）
            for (int s = -1; s <= 1; s += 2)
                K.Cyl($"Guy_{s}", t, new Vector3(s * (hw + 0.5f), 0.5f, hd + 0.4f), 0.04f, 1.4f, K.Mat(Rope), solid: false, eulerLocal: new Vector3(45f, 0f, s * 25f));
        }

        // ── 装備ラック ─────────────────────────────────────────────
        private static void BuildGearRack(Transform t)
        {
            t.localRotation = Quaternion.Euler(0f, -18f, 0f);
            var w = K.Mat(Timber);
            K.Box("Leg_L", t, new Vector3(-1.1f, 0.9f, 0f), new Vector3(0.12f, 1.8f, 0.12f), w);
            K.Box("Leg_R", t, new Vector3(1.1f, 0.9f, 0f), new Vector3(0.12f, 1.8f, 0.12f), w);
            K.Box("Bar", t, new Vector3(0f, 1.7f, 0f), new Vector3(2.5f, 0.1f, 0.1f), K.Mat(TimberDark));
            // 吊り下げ装備（ピッケル・ザイル・ヘルメット代用）
            K.Cyl("Coil", t, new Vector3(-0.7f, 1.2f, 0f), 0.28f, 0.5f, K.Mat(Rope), solid: false, eulerLocal: new Vector3(90f, 0f, 0f));
            K.Box("Pack", t, new Vector3(0.2f, 1.1f, 0f), new Vector3(0.5f, 0.7f, 0.35f), K.Mat(CanvasRed), solid: false);
            K.Cyl("AxeShaft", t, new Vector3(0.9f, 1.1f, 0f), 0.04f, 1.0f, K.Mat(TimberLight), solid: false);
            K.Box("AxeHead", t, new Vector3(0.9f, 1.55f, 0.08f), new Vector3(0.28f, 0.12f, 0.18f), K.Mat(Iron, 0.4f, 0.6f), solid: false);
        }

        // ── 焚き火（リスポーンのランドマーク） ──────────────────────
        private static void BuildCampfire(Transform t)
        {
            var stone = K.Mat(Stone, 0.05f);
            var stoneD = K.Mat(StoneDark, 0.05f);
            // 石組みのリング（8 個）
            const int n = 8; const float ringR = 1.15f;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                var pos = new Vector3(Mathf.Cos(a) * ringR, 0.18f, Mathf.Sin(a) * ringR);
                K.Box($"Stone_{i}", t, pos, new Vector3(0.5f, 0.36f, 0.42f),
                      i % 2 == 0 ? stone : stoneD, eulerLocal: new Vector3(0f, a * Mathf.Rad2Deg, Random.Range(-8f, 8f)));
            }
            // 灰の底
            K.Cyl("Ash", t, new Vector3(0f, 0.06f, 0f), ringR - 0.2f, 0.1f, stoneD, solid: false);
            // 交差した丸太
            var logMat = K.Mat(TimberDark);
            K.Cyl("Log_1", t, new Vector3(0f, 0.22f, 0f), 0.12f, 1.6f, logMat, solid: false, eulerLocal: new Vector3(90f, 25f, 6f));
            K.Cyl("Log_2", t, new Vector3(0f, 0.28f, 0f), 0.12f, 1.6f, logMat, solid: false, eulerLocal: new Vector3(90f, -35f, -6f));
            K.Cyl("Log_3", t, new Vector3(0f, 0.34f, 0f), 0.11f, 1.5f, logMat, solid: false, eulerLocal: new Vector3(78f, 80f, 0f));

            // 発光する炎（下→上へ細く。HDR 発光を上げ過ぎると白飛びするので橙〜黄に留める）
            K.Cyl("Flame_lo", t, new Vector3(0f, 0.45f, 0f), 0.38f, 0.55f, K.Mat(FlameLow, emission: FlameLow * 1.7f), solid: false);
            K.Cyl("Flame_mid", t, new Vector3(0f, 0.8f, 0f), 0.24f, 0.6f, K.Mat(FlameMid, emission: FlameMid * 1.9f), solid: false);
            var tip = K.Cyl("Flame_top", t, new Vector3(0f, 1.2f, 0f), 0.12f, 0.55f, K.Mat(FlameMid, emission: FlameMid * 2.1f), solid: false);

            var l = K.PointLight("FireLight", t, new Vector3(0f, 1.1f, 0f), FireLight, 16f, 4.2f);
            l.gameObject.AddComponent<CampLightFlicker>().Init(l, tip.transform, 0.32f, 7f);

            // 焚き火を囲む丸太ベンチ 2 つ
            var bench = K.Mat(Timber);
            K.Cyl("Bench_1", t, new Vector3(0f, 0.32f, 2.0f), 0.3f, 2.2f, bench, eulerLocal: new Vector3(0f, 90f, 90f));
            K.Cyl("Bench_2", t, new Vector3(-1.9f, 0.32f, -0.6f), 0.3f, 2.0f, bench, eulerLocal: new Vector3(0f, 20f, 90f));
        }

        // ── 貨物（積み木箱＋防水シート） ────────────────────────────
        private static void BuildCargo(Transform t)
        {
            var crate = K.Mat(TimberLight);
            var crateD = K.Mat(Timber);
            var edge = K.Mat(TimberDark);
            void Crate(string n, Vector3 p, float s, Material m, float yaw)
            {
                var c = K.Box(n, t, p, new Vector3(s, s, s), m, eulerLocal: new Vector3(0f, yaw, 0f));
                // 枠の縁取り（細い当たり判定なしの帯）
                K.Box(n + "_x", c.transform, new Vector3(0f, 0f, 0.5f), new Vector3(1.02f, 0.12f, 0.03f), edge, solid: false);
                K.Box(n + "_y", c.transform, new Vector3(0.5f, 0f, 0f), new Vector3(0.03f, 0.12f, 1.02f), edge, solid: false);
            }
            Crate("Crate_1", new Vector3(0f, 0.55f, 0f), 1.1f, crate, 8f);
            Crate("Crate_2", new Vector3(1.2f, 0.45f, 0.3f), 0.9f, crateD, -14f);
            Crate("Crate_3", new Vector3(0.2f, 1.5f, 0.1f), 0.8f, crateD, 20f);
            // 防水シート
            K.Box("Tarp", t, new Vector3(0.5f, 2.0f, 0.1f), new Vector3(2.2f, 0.06f, 1.8f), K.Mat(TarpGreen), solid: false, eulerLocal: new Vector3(4f, 12f, -3f));
        }

        // ── 樽（金属タガ付き） ──────────────────────────────────────
        private static void BuildBarrels(Transform t)
        {
            void Barrel(string n, Vector3 p, float yaw)
            {
                var b = K.Cyl(n, t, p, 0.42f, 1.1f, K.Mat(Timber), eulerLocal: new Vector3(0f, yaw, 0f));
                K.Cyl(n + "_hoopT", b.transform, new Vector3(0f, 0.6f, 0f), 0.52f, 0.16f, K.Mat(Iron, 0.3f, 0.5f), solid: false);
                K.Cyl(n + "_hoopB", b.transform, new Vector3(0f, -0.6f, 0f), 0.52f, 0.16f, K.Mat(Iron, 0.3f, 0.5f), solid: false);
            }
            Barrel("Barrel_1", new Vector3(0f, 0.55f, 0f), 0f);
            Barrel("Barrel_2", new Vector3(0.95f, 0.55f, 0.2f), 30f);
            // 横倒しの 1 つ
            K.Cyl("Barrel_3", t, new Vector3(0.4f, 0.42f, 1.1f), 0.42f, 1.1f, K.Mat(TimberDark), eulerLocal: new Vector3(90f, 10f, 0f));
        }

        // ── 旗竿（チームバナー） ────────────────────────────────────
        private static void BuildFlagPole(Transform t)
        {
            K.Box("Base", t, new Vector3(0f, 0.25f, 0f), new Vector3(0.9f, 0.5f, 0.9f), K.Mat(Stone, 0.05f));
            K.Cyl("Pole", t, new Vector3(0f, 3.0f, 0f), 0.1f, 6.0f, K.Mat(Iron, 0.25f, 0.4f));
            K.Cyl("Finial", t, new Vector3(0f, 6.1f, 0f), 0.16f, 0.32f, K.Mat(Gold, 0.3f, 0.6f, emission: GlowAmber * 0.5f), solid: false);
            // 旗（発光トリム付き）
            K.Box("Flag", t, new Vector3(0.95f, 5.3f, 0f), new Vector3(1.8f, 1.1f, 0.05f), K.Mat(CanvasRed), solid: false);
            K.Box("FlagBand", t, new Vector3(0.95f, 5.75f, 0.01f), new Vector3(1.8f, 0.18f, 0.06f), K.Mat(Gold, emission: GlowAmber * 0.4f), solid: false);
        }

        // ── 寝袋（焚き火脇） ────────────────────────────────────────
        private static void BuildBedrolls(Transform t)
        {
            K.Box("Bedroll_1", t, new Vector3(0f, 0.12f, 0f), new Vector3(0.8f, 0.18f, 2.0f), K.Mat(CanvasBlue), solid: false, eulerLocal: new Vector3(0f, 24f, 0f));
            K.Box("Pillow_1", t, new Vector3(0.35f, 0.22f, 0.8f), new Vector3(0.7f, 0.16f, 0.5f), K.Mat(CanvasCream), solid: false, eulerLocal: new Vector3(0f, 24f, 0f));
            K.Box("Bedroll_2", t, new Vector3(1.3f, 0.12f, -0.4f), new Vector3(0.8f, 0.18f, 2.0f), K.Mat(CanvasRed), solid: false, eulerLocal: new Vector3(0f, -10f, 0f));
        }

        // ── 帰還/搬出デッキ（ReturnZone の見た目） ──────────────────
        private static void BuildExtractionDeck(Transform basecamp, Vector3 center, float topY)
        {
            var rz = GameObject.Find("ReturnZone")?.transform;
            Transform t;
            if (rz != null)
            {
                rz.position = new Vector3(center.x, topY, center.z - ReturnDist);
                ClearChildrenExcept(rz, null);
                t = rz; // ReturnZone の Awake トリガー(10x4x10)はこの位置を中心に張られる
            }
            else
            {
                t = K.Group("ExtractionDeck", basecamp, new Vector3(center.x, topY, center.z - ReturnDist));
            }

            // 一段高い板張りデッキ（プレイヤーが乗れる ＝ 当たり判定あり）
            K.Box("DeckFloor", t, new Vector3(0f, 0.12f, 0f), new Vector3(9f, 0.24f, 9f), K.Mat(Deck, 0.1f));
            // 板目（細い溝・装飾）
            for (int i = -3; i <= 3; i++)
                K.Box($"Plank_{i}", t, new Vector3(i * 1.25f, 0.25f, 0f), new Vector3(0.06f, 0.04f, 8.6f), K.Mat(StoneDark), solid: false);

            // ヘリパッドの H マーキング（塗装。発光させると面積が大きく白飛びするためアルベドのみ）
            var paint = K.Mat(PadPaint, 0.0f);
            K.Box("H_l", t, new Vector3(-1.2f, 0.26f, 0f), new Vector3(0.4f, 0.04f, 3.2f), paint, solid: false);
            K.Box("H_r", t, new Vector3(1.2f, 0.26f, 0f), new Vector3(0.4f, 0.04f, 3.2f), paint, solid: false);
            K.Box("H_c", t, new Vector3(0f, 0.26f, 0f), new Vector3(2.0f, 0.04f, 0.4f), paint, solid: false);
            // 塗装の外周枠（細い四辺。リングの塗りつぶしはやめる）
            K.Box("Mark_N", t, new Vector3(0f, 0.26f, 3.9f), new Vector3(8.0f, 0.04f, 0.22f), paint, solid: false);
            K.Box("Mark_S", t, new Vector3(0f, 0.26f, -3.9f), new Vector3(8.0f, 0.04f, 0.22f), paint, solid: false);
            K.Box("Mark_E", t, new Vector3(3.9f, 0.26f, 0f), new Vector3(0.22f, 0.04f, 8.0f), paint, solid: false);
            K.Box("Mark_W", t, new Vector3(-3.9f, 0.26f, 0f), new Vector3(0.22f, 0.04f, 8.0f), paint, solid: false);

            // 四隅の標識灯（小さなレンズの発光のみ。控えめに）
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var p = new Vector3(sx * 4.2f, 0.0f, sz * 4.2f);
                    K.Box("Bollard", t, p + Vector3.up * 0.4f, new Vector3(0.22f, 0.8f, 0.22f), K.Mat(Iron, 0.3f, 0.4f));
                    K.PointLight("PadLight", t, p + Vector3.up * 0.85f, GlowAmber, 7f, 1.1f);
                    K.Cyl("PadLensTop", t, p + Vector3.up * 0.85f, 0.1f, 0.18f, K.Mat(GlowAmber, emission: GlowAmber * 0.8f), solid: false);
                }
        }

        // ── 告知/天候ボード（WeatherBoardManager の Canvas を温存） ──
        private static void BuildNoticeBoard(Transform basecamp, Vector3 center, float topY)
        {
            var wb = GameObject.Find("WeatherBoard")?.transform;
            if (wb == null) return;
            wb.position = new Vector3(center.x - 10.5f, topY, center.z - 1.5f);
            wb.localRotation = Quaternion.Euler(0f, 90f, 0f); // 表示面を camp 中央(+X)へ

            // 元の Cube ビジュアル/当たり判定は隠す（WeatherBoardManager と Canvas は温存）
            var mr = wb.GetComponent<MeshRenderer>(); if (mr != null) mr.enabled = false;
            var col = wb.GetComponent<Collider>(); if (col != null) col.enabled = false;

            // Canvas を読みやすい高さへ持ち上げる
            var canvas = wb.Find("BoardCanvas");
            if (canvas != null)
            {
                var lp = canvas.localPosition;
                canvas.localPosition = new Vector3(lp.x, 1.75f, lp.z);
            }

            // 木枠ボード本体＋支柱＋小屋根（ローカル：wb 原点 = pad 天面）
            var t = wb;
            K.Cyl("Post_L", t, new Vector3(-1.5f, 1.0f, -0.12f), 0.12f, 2.0f, K.Mat(Timber));
            K.Cyl("Post_R", t, new Vector3(1.5f, 1.0f, -0.12f), 0.12f, 2.0f, K.Mat(Timber));
            K.Box("Panel", t, new Vector3(0f, 1.75f, -0.12f), new Vector3(3.2f, 1.7f, 0.12f), K.Mat(TimberLight));
            K.Box("Frame_T", t, new Vector3(0f, 2.65f, -0.12f), new Vector3(3.5f, 0.18f, 0.18f), K.Mat(TimberDark), solid: false);
            K.Box("Frame_B", t, new Vector3(0f, 0.9f, -0.12f), new Vector3(3.5f, 0.14f, 0.16f), K.Mat(TimberDark), solid: false);
            // 雨よけの小屋根
            K.Box("Roof_L", t, new Vector3(-0.9f, 2.95f, -0.12f), new Vector3(2.0f, 0.07f, 0.9f), K.Mat(CanvasCream), solid: false, eulerLocal: new Vector3(22f, 0f, 0f));
            K.Box("Roof_R", t, new Vector3(0.9f, 2.95f, -0.12f), new Vector3(2.0f, 0.07f, 0.9f), K.Mat(CanvasCream), solid: false, eulerLocal: new Vector3(22f, 180f, 0f));
        }

        // ── 外周の丸太柵（ゲート側だけ開口）＋ランタン ──────────────
        private static void BuildPerimeter(Transform t)
        {
            var post = K.Mat(Timber);
            var rail = K.Mat(TimberDark);
            const float step = 3.0f;

            for (int side = 0; side < 4; side++)
            {
                // side: 0=+Z(ゲート/開口) 1=-Z 2=+X 3=-X
                for (float p = -Perimeter; p <= Perimeter + 0.01f; p += step)
                {
                    Vector3 postPos;
                    if (side == 0) postPos = new Vector3(p, 0f, Perimeter);
                    else if (side == 1) postPos = new Vector3(p, 0f, -Perimeter);
                    else if (side == 2) postPos = new Vector3(Perimeter, 0f, p);
                    else postPos = new Vector3(-Perimeter, 0f, p);

                    // ゲート側(+Z)の中央は開口（柵を置かない）
                    if (side == 0 && Mathf.Abs(p) < GateOpening) continue;

                    K.Cyl($"FencePost_{side}_{p:F0}", t, postPos + Vector3.up * 0.6f, 0.12f, 1.2f, post);
                }

                // 横レール（2 段）。開口部はスキップ。
                BuildRails(t, side, rail);
            }

            // 四隅のランタン柱
            BuildLantern(t, new Vector3(Perimeter, 0f, Perimeter));
            BuildLantern(t, new Vector3(-Perimeter, 0f, Perimeter));
            BuildLantern(t, new Vector3(Perimeter, 0f, -Perimeter));
            BuildLantern(t, new Vector3(-Perimeter, 0f, -Perimeter));
        }

        private static void BuildRails(Transform t, int side, Material rail)
        {
            // 各辺を 2 区間（開口を挟む +Z 以外は 1 本でも可）に分けて細い横木を渡す
            float[] heights = { 0.5f, 1.0f };
            foreach (var hy in heights)
            {
                if (side == 0)
                {
                    // +Z は開口の左右 2 本
                    float segLen = (Perimeter - GateOpening) * 0.5f;
                    float cxOff = (GateOpening + Perimeter) * 0.5f;
                    K.Box($"Rail_z_l_{hy:F0}", t, new Vector3(-cxOff, hy, Perimeter), new Vector3(Perimeter - GateOpening, 0.08f, 0.08f), rail, solid: false);
                    K.Box($"Rail_z_r_{hy:F0}", t, new Vector3(cxOff, hy, Perimeter), new Vector3(Perimeter - GateOpening, 0.08f, 0.08f), rail, solid: false);
                }
                else if (side == 1)
                    K.Box($"Rail_z_b_{hy:F0}", t, new Vector3(0f, hy, -Perimeter), new Vector3(Perimeter * 2f, 0.08f, 0.08f), rail, solid: false);
                else if (side == 2)
                    K.Box($"Rail_x_e_{hy:F0}", t, new Vector3(Perimeter, hy, 0f), new Vector3(0.08f, 0.08f, Perimeter * 2f), rail, solid: false);
                else
                    K.Box($"Rail_x_w_{hy:F0}", t, new Vector3(-Perimeter, hy, 0f), new Vector3(0.08f, 0.08f, Perimeter * 2f), rail, solid: false);
            }
        }

        private static void BuildLantern(Transform parent, Vector3 basePos)
        {
            K.Cyl("LanternPost", parent, basePos + Vector3.up * 1.3f, 0.1f, 2.6f, K.Mat(TimberDark));
            K.Box("LanternArm", parent, basePos + new Vector3(0f, 2.5f, 0f), new Vector3(0.7f, 0.08f, 0.08f), K.Mat(TimberDark), solid: false);
            var cagePos = basePos + new Vector3(0.3f, 2.35f, 0f);
            K.Box("LanternCage", parent, cagePos, new Vector3(0.3f, 0.4f, 0.3f), K.Mat(Iron, 0.3f, 0.5f), solid: false);
            K.Box("LanternGlow", parent, cagePos, new Vector3(0.16f, 0.26f, 0.16f), K.Mat(GlowAmber, emission: GlowAmber * 1.2f), solid: false);
            var l = K.PointLight("LanternLight", parent, cagePos, WarmLight, 9f, 1.6f);
            l.gameObject.AddComponent<CampLightFlicker>().Init(l, null, 0.12f, 5f);
        }

        // ── 地面デカール（土の広場・通路の踏み跡） ──────────────────
        private static void BuildGroundDecals(Transform t)
        {
            // 中央の踏み固められた広場（薄い円盤）
            K.Cyl("Plaza", t, new Vector3(0f, 0.02f, 0f), 9f, 0.02f, K.Mat(new Color(0.27f, 0.22f, 0.16f), 0.0f), solid: false);
            // ゲートへ続く通路
            K.Box("PathToGate", t, new Vector3(0f, 0.025f, 7.5f), new Vector3(3.0f, 0.02f, 9f), K.Mat(new Color(0.30f, 0.25f, 0.18f), 0.0f), solid: false);
            // 搬出デッキへ続く通路
            K.Box("PathToDeck", t, new Vector3(0f, 0.025f, -7.5f), new Vector3(3.0f, 0.02f, 9f), K.Mat(new Color(0.30f, 0.25f, 0.18f), 0.0f), solid: false);
        }

        /// <summary>
        /// 拠点（リスポーンの安全地帯）の XZ フットプリント内に紛れ込んだ散在物を片付ける：
        ///   ・Hazards（落石/氷床など）…… 安全地帯に湧いた危険物は誤配置なので非アクティブ化する。
        ///   ・ZoneRuntime の "Marker"（スポーン点の可視ギズモ）…… 見た目だけ無効化（スポーンは transform 参照で継続）。
        /// 機能オブジェクト（Relics / ClimbingPoints / RouteGate Blocker / 機能トリガー）には触れない。
        /// </summary>
        private static void ClearCampFootprint(float half)
        {
            int hazards = 0, markers = 0;
            var hz = GameObject.Find("Hazards");
            if (hz != null)
                foreach (Transform c in hz.transform)
                {
                    var p = c.position;
                    if (Mathf.Abs(p.x) <= half && Mathf.Abs(p.z) <= half)
                    {
                        c.gameObject.SetActive(false);
                        hazards++;
                    }
                }

            var zr = GameObject.Find("ZoneRuntime");
            if (zr != null)
                foreach (var mr in zr.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.name != "Marker") continue;
                    var p = mr.transform.position;
                    if (Mathf.Abs(p.x) <= half && Mathf.Abs(p.z) <= half) { mr.enabled = false; markers++; }
                }

            if (hazards + markers > 0)
                Debug.Log($"[BasecampBuilder] 拠点内の散在物を整理: hazards={hazards} 無効化 / markers={markers} 非表示");
        }

        // ── ユーティリティ ─────────────────────────────────────────
        private static void FoldHelipadMarker(Vector3 center, float topY)
        {
            var hp = GameObject.Find("HelipadMarker");
            if (hp == null) return;
            // 旧マーカーは埋没した純装飾。搬出デッキ脇へ移し、見た目は無効化（デッキ側が代替）。
            hp.transform.position = new Vector3(center.x + 6f, topY, center.z - ReturnDist);
            foreach (var r in hp.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            foreach (var c in hp.GetComponentsInChildren<Collider>(true)) c.enabled = false;
        }

        private static void ClearChildrenExcept(Transform parent, string keepName)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var c = parent.GetChild(i);
                if (keepName != null && c.name == keepName) continue;
                Object.Destroy(c.gameObject);
            }
        }

        private static void DestroyIfExists(Transform parent, string childName)
        {
            var c = parent.Find(childName);
            if (c != null) Object.Destroy(c.gameObject);
        }
    }
}
