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

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _stretcher = GetComponent<StretcherItem>();
    }

    public override void OnNetworkSpawn()
    {
        _carrierA.OnValueChanged += OnCarrierAChanged;
        _carrierB.OnValueChanged += OnCarrierBChanged;
    }

    public override void OnNetworkDespawn()
    {
        _carrierA.OnValueChanged -= OnCarrierAChanged;
        _carrierB.OnValueChanged -= OnCarrierBChanged;
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
        string status = newId == EMPTY ? "解放" : $"client {newId} が接続";
        Debug.Log($"[NetStretcher] 端A: {status}");
    }

    private void OnCarrierBChanged(ulong oldId, ulong newId)
    {
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
