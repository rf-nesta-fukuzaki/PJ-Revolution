# Stage 01 MAP — Cursorクイックリファレンス

MAP実装中に最もよく参照するコード・設定のまとめ。

---

## 重要なファイルパス一覧

```
# 地形
Assets/Sandbox/Script/System/MountainTerrainGenerator.cs
Assets/Sandbox/Terrain/MountainTerrainData.asset

# スポーンシステム
Assets/Sandbox/Script/System/SpawnManager.cs
Assets/Sandbox/Script/System/SpawnPoint.cs       → SpawnLayer enum 定義あり
Assets/Sandbox/Script/System/RouteGate.cs

# ゲームフロー
Assets/Sandbox/Script/System/ExpeditionManager.cs
Assets/Sandbox/Script/System/ExpeditionEvents.cs

# ハザード
Assets/Sandbox/Script/Hazard/RockfallTrigger.cs
Assets/Sandbox/Script/Hazard/IcePatch.cs
Assets/Sandbox/Script/Hazard/CollapsiblePlatform.cs
Assets/Sandbox/Script/Hazard/TempleTraps.cs

# 祠
Assets/Sandbox/Script/System/ReviveShrine.cs

# プレイヤー
Assets/Sandbox/Script/Player/ExplorerController.cs
Assets/Sandbox/Script/Player/PlayerHealthSystem.cs

# サウンド
Assets/Sandbox/Script/Audio/SoundId.cs   ← SE名の enum
Assets/Sandbox/Script/Audio/AudioManager.cs

# シーン
Assets/Sandbox/Scenes/Gameplay.unity   ← メイン開発シーン
Assets/Sandbox/Prefabs/Relics/         ← 遺物プレハブ8種
```

---

## SpawnLayer enum（SpawnPoint.cs）

```csharp
public enum SpawnLayer { Relic, Hazard, Route, Item }
```

## SoundId enum（SoundId.cs）— MAP実装で使うもの

```csharp
SoundId.Checkpoint    // チェックポイント通過音
SoundId.Summit        // 山頂到達音
SoundId.ShrineActivate // 祠使用音
SoundId.RockfallWarning // 落石警告音
```

（実際のID名は `Assets/Sandbox/Script/Audio/SoundId.cs` で確認）

---

## よく使うパターン

### ExpeditionManager への通知
```csharp
// チェックポイント通過
ExpeditionManager.Instance?.OnCheckpointReached(checkpointIndex);

// 遠征終了（山頂到達 or 帰還）
ExpeditionManager.Instance?.ReturnToBase(allSurvived: true);

// 動的チェックポイント登録（BivouacTent等）
ExpeditionManager.Instance?.RegisterDynamicCheckpoint(transform);
```

### AudioManager 呼び出し
```csharp
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

PPAudioManager.Instance?.PlaySE(SoundId.Checkpoint, transform.position);
```

### プレイヤー検出
```csharp
private void OnTriggerEnter(Collider other)
{
    if (!other.CompareTag("Player")) return;
    // プレイヤー固有の処理
}
```

### Terrain高さ取得
```csharp
Terrain terrain = Terrain.activeTerrain;
float y = terrain.SampleHeight(new Vector3(x, 0, z));
```

---

## 地形座標系

```
World座標範囲:
  X: -150 〜 +150 (幅 300m)
  Z: -150 〜 +150 (奥行 300m)
  Y:   0  〜 +220 (高さ)

方向:
  Z+ = 山頂方向（北）
  Z- = ベースキャンプ方向（南）
  X= 0 = 山の中心線

ゾーン中心のZ座標（目安）:
  Basecamp:  Z = -138
  Zone1:     Z = -96
  Zone2:     Z = -45
  Zone3:     Z = +6
  Zone4:     Z = +45
  Zone5:     Z = +84
  Zone6:     Z = +129
```

---

## MountainTerrainGenerator の名前参照リスト

以下の GameObject名は **完全一致** が必要（スクリプトが名前で検索）:

```
ゾーンマーカー（自動配置対象）:
  "Basecamp"
  "Zone1_Forest"
  "Zone2_RockySlope"
  "Zone3_CliffWall"
  "Zone4_TempleRuins"
  "Zone5_IceWall"
  "Zone6_Summit"

スナップ対象コンテナ（名前重要）:
  "GrappableRocks"      (yOffset: 0.0)
  "IcePatches"          (yOffset: 0.1)
  "Checkpoints"         (yOffset: 1.5)
  "RouteGates"          (yOffset: 0.0)
  "RelicSpawnPoints"    (yOffset: 0.5)
  "PlayerSpawnPoints"   (yOffset: 0.5)
  "HazardSpawnPoints"   (yOffset: 3.0)
```

---

## 既存コンポーネントの主要フィールド

### RouteGate
```csharp
[SerializeField] string      _routeName;
[SerializeField] bool        _defaultOpen = true;
[SerializeField] GameObject[] _blockers;
[SerializeField] ParticleSystem _openParticles;
// API:
void SetOpen(bool open);
bool IsOpen { get; }
```

### ReviveShrine
```csharp
[SerializeField] ParticleSystem _ambientParticles;
[SerializeField] ParticleSystem _reviveParticles;
[SerializeField] Color _availableColor = Color.cyan;
[SerializeField] Color _usedColor = Color.gray;
// API:
void Use();
bool IsAvailable { get; }
```

### SpawnPoint
```csharp
[SerializeField] SpawnLayer _layer;
[SerializeField] int _zoneId;
[SerializeField] float _activateChance = 0.5f;
[SerializeField] GameObject[] _spawnPrefabs;
[SerializeField] bool _pickRandom = true;
// API:
bool TryActivate();
void Activate(int prefabIndex = -1);
void Deactivate();
void SetPrefabPool(GameObject[] pool);
```

### RockfallTrigger
```csharp
[SerializeField] float _triggerInterval = 30f;
[SerializeField] float _intervalVariance = 15f;
[SerializeField] float _rockDamage = 25f;
[SerializeField] int _rockCount = 3;
[SerializeField] float _spreadRadius = 4f;
[SerializeField] bool _autoTrigger = true;
[SerializeField] GameObject _rockPrefab; // null=動的生成
// API:
void Activate(); // 手動トリガー
```

---

## Prefab 存在確認リスト

```
Assets/Sandbox/Prefabs/
├── PlayerPrefab.prefab                    ✓
├── Relics/
│   ├── GoldenDuckRelic.prefab            ✓
│   ├── CrystalCupRelic.prefab            ✓
│   ├── GreatStoneSlabRelic.prefab        ✓
│   ├── SingingVaseRelic.prefab           ✓
│   ├── FloatingSphereRelic.prefab        ✓
│   ├── TwinStatueRelic.prefab            ✓
│   ├── SlipperyFishStatueRelic.prefab    ✓
│   └── MagneticHelmetRelic.prefab        ✓

※ ハザードPrefab（RockfallTrigger等）は既存Prefabがなければ
  SpawnPoint._spawnPrefabsに直接コンポーネントを持つGameObjectを
  プレハブ化して使用すること。
```

---

## よくあるNullRef対策

```csharp
// Instance パターン
ExpeditionManager.Instance?.OnCheckpointReached(index);  // null安全

// コンポーネント取得
var col = GetComponent<Collider>();
if (col == null) col = gameObject.AddComponent<SphereCollider>();

// Terrain取得
var terrain = Terrain.activeTerrain;
if (terrain == null) { Debug.LogError("Terrain not found"); return; }
```
