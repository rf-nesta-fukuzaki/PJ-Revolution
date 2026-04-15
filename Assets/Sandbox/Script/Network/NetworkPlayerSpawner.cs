using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §8.3 — プレイヤーの接続時スポーンを管理する NetworkBehaviour。
/// ホスト権威: ホストがスポーン位置を決定し全クライアントに通知する。
/// NetworkManager の PlayerPrefab の代わりに手動スポーンを使用することで
/// チームメイトのキャラクター参照をローカルに保持できる。
/// </summary>
public class NetworkPlayerSpawner : NetworkBehaviour
{
    [Header("プレイヤー設定")]
    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private Transform[] _spawnPoints;

    // クライアントID → 生成プレイヤーの対応表（ホストのみが持つ）
    private readonly Dictionary<ulong, NetworkObject> _spawnedPlayers = new();

    // ── Server-side ─────────────────────────────────────────────
    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback    += HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback   += HandleClientDisconnected;

        // ホスト自身をスポーン
        SpawnPlayerForClient(NetworkManager.Singleton.LocalClientId);
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        NetworkManager.Singleton.OnClientConnectedCallback    -= HandleClientConnected;
        NetworkManager.Singleton.OnClientDisconnectCallback   -= HandleClientDisconnected;
    }

    // ── 接続ハンドラ ─────────────────────────────────────────────
    private void HandleClientConnected(ulong clientId)
    {
        // ホスト自身は OnNetworkSpawn で処理済み
        if (clientId == NetworkManager.Singleton.LocalClientId) return;
        SpawnPlayerForClient(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!_spawnedPlayers.TryGetValue(clientId, out var netObj)) return;

        if (netObj != null)
            netObj.Despawn(true);

        _spawnedPlayers.Remove(clientId);
        Debug.Log($"[Spawner] プレイヤー切断 → 破棄: client={clientId}");
    }

    // ── スポーン ─────────────────────────────────────────────────
    private void SpawnPlayerForClient(ulong clientId)
    {
        if (_playerPrefab == null)
        {
            Debug.LogError("[Spawner] PlayerPrefab が未設定です");
            return;
        }

        var spawnPos = GetSpawnPosition((int)_spawnedPlayers.Count);
        var instance = Instantiate(_playerPrefab, spawnPos, Quaternion.identity);
        var netObj   = instance.GetComponent<NetworkObject>();

        if (netObj == null)
        {
            Debug.LogError("[Spawner] PlayerPrefab に NetworkObject がありません");
            Destroy(instance);
            return;
        }

        netObj.SpawnAsPlayerObject(clientId, true);
        _spawnedPlayers[clientId] = netObj;

        // ScoreTracker にプレイヤーを登録（IDは clientId を int にキャスト）
        ScoreTracker.Instance?.RegisterPlayer((int)clientId, $"Player {clientId}");

        Debug.Log($"[Spawner] プレイヤースポーン: client={clientId}  pos={spawnPos}");
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (_spawnPoints != null && index < _spawnPoints.Length)
            return _spawnPoints[index].position;

        // スポーンポイント未設定時はランダムオフセット
        return Vector3.right * (index * 2f);
    }

    // ── クライアント照会 ─────────────────────────────────────────
    /// <summary>指定クライアントの NetworkObject を返す（ホスト専用）。</summary>
    public NetworkObject GetPlayerObject(ulong clientId)
    {
        _spawnedPlayers.TryGetValue(clientId, out var netObj);
        return netObj;
    }

    public IReadOnlyDictionary<ulong, NetworkObject> AllPlayers => _spawnedPlayers;
}
