using UnityEngine;

/// <summary>
/// GDD §8.3 — ウインチケーブル（フック + LineRenderer）。巻き取りでフックをアンカー方向へ引く。
/// </summary>
public sealed class WinchCableSystem : MonoBehaviour, IWinchCableDriver
{
    private const float HookMass = 2f;
    private const float HookRadius = 0.12f;

    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private float _maxLength = 20f;
    [SerializeField] private float _reelSpeed = 1.5f;

    private Transform   _anchor;
    private Rigidbody   _hookRb;
    private Rigidbody   _attachedRb;
    private SpringJoint _hookJoint;
    private float       _currentLength;
    private bool        _isBroken;

    public bool HasHook       => _hookRb != null;
    public bool IsAttached    => _attachedRb != null;
    public bool IsBroken        => _isBroken;
    public float CurrentLength  => _currentLength;
    public Rigidbody HookBody   => _hookRb;
    public Rigidbody AttachedBody => _attachedRb;

    public void Configure(Transform anchor, LineRenderer lineRenderer, float maxLength, float reelSpeed)
    {
        _anchor       = anchor;
        _lineRenderer = lineRenderer;
        _maxLength    = maxLength;
        _reelSpeed    = reelSpeed;
        _currentLength = maxLength;
    }

    public bool DeployHook(Vector3 spawnPosition)
    {
        if (_isBroken || _hookRb != null || _anchor == null) return false;

        var hookGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        hookGo.name = "WinchCableHook";
        hookGo.transform.position = spawnPosition;
        hookGo.transform.localScale = Vector3.one * HookRadius * 2f;
        Object.Destroy(hookGo.GetComponent<Collider>());
        var col = hookGo.AddComponent<SphereCollider>();
        col.radius = HookRadius;

        _hookRb = hookGo.AddComponent<Rigidbody>();
        _hookRb.mass = HookMass;
        _hookRb.useGravity = true;
        _hookRb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        _hookJoint = hookGo.AddComponent<SpringJoint>();
        _hookJoint.connectedBody = null;
        _hookJoint.anchor = Vector3.zero;
        _hookJoint.autoConfigureConnectedAnchor = false;
        _hookJoint.connectedAnchor = _anchor.position;
        _hookJoint.spring = 8000f;
        _hookJoint.damper = 120f;
        _hookJoint.minDistance = 0.5f;
        _hookJoint.maxDistance = _currentLength;

        if (_lineRenderer != null)
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.enabled = true;
        }

        return true;
    }

    public bool TryAttachHookTo(Rigidbody target)
    {
        if (_hookRb == null || target == null || _isBroken) return false;

        _attachedRb = target;
        _hookJoint.connectedBody = target;
        _hookJoint.autoConfigureConnectedAnchor = true;
        return true;
    }

    public void Reel(float deltaTime)
    {
        if (_hookJoint == null || _isBroken) return;

        _currentLength = Mathf.Max(0.5f, _currentLength - _reelSpeed * deltaTime);
        _hookJoint.maxDistance = _currentLength;

        if (_attachedRb != null)
        {
            Vector3 pull = (_anchor.position - _attachedRb.position).normalized;
            _attachedRb.AddForce(pull * 600f, ForceMode.Force);
        }
        else if (_hookRb != null)
        {
            Vector3 pull = (_anchor.position - _hookRb.position).normalized;
            _hookRb.AddForce(pull * 400f, ForceMode.Force);
        }
    }

    public float EstimateTension()
    {
        if (_attachedRb == null && _hookRb == null) return 0f;
        var body = _attachedRb != null ? _attachedRb : _hookRb;
        float dist = Vector3.Distance(_anchor.position, body.position);
        return dist * body.mass * 9.81f;
    }

    public void BreakCable()
    {
        if (_isBroken) return;
        _isBroken = true;

        if (_hookJoint != null)
        {
            _hookJoint.connectedBody = null;
            Destroy(_hookJoint);
            _hookJoint = null;
        }

        if (_hookRb != null)
        {
            _hookRb.transform.SetParent(null);
            _hookRb.isKinematic = false;
        }

        if (_lineRenderer != null)
            _lineRenderer.enabled = false;

        _attachedRb = null;
    }

    public void RetractAndDestroy()
    {
        BreakCable();
        if (_hookRb != null)
        {
            Destroy(_hookRb.gameObject);
            _hookRb = null;
        }
    }

    private void LateUpdate()
    {
        if (_lineRenderer == null || !_lineRenderer.enabled || _anchor == null) return;

        Vector3 end = _attachedRb != null
            ? _attachedRb.position
            : _hookRb != null ? _hookRb.position : _anchor.position;

        _lineRenderer.SetPosition(0, _anchor.position);
        _lineRenderer.SetPosition(1, end);
    }
}
