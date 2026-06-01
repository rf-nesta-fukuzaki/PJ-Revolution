using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// オンラインマルチ（最大4人）のパーティ権威管理。
/// 不足スロットは NPC で補充。クライアント接続=後入り（NPC→人間）、切断=後抜け（人間→NPC）。
/// </summary>
[DefaultExecutionOrder(40)]
public sealed class NetworkPartyManager : NetworkBehaviour
{
    public const ulong NoClient = ulong.MaxValue;

    private static NetworkPartyManager _instance;

    [Header("参照")]
    [SerializeField] private NetworkPlayerSpawner _playerSpawner;

    private readonly NetworkVariable<int> _humanCountNet = new(
        1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly Dictionary<ulong, NetworkObject> _clientPlayers = new();
    private readonly List<ulong> _pendingJoinClients = new();
    private bool _partyReady;
    private bool _handlersBound;

    public static NetworkPartyManager Instance => _instance;
    public bool IsPartyReady => _partyReady;
    public int HumanCount => _humanCountNet.Value;

    public static bool ShouldManageParty =>
        LocalCoopSettings.IsActive && LocalCoopSettings.IsOnline;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("[NetworkParty] 重複した NetworkPartyManager を無効化します。");
            enabled = false;
            return;
        }

        _instance = this;
        if (_playerSpawner == null)
            _playerSpawner = GetComponent<NetworkPlayerSpawner>();
    }

    public override void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        if (!ShouldManageParty) return;

        BindConnectionHandlers();
        StartCoroutine(ServerInitializePartyCoroutine());
    }

    public override void OnNetworkDespawn()
    {
        UnbindConnectionHandlers();

        // セッション切替・再起動に備えて状態をリセットする。
        _partyReady = false;
        _clientPlayers.Clear();
        _pendingJoinClients.Clear();
    }

    public void BindConnectionHandlers()
    {
        if (_handlersBound || !IsServer) return;
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnClientConnectedCallback += HandleClientConnected;
        nm.OnClientDisconnectCallback += HandleClientDisconnected;
        _handlersBound = true;
    }

    public void UnbindConnectionHandlers()
    {
        if (!_handlersBound) return;
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        nm.OnClientConnectedCallback -= HandleClientConnected;
        nm.OnClientDisconnectCallback -= HandleClientDisconnected;
        _handlersBound = false;
    }

    private IEnumerator ServerInitializePartyCoroutine()
    {
        const float timeout = 20f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer && nm.LocalClient?.PlayerObject != null)
                break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_partyReady) yield break;

        var hostObject = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (hostObject == null)
        {
            Debug.LogError("[NetworkParty] ホストプレイヤーが見つかりません。");
            yield break;
        }

        ServerAssignHostSlot(hostObject);
        ServerFillNpcSlots();

        _partyReady = true;
        SyncHumanCount();
        SandboxLocalCoopBootstrap.Instance?.RefreshPresentation();
        FlushPendingJoins();
        Debug.Log("[NetworkParty] オンラインパーティ初期化完了（ホスト + NPC 補充）");
    }

    private void FlushPendingJoins()
    {
        if (_pendingJoinClients.Count == 0) return;
        var copy = new List<ulong>(_pendingJoinClients);
        _pendingJoinClients.Clear();
        foreach (ulong clientId in copy)
            ServerPromoteClientToParty(clientId);
    }

    private void HandleClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        var nm = NetworkManager.Singleton;
        if (nm != null && clientId == nm.LocalClientId) return;

        if (!_partyReady)
        {
            if (!_pendingJoinClients.Contains(clientId))
                _pendingJoinClients.Add(clientId);
            return;
        }

        ServerPromoteClientToParty(clientId);
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (!IsServer || !_partyReady) return;
        var nm = NetworkManager.Singleton;
        if (nm != null && clientId == nm.LocalClientId) return;

        ServerDemoteClientFromParty(clientId);
    }

    private void ServerAssignHostSlot(NetworkObject hostPlayer)
    {
        var bootstrap = SandboxLocalCoopBootstrap.Instance;
        if (bootstrap == null) return;

        ulong hostId = NetworkManager.Singleton.LocalClientId;
        Vector3 pos = ResolveSpawnPosition(0, hostPlayer.transform.position);
        TeleportPlayer(hostPlayer, pos);

        bootstrap.ConfigureOnlineHuman(hostPlayer.gameObject, 0, hostId, $"Player 1");
        _clientPlayers[hostId] = hostPlayer;
    }

    private void ServerFillNpcSlots()
    {
        var bootstrap = SandboxLocalCoopBootstrap.Instance;
        if (bootstrap == null) return;

        for (int slot = 1; slot < LocalCoopSettings.MaxPartySize; slot++)
            bootstrap.SpawnNetworkNpcAt(slot, ResolveSpawnPosition(slot, Vector3.zero));
    }

    private void ServerPromoteClientToParty(ulong clientId)
    {
        var roster = LocalCoopRoster.Instance;
        var bootstrap = SandboxLocalCoopBootstrap.Instance;
        if (roster == null || bootstrap == null || _playerSpawner == null) return;

        int slot = roster.FindFirstNpcSlot();
        if (slot < 0)
        {
            Debug.LogWarning($"[NetworkParty] 後入り失敗: 空き NPC スロットなし (client={clientId})");
            return;
        }

        var npcMember = roster.GetSlot(slot);
        Vector3 pos = npcMember != null ? npcMember.transform.position : ResolveSpawnPosition(slot, Vector3.zero);
        Quaternion rot = npcMember != null ? npcMember.transform.rotation : Quaternion.identity;

        bootstrap.DespawnPartyActor(npcMember?.gameObject);

        NetworkObject playerObject = SpawnPlayerForClientAt(clientId, slot, pos, rot);
        if (playerObject == null) return;

        string displayName = $"Player {slot + 1}";
        bootstrap.ConfigureOnlineHuman(playerObject.gameObject, slot, clientId, displayName);
        _clientPlayers[clientId] = playerObject;

        SyncHumanCount();
        NotifyPartyChangedClientRpc();
        Debug.Log($"[NetworkParty] 後入り: client={clientId} → スロット {slot}");
    }

    private void ServerDemoteClientFromParty(ulong clientId)
    {
        var roster = LocalCoopRoster.Instance;
        var bootstrap = SandboxLocalCoopBootstrap.Instance;
        if (roster == null || bootstrap == null) return;

        int slot = roster.FindSlotByClientId(clientId);
        if (slot < 0)
        {
            _clientPlayers.Remove(clientId);
            return;
        }

        if (slot == 0)
        {
            Debug.LogWarning("[NetworkParty] ホストスロットの後抜けは未対応（セッション終了を推奨）");
            return;
        }

        Vector3 pos = Vector3.zero;
        if (_clientPlayers.TryGetValue(clientId, out var netObj) && netObj != null)
            pos = netObj.transform.position;
        else
        {
            var member = roster.GetSlot(slot);
            if (member != null) pos = member.transform.position;
        }

        DespawnClientPlayer(clientId);

        bootstrap.SpawnNetworkNpcAt(slot, pos);

        SyncHumanCount();
        NotifyPartyChangedClientRpc();
        Debug.Log($"[NetworkParty] 後抜け: client={clientId} ← スロット {slot} を NPC 化");
    }

    private NetworkObject SpawnPlayerForClientAt(ulong clientId, int slot, Vector3 pos, Quaternion rot)
    {
        var nm = NetworkManager.Singleton;
        var existing = nm.SpawnManager?.GetPlayerNetworkObject(clientId);
        if (existing != null)
        {
            TeleportPlayer(existing, pos);
            existing.transform.rotation = rot;
            return existing;
        }

        GameObject prefab = _playerSpawner.PlayerPrefab;
        if (prefab == null)
        {
            Debug.LogError("[NetworkParty] PlayerPrefab 未設定");
            return null;
        }

        var instance = Instantiate(prefab, pos, rot);
        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Destroy(instance);
            return null;
        }

        netObj.SpawnAsPlayerObject(clientId, true);
        GameServices.Score?.RegisterPlayer(slot, $"Player {slot + 1}");
        return netObj;
    }

    private void DespawnClientPlayer(ulong clientId)
    {
        if (!_clientPlayers.TryGetValue(clientId, out var netObj))
            return;

        if (netObj != null && netObj.IsSpawned)
            netObj.Despawn(true);

        _clientPlayers.Remove(clientId);
    }

    private Vector3 ResolveSpawnPosition(int slot, Vector3 fallback)
    {
        if (_playerSpawner != null)
            return _playerSpawner.GetSpawnPositionForIndex(slot);
        return fallback.sqrMagnitude > 0.01f ? fallback : new Vector3(slot * 2.5f, 2f, 0f);
    }

    private static void TeleportPlayer(NetworkObject netObj, Vector3 pos)
    {
        var rb = netObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = pos;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            netObj.transform.position = pos;
        }

        Physics.SyncTransforms();
    }

    private void SyncHumanCount()
    {
        int count = LocalCoopRoster.Instance?.HumanCount ?? 1;
        _humanCountNet.Value = count;
        LocalCoopSettings.HumanCount = count;
    }

    [ClientRpc]
    private void NotifyPartyChangedClientRpc()
    {
        LocalCoopSettings.HumanCount = _humanCountNet.Value;
        SandboxLocalCoopBootstrap.Instance?.RefreshPresentation();
    }
}
