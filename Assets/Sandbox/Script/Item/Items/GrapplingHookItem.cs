using System.Collections;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

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
    [Tooltip("発射後、フックが標的に到達するまでの飛翔速度 (m/s)。視覚演出用。")]
    [SerializeField] private float   _hookFlySpeed   = 40f;
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

        // GDD §15.2 — grappling_fire（命中失敗を問わず発射時に鳴らす）
        PPAudioManager.Instance?.PlaySE(SoundId.GrapplingFire, origin);

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

        // 飛翔演出：発射点 → 標的へ _hookFlySpeed でフックが飛ぶビジュアル
        // GDD §15.2 — grappling_hit SE は HookFlyRoutine 到達時に発火（ビジュアル同期）
        SpawnFlyingHook(origin, _anchorPoint);

        ConsumeDurability(GetUseDurabilityDrain());

        Debug.Log($"[GrapplingHook] 引っかかった: {hit.collider.name} ({_lineLength:F1}m)");
        return true;
    }

    // ── フック飛翔演出 ────────────────────────────────────────
    private void SpawnFlyingHook(Vector3 origin, Vector3 target)
    {
        if (_hookVisual != null) Destroy(_hookVisual);

        var vis = GameObject.CreatePrimitive(PrimitiveType.Cube);
        vis.name = "FlyingHook";
        var col = vis.GetComponent<Collider>();
        if (col != null) Destroy(col);
        vis.transform.position   = origin;
        vis.transform.localScale = new Vector3(0.12f, 0.12f, 0.28f);
        vis.transform.rotation   = Quaternion.LookRotation((target - origin).sqrMagnitude > 0.001f
            ? (target - origin).normalized : Vector3.forward);

        _hookVisual = vis;
        StartCoroutine(HookFlyRoutine(vis, origin, target));
    }

    private IEnumerator HookFlyRoutine(GameObject vis, Vector3 origin, Vector3 target)
    {
        float dist     = Vector3.Distance(origin, target);
        float speed    = Mathf.Max(_hookFlySpeed, 0.1f);
        float duration = dist / speed;
        float elapsed  = 0f;

        while (elapsed < duration && vis != null && _isGrappling)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            vis.transform.position = Vector3.Lerp(origin, target, t);
            yield return null;
        }

        if (vis != null && _isGrappling)
        {
            vis.transform.position = target;   // 到達後は標的に張り付いてロープの終端ビジュアルとして残す
            // GDD §15.2 — grappling_hit（着弾音はフック到達と同期）
            PPAudioManager.Instance?.PlaySE(SoundId.GrapplingHit, target);
        }
    }

    /// <summary>グラップリングを解除する。</summary>
    public void Release()
    {
        _isGrappling = false;
        _playerRb    = null;
        if (_hookVisual != null)
        {
            Destroy(_hookVisual);
            _hookVisual = null;
        }
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
