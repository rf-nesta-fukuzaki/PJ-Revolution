using UnityEngine;

/// <summary>
/// GDD §8.3 — ウインチケーブル（ConfigurableJoint チェーン版）。
/// 20 セグメント × 1m、最大長 20m。Phase B 初版。
/// </summary>
public sealed class WinchCableChain : MonoBehaviour, IWinchCableDriver
{
    private const int   DefaultSegmentCount = 20;
    private const float DefaultSegmentLength = 1f;
    private const float SegmentRadius = 0.06f;
    private const float SegmentMass   = 0.15f;

    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private int    _segmentCount  = DefaultSegmentCount;
    [SerializeField] private float  _segmentLength = DefaultSegmentLength;
    [SerializeField] private float  _jointSpring   = 12000f;
    [SerializeField] private float  _jointDamper   = 180f;
    [SerializeField] private bool   _enableSegmentColliders = true;

    private Transform _anchor;
    private float     _maxLength;
    private float     _reelSpeed;
    private float     _currentLength;
    private bool      _isBroken;
    private bool      _simulatePhysics = true;
    private float     _lastEstimatedTension;

    private Rigidbody[]         _segments;
    private ConfigurableJoint[] _joints;
    private Rigidbody           _attachedRb;
    private GameObject          _chainRoot;
    private PhysicsMaterial     _cableFrictionMat;

    private Vector3[] _visualTargetPositions;
    private Vector3[] _visualCurrentPositions;

    public bool HasHook    => _segments != null && _segments.Length > 0;
    public bool IsAttached => _attachedRb != null;
    public bool IsBroken   => _isBroken;
    public Rigidbody HookBody     => HasHook ? _segments[^1] : null;
    public Rigidbody AttachedBody => _attachedRb;

    public void Configure(Transform anchor, LineRenderer lineRenderer, float maxLength, float reelSpeed)
    {
        _anchor       = anchor;
        _lineRenderer = lineRenderer;
        _maxLength    = maxLength;
        _reelSpeed    = reelSpeed;
        _currentLength = maxLength;
    }

    /// <summary>ホスト以外 — 物理なしで LineRenderer のみ補間描画。</summary>
    public void SetSimulatePhysics(bool simulate)
    {
        _simulatePhysics = simulate;

        if (_segments == null) return;

        foreach (var seg in _segments)
        {
            if (seg == null) continue;
            seg.isKinematic = !simulate;
            seg.detectCollisions = simulate;

            var col = seg.GetComponent<Collider>();
            if (col != null)
                col.enabled = simulate && _enableSegmentColliders;
        }
    }

    /// <summary>サーバーから同期されたフック位置でクライアント描画を更新。</summary>
    public void ApplyClientHookPosition(Vector3 hookWorldPosition)
    {
        if (_simulatePhysics || _isBroken || _anchor == null || !HasHook) return;

        EnsureVisualBuffers();
        _visualTargetPositions[0] = _anchor.position;
        int last = _visualTargetPositions.Length - 1;
        _visualTargetPositions[last] = hookWorldPosition;

        for (int i = 1; i < last; i++)
        {
            float t = (float)i / last;
            Vector3 straight = Vector3.Lerp(_anchor.position, hookWorldPosition, t);
            float sag = Mathf.Sin(t * Mathf.PI) * 0.35f;
            _visualTargetPositions[i] = straight + Vector3.down * sag;
        }
    }

    public bool DeployHook(Vector3 spawnPosition)
    {
        if (_isBroken || _anchor == null || HasHook) return false;

        _chainRoot = new GameObject("WinchCableChain");
        _chainRoot.transform.SetParent(_anchor, false);

        int count = Mathf.Max(2, _segmentCount);
        float segLen = _segmentLength > 0f ? _segmentLength : _maxLength / count;
        _segments = new Rigidbody[count];
        _joints   = new ConfigurableJoint[count];
        EnsureVisualBuffers();

        Rigidbody prevBody = EnsureAnchorBody();

        for (int i = 0; i < count; i++)
        {
            var segGo = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            segGo.name = $"CableSeg_{i}";
            segGo.transform.SetParent(_chainRoot.transform, false);
            segGo.transform.localScale = new Vector3(SegmentRadius * 2f, segLen * 0.5f, SegmentRadius * 2f);

            var col = segGo.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = _simulatePhysics && _enableSegmentColliders;
                if (_enableSegmentColliders)
                    col.material = GetCableFrictionMaterial();
            }

            var rb = segGo.AddComponent<Rigidbody>();
            rb.mass = SegmentMass;
            rb.useGravity = _simulatePhysics;
            rb.isKinematic = !_simulatePhysics;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            Vector3 pos = i == 0
                ? spawnPosition
                : spawnPosition + Vector3.down * segLen * i;
            segGo.transform.position = pos;

            _segments[i] = rb;
            _joints[i]   = CreateSegmentJoint(rb, prevBody, segLen);
            prevBody     = rb;
        }

        if (_lineRenderer != null)
        {
            _lineRenderer.positionCount = count + 1;
            _lineRenderer.enabled = true;
        }

        return true;
    }

    public bool TryAttachHookTo(Rigidbody target)
    {
        if (!HasHook || target == null || _isBroken || _attachedRb != null) return false;

        var lastJoint = _joints[^1];
        if (lastJoint == null) return false;

        lastJoint.connectedBody = target;
        lastJoint.autoConfigureConnectedAnchor = true;

        _attachedRb = target;
        return true;
    }

    public void Reel(float deltaTime)
    {
        if (!_simulatePhysics || _isBroken || !HasHook) return;

        _currentLength = Mathf.Max(_segmentLength, _currentLength - _reelSpeed * deltaTime);
        float perSegMax = _currentLength / _segments.Length;

        for (int i = 0; i < _joints.Length; i++)
        {
            if (_joints[i] == null) continue;
            var limit = _joints[i].linearLimit;
            limit.limit = perSegMax;
            _joints[i].linearLimit = limit;
        }

        var pullTarget = _attachedRb != null ? _attachedRb : HookBody;
        if (pullTarget != null && _anchor != null)
        {
            Vector3 pull = (_anchor.position - pullTarget.position).normalized;
            pullTarget.AddForce(pull * 500f, ForceMode.Force);
        }
    }

    public float EstimateTension()
    {
        if (!HasHook || _anchor == null) return 0f;

        if (!_simulatePhysics)
            return _lastEstimatedTension;

        float total = 0f;
        for (int i = 0; i < _joints.Length; i++)
        {
            var joint = _joints[i];
            if (joint == null) continue;

            // Joint.currentForce = 前フレームに拘束を保つため掛かった反力（=ケーブル張力）。
            // ConfigurableJoint に GetCurrentForces(out,out) は存在しない（正しくは currentForce プロパティ）。
            total += joint.currentForce.magnitude;
        }

        if (_attachedRb != null && HookBody != null)
        {
            Vector3 stretch = HookBody.position - _anchor.position;
            float dist = stretch.magnitude;
            float rest = _currentLength;
            if (dist > rest)
                total += (dist - rest) * _jointSpring * 0.001f;
        }

        _lastEstimatedTension = total;
        return total;
    }

    public void BreakCable()
    {
        if (_isBroken) return;
        _isBroken = true;
        _attachedRb = null;

        if (_lineRenderer != null)
            _lineRenderer.enabled = false;

        if (_chainRoot != null)
        {
            foreach (var rb in _segments)
            {
                if (rb == null) continue;
                rb.transform.SetParent(null);
                if (_simulatePhysics)
                    rb.isKinematic = false;
            }
        }
    }

    public void RetractAndDestroy()
    {
        BreakCable();
        if (_chainRoot != null)
        {
            Destroy(_chainRoot);
            _chainRoot = null;
        }

        _segments = null;
        _joints   = null;
        _visualTargetPositions = null;
        _visualCurrentPositions = null;
    }

    private Rigidbody EnsureAnchorBody()
    {
        var anchorRb = _anchor.GetComponent<Rigidbody>();
        if (anchorRb != null) return anchorRb;

        anchorRb = _anchor.gameObject.AddComponent<Rigidbody>();
        anchorRb.isKinematic = true;
        anchorRb.useGravity  = false;
        return anchorRb;
    }

    private ConfigurableJoint CreateSegmentJoint(Rigidbody body, Rigidbody connected, float maxDistance)
    {
        var joint = body.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = connected;
        joint.autoConfigureConnectedAnchor = true;
        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Limited;
        joint.linearLimit = new SoftJointLimit { limit = maxDistance };
        joint.xDrive = joint.yDrive = joint.zDrive = new JointDrive
        {
            positionSpring = _jointSpring,
            positionDamper = _jointDamper,
            maximumForce   = 5000f,
        };
        return joint;
    }

    private PhysicsMaterial GetCableFrictionMaterial()
    {
        if (_cableFrictionMat != null) return _cableFrictionMat;

        _cableFrictionMat = new PhysicsMaterial("WinchCableFriction")
        {
            staticFriction  = 0.45f,
            dynamicFriction = 0.35f,
            bounciness      = 0.02f,
        };
        return _cableFrictionMat;
    }

    private void EnsureVisualBuffers()
    {
        if (!HasHook) return;
        int count = _segments.Length + 1;
        if (_visualTargetPositions == null || _visualTargetPositions.Length != count)
        {
            _visualTargetPositions = new Vector3[count];
            _visualCurrentPositions = new Vector3[count];
        }
    }

    private void LateUpdate()
    {
        if (_lineRenderer == null || !_lineRenderer.enabled || _anchor == null || !HasHook) return;

        if (_simulatePhysics)
        {
            _lineRenderer.SetPosition(0, _anchor.position);
            for (int i = 0; i < _segments.Length; i++)
                _lineRenderer.SetPosition(i + 1, _segments[i].position);
            return;
        }

        UpdateClientVisualLine();
    }

    private void UpdateClientVisualLine()
    {
        if (_visualTargetPositions == null || _visualCurrentPositions == null) return;

        float lerp = 1f - Mathf.Exp(-12f * Time.deltaTime);
        _lineRenderer.SetPosition(0, _anchor.position);

        for (int i = 0; i < _segments.Length; i++)
        {
            int idx = i + 1;
            if (_visualTargetPositions[idx] == Vector3.zero && i == _segments.Length - 1)
                _visualTargetPositions[idx] = _segments[i].position;

            _visualCurrentPositions[idx] = Vector3.Lerp(
                _visualCurrentPositions[idx],
                _visualTargetPositions[idx],
                lerp);

            _lineRenderer.SetPosition(idx, _visualCurrentPositions[idx]);
        }
    }
}
