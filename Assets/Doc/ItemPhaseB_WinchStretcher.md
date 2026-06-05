# Phase B — 担架 NGO / ウインチ物理 設計

GDD §8.3 に沿った Phase B の実装方針。Phase A 完了後の初版。

## 1. 担架（NetworkStretcherSync）

### 現状（Phase B 初版）

| 項目 | 実装 |
|------|------|
| `NetworkObject` 付与 | `ItemRuntimeFactory` が Stretcher 生成時に追加 |
| `NetworkStretcherSync` | 同上（端 A/B の carrier ClientId 同期） |
| サーバースポーン | `NetworkRuntimeItemSpawn` — ロッカー購入・山中ドロップ時 `IsServer` で `Spawn()` |
| ローカル E 操作 | `PlayerInteraction` → `RequestAttachServerRpc` / `RequestDetachServerRpc` |

### 同期フロー

```
Client E → RequestAttachServerRpc
  → Server: _carrierA/_carrierB NetworkVariable 更新
  → All: OnCarrierAChanged → StretcherItem.TryAttach(player)
```

### 残タスク（Phase B+）

- [x] 担架 `NetworkObject` を DefaultNetworkPrefabs に登録（Editor: `Peak Plunder → Network → Build Runtime Item Prefabs`）
- [x] 展開/折畳・遺物載せの ServerRpc（`_expanded` / `_mountedRelic` NetworkVariable）
- [x] ホスト/クライアント切断時 `ForceDetach`（`NetworkRuntimeItemDisconnectHandler`）
- [x] 設置済み担架の Transform 同期（`NetworkTransform` + `NetworkRigidbody`）

## 2. ウインチケーブル（WinchCableChain）

### 現状（Phase B 初版）

| 項目 | GDD | 初版実装 |
|------|-----|---------|
| セグメント数 | 20 | 20（定数 `DefaultSegmentCount`） |
| 最大長 | 20m | `_maxCableLength` |
| Joint | ConfigurableJoint チェーン | 各 Capsule + ConfigurableJoint（Limited） |
| 巻上 | Joint 距離短縮 1.5m/s | `linearLimit` を毎 Reel で短縮 + AddForce |
| 張力切断 | 5000N | `PortableWinchItem.CheckCableBreak`（既存） |
| ドライバ差替 | — | `IWinchCableDriver` — Chain / System |

`PortableWinchItem` は **WinchCableChain を既定**、`WinchCableSystem`（SpringJoint 1球）はレガシー無効化。

### 残タスク（Phase B+）

- [x] ケーブル展開/巻上/切断/回収の **ServerRpc**（`NetworkPortableWinchSync`、ホスト権威物理）
- [x] `RelicGrabPoint` への接続（ウインチ E 接続時に優先）
- [x] セグメント Collider 有効化と地形擦れ（`_enableSegmentColliders` + 摩擦 PhysicMaterial）
- [x] `EstimateTension` を Joint force ベースに改善（`GetCurrentForces` + 伸び補正）
- [x] NGO: クライアント LineRenderer 補間（`_hookWorldPosition` NetworkVariable + `ApplyClientHookPosition`）

## 3. ロングロープ遺物括り（GDD §4.6）

| 操作 | 実装 |
|------|------|
| ロングロープ手持ち + 遺物 2m 以内 + **E** | `PlayerInteraction.TryAttachLongRopeToRelic` |
| 物理接続 | `RopeManager.ConnectPlayerToRelic` → `PlayerRopeSystem` |
| 遺物アンカー | `RelicGrabPoint`（全 `RelicBase` に自動付与） |
| 張力切断 | **2500N**（`ShopRopeConstants.LongRopeBreakForce`） |
| NGO 同期 | `PlayerShopRopeNetworkBridge`（ServerRpc + ClientRpc） |
| 切断 | `LongRopeItem.CutRope()` / 破損時 |

## 4. Prefab 初回セットアップ

Unity Editor で **1 回** 実行:

```
Peak Plunder → Network → Build Runtime Item Prefabs
```

- `Assets/Resources/NetworkItems/StretcherNetworkItem.prefab`
- `Assets/Resources/NetworkItems/PortableWinchNetworkItem.prefab`
- `Assets/DefaultNetworkPrefabs.asset` に自動追加

## 5. 検証手順

### 担架 NGO（2 クライアント）

1. Host + Client で `SandboxOfflineCombined`
2. 担架購入 → ロッカーから E で拾う
3. Client A / B がそれぞれ E で担架端を掴む
4. 一方が切断 → 担架から外れる（`ForceDetach`）
5. 地面に置いた担架が Late Join クライアントでも同位置に見える

### ウインチチェーン

1. ウインチ R 設置 → E ケーブル → E 遺物/キューブ接続 → R 巻上
2. Client 側で LineRenderer がフック位置に追従する
3. 張力超過で X / 自動切断

### ロングロープ括り（Co-op）

1. Player A がロングロープで遺物に E
2. Player B も同じ遺物-ロープ接続が見える
3. Player A 切断 → ロープ解放

## 6. 関連ファイル

- `Assets/Sandbox/Script/Network/NetworkRuntimeItemSpawn.cs`
- `Assets/Sandbox/Script/Network/NetworkRuntimeItemDisconnectHandler.cs`
- `Assets/Sandbox/Script/Network/NetworkRuntimeItemPrefabs.cs`
- `Assets/Sandbox/Script/Network/NetworkStretcherSync.cs`
- `Assets/Sandbox/Script/Network/NetworkPortableWinchSync.cs`
- `Assets/Sandbox/Script/Network/PlayerShopRopeNetworkBridge.cs`
- `Assets/Sandbox/Script/Rope/ShopRopeConstants.cs`
- `Assets/Sandbox/Editor/NetworkRuntimeItemPrefabBuilder.cs`
- `Assets/Sandbox/Script/Relic/RelicGrabPoint.cs`
- `Assets/Sandbox/Script/Item/WinchCableChain.cs`
- `Assets/Sandbox/Script/Item/IWinchCableDriver.cs`
- `Assets/Sandbox/Script/Item/Items/PortableWinchItem.cs`
- `Assets/Sandbox/Script/Item/Items/LongRopeItem.cs`
- `Assets/Sandbox/Script/Rope/RopeManager.cs`
