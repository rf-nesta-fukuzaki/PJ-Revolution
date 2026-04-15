using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 磁力の兜に引き寄せられる金属アイテムに付けるマーカーコンポーネント。
/// </summary>
public class MagneticTarget : MonoBehaviour
{
    [SerializeField] private string _itemName = "アイテム";
    private Rigidbody _cachedRigidbody;

    private static readonly List<MagneticTarget> s_registeredTargets = new();

    public string ItemName => _itemName;
    public Rigidbody TargetRigidbody => _cachedRigidbody;
    public static IReadOnlyList<MagneticTarget> RegisteredTargets => s_registeredTargets;

    private void Awake()
    {
        _cachedRigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        if (!s_registeredTargets.Contains(this))
            s_registeredTargets.Add(this);
    }

    private void OnDisable()
    {
        s_registeredTargets.Remove(this);
    }
}
