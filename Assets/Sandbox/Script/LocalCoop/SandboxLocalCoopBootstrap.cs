using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// SandboxOfflineCombined 向けローカル Co-op 起動。
/// 最大4人のパーティを構成し、人間が足りない分を NPC で補う。
/// 後入り/後抜けは <see cref="LocalCoopJoinLeaveController"/> が担当する。
/// </summary>
[DefaultExecutionOrder(50)]
public sealed class SandboxLocalCoopBootstrap : MonoBehaviour
{
    [Header("パーティ構成")]
    [SerializeField, Range(1, 4)] private int _humanPlayerCount = 1;
    [SerializeField] private bool _autoDetectGamepads = true;
    [SerializeField] private bool _persistHumanCount = true;

    [Header("スポーン")]
    [SerializeField] private Vector3 _spawnBase = new Vector3(0f, 2f, 0f);
    [SerializeField] private float _spawnSpacing = 2.5f;
    [SerializeField] private GameObject _playerPrefabOverride;

    [Header("NPC 補充（OfflineNPCSpawner と同設定）")]
    [SerializeField] private GameObject _explorerModelPrefab;
    [SerializeField] private RuntimeAnimatorController _animatorController;
    [SerializeField] private Vector3 _modelOffset = new Vector3(0f, -0.9f, 0f);
    [SerializeField] private Vector3 _modelScale = Vector3.one;
    [SerializeField] private string[] _npcNames = { "Alex", "Jordan", "Riley", "Sam" };
    [SerializeField] private Color[] _npcColors =
    {
        new Color(0.25f, 0.55f, 1.00f),
        new Color(0.20f, 0.80f, 0.30f),
        new Color(0.95f, 0.45f, 0.10f),
        new Color(0.75f, 0.35f, 0.85f),
    };

    private LocalCoopSplitScreen _splitScreen;
    private LocalCoopRoster _roster;
    private bool _partyBuilt;

    public static SandboxLocalCoopBootstrap Instance { get; private set; }
    public bool IsPartyReady => _partyBuilt;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        TryAutoLoadAssets();
        Instance = this;
        LocalCoopSettings.IsActive = true;
        _roster = LocalCoopRoster.CreateOrReplace();

        if (!LocalCoopSettings.IsOnline)
        {
            int configured = _persistHumanCount
                ? LocalCoopSettings.LoadHumanCountFromPrefs(_humanPlayerCount)
                : _humanPlayerCount;
            LocalCoopSettings.HumanCount = LocalCoopSettings.ResolveHumanCount(configured, _autoDetectGamepads);
            if (_persistHumanCount)
                LocalCoopSettings.SaveHumanCountToPrefs(LocalCoopSettings.HumanCount);
        }

        DisableLegacyNpcSpawner();
        _splitScreen = gameObject.GetComponent<LocalCoopSplitScreen>();
        if (_splitScreen == null)
            _splitScreen = gameObject.AddComponent<LocalCoopSplitScreen>();

        if (!LocalCoopSettings.IsOnline && GetComponent<LocalCoopJoinLeaveController>() == null)
            gameObject.AddComponent<LocalCoopJoinLeaveController>();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
            LocalCoopSettings.IsActive = false;
            LocalCoopRoster.ClearInstance();
        }
    }

    private Coroutine _buildRoutine;

    private void Start()
    {
        // セッションブートストラッパーがある場合は、そちらが各セッション開始時に
        // RebuildParty を駆動する（オフライン/オンライン/ホスト/ゲストの切替に対応）。
        // 単体シーン（セッション UI 無し）ではここで自前ビルドする。
        if (SandboxOnlineSessionBootstrap.Instance == null)
            RebuildParty();
    }

    /// <summary>
    /// パーティを作り直す。既存の非ネットワークアクター（オフライン NPC 等）を破棄し、
    /// ロスターをクリアしてから現在のセッションに合わせて再構築する。
    /// セッション切替・再起動時に <see cref="SandboxOnlineSessionBootstrap"/> から呼ばれる。
    /// </summary>
    public void RebuildParty()
    {
        if (_buildRoutine != null)
            StopCoroutine(_buildRoutine);
        _buildRoutine = StartCoroutine(RebuildRoutine());
    }

    private IEnumerator RebuildRoutine()
    {
        TeardownPartyActors();
        _partyBuilt = false;
        ConfigureForCurrentMode();
        yield return BuildPartyWhenReady();
    }

    /// <summary>現在のモードに合わせて JoinLeave 制御と人数設定を切り替える。</summary>
    private void ConfigureForCurrentMode()
    {
        var joinLeave = GetComponent<LocalCoopJoinLeaveController>();

        if (!LocalCoopSettings.IsOnline)
        {
            if (joinLeave == null)
                joinLeave = gameObject.AddComponent<LocalCoopJoinLeaveController>();
            joinLeave.enabled = true;

            int configured = _persistHumanCount
                ? LocalCoopSettings.LoadHumanCountFromPrefs(_humanPlayerCount)
                : _humanPlayerCount;
            LocalCoopSettings.HumanCount = LocalCoopSettings.ResolveHumanCount(configured, _autoDetectGamepads);
            if (_persistHumanCount)
                LocalCoopSettings.SaveHumanCountToPrefs(LocalCoopSettings.HumanCount);
        }
        else if (joinLeave != null)
        {
            // オンラインでは後入り/後抜けは NetworkPartyManager が担当するため無効化。
            joinLeave.enabled = false;
        }
    }

    /// <summary>セッション停止直前に、非ネットワークアクターのみ先行破棄する。</summary>
    public void RebuildPartyTeardownOnly()
    {
        if (_buildRoutine != null)
        {
            StopCoroutine(_buildRoutine);
            _buildRoutine = null;
        }
        _partyBuilt = false;
        TeardownPartyActors();
    }

    /// <summary>非ネットワークの既存アクターを破棄しロスターを初期化する。</summary>
    private void TeardownPartyActors()
    {
        if (_roster == null)
        {
            _roster = LocalCoopRoster.CreateOrReplace();
            return;
        }

        for (int slot = 0; slot < LocalCoopSettings.MaxPartySize; slot++)
        {
            var member = _roster.GetSlot(slot);
            if (member == null) continue;

            var go = member.gameObject;
            if (go == null) continue;

            // スポーン済みネットワークオブジェクトは NGO の Shutdown 側で破棄される。
            var netObj = go.GetComponent<NetworkObject>();
            bool spawned = netObj != null && netObj.IsSpawned;
            if (!spawned)
                Destroy(go);
        }

        _roster.ClearAll();
    }

    public bool TryPromoteSlot(int slot, Gamepad gamepad)
    {
        if (!_partyBuilt || _roster == null) return false;
        if (!_roster.TryPromoteNpcToHuman(slot, gamepad, this)) return false;
        RefreshPresentation();
        return true;
    }

    public bool TryDemoteSlot(int slot)
    {
        if (!_partyBuilt || _roster == null) return false;
        if (!_roster.TryDemoteHumanToNpc(slot, this)) return false;
        RefreshPresentation();
        return true;
    }

    public void RefreshPresentation()
    {
        if (LocalCoopSettings.IsOnline)
        {
            RefreshOnlineLocalPresentation();
            return;
        }

        if (_roster == null) return;
        _roster.SyncHumanCountSetting();
        if (_persistHumanCount)
            LocalCoopSettings.SaveHumanCountToPrefs(LocalCoopSettings.HumanCount);
        _splitScreen.ApplyLayout(_roster.CollectHumanExplorers());
    }

    private void RefreshOnlineLocalPresentation()
    {
        var nm = NetworkManager.Singleton;
        var localPlayer = nm?.LocalClient?.PlayerObject;
        if (localPlayer == null) return;

        var explorer = localPlayer.GetComponent<ExplorerController>();
        if (explorer == null) return;

        var look = localPlayer.GetComponent<ExplorerCameraLook>();
        look?.SetLocalCoopHuman(true);

        _splitScreen.ApplyLayout(new List<ExplorerController> { explorer });
    }

    public void ConfigureOnlineHuman(GameObject root, int slot, ulong clientId, string displayName)
    {
        ConfigureHuman(root, slot, displayName, gamepad: null);
        var member = root.GetComponent<LocalCoopPartyMember>();
        member?.SetNetworkOwner(clientId);
        _roster?.RegisterSlot(slot, member);
        if (slot == 0)
            _roster?.RegisterHost(root, member);
        GameServices.Score?.RegisterPlayer(PlayerScoreId.FromRoot(root), displayName);
    }

    public GameObject SpawnNetworkNpcAt(int partySlot, Vector3 position)
    {
        string name = ResolveNpcName(partySlot);
        Color color = ResolveNpcColor(partySlot);

        if (LocalCoopSettings.IsOnline)
            return SpawnOnlineNpc(partySlot, position, name);

        var go = LocalCoopNpcFactory.SpawnNpc(
            partySlot, name, position,
            _explorerModelPrefab, _animatorController, _modelOffset, _modelScale, color);

        var member = go.GetComponent<LocalCoopPartyMember>();
        member?.SetNetworkOwner(NetworkPartyManager.NoClient);
        _roster?.RegisterSlot(partySlot, member);
        // 個人スコア登録は NPCController が自身の GetInstanceID で冪等に行う（記録側と同一スキーム）。
        // bootstrap 側で別キー登録すると記録の当たらない幻エントリになるため、ここでは登録しない。
        return go;
    }

    /// <summary>
    /// オンライン NPC は登録済み PlayerPrefab を流用して生成する。
    /// 実行時生成プレハブを <c>AddNetworkPrefab</c> で登録するのは
    /// NetworkManager 起動後には不可能（ForceSamePrefabs）かつホスト/クライアント間で
    /// ハッシュ不一致になるため、既登録の PlayerPrefab を NPC 化して使う。
    /// </summary>
    private GameObject SpawnOnlineNpc(int partySlot, Vector3 position, string name)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
        {
            Debug.LogWarning("[LocalCoop] オンライン NPC はサーバーのみ生成できます。");
            return null;
        }

        GameObject prefab = ResolvePlayerPrefab();
        if (prefab == null)
        {
            Debug.LogError("[LocalCoop] オンライン NPC 用 PlayerPrefab が未設定です。");
            return null;
        }

        var go = Instantiate(prefab, position, Quaternion.identity);
        go.name = $"NPC_{name}";

        var netObj = go.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogError("[LocalCoop] PlayerPrefab に NetworkObject がありません。");
            Destroy(go);
            return null;
        }

        // 通常 NetworkObject としてスポーン（プレイヤーオブジェクトにはしない）。
        netObj.Spawn(true);

        // 人間操作系（カメラ/入力/ロープ等）を無効化し NPC 制御へ切り替える。
        SetHostControlMode(go, human: false);

        var member = go.GetComponent<LocalCoopPartyMember>();
        if (member == null)
            member = go.AddComponent<LocalCoopPartyMember>();
        member.Configure(partySlot, isHuman: false, name);
        member.SetNetworkOwner(NetworkPartyManager.NoClient);

        _roster?.RegisterSlot(partySlot, member);
        // 個人スコア登録は NPCController が自身の GetInstanceID で冪等に行う（記録側と同一スキーム）。
        // bootstrap 側で別キー登録すると記録の当たらない幻エントリになるため、ここでは登録しない。
        return go;
    }

    public void DespawnPartyActor(GameObject actor)
    {
        if (actor == null) return;

        var netObj = actor.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            netObj.Despawn(true);

        Destroy(actor);
    }

    public GameObject SpawnHumanAt(int slot, Vector3 position, Quaternion rotation, string displayName, Gamepad gamepad)
    {
        GameObject prefab = ResolvePlayerPrefab();
        if (prefab == null) return null;

        var instance = Instantiate(prefab, position, rotation);
        instance.name = $"Player_Slot{slot}";

        var netObj = instance.GetComponent<NetworkObject>();
        if (netObj != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            netObj.Spawn(true);

        ConfigureHuman(instance, slot, displayName, gamepad);
        return instance;
    }

    public GameObject SpawnNpcAt(int partySlot, Vector3 position, string displayName, Color color)
    {
        var go = LocalCoopNpcFactory.SpawnNpc(
            partySlot,
            displayName,
            position,
            _explorerModelPrefab,
            _animatorController,
            _modelOffset,
            _modelScale,
            color);

        _roster.RegisterSlot(partySlot, go.GetComponent<LocalCoopPartyMember>());
        // 個人スコア登録は NPCController(GetInstanceID) が冪等に行う（幻エントリ回避）。
        return go;
    }

    public string ResolveNpcName(int index)
    {
        if (_npcNames != null && index < _npcNames.Length && !string.IsNullOrEmpty(_npcNames[index]))
            return _npcNames[index];
        return $"Companion_{index + 1}";
    }

    public Color ResolveNpcColor(int index)
    {
        if (_npcColors != null && index < _npcColors.Length)
            return _npcColors[index];
        return Color.HSVToRGB(index / 6f, 0.7f, 0.9f);
    }

    public void EnableHostAsHuman(GameObject hostRoot, Gamepad gamepad, string displayName)
    {
        SetHostControlMode(hostRoot, human: true);
        ConfigureHuman(hostRoot, 0, displayName, gamepad);
    }

    public void EnableHostAsNpc(GameObject hostRoot, string npcName)
    {
        SetHostControlMode(hostRoot, human: false);
        var member = hostRoot.GetComponent<LocalCoopPartyMember>();
        if (member == null)
            member = hostRoot.AddComponent<LocalCoopPartyMember>();
        member.Configure(0, isHuman: false, npcName);
        _roster.RegisterSlot(0, member);
        // 個人スコア登録は NPCController(GetInstanceID) が冪等に行う（幻エントリ回避）。
    }

    private void SetHostControlMode(GameObject hostRoot, bool human)
    {
        var explorer = hostRoot.GetComponent<ExplorerController>();
        var look = hostRoot.GetComponent<ExplorerCameraLook>();
        var interaction = hostRoot.GetComponent<PlayerInteraction>();
        var scramble = hostRoot.GetComponent<ScrambleClimbController>();
        var wireRope = hostRoot.GetComponent<WireRopeActionController>();
        var npc = hostRoot.GetComponent<NPCController>();

        if (human)
        {
            if (npc != null)
                Destroy(npc);

            if (explorer != null) explorer.enabled = true;
            if (interaction != null) interaction.enabled = true;
            if (scramble != null) scramble.enabled = true;
            if (wireRope != null) wireRope.enabled = true;
            if (look != null)
            {
                look.enabled = true;
                look.SetLocalCoopHuman(true);
                var cam = look.GetComponentInChildren<Camera>();
                if (cam != null) cam.enabled = true;
            }
        }
        else
        {
            if (explorer != null) explorer.enabled = false;
            if (interaction != null) interaction.enabled = false;
            if (scramble != null) scramble.enabled = false;
            if (wireRope != null) wireRope.enabled = false;
            if (look != null)
            {
                look.SetLocalCoopHuman(false);
                look.enabled = false;
                var cam = look.GetComponentInChildren<Camera>();
                if (cam != null)
                {
                    cam.enabled = false;
                    cam.tag = "Untagged";
                }
            }

            if (npc == null)
                hostRoot.AddComponent<NPCController>();
        }
    }

    private void TryAutoLoadAssets()
    {
#if UNITY_EDITOR
        if (_explorerModelPrefab == null)
            _explorerModelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Sandbox/Art/Models/Explorer.fbx");
        if (_animatorController == null)
            _animatorController = UnityEditor.AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                "Assets/Sandbox/Art/Animation/Explorer/ExplorerAnimator.controller");
        if (_playerPrefabOverride == null)
            _playerPrefabOverride = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Sandbox/Prefabs/PlayerPrefab.prefab");
#endif
    }

    private static void DisableLegacyNpcSpawner()
    {
        var legacy = Object.FindFirstObjectByType<OfflineNPCSpawner>();
        if (legacy != null)
            legacy.enabled = false;
    }

    private IEnumerator BuildPartyWhenReady()
    {
        const float timeout = 15f;
        float elapsed = 0f;

        while (elapsed < timeout)
        {
            var nm = NetworkManager.Singleton;
            // ホスト/クライアントどちらでも自プレイヤーが生成されたら開始。
            if (nm != null && nm.IsListening && nm.LocalClient?.PlayerObject != null)
                break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (_partyBuilt) yield break;

        if (LocalCoopSettings.IsOnline)
        {
            _partyBuilt = true;
            yield break;
        }

        var hostPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        if (hostPlayer == null)
        {
            Debug.LogError("[LocalCoop] NGO ホストプレイヤーが見つかりません。パーティ構築をスキップします。");
            yield break;
        }

        BuildParty(hostPlayer.gameObject);
        _partyBuilt = true;
        RefreshPresentation();
    }

    private void BuildParty(GameObject hostPlayerRoot)
    {
        int humans = LocalCoopSettings.HumanCount;
        int npcCount = LocalCoopSettings.NpcFillCount;

        ConfigureHuman(hostPlayerRoot, slot: 0, displayName: "Player 1", gamepad: null);
        _roster.RegisterHost(hostPlayerRoot, hostPlayerRoot.GetComponent<LocalCoopPartyMember>());

        GameObject prefab = ResolvePlayerPrefab();
        for (int slot = 1; slot < humans; slot++)
        {
            Gamepad pad = slot - 1 < Gamepad.all.Count ? Gamepad.all[slot - 1] : null;
            SpawnHumanPlayer(prefab, slot, pad);
        }

        for (int i = 0; i < npcCount; i++)
        {
            int slot = humans + i;
            SpawnNpcCompanion(slot, i);
        }

        _roster.SyncHumanCountSetting();
        Debug.Log($"[LocalCoop] パーティ構築完了: 人間={humans}, NPC補充={npcCount}, 合計={LocalCoopSettings.MaxPartySize}");
    }

    private GameObject ResolvePlayerPrefab()
    {
        if (_playerPrefabOverride != null) return _playerPrefabOverride;
        var spawner = Object.FindFirstObjectByType<NetworkPlayerSpawner>();
        if (spawner != null && spawner.PlayerPrefab != null)
            return spawner.PlayerPrefab;

        return NetworkManager.Singleton != null ? NetworkManager.Singleton.NetworkConfig.PlayerPrefab : null;
    }

    private GameObject SpawnHumanPlayer(GameObject prefab, int slot, Gamepad gamepad)
    {
        if (prefab == null)
        {
            Debug.LogError("[LocalCoop] PlayerPrefab が未設定のため追加人間プレイヤーをスポーンできません。");
            return null;
        }

        Vector3 pos = GetSpawnPosition(slot);
        var instance = Instantiate(prefab, pos, Quaternion.identity);
        ConfigureHuman(instance, slot, $"Player {slot + 1}", gamepad);
        _roster.RegisterSlot(slot, instance.GetComponent<LocalCoopPartyMember>());
        return instance;
    }

    private void SpawnNpcCompanion(int partySlot, int npcNameIndex)
    {
        string name = ResolveNpcName(npcNameIndex);
        Color color = ResolveNpcColor(npcNameIndex);
        Vector3 pos = GetSpawnPosition(partySlot);
        SpawnNpcAt(partySlot, pos, name, color);
    }

    private void ConfigureHuman(GameObject root, int slot, string displayName, Gamepad gamepad)
    {
        root.tag = "Player";

        var member = root.GetComponent<LocalCoopPartyMember>();
        if (member == null)
            member = root.AddComponent<LocalCoopPartyMember>();
        member.Configure(slot, isHuman: true, displayName, gamepad);

        GameServices.Score?.RegisterPlayer(PlayerScoreId.FromRoot(root), displayName);

        var look = root.GetComponent<ExplorerCameraLook>();
        if (look != null)
            look.SetLocalCoopHuman(true);
    }

    private Vector3 GetSpawnPosition(int slot)
    {
        var spawner = Object.FindFirstObjectByType<NetworkPlayerSpawner>();
        if (spawner != null)
            return spawner.GetSpawnPositionForIndex(slot);

        return _spawnBase + Vector3.right * (slot * _spawnSpacing);
    }
}
