using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §5.2 / §8.3 — ポータブルウインチ（7ステップ操作フロー）。
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class PortableWinchItem : ItemBase
{
    public const float OperateRange = 3f;
    private const float MaxSlopeDeg = 30f;
    private const float CableBreakTension = 5000f;
    private const float OverloadThreshold = 700f;
    private const float OverloadBreakChancePerSec = 0.02f;

    [Header("ウインチ設定")]
    [SerializeField] private float _maxCableLength = 20f;
    [SerializeField] private float _reelSpeed      = 1.5f;

    private LineRenderer          _lineRenderer;
    private IWinchCableDriver     _cable;
    private Transform             _cableAnchor;
    private NetworkPortableWinchSync _netSync;
    private bool                  _isDeployed;
    private bool                  _isReeling;
    private float                 _overloadTimer;

    public bool IsDeployedInWorld => _isDeployed;
    public bool IsReeling         => _isReeling;
    public bool HasCableHook      => _cable != null && _cable.HasHook;
    public bool IsCableAttached   => _cable != null && _cable.IsAttached;
    public bool IsCableBroken     => _cable != null && _cable.IsBroken;
    public Rigidbody CableAttachedBody => _cable?.AttachedBody;

    protected override void Awake()
    {
        base.Awake();
        _itemName          = "ポータブルウインチ";
        _cost              = 20;
        _weight            = 3f;
        _slots             = 2;
        _maxDurability     = 50f;
        _currentDurability = _maxDurability;
        _impactDmgScale    = 1.5f;

        _lineRenderer = GetComponent<LineRenderer>();
        _netSync      = GetComponent<NetworkPortableWinchSync>();
        EnsureLineRendererDefaults();
        ResolveCableDriver();
        EnsureCableAnchor();
    }

    private void ResolveCableDriver()
    {
        var chain = GetComponent<WinchCableChain>();
        if (chain == null)
            chain = gameObject.AddComponent<WinchCableChain>();

        var legacy = GetComponent<WinchCableSystem>();
        if (legacy != null)
            legacy.enabled = false;

        _cable = chain;
    }

    private void EnsureCableAnchor()
    {
        if (_cableAnchor != null) return;
        var go = transform.Find("CableAnchor");
        if (go == null)
        {
            go = new GameObject("CableAnchor").transform;
            go.SetParent(transform, false);
            go.localPosition = new Vector3(0f, 0.15f, 0f);
        }
        _cableAnchor = go;
    }

    private void EnsureLineRendererDefaults()
    {
        if (_lineRenderer == null) return;
        _lineRenderer.startWidth = 0.03f;
        _lineRenderer.endWidth   = 0.015f;
        _lineRenderer.enabled    = false;
        _lineRenderer.positionCount = 2;
    }

    public bool ShouldRunCableSimulation()
    {
        if (_netSync == null || !_netSync.IsNetworkActive) return true;
        return _netSync.IsServer;
    }

    public void RefreshCableSimulationMode()
    {
        if (_cable is WinchCableChain chain)
            chain.SetSimulatePhysics(ShouldRunCableSimulation());
    }

    public Vector3 GetCableHookWorldPosition()
    {
        if (_cable == null || !_cable.HasHook) return _cableAnchor != null ? _cableAnchor.position : transform.position;
        var body = _cable.AttachedBody ?? _cable.HookBody;
        return body != null ? body.position : transform.position;
    }

    public void ApplyClientCableHookPosition(Vector3 hookPosition)
    {
        if (_cable is WinchCableChain chain)
            chain.ApplyClientHookPosition(hookPosition);
    }

    private void FixedUpdate()
    {
        if (!ShouldRunCableSimulation()) return;
        if (!_isDeployed || _cable == null || _cable.IsBroken) return;

        if (_isReeling)
        {
            _cable.Reel(Time.fixedDeltaTime);
            ConsumeDurability(5f * Time.fixedDeltaTime);
        }

        CheckCableBreak();
    }

    // ── 公開 API（ネットワーク経由） ─────────────────────────────
    public bool TryDeployFromHand(PlayerInventory inventory, Transform player)
    {
        if (_netSync != null && _netSync.IsNetworkActive)
            return _netSync.RequestDeploy(inventory, player);
        return DeployFromHandLocal(inventory, player);
    }

    public bool TryDeployCable()
    {
        if (_netSync != null && _netSync.IsNetworkActive)
            return _netSync.RequestDeployCable();
        return DeployCableLocal();
    }

    public bool TryAttachCableTo(Rigidbody target)
    {
        target = ResolveCableTarget(target);
        if (target == null) return false;

        if (_netSync != null && _netSync.IsNetworkActive)
            return _netSync.RequestAttachCable(target);
        return AttachCableLocal(target);
    }

    public bool TryToggleReel(Transform operatorTransform)
    {
        if (_netSync != null && _netSync.IsNetworkActive)
            return _netSync.RequestToggleReel(operatorTransform);
        return ToggleReelLocal(operatorTransform);
    }

    public void StopReel() => StopReelLocal();

    public void UpdateOperatorDistance(Transform operatorTransform)
    {
        if (!_isReeling || operatorTransform == null) return;
        if (Vector3.Distance(operatorTransform.position, transform.position) > OperateRange)
            StopReelLocal();
    }

    public bool TryRetrieve(PlayerInventory inventory)
    {
        if (_netSync != null && _netSync.IsNetworkActive)
            return _netSync.RequestRetrieve(inventory);
        return RetrieveLocal(inventory);
    }

    public void CutCable()
    {
        if (_netSync != null && _netSync.IsNetworkActive)
            _netSync.RequestCutCable();
        else
            CutCableLocal();
    }

    // ── ローカル/サーバー適用 ───────────────────────────────────
    public bool DeployFromHandLocal(PlayerInventory inventory, Transform player)
    {
        if (_isBroken || _isDeployed || inventory == null || player == null) return false;
        if (inventory.HandItem != this) return false;

        if (!TryFindDeployPoint(player, out var point, out var normal))
        {
            Debug.Log("[Winch] 設置できません（傾斜30°超 or 地面なし）");
            return false;
        }

        inventory.Remove(this);
        ApplyDeployAt(point, Quaternion.FromToRotation(Vector3.up, normal));
        return true;
    }

    public void ApplyDeployAt(Vector3 point, Quaternion rotation)
    {
        transform.SetParent(null);
        transform.SetPositionAndRotation(point, rotation);
        gameObject.SetActive(true);

        _rb.isKinematic = true;
        _rb.useGravity  = false;
        _isDeployed     = true;

        _cable.Configure(_cableAnchor, _lineRenderer, _maxCableLength, _reelSpeed);
        GameServices.Audio?.PlaySE(SoundId.WinchStart, point);
        Debug.Log($"[Winch] 設置完了: {point}");
    }

    public bool DeployCableLocal()
    {
        if (!_isDeployed || _cable == null || _cable.HasHook || _cable.IsBroken) return false;

        Vector3 spawn = _cableAnchor.position + _cableAnchor.forward * 0.5f + Vector3.up * 0.2f;
        if (!_cable.DeployHook(spawn)) return false;

        RefreshCableSimulationMode();
        Debug.Log("[Winch] ケーブル展開");
        return true;
    }

    public bool AttachCableLocal(Rigidbody target)
    {
        if (!_isDeployed || _cable == null || target == null) return false;
        if (!_cable.HasHook || _cable.IsAttached) return false;
        if (!_cable.TryAttachHookTo(target)) return false;

        GameServices.Audio?.PlaySE(SoundId.WinchStart, target.position);
        Debug.Log($"[Winch] ケーブル接続: {target.name}");
        return true;
    }

    public bool ToggleReelLocal(Transform operatorTransform)
    {
        if (!_isDeployed || _cable == null || _cable.IsBroken) return false;
        if (operatorTransform != null
            && Vector3.Distance(operatorTransform.position, transform.position) > OperateRange)
        {
            StopReelLocal();
            return false;
        }

        if (!_cable.HasHook && !_cable.IsAttached) return false;

        _isReeling = !_isReeling;
        if (_isReeling)
            GameServices.Audio?.PlaySE2D(SoundId.WinchLoop);
        else
            GameServices.Audio?.StopLoop(SoundId.WinchLoop);

        Debug.Log(_isReeling ? "[Winch] 巻き上げ開始" : "[Winch] 巻き上げ停止");
        return true;
    }

    public void StopReelLocal()
    {
        if (!_isReeling) return;
        _isReeling = false;
        GameServices.Audio?.StopLoop(SoundId.WinchLoop);
    }

    public bool RetrieveLocal(PlayerInventory inventory)
    {
        if (!_isDeployed || inventory == null) return false;
        if (_cable != null && (_cable.IsAttached || (_cable.HasHook && !_cable.IsBroken))) return false;

        _cable?.RetractAndDestroy();
        _isDeployed = false;
        _isReeling  = false;
        _rb.isKinematic = true;

        if (inventory.TryEquipHand(this))
        {
            Debug.Log("[Winch] 回収して手持ちに");
            return true;
        }

        _rb.isKinematic = false;
        return false;
    }

    public void CutCableLocal()
    {
        if (_cable == null || (!_cable.HasHook && !_cable.IsAttached)) return;

        _cable.BreakCable();
        _isReeling = false;
        GameServices.Audio?.StopLoop(SoundId.WinchLoop);
        GameServices.Audio?.PlaySE(SoundId.WinchCableSnap, transform.position);
        ConsumeDurability(30f);
        Debug.Log("[Winch] ケーブル切断");
    }

    public void ApplyBrokenCableVisual()
    {
        if (_cable == null || _cable.IsBroken) return;
        _cable.BreakCable();
        _isReeling = false;
    }

    public static bool TryFindDeployPoint(Transform player, out Vector3 point, out Vector3 normal)
    {
        point  = player.position;
        normal = Vector3.up;

        if (!Physics.Raycast(player.position + Vector3.up * 0.5f, Vector3.down, out var hit, 4f))
            return false;

        if (Vector3.Angle(hit.normal, Vector3.up) > MaxSlopeDeg)
            return false;

        point  = hit.point + hit.normal * 0.05f;
        normal = hit.normal;
        return true;
    }

    private static Rigidbody ResolveCableTarget(Rigidbody target)
    {
        if (target == null) return null;

        var grab = target.GetComponentInParent<RelicGrabPoint>();
        if (grab != null)
            return grab.AttachBody;

        return target;
    }

    private void CheckCableBreak()
    {
        if (_cable == null) return;

        float tension = _cable.EstimateTension();
        if (tension > CableBreakTension)
        {
            CutCable();
            return;
        }

        if (tension > OverloadThreshold)
        {
            _overloadTimer += Time.fixedDeltaTime;
            if (Random.value < OverloadBreakChancePerSec * Time.fixedDeltaTime)
                CutCable();
        }
        else
        {
            _overloadTimer = 0f;
        }
    }

    public static PortableWinchItem FindDeployedNear(Vector3 position, float range)
    {
        var all = Object.FindObjectsByType<PortableWinchItem>(FindObjectsSortMode.None);
        PortableWinchItem nearest = null;
        float best = range;

        foreach (var winch in all)
        {
            if (winch == null || !winch._isDeployed) continue;
            float d = Vector3.Distance(position, winch.transform.position);
            if (d <= best)
            {
                best = d;
                nearest = winch;
            }
        }

        return nearest;
    }

    protected override void OnItemBroken()
    {
        CutCable();
        base.OnItemBroken();
    }
}
