using UnityEngine;

/// <summary>
/// <see cref="WireRopeActionController"/> の視覚表現（LineRenderer によるロープ／頭上ラッソ描画と
/// フック球の生成・追従・破棄）を物理ロジックから分離した描画コンポーネント。
///
/// 責務分担：コントローラは「何を・どこに」描くか（アンカー・たわみ・太さ）を物理状態から決め、
/// 本クラスは「どう描くか」（LineRenderer のセグメント構築・マテリアル・フック球の寿命管理）だけを担う。
/// すべて実行時生成・実行時マテリアル（アセットや他シーンを汚さない）。
/// </summary>
[DisallowMultipleComponent]
public sealed class RopeRenderer : MonoBehaviour
{
    private const int SpinSegments = 32;
    private const int RopeLineSegments = 12;
    private const string VisualChildName = "WireRopeVisual";

    private LineRenderer _lineRenderer;
    private Transform _visualRoot;
    private GameObject _hookVisual;
    private float _hookVisualScale = 0.22f;

    private float _startWidth = 0.09f;
    private float _endWidth = 0.05f;

    private static Material s_ropeMaterial;

    /// <summary>コントローラが Awake で 1 回呼ぶ。LineRenderer 階層を整え、既定の太さ・色を設定する。</summary>
    public void Configure(float startWidth, float endWidth, Color color)
    {
        _startWidth = startWidth;
        _endWidth = endWidth;
        EnsureVisualHierarchy(color);
    }

    private void EnsureVisualHierarchy(Color color)
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

        ConfigureLineRenderer(_lineRenderer, color);
    }

    private void ConfigureLineRenderer(LineRenderer lr, Color color)
    {
        lr.enabled = false;
        lr.useWorldSpace = true;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        lr.textureMode = LineTextureMode.Stretch;
        lr.alignment = LineAlignment.View;
        lr.numCapVertices = 6;
        lr.numCornerVertices = 4;
        lr.startWidth = _startWidth;
        lr.endWidth = _endWidth;
        lr.startColor = color;
        lr.endColor = color;
        lr.material = CreateRopeMaterial();
    }

    public void SetVisible(bool visible)
    {
        if (_lineRenderer != null)
            _lineRenderer.enabled = visible;
    }

    public void SetWidths(float startWidth, float endWidth)
    {
        if (_lineRenderer == null) return;
        _lineRenderer.startWidth = startWidth;
        _lineRenderer.endWidth = endWidth;
    }

    /// <summary>手元→アンカー間を、中央が下へたわむ曲線で描く（end 幅は Configure 既定値を使う）。</summary>
    public void DrawCurve(Vector3 start, Vector3 end, float sag, float startWidth)
    {
        if (_lineRenderer == null) return;
        SetVisible(true);
        _lineRenderer.startWidth = startWidth;
        _lineRenderer.endWidth = _endWidth;

        int count = RopeLineSegments + 1;
        _lineRenderer.positionCount = count;
        for (int i = 0; i < count; i++)
        {
            float t = i / (float)RopeLineSegments;
            Vector3 p = Vector3.Lerp(start, end, t);
            p += Vector3.down * (sag * 4f * t * (1f - t));
            _lineRenderer.SetPosition(i, p);
        }
    }

    /// <summary>手元のテザー＋頭上で回る輪（カウボーイラッソ）を描く。</summary>
    public void DrawLasso(Vector3 hand, Vector3 center, Vector3 axisRight, Vector3 axisForward,
                          float radius, float spinAngle, float attachAngle, float startWidth, float endWidth)
    {
        if (_lineRenderer == null) return;

        int circleCount = SpinSegments + 1;
        int totalCount = 1 + circleCount;
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

    // ── フック球 ─────────────────────────────────────────────
    public void SpawnHook(Vector3 origin)
    {
        ClearHook();
        var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vis.name = "WireRopeHook";
        var col = vis.GetComponent<Collider>();
        if (col != null) Destroy(col);

        vis.transform.localScale = Vector3.one * 0.22f;
        vis.transform.position = origin;

        var mr = vis.GetComponent<MeshRenderer>();
        if (mr != null)
            mr.sharedMaterial = CreateRopeMaterial();

        _hookVisual = vis;
    }

    public void SyncHook(Vector3 pos, float dt)
    {
        if (_hookVisual == null)
            return;

        _hookVisual.transform.position = pos;
        _hookVisualScale = Mathf.Lerp(_hookVisualScale, 0.22f, dt * 14f);
        _hookVisual.transform.localScale = Vector3.one * _hookVisualScale;
    }

    public void PulseHook(float scaleMul)
    {
        if (_hookVisual == null)
            return;

        _hookVisualScale = 0.22f * scaleMul;
    }

    public void ClearHook()
    {
        if (_hookVisual == null) return;
        Destroy(_hookVisual);
        _hookVisual = null;
    }

    private static Material CreateRopeMaterial()
    {
        if (s_ropeMaterial != null) return s_ropeMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Unlit/Color")
                     ?? Shader.Find("Sprites/Default");
        if (shader == null) return null;

        s_ropeMaterial = new Material(shader) { name = "WireRopeUnlit" };
        if (s_ropeMaterial.HasProperty("_BaseColor"))
            s_ropeMaterial.SetColor("_BaseColor", new Color(0.95f, 0.82f, 0.45f, 1f));
        else
            s_ropeMaterial.color = new Color(0.95f, 0.82f, 0.45f, 1f);

        return s_ropeMaterial;
    }
}
