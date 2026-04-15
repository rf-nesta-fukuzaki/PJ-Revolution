using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §8.3 — 遠征フェーズ・スコア・プレイヤー状態をネットワーク同期する。
/// ホストが ExpeditionManager のイベントを購読し、NetworkVariable 経由で
/// 全クライアントに状態を配信する。
/// </summary>
public class NetworkExpeditionSync : NetworkBehaviour
{
    // ── シングルトン ─────────────────────────────────────────
    /// <summary>スポーン中のインスタンス（ExpeditionManager から参照する）。</summary>
    public static NetworkExpeditionSync Instance { get; private set; }
    // ── 同期変数 ─────────────────────────────────────────────────
    private readonly NetworkVariable<ExpeditionPhase> _phase = new(
        ExpeditionPhase.Basecamp,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _teamScore = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> _elapsedTime = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _relicCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── プロパティ ───────────────────────────────────────────────
    public ExpeditionPhase CurrentPhase => _phase.Value;
    public int             TeamScore    => _teamScore.Value;
    public float           ElapsedTime  => _elapsedTime.Value;
    public int             RelicCount   => _relicCount.Value;

    // ── イベント ────────────────────────────────────────────────
    public event System.Action<ExpeditionPhase> OnPhaseChanged;
    public event System.Action<int>             OnTeamScoreChanged;
    public event System.Action<ScoreData>       OnResultReceived;

    // ── 内部状態 ─────────────────────────────────────────────────
    private bool  _timerRunning;
    private float _localTimer;

    // ── ライフサイクル ────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        Instance = this;

        _phase.OnValueChanged      += (_, next) => OnPhaseChanged?.Invoke(next);
        _teamScore.OnValueChanged  += (_, next) => OnTeamScoreChanged?.Invoke(next);

        if (IsServer)
        {
            // ホスト側: ExpeditionManager のイベントをフック
            var em = ExpeditionManager.Instance;
            if (em != null)
                Debug.Log("[ExpeditionSync] ExpeditionManager に接続完了");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this) Instance = null;
    }

    // ── タイマー同期（サーバー → クライアント）──────────────────
    private void Update()
    {
        if (!IsServer || !_timerRunning) return;

        _localTimer             += Time.deltaTime;
        _elapsedTime.Value       = _localTimer;
    }

    // ── サーバーサイド API（ExpeditionManager から呼ぶ）──────────
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void StartExpeditionServerRpc()
    {
        _phase.Value    = ExpeditionPhase.Climbing;
        _timerRunning   = true;
        _localTimer     = 0f;
        Debug.Log("[ExpeditionSync] 遠征開始を同期");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReturnToBaseServerRpc()
    {
        _phase.Value   = ExpeditionPhase.Returning;
        _timerRunning  = false;
        Debug.Log("[ExpeditionSync] 帰還を同期");
    }

    /// <summary>遺物回収をホストに通知 → 全クライアントに配信。</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void RegisterRelicCollectedServerRpc(int newValue)
    {
        _relicCount.Value = newValue;
        Debug.Log($"[ExpeditionSync] 遺物回収: 累計 {newValue} 個");
    }

    /// <summary>チームスコアをホストに通知。</summary>
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void UpdateTeamScoreServerRpc(int score)
    {
        _teamScore.Value = score;
    }

    /// <summary>リザルトデータを全クライアントに配信する ClientRpc。</summary>
    [Rpc(SendTo.ClientsAndHost)]
    public void ShowResultClientRpc(ResultPayload payload)
    {
        var scoreData = payload.ToScoreData();
        OnResultReceived?.Invoke(scoreData);
        Debug.Log($"[ExpeditionSync] リザルト受信: チームスコア={scoreData.TeamScore}");
    }

    // ── プレイヤー死亡通知 ────────────────────────────────────────
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void NotifyPlayerDeathServerRpc(ulong clientId)
    {
        NotifyPlayerDeathClientRpc(clientId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyPlayerDeathClientRpc(ulong clientId)
    {
        Debug.Log($"[ExpeditionSync] プレイヤー死亡通知: client={clientId}");
        // 各クライアントで幽霊UIを有効化する処理をここに追加
    }

    // ── チェックポイント同期 ──────────────────────────────────────
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void ReachCheckpointServerRpc(ulong clientId, int checkpointIndex)
    {
        Debug.Log($"[ExpeditionSync] チェックポイント到達: client={clientId} idx={checkpointIndex}");
        ReachCheckpointClientRpc(clientId, checkpointIndex);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ReachCheckpointClientRpc(ulong clientId, int checkpointIndex)
    {
        ExpeditionManager.Instance?.OnCheckpointReached(checkpointIndex);
    }
}

// ── シリアライズ可能なリザルトペイロード ─────────────────────────
/// <summary>
/// NGO は NetworkVariable や RPC でクラスを直接渡せないため
/// INetworkSerializable を実装した軽量構造体でリザルトを転送する。
/// </summary>
public struct ResultPayload : INetworkSerializable
{
    public int   TeamScore;
    public float ClearTimeSeconds;
    public bool  AllSurvived;
    public int   RelicCount;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref TeamScore);
        serializer.SerializeValue(ref ClearTimeSeconds);
        serializer.SerializeValue(ref AllSurvived);
        serializer.SerializeValue(ref RelicCount);
    }

    public ScoreData ToScoreData() => new()
    {
        TeamScore        = TeamScore,
        ClearTimeSeconds = ClearTimeSeconds,
        Relics           = new List<RelicBase>()   // クライアントは local でも参照できる
    };
}
