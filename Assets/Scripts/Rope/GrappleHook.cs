using UnityEngine;

/// <summary>
/// Raycast でグラップルフックを発射し RopeSystem に接続を渡す。
/// </summary>
public class GrappleHook : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float maxGrappleDistance = 30f;
    [SerializeField] private LayerMask grappableLayer;
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
        // grappableLayer が設定されていない場合は全レイヤーを対象にする
        if (grappableLayer == 0)
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

        if (Physics.Raycast(ray, out hit, maxGrappleDistance, grappableLayer))
        {
            // 自分自身には当たらない
            if (hit.collider.gameObject == transform.root.gameObject)
            {
                hit = default;
                return false;
            }
            return true;
        }
        return false;
    }

    // スコープ照準中に Grappable を検出しているか（HudManager から参照）
    public bool IsAimingAtGrappable()
    {
        Transform origin = firePoint != null ? firePoint : (_cam != null ? _cam.transform : transform);
        Ray ray = new Ray(origin.position, origin.forward);
        return Physics.Raycast(ray, maxGrappleDistance, grappableLayer);
    }

    private void OnDrawGizmos()
    {
        Transform origin = firePoint != null ? firePoint : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(origin.position, origin.forward * maxGrappleDistance);
    }
}
