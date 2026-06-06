using System.Collections.Generic;
using PeakPlunder.Audio;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// アンカーボルト・ビバークテント・山中ドロップなどワールド配置の NGO 同期。
/// ServerRpc → ClientRpc で全クライアントに同じオブジェクトを生成する（Prefab 登録不要）。
/// </summary>
public class NetworkWorldPlacementsSync : NetworkBehaviour
{
    private const byte KindAnchor    = 1;
    private const byte KindBivouac   = 2;
    private const byte KindFieldDrop = 3;
    private const byte KindFlare     = 4;

    public static NetworkWorldPlacementsSync Instance { get; private set; }

    private static readonly HashSet<int> s_localPlacementKeys = new();

    private readonly NetworkVariable<bool> _bivouacPlacedThisExpedition = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[NetworkWorldPlacementsSync] 重複インスタンスを破棄します。");
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public override void OnNetworkDespawn()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    /// <summary>実行時にシングルトンを確保し、必要ならサーバーでスポーンする。</summary>
    public static NetworkWorldPlacementsSync EnsureExists()
    {
        if (Instance != null)
            return Instance;

        var existing = Object.FindFirstObjectByType<NetworkWorldPlacementsSync>();
        if (existing != null)
            return existing;

        var nm = NetworkManager.Singleton;
        if (nm != null && !nm.IsServer)
            return null;

        var go = new GameObject(nameof(NetworkWorldPlacementsSync));
        go.AddComponent<NetworkObject>();
        var sync = go.AddComponent<NetworkWorldPlacementsSync>();

        if (nm != null && nm.IsServer)
        {
            var netObj = go.GetComponent<NetworkObject>();
            if (netObj != null && !netObj.IsSpawned)
                netObj.Spawn(destroyWithScene: true);
        }

        return sync;
    }

    // ── アンカーボルト ─────────────────────────────────────────

    public void RequestPlaceAnchor(Vector3 position, Vector3 normal, int playerId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            SpawnAnchorLocal(position, normal, playerId);
            return;
        }

        if (IsServer)
            BroadcastAnchorClientRpc(position, normal, playerId);
        else
            RequestPlaceAnchorServerRpc(position, normal, playerId);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlaceAnchorServerRpc(Vector3 position, Vector3 normal, int playerId)
        => BroadcastAnchorClientRpc(position, normal, playerId);

    [ClientRpc]
    private void BroadcastAnchorClientRpc(Vector3 position, Vector3 normal, int playerId)
    {
        SpawnAnchorLocal(position, normal, playerId);
    }

    private static void SpawnAnchorLocal(Vector3 position, Vector3 normal, int playerId)
    {
        if (!TryBeginLocalPlacement(KindAnchor, position))
            return;

        WorldPlacementFactory.CreateAnchorBolt(position, normal);
        GameServices.Audio?.PlaySE(SoundId.AnchorBoltSet, position);
        GameServices.Score?.RecordRopePlacement(playerId);
    }

    // ── ビバークテント ─────────────────────────────────────────

    public bool RequestPlaceBivouac(Vector3 position, Quaternion rotation, float shelterRadius)
    {
        if (BivouacTentItem.IsPlacedThisExpedition)
            return false;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
            return SpawnBivouacLocal(position, rotation, shelterRadius);

        if (IsServer)
        {
            if (_bivouacPlacedThisExpedition.Value)
                return false;

            _bivouacPlacedThisExpedition.Value = true;
            BroadcastBivouacClientRpc(position, rotation, shelterRadius);
            return true;
        }

        RequestPlaceBivouacServerRpc(position, rotation, shelterRadius);
        return true;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestPlaceBivouacServerRpc(Vector3 position, Quaternion rotation, float shelterRadius)
    {
        if (_bivouacPlacedThisExpedition.Value)
            return;

        _bivouacPlacedThisExpedition.Value = true;
        BroadcastBivouacClientRpc(position, rotation, shelterRadius);
    }

    [ClientRpc]
    private void BroadcastBivouacClientRpc(Vector3 position, Quaternion rotation, float shelterRadius)
    {
        SpawnBivouacLocal(position, rotation, shelterRadius);
    }

    private static bool SpawnBivouacLocal(Vector3 position, Quaternion rotation, float shelterRadius)
    {
        if (!TryBeginLocalPlacement(KindBivouac, position))
            return false;

        WorldPlacementFactory.CreateBivouacTent(position, rotation, shelterRadius);
        BivouacTentItem.MarkPlacedThisExpedition();
        GameServices.Audio?.PlaySE(SoundId.TentSetup, position);
        return true;
    }

    // ── 山中ドロップ ───────────────────────────────────────────

    public void SpawnFieldDropAuthoritative(ShopItemType itemType, Vector3 position, Quaternion rotation, float durability)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            WorldPlacementFactory.CreateFieldDrop(itemType, position, rotation, durability);
            return;
        }

        if (!IsServer)
            return;

        BroadcastFieldDropClientRpc((byte)itemType, position, rotation, durability);
    }

    [ClientRpc]
    private void BroadcastFieldDropClientRpc(byte itemTypeByte, Vector3 position, Quaternion rotation, float durability)
    {
        var itemType = (ShopItemType)itemTypeByte;
        if (!TryBeginLocalPlacement(KindFieldDrop, position, itemType))
            return;

        WorldPlacementFactory.CreateFieldDrop(itemType, position, rotation, durability);
    }

    public void RequestSpawnFlare(Vector3 origin, Vector3 direction, float speed, float burnTime, float visibleRange)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening)
        {
            WorldPlacementFactory.CreateFlareProjectile(origin, direction, speed, burnTime, visibleRange);
            return;
        }

        if (IsServer)
            BroadcastFlareClientRpc(origin, direction, speed, burnTime, visibleRange);
        else
            RequestSpawnFlareServerRpc(origin, direction, speed, burnTime, visibleRange);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestSpawnFlareServerRpc(
        Vector3 origin, Vector3 direction, float speed, float burnTime, float visibleRange)
        => BroadcastFlareClientRpc(origin, direction, speed, burnTime, visibleRange);

    [ClientRpc]
    private void BroadcastFlareClientRpc(
        Vector3 origin, Vector3 direction, float speed, float burnTime, float visibleRange)
    {
        if (!TryBeginLocalPlacement(KindFlare, origin))
            return;

        WorldPlacementFactory.CreateFlareProjectile(origin, direction, speed, burnTime, visibleRange);
    }

    public static void ResetExpeditionState()
    {
        s_localPlacementKeys.Clear();
        BivouacTentItem.ResetExpeditionFlag();

        if (Instance != null && Instance.IsServer)
            Instance._bivouacPlacedThisExpedition.Value = false;
    }

    private static bool TryBeginLocalPlacement(byte kind, Vector3 position, ShopItemType itemType = ShopItemType.ShortRope10m)
    {
        int key = kind == KindFieldDrop
            ? WorldPlacementFactory.MakePlacementKey(itemType, position)
            : WorldPlacementFactory.MakePlacementKey(kind, position);

        if (!s_localPlacementKeys.Add(key))
            return false;

        return true;
    }
}
