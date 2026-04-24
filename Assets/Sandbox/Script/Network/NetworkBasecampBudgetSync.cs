using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §2.2 — チーム共有予算（100pt）のネットワーク同期コンパニオン。
///
/// BasecampShop は MonoBehaviour のため NetworkVariable を持てない。
/// このコンポーネントを同一 GameObject（または Managers に追加）することで
/// 予算の状態をサーバー権威で全クライアントに同期する。
///
/// フロー:
///   クライアント側: BasecampShop.TryPurchase →
///       DeductServerRpc → サーバーが _budget を更新 →
///       NetworkVariable の変化が全クライアントに伝播 →
///       BasecampShop.OnBudgetChanged → UI 更新
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class NetworkBasecampBudgetSync : NetworkBehaviour
{
    public static NetworkBasecampBudgetSync Instance { get; private set; }

    public const int INITIAL_BUDGET = 100;

    private readonly NetworkVariable<int> _budget = new(
        INITIAL_BUDGET,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>現在の残予算（全クライアントで同一）。</summary>
    public int TeamBudget => _budget.Value;

    /// <summary>予算が変化したときに発火（新しい残予算を渡す）。</summary>
    public event System.Action<int> OnBudgetChanged;

    /// <summary>
    /// 購入結果が自分（このクライアント）に返ったときに発火する。
    /// GDD §8.5 の排他制御に従い、サーバー権威で承認/拒否された結果をローカル UI に通知する。
    /// (itemName, success, reason) — 失敗時は reason に「予算不足です」等が入る。
    /// </summary>
    public event System.Action<string, bool, string> OnPurchaseResultForLocal;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        _budget.OnValueChanged += HandleBudgetChanged;

        // ホストがセッション開始時に予算を初期化
        if (IsServer)
            _budget.Value = INITIAL_BUDGET;
    }

    public override void OnNetworkDespawn()
    {
        _budget.OnValueChanged -= HandleBudgetChanged;
    }

    private void HandleBudgetChanged(int _, int newVal)
    {
        OnBudgetChanged?.Invoke(newVal);
    }

    // ── 購入（ServerRpc） ────────────────────────────────────
    /// <summary>
    /// 購入コストをサーバーに送信する。
    /// サーバー側で残予算を確認し、足りなければ何もしない。
    /// 結果は NetworkVariable の変化（全クライアント更新）として反映される。
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DeductServerRpc(int amount)
    {
        if (amount <= 0) return;

        if (_budget.Value < amount)
        {
            Debug.Log($"[BudgetSync] 予算不足 — 要求:{amount}pt  残り:{_budget.Value}pt");
            return;
        }

        _budget.Value -= amount;
        Debug.Log($"[BudgetSync] 購入承認 -{amount}pt  残り:{_budget.Value}pt");
    }

    // ── 購入リクエスト（GDD §8.5 排他制御）─────────────────────
    /// <summary>
    /// GDD §8.5: 購入コストと商品名をサーバーに送信。
    /// サーバーは残予算を確認して成功なら予算を減らし、失敗なら元のクライアントに「予算不足です」を返す。
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RequestPurchaseServerRpc(string itemName, int cost, RpcParams rpc = default)
    {
        ulong sender = rpc.Receive.SenderClientId;

        if (string.IsNullOrEmpty(itemName) || cost <= 0)
        {
            SendPurchaseResultToClient(sender, itemName, false, "不正なリクエスト");
            return;
        }

        if (_budget.Value < cost)
        {
            SendPurchaseResultToClient(sender, itemName, false, "予算不足です");
            return;
        }

        _budget.Value -= cost;
        Debug.Log($"[BudgetSync] 購入承認 {itemName} -{cost}pt 残り:{_budget.Value}pt (requester={sender})");
        SendPurchaseResultToClient(sender, itemName, true, string.Empty);
    }

    private void SendPurchaseResultToClient(ulong clientId, string itemName, bool success, string reason)
    {
        var prms = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
        PurchaseResultClientRpc(itemName, success, reason, prms);
    }

    [ClientRpc]
    private void PurchaseResultClientRpc(string itemName, bool success, string reason, ClientRpcParams rpcParams = default)
    {
        OnPurchaseResultForLocal?.Invoke(itemName, success, reason);
    }

    // ── 返品（ServerRpc） ────────────────────────────────────
    /// <summary>
    /// 返品コストをサーバーに送信し、予算を加算する。
    /// 上限は INITIAL_BUDGET。
    /// </summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RefundServerRpc(int amount)
    {
        if (amount <= 0) return;

        _budget.Value = UnityEngine.Mathf.Min(INITIAL_BUDGET, _budget.Value + amount);
        Debug.Log($"[BudgetSync] 返品 +{amount}pt  残り:{_budget.Value}pt");
    }

    // ── オフライン用リセット（NGO 未使用テスト向け）──────────
    /// <summary>NetworkObject が未スポーンの場合のローカル初期化（テスト・オフライン用）。</summary>
    public void ResetLocal() { }  // NetworkVariable はスポーン後にのみ操作可能
}
