using UnityEngine;

/// <summary>
/// 遺物のプリミティブビジュアル構築を担当するプレゼンテーションコンポーネント。
/// <see cref="RelicBase"/> から視覚生成ロジックを分離し SRP を満たす。
/// </summary>
[DisallowMultipleComponent]
public sealed class RelicVisualizer : MonoBehaviour
{
    private const string VizPrefix = "RelicViz_";

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
    private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
    private static Material s_sharedMaterial;

    public void Rebuild(System.Action buildVisual)
    {
        Clear();
        var meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null) meshRenderer.enabled = false;
        buildVisual?.Invoke();
    }

    public void Clear()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            var child = transform.GetChild(i);
            if (!child.name.StartsWith(VizPrefix)) continue;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(child.gameObject); else
#endif
            Destroy(child.gameObject);
        }
    }

    public GameObject CreatePrimitive(
        PrimitiveType type,
        string label,
        Vector3 localPos,
        Vector3 localScale,
        Color color,
        float metallic = 0f,
        float smoothness = 0.5f)
        => CreatePrimitiveRot(type, label, localPos, Quaternion.identity, localScale, color, metallic, smoothness);

    public GameObject CreatePrimitiveRot(
        PrimitiveType type,
        string label,
        Vector3 localPos,
        Quaternion localRot,
        Vector3 localScale,
        Color color,
        float metallic = 0f,
        float smoothness = 0.5f)
    {
        Contract.RequiresNotNull(label, nameof(label));

        var go = GameObject.CreatePrimitive(type);
        go.name = VizPrefix + label;
        go.transform.SetParent(transform);
        go.transform.localPosition = localPos;
        go.transform.localRotation = localRot;
        go.transform.localScale = localScale;

        var col = go.GetComponent<Collider>();
        if (col != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(col); else
#endif
            Destroy(col);
        }

        var renderer = go.GetComponent<MeshRenderer>();
        if (renderer == null) return go;

        var sharedMaterial = GetSharedMaterial();
        if (sharedMaterial != null)
            renderer.sharedMaterial = sharedMaterial;

        var block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        block.SetColor(BaseColorId, color);
        block.SetColor(ColorId, color);
        block.SetFloat(MetallicId, metallic);
        block.SetFloat(SmoothnessId, smoothness);
        renderer.SetPropertyBlock(block);

        return go;
    }

    private static Material GetSharedMaterial()
    {
        if (s_sharedMaterial != null) return s_sharedMaterial;

        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null) return null;

        s_sharedMaterial = new Material(shader) { name = "RelicVizSharedMaterial" };
        return s_sharedMaterial;
    }
}
