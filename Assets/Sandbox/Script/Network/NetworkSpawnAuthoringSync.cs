using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// SpawnManager のサーバー権威結果（ルート開閉・ハザード配置）を全クライアントへ配信する。
/// 遺物は NetworkObject.Spawn で自動複製されるため、ここではルートとハザードのみ扱う。
/// </summary>
public class NetworkSpawnAuthoringSync : NetworkBehaviour
{
    public static NetworkSpawnAuthoringSync Instance { get; private set; }

    private readonly List<RouteStatePayload>  _pendingRoutes  = new();
    private readonly List<SpawnStatePayload>  _pendingHazards = new();

    public override void OnNetworkSpawn()
    {
        if (Instance != null && Instance != this)
        {
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

    public static NetworkSpawnAuthoringSync EnsureExists()
    {
        if (Instance != null)
            return Instance;

        var existing = Object.FindFirstObjectByType<NetworkSpawnAuthoringSync>();
        if (existing != null)
            return existing;

        var nm = NetworkManager.Singleton;
        if (nm != null && !nm.IsServer)
            return null;

        var go = new GameObject(nameof(NetworkSpawnAuthoringSync));
        go.AddComponent<NetworkObject>();
        var sync = go.AddComponent<NetworkSpawnAuthoringSync>();

        if (nm != null && nm.IsServer)
        {
            var netObj = go.GetComponent<NetworkObject>();
            if (!netObj.IsSpawned)
                netObj.Spawn(destroyWithScene: true);
        }

        return sync;
    }

    public void BeginBatch()
    {
        _pendingRoutes.Clear();
        _pendingHazards.Clear();
    }

    public void RecordRoute(Vector3 position, bool isOpen)
        => _pendingRoutes.Add(new RouteStatePayload { Position = position, IsOpen = isOpen });

    public void RecordHazard(Vector3 position, int prefabIndex)
        => _pendingHazards.Add(new SpawnStatePayload { Position = position, PrefabIndex = prefabIndex });

    public void FlushToClients()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            ApplyRoutesLocal(_pendingRoutes.ToArray());
            ApplyHazardsLocal(_pendingHazards.ToArray());
            return;
        }

        if (!IsServer)
            return;

        ApplyRoutesClientRpc(_pendingRoutes.ToArray());
        ApplyHazardsClientRpc(_pendingHazards.ToArray());
    }

    [ClientRpc]
    private void ApplyRoutesClientRpc(RouteStatePayload[] routes)
        => ApplyRoutesLocal(routes);

    [ClientRpc]
    private void ApplyHazardsClientRpc(SpawnStatePayload[] hazards)
        => ApplyHazardsLocal(hazards);

    private static void ApplyRoutesLocal(RouteStatePayload[] routes)
    {
        if (routes == null) return;

        foreach (var route in routes)
        {
            var gate = FindRouteGateNear(route.Position);
            gate?.SetOpen(route.IsOpen);
        }
    }

    private static void ApplyHazardsLocal(SpawnStatePayload[] hazards)
    {
        if (hazards == null) return;

        foreach (var hazard in hazards)
        {
            var sp = FindSpawnPointNear(hazard.Position, SpawnLayer.Hazard);
            if (sp == null || sp.IsActive) continue;
            sp.Activate(hazard.PrefabIndex);
        }
    }

    private static RouteGate FindRouteGateNear(Vector3 position)
    {
        const float tolerance = 1.5f;
        float tolSqr = tolerance * tolerance;

        foreach (var gate in Object.FindObjectsByType<RouteGate>(FindObjectsSortMode.None))
        {
            if (gate == null) continue;
            if ((gate.transform.position - position).sqrMagnitude <= tolSqr)
                return gate;
        }

        return null;
    }

    private static SpawnPoint FindSpawnPointNear(Vector3 position, SpawnLayer layer)
    {
        const float tolerance = 1.5f;
        float tolSqr = tolerance * tolerance;

        foreach (var sp in Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None))
        {
            if (sp == null || sp.Layer != layer) continue;
            if ((sp.transform.position - position).sqrMagnitude <= tolSqr)
                return sp;
        }

        return null;
    }
}

public struct RouteStatePayload : INetworkSerializable
{
    public Vector3 Position;
    public bool    IsOpen;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref IsOpen);
    }
}

public struct SpawnStatePayload : INetworkSerializable
{
    public Vector3 Position;
    public int     PrefabIndex;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Position);
        serializer.SerializeValue(ref PrefabIndex);
    }
}
