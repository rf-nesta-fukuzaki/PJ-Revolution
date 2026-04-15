using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

/// <summary>
/// GDD §8.3 — 遺物の物理をホスト権威で同期する NetworkBehaviour。
/// RelicBase に AddComponent して使用する。
///
/// ホスト権威方式:
///   - 物理演算はホストのみで実行
///   - HP・ダメージ状態は NetworkVariable で全クライアントに配信
///   - 位置は NetworkTransform（ServerAuthority）が担当
/// </summary>
[RequireComponent(typeof(RelicBase))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NetworkRigidbody))]
public class NetworkRelicSync : NetworkBehaviour
{
    // ── 同期変数 ─────────────────────────────────────────────────
    /// <summary>遺物の現在HP（0-100）。ホストが書き込み、全員が読める。</summary>
    private readonly NetworkVariable<float> _networkHp = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>遺物の状態区分。ホストが書き込み。</summary>
    private readonly NetworkVariable<RelicCondition> _networkCondition = new(
        RelicCondition.Perfect,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    /// <summary>誰かが持っているか。持っているクライアントIDを保持（未保持は ulong.MaxValue）。</summary>
    private readonly NetworkVariable<ulong> _heldByClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── 参照 ─────────────────────────────────────────────────────
    private RelicBase        _relic;
    private NetworkTransform _networkTransform;
    private NetworkRigidbody _networkRigidbody;

    // ── プロパティ ───────────────────────────────────────────────
    public float          NetworkHp        => _networkHp.Value;
    public RelicCondition NetworkCondition => _networkCondition.Value;
    public bool           IsHeldByNetwork  => _heldByClientId.Value != ulong.MaxValue;

    // ── ライフサイクル ────────────────────────────────────────────
    private void Awake()
    {
        _relic            = GetComponent<RelicBase>();
        _networkTransform = GetComponent<NetworkTransform>();
        _networkRigidbody = GetComponent<NetworkRigidbody>();
    }

    public override void OnNetworkSpawn()
    {
        // ネットワークHP変化をローカル遺物HPに反映（クライアント側 UI 更新など）
        _networkHp.OnValueChanged += OnNetworkHpChanged;
        _networkCondition.OnValueChanged += OnNetworkConditionChanged;

        if (IsServer)
        {
            // ホストではローカルの衝突ダメージをネットワーク変数に書き込む
            _relic.OnDamaged += HandleDamageOnServer;
        }
        else
        {
            // クライアントでは物理演算を無効化してホストに任せる
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
        }
    }

    public override void OnNetworkDespawn()
    {
        _networkHp.OnValueChanged        -= OnNetworkHpChanged;
        _networkCondition.OnValueChanged -= OnNetworkConditionChanged;

        if (IsServer)
            _relic.OnDamaged -= HandleDamageOnServer;
    }

    // ── ダメージ同期（サーバー側）────────────────────────────────
    private void HandleDamageOnServer(float damage, float currentHp)
    {
        _networkHp.Value        = currentHp;
        _networkCondition.Value = _relic.Condition;
    }

    // ── 持ち上げ同期 ─────────────────────────────────────────────
    /// <summary>クライアントがアイテムを持ち上げた際にサーバーへ通知する。</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PickupServerRpc(ulong clientId, RpcParams rpcParams = default)
    {
        if (IsHeldByNetwork) return;  // 既に誰かが持っている

        _heldByClientId.Value = clientId;
        // 物理を持ち手に追従させる（位置同期は NetworkTransform が行う）
        Debug.Log($"[RelicSync] {_relic.RelicName} 持ち上げ: client={clientId}");
    }

    /// <summary>クライアントがアイテムを置いた際にサーバーへ通知する。</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PutDownServerRpc(RpcParams rpcParams = default)
    {
        _heldByClientId.Value = ulong.MaxValue;

        // 置いた後は物理を有効化
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        Debug.Log($"[RelicSync] {_relic.RelicName} 置き直し");
    }

    /// <summary>ホスト側から直接ダメージを加える（落石トリガーなどから呼び出し）。</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ApplyDamageServerRpc(float damage, RpcParams rpcParams = default)
    {
        if (!IsServer) return;
        _relic.ApplyDamage(damage);
    }

    // ── 値変化コールバック（クライアント側UI/演出更新）────────────
    private void OnNetworkHpChanged(float oldHp, float newHp)
    {
        // クライアント側でのHP変化エフェクト（Coroutine等はここに追加）
        if (newHp < oldHp)
            Debug.Log($"[RelicSync] {_relic.RelicName} HP: {oldHp:F0} → {newHp:F0}");
    }

    private void OnNetworkConditionChanged(RelicCondition oldCond, RelicCondition newCond)
    {
        if (newCond == RelicCondition.Destroyed)
        {
            Debug.Log($"[RelicSync] {_relic.RelicName} 破壊！");
        }
    }

    // ── デバッグ ─────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = IsHeldByNetwork ? Color.yellow : Color.white;
        Gizmos.DrawWireSphere(transform.position, 0.4f);
    }
}
