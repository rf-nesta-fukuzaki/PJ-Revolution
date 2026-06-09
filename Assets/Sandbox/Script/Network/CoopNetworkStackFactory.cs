using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// タイトルシーンから Co-op（UGS Relay + Lobby）を開始するために
/// NetworkBootstrap / LobbyManager / NetworkManager を保証する。
/// </summary>
public static class CoopNetworkStackFactory
{
    private const string PlayerPrefabPath = "Assets/Sandbox/Prefabs/PlayerPrefab.prefab";

    /// <summary>Co-op ロビー操作前に呼ぶ。既存スタックがあれば何もしない。</summary>
    public static void EnsureForTitleScene()
    {
        EnsureSingleton<NetworkBootstrap>("[NetworkBootstrap]");
        EnsureSingleton<LobbyManager>("[LobbyManager]");

        if (NetworkManager.Singleton != null) return;

        var root = new GameObject("[CoopNetworkStack]");
        Object.DontDestroyOnLoad(root);
        root.AddComponent<UnityTransport>();

        var nm = root.AddComponent<NetworkManager>();
        root.AddComponent<NetworkManagerConfigGuard>();

        // 実行時に AddComponent した NetworkManager は NetworkConfig が未初期化（null）のため、
        // PlayerPrefab を設定する前に生成しておく。
        nm.NetworkConfig ??= new NetworkConfig();
        nm.NetworkConfig.EnsureNetworkVariableLengthSafety = true;

        var prefab = LoadPlayerPrefab();
        if (prefab != null)
            nm.NetworkConfig.PlayerPrefab = prefab;
        else
            Debug.LogWarning("[CoopNetwork] PlayerPrefab が未設定です。Co-op 開始時に失敗する可能性があります。");
    }

    private static T EnsureSingleton<T>(string name) where T : Component
    {
        var existing = Object.FindFirstObjectByType<T>();
        if (existing != null) return existing;

        var go = new GameObject(name);
        Object.DontDestroyOnLoad(go);
        return go.AddComponent<T>();
    }

    private static GameObject LoadPlayerPrefab()
    {
        var fromResources = Resources.Load<GameObject>("PlayerPrefab");
        if (fromResources != null) return fromResources;

#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
#else
        return null;
#endif
    }
}
