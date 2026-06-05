using UnityEngine;

/// <summary>
/// GDD §4.6 / §8.3 — ロープ・ウインチケーブルの接続アンカー（遺物側）。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RelicGrabPoint : MonoBehaviour
{
    [SerializeField] private Transform _attachPoint;
    [SerializeField] private float     _attachRadius = 0.35f;

    private Rigidbody _rb;

    public Rigidbody AttachBody      => _rb;
    public Vector3   AttachPosition  => _attachPoint != null ? _attachPoint.position : transform.position;
    public float     AttachRadius    => _attachRadius;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        EnsureAttachPoint();
    }

    private void EnsureAttachPoint()
    {
        if (_attachPoint != null) return;

        var existing = transform.Find("RelicGrabPoint");
        if (existing != null)
        {
            _attachPoint = existing;
            return;
        }

        var go = new GameObject("RelicGrabPoint");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        _attachPoint = go.transform;
    }

    public bool IsWithinAttachRange(Vector3 from)
        => Vector3.Distance(from, AttachPosition) <= _attachRadius + 2f;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.6f);
        Gizmos.DrawWireSphere(AttachPosition, _attachRadius);
    }
}
