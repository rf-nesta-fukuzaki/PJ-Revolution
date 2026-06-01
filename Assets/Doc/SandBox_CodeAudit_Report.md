# SandBox スクリプト コード監査レポート

> **対象**: `Assets/SandBox/Script/` 以下の全 72 `.cs` ファイル  
> **監査日**: 2026-04-15  
> **監査者**: AI 自動コード監査  

---

## 目次

1. [サマリー](#1-サマリー)
2. [🔴 CRITICAL — 非推奨 API / 廃止予定メソッドの使用](#2--critical--非推奨-api--廃止予定メソッドの使用)
3. [🟠 HIGH — レガシー入力システムの混在](#3--high--レガシー入力システムの混在)
4. [🟡 MEDIUM — パフォーマンス上の問題](#4--medium--パフォーマンス上の問題)
5. [🟡 MEDIUM — ランタイムでのリソース生成（メモリリーク懸念）](#5--medium--ランタイムでのリソース生成メモリリーク懸念)
6. [🟡 MEDIUM — 二重実装・機能重複](#6--medium--二重実装機能重複)
7. [🔵 LOW — 設計上の懸念・改善推奨事項](#7--low--設計上の懸念改善推奨事項)
8. [📊 ファイル別 検出一覧表](#8--ファイル別-検出一覧表)
9. [推奨対応ロードマップ](#9-推奨対応ロードマップ)

---

## 1. サマリー

| 深刻度 | 件数 | 概要 |
|:------:|:----:|------|
| 🔴 CRITICAL | **5** | 非推奨 NGO RPC属性、`#pragma warning disable CS0618` で警告を抑制 |
| 🟠 HIGH | **6** | レガシー `Input` クラスの直接使用（`InputStateReader` 未経由） |
| 🟡 MEDIUM | **17** | `GetComponent` 毎フレーム呼出、`FindObjectsByType` 多用、`new Material` / `Shader.Find` の濫用 |
| 🔵 LOW | **10** | async void パターン、`GameObject.Find` 使用、レガシー互換コード残存 |

**検出問題の総計: 38 件**

---

## 2. 🔴 CRITICAL — 非推奨 API / 廃止予定メソッドの使用

### 2-1. NGO 旧 RPC 属性 `[ServerRpc]` / `[ClientRpc]` の使用（NGO 2.x 非推奨）

NGO 2.x では `[ServerRpc]` / `[ClientRpc]` は非推奨となり、統合 `[Rpc]` 属性に置き換えられています。  
本プロジェクトでは **新旧が混在** しています。

| ファイル | 行 | 使用されている属性 | 状態 |
|----------|----|--------------------|------|
| `NetworkExpeditionSync.cs` | L4, L84, L93, L102, L110, L117 | `[ServerRpc(RequireOwnership = false)]` / `[ClientRpc]` | ❌ 旧属性 + `#pragma warning disable CS0618` |
| `NetworkRelicSync.cs` | L4, L95, L106, L119 | `[ServerRpc(RequireOwnership = false)]` | ❌ 旧属性 + `#pragma warning disable CS0618` |
| `ProximityVoiceChat.cs` | L3, L108 | `[ServerRpc(RequireOwnership = false)]` | ❌ 旧属性 + `#pragma warning disable CS0618` |
| `NetworkBasecampBudgetSync.cs` | L67, L87 | `[Rpc(SendTo.Server)]` | ✅ 新属性（正しい） |
| `NetworkStretcherSync.cs` | L62, L89 | `[Rpc(SendTo.Server)]` | ✅ 新属性（正しい） |
| `GhostSystem.cs` | L98, L176, L182, L243 | `[Rpc(SendTo.Server)]` / `[Rpc(SendTo.ClientsAndHost)]` | ✅ 新属性（正しい） |

**問題点**: 3ファイルで `#pragma warning disable CS0618` を使って非推奨警告を意図的に抑制しています。  
これはコンパイラ警告を隠すだけで、将来のNGOバージョンアップで **コンパイルエラーに変わるリスク** があります。

**推奨修正**:
```diff
- #pragma warning disable CS0618
- [ServerRpc(RequireOwnership = false)]
- public void PickupServerRpc(ulong clientId, ServerRpcParams rpcParams = default)
+ [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
+ public void PickupServerRpc(ulong clientId, RpcParams rpcParams = default)
```

---

### 2-2. `ServerRpcParams` → `RpcParams` 型の不統一

旧属性を使用しているファイルでは `ServerRpcParams` 型がパラメータに使用されていますが、新 `[Rpc]` 属性では `RpcParams` に変更されています。

| ファイル | 行 | 旧型 | 新型 |
|----------|-----|------|------|
| `NetworkRelicSync.cs` | L96, L107, L120 | `ServerRpcParams` | `RpcParams` |
| `ProximityVoiceChat.cs` | - | なし（パラメータ不使用） | - |

---

## 3. 🟠 HIGH — レガシー入力システムの混在

プロジェクト全体では `InputStateReader` による抽象化が行われていますが、一部スクリプトで **レガシー `Input` クラスを直接使用** しています。

### 3-1. `InputStateReader` 未経由の直接 `Input` 使用

| ファイル | 行 | 使用コード | 問題 |
|----------|-----|-----------|------|
| `ClimbingController.cs` | L104 | `Input.GetKeyDown(KeyCode.E)` | `InputStateReader` 未使用 |
| `ClimbingController.cs` | L189 | `Input.GetAxis("Vertical")` | `InputStateReader` 未使用 |
| `PlayerInteraction.cs` | L58-60 | `Input.GetKeyDown(KeyCode.E/F/G)` | `#else` ブランチでレガシー使用 |
| `GhostSystem.cs` | L127-129 | `Input.GetAxis`, `Input.GetKey` | `#else` ブランチでレガシー使用 |
| `GhostSystem.cs` | L150 | `Input.GetMouseButtonDown(0)` | `#else` ブランチでレガシー使用 |

**特に問題**: `ClimbingController.cs` は `#if` 切り替えすらなく、常にレガシー `Input` を使用しています。  
他のスクリプト（`ExplorerCameraLook`, `ExplorerController`）が `InputStateReader` で統一されているのと矛盾しています。

**推奨修正（ClimbingController.cs）**:
```diff
  private void HandleGrabInput()
  {
-     if (Input.GetKeyDown(KeyCode.E))
+     if (InputStateReader.InteractPressedThisFrame())  // ※メソッド名は実装に合わせる
      {
          ...
      }
  }
```

### 3-2. `#if ENABLE_INPUT_SYSTEM` 分岐によるコード二重化

以下のファイルは新旧入力システムの両方をサポートしていますが、コードが事実上2倍に膨らんでいます：

| ファイル | 行範囲 | 内容 |
|----------|--------|------|
| `PlayerInteraction.cs` | L51-61 | E/F/G キー入力の新旧分岐 |
| `GhostSystem.cs` | L114-131, L146-151 | 移動入力 + ピン入力の新旧分岐 |

**推奨**: `InputStateReader` に `InteractPressedThisFrame()` 等を追加し、各スクリプトでは分岐を排除する。

---

## 4. 🟡 MEDIUM — パフォーマンス上の問題

### 4-1. `GetComponent<T>()` の毎フレーム呼び出し

| ファイル | 行 | メソッド | 呼び出し |
|----------|-----|---------|---------|
| `StaminaSystem.cs` | L109 | `GetIsMoving()` → `Update()` 毎フレーム | `GetComponent<Rigidbody>()` |

**問題**: `GetComponent` は反射的検索を行うため、毎フレーム呼び出しはパフォーマンス的に非推奨です。  
**推奨**: `Awake()` でキャッシュする。

```diff
+ private Rigidbody _rb;
+ private void Awake() { _rb = GetComponent<Rigidbody>(); }
  
  private bool GetIsMoving()
  {
-     var rb = GetComponent<Rigidbody>();
-     return rb != null && rb.linearVelocity.sqrMagnitude > 0.5f;
+     return _rb != null && _rb.linearVelocity.sqrMagnitude > 0.5f;
  }
```

### 4-2. `FindObjectsByType<T>()` の頻繁な使用

`FindObjectsByType` はシーン全体を走査する重い処理です。以下の使用箇所を確認してください：

| ファイル | 行 | 呼ばれるタイミング | 対象型 |
|----------|-----|-------------------|--------|
| `ProximityVoiceChat.cs` | L144 | `Update()` 毎フレーム | `SingingVaseRelic` |
| `WeatherSystem.cs` | L174-175 | `FixedUpdate()` 3秒ごと | `Rigidbody`, `CrystalCupRelic` |
| `GhostSystem.cs` | L211 | Coroutine 0.5秒ごと | `ReviveShrine` |
| `MagneticHelmetRelic.cs` | L46 | `FixedUpdate()` 毎物理フレーム | `MagneticTarget` |
| `SingingVaseRelic.cs` | L99 | 条件的 | `RockfallTrigger` |
| `SpawnManager.cs` | L45-46, L91 | `Start()` 1回のみ | `SpawnPoint`, `RouteGate`, `TwinStatueRelic` |
| `ExpeditionManager.cs` | L150 | 条件的呼び出し | `PlayerHealthSystem` |
| `FoodItem.cs` | L55 | 使用時 | `StaminaSystem` |
| `BasecampShop.cs` | L301 | リスト更新時 | `PlayerInventory` |

**特に問題**:
- `ProximityVoiceChat.cs` L144: **毎フレーム** `FindObjectsByType<SingingVaseRelic>` → 重大なパフォーマンス劣化の原因。キャッシュすべき。
- `MagneticHelmetRelic.cs` L46: **毎物理フレーム** `FindObjectsByType<MagneticTarget>` → 同上。

### 4-3. `GameObject.Find()` の使用

| ファイル | 行 | 用途 |
|----------|-----|------|
| `MountainTerrainGenerator.cs` | L241, L273, L312 | ゾーンオブジェクトの名前ベース検索 |

**問題**: 文字列ベースのオブジェクト検索は脆弱（名前変更で壊れる）かつ低速です。  
**推奨**: Inspector からの参照注入（`[SerializeField]`）に置き換える。

---

## 5. 🟡 MEDIUM — ランタイムでのリソース生成（メモリリーク懸念）

### 5-1. `new Material()` / `Shader.Find()` のランタイム生成

`new Material()` はランタイムでマテリアルのコピーを生成し、明示的に `Destroy` しない限りメモリリークします。

| ファイル | 行 | 状況 | リーク可能性 |
|----------|-----|------|-------------|
| `RelicBase.cs` | L234-236 | `VizChildRot()` 内で毎回生成 | 🟡 `BuildVisual` が頻繁に呼ばれると蓄積 |
| `GhostSystem.cs` | L193-196 | `SpawnPinClientRpc()` でピンごとに生成 | 🟡 最大5個×4人分 |
| `RopeManager.cs` | L126 | `CreateDefaultRope()` で生成 | 🟡 接続のたびに生成 |
| `BasecampShop.cs` | L347-348 | プレビュー生成時 | 🟡 ショップ再構築で蓄積 |
| `FlareGunItem.cs` | L61 | フレア発射時 | 🟡 発射のたびに生成 |
| `CharacterCosmeticApplier.cs` | L268-270 | コスメ適用時 | 🟡 キャラ変更のたびに蓄積 |

### 5-2. `new PhysicsMaterial()` のランタイム生成

| ファイル | 行 | 状況 |
|----------|-----|------|
| `IcePatch.cs` | L31 | 踏むたびに新規生成の可能性 |
| `WeatherFrictionAdapter.cs` | L25 | `Awake` で1回のみ（許容範囲） |
| `SlipperyFishStatueRelic.cs` | L36 | `Awake` で1回のみ（許容範囲） |

**`IcePatch.cs` の問題**: `OnTriggerEnter` → `ApplyFriction` で `col.material == null` の場合に毎回 `new PhysicsMaterial` が生成されます。  
同じ Collider が何度もトリガーに入る場合、毎回新規生成は発生しませんが、設計として `Awake` での初期化が推奨されます。

### 5-3. `new MaterialPropertyBlock()` の不要なインスタンス化

| ファイル | 行 | 問題 |
|----------|-----|------|
| `GrabPoint.cs` | L43 | `SetHighlight()` が呼ばれるたびに `new MaterialPropertyBlock()` → フィールドにキャッシュすべき |
| `ReviveShrine.cs` | L46 | 同上 |

**推奨**: `MaterialPropertyBlock` をフィールドに保持して再利用する。
```diff
+ private readonly MaterialPropertyBlock _mpb = new();

  public void SetHighlight(bool on)
  {
-     var mpb = new MaterialPropertyBlock();
-     _markerRenderer.GetPropertyBlock(mpb);
-     mpb.SetColor("_BaseColor", on ? _highlightColor : _defaultColor);
-     _markerRenderer.SetPropertyBlock(mpb);
+     _markerRenderer.GetPropertyBlock(_mpb);
+     _mpb.SetColor("_BaseColor", on ? _highlightColor : _defaultColor);
+     _markerRenderer.SetPropertyBlock(_mpb);
  }
```

---

## 6. 🟡 MEDIUM — 二重実装・機能重複

### 6-1. `StretcherItem` — レガシー API と新 API の二重実装

`StretcherItem.cs` には **2つの担ぎ手管理 API** が共存しています:

| API | メソッド | 用途 |
|-----|---------|------|
| **新API**（`PlayerInteraction` 連携） | `TryAttach()`, `Detach()` | `PlayerInteraction` からの呼び出し |
| **レガシーAPI** | `SetCarrierA()`, `SetCarrierB()`, `ReleaseCarrierA()` | コメントに「レガシー互換」と明記（L129） |

**問題**: レガシー API は `_driverA`/`_driverB` を設定せず `_carrierA`/`_carrierB` のみを操作するため、状態の不整合が発生する可能性があります（例: `IsEndAFree` は `_driverA == null` を見るが、`SetCarrierA` は `_driverA` を設定しない）。

**推奨**: レガシー API を削除し、`TryAttach()` / `Detach()` に統一する。  
`NetworkStretcherSync` が正しく `TryAttach` / `Detach` 経由で動作することを確認後、L129-138 を削除してください。

### 6-2. `RelicPackingBuffer` と `RelicBase` の `OnCollisionEnter` 二重処理

`RelicPackingBuffer.cs` と `RelicBase.cs` は **同一 GameObject にアタッチ** され、両方が `OnCollisionEnter` を持ちます。

| コンポーネント | OnCollisionEnter の処理 |
|---------------|------------------------|
| `RelicBase` | ダメージ計算 → `ApplyDamage()` |
| `RelicPackingBuffer` | 負ダメージ（回復）で「軽減」を近似 |

**問題点**:
1. Unity の同一 GO 上での `OnCollisionEnter` 呼び出し順序は **不定** です（コメントでも注意喚起あり L27）。
2. `RelicBase` がダメージを適用 → その後 `RelicPackingBuffer` が負ダメージで回復、という順序が保証されないため、一瞬 HP=0 で破壊判定が走る可能性があります。

**推奨**: `RelicBase.CalculateDamage()` を virtual にして `RelicPackingBuffer` がダメージ計算段階で軽減率を適用する設計に変更する。

### 6-3. `LobbyManager` — `SetRelayServerData` と `SetClientRelayData` の重複

| メソッド | 行 | 実装 |
|---------|-----|------|
| `SetRelayServerData()` | L220-224 | `transport.SetRelayServerData(new RelayServerData(allocation, "dtls"))` |
| `SetClientRelayData()` | L226-230 | `transport.SetRelayServerData(new RelayServerData(allocation, "dtls"))` |

**問題**: 2つのメソッドは完全に同一の処理です。1つに統合すべきです。

### 6-4. `WeatherFrictionAdapter` と `IcePatch` の摩擦制御の重複

両方ともプレイヤーの `PhysicsMaterial` の `dynamicFriction` / `staticFriction` を操作します。

| コンポーネント | トリガー | 摩擦値 |
|---------------|---------|--------|
| `WeatherFrictionAdapter` | 天候変化イベント | 0.05 〜 0.6 |
| `IcePatch` | `OnTriggerEnter`/`Exit` | 0.02 / 0.6 |

**問題**: `IcePatch` は `col.material` を直接操作し、`WeatherFrictionAdapter` は独自の `PhysicsMaterial` を生成して `_collider.material` に設定します。天候変化と氷面ハザードが **同時に発生** した場合、どちらの値が有効になるか不定です。

**推奨**: 摩擦の管理を統合するか、優先度ルールを明確にする。

---

## 7. 🔵 LOW — 設計上の懸念・改善推奨事項

### 7-1. `async void` パターンの多用

`async void` は例外が呼び出し元に伝播しないため、Unity では推奨されません。

| ファイル | 行 | メソッド |
|----------|-----|---------|
| `LobbyManager.cs` | L173, L192 | `HandleHeartbeat()`, `HandleLobbyPoll()` — `Update` から毎フレーム呼出 |
| `NetworkBootstrap.cs` | L34, L77 | `Start()`, `Retry()` |
| `MainMenuManager.cs` | L110, L118, L133, L142 | `OnCreateRoom()`, `OnJoinRoom()`, `OnStartGame()`, `OnLeaveRoom()` |
| `VivoxVoiceService.cs` | L68, L95 | `JoinChannel()`, `LeaveChannel()` |

**特に問題**: `HandleHeartbeat()` / `HandleLobbyPoll()` は `Update()` から毎フレーム呼ばれますが、`async void` のため前の Task が完了する前に次のフレームで再度呼ばれる可能性があります。`_heartbeatTimer` でガードされていますが、設計として脆弱です。

**推奨**: `UniTask` の `void` を使うか、少なくとも再入防止フラグを追加する。

### 7-2. `Camera.main` のフォールバック使用

| ファイル | 行 | 状況 |
|----------|-----|------|
| `PlayerInteraction.cs` | L40 | `Camera.main?.transform` をフォールバックで使用 |
| `VivoxVoiceService.cs` | L147 | `Camera.main` を直接使用 |

**問題**: `Camera.main` は内部的に `FindGameObjectWithTag("MainCamera")` を呼び出すため低速です（Unity 2020.2+ ではキャッシュされていますが、マルチカメラ構成で問題となることがあります）。

### 7-3. マジックストリング — アイテム名での文字列比較

| ファイル | 行 | コード |
|----------|-----|--------|
| `ClimbingController.cs` | L201 | `inv.HasItem("アイスアックス")` |
| `CharacterCosmeticApplier.cs` | L110, L224, L237 | `skinId == "skin_ninja"`, `packId == "pack_large"` etc. |

**推奨**: アイテムIDを `enum` や `ScriptableObject` に定義し、文字列比較を排除する。

### 7-4. `NetworkExpeditionSync` — 旧 `[ClientRpc]` 属性のメソッドシグネチャ

`ShowResultClientRpc` (L117-118) は旧 `[ClientRpc]` 属性ですが、`GhostSystem.cs` では同様の機能が新 `[Rpc(SendTo.ClientsAndHost)]` で実装されています。統一すべきです。

### 7-5. シングルトンの DontDestroyOnLoad 非統一

| ファイル | DDOL対応 | 備考 |
|----------|----------|------|
| `LobbyManager.cs` | ✅ `DontDestroyOnLoad` + `transform.SetParent(null)` | 正しい |
| `NetworkBootstrap.cs` | ✅ 同上 | 正しい |
| `ScoreTracker.cs` | ❌ なし | シーン遷移で破棄される |
| `ExpeditionManager.cs` | ❌ なし | 同上 |
| `SpawnManager.cs` | ❌ なし | 同上（ゲームシーン内のみなら許容） |
| `WeatherSystem.cs` | ❌ なし | 同上 |
| `RopeManager.cs` | ❌ なし | 同上 |
| `BasecampShop.cs` | ❌ なし | 同上 |
| `CosmeticManager.cs` | ❌ なし | 同上 |
| `ExpeditionHUD.cs` | ❌ なし | 同上 |

ゲームデザインによってはシーン内限定シングルトンで問題ありませんが、意図が統一されているか確認が必要です。

---

## 8. 📊 ファイル別 検出一覧表

| # | ファイル | 🔴 | 🟠 | 🟡 | 🔵 | 主な検出内容 |
|---|---------|:---:|:---:|:---:|:---:|-------------|
| 1 | `ExplorerCameraLook.cs` | - | - | - | - | 問題なし |
| 2 | `ExplorerController.cs` | - | - | - | - | 問題なし |
| 3 | `ClimbingController.cs` | - | **2** | - | 1 | `Input.GetKeyDown/GetAxis` 直接使用、マジックストリング |
| 4 | `GrabPoint.cs` | - | - | 1 | - | `MaterialPropertyBlock` 毎回生成 |
| 5 | `CollapsiblePlatform.cs` | - | - | - | - | 問題なし |
| 6 | `IcePatch.cs` | - | - | 2 | - | `PhysicsMaterial` ランタイム生成、摩擦制御重複 |
| 7 | `RockDamageOnCollision.cs` | - | - | - | - | 問題なし |
| 8 | `RockfallTrigger.cs` | - | - | - | - | 問題なし |
| 9 | `ItemBase.cs` | - | - | - | - | 問題なし |
| 10 | `RelicPackingBuffer.cs` | - | - | 1 | - | `OnCollisionEnter` 二重処理 |
| 11 | `StretcherItem.cs` | - | - | 1 | - | レガシー API 残存 |
| 12 | `PlayerHealthSystem.cs` | - | - | - | - | 問題なし |
| 13 | `PlayerInteraction.cs` | - | 1 | - | 1 | レガシー `Input` 分岐、`Camera.main` フォールバック |
| 14 | `PlayerInventory.cs` | - | - | - | - | 問題なし |
| 15 | `StaminaSystem.cs` | - | - | 1 | - | `GetComponent` 毎フレーム |
| 16 | `WeatherFrictionAdapter.cs` | - | - | 1 | - | 摩擦制御重複 |
| 17 | `LobbyManager.cs` | - | - | 1 | 1 | 重複メソッド、`async void`×2 |
| 18 | `NetworkBasecampBudgetSync.cs` | - | - | - | - | 問題なし（新API使用） |
| 19 | `NetworkBootstrap.cs` | - | - | - | 1 | `async void` |
| 20 | `NetworkExpeditionSync.cs` | **3** | - | - | 1 | 旧RPC属性×3、旧ClientRpc |
| 21 | `NetworkPlayerSpawner.cs` | - | - | - | - | 問題なし |
| 22 | `NetworkRelicSync.cs` | **2** | - | - | - | 旧ServerRpc属性×3、`ServerRpcParams` 型 |
| 23 | `NetworkStretcherSync.cs` | - | - | - | - | 問題なし（新API使用） |
| 24 | `ProximityVoiceChat.cs` | **1** | - | 1 | - | 旧ServerRpc属性、`FindObjectsByType` 毎フレーム |
| 25 | `VivoxVoiceService.cs` | - | - | - | 2 | `async void`、`Camera.main` |
| 26 | `RelicBase.cs` | - | - | 1 | - | `new Material` + `Shader.Find` |
| 27 | `RelicCarrier.cs` | - | - | - | - | 問題なし |
| 28 | `RelicDamageTracker.cs` | - | - | - | - | 問題なし |
| 29 | `MagneticTarget.cs` | - | - | - | - | 問題なし |
| 30 | `MagneticHelmetRelic.cs` | - | - | 1 | - | `FindObjectsByType` 毎物理フレーム |
| 31 | `SingingVaseRelic.cs` | - | - | 1 | - | `FindObjectsByType` 条件的 |
| 32 | `GhostSystem.cs` | - | 2 | 1 | - | レガシー `Input` 分岐、`FindObjectsByType` Coroutine、`new Material` |
| 33 | `MountainTerrainGenerator.cs` | - | - | - | 1 | `GameObject.Find`×3 |
| 34 | `ReviveShrine.cs` | - | - | 1 | - | `MaterialPropertyBlock` 毎回生成 |
| 35 | `RopeManager.cs` | - | - | 1 | - | `new Material` + `Shader.Find` |
| 36 | `BasecampShop.cs` | - | - | 1 | - | `new Material` + `Shader.Find` |
| 37 | `MainMenuManager.cs` | - | - | - | 1 | `async void`×4 |
| 38 | `CharacterCosmeticApplier.cs` | - | - | 1 | 1 | `new Material`、マジックストリング |
| 39 | `FlareGunItem.cs` | - | - | 1 | - | `new Material` |
| 40 | `WeatherSystem.cs` | - | - | 1 | - | `FindObjectsByType` 3秒ごと |
| 41 | `FoodItem.cs` | - | - | 1 | - | `FindObjectsByType` |
| 42-72 | その他 Item/Relic/UI | - | - | - | - | 特記なし |

---

## 9. 推奨対応ロードマップ

### Phase 1: 即時対応（🔴 CRITICAL）
1. **NGO RPC属性の統一** — `NetworkExpeditionSync.cs`, `NetworkRelicSync.cs`, `ProximityVoiceChat.cs` の `[ServerRpc]`/`[ClientRpc]` を `[Rpc]` に移行
2. **`#pragma warning disable CS0618` の除去** — 上記移行完了後に削除
3. **`ServerRpcParams` → `RpcParams` 型への変更**

### Phase 2: 短期対応（🟠 HIGH）
4. **`ClimbingController.cs` のレガシー `Input` 除去** — `InputStateReader` に統一
5. **`PlayerInteraction.cs` / `GhostSystem.cs` の入力分岐統合** — `InputStateReader` にメソッド追加して分岐を排除

### Phase 3: 中期対応（🟡 MEDIUM）
6. **`StaminaSystem.GetIsMoving()` の `GetComponent` キャッシュ化**
7. **`FindObjectsByType` の毎フレーム呼び出し排除** — 特に `ProximityVoiceChat.cs`, `MagneticHelmetRelic.cs`
8. **`StretcherItem` レガシー API 削除**
9. **`RelicPackingBuffer` のダメージ軽減方式を `CalculateDamage` オーバーライドに変更**
10. **`MaterialPropertyBlock` のキャッシュ化** — `GrabPoint`, `ReviveShrine`
11. **`LobbyManager.SetRelayServerData` / `SetClientRelayData` の統合**

### Phase 4: 長期対応（🔵 LOW）
12. マジックストリングの `enum` / `ScriptableObject` 化
13. `async void` パターンの `UniTask` 移行
14. `GameObject.Find` の Inspector 参照化
15. `Shader.Find` / `new Material` の削減（アセット化）

---

> **注意**: 本レポートは静的コード解析に基づくものです。実行時の挙動やプロジェクト固有のパフォーマンス要件によっては、一部の指摘が許容範囲となる場合があります。特に SandBox ディレクトリはプロトタイプ目的で使用されている可能性があるため、本番環境への移植時に優先的に対応することを推奨します。
