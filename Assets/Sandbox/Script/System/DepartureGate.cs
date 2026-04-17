using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §2.2 — 出発ゲート（ベースキャンプ出発条件管理）。
/// BoxCollider (isTrigger=true, 8m×4m×4m), Tag: DepartureGate。
/// 全プレイヤーがエリア内に入った状態でホストが E キーを押すと
/// ExpeditionManager.StartExpedition() を呼び遠征を開始する。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DepartureGate : MonoBehaviour
{
    // ── GDD パラメータ ────────────────────────────────────────
    private static readonly Vector3 GATE_SIZE = new(8f, 4f, 4f);

    [Header("UI フィードバック")]
    [SerializeField] private GameObject _readyIndicatorPrefab;  // 全員準備完了のビジュアル（任意）

    // ── 状態 ─────────────────────────────────────────────────
    private readonly HashSet<int> _playersInZone = new();
    private bool _departed;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        gameObject.tag = "DepartureGate";

        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = GATE_SIZE;
        col.center = new Vector3(0f, GATE_SIZE.y * 0.5f, 0f);
    }

    private void Update()
    {
        if (_departed) return;

        // ホストのみ出発操作を受け付ける（ホスト権威）
        bool isHost = !NetworkManager.Singleton || NetworkManager.Singleton.IsHost;
        if (!isHost) return;

        if (!InputStateReader.InteractPressedThisFrame()) return;
        TryDepart();
    }

    // ── トリガー判定 ─────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponentInParent<PlayerHealthSystem>();
        if (health == null || health.IsDead) return;

        _playersInZone.Add(health.GetInstanceID());
        Debug.Log($"[DepartureGate] {other.name} がゲート内に入った " +
                  $"({_playersInZone.Count}/{GetTotalPlayerCount()})");
    }

    private void OnTriggerExit(Collider other)
    {
        var health = other.GetComponentInParent<PlayerHealthSystem>();
        if (health == null) return;

        _playersInZone.Remove(health.GetInstanceID());
        Debug.Log($"[DepartureGate] {other.name} がゲートから出た " +
                  $"({_playersInZone.Count}/{GetTotalPlayerCount()})");
    }

    // ── 出発試行 ─────────────────────────────────────────────
    private void TryDepart()
    {
        // ゲートの近く（5m以内）でのみ反応
        if (NetworkManager.Singleton && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            float dist = Vector3.Distance(
                NetworkManager.Singleton.LocalClient.PlayerObject.transform.position,
                transform.position);
            if (dist > 5f) return;
        }

        int total = GetTotalPlayerCount();
        if (total <= 0)
        {
            // シングルプレイ or テスト環境では即時出発
            Depart();
            return;
        }

        if (_playersInZone.Count < total)
        {
            Debug.Log($"[DepartureGate] 出発不可: {_playersInZone.Count}/{total} 人しかゲート内にいません");
            return;
        }

        Depart();
    }

    private void Depart()
    {
        if (_departed) return;
        _departed = true;

        Debug.Log("[DepartureGate] 全員集合！出発します！");
        _readyIndicatorPrefab?.SetActive(false);

        GameServices.Expedition?.StartExpedition();
    }

    // ── ヘルパー ─────────────────────────────────────────────
    private static int GetTotalPlayerCount()
    {
        if (NetworkManager.Singleton && NetworkManager.Singleton.IsListening)
            return NetworkManager.Singleton.ConnectedClients.Count;

        // シングルプレイ / 未ネットワーク環境
        return PlayerHealthSystem.RegisteredPlayers?.Count ?? 0;
    }

    public bool AllPlayersReady =>
        _playersInZone.Count > 0 &&
        _playersInZone.Count >= GetTotalPlayerCount();

    // ── Gizmos ───────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = AllPlayersReady ? Color.green : new Color(1f, 0.8f, 0f, 0.5f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(new Vector3(0f, GATE_SIZE.y * 0.5f, 0f), GATE_SIZE);
    }
}
