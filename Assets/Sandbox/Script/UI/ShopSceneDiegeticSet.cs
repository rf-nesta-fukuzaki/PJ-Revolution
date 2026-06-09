using UnityEngine;
using Sandbox.World.Environment;

namespace Sandbox.UI
{
    /// <summary>
    /// Shop シーン用の手続き 3D ベースキャンプ（R.E.P.O. 基地端末 + PEAK 山岳遠景）。
    /// Primitive のみ。UI オーバーレイの背後に diegetic な奥行きを与える。
    /// カメラはプレップカウンター（出発準備机）を正面に捉える構図。
    /// </summary>
    public static class ShopSceneDiegeticSet
    {
        private const string RootName = "ShopDiegeticSet";

        // 出発準備カウンターの中心（カメラの注視点）
        public static readonly Vector3 CounterFocus = new(0f, 1.0f, 2.2f);

        public static void Ensure(Transform sceneRoot)
        {
            if (sceneRoot == null) return;
            if (sceneRoot.Find(RootName) != null) return;

            var root = new GameObject(RootName);
            root.transform.SetParent(sceneRoot, false);

            SetupLighting(root.transform);
            SetupAtmosphere(root.transform);
            BuildSkyBackdrop(root.transform);
            BuildPergola(root.transform);
            BuildFloorAndWalls(root.transform);
            BuildPrepCounter(root.transform);
            BuildShelter(root.transform);
            BuildSupplyClutter(root.transform);
            BuildCampfire(root.transform);
            BuildDistantPeaks(root.transform);
        }

        /// <summary>
        /// 既存の URP シネマティック・ポストプロセス（Bloom/ACES/ColorAdjust/SplitToning/Vignette/Grain）
        /// を Shop にも適用する。ランタンや焚き火のハイライトに自然な発光と暖色のフィルミックな色域を与える。
        /// 屋外キャンプ向けにブルームの閾値を下げ、暮色を強めるプリセットで生成する。
        /// </summary>
        private static void SetupAtmosphere(Transform root)
        {
            var go = new GameObject("ShopAtmosphere");
            go.transform.SetParent(root, false);
            go.SetActive(false); // OnEnable(Build) 前にプリセットを差し込むため一旦無効化
            var vps = go.AddComponent<VolumeProfileSetup>();
            vps.ApplyCozyDuskPreset();
            go.SetActive(true);
        }

        /// <summary>
        /// PEAK 風の夕景の空。黒い空虚を温かい夕空グラデの巨大な無光沢板で置き換える。
        /// 低いパラペット越しに遠景の峰の背後へ広がる。
        /// </summary>
        private static void BuildSkyBackdrop(Transform root)
        {
            var sky = GameObject.CreatePrimitive(PrimitiveType.Quad);
            sky.name = "SkyBackdrop";
            var col = sky.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);
            sky.transform.SetParent(root, false);
            sky.transform.localPosition = new Vector3(0f, 10f, 34f);
            sky.transform.localScale = new Vector3(150f, 80f, 1f);

            var rend = sky.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture");
            var mat = new Material(shader);
            mat.mainTexture = BuildDuskSkyTexture();
            rend.sharedMaterial = mat;
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static Texture2D s_duskSky;
        private static Texture2D BuildDuskSkyTexture()
        {
            if (s_duskSky != null) return s_duskSky;
            const int h = 128;
            var tex = new Texture2D(4, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            // 地平の暖橙 → 中段の赤紫 → 上部の紫紺（PEAK 夕景）
            Color bottom = new(0.62f, 0.36f, 0.24f);
            Color mid    = new(0.40f, 0.24f, 0.28f);
            Color top    = new(0.13f, 0.12f, 0.20f);
            var px = new Color[4 * h];
            for (int y = 0; y < h; y++)
            {
                float t = y / (float)(h - 1);
                Color c = t < 0.45f
                    ? Color.Lerp(bottom, mid, Mathf.InverseLerp(0f, 0.45f, t))
                    : Color.Lerp(mid, top, Mathf.InverseLerp(0.45f, 1f, t));
                for (int x = 0; x < 4; x++) px[y * 4 + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            s_duskSky = tex;
            return tex;
        }

        private static void SetupLighting(Transform root)
        {
            var sunGo = new GameObject("CampSun");
            sunGo.transform.SetParent(root, false);
            sunGo.transform.rotation = Quaternion.Euler(34f, -38f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.80f, 0.58f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;

            var fillGo = new GameObject("CampFill");
            fillGo.transform.SetParent(root, false);
            fillGo.transform.rotation = Quaternion.Euler(14f, 130f, 0f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.5f, 0.62f, 0.85f);
            fill.intensity = 0.62f;

            // カウンター周辺の底面反射（3D の沈みを防ぐ）
            var bounceGo = new GameObject("FloorBounce");
            bounceGo.transform.SetParent(root, false);
            bounceGo.transform.localPosition = new Vector3(0f, 0.4f, 2.8f);
            var bounce = bounceGo.AddComponent<Light>();
            bounce.type = LightType.Point;
            bounce.color = new Color(0.95f, 0.84f, 0.66f);
            bounce.intensity = 2.4f;
            bounce.range = 9f;

            // カウンターを照らす吊りランタン（暖色）
            var lanternGo = new GameObject("CounterLantern");
            lanternGo.transform.SetParent(root, false);
            lanternGo.transform.localPosition = new Vector3(0f, 2.6f, 2.0f);
            var lantern = lanternGo.AddComponent<Light>();
            lantern.type = LightType.Point;
            lantern.color = new Color(1f, 0.82f, 0.55f);
            lantern.intensity = 4.2f;
            lantern.range = 8.5f;
            CreatePrimitive(lanternGo.transform, "LanternBulb", PrimitiveType.Sphere,
                Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f), new Color(1f, 0.88f, 0.6f), emissive: 2.2f);
            CreatePrimitive(lanternGo.transform, "LanternWire", PrimitiveType.Cylinder,
                new Vector3(0f, 0.5f, 0f), new Vector3(0.012f, 0.5f, 0.012f), new Color(0.05f, 0.05f, 0.06f));
            CreatePrimitive(lanternGo.transform, "LanternShade", PrimitiveType.Cylinder,
                new Vector3(0f, -0.08f, 0f), new Vector3(0.22f, 0.14f, 0.22f),
                new Color(0.18f, 0.14f, 0.10f), smoothness: 0.25f);
            // 吊りロープ
            CreatePrimitive(lanternGo.transform, "LanternRope", PrimitiveType.Cylinder,
                new Vector3(0f, 1.1f, 0f), new Vector3(0.02f, 1.1f, 0.02f),
                new Color(0.38f, 0.30f, 0.18f));
        }

        /// <summary>カウンター上に渡した開放的な木のパーゴラ（屋根は張らない）。吊りランタンの構造材。</summary>
        private static void BuildPergola(Transform root)
        {
            // 4 隅の柱
            float[] xs = { -3.4f, 3.4f };
            float[] zs = { 0.6f, 4.0f };
            foreach (var x in xs)
            foreach (var z in zs)
            {
                CreatePrimitive(root, "PergolaPost", PrimitiveType.Cylinder,
                    new Vector3(x, 1.6f, z), new Vector3(0.16f, 1.6f, 0.16f),
                    new Color(0.26f, 0.19f, 0.12f), smoothness: 0.1f);
            }
            // 上端の桁（前後）
            for (int i = 0; i < 2; i++)
            {
                CreatePrimitive(root, $"PergolaBeamZ{i}", PrimitiveType.Cube,
                    new Vector3(-3.4f + i * 6.8f, 3.2f, 2.3f), new Vector3(0.16f, 0.16f, 4.2f),
                    new Color(0.30f, 0.22f, 0.14f), smoothness: 0.1f);
            }
            // 横方向の細い垂木（屋根は張らず空が透ける）
            for (int i = 0; i < 5; i++)
            {
                CreatePrimitive(root, $"PergolaRafter{i}", PrimitiveType.Cube,
                    new Vector3(-3.4f + i * 1.7f, 3.25f, 2.3f), new Vector3(0.10f, 0.10f, 7.0f),
                    new Color(0.27f, 0.20f, 0.13f), smoothness: 0.1f);
            }
        }

        private static void BuildFloorAndWalls(Transform root)
        {
            var floor = CreatePrimitive(root, "Floor", PrimitiveType.Cube,
                new Vector3(0f, -0.05f, 2.5f), new Vector3(18f, 0.1f, 14f),
                new Color(0.30f, 0.24f, 0.19f));

            // 床の木目板（暖色の縞）
            for (int i = 0; i < 11; i++)
            {
                CreatePrimitive(floor.transform, $"Plank{i}", PrimitiveType.Cube,
                    new Vector3(-6.5f + i * 1.3f, 0.055f, 0f), new Vector3(0.06f, 0.012f, 13f),
                    new Color(0.18f, 0.14f, 0.10f));
            }

            // 奥の低いパラペット（開放的な屋外キャンプ。遠景の峰と夕空を見せるため低く）
            CreatePrimitive(root, "BackWall", PrimitiveType.Cube,
                new Vector3(0f, 0.55f, 8.6f), new Vector3(18f, 1.5f, 0.3f),
                new Color(0.20f, 0.17f, 0.14f));
            // 左右の低い柵（腰高）
            CreatePrimitive(root, "SideWallL", PrimitiveType.Cube,
                new Vector3(-8.6f, 0.55f, 3f), new Vector3(0.3f, 1.5f, 12f),
                new Color(0.18f, 0.15f, 0.12f));
            CreatePrimitive(root, "SideWallR", PrimitiveType.Cube,
                new Vector3(8.6f, 0.55f, 3f), new Vector3(0.3f, 1.5f, 12f),
                new Color(0.18f, 0.15f, 0.12f));

            // カウンター背面の低い物資棚
            CreatePrimitive(root, "BackShelf", PrimitiveType.Cube,
                new Vector3(0f, 0.65f, 7.9f), new Vector3(6.5f, 1.3f, 0.4f),
                new Color(0.22f, 0.18f, 0.13f));
            for (int i = 0; i < 4; i++)
            {
                CreatePrimitive(root, $"ShelfItem{i}", PrimitiveType.Cube,
                    new Vector3(-2.4f + i * 1.6f, 1.4f, 7.85f), new Vector3(0.5f, 0.4f, 0.35f),
                    new Color(0.30f, 0.24f, 0.18f), smoothness: 0.15f);
            }

            // 掲示柱に立てた木製の遠征看板（端末風モニタは屋外キャンプに合わないため木板に）
            CreatePrimitive(root, "NoticePost", PrimitiveType.Cylinder,
                new Vector3(3.6f, 0.9f, 8.0f), new Vector3(0.12f, 0.9f, 0.12f),
                new Color(0.20f, 0.15f, 0.10f));
            var board = CreatePrimitive(root, "NoticeBoard", PrimitiveType.Cube,
                new Vector3(3.6f, 1.85f, 8.0f), new Vector3(2.0f, 1.2f, 0.08f),
                new Color(0.30f, 0.22f, 0.14f), smoothness: 0.1f);
            board.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            CreatePrimitive(board.transform, "NoticePaper", PrimitiveType.Cube,
                new Vector3(0f, 0.05f, -0.6f), new Vector3(0.7f, 0.8f, 0.02f),
                new Color(0.80f, 0.74f, 0.58f), emissive: 0.03f);

            // カウンター正面を照らすスポット（3D が沈まないよう）
            var spotGo = new GameObject("CounterSpot");
            spotGo.transform.SetParent(root, false);
            spotGo.transform.localPosition = new Vector3(0f, 3.4f, 0.8f);
            spotGo.transform.localRotation = Quaternion.Euler(48f, 0f, 0f);
            var spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1f, 0.86f, 0.68f);
            spot.intensity = 3.8f;
            spot.range = 14f;
            spot.spotAngle = 62f;
            spot.shadows = LightShadows.Soft;
        }

        private static void BuildPrepCounter(Transform root)
        {
            // カメラ正面の出発準備カウンター
            var top = CreatePrimitive(root, "CounterTop", PrimitiveType.Cube,
                new Vector3(0f, 0.92f, 2.3f), new Vector3(4.4f, 0.12f, 1.4f),
                new Color(0.26f, 0.20f, 0.15f));
            CreatePrimitive(top.transform, "CounterEdge", PrimitiveType.Cube,
                new Vector3(0f, -0.08f, 0.52f), new Vector3(4.4f, 0.18f, 0.12f),
                new Color(0.16f, 0.12f, 0.09f));
            CreatePrimitive(root, "CounterBacksplash", PrimitiveType.Cube,
                new Vector3(0f, 1.35f, 2.95f), new Vector3(4.2f, 0.7f, 0.08f),
                new Color(0.12f, 0.10f, 0.08f), smoothness: 0.08f);
            CreatePrimitive(root, "CounterSign", PrimitiveType.Cube,
                new Vector3(0f, 1.55f, 2.98f), new Vector3(2.2f, 0.28f, 0.04f),
                new Color(0.08f, 0.09f, 0.07f), emissive: 0.06f);
            // 脚
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
            {
                CreatePrimitive(root, "CounterLeg", PrimitiveType.Cube,
                    new Vector3(sx * 2.0f, 0.45f, 2.3f + sz * 0.55f), new Vector3(0.16f, 0.9f, 0.16f),
                    new Color(0.14f, 0.11f, 0.08f));
            }

            // カウンター上の備品（地図・ランタン缶・小物）
            CreatePrimitive(root, "Map", PrimitiveType.Cube,
                new Vector3(-1.4f, 1.0f, 2.3f), new Vector3(1.0f, 0.02f, 0.7f),
                new Color(0.78f, 0.72f, 0.55f), emissive: 0.02f);
            CreatePrimitive(root, "Mug", PrimitiveType.Cylinder,
                new Vector3(1.5f, 1.06f, 2.0f), new Vector3(0.18f, 0.12f, 0.18f),
                new Color(0.5f, 0.45f, 0.4f));
            CreatePrimitive(root, "Piton", PrimitiveType.Cube,
                new Vector3(0.6f, 1.0f, 2.5f), new Vector3(0.5f, 0.04f, 0.2f),
                new Color(0.6f, 0.62f, 0.66f), metallic: 0.7f, smoothness: 0.5f);
        }

        private static void BuildShelter(Transform root)
        {
            // 右手のテント（傾けたキャンバス屋根）
            var post1 = CreatePrimitive(root, "TentPost1", PrimitiveType.Cylinder,
                new Vector3(5.2f, 1.0f, 4.2f), new Vector3(0.1f, 1.0f, 0.1f),
                new Color(0.22f, 0.16f, 0.10f));
            var roof = CreatePrimitive(root, "TentCanvas", PrimitiveType.Cube,
                new Vector3(5.6f, 2.0f, 4.6f), new Vector3(3.2f, 0.08f, 3.0f),
                new Color(0.55f, 0.42f, 0.28f));
            roof.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
        }

        private static void BuildSupplyClutter(Transform root)
        {
            CreatePrimitive(root, "CrateA", PrimitiveType.Cube,
                new Vector3(-4.2f, 0.45f, 3.4f), new Vector3(1.0f, 0.9f, 1.0f),
                new Color(0.30f, 0.23f, 0.16f));
            CreatePrimitive(root, "CrateAStrap", PrimitiveType.Cube,
                new Vector3(-4.2f, 0.45f, 3.88f), new Vector3(1.05f, 0.12f, 0.04f),
                new Color(0.20f, 0.16f, 0.12f));
            CreatePrimitive(root, "CrateB", PrimitiveType.Cube,
                new Vector3(-4.3f, 1.15f, 3.2f), new Vector3(0.7f, 0.5f, 0.7f),
                new Color(0.26f, 0.20f, 0.14f));
            CreatePrimitive(root, "CrateC", PrimitiveType.Cube,
                new Vector3(-3.3f, 0.35f, 4.0f), new Vector3(0.8f, 0.7f, 0.8f),
                new Color(0.24f, 0.18f, 0.13f));
            // ロープコイル
            CreatePrimitive(root, "RopeCoilA", PrimitiveType.Cylinder,
                new Vector3(4.4f, 0.12f, 3.0f), new Vector3(0.55f, 0.1f, 0.55f),
                new Color(0.42f, 0.34f, 0.22f));
            CreatePrimitive(root, "RopeCoilB", PrimitiveType.Cylinder,
                new Vector3(-2.6f, 0.95f, 2.2f), new Vector3(0.4f, 0.07f, 0.4f),
                new Color(0.40f, 0.32f, 0.20f));
            // バックパック
            CreatePrimitive(root, "Backpack", PrimitiveType.Capsule,
                new Vector3(2.9f, 0.5f, 3.6f), new Vector3(0.7f, 0.6f, 0.55f),
                new Color(0.34f, 0.28f, 0.22f));
        }

        private static void BuildCampfire(Transform root)
        {
            var pit = CreatePrimitive(root, "FirePit", PrimitiveType.Cylinder,
                new Vector3(-5.4f, 0.08f, 5.6f), new Vector3(0.9f, 0.06f, 0.9f),
                new Color(0.08f, 0.08f, 0.09f));

            var fireLightGo = new GameObject("FireLight");
            fireLightGo.transform.SetParent(pit.transform, false);
            fireLightGo.transform.localPosition = new Vector3(0f, 4f, 0f);
            var fire = fireLightGo.AddComponent<Light>();
            fire.type = LightType.Point;
            fire.color = new Color(1f, 0.5f, 0.18f);
            fire.intensity = 2.8f;
            fire.range = 10f;

            CreatePrimitive(pit.transform, "Ember", PrimitiveType.Sphere,
                new Vector3(0f, 2.2f, 0f), new Vector3(0.45f, 1.6f, 0.45f),
                new Color(0.98f, 0.5f, 0.14f), emissive: 1.6f);
        }

        private static void BuildDistantPeaks(Transform root)
        {
            // 奥壁の上に覗く遠景の峰（窓越しの夕景イメージ・ギザギザ）
            AddPeakCluster(root, "PeakFarL", new Vector3(-9f, 5.5f, 16f),
                new Color(0.16f, 0.14f, 0.20f), 8f, 8f);
            AddPeakCluster(root, "PeakFarC", new Vector3(2f, 7.5f, 18f),
                new Color(0.12f, 0.11f, 0.17f), 11f, 12f);
            AddPeakCluster(root, "PeakFarR", new Vector3(11f, 5f, 17f),
                new Color(0.10f, 0.10f, 0.15f), 7f, 7f);
        }

        private static void AddPeakCluster(Transform root, string name, Vector3 basePos, Color color, float width, float height)
        {
            CreatePrimitive(root, name, PrimitiveType.Cube,
                basePos, new Vector3(width, height * 0.55f, 1f), color);
            CreatePrimitive(root, name + "_Spire", PrimitiveType.Cube,
                basePos + new Vector3(width * 0.15f, height * 0.35f, 0f),
                new Vector3(width * 0.35f, height * 0.55f, 1f), Color.Lerp(color, Color.white, 0.08f));
            CreatePrimitive(root, name + "_Shoulder", PrimitiveType.Cube,
                basePos + new Vector3(-width * 0.28f, height * 0.05f, 0f),
                new Vector3(width * 0.4f, height * 0.35f, 1f), Color.Lerp(color, Color.black, 0.12f));
        }

        private static GameObject CreatePrimitive(Transform parent, string name, PrimitiveType type,
            Vector3 pos, Vector3 scale, Color color, float emissive = 0f, float metallic = 0f, float smoothness = 0.1f)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;

            var col = go.GetComponent<Collider>();
            if (col != null) Object.Destroy(col);

            var rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = color;
                mat.SetFloat("_Metallic", metallic);
                mat.SetFloat("_Smoothness", smoothness);
                if (emissive > 0f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", color * emissive);
                }
                rend.sharedMaterial = mat;
            }
            return go;
        }
    }
}
