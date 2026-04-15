using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// GDD §4.2 — 復活の祠。
/// 山に散らばっている。幽霊プレイヤーが近づくと1回限り復活可能。
/// </summary>
public class ReviveShrine : MonoBehaviour
{
    [SerializeField] private ParticleSystem _ambientParticles;
    [SerializeField] private ParticleSystem _reviveParticles;
    [SerializeField] private Color          _availableColor = Color.cyan;
    [SerializeField] private Color          _usedColor      = Color.gray;

    private bool     _used;
    private Renderer _renderer;
    private MaterialPropertyBlock _propertyBlock;

    private static readonly List<ReviveShrine> s_registeredShrines = new();
    public static IReadOnlyList<ReviveShrine> RegisteredShrines => s_registeredShrines;

    public bool IsAvailable => !_used;

    private void OnEnable()
    {
        if (!s_registeredShrines.Contains(this))
            s_registeredShrines.Add(this);
    }

    private void OnDisable()
    {
        s_registeredShrines.Remove(this);
    }

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
        _renderer = GetComponentInChildren<Renderer>();
        UpdateVisuals();
    }

    private void Start()
    {
        if (_ambientParticles != null) _ambientParticles.Play();
    }

    public void Use()
    {
        if (_used) return;
        _used = true;

        if (_ambientParticles != null) _ambientParticles.Stop();
        if (_reviveParticles != null) _reviveParticles.Play();
        UpdateVisuals();

        Debug.Log($"[ReviveShrine] 祠を使用: {name}");
    }

    private void UpdateVisuals()
    {
        if (_renderer == null) return;

        _renderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetColor("_BaseColor", _used ? _usedColor : _availableColor);
        _renderer.SetPropertyBlock(_propertyBlock);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = _used ? Color.gray : Color.cyan;
        Gizmos.DrawSphere(transform.position, 1.5f);
        Gizmos.DrawIcon(transform.position + Vector3.up * 2f, "console.infoicon.sml");
    }
}
