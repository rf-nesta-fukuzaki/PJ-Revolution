using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §3.1 — ポイント＆グラブ方式の登攀コントローラー。
/// 状態遷移は <see cref="ClimbingStateMachine"/> に委譲する。
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ClimbingController : MonoBehaviour
{
    [Header("設定 (ScriptableObject — 未設定時は Inspector デフォルト)")]
    [SerializeField] private ClimbingConfigSO _config;

    [Header("検出設定 (Config 未設定時のフォールバック)")]
    [SerializeField] private float _detectionRadius  = 2.5f;
    [SerializeField] private LayerMask _grabLayer;

    [Header("クライミング物理 (Config 未設定時のフォールバック)")]
    [SerializeField] private float _pullSpeed        = 4f;
    [SerializeField] private float _holdGravityScale = 0.2f;
    [SerializeField] private float _releaseImpulse   = 3f;
    [SerializeField] private float _holdHeightOffset = 0.8f;
    [SerializeField] private float _verticalInputScale = 0.5f;

    [Header("スタミナ")]
    [SerializeField] private StaminaSystem _stamina;

    private readonly ClimbingStateMachine _stateMachine = new();

    private Rigidbody         _rb;
    private PlayerInventory   _inventory;
    private Animator          _animator;
    private GrabPoint         _nearestGrabPoint;
    private GrabPoint         _currentGrabPoint;

    private static readonly int IsClimbingHash = Animator.StringToHash("IsClimbing");

    private readonly HashSet<GrabPoint> _highlighted = new();
    private readonly Collider[]         _overlapBuffer = new Collider[32];
    private readonly HashSet<GrabPoint> _scanResultSet = new();

    private float DetectionRadiusValue   => _config != null ? _config.DetectionRadius : _detectionRadius;
    private float PullSpeedValue         => _config != null ? _config.PullSpeed : _pullSpeed;
    private float HoldGravityScaleValue  => _config != null ? _config.HoldGravityScale : _holdGravityScale;
    private float ReleaseImpulseValue    => _config != null ? _config.ReleaseImpulse : _releaseImpulse;
    private float HoldHeightOffsetValue  => _config != null ? _config.HoldHeightOffset : _holdHeightOffset;
    private float VerticalInputScaleValue => _config != null ? _config.VerticalInputScale : _verticalInputScale;

    public bool IsClimbing => _stateMachine.IsClimbing;
    public GrabPoint HeldPoint => _currentGrabPoint;

    private void Awake()
    {
        _rb        = GetComponent<Rigidbody>();
        _inventory = GetComponent<PlayerInventory>();
        _animator  = GetComponentInChildren<Animator>();
        if (_stamina == null)
            _stamina = GetComponent<StaminaSystem>();

        _stateMachine.OnStateChanged += HandleClimbingStateChanged;
    }

    private void OnDestroy()
    {
        _stateMachine.OnStateChanged -= HandleClimbingStateChanged;
    }

    private void HandleClimbingStateChanged(ClimbingState prev, ClimbingState next)
    {
        _animator?.SetBool(IsClimbingHash, next == ClimbingState.Climbing);
    }

    private void Update()
    {
        ScanNearbyGrabPoints();
        HandleGrabInput();
    }

    private void FixedUpdate()
    {
        if (!_stateMachine.IsClimbing) return;
        ApplyClimbingPhysics();
    }

    private void ScanNearbyGrabPoints()
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, DetectionRadiusValue, _overlapBuffer, _grabLayer);

        _scanResultSet.Clear();
        GrabPoint closest  = null;
        float     closestD = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            var gp = _overlapBuffer[i].GetComponent<GrabPoint>();
            if (gp == null || gp.IsOccupied) continue;

            _scanResultSet.Add(gp);
            float d = Vector3.Distance(transform.position, gp.transform.position);
            if (d < closestD)
            {
                closestD = d;
                closest  = gp;
            }
        }

        foreach (var gp in _highlighted)
        {
            if (!_scanResultSet.Contains(gp))
                gp.SetHighlight(false);
        }

        _highlighted.Clear();

        foreach (var gp in _scanResultSet)
        {
            gp.SetHighlight(true);
            _highlighted.Add(gp);
        }

        _nearestGrabPoint = closest;
    }

    private void HandleGrabInput()
    {
        if (!InputStateReader.InteractPressedThisFrame()) return;

        if (_stateMachine.IsClimbing)
            ReleaseGrab();
        else
            TryGrab();
    }

    private void TryGrab()
    {
        if (_nearestGrabPoint == null) return;
        if (_nearestGrabPoint.RequireIceAxe && !ConsumeIceAxeUse()) return;
        if (!_nearestGrabPoint.TryOccupy()) return;

        _currentGrabPoint = _nearestGrabPoint;
        if (!_stateMachine.TryGrab()) return;

        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        GameServices.Audio?.PlaySE(SoundId.ClimbGrab, _currentGrabPoint.transform.position);
        Debug.Log($"[Climbing] グラブ: {_currentGrabPoint.name}");
    }

    private void ReleaseGrab()
    {
        if (_currentGrabPoint == null) return;

        Vector3 grabPos = _currentGrabPoint.transform.position;

        _currentGrabPoint.Release();
        _currentGrabPoint = null;
        _stateMachine.TryRelease();

        GameServices.Audio?.PlaySE(SoundId.ClimbRelease, grabPos);

        _rb.AddForce(Vector3.up * ReleaseImpulseValue + transform.forward * ReleaseImpulseValue * 0.5f,
            ForceMode.Impulse);
    }

    private void ApplyClimbingPhysics()
    {
        if (_currentGrabPoint == null)
        {
            _stateMachine.TryRelease();
            return;
        }

        if (_stamina != null)
        {
            _stamina.Consume(_currentGrabPoint.StaminaDrain * Time.fixedDeltaTime);
            if (_stamina.IsEmpty)
            {
                ReleaseGrab();
                return;
            }
        }

        Vector3 targetPos = _currentGrabPoint.transform.position - transform.up * HoldHeightOffsetValue;
        Vector3 toTarget  = targetPos - _rb.position;

        if (toTarget.magnitude > 0.1f)
        {
            _rb.linearVelocity = toTarget.normalized * PullSpeedValue;
        }
        else
        {
            Vector3 vel = _rb.linearVelocity;
            vel.y = Mathf.Max(vel.y, -0.5f) * HoldGravityScaleValue;
            _rb.linearVelocity = vel;
            _rb.AddForce(-Physics.gravity * (1f - HoldGravityScaleValue), ForceMode.Acceleration);
        }

        float vertInput = InputStateReader.ReadVerticalAxisRaw();
        if (Mathf.Abs(vertInput) > 0.1f)
            _rb.AddForce(Vector3.up * vertInput * PullSpeedValue * VerticalInputScaleValue, ForceMode.Acceleration);
    }

    private bool ConsumeIceAxeUse()
    {
        if (_inventory == null) return false;
        var axe = _inventory.GetItem(IceAxeItem.ItemNameKey) as IceAxeItem;
        return axe != null && axe.TryUse();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, DetectionRadiusValue);
    }
}
