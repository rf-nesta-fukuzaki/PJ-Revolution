using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// <summary>
/// アイテム系（インベントリ・ロッカー・ショップ UI・プレイヤー配線）の実行時セルフヒール。
/// Gameplay / SandboxOfflineCombined いずれでも PlayMode 直後に不足分を補う。
/// </summary>
[DefaultExecutionOrder(-1000)]
public sealed class ItemGameplayBootstrap : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Object.FindFirstObjectByType<ItemGameplayBootstrap>() != null) return;
        var go = new GameObject(nameof(ItemGameplayBootstrap));
        go.AddComponent<ItemGameplayBootstrap>();
    }

    private void Awake()
    {
        EnsureEventSystem();
        EnsureInventoryHud();
        EnsureGhostHud();
        EnsureHintManager();
        EnsureEnemySpawner();
        EnsureNetworkGameplayServices();
        StartCoroutine(EnsureBasecampItemLockerWhenReady());
        EnsureAllPlayerItemComponents();
    }

    private void Start()
    {
        EnsureShopRuntimeUi();
        EnsureNetworkGameplayServices();
        EnsureAllPlayerItemComponents();
        StartCoroutine(DeferredWire());
        StartCoroutine(EnsureNetworkServicesWhenReady());
    }

    private System.Collections.IEnumerator DeferredWire()
    {
        for (int i = 0; i < 30; i++)
        {
            EnsureAllPlayerItemComponents();
            yield return null;
        }
    }

    public static void EnsureAllPlayerItemComponents()
    {
        foreach (var inventory in PlayerInventory.RegisteredInventories)
        {
            if (inventory == null) continue;
            WirePlayer(inventory.gameObject);
        }

        var tagged = GameObject.FindGameObjectWithTag("Player");
        if (tagged != null)
            WirePlayer(tagged);
    }

    private static void WirePlayer(GameObject root)
    {
        if (root.GetComponent<ItemUseController>() == null)
            root.AddComponent<ItemUseController>();
        if (root.GetComponent<ItemHandController>() == null)
            root.AddComponent<ItemHandController>();
        if (root.GetComponent<PlayerShopRopeNetworkBridge>() == null)
            root.AddComponent<PlayerShopRopeNetworkBridge>();
        if (root.GetComponent<HintTriggerDriver>() == null)
            root.AddComponent<HintTriggerDriver>();
        if (root.GetComponent<ProximityVoiceChat>() == null)
            root.AddComponent<ProximityVoiceChat>();
        if (root.GetComponent<NetworkGrapplingHookSync>() == null)
            root.AddComponent<NetworkGrapplingHookSync>();

        var interaction = root.GetComponent<PlayerInteraction>();
        if (interaction != null && !interaction.enabled)
            interaction.enabled = true;

        EnsureLocalCoopMember(root);
    }

    private static void EnsureLocalCoopMember(GameObject root)
    {
        if (!LocalCoopSettings.IsActive) return;

        var member = root.GetComponent<LocalCoopPartyMember>();
        if (member != null) return;

        member = root.AddComponent<LocalCoopPartyMember>();
        member.Configure(0, isHuman: true, "Player 1");
    }

    private static void EnsureInventoryHud()
    {
        if (InventoryHud.Instance != null) return;
        new GameObject("InventoryHud").AddComponent<InventoryHud>();
    }

    // GDD §14.4 — 死亡・幽霊 UI（死亡フラッシュ/幽霊オーバーレイ/範囲制限警告/復活チャネリングバー）。
    private static void EnsureGhostHud() => GhostHud.EnsureExists();

    private static System.Collections.IEnumerator EnsureBasecampItemLockerWhenReady()
    {
        var conformer = Object.FindFirstObjectByType<Sandbox.World.Integration.CombinedTerrainConformer>();
        if (conformer != null)
        {
            for (int i = 0; i < 600; i++)
            {
                if (BasecampItemLocker.Instance != null)
                    yield break;
                yield return null;
            }
        }

        EnsureBasecampItemLocker();
    }

    private static void EnsureBasecampItemLocker()
    {
        if (BasecampItemLocker.Instance != null) return;

        var shop = Object.FindFirstObjectByType<BasecampShop>();
        var anchor = shop != null ? shop.transform : Object.FindFirstObjectByType<ExpeditionManager>()?.transform;
        if (anchor == null) return;

        var rack = new GameObject("GearRack_ItemLocker");
        rack.transform.SetPositionAndRotation(
            anchor.position + anchor.forward * 2.5f + Vector3.up * 1.2f,
            anchor.rotation);

        rack.AddComponent<BasecampItemLocker>();

        CreateShelf(rack.transform, "Shelf_A", new Vector3(-0.6f, 0.35f, 0.15f));
        CreateShelf(rack.transform, "Shelf_B", new Vector3(0.6f, 0.35f, 0.15f));
        CreateShelf(rack.transform, "RefundShelf", new Vector3(0f, -0.25f, 0.35f));

        Debug.Log("[ItemGameplayBootstrap] BasecampItemLocker をショップ近くに生成しました。");
    }

    private static void CreateShelf(Transform parent, string name, Vector3 localPos)
    {
        var shelf = new GameObject(name).transform;
        shelf.SetParent(parent, false);
        shelf.localPosition = localPos;
    }

    private static void EnsureShopRuntimeUi()
    {
        foreach (var shop in Object.FindObjectsByType<BasecampShop>(FindObjectsSortMode.None))
            shop.EnsureRuntimeUiIfMissing();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private static void EnsureEnemySpawner()
    {
        var gm = GameObject.Find("GameManager");
        if (gm == null) return;
        if (gm.GetComponent<EnemySpawner>() != null) return;

        gm.AddComponent<EnemySpawner>();
        Debug.Log("[ItemGameplayBootstrap] GameManager に EnemySpawner を追加しました。");
    }

    private static void EnsureHintManager()
    {
        if (Object.FindFirstObjectByType<HintManager>() != null) return;

        var gm = GameObject.Find("GameManager");
        var host = gm != null ? gm : new GameObject("GameManager");
        host.AddComponent<HintManager>();
        Debug.Log("[ItemGameplayBootstrap] HintManager を追加しました。");
    }

    public static void EnsureNetworkGameplayServices()
    {
        var gm = GameObject.Find("GameManager");
        if (gm != null && gm.GetComponent<SceneServiceInstaller>() == null)
            gm.AddComponent<SceneServiceInstaller>();

        EnsureChildNetworkService<NetworkExpeditionSync>("NetworkExpeditionSync", gm);
        EnsureChildNetworkService<NetworkSpawnAuthoringSync>("NetworkSpawnAuthoringSync", gm);
        NetworkWorldPlacementsSync.EnsureExists();

        if (Object.FindFirstObjectByType<HelicopterController>() == null)
        {
            var heli = new GameObject("HelicopterController");
            heli.AddComponent<NetworkObject>();
            heli.AddComponent<HelicopterController>();
            Debug.Log("[ItemGameplayBootstrap] HelicopterController を追加しました。");
        }
    }

    private static System.Collections.IEnumerator EnsureNetworkServicesWhenReady()
    {
        for (int i = 0; i < 120; i++)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                EnsureNetworkGameplayServices();
                yield break;
            }

            yield return null;
        }
    }

    private static void EnsureChildNetworkService<T>(string childName, GameObject parent) where T : Component
    {
        if (Object.FindFirstObjectByType<T>() != null)
            return;

        GameObject host = parent != null ? parent : new GameObject("GameManager");
        var childTr = host.transform.Find(childName);
        var childGo = childTr != null ? childTr.gameObject : new GameObject(childName);
        if (childTr == null)
            childGo.transform.SetParent(host.transform, false);

        if (childGo.GetComponent<NetworkObject>() == null)
            childGo.AddComponent<NetworkObject>();
        if (childGo.GetComponent<T>() == null)
            childGo.AddComponent<T>();
    }
}
