using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §5.2 + §8.3 — 折りたたみ担架のネットワーク同期コンパニオン。
///
/// StretcherItem は ItemBase（MonoBehaviour）継承のため NetworkBehaviour を直接継承できない。
/// このコンポーネントを同一 GameObject に追加することで、担ぎ手の割り当てをサーバー権威で同期する。
///
/// 同期する状態:
///   _carrierA — 端Aを担いでいるクライアントの ID（ulong.MaxValue = 未接続）
///   _carrierB — 端Bを担いでいるクライアントの ID（ulong.MaxValue = 未接続）
///
/// フロー:
///   クライアント側: PlayerInteraction.TryAttachToStretcher →
///       RequestAttachServerRpc → サーバーが _carrierA/_carrierB を更新 →
///       全クライアントの OnCarrierChanged コールバックが発火
/// </summary>
[RequireComponent(typeof(StretcherItem))]
[RequireComponent(typeof(NetworkObject))]
public class NetworkStretcherSync : NetworkBehaviour
{
    private StretcherItem _stretcher;

    // ── 同期変数 ─────────────────────────────────────────────
    // ulong.MaxValue = 未接続スロット
    private static readonly ulong EMPTY = ulong.MaxValue;

    private readonly NetworkVariable<ulong> _carrierA = new(
        EMPTY,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> _carrierB = new(
        EMPTY,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> _expanded = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<NetworkObjectReference> _mountedRelic = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool IsNetworkActive => IsSpawned && NetworkManager.Singleton != null;
    private void Awake()
    {
        _stretcher = GetComponent<StretcherItem>();
    }

    public override void OnNetworkSpawn()
    {
        _carrierA.OnValueChanged += OnCarrierAChanged;
        _carrierB.OnValueChanged += OnCarrierBChanged;
        _expanded.OnValueChanged += OnExpandedChanged;
        _mountedRelic.OnValueChanged += OnMountedRelicChanged;
    }

    public override void OnNetworkDespawn()
    {
        _carrierA.OnValueChanged -= OnCarrierAChanged;
        _carrierB.OnValueChanged -= OnCarrierBChanged;
        _expanded.OnValueChanged -= OnExpandedChanged;
        _mountedRelic.OnValueChanged -= OnMountedRelicChanged;
    }

    public bool RequestToggleExpand()
    {
        if (!IsNetworkActive) return false;
        if (IsServer) return ApplyToggleExpandLocal();
        RequestToggleExpandServerRpc();
        return true;
    }

    public bool RequestMountRelic(RelicBase relic)
    {
        if (!IsNetworkActive || relic == null) return false;

        var netObj = relic.GetComponent<NetworkObject>();
        if (netObj == null) return _stretcher.MountRelicLocal(relic);

        if (IsServer) return ApplyMountRelicLocal(netObj);
        RequestMountRelicServerRpc(new NetworkObjectReference(netObj));
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestToggleExpandServerRpc(RpcParams rpcParams = default)
    {
        ApplyToggleExpandLocal();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestMountRelicServerRpc(NetworkObjectReference relicRef, RpcParams rpcParams = default)
    {
        if (!relicRef.TryGet(out var netObj)) return;
        ApplyMountRelicLocal(netObj);
    }

    private bool ApplyToggleExpandLocal()
    {
        if (_stretcher == null) return false;
        bool ok = _stretcher.ToggleExpandLocal();
        if (ok && IsServer)
            _expanded.Value = _stretcher.IsExpanded;
        return ok;
    }

    private bool ApplyMountRelicLocal(NetworkObject netObj)
    {
        var relic = netObj.GetComponent<RelicBase>();
        if (relic == null) return false;

        bool ok = _stretcher.MountRelicLocal(relic);
        if (ok && IsServer)
            _mountedRelic.Value = netObj;
        return ok;
    }

    private void OnExpandedChanged(bool _, bool expanded)
    {
        _stretcher?.ApplyExpandedState(expanded);
    }

    private void OnMountedRelicChanged(NetworkObjectReference _, NetworkObjectReference current)
    {
        if (IsServer || _stretcher == null) return;
        if (!current.TryGet(out var netObj)) return;

        var relic = netObj.GetComponent<RelicBase>();
        if (relic != null && _stretcher.MountedRelic == null)
            _stretcher.MountRelicLocal(relic);
    }

    private void ApplyCarrierSlot(ulong clientId, bool isEndA)
    {
        if (_stretcher == null || clientId == EMPTY) return;

        var player = FindPlayerInteraction(clientId);
        if (player == null) return;

        if (isEndA && _stretcher.IsEndAFree)
            _stretcher.TryAttach(player, out _);
        else if (!isEndA && _stretcher.IsEndBFree)
            _stretcher.TryAttach(player, out _);
    }

    private void ClearCarrierSlot(ulong clientId)
    {
        var player = FindPlayerInteraction(clientId);
        if (player != null)
            _stretcher?.Detach(player);
    }

    private static PlayerInteraction FindPlayerInteraction(ulong clientId)
    {
        foreach (var inv in PlayerInventory.RegisteredInventories)
        {
            if (inv == null) continue;
            var netObj = inv.GetComponent<NetworkObject>();
            if (netObj != null && netObj.OwnerClientId == clientId)
                return inv.GetComponent<PlayerInteraction>();
        }
        return null;
    }

    // ── クライアント → サーバー RPC ──────────────────────────
    /// <summary>
    /// 担架への乗り込みをサーバーに要求する。
    /// 空きスロットがあれば割り当て、満員なら無視される。
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestAttachServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        // 既に乗り込んでいる場合は重複無視
        if (_carrierA.Value == clientId || _carrierB.Value == clientId) return;

        if (_carrierA.Value == EMPTY)
        {
            _carrierA.Value = clientId;
            Debug.Log($"[NetStretcher] 端A割り当て → client {clientId}");
        }
        else if (_carrierB.Value == EMPTY)
        {
            _carrierB.Value = clientId;
            Debug.Log($"[NetStretcher] 端B割り当て → client {clientId}（2人担架スタート）");
        }
        else
        {
            Debug.Log("[NetStretcher] 担架が満員（2人） → RequestAttach 拒否");
        }
    }

    /// <summary>
    /// 担架からの離脱をサーバーに通知する。
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestDetachServerRpc(RpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;

        if (_carrierA.Value == clientId)
        {
            _carrierA.Value = EMPTY;
            Debug.Log($"[NetStretcher] 端A解放 client {clientId}");
        }
        else if (_carrierB.Value == clientId)
        {
            _carrierB.Value = EMPTY;
            Debug.Log($"[NetStretcher] 端B解放 client {clientId}");
        }
    }

    // ── サーバー側強制解放（ホストからの管理用）─────────────
    /// <summary>サーバーが直接スロットを解放する（プレイヤー切断時など）。</summary>
    public void ForceDetach(ulong clientId)
    {
        if (!IsServer) return;

        if (_carrierA.Value == clientId) _carrierA.Value = EMPTY;
        if (_carrierB.Value == clientId) _carrierB.Value = EMPTY;
    }

    // ── 変化コールバック ──────────────────────────────────────
    private void OnCarrierAChanged(ulong oldId, ulong newId)
    {
        if (newId == EMPTY)
            ClearCarrierSlot(oldId);
        else
            ApplyCarrierSlot(newId, isEndA: true);

        string status = newId == EMPTY ? "解放" : $"client {newId} が接続";
        Debug.Log($"[NetStretcher] 端A: {status}");
    }

    private void OnCarrierBChanged(ulong oldId, ulong newId)
    {
        if (newId == EMPTY)
            ClearCarrierSlot(oldId);
        else
            ApplyCarrierSlot(newId, isEndA: false);

        string status = newId == EMPTY ? "解放" : $"client {newId} が接続";
        Debug.Log($"[NetStretcher] 端B: {status}");
    }

    // ── クエリ API ───────────────────────────────────────────
    /// <summary>指定クライアントが端Aを担いでいるか。</summary>
    public bool IsCarrierA(ulong clientId) => _carrierA.Value == clientId;

    /// <summary>指定クライアントが端Bを担いでいるか。</summary>
    public bool IsCarrierB(ulong clientId) => _carrierB.Value == clientId;

    /// <summary>指定クライアントがいずれかの端を担いでいるか。</summary>
    public bool IsCarrier(ulong clientId) =>
        _carrierA.Value == clientId || _carrierB.Value == clientId;

    public bool IsEndAOccupied => _carrierA.Value != EMPTY;
    public bool IsEndBOccupied => _carrierB.Value != EMPTY;
    public bool IsFullyOccupied => IsEndAOccupied && IsEndBOccupied;

    /// <summary>端Aを担いでいるクライアントID。未接続は ulong.MaxValue。</summary>
    public ulong CarrierAClientId => _carrierA.Value;

    /// <summary>端Bを担いでいるクライアントID。未接続は ulong.MaxValue。</summary>
    public ulong CarrierBClientId => _carrierB.Value;
}
