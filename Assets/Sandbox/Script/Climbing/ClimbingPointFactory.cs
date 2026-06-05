using UnityEngine;

/// <summary>
/// GDD §4.3 — アイスアックス / グラップリングフックで動的生成する GrabPoint。
/// </summary>
public static class ClimbingPointFactory
{
    private const float ICE_AXE_LIFETIME_SEC = 300f;
    private const float GRAPPLE_LIFETIME_SEC = float.PositiveInfinity;

    public static GrabPoint CreateIceAxePoint(Vector3 position, Vector3 normal)
    {
        var go = CreateBasePoint("IceAxePoint", position, normal, requireIceAxe: true);
        go.AddComponent<TimedGrabPoint>().Init(ICE_AXE_LIFETIME_SEC);
        return go.GetComponent<GrabPoint>();
    }

    public static GrabPoint CreateGrapplePoint(Vector3 position, Vector3 normal)
    {
        var go = CreateBasePoint("GrapplePoint", position, normal, requireIceAxe: false);
        return go.GetComponent<GrabPoint>();
    }

    private static GameObject CreateBasePoint(string name, Vector3 position, Vector3 normal, bool requireIceAxe)
    {
        var go = new GameObject(name);
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(normal));

        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0) go.layer = layer;
        try { go.tag = "ClimbingPoint"; }
        catch (UnityException) { /* optional tag */ }

        var col = go.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 0.3f;

        var gp = go.AddComponent<GrabPoint>();
        gp.Configure(requireIceAxe, 5f);

        var vis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        vis.name = "Marker";
        vis.transform.SetParent(go.transform, false);
        vis.transform.localScale = Vector3.one * 0.2f;
        var visCol = vis.GetComponent<Collider>();
        if (visCol != null)
        {
            if (Application.isPlaying)
                Object.Destroy(visCol);
            else
                Object.DestroyImmediate(visCol);
        }

        var rend = vis.GetComponent<Renderer>();
        if (rend != null)
        {
            var color = requireIceAxe ? Color.cyan : Color.yellow;
            if (Application.isPlaying)
                rend.material.color = color;
            else if (rend.sharedMaterial != null)
                rend.sharedMaterial.color = color;
        }

        return go;
    }
}

/// <summary>生成から一定時間後に GrabPoint を破棄（アイスアックス劣化演出）。</summary>
public sealed class TimedGrabPoint : MonoBehaviour
{
    private float _lifetime;
    private float _spawnTime;

    public void Init(float lifetimeSeconds)
    {
        _lifetime   = lifetimeSeconds;
        _spawnTime  = Time.time;
    }

    private void Update()
    {
        if (_lifetime <= 0f || float.IsPositiveInfinity(_lifetime)) return;
        if (Time.time - _spawnTime >= _lifetime)
            Destroy(gameObject);
    }
}
