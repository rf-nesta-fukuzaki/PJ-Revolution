using UnityEngine;
using System.Collections.Generic;
using PeakPlunder.Audio;

/// <summary>
/// GDD §7.1 L5 — 氷面ハザード。
/// 踏んだプレイヤーの Rigidbody 摩擦を大幅に下げる。
/// </summary>
public class IcePatch : MonoBehaviour
{
    [SerializeField] private float _frictionOverride = 0.02f;
    [SerializeField] private float _normalFriction   = 0.6f;

    [Header("Visual (AAA icy sheen)")]
    [Tooltip("ON なら起動時にこの氷面の MeshRenderer をツヤのある反射的な氷マテリアルへ差し替える（平板な白板を解消）。")]
    [SerializeField] private bool  _upgradeVisual   = true;
    [SerializeField] private Color _iceColor        = new Color(0.66f, 0.82f, 0.93f, 1f);
    [SerializeField] private Color _iceEmission     = new Color(0.05f, 0.10f, 0.16f, 1f); // 影でも黒く沈まない冷たい発光
    [Range(0f, 1f)] [SerializeField] private float _iceSmoothness = 0.93f;                 // 空/太陽を映す氷の照り

    private readonly Dictionary<Collider, PhysicsMaterial> _runtimeMaterials = new();
    private Material _iceMat;

    private void Awake()
    {
        if (_upgradeVisual) UpgradeVisual();
    }

    // フラットな白い板（URP/Lit 既定）を、空と太陽を映すツヤ氷へ。高 smoothness の鏡面で
    // 水平な氷面が天空を反射し、太陽のグリントが走る。冷たい微発光で陰でも氷として読める。
    private void UpgradeVisual()
    {
        var mr = GetComponent<MeshRenderer>();
        if (mr == null) return;
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;

        _iceMat = new Material(lit) { name = "IcePatchIceMat" };
        if (_iceMat.HasProperty("_BaseColor")) _iceMat.SetColor("_BaseColor", _iceColor);
        if (_iceMat.HasProperty("_Smoothness")) _iceMat.SetFloat("_Smoothness", _iceSmoothness);
        if (_iceMat.HasProperty("_Metallic"))   _iceMat.SetFloat("_Metallic", 0f);
        if (_iceMat.HasProperty("_SpecularHighlights")) _iceMat.SetFloat("_SpecularHighlights", 1f);
        if (_iceMat.HasProperty("_EnvironmentReflections")) _iceMat.SetFloat("_EnvironmentReflections", 1f);
        _iceMat.EnableKeyword("_EMISSION");
        _iceMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        if (_iceMat.HasProperty("_EmissionColor")) _iceMat.SetColor("_EmissionColor", _iceEmission);

        mr.sharedMaterial = _iceMat;
    }

    private void OnTriggerEnter(Collider other)
    {
        ApplyFriction(other, _frictionOverride, true);

        // GDD §15.2 — ice_crack（氷面に乗った瞬間のピキッ音）
        // プレイヤーのみに反応してスパム防止
        if (other != null && other.CompareTag("Player"))
        {
            GameServices.Audio?.PlaySE(SoundId.IceCrack, other.transform.position);

            // ポップな氷の破片バースト（足元の氷面で青白く弾ける）。
            var fxPos = new Vector3(other.transform.position.x, transform.position.y + 0.1f, other.transform.position.z);
            Sandbox.World.Environment.StylizedImpactFx.Spawn(fxPos, new Color(0.72f, 0.90f, 1.00f), 0.8f, 12);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        ApplyFriction(other, _normalFriction, false);
    }

    private void ApplyFriction(Collider col, float friction, bool enteringPatch)
    {
        var rb = col != null ? col.attachedRigidbody : null;
        if (rb == null) return;

        var adapter = col.GetComponent<WeatherFrictionAdapter>() ?? col.GetComponentInParent<WeatherFrictionAdapter>();
        if (adapter != null)
        {
            if (enteringPatch) adapter.SetHazardFrictionOverride(_frictionOverride);
            else adapter.ClearHazardFrictionOverride();
            return;
        }

        if (!_runtimeMaterials.TryGetValue(col, out var mat) || mat == null)
        {
            var source = col.material;
            mat = source != null ? CloneMaterial(source) : new PhysicsMaterial("IceContactMat");
            _runtimeMaterials[col] = mat;
            col.material = mat;
        }

        mat.dynamicFriction = friction;
        mat.staticFriction  = friction;
    }

    private static PhysicsMaterial CloneMaterial(PhysicsMaterial source)
    {
        return new PhysicsMaterial($"{source.name}_IcePatchRuntime")
        {
            dynamicFriction = source.dynamicFriction,
            staticFriction = source.staticFriction,
            bounciness = source.bounciness,
            frictionCombine = source.frictionCombine,
            bounceCombine = source.bounceCombine
        };
    }

    private void OnDestroy()
    {
        foreach (var kv in _runtimeMaterials)
        {
            if (kv.Value != null)
                Destroy(kv.Value);
        }
        _runtimeMaterials.Clear();

        if (_iceMat != null) Destroy(_iceMat);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.4f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
