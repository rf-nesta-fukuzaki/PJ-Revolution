using UnityEngine;

/// <summary>
/// Raycast でグラップルフックを発射し RopeSystem に接続を渡す。
/// </summary>
public class GrappleHook : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxGrappleDistance = 50f;
    [SerializeField] private LayerMask grappableLayer = ~0;
    [SerializeField] private float hookSpeed = 50f;

    [Header("References")]
    [SerializeField] private RopeSystem ropeSystem;
    [SerializeField] private Transform firePoint;

    private Camera _cam;

    private void Awake()
    {
        _cam = Camera.main;
        if (ropeSystem == null)
            ropeSystem = GetComponentInParent<RopeSystem>();
        if (ropeSystem == null)
            ropeSystem = FindFirstObjectByType<RopeSystem>();
    }

    private void Start()
    {
        grappableLayer = ~0;
    }

    // ─── 公開 API ───

    public void FireSwing()
    {
        if (ropeSystem == null) return;
        if (ropeSystem.IsAttached)
        {
            ropeSystem.Release();
            return;
        }

        if (TryGetHitPoint(out RaycastHit hit))
        {
            ropeSystem.AttachSwing(hit.point);
            AudioManager.Instance?.PlaySE("rope_fire");
        }
    }

    public void FirePull()
    {
        if (ropeSystem == null) return;
        if (ropeSystem.IsAttached)
        {
            ropeSystem.Release();
            return;
        }

        if (TryGetHitPoint(out RaycastHit hit))
        {
            Rigidbody rb = hit.rigidbody;
            if (rb != null)
                ropeSystem.AttachPull(rb);
            else
                ropeSystem.AttachSwing(hit.point);  // Rigidbody なしはスイングで代用
            AudioManager.Instance?.PlaySE("rope_fire");
        }
    }

    public void Release()
    {
        ropeSystem?.Release();
    }

    // ─── 内部処理 ───

    private bool TryGetHitPoint(out RaycastHit hit)
    {
        Transform origin = firePoint != null ? firePoint : (_cam != null ? _cam.transform : transform);
        Ray ray = new Ray(origin.position, origin.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxGrappleDistance, ~0, QueryTriggerInteraction.Ignore);

        if (hits.Length == 0)
        {
            hit = default;
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        RaycastHit? fallbackHit = null;
        foreach (RaycastHit candidate in hits)
        {
            if (!IsValidAnchor(candidate))
                continue;

            if (candidate.collider.CompareTag("Grappable"))
            {
                hit = candidate;
                return true;
            }

            if (!fallbackHit.HasValue)
                fallbackHit = candidate;
        }

        if (fallbackHit.HasValue)
        {
            hit = fallbackHit.Value;
            return true;
        }

        hit = default;
        return false;
    }

    // スコープ照準中に Grappable を検出しているか（HudManager から参照）
    public bool IsAimingAtGrappable()
    {
        return TryGetHitPoint(out _);
    }

    private bool IsValidAnchor(RaycastHit hit)
    {
        Collider collider = hit.collider;
        if (collider == null)
            return false;

        if (collider.transform.root == transform.root)
            return false;

        return ((1 << collider.gameObject.layer) & grappableLayer.value) != 0;
    }

    private void OnDrawGizmos()
    {
        Transform origin = firePoint != null ? firePoint : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin.position, origin.forward * maxGrappleDistance);
    }
}
