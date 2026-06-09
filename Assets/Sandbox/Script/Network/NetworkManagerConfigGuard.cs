using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NetworkManager.Awake より前に PlayerPrefab を補完し、null Prefab 登録による NGO 警告を防ぐ。
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class NetworkManagerConfigGuard : MonoBehaviour
{
    private const string PlayerPrefabPath = "Assets/Sandbox/Prefabs/PlayerPrefab.prefab";

    private void Awake() => Ensure(GetComponent<NetworkManager>());

    /// <summary>Host/Client 起動直前の二重チェック用。</summary>
    public static void Ensure(NetworkManager networkManager)
    {
        if (networkManager == null) return;

        networkManager.NetworkConfig ??= new NetworkConfig();

        if (networkManager.NetworkConfig.PlayerPrefab != null) return;

        var prefab = LoadPlayerPrefab();
        if (prefab == null)
        {
            Debug.LogError("[NetworkManagerConfigGuard] PlayerPrefab が未設定です。");
            return;
        }

        networkManager.NetworkConfig.PlayerPrefab = prefab;
        Debug.Log("[NetworkManagerConfigGuard] PlayerPrefab を実行時に補完しました。");
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
