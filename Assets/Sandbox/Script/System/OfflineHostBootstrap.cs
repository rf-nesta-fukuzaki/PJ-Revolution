using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// OfflineTestScene 用 NGO Host 起動ロジック。
/// </summary>
public sealed class OfflineHostBootstrap
{
    public bool IsInitialized { get; private set; }

    public IEnumerator StartHost(string offlinePlayerName)
    {
        for (int i = 0; i < 10; i++)
        {
            if (NetworkManager.Singleton != null) break;
            yield return null;
        }

        var networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("[OfflineBoot] NetworkManager が見つかりません。");
            yield break;
        }

        var transport = networkManager.GetComponent<UnityTransport>();
        if (transport != null)
        {
            ushort port = FindAvailableUdpPort(7777);
            transport.SetConnectionData("127.0.0.1", port);
            Debug.Log($"[OfflineBoot] UDP ポート {port} を使用します。");
        }

        int trimmed = TrimDuplicateNetworkRelicClones();
        if (trimmed > 0)
        {
            Debug.Log($"[OfflineBoot] Host 起動前に重複遺物クローンを {trimmed} 個削除しました。");
            yield return null;
        }

        if (!networkManager.StartHost())
        {
            Debug.LogError("[OfflineBoot] StartHost() が失敗しました。");
            yield break;
        }

        yield return null;

        // 個人スコアは正準ID（プレイヤー root の InstanceID）で統一する。NetworkPlayerSpawner も
        // 同じ FromRoot で登録するため、同一キーで重複無視され（先に登録した名前=offlinePlayerName が残る）、
        // 旧来の固定 id=0 登録が生んでいた「記録が当たらない幻エントリ」を解消する。
        var playerObj = networkManager.LocalClient?.PlayerObject;
        if (playerObj != null)
            GameServices.Score?.RegisterPlayer(PlayerScoreId.FromRoot(playerObj.gameObject), offlinePlayerName);
        else
            GameServices.Score?.RegisterPlayer(0, offlinePlayerName); // フォールバック（通常は到達しない）
        IsInitialized = true;
        Debug.Log("[OfflineBoot] NGO Host 起動完了。ゲームループをオフラインで検証開始。");
    }

    public static int TrimDuplicateNetworkRelicClones()
    {
        var relics = Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None);
        var seenNames = new HashSet<string>();
        int removed = 0;

        foreach (var relic in relics)
        {
            if (relic == null) continue;

            var go = relic.gameObject;
            if (!go.name.Contains("(Clone)")) continue;
            if (!relic.TryGetComponent<NetworkObject>(out _)) continue;

            string key = go.name.Replace("(Clone)", string.Empty).Trim();
            if (seenNames.Add(key)) continue;

            Object.Destroy(go);
            removed++;
        }

        return removed;
    }

    public static ushort FindAvailableUdpPort(ushort startPort = 7777)
    {
        for (ushort port = startPort; port < startPort + 20; port++)
        {
            try
            {
                using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
                return port;
            }
            catch (SocketException)
            {
            }
        }

        return startPort;
    }
}
