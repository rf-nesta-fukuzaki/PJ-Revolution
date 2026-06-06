using System.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

/// <summary>
/// Gameplay.unity 等、NetworkManager 未配置シーン向けのオフライン Host 起動。
/// プレイヤーが存在しない場合に NGO Host + PlayerPrefab スポーンを行う。
/// </summary>
[DefaultExecutionOrder(-900)]
public sealed class GameplaySceneHostBootstrap : MonoBehaviour
{
    private const string DefaultPlayerPrefabPath = "Assets/Sandbox/Prefabs/PlayerPrefab.prefab";

    [SerializeField] private GameObject _playerPrefab;
    [SerializeField] private bool _autoStartHost = true;

    private readonly OfflineHostBootstrap _hostBootstrap = new();
    private bool _started;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreateIfNeeded()
    {
        // タイトル / ショップ等の非ゲームプレイシーンでは Host を起動しない（ネットワーク不要）。
        var activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (activeScene == GameFlow.TitleScene || activeScene == GameFlow.ShopScene) return;

        if (NetworkManager.Singleton != null) return;
        if (Object.FindFirstObjectByType<GameplaySceneHostBootstrap>() != null) return;

        foreach (var inv in PlayerInventory.RegisteredInventories)
        {
            if (inv != null && inv.gameObject.activeInHierarchy)
                return;
        }

        if (GameObject.FindGameObjectWithTag("Player") != null) return;

        var go = new GameObject(nameof(GameplaySceneHostBootstrap));
        go.AddComponent<GameplaySceneHostBootstrap>();
    }

    private void Start()
    {
        if (!_autoStartHost || _started) return;
        StartCoroutine(BootIfNeeded());
    }

    private IEnumerator BootIfNeeded()
    {
        yield return null;

        if (HasPlayablePlayer())
            yield break;

        var nm = NetworkManager.Singleton;
        if (nm == null)
            nm = CreateNetworkStack();

        if (nm == null)
        {
            Debug.LogError("[GameplayHost] NetworkManager を生成できませんでした。");
            yield break;
        }

        if (!nm.IsListening)
        {
            LocalCoopSettings.Configure(PartyPlayMode.OfflineLocal);
            yield return _hostBootstrap.StartHost("Player");
        }

        yield return WaitForPlayerSpawn();
        ItemGameplayBootstrap.EnsureNetworkGameplayServices();
        TeleportLocalPlayerToBasecamp();
        ItemGameplayBootstrap.EnsureAllPlayerItemComponents();
        _started = true;
    }

    private static bool HasPlayablePlayer()
    {
        if (GameObject.FindGameObjectWithTag("Player") != null)
            return true;

        foreach (var inv in PlayerInventory.RegisteredInventories)
        {
            if (inv != null && inv.gameObject.activeInHierarchy)
                return true;
        }

        return false;
    }

    private IEnumerator WaitForPlayerSpawn()
    {
        for (int i = 0; i < 120; i++)
        {
            if (HasPlayablePlayer())
                yield break;

            var nm = NetworkManager.Singleton;
            if (nm?.LocalClient?.PlayerObject != null)
                yield break;

            yield return null;
        }

        Debug.LogWarning("[GameplayHost] プレイヤースポーン待機がタイムアウトしました。");
    }

    private static void TeleportLocalPlayerToBasecamp()
    {
        var nm = NetworkManager.Singleton;
        var playerObj = nm?.LocalClient?.PlayerObject;
        if (playerObj == null) return;

        var spawn = ResolveSpawnPosition();
        var rb = playerObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = spawn;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            playerObj.transform.position = spawn;
        }

        Physics.SyncTransforms();
        playerObj.gameObject.tag = "Player";
        Debug.Log($"[GameplayHost] プレイヤーをスポーン地点へ移動: {spawn}");
    }

    private static Vector3 ResolveSpawnPosition()
    {
        var points = Object.FindObjectsByType<SpawnPoint>(FindObjectsSortMode.None);
        foreach (var sp in points)
        {
            if (sp == null) continue;
            if (sp.Layer == SpawnLayer.Route || sp.name.Contains("Basecamp", System.StringComparison.OrdinalIgnoreCase))
                return sp.transform.position;
        }

        var fallback = GameObject.Find("PlayerSpawn_Basecamp");
        if (fallback != null)
            return fallback.transform.position;

        return new Vector3(0f, 2f, -130f);
    }

    private NetworkManager CreateNetworkStack()
    {
        var prefab = ResolvePlayerPrefab();
        if (prefab == null)
        {
            Debug.LogError("[GameplayHost] PlayerPrefab が未設定です。");
            return null;
        }

        var root = new GameObject("NetworkManager");
        DontDestroyOnLoad(root);
        root.AddComponent<UnityTransport>();

        var nm = root.AddComponent<NetworkManager>();

        // 実行時に AddComponent した NetworkManager は NetworkConfig が未初期化（null）のため、
        // PlayerPrefab を設定する前に生成しておく。
        nm.NetworkConfig ??= new NetworkConfig();
        nm.NetworkConfig.PlayerPrefab = prefab;
        nm.NetworkConfig.EnsureNetworkVariableLengthSafety = true;

        Debug.Log("[GameplayHost] NetworkManager を実行時生成しました。");
        return nm;
    }

    private GameObject ResolvePlayerPrefab()
    {
        if (_playerPrefab != null) return _playerPrefab;

        var existing = Object.FindFirstObjectByType<NetworkPlayerSpawner>();
        if (existing != null && existing.PlayerPrefab != null)
            return existing.PlayerPrefab;

#if UNITY_EDITOR
        _playerPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultPlayerPrefabPath);
#endif
        return _playerPrefab;
    }
}
