using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §5.1 — ショート/ロングロープの NGO 同期（プレイヤー間・遺物・アンカー切断）。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerShopRopeNetworkBridge : NetworkBehaviour
{
    private ulong _activePartnerClientId = ulong.MaxValue;

    public bool RequestConnectToPlayer(
        IShopRopeItem rope,
        int playerIdA,
        int playerIdB,
        float length,
        float breakForce)
    {
        if (rope == null || rope.IsConnected) return false;

        var partnerRb = FindPlayerRbByScoreId(playerIdB);
        if (partnerRb == null) return false;

        var partnerNet = partnerRb.GetComponent<NetworkObject>();
        if (!IsSpawned || partnerNet == null || !partnerNet.IsSpawned)
            return rope.TryConnectToPlayer(playerIdA, playerIdB);

        if (IsServer)
        {
            BroadcastConnectPlayerClientRpc(OwnerClientId, partnerNet.OwnerClientId, length, breakForce);
            return true;
        }

        RequestConnectPlayerServerRpc(partnerNet, length, breakForce);
        return true;
    }

    public bool RequestAttachToRelic(
        IShopRopeItem rope,
        RelicBase relic,
        float length,
        Vector3 fromPosition,
        float breakForce)
    {
        if (rope == null || relic == null || rope.IsConnected) return false;

        var relicNet = relic.GetComponent<NetworkObject>();
        if (!IsSpawned || relicNet == null || !relicNet.IsSpawned)
            return TryAttachRelicLocal(rope, relic, fromPosition, length, breakForce);

        if (!ValidateRelicAttach(relic, fromPosition)) return false;

        if (IsServer)
        {
            BroadcastAttachRelicClientRpc(OwnerClientId, new NetworkObjectReference(relicNet), length, breakForce);
            return true;
        }

        RequestAttachRelicServerRpc(new NetworkObjectReference(relicNet), length, fromPosition, breakForce);
        return true;
    }

    public bool RequestConnectToAnchor(
        IShopRopeItem rope,
        Transform anchor,
        int playerId,
        Vector3 fromPosition,
        float length,
        float breakForce)
    {
        if (rope == null || anchor == null || rope.IsConnected) return false;
        if (Vector3.Distance(fromPosition, anchor.position) > ShopRopeConstants.AnchorConnectRange)
            return false;

        if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return ApplyAnchorConnectLocal(rope, anchor, playerId, length, breakForce);

        if (IsServer)
        {
            BroadcastConnectAnchorClientRpc(OwnerClientId, anchor.position, length, breakForce);
            return true;
        }

        RequestConnectAnchorServerRpc(anchor.position, length, breakForce);
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestConnectAnchorServerRpc(
        Vector3 anchorPosition,
        float length,
        float breakForce,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        BroadcastConnectAnchorClientRpc(OwnerClientId, anchorPosition, length, breakForce);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    private void BroadcastConnectAnchorClientRpc(
        ulong ownerClientId,
        Vector3 anchorPosition,
        float length,
        float breakForce)
    {
        var anchor = FindAnchorNear(anchorPosition);
        var ownerRb = FindPlayerRbByClientId(ownerClientId);
        if (anchor == null || ownerRb == null) return;

        int playerId = PlayerScoreId.FromMember(ownerRb);
        var durability = IsOwner && ownerClientId == OwnerClientId
            ? FindHandShopRope() as ItemBase
            : null;

        GameServices.Ropes?.DisconnectPlayerAnchor(playerId);
        GameServices.Ropes?.ConnectPlayerToAnchor(playerId, anchor, length, breakForce, durability);

        if (IsOwner && ownerClientId == OwnerClientId)
            FindHandShopRope()?.ApplyAnchorConnectState(anchor, playerId);
    }

    private static bool ApplyAnchorConnectLocal(
        IShopRopeItem rope,
        Transform anchor,
        int playerId,
        float length,
        float breakForce)
    {
        if (GameServices.Ropes == null) return false;
        if (!GameServices.Ropes.ConnectPlayerToAnchor(playerId, anchor, length, breakForce, rope as ItemBase))
            return false;

        rope.ApplyAnchorConnectState(anchor, playerId);
        return true;
    }

    private static Transform FindAnchorNear(Vector3 position)
    {
        const float tolerance = 0.75f;
        float tolSqr = tolerance * tolerance;

        foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (go == null || !go.name.Contains("AnchorBolt_Placed")) continue;
            if ((go.position - position).sqrMagnitude <= tolSqr)
                return go;
        }

        return null;
    }

    public void RequestDisconnectShopRope(IShopRopeItem rope)
    {
        if (rope == null || !rope.IsConnected) return;

        if (!IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            rope.CutRopeLocalOnly();
            return;
        }

        if (IsServer)
        {
            BroadcastDisconnectShopRopeClientRpc(OwnerClientId);
            return;
        }

        RequestDisconnectShopRopeServerRpc();
    }

    public void ServerForceDisconnectAllRopes()
    {
        if (!IsServer) return;
        BroadcastDisconnectShopRopeClientRpc(OwnerClientId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestConnectPlayerServerRpc(
        NetworkObjectReference partnerRef,
        float length,
        float breakForce,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!partnerRef.TryGet(out var partnerNet)) return;

        BroadcastConnectPlayerClientRpc(OwnerClientId, partnerNet.OwnerClientId, length, breakForce);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    private void BroadcastConnectPlayerClientRpc(
        ulong ownerClientId,
        ulong partnerClientId,
        float length,
        float breakForce)
    {
        ApplyPlayerPairConnection(ownerClientId, partnerClientId, length, breakForce, null);

        if (!IsOwner || ownerClientId != OwnerClientId) return;

        var rope = FindHandShopRope();
        if (rope == null) return;

        var ownerRb = FindPlayerRbByClientId(ownerClientId);
        var partnerRb = FindPlayerRbByClientId(partnerClientId);
        if (ownerRb == null || partnerRb == null) return;

        rope.ApplyPlayerConnectState(
            PlayerScoreId.FromMember(ownerRb),
            PlayerScoreId.FromMember(partnerRb));
    }

    private void ApplyPlayerPairConnection(
        ulong ownerClientId,
        ulong partnerClientId,
        float length,
        float breakForce,
        IShopRopeItem ownerRopeHint)
    {
        var ownerRb = FindPlayerRbByClientId(ownerClientId);
        var partnerRb = FindPlayerRbByClientId(partnerClientId);
        if (ownerRb == null || partnerRb == null) return;

        int idA = PlayerScoreId.FromMember(ownerRb);
        int idB = PlayerScoreId.FromMember(partnerRb);

        var ropes = GameServices.Ropes;
        if (ropes == null) return;

        ItemBase durabilityItem = ownerRopeHint as ItemBase;
        if (durabilityItem == null && IsOwner && ownerClientId == OwnerClientId)
            durabilityItem = FindHandShopRope() as ItemBase;

        ropes.DisconnectRope(idA, idB);
        ropes.ConnectRope(idA, idB, length, breakForce, durabilityItem);

        if (IsServer && ownerClientId == OwnerClientId)
            _activePartnerClientId = partnerClientId;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestAttachRelicServerRpc(
        NetworkObjectReference relicRef,
        float length,
        Vector3 fromPosition,
        float breakForce,
        RpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!relicRef.TryGet(out var relicNet)) return;

        var relic = relicNet.GetComponent<RelicBase>();
        if (relic == null || !ValidateRelicAttach(relic, fromPosition)) return;

        BroadcastAttachRelicClientRpc(OwnerClientId, relicRef, length, breakForce);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    private void BroadcastAttachRelicClientRpc(
        ulong playerClientId,
        NetworkObjectReference relicRef,
        float length,
        float breakForce)
    {
        if (!relicRef.TryGet(out var relicNet)) return;
        var relic = relicNet.GetComponent<RelicBase>();

        ApplyRelicConnection(playerClientId, relicNet, length, breakForce);

        if (IsOwner && playerClientId == OwnerClientId && relic != null)
            FindHandShopRope()?.ApplyRelicAttachState(relic, PlayerScoreId.FromMember(this));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestDisconnectShopRopeServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        BroadcastDisconnectShopRopeClientRpc(clientId);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Everyone)]
    private void BroadcastDisconnectShopRopeClientRpc(ulong initiatorClientId)
    {
        ApplyDisconnectForClient(initiatorClientId);

        if (IsOwner && initiatorClientId == OwnerClientId)
            FindHandShopRope()?.CutRopeLocalOnly();
        else if (IsOwner && _activePartnerClientId == initiatorClientId)
            FindHandShopRope()?.CutRopeLocalOnly();
    }

    private static void ApplyRelicConnection(
        ulong playerClientId,
        NetworkObject relicNet,
        float length,
        float breakForce)
    {
        var relicRb = relicNet.GetComponent<Rigidbody>();
        if (relicRb == null) return;

        if (relicRb.isKinematic)
            relicRb.isKinematic = false;

        var playerRb = FindPlayerRbByClientId(playerClientId);
        if (playerRb == null) return;

        var ropes = GameServices.Ropes;
        if (ropes == null) return;

        int playerId = PlayerScoreId.FromMember(playerRb);
        ropes.DisconnectPlayerRelic(playerId);
        ropes.ConnectPlayerToRelic(playerId, relicRb, length, breakForce, null);
    }

    private static void ApplyDisconnectForClient(ulong clientId)
    {
        var rb = FindPlayerRbByClientId(clientId);
        if (rb == null) return;

        int playerId = PlayerScoreId.FromMember(rb);
        GameServices.Ropes?.DisconnectAllForPlayer(playerId);
    }

    private static bool TryAttachRelicLocal(
        IShopRopeItem rope,
        RelicBase relic,
        Vector3 fromPosition,
        float length,
        float breakForce)
    {
        if (rope is ShortRopeItem shortRope)
            return shortRope.TryAttachToRelicLocal(
                relic, PlayerScoreId.FromMember(shortRope), fromPosition, length, breakForce);

        if (rope is LongRopeItem longRope)
            return longRope.TryAttachToRelicLocal(
                relic, PlayerScoreId.FromMember(longRope), fromPosition, length, breakForce);

        return false;
    }

    private bool ValidateRelicAttach(RelicBase relic, Vector3 fromPosition)
    {
        if (relic == null) return false;

        var grab = relic.GetComponent<RelicGrabPoint>();
        if (grab != null && !grab.IsWithinAttachRange(fromPosition)) return false;
        if (grab == null && Vector3.Distance(fromPosition, relic.transform.position) > ShopRopeConstants.ConnectRange)
            return false;

        var carrier = relic.GetComponent<RelicCarrier>();
        return carrier == null || !carrier.IsBeingCarried;
    }

    private IShopRopeItem FindHandShopRope()
    {
        var inv = GetComponent<PlayerInventory>();
        return inv?.HandItem as IShopRopeItem;
    }

    private static Rigidbody FindPlayerRbByClientId(ulong clientId)
    {
        foreach (var inv in PlayerInventory.RegisteredInventories)
        {
            if (inv == null) continue;
            var netObj = inv.GetComponent<NetworkObject>();
            if (netObj == null || netObj.OwnerClientId != clientId) continue;

            var rb = inv.GetComponent<Rigidbody>();
            if (rb != null) return rb;
        }
        return null;
    }

    private static Rigidbody FindPlayerRbByScoreId(int playerId)
    {
        foreach (var inv in PlayerInventory.RegisteredInventories)
        {
            if (inv == null) continue;
            if (PlayerScoreId.FromMember(inv) != playerId) continue;

            var rb = inv.GetComponent<Rigidbody>();
            if (rb != null) return rb;
        }
        return null;
    }
}
