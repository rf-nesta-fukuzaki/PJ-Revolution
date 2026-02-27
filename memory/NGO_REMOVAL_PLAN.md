# NGO除去計画

ステータス: **実行完了・検証済み** (2026-02-26)
作成日: 2026-02-26

### 最終検証結果
- `using Unity.Netcode;` — 0件 ✅
- `: NetworkBehaviour` — 0件 ✅
- `[ServerRpc]` / `[ClientRpc]` — 0件 ✅
- `OnNetworkSpawn` / `OnNetworkDespawn` — 0件 ✅
- `NetworkBootstrapper` / `CaveNetworkSync` / `NetworkPlayer` — 0件（コード内）✅
- `NetworkSurvivalStats` — SurvivalStats.cs の説明コメント1行のみ（コンパイル影響なし）✅
- `Network/` フォルダ — 削除済み ✅
- 全スクリプト: 45ファイル（MonoBehaviour ベース）✅
- 残存問題: なし

---

## 概要

全49スクリプト中、NGO依存を持つファイルは以下:
- `using Unity.Netcode;` あり: 19ファイル
- `NetworkManager` / `NetworkObject` 等の参照あり: 27ファイル（上記を含む）

---

## DELETE対象

### Network/ フォルダ（全削除）
| ファイル | 理由 |
|---|---|
| `Assets/Scripts/Network/NetworkBootstrapper.cs` | NGO専用 (Host/Client/Standalone選択UIと起動制御) |
| `Assets/Scripts/Network/CaveNetworkSync.cs` | NGO専用 (洞窟シードの Host→Client 同期) |
| `Assets/Scripts/Network/SpawnPointResolver.cs` | NGO専用 (Physics.Raycastでスポーン床面Y取得) |
| `Assets/Scripts/Network/NetworkPlayer.cs` | NGO専用 (プレイヤー位置/視線/ジャンプ ServerRpc) |
| `Assets/Scripts/Network/NetworkTorchAdapter.cs` | NGO専用 (たいまつ燃料/点灯 NetworkVariable管理) |
| `Assets/Scripts/Network/NetworkSurvivalStats.cs` | DELETE → **SurvivalStats.cs として新規作成** (ロジック移植) |

### Editor/ フォルダ（不要スクリプト）
Glob確認結果: `Assets/Scripts/Editor/PrefabApplyHelper.cs` のみ存在。
CLAUDE.mdに記載されている TMPFontReplacer.cs, SceneCleanupTool.cs, BatPrefabSetup.cs, PlayerPrefabSetup.cs は**存在しない**ため削除不要。

---

## 新規作成対象

### `Assets/Scripts/SurvivalStats.cs`
NetworkSurvivalStats.cs のシングルプレイ版。MonoBehaviour として作成。

```
変更内容:
- NetworkBehaviour → MonoBehaviour
- using Unity.Netcode; 削除
- NetworkVariable<float> Health/Oxygen/Hunger → private float + public float プロパティ + OnChanged イベント
  例: public float Health { get; private set; } = 100f;
      public event Action<float, float> OnHealthChanged; (prev, current)
- NetworkVariable<bool> IsDowned → bool IsDowned { get; private set; }
  public event Action<bool, bool> OnIsDownedChanged;
- ApplyStatModification(StatType, float) → そのまま通常メソッドとして残す
- ModifyStatServerRpc → ApplyStatModification に統合 (直接呼ぶ)
- IsServer チェック → 削除
- StatType enum はここで定義 (NetworkSurvivalStats から移植)
- Update() の酸素・空腹消費ロジック → そのまま維持
```

### `Assets/Scripts/Game/SimpleSpawner.cs`
CaveGenerator.OnCaveGenerated後にプレイヤーをスポーン地点へ移動させる（提供済みテンプレート通り）。

---

## REWRITE対象

---

### `Assets/Scripts/PlayerStateManager.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `[RequireComponent(typeof(NetworkPlayer))]` → 削除
- `NetworkVariable<PlayerState> CurrentState` → `PlayerState _currentState; public PlayerState CurrentState { get; private set; }`
  - `CurrentState.Value` を参照している全箇所は `CurrentState` に変更
  - `OnValueChanged` 購読は削除 (StateChangedイベントで代替も可)
- `NetworkSurvivalStats _survivalStats` → `SurvivalStats _survivalStats`
- `_survivalStats.IsDowned.Value` → `_survivalStats.IsDowned`
- `_survivalStats.IsDowned.OnValueChanged += OnIsDownedChanged` → `_survivalStats.OnIsDownedChanged += OnIsDownedChanged`
- `OnNetworkSpawn()` → `Start()` にマージ: `_rb.isKinematic = false;` 初期化のみ
- `OnNetworkDespawn()` → `OnDestroy()` にマージ: イベント解除
- `[ServerRpc] RequestStateChangeServerRpc` → 通常メソッド `RequestStateChange` に変更
- `IsServer` チェック → 削除
- `IsSpawned` チェック → 削除
- `IsOwner/IsServer` のデバッグログ → 削除
- `NetworkManager.Singleton` 参照 → 削除
- `InitializeStandalone()` → 削除
- `Update()` の `isNetworked && !IsServer` ガード → 削除 (常に実行)
- `SetState()` の isNetworked/スタンドアロン分岐 → 削除 (直接 `OnStateChanged` 呼ぶ)

---

### `Assets/Scripts/PlayerInputController.cs`
- `using Unity.Netcode;` 削除
- `[SerializeField] NetworkPlayer networkPlayer` フィールド削除
- `[SerializeField] NetworkTorchAdapter networkTorchAdapter` フィールド削除
- `IsNetworkActive` プロパティ削除
- `IsNormalState()`: NetworkManager参照削除、`_stateManager.CurrentState.Value` → `_stateManager.CurrentState`
- `HandleMovement()`: `if (IsNetworkActive)` 分岐削除、`playerMovement.Move(input)` のみ残す
- `HandleJump()`: `if (IsNetworkActive)` 分岐削除、`playerMovement.Jump()` のみ残す
- `HandleTorch()`: `if (IsNetworkActive)` 分岐削除、`torchSystem?.ToggleTorch()` のみ残す
- `HandleDebug()`: `if (IsNetworkActive)` 分岐削除、`torchSystem?.RefillFuel()` のみ残す
- `Awake()` の `networkPlayer/networkTorchAdapter` 取得処理削除

---

### `Assets/Scripts/FirstPersonLook.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `[RequireComponent(typeof(NetworkPlayer))]` → 削除
- `NetworkPlayer _networkPlayer` フィールド削除
- `IsNetworkActive` プロパティ削除
- `OnNetworkSpawn()` → 削除 (Awakeでの参照取得で十分)
- `Update()` の `IsNetworkActive && !IsOwner` ガード → 削除
- `OnGUI()` の `IsNetworkActive && !IsOwner` ガード → 削除
- `ApplyMouseLook()`:
  - `if (IsNetworkActive && !Mathf.Approximately(mouseX, 0f)) _networkPlayer?.LookServerRpc(newYAngle)` → 削除
  - `if (IsNetworkActive && _networkPlayer != null) _networkPlayer.PitchAngle.Value = _pitch` → 削除
- `HandleCursorLock()`: `_stateManager.IsSpawned &&` → 削除
- `_stateManager.CurrentState.Value` → `_stateManager.CurrentState`
- `Awake()` の `_networkPlayer = GetComponent<NetworkPlayer>()` 削除

---

### `Assets/Scripts/Climbing/PlayerClimbing.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `[RequireComponent(typeof(NetworkPlayer))]` → 削除
- `NetworkVariable<bool> IsClimbing` → `private bool _isClimbing; public bool IsClimbing => _isClimbing;`
- `NetworkSurvivalStats _survivalStats` → `SurvivalStats _survivalStats`
- `_survivalStats.Hunger.Value` → `_survivalStats.Hunger`
- `OnNetworkSpawn/Despawn` → 削除
- `FixedUpdate()` の `if (!IsServer) return;` → 削除
- `StartClimbing()` の `if (!IsServer) return;` → 削除
- `StopClimbing()` の `if (!IsServer) return;` → 削除
- `IsClimbing.Value` → `_isClimbing` (直接代入に変更)
- `[ServerRpc] ClimbInputServerRpc` → 属性削除、メソッド名 `ProcessClimbInput(float vertical, bool jump)` に変更
- `Update()` の `if (!IsOwner) return;` → 削除 (シングルなので常にOwner)
- `SnapToRopeNextFrame()` の `if (!IsServer || !IsClimbing.Value ...)` → `if (!IsClimbing ...)`

---

### `Assets/Scripts/Climbing/RopeController.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `[RequireComponent(typeof(NetworkObject))]` → 削除
- `NetworkVariable<bool> IsDeployed` → `private bool _isDeployed; public bool IsDeployed => _isDeployed;`
- `OnNetworkSpawn/Despawn` → `Start()` で `UpdateVisuals(false)` を呼ぶ
- `Interact(NetworkPlayer player)` → IInteractable 変更と合わせてシグネチャ変更
- `IsDeployed.Value` → `_isDeployed`
- `if (!IsServer) return;` → 削除
- `IsClimbing.Value` → `IsClimbing` (PlayerClimbing変更後)
- Gizmoの `IsDeployed.Value` → `IsDeployed` (プロパティ)

---

### `Assets/Scripts/Enemy/BatAI.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `NetworkVariable<BatState> CurrentState` → `BatState _currentState; public BatState CurrentState { get; private set; }`
- `NetworkSurvivalStats プレイヤーステータス` → `SurvivalStats プレイヤーステータス` (後方互換フィールド)
- `OnNetworkSpawn()` → `Start()` で `_homePosition = transform.position; CurrentState = BatState.Sleeping;`
- `OnNetworkDespawn()` → 削除
- `Update()` の `isNetworked && !IsServer` ガード → 削除
- `SetState()` の isNetworked/スタンドアロン分岐 → 削除 (直接 `OnStateChanged` 呼ぶ)
- `[ClientRpc] AttackClientRpc` → 削除 (スタブなので)
- `PerformBite()` の `isNetworked && IsSpawned` ガード (AttackClientRpc呼び出し) → 削除
- `CurrentState.Value` → `CurrentState` (全箇所)
- `targetStats.IsDowned.Value` → `targetStats.IsDowned`
- `targetStats.Health.Value` → `targetStats.Health` (デバッグログ)
- `NetworkManager.Singleton` 参照 → 全削除

---

### `Assets/Scripts/Enemy/BatSpawner.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `[RequireComponent(typeof(NetworkObject))]` → 削除
- `Dictionary<ulong, GameObject> _playerObjects` → 削除
- `OnNetworkSpawn/Despawn` → 削除 (Awake/OnDestroy で処理済み)
- `UnsubscribeNetworkCallbacks()` → 削除
- `OnClientConnected/Disconnected` → 削除
- `RegisterPlayerWithRetry` コルーチン → 削除
- `ScanAndRegisterConnectedPlayers()` → 削除
- `OnCaveGeneratedHandler()` の isNetworked ガード → 削除
- `OnCaveGeneratedHandler()` の NGO分岐 → `FindAndRegisterPlayerStandalone()` のみ呼ぶ
- `SpawnOneBat()`: NGOパス削除、`Instantiate` のみ残す
- `RegisterPlayer()`:
  - `NetworkTorchAdapter adapter` の取得・渡し → 削除
  - `NetworkSurvivalStats stats` → `SurvivalStats stats`
  - `BatPerception.AddTarget()` シグネチャ変更と連動
- `RegisterPlayerExternal/UnregisterPlayerExternal()`: `isNetworked && !IsServer` ガード削除
- `NetworkManager.Singleton` 参照 → 全削除
- `IsServer` チェック → 全削除

---

### `Assets/Scripts/Enemy/BatPerception.cs`
(MonoBehaviourだが NetworkTorchAdapter/NetworkSurvivalStats を参照しているため変更必要)
- `List<NetworkTorchAdapter> _torchAdapters` → 削除 (TorchSystem.IsLit で十分)
- `List<NetworkSurvivalStats> _stats` → `List<SurvivalStats> _stats`
- `NetworkBootstrapper _bootstrapper` フィールド削除
- `Awake()` の `_bootstrapper = FindFirstObjectByType<NetworkBootstrapper>()` → 削除
- `SetTargets()` (List版): `List<NetworkTorchAdapter> torchAdapters` 引数削除
- `AddTarget()`: `NetworkTorchAdapter torchAdapter` 引数削除
- `RemoveTarget()`: `_torchAdapters.RemoveAt(idx)` 削除
- `GetNearestPlayerStats()` の戻り型: `NetworkSurvivalStats` → `SurvivalStats`
- `IsPlayerDowned()`: `sm.CurrentState.Value` → `sm.CurrentState`
- `IsTorchLit()` の NetworkTorchAdapter 参照コメント → 削除 (TorchSystem.IsLit のみ使用)
- 旧 `SetTargets(Transform, TorchSystem)` の `_torchAdapters.Add(null)` → 削除

---

### `Assets/Scripts/Interaction/IInteractable.cs`
- `void Interact(NetworkPlayer player)` → `void Interact(GameObject interactor)` に変更
  - 全実装クラスのシグネチャを合わせて変更
  - シングルプレイではplayerオブジェクトへの参照はGameObjectで十分

---

### `Assets/Scripts/Interaction/PlayerInteractor.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `[RequireComponent(typeof(NetworkPlayer))]` → 削除
- `OnNetworkSpawn()` → `Start()` に変更 (`_ui` / `_camera` 取得)
- `OnNetworkDespawn()` → `OnDestroy()` に変更
- `IsOwner` チェック → 削除
- `Update()` の `if (!IsOwner) return;` → 削除
- インタラクト時の分岐:
  - `if (_currentTarget is NetworkBehaviour nb)` 分岐 → 削除
  - 直接 `_currentTarget.Interact(gameObject)` を呼ぶ
- `[ServerRpc] InteractServerRpc` → 削除
- `NetworkManager.Singleton.SpawnManager.SpawnedObjects` 参照 → 削除
- `GetComponent<NetworkPlayer>()` → 削除

---

### `Assets/Scripts/Interaction/ResourceItem.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `[RequireComponent(typeof(NetworkObject))]` → 削除
- `Interact(NetworkPlayer player)` → `Interact(GameObject interactor)`
- `if (!IsServer) return;` → 削除
- `NetworkTorchAdapter` 参照 → `TorchSystem` に変更:
  - `player.GetComponentInChildren<NetworkTorchAdapter>()` → `interactor.GetComponentInChildren<TorchSystem>()`
  - `torch.SetFuelServer(torch.CurrentFuel + restoreAmount)` → `torch.RefillFuel(restoreAmount)`
- `NetworkSurvivalStats stats` → `SurvivalStats stats`
- `NetworkObject.Despawn(true)` → `Destroy(gameObject)`

---

### `Assets/Scripts/Interaction/CollectibleGem.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `[RequireComponent(typeof(NetworkObject))]` → 削除
- `Interact(NetworkPlayer player)` → `Interact(GameObject interactor)`
- `if (!IsServer) return;` → 削除
- `NetworkObject.Despawn(true)` → `Destroy(gameObject)`

---

### `Assets/Scripts/Interaction/DownedReviveInteractable.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `NetworkSurvivalStats _survivalStats` → `SurvivalStats _survivalStats`
- `Interact(NetworkPlayer rescuer)` → `Interact(GameObject interactor)`
- `isNetworked && !IsServer` ガード → 削除
- `_survivalStats.IsDowned.Value` → `_survivalStats.IsDowned`

---

### `Assets/Scripts/Interaction/PlacedResourceItem.cs`
- `Interact(NetworkPlayer player)` → `Interact(GameObject interactor)`
- FuelCanisterの取得: `player.GetComponentInChildren<TorchSystem>()` → `interactor?.GetComponentInChildren<TorchSystem>()`
- `NetworkSurvivalStats stats` → `SurvivalStats stats`
- `player.GetComponent<NetworkSurvivalStats>()` → `interactor?.GetComponent<SurvivalStats>()`
- `FindFirstObjectByType<NetworkSurvivalStats>()` → `FindFirstObjectByType<SurvivalStats>()`

---

### `Assets/Scripts/Cosmetics/PlayerCosmetics.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Collections;` / `using Unity.Netcode;` 削除
- `[RequireComponent(typeof(NetworkObject))]` → 削除
- `NetworkVariable<FixedString32Bytes> EquippedHat/Pickaxe/TorchSkin/Accessory` →
  `private string _equippedHat = ""; public string EquippedHat => _equippedHat;` 等
- `OnNetworkSpawn()` → `Start()` に変更 (ApplySlot呼び出しのみ)
- `OnNetworkDespawn()` → 削除
- `OnHat/Pickaxe/TorchSkin/AccessoryChanged` メソッド → 削除
- `RequestEquip()`: isNetworked 分岐削除、直接 `EquipLocal(category, itemId)` に統合
- `[ServerRpc] EquipServerRpc` → 削除
- `OwnerClientId` 参照削除
- `NetworkManager.Singleton` 参照削除

---

### `Assets/Scripts/Audio/ProximityAudioSource.cs`
- `NetworkBehaviour` → `MonoBehaviour`
- `using Unity.Netcode;` 削除
- `IsLocalPlayer` プロパティ: `NetworkBootstrapper.IsStandaloneMode` / `IsOwner` → 常に `true` を返す
  (シングルプレイでは常にローカルプレイヤー)

---

### `Assets/Scripts/UI/LobbyUI.cs`
- `using Unity.Netcode;` / `using Unity.Netcode.Transports.UTP;` 削除
- `NetworkBootstrapper networkBootstrapper` フィールド → 削除
- `[SerializeField] Button hostButton/joinButton` → 削除
- `[SerializeField] TMP_InputField ipInputField` → 削除
- `[SerializeField] ushort port` → 削除
- `OnSoloClicked()`: `networkBootstrapper.StartStandalone()` 削除
  → `CaveGenerator.Generate()` または `SimpleSpawner` のトリガーに変更
  → `UIFlowController.Instance?.GoTo(UIScreen.Playing)` は残す
- `OnHostClicked()` → 削除
- `OnJoinClicked()` → 削除
- `ApplyTransportSettings()` → 削除
- `Awake()` の hostButton/joinButton AddListener → 削除

---

### `Assets/Scripts/UI/HUDAnimator.cs`
- `NetworkSurvivalStats _stats` → `SurvivalStats _stats`
- `BindToPlayer(NetworkSurvivalStats stats, TorchSystem torchSystem)` → `BindToPlayer(SurvivalStats stats, ...)`
- `_stats.Health.Value / .Oxygen.Value / .Hunger.Value` → `_stats.Health / .Oxygen / .Hunger`
- `_stats.Health.OnValueChanged += OnHealthChanged` → `_stats.OnHealthChanged += OnHealthChanged`
  (SurvivalStats側でイベントを用意)
- `PollStandalone()` のポーリングロジック → そのまま維持（但し .Value → プロパティ名変更）
- `UnbindAll()` の OnValueChanged解除 → SurvivalStatsのイベント解除に変更

---

### `Assets/Scripts/UIManager.cs`
- `NetworkSurvivalStats _boundStats / _pollStats` → `SurvivalStats _boundStats / _pollStats`
- `BindStandalonePlayer(TorchSystem, NetworkSurvivalStats)` → `BindStandalonePlayer(TorchSystem, SurvivalStats)`
- `BindToLocalPlayer(TorchSystem, NetworkSurvivalStats)` → `BindToPlayer(TorchSystem, SurvivalStats)` に改名・統合
- `BindSurvivalStats(NetworkSurvivalStats)` → `BindSurvivalStats(SurvivalStats)` に変更
- `_boundStats.Health.OnValueChanged` 等 → SurvivalStatsのイベントに変更
- `_pollStats.Health.Value/.Oxygen.Value` 等 → `_pollStats.Health/.Oxygen` 等のプロパティに
- `connectionStatusText` / `waitingMessage` フィールド → 削除（シングルプレイでは不要）

---

### `Assets/Scripts/GameManager.cs`
- `psm.CurrentState.Value != PlayerState.Downed` → `psm.CurrentState != PlayerState.Downed`
  (PlayerStateManager.CurrentState がプロパティに変わるため)

---

## KEEP対象（変更不要）

- `Assets/Scripts/PlayerMovement.cs` - MonoBehaviour、PlayerStateManager経由でStateを参照するが内部的には `_stateManager.IsSpawned` / `CurrentState.Value` を参照 → **要確認**: IsSpawned削除後に変更が必要
- `Assets/Scripts/TorchSystem.cs` - MonoBehaviour、`externalControl` フラグはシングル用に残しても無害
- `Assets/Scripts/Cave/CaveGenerator.cs` - MonoBehaviour、NGO依存なし
- `Assets/Scripts/Cave/CaveContentPlacer.cs` - MonoBehaviour、NGO依存なし
- `Assets/Scripts/Cave/GlowCrystal.cs` - MonoBehaviour
- `Assets/Scripts/Cave/CaveVisualizer.cs` - MonoBehaviour
- `Assets/Scripts/Cave/MeshGenerator.cs` - MonoBehaviour
- `Assets/Scripts/Cave/NoiseSettings.cs` - struct
- `Assets/Scripts/Cave/MarchingCubesTable.cs` - static class
- `Assets/Scripts/Cave/CaveNoiseGenerator.cs` - MonoBehaviour
- `Assets/Scripts/Cave/CaveChunk.cs` - MonoBehaviour
- `Assets/Scripts/EscapeGate.cs` - MonoBehaviour（未確認だが依存なしと想定）
- `Assets/Scripts/ResultUI.cs` - MonoBehaviour（未確認だが依存なしと想定）
- `Assets/Scripts/Cosmetics/CosmeticDatabase.cs` - ScriptableObject
- `Assets/Scripts/Cosmetics/PlayerCosmeticSaveData.cs` - MonoBehaviour
- `Assets/Scripts/Cosmetics/CosmeticShopUI.cs` - MonoBehaviour
- `Assets/Scripts/Audio/FootstepAudio.cs` - MonoBehaviour（ProximityAudioSource参照はあるが型は変わる）
- `Assets/Scripts/Audio/ProximityAudioManager.cs` - MonoBehaviour
- `Assets/Scripts/UI/UIFlowController.cs` - MonoBehaviour
- `Assets/Scripts/UI/TitleScreenUI.cs` - MonoBehaviour
- `Assets/Scripts/UI/OptionsUI.cs` - MonoBehaviour
- `Assets/Scripts/UI/PauseManager.cs` - MonoBehaviour
- `Assets/Scripts/Editor/PrefabApplyHelper.cs` - Editor専用

### 要注意のKEEP
- `PlayerMovement.cs` L54: `_stateManager.IsSpawned` → `IsSpawned` プロパティがなくなるため削除
- `PlayerMovement.cs` L55: `_stateManager.CurrentState.Value` → `_stateManager.CurrentState`

---

## 変換クイックリファレンス

| Before (NGO) | After (シングルプレイ) |
|---|---|
| `using Unity.Netcode;` | 削除 |
| `: NetworkBehaviour` | `: MonoBehaviour` |
| `NetworkVariable<T> _var = new(...)` | `T _var; public T VarName { get; private set; }` |
| `_var.Value` | `_var` (プロパティ) |
| `_var.OnValueChanged += cb` | 独自イベント `OnVarChanged` を定義 |
| `[ServerRpc] void FooServerRpc()` | `void Foo()` |
| `[ClientRpc] void FooClientRpc()` | 削除またはローカル呼び出し |
| `OnNetworkSpawn()` | `void Start()` にマージ |
| `OnNetworkDespawn()` | `void OnDestroy()` にマージ |
| `IsOwner / IsServer / IsHost` チェック | 削除（常にtrue扱い） |
| `if (isNetworked && !IsServer) return;` | 削除 |
| `NetworkManager.Singleton != null && ...IsListening` | 削除 |
| `NetworkObject.Despawn(true)` | `Destroy(gameObject)` |
| `GetComponent<NetworkObject>().Spawn()` | `Instantiate(...)` のみ |
| `NetworkSurvivalStats` | `SurvivalStats` |
| `NetworkTorchAdapter` | `TorchSystem` (直接参照) |
| `NetworkPlayer player` (Interact引数) | `GameObject interactor` |

---

## 依存関係グラフ (削除される型の参照元)

### `NetworkPlayer` 型を参照:
- PlayerInputController.cs (削除)
- FirstPersonLook.cs (NetworkPlayer._networkPlayer フィールド → 削除)
- PlayerClimbing.cs (RequireComponent → 削除)
- PlayerInteractor.cs (InteractServerRpc内 → 削除)
- RopeController.cs (Interact引数 → 変更)
- ResourceItem.cs (Interact引数 → 変更)
- CollectibleGem.cs (Interact引数 → 変更)
- DownedReviveInteractable.cs (Interact引数 → 変更)
- PlacedResourceItem.cs (Interact引数 → 変更)
- IInteractable.cs (Interact引数 → 変更)

### `NetworkSurvivalStats` 型を参照:
- PlayerStateManager.cs → SurvivalStats に変更
- PlayerClimbing.cs → SurvivalStats に変更
- BatAI.cs (後方互換フィールド) → SurvivalStats に変更
- BatSpawner.cs → SurvivalStats に変更
- BatPerception.cs → SurvivalStats に変更
- HUDAnimator.cs → SurvivalStats に変更
- UIManager.cs → SurvivalStats に変更
- ResourceItem.cs → SurvivalStats に変更
- DownedReviveInteractable.cs → SurvivalStats に変更
- PlacedResourceItem.cs → SurvivalStats に変更

### `NetworkTorchAdapter` 型を参照:
- PlayerInputController.cs → 削除
- BatSpawner.cs → 削除
- BatPerception.cs → 削除
- ResourceItem.cs → TorchSystem に変更

### `NetworkBootstrapper` 型を参照:
- LobbyUI.cs → 削除
- ProximityAudioSource.cs → 削除
- BatPerception.cs → 削除

---

## 実行順序 (推奨)

1. `NetworkSurvivalStats.cs` を参考に `SurvivalStats.cs` を新規作成 (StatType enumも含む)
2. `Network/` フォルダの6ファイルを削除
3. `IInteractable.cs` の Interact シグネチャを変更
4. `PlayerStateManager.cs` をREWRITE (NetworkVariable→プロパティ)
5. `SurvivalStats` を参照する全スクリプトを一括REWRITE
6. 残りのREWRITE対象を処理
7. `SimpleSpawner.cs` を新規作成
8. `LobbyUI.cs` を簡略化
9. コンパイルエラーがゼロになることを確認
