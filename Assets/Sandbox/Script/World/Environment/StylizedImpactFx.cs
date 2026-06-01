using UnityEngine;

namespace Sandbox.World.Environment
{
    /// <summary>
    /// PEAK 風のポップな衝撃バースト VFX を「コードのみ・プレハブ/アセット不要」で生成する軽量ユーティリティ。
    /// リアルではなく、誇張された漫画的な砂煙/破片を一度だけ噴出して自動破棄する（ドタバタ感）。
    /// 落石・氷・崩落・ラグドール衝突など各所から <see cref="Spawn"/> を呼ぶ（DRY）。
    ///
    /// 設計方針:
    ///  - URP 未設定環境のマゼンタ化を避けるため、必ず URP Particles/Unlit を明示割り当て。
    ///  - 加算ブレンド＋明るめ色で Bloom が拾い、ローポリでもリッチに見える（引き算の美学を補助）。
    ///  - StopAction=Destroy で寿命後に自動消滅。プールはしない（イベント頻度が低く KISS 優先）。
    /// </summary>
    public static class StylizedImpactFx
    {
        private static Material s_dustMat;

        /// <summary>
        /// 指定位置にポップな衝撃バーストを発生させる。
        /// </summary>
        /// <param name="position">発生ワールド座標（衝突点など）。</param>
        /// <param name="color">主たる砂煙/破片の色。</param>
        /// <param name="scale">大きさ・勢いのスケール（衝突速度に比例させると気持ちよい）。</param>
        /// <param name="burst">噴出パーティクル数。</param>
        public static void Spawn(Vector3 position, Color color, float scale = 1f, int burst = 16)
        {
            scale = Mathf.Clamp(scale, 0.3f, 4f);

            var go = new GameObject("StylizedImpactFx");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.4f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f * scale, 5.0f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f * scale, 0.95f * scale);
            main.startColor = color;
            main.gravityModifier = 0.35f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 80;
            main.stopAction = ParticleSystemStopAction.Destroy; // 寿命後に GameObject ごと自動破棄

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(4, burst)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f * scale;

            // 寿命で縮小しつつフェードアウト（漫画的な poof）
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.6f), new Keyframe(0.25f, 1.0f), new Keyframe(1f, 0.05f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.35f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetDustMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.View;

            ps.Play();
        }

        /// <summary>
        /// 収集の「ポンッ」。上向きコーンに弾ける明るいスパークル（金=遺物 / 白シアン=アイテム）。
        /// 砂煙 <see cref="Spawn"/> より小さく・速く・キラッと。color に主色を渡す。
        /// </summary>
        public static void CollectPop(Vector3 position, Color color, float scale = 1f, int burst = 16)
        {
            scale = Mathf.Clamp(scale, 0.3f, 4f);

            var go = new GameObject("StylizedCollectPop");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f * scale, 4.5f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f * scale, 0.18f * scale);
            main.startColor = color;
            main.gravityModifier = 0.25f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(8, burst)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.05f * scale;
            shape.rotation = new Vector3(-90f, 0f, 0f); // コーンを上向き(+Y)に

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.5f), new Keyframe(0.2f, 1.0f), new Keyframe(1f, 0.0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(color, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.9f, 0.3f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetDustMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.View;

            ps.Play();
        }

        /// <summary>
        /// 山頂到達の一発お祝い紙吹雪。多色のヒラヒラがアーチを描いて舞い落ちる（一度きり）。
        /// 既存 SummitVisualEffects の常時アンビエント雲とは別物のワンショットバースト。
        /// </summary>
        public static void Confetti(Vector3 position, float scale = 1f, int burst = 80)
        {
            scale = Mathf.Clamp(scale, 0.3f, 4f);

            var go = new GameObject("StylizedConfetti");
            go.transform.position = position;

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.duration = 0.3f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.0f, 6.0f); // 長い滞空
            main.startSpeed = new ParticleSystem.MinMaxCurve(8.0f * scale, 12.0f * scale);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f * scale, 0.35f * scale);
            main.gravityModifier = 0.8f; // 打ち上がってから舞い落ちる
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 160;
            main.stopAction = ParticleSystemStopAction.Destroy;

            // 多色（赤/黄/青/緑/マゼンタ）をパーティクルごとにランダム化
            var confettiGrad = new Gradient();
            confettiGrad.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1.00f, 0.25f, 0.25f), 0.00f),
                    new GradientColorKey(new Color(1.00f, 0.90f, 0.20f), 0.25f),
                    new GradientColorKey(new Color(0.20f, 0.50f, 1.00f), 0.50f),
                    new GradientColorKey(new Color(0.25f, 0.90f, 0.35f), 0.75f),
                    new GradientColorKey(new Color(1.00f, 0.30f, 0.85f), 1.00f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            var startColor = new ParticleSystem.MinMaxGradient(confettiGrad)
            {
                mode = ParticleSystemGradientMode.RandomColor
            };
            main.startColor = startColor;

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)Mathf.Max(24, burst)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.3f * scale;
            shape.rotation = new Vector3(-90f, 0f, 0f); // 上向き噴出

            var rol = ps.rotationOverLifetime;
            rol.enabled = true;
            rol.z = new ParticleSystem.MinMaxCurve(Mathf.Deg2Rad * -180f, Mathf.Deg2Rad * 180f); // ヒラヒラ回転

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.8f), new Keyframe(0.5f, 1.0f), new Keyframe(1f, 0.9f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var fade = new Gradient();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
            col.color = fade;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetDustMaterial();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.alignment = ParticleSystemRenderSpace.View;

            ps.Play();
        }

        private static Material GetDustMaterial()
        {
            if (s_dustMat != null) return s_dustMat;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Sprites/Default");
            s_dustMat = new Material(sh) { name = "StylizedImpactDustMat" };
            // 加算寄りの半透明（Bloom で映える）。プロパティが無いシェーダーでは無視される。
            if (s_dustMat.HasProperty("_Surface")) s_dustMat.SetFloat("_Surface", 1f); // Transparent
            if (s_dustMat.HasProperty("_Blend")) s_dustMat.SetFloat("_Blend", 1f);     // Additive
            return s_dustMat;
        }
    }
}
