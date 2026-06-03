using UnityEngine;
using Sandbox.World.Environment;

/// <summary>
/// <see cref="WireRopeActionController"/> の視覚表現（LineRenderer によるロープ／頭上ラッソ描画と
/// フック生成・追従・破棄、接続/衝突の演出 FX）を物理ロジックから分離した描画コンポーネント。
///
/// 責務分担：コントローラは「何を・どこに」描くか（アンカー・たわみ・太さ・張力イベント）を物理状態から決め、
/// 本クラスは「どう描くか」（撚りワイヤーの質感・円筒シェーディング・巻き取りスクロール・弦の振動・
/// スタイライズドフックの寿命管理・接触バースト）だけを担う。
/// すべて実行時生成・実行時マテリアル（アセットや他シーンを汚さない）。
/// </summary>
[DisallowMultipleComponent]
public sealed class RopeRenderer : MonoBehaviour
{
    private const int SpinSegments = 36;
    private const int RopeLineSegments = 20;
    private const string VisualChildName = "WireRopeVisual";

    // 弦の「ビヨン」振動。アタッチ/張力スパイク/スリングショットで AddTwang される。
    private const float TwangFrequency = 34f;     // 一次モードの角速度
    private const float TwangDecayPerSec = 0.42f;  // 振幅 / 秒の減衰
    private const float TwangMaxAmplitude = 0.16f; // 振幅上限[m]

    private LineRenderer _lineRenderer;
    private Transform _visualRoot;
    private Transform _hook;
    private float _hookScale = HookNominalScale;
    private const float HookNominalScale = 0.26f;
    private float _hookSpin;

    private float _startWidth = 0.09f;
    private float _endWidth = 0.05f;
    private Color _tint = new Color(0.95f, 0.82f, 0.45f, 1f);

    private Material _lineMaterial;       // インスタンス（巻き取りスクロールを個別に動かす）
    private float _uvOffset;
    private float _reelScroll;            // U スクロール速度（巻き取り中 > 0）

    private float _twang;                 // 現在の振動振幅[m]（減衰）
    private float _twangTime;             // 振動位相

    private static Texture2D s_braidTexture;
    private static Material s_hookMaterial;
    private static Material s_hookTipMaterial;

    private Camera _viewCamera;

    /// <summary>コントローラが Awake で 1 回呼ぶ。LineRenderer 階層を整え、撚りワイヤーの質感・色を設定する。</summary>
    public void Configure(float startWidth, float endWidth, Color color)
    {
        _startWidth = startWidth;
        _endWidth = endWidth;
        _tint = color;
        EnsureVisualHierarchy();
    }

    private void EnsureVisualHierarchy()
    {
        _visualRoot = transform.Find(VisualChildName);
        if (_visualRoot == null)
        {
            var go = new GameObject(VisualChildName);
            go.transform.SetParent(transform, false);
            _visualRoot = go.transform;
        }

        _lineRenderer = _visualRoot.GetComponent<LineRenderer>();
        if (_lineRenderer == null)
            _lineRenderer = _visualRoot.gameObject.AddComponent<LineRenderer>();

        ConfigureLineRenderer(_lineRenderer);
    }

    private void ConfigureLineRenderer(LineRenderer lr)
    {
        lr.enabled = false;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        lr.textureMode = LineTextureMode.Stretch; // U=全長 0..1、V=太さ方向。撚り柄は texture 側で繰り返す。
        lr.alignment = LineAlignment.View;
        lr.numCapVertices = 8;
        lr.numCornerVertices = 6;
        lr.startWidth = _startWidth;
        lr.endWidth = _endWidth;

        // 手元をわずかに明るく、先端を落として奥行きを出す（質感は texture が担うので頂点色は控えめ）。
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.82f, 0.82f, 0.82f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        lr.colorGradient = grad;

        // インスタンスマテリアルの寿命は本クラスが管理するため sharedMaterial で割り当てる
        // （lr.material のゲッターが自動複製して二重解放になるのを避ける）。
        lr.sharedMaterial = GetLineMaterial();
    }

    private Material GetLineMaterial()
    {
        if (_lineMaterial != null) return _lineMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Texture")
                     ?? Shader.Find("Sprites/Default");
        if (shader == null) return null;

        _lineMaterial = new Material(shader) { name = "WireRopeBraid" };
        Texture2D braid = GetBraidTexture();
        if (_lineMaterial.HasProperty("_BaseMap"))
        {
            _lineMaterial.SetTexture("_BaseMap", braid);
            _lineMaterial.SetColor("_BaseColor", _tint);
        }
        else
        {
            _lineMaterial.mainTexture = braid;
            _lineMaterial.color = _tint;
        }
        return _lineMaterial;
    }

    public void SetVisible(bool visible)
    {
        if (_lineRenderer != null)
            _lineRenderer.enabled = visible;
        if (!visible)
        {
            _twang = 0f;
            _reelScroll = 0f;
        }
    }

    public void SetWidths(float startWidth, float endWidth)
    {
        if (_lineRenderer == null) return;
        _lineRenderer.startWidth = startWidth;
        _lineRenderer.endWidth = endWidth;
    }

    /// <summary>ロープを弾く（接続・張力スパイク・離脱で呼ぶ）。弦のように減衰振動する。</summary>
    public void AddTwang(float amplitude)
    {
        _twang = Mathf.Min(TwangMaxAmplitude, _twang + Mathf.Abs(amplitude));
        _twangTime = 0f;
    }

    /// <summary>巻き取りスクロール速度。撚り柄が流れて「ケーブルが手繰り寄せられている」感を出す。0 で停止。</summary>
    public void SetReelScroll(float speed) => _reelScroll = speed;

    /// <summary>手元→アンカー間を、中央が下へたわむ曲線で描く。張力振動・巻き取りスクロールを反映する。</summary>
    public void DrawCurve(Vector3 start, Vector3 end, float sag, float startWidth)
    {
        if (_lineRenderer == null) return;
        float dt = Mathf.Max(0f, Time.deltaTime);
        AdvanceDynamics(dt);

        SetVisible(true);
        _lineRenderer.startWidth = startWidth;
        _lineRenderer.endWidth = _endWidth;

        Vector3 axis = end - start;
        float len = axis.magnitude;
        Vector3 dir = len > 0.001f ? axis / len : Vector3.forward;
        GetTwangBasis(dir, out Vector3 perpA, out Vector3 perpB);

        int count = RopeLineSegments + 1;
        if (_lineRenderer.positionCount != count)
            _lineRenderer.positionCount = count;

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)RopeLineSegments;
            Vector3 p = Vector3.Lerp(start, end, t);
            p += Vector3.down * (sag * 4f * t * (1f - t));
            if (_twang > 0.0001f)
                p += ComputeTwangOffset(t, perpA, perpB);
            _lineRenderer.SetPosition(i, p);
        }
    }

    /// <summary>手元のテザー＋頭上で回る輪（カウボーイラッソ）を描く。</summary>
    public void DrawLasso(Vector3 hand, Vector3 center, Vector3 axisRight, Vector3 axisForward,
                          float radius, float spinAngle, float attachAngle, float startWidth, float endWidth)
    {
        if (_lineRenderer == null) return;
        AdvanceDynamics(Mathf.Max(0f, Time.deltaTime));

        int circleCount = SpinSegments + 1;
        int totalCount = 1 + circleCount;
        if (_lineRenderer.positionCount != totalCount)
            _lineRenderer.positionCount = totalCount;
        _lineRenderer.SetPosition(0, hand);

        for (int i = 0; i < circleCount; i++)
        {
            float angle = (attachAngle + spinAngle + (360f / SpinSegments) * i) * Mathf.Deg2Rad;
            Vector3 onCircle = center + (axisRight * Mathf.Cos(angle) + axisForward * Mathf.Sin(angle)) * radius;
            _lineRenderer.SetPosition(1 + i, onCircle);
        }

        _lineRenderer.startWidth = startWidth;
        _lineRenderer.endWidth = endWidth;
    }

    private void AdvanceDynamics(float dt)
    {
        if (_twang > 0f)
        {
            _twangTime += dt;
            _twang = Mathf.MoveTowards(_twang, 0f, TwangDecayPerSec * dt);
        }

        if (Mathf.Abs(_reelScroll) > 0.0001f && _lineMaterial != null)
        {
            // 全長 0..1 にマップされた撚り柄を流す（負方向＝手元へ手繰り寄せる向き）。
            _uvOffset = Mathf.Repeat(_uvOffset - _reelScroll * dt, 1f);
            if (_lineMaterial.HasProperty("_BaseMap"))
                _lineMaterial.SetTextureOffset("_BaseMap", new Vector2(_uvOffset, 0f));
            else
                _lineMaterial.mainTextureOffset = new Vector2(_uvOffset, 0f);
        }
    }

    private void GetTwangBasis(Vector3 dir, out Vector3 perpA, out Vector3 perpB)
    {
        perpA = Vector3.Cross(dir, Vector3.up);
        if (perpA.sqrMagnitude < 0.01f)
            perpA = Vector3.Cross(dir, Vector3.forward);
        perpA.Normalize();
        perpB = Vector3.Cross(dir, perpA).normalized;
    }

    private Vector3 ComputeTwangOffset(float t, Vector3 perpA, Vector3 perpB)
    {
        // 両端固定の弦：基本モード sin(pi t) と二次モード sin(2pi t) を別位相で重ねる。
        float mode1 = Mathf.Sin(t * Mathf.PI) * Mathf.Sin(_twangTime * TwangFrequency);
        float mode2 = Mathf.Sin(t * Mathf.PI * 2f) * Mathf.Sin(_twangTime * TwangFrequency * 1.73f + 1.1f);
        return perpA * (_twang * mode1) + perpB * (_twang * 0.45f * mode2);
    }

    // ── スタイライズド・フック ────────────────────────────────
    public void SpawnHook(Vector3 origin)
    {
        ClearHook();

        var root = new GameObject("WireRopeHook").transform;
        root.position = origin;
        root.localScale = Vector3.one * _hookScale;

        // 撚りワイヤー先端の金属フック：芯シャフト＋三本の爪＋光る先端。
        Material steel = GetHookMaterial();
        Material tip = GetHookTipMaterial();

        var shaft = CreatePart(PrimitiveType.Cylinder, root, steel);
        shaft.localScale = new Vector3(0.14f, 0.5f, 0.14f);
        shaft.localPosition = new Vector3(0f, 0f, 0.02f);
        shaft.localRotation = Quaternion.Euler(90f, 0f, 0f); // 長軸を +Z（飛翔方向）へ

        // 爪（3 本）を先端から後ろへ反らせて開く（引っ掛ける形）。
        for (int i = 0; i < 3; i++)
        {
            float deg = i * 120f;
            var claw = CreatePart(PrimitiveType.Cube, root, steel);
            claw.localScale = new Vector3(0.08f, 0.08f, 0.58f);
            Quaternion around = Quaternion.AngleAxis(deg, Vector3.forward);
            Vector3 outDir = around * Vector3.up;
            claw.localPosition = new Vector3(0f, 0f, 0.4f) + outDir * 0.15f;
            claw.localRotation = Quaternion.LookRotation(
                Quaternion.AngleAxis(42f, around * Vector3.right) * Vector3.forward, outDir);
        }

        var tipSphere = CreatePart(PrimitiveType.Sphere, root, tip);
        tipSphere.localScale = Vector3.one * 0.2f;
        tipSphere.localPosition = new Vector3(0f, 0f, 0.44f);

        _hook = root;
        _hookSpin = 0f;
    }

    private static Transform CreatePart(PrimitiveType type, Transform parent, Material mat)
    {
        var go = GameObject.CreatePrimitive(type);
        var col = go.GetComponent<Collider>();
        if (col != null) Destroy(col);
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null)
        {
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    public void SyncHook(Vector3 pos, float dt)
    {
        if (_hook == null) return;

        _hook.position = pos;
        _hookScale = Mathf.Lerp(_hookScale, HookNominalScale, dt * 14f);
        _hook.localScale = Vector3.one * _hookScale;

        // 飛翔中の手応えとして緩く回転（追従ベクトルが取れないので素朴な自転）。
        _hookSpin += dt * 220f;
        _hook.rotation = Quaternion.Euler(0f, _hookSpin, _hookSpin * 0.4f);
    }

    public void PulseHook(float scaleMul)
    {
        if (_hook == null) return;
        _hookScale = HookNominalScale * scaleMul;
    }

    public void ClearHook()
    {
        if (_hook == null) return;
        Destroy(_hook.gameObject);
        _hook = null;
    }

    /// <summary>接続・スリングショット時の漫画的バースト（砂煙＋金属スパーク）。</summary>
    public void HitBurst(Vector3 point, float scale, bool spark)
    {
        StylizedImpactFx.Spawn(point, new Color(0.85f, 0.78f, 0.6f, 1f), scale, Mathf.RoundToInt(10 * scale) + 6);
        if (spark)
            StylizedImpactFx.CollectPop(point, new Color(1f, 0.92f, 0.55f, 1f), scale * 0.8f, 12);
    }

    // ── 手続きアセット（静的・1 度だけ生成して共有） ──────────
    private static Texture2D GetBraidTexture()
    {
        if (s_braidTexture != null) return s_braidTexture;

        const int w = 128; // U（全長方向）— 撚りの繰り返し
        const int h = 16;   // V（太さ方向）— 円筒シェーディング
        var tex = new Texture2D(w, h, TextureFormat.RGB24, true)
        {
            name = "WireRopeBraidTex",
            wrapModeU = TextureWrapMode.Repeat,
            wrapModeV = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            anisoLevel = 2,
        };

        const float twists = 24f;           // 全長あたりの撚り本数
        var px = new Color32[w * h];
        for (int y = 0; y < h; y++)
        {
            float v = y / (float)(h - 1);            // 0..1 across
            float cyl = Mathf.Sin(v * Mathf.PI);     // 端=0, 中心=1 → 丸み
            float shade = Mathf.Lerp(0.30f, 1.18f, cyl);
            for (int x = 0; x < w; x++)
            {
                float u = x / (float)w;              // 0..1 along (repeat)
                // 撚り（対向 2 方向）で編み込み感を作る。
                float diag = Mathf.Repeat(u * twists + v, 1f);
                float groove = 0.74f + 0.26f * Mathf.Sin(diag * Mathf.PI * 2f);
                float diag2 = Mathf.Repeat(u * twists - v + 0.5f, 1f);
                float groove2 = 0.88f + 0.12f * Mathf.Sin(diag2 * Mathf.PI * 2f);
                float fiber = 0.96f + 0.04f * Hash01(x * 3 + 1, y * 7 + 3);
                float b = Mathf.Clamp01(shade * groove * groove2 * fiber);
                byte c = (byte)(Mathf.Clamp01(b) * 255f);
                px[y * w + x] = new Color32(c, c, c, 255);
            }
        }

        tex.SetPixels32(px);
        tex.Apply(true);
        s_braidTexture = tex;
        return tex;
    }

    private static float Hash01(int x, int y)
    {
        float n = Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.5453f;
        return n - Mathf.Floor(n);
    }

    private static Material GetHookMaterial()
    {
        if (s_hookMaterial != null) return s_hookMaterial;

        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit != null)
        {
            s_hookMaterial = new Material(lit) { name = "WireRopeHookSteel" };
            s_hookMaterial.SetColor("_BaseColor", new Color(0.32f, 0.34f, 0.38f, 1f));
            if (s_hookMaterial.HasProperty("_Metallic")) s_hookMaterial.SetFloat("_Metallic", 0.85f);
            if (s_hookMaterial.HasProperty("_Smoothness")) s_hookMaterial.SetFloat("_Smoothness", 0.62f);
            return s_hookMaterial;
        }

        var unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        s_hookMaterial = new Material(unlit) { name = "WireRopeHookSteel" };
        if (s_hookMaterial.HasProperty("_BaseColor")) s_hookMaterial.SetColor("_BaseColor", new Color(0.42f, 0.44f, 0.48f, 1f));
        else s_hookMaterial.color = new Color(0.42f, 0.44f, 0.48f, 1f);
        return s_hookMaterial;
    }

    private static Material GetHookTipMaterial()
    {
        if (s_hookTipMaterial != null) return s_hookTipMaterial;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        s_hookTipMaterial = new Material(shader) { name = "WireRopeHookTip" };
        var glow = new Color(1f, 0.86f, 0.5f, 1f);
        if (s_hookTipMaterial.HasProperty("_BaseColor")) s_hookTipMaterial.SetColor("_BaseColor", glow);
        else s_hookTipMaterial.color = glow;
        if (s_hookTipMaterial.HasProperty("_EmissionColor"))
        {
            s_hookTipMaterial.EnableKeyword("_EMISSION");
            s_hookTipMaterial.SetColor("_EmissionColor", glow * 1.5f);
        }
        return s_hookTipMaterial;
    }

    private void OnDestroy()
    {
        if (_lineMaterial != null) Destroy(_lineMaterial);
    }
}
