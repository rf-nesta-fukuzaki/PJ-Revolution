using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// GDD §8.3 — プレイヤーの接続時スポーンを管理する NetworkBehaviour。
/// ホスト権威: ホストがスポーン位置を決定し全クライアントに通知する。
///
/// 【NGO自動スポーン対応】
/// NetworkManager の PlayerPrefab が設定されている場合、NGO は StartHost() 時に
/// (0,0,0) へプレイヤーを自動スポーンする。このまま放置すると地面(Y=0.25)より
/// カプセル底面が 1.15m 埋まり、物理デペネトレーションが失敗して地面を抜ける。
/// SpawnPlayerForClient() は既存の自動スポーン済みプレイヤーを検出した場合、
/// 新規生成せず正しい位置へテレポートして再利用する。
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
        var spawnPos = GetSpawnPosition((int)_spawnedPlayers.Count);

        // NGO の PlayerPrefab 自動スポーンが先に (0,0,0) へ生成済みか確認
        var existingPlayer = NetworkManager.Singleton.SpawnManager?.GetPlayerNetworkObject(clientId);
        if (existingPlayer != null)
        {
            // 既存オブジェクトを正しい位置へ即時テレポート（物理フレーム前に実行）
            TeleportToSpawnPoint(existingPlayer, spawnPos);
            _spawnedPlayers[clientId] = existingPlayer;

            GameServices.Score?.RegisterPlayer((int)clientId, $"Player {clientId}");
            Debug.Log($"[Spawner] NGO自動スポーン済みプレイヤーを移動: client={clientId}  pos={spawnPos}");
            return;
        }

        // 自動スポーンなし → 手動で生成
        if (_playerPrefab == null)
        {
            Debug.LogError("[Spawner] PlayerPrefab が未設定です");
            return;
        }

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

        // IScoreService にプレイヤーを登録（GameServices 経由でシングルトン直結を回避）
        GameServices.Score?.RegisterPlayer((int)clientId, $"Player {clientId}");

        Debug.Log($"[Spawner] プレイヤースポーン: client={clientId}  pos={spawnPos}");
    }

    /// <summary>
    /// Rigidbody の物理位置を直接更新してスポーン位置へ瞬時に移動する。
    /// AutoSyncTransforms = false 環境で transform.position だけを変えても
    /// 物理エンジンが追従しないため、rb.position を使って直接セットする。
    /// Physics.SyncTransforms() で transform との同期も保証する。
    /// </summary>
    private static void TeleportToSpawnPoint(NetworkObject netObj, Vector3 pos)
    {
        var rb = netObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position        = pos;
            rb.linearVelocity  = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            netObj.transform.position = pos;
        }

        // Physics と Transform の同期を強制
        Physics.SyncTransforms();
    }

    private Vector3 GetSpawnPosition(int index)
    {
        if (_spawnPoints != null && index < _spawnPoints.Length)
            return _spawnPoints[index].position;

        // スポーンポイント未設定時: X方向にずらし、地面(Y=0.25)上に十分な余裕を持たせる
        // カプセル半高(0.9) + 地面天面(0.25) + 余裕(0.85) = 2.0
        return new Vector3(index * 2f, 2.0f, 0f);
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
