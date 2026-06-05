using UnityEngine;

/// <summary>
/// GDD §5.2 — 折りたたみ担架（ConfigurableJoint 2人運搬・遺物スリップ）。
/// </summary>
public class StretcherItem : ItemBase
{
    private const float JointBreakForce = 4000f;

    [Header("担架エンド")]
    [SerializeField] private Transform _endA;
    [SerializeField] private Transform _endB;
    [SerializeField] private Transform _relicMount;

    [Header("設定")]
    [SerializeField] private float _slideTiltAngle = 45f;
    [SerializeField] private float _slideForce     = 8f;
    [SerializeField] private float _snapRadius     = 2.0f;

    private Rigidbody           _endRbA;
    private Rigidbody           _endRbB;
    private ConfigurableJoint   _jointA;
    private ConfigurableJoint   _jointB;
    private PlayerInteraction   _driverA;
    private PlayerInteraction   _driverB;
    private RelicBase           _mountedRelic;
    private bool                _relicSlidOff;
    private bool                _isExpanded = true;
    private BoxCollider         _bodyCollider;
    private PhysicsMaterial      _highFrictionMat;
    private float               _soloDragTimer;

    public bool IsExpanded     => _isExpanded;
    public bool IsCarriedByTwo => _driverA != null && _driverB != null;
    public bool IsEndAFree     => _driverA == null;
    public bool IsEndBFree     => _driverB == null;
    public RelicBase MountedRelic => _mountedRelic;

    public bool TryToggleExpand()
    {
        var sync = GetComponent<NetworkStretcherSync>();
        if (sync != null && sync.IsNetworkActive)
            return sync.RequestToggleExpand();
        return ToggleExpandLocal();
    }

    public bool ToggleExpandLocal()
    {
        if (_isBroken) return false;
        if (_driverA != null || _driverB != null) return false;

        _isExpanded = !_isExpanded;
        ApplyExpandedCollider();
        return true;
    }

    public void ApplyExpandedState(bool expanded)
    {
        if (_driverA != null || _driverB != null) return;
        _isExpanded = expanded;
        ApplyExpandedCollider();
    }

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "折りたたみ担架";
        _cost              = 10;
        _weight            = 3f;
        _slots             = 3;
        _maxDurability     = 70f;
        _currentDurability = _maxDurability;
        EnsureStretcherStructure();
    }

    private void EnsureStretcherStructure()
    {
        _bodyCollider = GetComponent<BoxCollider>();
        if (_bodyCollider == null)
            _bodyCollider = gameObject.AddComponent<BoxCollider>();

        _highFrictionMat = new PhysicsMaterial("StretcherFriction")
        {
            staticFriction  = 0.8f,
            dynamicFriction = 0.8f,
            bounciness      = 0.05f,
        };
        _bodyCollider.material = _highFrictionMat;

        if (_endA == null)
        {
            var go = new GameObject("EndA");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(-1f, 0f, 0f);
            _endA = go.transform;
        }
        if (_endB == null)
        {
            var go = new GameObject("EndB");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(1f, 0f, 0f);
            _endB = go.transform;
        }
        if (_relicMount == null)
        {
            var go = new GameObject("RelicMount");
            go.transform.SetParent(transform, false);
            _relicMount = go.transform;
        }

        _endRbA = EnsureEndRigidbody(_endA);
        _endRbB = EnsureEndRigidbody(_endB);

        ApplyExpandedCollider();
    }

    private static Rigidbody EnsureEndRigidbody(Transform end)
    {
        var rb = end.GetComponent<Rigidbody>();
        if (rb == null) rb = end.gameObject.AddComponent<Rigidbody>();
        rb.mass = 5f;
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        var fj = end.GetComponent<FixedJoint>();
        if (fj == null) fj = end.gameObject.AddComponent<FixedJoint>();
        fj.connectedBody = end.parent.GetComponent<Rigidbody>();
        fj.breakForce = JointBreakForce;

        return rb;
    }

    private void ApplyExpandedCollider()
    {
        if (_bodyCollider == null) return;
        _bodyCollider.size = _isExpanded
            ? new Vector3(2f, 0.15f, 0.8f)
            : new Vector3(0.5f, 0.3f, 0.3f);
        _bodyCollider.center = Vector3.zero;
    }

    private void FixedUpdate()
    {
        ApplySoloDragDamage();

        if (_mountedRelic == null || _relicSlidOff) return;
        float tilt = GetStretcherTiltAngle();
        if (tilt > _slideTiltAngle)
            SlideRelicOff(tilt);
    }

    private void ApplySoloDragDamage()
    {
        if (_mountedRelic == null || IsCarriedByTwo) return;
        if (_driverA == null && _driverB == null) return;

        _soloDragTimer += Time.fixedDeltaTime;
        if (_soloDragTimer >= 1f)
        {
            _soloDragTimer = 0f;
            _mountedRelic?.ApplyDamage(1f);
        }
    }

    public bool TryAttach(PlayerInteraction player, out Transform attachPoint)
    {
        attachPoint = null;
        if (player == null) return false;

        Transform nearest = GetNearestFreeEnd(player.transform, out float nearestDist);
        if (nearest == null || nearestDist > _snapRadius) return false;

        var playerRb = player.GetComponent<Rigidbody>();
        if (playerRb == null) return false;

        if (_driverA == null && nearest == _endA)
        {
            _driverA    = player;
            attachPoint = _endA;
            _jointA     = CreateCarrierJoint(_endRbA, playerRb);
            Debug.Log($"[Stretcher] {player.name} が端Aを掴んだ");
            return true;
        }

        if (_driverB == null && _driverA != player && nearest == _endB)
        {
            _driverB    = player;
            attachPoint = _endB;
            _jointB     = CreateCarrierJoint(_endRbB, playerRb);
            Debug.Log($"[Stretcher] {player.name} が端Bを掴んだ → 2人担架");
            return true;
        }

        return false;
    }

    private static ConfigurableJoint CreateCarrierJoint(Rigidbody stretcherEnd, Rigidbody playerRb)
    {
        var joint = stretcherEnd.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = playerRb;
        joint.autoConfigureConnectedAnchor = true;
        joint.xMotion = ConfigurableJointMotion.Limited;
        joint.yMotion = ConfigurableJointMotion.Limited;
        joint.zMotion = ConfigurableJointMotion.Limited;
        joint.linearLimit = new SoftJointLimit { limit = 0.35f };
        joint.breakForce = JointBreakForce;
        return joint;
    }

    public void Detach(PlayerInteraction player)
    {
        if (player == null) return;

        if (_driverA == player)
        {
            DestroyJoint(ref _jointA);
            _driverA = null;
            Debug.Log("[Stretcher] 端Aが離れた");
        }
        else if (_driverB == player)
        {
            DestroyJoint(ref _jointB);
            _driverB = null;
            Debug.Log("[Stretcher] 端Bが離れた");
        }
    }

    private static void DestroyJoint(ref ConfigurableJoint joint)
    {
        if (joint != null)
        {
            Destroy(joint);
            joint = null;
        }
    }

    public bool IsAttachedBy(PlayerInteraction player) =>
        _driverA == player || _driverB == player;

    private Transform GetNearestFreeEnd(Transform from, out float dist)
    {
        dist = float.PositiveInfinity;
        Transform result = null;

        if (_endA != null && _driverA == null)
        {
            float dA = Vector3.Distance(from.position, _endA.position);
            if (dA < dist) { dist = dA; result = _endA; }
        }
        if (_endB != null && _driverB == null)
        {
            float dB = Vector3.Distance(from.position, _endB.position);
            if (dB < dist) { dist = dB; result = _endB; }
        }
        return result;
    }

    public bool MountRelic(RelicBase relic)
    {
        var sync = GetComponent<NetworkStretcherSync>();
        if (sync != null && sync.IsNetworkActive)
            return sync.RequestMountRelic(relic);
        return MountRelicLocal(relic);
    }

    public bool MountRelicLocal(RelicBase relic)
    {
        if (_mountedRelic != null || relic == null) return false;

        _mountedRelic = relic;
        _relicSlidOff = false;

        var carrier = relic.GetComponent<RelicCarrier>();
        carrier?.DetachCarrierState();

        if (_relicMount != null)
        {
            relic.transform.SetParent(_relicMount);
            relic.transform.localPosition = Vector3.zero;
        }

        var rb = relic.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity  = false;
        }

        return true;
    }

    private float GetStretcherTiltAngle()
    {
        if (_endA == null || _endB == null) return 0f;
        float heightDiff = Mathf.Abs(_endA.position.y - _endB.position.y);
        float length     = Vector3.Distance(_endA.position, _endB.position);
        if (length < 0.001f) return 0f;
        return Mathf.Atan2(heightDiff, length) * Mathf.Rad2Deg;
    }

    private void SlideRelicOff(float tiltAngle)
    {
        _relicSlidOff = true;

        if (_bodyCollider != null && _highFrictionMat != null)
            _highFrictionMat.dynamicFriction = 0.05f;

        Vector3 slideDir = _endA.position.y < _endB.position.y
            ? (_endA.position - _endB.position).normalized
            : (_endB.position - _endA.position).normalized;

        var rb = _mountedRelic.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity  = true;
            rb.AddForce(slideDir * _slideForce + Vector3.up * 1f, ForceMode.Impulse);
        }

        _mountedRelic.transform.SetParent(null);
        _mountedRelic = null;

        Debug.Log($"[Stretcher] 傾き {tiltAngle:F1}° — 遺物が滑り落ちた");
    }

    private void OnJointBreak(float breakForce)
    {
        Debug.Log("[Stretcher] Joint破断 — 担架が手から離れた");
        _driverA = null;
        _driverB = null;
        _jointA  = null;
        _jointB  = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (_endA != null)
        {
            Gizmos.color = _driverA != null ? Color.green : Color.white;
            Gizmos.DrawWireSphere(_endA.position, 0.3f);
        }
        if (_endB != null)
        {
            Gizmos.color = _driverB != null ? Color.green : Color.white;
            Gizmos.DrawWireSphere(_endB.position, 0.3f);
        }
    }
}
