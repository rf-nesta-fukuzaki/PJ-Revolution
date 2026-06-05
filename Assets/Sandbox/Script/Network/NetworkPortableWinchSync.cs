using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §8.3 — ポータブルウインチの ServerRpc 同期（設置/ケーブル/巻上/切断/回収）。
/// ケーブル物理はホスト権威（<see cref="PortableWinchItem.ShouldRunCableSimulation"/>）。
/// </summary>
[RequireComponent(typeof(PortableWinchItem))]
public class NetworkPortableWinchSync : NetworkBehaviour
{
    public enum CablePhase : byte
    {
        None     = 0,
        HookOut  = 1,
        Attached = 2,
        Broken   = 3,
    }

    private PortableWinchItem _winch;

    private readonly NetworkVariable<bool> _deployed = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> _reeling = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<CablePhase> _cablePhase = new(
        CablePhase.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Vector3> _deployPosition = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Quaternion> _deployRotation = new(
        Quaternion.identity,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<NetworkObjectReference> _attachedTarget = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<Vector3> _hookWorldPosition = new(
        Vector3.zero,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsNetworkActive => IsSpawned && NetworkManager.Singleton != null;

    private void Awake() => _winch = GetComponent<PortableWinchItem>();

    public override void OnNetworkSpawn()
    {
        _deployed.OnValueChanged += OnDeployedChanged;
        _reeling.OnValueChanged += OnReelingChanged;
        _cablePhase.OnValueChanged += OnCablePhaseChanged;
        _hookWorldPosition.OnValueChanged += OnHookWorldPositionChanged;
        _winch?.RefreshCableSimulationMode();
    }

    public override void OnNetworkDespawn()
    {
        _deployed.OnValueChanged -= OnDeployedChanged;
        _reeling.OnValueChanged -= OnReelingChanged;
        _cablePhase.OnValueChanged -= OnCablePhaseChanged;
        _hookWorldPosition.OnValueChanged -= OnHookWorldPositionChanged;
    }

    private void FixedUpdate()
    {
        if (!IsServer || _winch == null) return;
        if (_cablePhase.Value == CablePhase.None || _cablePhase.Value == CablePhase.Broken) return;

        _hookWorldPosition.Value = _winch.GetCableHookWorldPosition();
    }

    public bool RequestDeploy(PlayerInventory inventory, Transform player)
    {
        if (!IsNetworkActive || inventory == null || player == null) return false;

        if (IsServer)
        {
            EnsureSpawnedOnServer();
            return ApplyDeployLocal(inventory, player);
        }

        if (!PortableWinchItem.TryFindDeployPoint(player, out var point, out var normal))
            return false;

        RequestDeployServerRpc(point, Quaternion.FromToRotation(Vector3.up, normal));
        return true;
    }

    public bool RequestDeployCable()
    {
        if (!IsNetworkActive) return false;
        if (IsServer) return ApplyDeployCableLocal();
        RequestDeployCableServerRpc();
        return true;
    }

    public bool RequestAttachCable(Rigidbody target)
    {
        if (!IsNetworkActive || target == null) return false;

        var netObj = target.GetComponentInParent<NetworkObject>();
        if (netObj == null) return ApplyAttachCableLocal(target);

        if (IsServer) return ApplyAttachCableLocal(target);

        RequestAttachCableServerRpc(netObj);
        return true;
    }

    public bool RequestToggleReel(Transform operatorTransform)
    {
        if (!IsNetworkActive) return false;
        if (IsServer) return ApplyToggleReelLocal(operatorTransform);
        RequestToggleReelServerRpc();
        return true;
    }

    public void RequestCutCable()
    {
        if (!IsNetworkActive)
        {
            _winch.CutCableLocal();
            return;
        }

        if (IsServer) ApplyCutCableLocal();
        else RequestCutCableServerRpc();
    }

    public bool RequestRetrieve(PlayerInventory inventory)
    {
        if (!IsNetworkActive || inventory == null) return false;
        if (IsServer) return ApplyRetrieveLocal(inventory);
        RequestRetrieveServerRpc();
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDeployServerRpc(Vector3 point, Quaternion rotation, RpcParams rpcParams = default)
    {
        var inv = FindInventory(rpcParams.Receive.SenderClientId);
        if (inv == null || inv.HandItem != _winch) return;

        EnsureSpawnedOnServer();
        inv.Remove(_winch);
        _winch.ApplyDeployAt(point, rotation);
        SyncFromWinch();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDeployCableServerRpc(RpcParams rpcParams = default)
    {
        ApplyDeployCableLocal();
        SyncFromWinch();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestAttachCableServerRpc(NetworkObjectReference targetRef, RpcParams rpcParams = default)
    {
        if (!targetRef.TryGet(out var netObj)) return;
        var rb = netObj.GetComponent<Rigidbody>();
        if (rb == null) return;

        ApplyAttachCableLocal(rb);
        SyncFromWinch();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestToggleReelServerRpc(RpcParams rpcParams = default)
    {
        var op = FindOperatorTransform(rpcParams.Receive.SenderClientId);
        ApplyToggleReelLocal(op);
        SyncFromWinch();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestCutCableServerRpc(RpcParams rpcParams = default)
    {
        ApplyCutCableLocal();
        SyncFromWinch();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestRetrieveServerRpc(RpcParams rpcParams = default)
    {
        var inv = FindInventory(rpcParams.Receive.SenderClientId);
        if (inv == null) return;
        ApplyRetrieveLocal(inv);
        SyncFromWinch();
    }

    private bool ApplyDeployLocal(PlayerInventory inventory, Transform player)
    {
        EnsureSpawnedOnServer();
        bool ok = _winch.DeployFromHandLocal(inventory, player);
        if (ok) SyncFromWinch();
        return ok;
    }

    private bool ApplyDeployCableLocal() => _winch.DeployCableLocal();

    private bool ApplyAttachCableLocal(Rigidbody target) => _winch.AttachCableLocal(target);

    private bool ApplyToggleReelLocal(Transform op) => _winch.ToggleReelLocal(op);

    private void ApplyCutCableLocal() => _winch.CutCableLocal();

    private bool ApplyRetrieveLocal(PlayerInventory inventory) => _winch.RetrieveLocal(inventory);

    private void SyncFromWinch()
    {
        if (!IsServer) return;

        _deployed.Value      = _winch.IsDeployedInWorld;
        _reeling.Value       = _winch.IsReeling;
        _cablePhase.Value    = _winch.IsCableBroken ? CablePhase.Broken
                               : _winch.IsCableAttached ? CablePhase.Attached
                               : _winch.HasCableHook ? CablePhase.HookOut
                               : CablePhase.None;
        _deployPosition.Value  = transform.position;
        _deployRotation.Value  = transform.rotation;

        var attached = _winch.CableAttachedBody;
        _attachedTarget.Value = attached != null
            ? attached.GetComponentInParent<NetworkObject>()
            : default;
    }

    private void EnsureSpawnedOnServer()
    {
        if (!IsServer) return;
        var netObj = GetComponent<NetworkObject>();
        if (netObj != null && !netObj.IsSpawned)
            netObj.Spawn(destroyWithScene: true);
    }

    private void OnDeployedChanged(bool _, bool deployed)
    {
        if (IsServer || _winch == null) return;
        if (deployed && !_winch.IsDeployedInWorld)
            _winch.ApplyDeployAt(_deployPosition.Value, _deployRotation.Value);
    }

    private void OnReelingChanged(bool _, bool reeling)
    {
        if (IsServer || _winch == null) return;
        if (reeling && !_winch.IsReeling)
            _winch.ToggleReelLocal(null);
        else if (!reeling && _winch.IsReeling)
            _winch.StopReelLocal();
    }

    private void OnCablePhaseChanged(CablePhase _, CablePhase phase)
    {
        if (IsServer || _winch == null) return;

        switch (phase)
        {
            case CablePhase.HookOut when !_winch.HasCableHook:
                _winch.DeployCableLocal();
                _winch.RefreshCableSimulationMode();
                break;
            case CablePhase.Attached when ! _winch.IsCableAttached && _attachedTarget.Value.TryGet(out var netObj):
                var rb = netObj.GetComponent<Rigidbody>();
                if (rb != null) _winch.AttachCableLocal(rb);
                _winch.RefreshCableSimulationMode();
                break;
            case CablePhase.Broken:
                _winch.ApplyBrokenCableVisual();
                break;
        }
    }

    private void OnHookWorldPositionChanged(Vector3 _, Vector3 hookPos)
    {
        if (IsServer || _winch == null) return;
        _winch.ApplyClientCableHookPosition(hookPos);
    }

    private static Transform FindOperatorTransform(ulong clientId)
    {
        foreach (var inv in PlayerInventory.RegisteredInventories)
        {
            if (inv == null) continue;
            var netObj = inv.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == clientId)
                return inv.transform;
        }
        return null;
    }

    private static PlayerInventory FindInventory(ulong clientId)
    {
        foreach (var inv in PlayerInventory.RegisteredInventories)
        {
            if (inv == null) continue;
            var netObj = inv.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == clientId)
                return inv;
        }
        return null;
    }
}
