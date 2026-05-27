# Stage 01 MAP — 実装メモ

## Unity実行環境

この作業環境では Unity 6000.3.8f1 の実行ファイルが見つからない。

確認済み:
- `Unity` / `Unity.exe` はPATH上に存在しない
- 標準インストール先候補から `Unity.exe` を検出できない
- Unity Installer の代表的なレジストリキーからも検出できない

Unity Editorでのシーン生成・PlayMode検証は、Unity環境が入っている別PCで実行する。

## 別PCで実行する作業

1. Unity 6000.3.8f1 でプロジェクトを開く
2. メニューから `Peak Plunder > Stage01 > Build Gameplay Scene` を実行する
3. `Assets/Sandbox/Scenes/Gameplay.unity` にStage01構造が生成されることを確認する
4. コンソールの `[Stage01 Validate]` ログで必須構造・個数・Manager参照が `OK` になることを確認する
5. PlayModeで登山ルート、SpawnManager、RouteGate、IcePatch、SummitGoalを検証する

## 実装方式

大量のScene YAML手編集は避け、`Assets/Sandbox/Script/Editor/Stage01MapBuilder.cs` のUnity Editor APIでシーンを構築する。

## Editorメニュー

- `Peak Plunder > Stage01 > Build Gameplay Scene`
  - Stage01のHierarchy、Terrain、岩、木、祠、RouteGate、SpawnPoint、IcePatch、SummitGoalを生成する
  - 生成後に自動で検証も実行する
- `Peak Plunder > Stage01 > Validate Gameplay Scene`
  - 生成済みシーンの必須構造と個数を再検証する

## 今回実装したファイル

### Runtime Script

| ファイル | 役割 |
|---|---|
| `Assets/Sandbox/Script/System/SummitGoalTrigger.cs` | Zone6山頂到達トリガー。Playerタグ侵入時に `ExpeditionManager.ReturnToBase(true)` を呼ぶ |
| `Assets/Sandbox/Script/System/ZoneCheckpoint.cs` | ゾーン境界チェックポイント。Playerタグ侵入時に `ExpeditionManager.OnCheckpointReached(index)` を呼ぶ |
| `Assets/Sandbox/Script/System/SpawnManager.cs` | `RunAllLayers()` 実行時にSpawnPoint/RouteGateキャッシュを遅延初期化するよう補強 |
| `Assets/Sandbox/Script/Hazard/TempleTraps.cs` | `PressurePlateArrow` の未設定射出口ガードと、矢ProjectileのTrigger/Rigidbody設定を追加 |
| `Assets/Sandbox/Script/Audio/SoundId.cs` | Stage01用 `Checkpoint` / `Summit` SE IDを追加 |

### Editor Script

| ファイル | 役割 |
|---|---|
| `Assets/Sandbox/Script/Editor/Stage01MapBuilder.cs` | Stage01 MAPをUnity Editor APIで一括生成するメインビルダー |
| `Assets/Sandbox/Script/Editor/Stage01MapValidator.cs` | 生成後のHierarchy、個数、Manager参照、生成Assetを検証する |
| `Assets/Sandbox/Script/Editor/GrappableRockPlacer.cs` | `GrappableRocks` 配下に53個以上のグラップル岩を自動配置する |

### Project Settings

| ファイル | 変更 |
|---|---|
| `ProjectSettings/TagManager.asset` | `SummitGoal` タグ、`Grappable` / `Hazard` レイヤーを追加 |

## 生成されるファイル形式・配置

`Stage01MapBuilder` 実行時に、以下のUnityアセットが生成または更新される。

| 種類 | 生成先 | 内容 |
|---|---|---|
| Scene | `Assets/Sandbox/Scenes/Gameplay.unity` | Stage01の実体シーン。Hierarchy、Terrain、プロップ、SpawnPointを配置 |
| TerrainData | `Assets/Sandbox/Terrain/MountainTerrainData.asset` | `MountainTerrainGenerator.Generate()` が生成するTerrainData |
| Material | `Assets/Sandbox/Materials/Stage01/*.mat` | 岩、木、神殿、氷、雪、金属などのURP/Litマテリアル |
| Prefab | `Assets/Sandbox/Prefabs/Hazards/*.prefab` | `Stage01_RockfallTrigger`、`Stage01_CollapsiblePlatform`、`Stage01_TempleTrap` |
| Prefab | `Assets/Sandbox/Prefabs/Items/Stage01_FieldSupply.prefab` | 山中ドロップアイテム用の仮Prefab |

## 生成される主なHierarchy

```text
GameManager
World
  Mountain
  Basecamp
  Zone1_Forest
  Zone2_RockySlope
  Zone3_CliffWall
  Zone4_TempleRuins
  Zone5_IceWall
  Zone6_Summit
GrappableRocks
IcePatches
Checkpoints
RouteGates
RelicSpawnPoints
PlayerSpawnPoints
HazardSpawnPoints
```

## Stage01生成後の検証項目

`Stage01MapValidator` は以下を確認する。

- 必須RootとZone markerが存在する
- `GrappableRocks` が50個以上
- Zone1の木が20本以上
- `IcePatch` が12個以上
- `RouteGate` が4個以上
- `ReviveShrine` が6個以上
- `ZoneCheckpoint` が5個以上
- Relic SpawnPointが9個
- Hazard SpawnPointが9個
- Item SpawnPointが5個
- `GameManager` に `SpawnManager` / `ExpeditionManager` / `ScoreTracker` / `AudioManager` がある
- `ReturnPoint` に `ReturnZone` / `NetworkObject` がある
- 生成Prefabと `MountainTerrainData.asset` が存在する

## 既知の注意点

- このPCではUnityを起動できないため、`Build Gameplay Scene` とPlayMode確認は未実行。
- `DefaultSoundLibrary` に `Checkpoint` / `Summit` の実SEが未登録の場合、音は鳴らないが例外にはならない。
- `ExpeditionManager._resultScreen` や `_fadeCanvas` は既存UI構成に依存するため、Unity側で必要に応じて接続確認する。
- 実際のプレイヤー生成・グラップル動作は、Unity入りPCで `PlayerPrefab` / `NetworkPlayerSpawner` / `OfflineTestBootstrapper` と合わせて確認する。

## 関連コミット

- `58b92fc` `feat: Stage01 MAP initial editor build`
- `8761932` `feat: add Stage01 MAP validation tooling`
- `6e5d317` `fix: harden Stage01 MAP editor build`
