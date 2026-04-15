using UnityEngine;

/// <summary>
/// GDD §5.2 — アイテム「グラップリングフック」
/// 遠距離の崖に到達。物理エイム。
/// コスト 12pt / 重量 2 / スロット 1 / 耐久 50
/// MagneticTarget（金属製）
/// </summary>
[RequireComponent(typeof(MagneticTarget))]
public class GrapplingHookItem : ItemBase
{
    [Header("グラップリング設定")]
    [SerializeField] private float   _maxRange       = 25f;
    [SerializeField] private float   _pullForce      = 600f;
#pragma warning disable CS0414
    [SerializeField] private float   _hookFlySpeed   = 40f;   // 将来のプロジェクタイル演出用
#pragma warning restore CS0414
    [SerializeField] private LayerMask _hookableLayers;

    private Vector3    _anchorPoint;
    private bool       _isGrappling;
    private GameObject _hookVisual;
    private Rigidbody  _playerRb;
    private float      _lineLength;

    public bool IsGrappling => _isGrappling;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "グラップリングフック";
        _cost              = 12;
        _weight            = 2f;
        _slots             = 1;
        _maxDurability     = 50f;
        _currentDurability = _maxDurability;
    }

    private void FixedUpdate()
    {
        if (!_isGrappling || _playerRb == null) return;

        ApplyGrappleForce();
    }

    // ── 発射 ─────────────────────────────────────────────────
    public bool Fire(Vector3 origin, Vector3 direction)
    {
        if (_isBroken || _isGrappling) return false;

        if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, _maxRange, _hookableLayers))
        {
            Debug.Log("[GrapplingHook] ミス");
            return false;
        }

        _anchorPoint = hit.point;
        _isGrappling = true;
        _lineLength  = Vector3.Distance(origin, _anchorPoint);

        _playerRb = GetComponentInParent<Rigidbody>();
        if (_playerRb == null)
            _playerRb = FindFirstObjectByType<ExplorerController>()?.GetComponent<Rigidbody>();

        ConsumeDurability(GetUseDurabilityDrain());

        Debug.Log($"[GrapplingHook] 引っかかった: {hit.collider.name} ({_lineLength:F1}m)");
        return true;
    }

    /// <summary>グラップリングを解除する。</summary>
    public void Release()
    {
        _isGrappling = false;
        _playerRb    = null;
        Debug.Log("[GrapplingHook] 解除");
    }

    // ── 引き付け力 ────────────────────────────────────────────
    private void ApplyGrappleForce()
    {
        Vector3 toAnchor = _anchorPoint - _playerRb.position;
        float   dist     = toAnchor.magnitude;

        if (dist > _lineLength)
        {
            // ロープが張っている → 引き付け
            Vector3 dir   = toAnchor.normalized;
            float   excess = dist - _lineLength;
            _playerRb.AddForce(dir * _pullForce * excess * 0.1f, ForceMode.Force);
        }

        // 最大射程を超えたら自動解除
        if (dist > _maxRange * 1.2f)
            Release();
    }

    protected override float GetUseDurabilityDrain() => 10f;

    protected override void OnItemBroken()
    {
        Release();
        Debug.Log("[GrapplingHook] グラップリングフックが壊れた！ケーブル切断！");
        base.OnItemBroken();
    }

    private void OnDrawGizmos()
    {
        if (!_isGrappling) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, _anchorPoint);
        Gizmos.DrawWireSphere(_anchorPoint, 0.3f);
    }
}
