using UnityEngine;
using System.Collections.Generic;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §7.1 L5 — 氷面ハザード。
/// 踏んだプレイヤーの Rigidbody 摩擦を大幅に下げる。
/// </summary>
public class IcePatch : MonoBehaviour
{
    [SerializeField] private float _frictionOverride = 0.02f;
    [SerializeField] private float _normalFriction   = 0.6f;

    private readonly Dictionary<Collider, PhysicsMaterial> _runtimeMaterials = new();

    private void OnTriggerEnter(Collider other)
    {
        ApplyFriction(other, _frictionOverride, true);

        // GDD §15.2 — ice_crack（氷面に乗った瞬間のピキッ音）
        // プレイヤーのみに反応してスパム防止
        if (other != null && other.CompareTag("Player"))
            PPAudioManager.Instance?.PlaySE(SoundId.IceCrack, other.transform.position);
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
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.5f, 0.8f, 1f, 0.4f);
        var col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
    }
}
