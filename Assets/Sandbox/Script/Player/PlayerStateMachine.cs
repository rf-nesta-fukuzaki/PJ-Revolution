using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// プレイヤーの上位ステートマシン（NetworkBehaviour）。
///
/// 全クライアントにステートを同期し、各コンポーネントがステート遷移を
/// このクラスに委譲することで boolean フラグの散在を防ぐ。
///
/// 利用パターン:
///   // 遷移要求（どのクライアントからも可）
///   _stateMachine.Transition(PlayerState.Ghost);
///
///   // 状態参照
///   if (_stateMachine.IsGhost) { ... }
///
///   // 変化購読
///   _stateMachine.OnStateChanged += (prev, next) => { ... };
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PlayerStateMachine : NetworkBehaviour
{
    // ── ネットワーク同期ステート（Server が書き込み権限を持つ）──
    private readonly NetworkVariable<PlayerState> _state = new(
        PlayerState.Alive,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── イベント（ローカル購読用） ────────────────────────────
    /// <summary>ステートが変化した際に発火。引数: (前ステート, 次ステート)</summary>
    public event Action<PlayerState, PlayerState> OnStateChanged;

    // ── プロパティ ───────────────────────────────────────────
    public PlayerState Current  => _state.Value;
    public bool IsAlive    => Current == PlayerState.Alive;
    public bool IsGhost    => Current == PlayerState.Ghost;
    public bool IsRagdoll  => Current == PlayerState.Ragdoll;
    public bool IsEmoting  => Current == PlayerState.Emoting;
    public bool IsBoarding => Current == PlayerState.Boarding;

    // ── ライフサイクル ────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        _state.OnValueChanged += HandleNetworkStateChanged;
    }

    public override void OnNetworkDespawn()
    {
        _state.OnValueChanged -= HandleNetworkStateChanged;
    }

    private void HandleNetworkStateChanged(PlayerState prev, PlayerState next)
    {
        OnStateChanged?.Invoke(prev, next);
    }

    // ── 遷移要求 ─────────────────────────────────────────────
    /// <summary>
    /// ステート遷移を要求する。
    /// 無効な遷移はエラーログ後に無視される（フェイルファスト）。
    /// </summary>
    public void Transition(PlayerState next)
    {
        if (Current == next) return;

        if (!IsValidTransition(Current, next))
        {
            Debug.LogError(
                $"[PlayerStateMachine] 無効な遷移: {Current} → {next}  " +
                $"(object: {gameObject.name})");
            return;
        }

        if (IsServer)
            _state.Value = next;
        else
            RequestTransitionServerRpc(next);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestTransitionServerRpc(PlayerState next)
    {
        if (!IsValidTransition(_state.Value, next))
        {
            Debug.LogWarning(
                $"[PlayerStateMachine] サーバーで無効な遷移を拒否: {_state.Value} → {next}");
            return;
        }
        _state.Value = next;
    }

    // ── 遷移ルール（ここに全ての合法遷移を列挙）─────────────
    /// <summary>
    /// 合法な遷移を宣言的に定義する。
    /// 新しい遷移が必要な場合はここに追加するだけでよい。
    /// </summary>
    public static bool IsValidTransition(PlayerState from, PlayerState to) =>
        (from, to) switch
        {
            // Alive から各ステートへ
            (PlayerState.Alive,    PlayerState.Ghost)    => true,
            (PlayerState.Alive,    PlayerState.Ragdoll)  => true,
            (PlayerState.Alive,    PlayerState.Emoting)  => true,
            (PlayerState.Alive,    PlayerState.Boarding) => true,

            // 各ステートから Alive へ（復帰）
            (PlayerState.Ghost,    PlayerState.Alive)    => true,   // 祠で復活
            (PlayerState.Ragdoll,  PlayerState.Alive)    => true,   // 3秒後に復帰
            (PlayerState.Emoting,  PlayerState.Alive)    => true,   // エモート終了
            (PlayerState.Boarding, PlayerState.Alive)    => true,   // 降機

            // エッジケース（エモート中に死亡）
            (PlayerState.Emoting,  PlayerState.Ghost)    => true,

            _ => false,
        };
}
