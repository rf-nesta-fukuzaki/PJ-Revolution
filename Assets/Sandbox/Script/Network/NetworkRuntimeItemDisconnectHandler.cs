using Unity.Netcode;
using UnityEngine;

/// <summary>
/// クライアント切断時に担架・ロープ接続など実行時アイテムの NGO 状態をサーバーで解放する。
/// </summary>
public static class NetworkRuntimeItemDisconnectHandler
{
    /// <summary>サーバー専用 — 切断クライアントを担架・ロープから外す。</summary>
    public static void NotifyClientDisconnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer) return;

        DetachFromAllStretchers(clientId);
        DetachShopRopeConnections(clientId);

        Debug.Log($"[NetItemDisconnect] client={clientId} の担架/ロープ接続を解放");
    }

    private static void DetachFromAllStretchers(ulong clientId)
    {
        foreach (var sync in Object.FindObjectsByType<NetworkStretcherSync>(FindObjectsSortMode.None))
        {
            if (sync == null || !sync.IsSpawned) continue;
            if (sync.IsCarrier(clientId))
                sync.ForceDetach(clientId);
        }
    }

    private static void DetachShopRopeConnections(ulong clientId)
    {
        foreach (var bridge in Object.FindObjectsByType<PlayerShopRopeNetworkBridge>(FindObjectsSortMode.None))
        {
            if (bridge == null || !bridge.IsSpawned) continue;
            if (bridge.OwnerClientId != clientId) continue;
            bridge.ServerForceDisconnectAllRopes();
        }
    }
}
