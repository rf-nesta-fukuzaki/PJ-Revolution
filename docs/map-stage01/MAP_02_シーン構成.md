# Stage 01 MAP設計書 — シーン構成と必須コンポーネント

**対象シーン:** `Assets/Sandbox/Scenes/Gameplay.unity`

---

## 1. 完全シーン階層（Hierarchy）

以下の通りに GameObject を配置する。既存の Gameplay.unity に追記する形で作業。
**`名前 (コンポーネント)` 形式で記載。**

```
[Scene Root]
├── GameManager (GameStateManager, ExpeditionManager, SpawnManager, AudioManager)
│
├── Network (NetworkBootstrap, LobbyManager, NetworkPlayerSpawner)
│
├── World
│   ├── Mountain (MountainTerrainGenerator)          ← ★ 地形生成の親
│   │   ├── [MountainTerrain]                        ← Generate実行後に自動生成
│   │   ├── PeakScaleLandmarks                       ← 巨大岩壁・外周連峰・山頂尖塔
│   │   └── PeakAscentSetpieces                      ← 細い岩棚・ロープ壁・ナイフリッジ
│   │
│   ├── Basecamp                                     ← ゾーンマーカー(名前が重要)
│   │   ├── BasecampShopArea (BasecampShop)          ← 既存
│   │   ├── DeparturePoint (DepartureGate)           ← 出発ゲート
│   │   ├── ReturnPoint (ReturnZone)                 ← 帰還ゾーン
│   │   └── SpawnAnchor_Basecamp                    ← プレイヤー初期スポーン位置
│   │
│   ├── Zone1_Forest                                 ← ゾーンマーカー(名前が重要)
│   │   ├── Forest_Trees_Root                       ← 木プロップ親
│   │   │   ├── Tree_01 (Grappable タグ)
│   │   │   ├── Tree_02 (Grappable タグ)
│   │   │   └── ... (計 20本)
│   │   ├── Zone1_Shrine (ReviveShrine)              ← 復活の祠 #1
│   │   └── Checkpoint_Zone1 (ZoneCheckpoint)       ← チェックポイント #0
│   │
│   ├── Zone2_RockySlope                            ← ゾーンマーカー
│   │   ├── RouteGates_Z2                           ← Z2のルートゲート群
│   │   │   ├── RouteGate_Z2_Main (RouteGate)
│   │   │   └── RouteGate_Z2_Short (RouteGate)
│   │   ├── Zone2_Shrine (ReviveShrine)             ← 復活の祠 #2
│   │   └── Checkpoint_Zone2 (ZoneCheckpoint)      ← チェックポイント #1
│   │
│   ├── Zone3_CliffWall                            ← ゾーンマーカー
│   │   ├── CliffBridges                           ← 崩れ足場群
│   │   │   ├── Bridge_Z3_A (CollapsiblePlatform)
│   │   │   └── Bridge_Z3_B (CollapsiblePlatform)
│   │   ├── RouteGate_Z3 (RouteGate)
│   │   ├── Zone3_Shrine (ReviveShrine)            ← 復活の祠 #3
│   │   └── Checkpoint_Zone3 (ZoneCheckpoint)     ← チェックポイント #2
│   │
│   ├── Zone4_TempleRuins                         ← ゾーンマーカー
│   │   ├── Temple_Geometry                       ← 神殿プロップ群
│   │   │   ├── TempleWall_01 (Grappable タグ)
│   │   │   ├── TempleWall_02 (Grappable タグ)
│   │   │   ├── TempleColumn_01 (Grappable タグ)
│   │   │   └── TempleColumn_02 (Grappable タグ)
│   │   ├── Temple_Traps (TempleTraps)
│   │   ├── Zone4_Shrine (ReviveShrine)           ← 復活の祠 #4
│   │   └── Checkpoint_Zone4 (ZoneCheckpoint)    ← チェックポイント #3
│   │
│   ├── Zone5_IceWall                            ← ゾーンマーカー
│   │   ├── IceWall_Geometry                    ← 氷壁プロップ群
│   │   │   ├── IceFormation_01 (Grappable タグ)
│   │   │   └── IceFormation_02 (Grappable タグ)
│   │   ├── Zone5_Shrine (ReviveShrine)         ← 復活の祠 #5
│   │   └── Checkpoint_Zone5 (ZoneCheckpoint)  ← チェックポイント #4
│   │
│   └── Zone6_Summit                           ← ゾーンマーカー
│       ├── Summit_Geometry                    ← 山頂プロップ
│       │   └── SummitFlag (Grappable タグ)   ← 旗
│       ├── SummitGoal (SummitGoalTrigger)    ← ★ 山頂到達トリガー
│       └── Summit_Shrine (ReviveShrine)      ← 復活の祠 #6
│
├── GrappableRocks                            ← ★ 名前が重要（MountainTerrainGeneratorが参照）
│   ├── Rock_Z1_01 (Grappable タグ, Rigidbody isKinematic=true)
│   ├── Rock_Z1_02
│   ├── ... (Zone1: 10個)
│   ├── Rock_Z2_01
│   ├── ... (Zone2: 15個)
│   ├── Rock_Z3_01
│   ├── ... (Zone3: 10個)
│   ├── Rock_Z4_01
│   ├── ... (Zone4: 8個)
│   ├── Rock_Z5_01
│   ├── ... (Zone5: 7個)
│   └── Rock_Z6_01, Rock_Z6_02, Rock_Z6_03 (Zone6: 3個)
│   合計: 53個以上 ← GDD要件：50個以上
│
├── IcePatches                                ← ★ 名前が重要
│   ├── IcePatch_Z5_01 (IcePatch)
│   ├── IcePatch_Z5_02 (IcePatch)
│   ├── ... (Zone5: 10個)
│   └── IcePatch_Z6_01 (IcePatch, Zone6に2個)
│
├── Checkpoints                              ← ★ 名前が重要（ExpeditionManagerが参照）
│   ├── Checkpoint_01 (Transform only — Zone2境界)
│   ├── Checkpoint_02 (Transform only — Zone3境界)
│   ├── Checkpoint_03 (Transform only — Zone4/5境界)
│   └── Checkpoint_04 (Transform only — Zone6手前)
│
├── RouteGates                              ← ★ 名前が重要
│   ├── RouteGate_Z2_Shortcut (RouteGate)  ← Zone2ショートカット
│   ├── RouteGate_Z3_Bridge (RouteGate)    ← Zone3橋
│   ├── RouteGate_Z4_East (RouteGate)      ← Zone4東ルート
│   └── RouteGate_Z5_Couloir (RouteGate)   ← Zone5クーロワール
│
├── RelicSpawnPoints                       ← ★ 名前が重要
│   ├── RelicSpawn_Z1_A (SpawnPoint: Layer=Relic, ZoneId=1)
│   ├── RelicSpawn_Z1_B (SpawnPoint: Layer=Relic, ZoneId=1)
│   ├── RelicSpawn_Z2_A (SpawnPoint: Layer=Relic, ZoneId=2)
│   ├── RelicSpawn_Z2_B (SpawnPoint: Layer=Relic, ZoneId=2)
│   ├── RelicSpawn_Z3_A (SpawnPoint: Layer=Relic, ZoneId=3)
│   ├── RelicSpawn_Z4_A (SpawnPoint: Layer=Relic, ZoneId=4, activateChance=0.7)
│   ├── RelicSpawn_Z4_B (SpawnPoint: Layer=Relic, ZoneId=4, activateChance=0.6)
│   ├── RelicSpawn_Z5_A (SpawnPoint: Layer=Relic, ZoneId=5, activateChance=0.5)
│   └── RelicSpawn_Z6_A (SpawnPoint: Layer=Relic, ZoneId=6, activateChance=0.4)
│
├── PlayerSpawnPoints                     ← ★ 名前が重要
│   └── PlayerSpawn_Basecamp (SpawnPoint: Layer=Route)
│
└── HazardSpawnPoints                    ← ★ 名前が重要
    ├── Hazard_Z2_Rock_01 (SpawnPoint: Layer=Hazard, ZoneId=2)
    ├── Hazard_Z2_Rock_02 (SpawnPoint: Layer=Hazard, ZoneId=2)
    ├── Hazard_Z3_Collapse_01 (SpawnPoint: Layer=Hazard, ZoneId=3)
    ├── Hazard_Z3_Collapse_02 (SpawnPoint: Layer=Hazard, ZoneId=3)
    ├── Hazard_Z4_Trap_01 (SpawnPoint: Layer=Hazard, ZoneId=4)
    ├── Hazard_Z5_Ice_01 (SpawnPoint: Layer=Hazard, ZoneId=5)
    ├── Hazard_Z5_Ice_02 (SpawnPoint: Layer=Hazard, ZoneId=5)
    └── Hazard_Z6_Rock_01 (SpawnPoint: Layer=Hazard, ZoneId=6)
```

---

## 2. MountainTerrainGenerator コンポーネント設定

`World/Mountain` GameObjectに以下の設定でアタッチ。

```
MountainTerrainGenerator:
  [Terrain サイズ]
  _terrainWidth  = 300
  _terrainLength = 300
  _terrainHeight = 520
  _resolution    = 513

  [Perlin ノイズ]
  _seed   = 42          ← 再現性のある地形（0に変えると別の山になる）
  _scale1 = 0.007
  _amp1   = 0.55
  _scale2 = 0.022
  _amp2   = 0.22
  _scale3 = 0.065
  _amp3   = 0.07

  [Terrain Material]
  _terrainMaterial = (URP/Lit マテリアル or null)
```

**設定後、Inspector の右クリックメニュー「Generate Mountain Terrain」を実行する。**
地形生成後、各オブジェクトが自動でTerrainにスナップされる。

---

## 3. ExpeditionManager コンポーネント設定

```
ExpeditionManager:
  _resultScreen  = (ResultScreen参照)
  _spawnManager  = (SpawnManager参照)
  _basecampShop  = (BasecampShop参照)
  _fadeDuration  = 1.0
  _respawnDelay  = 5.0
  _checkpoints   = [
    Checkpoints/Checkpoint_01,
    Checkpoints/Checkpoint_02,
    Checkpoints/Checkpoint_03,
    Checkpoints/Checkpoint_04
  ]
```

---

## 4. SpawnManager コンポーネント設定

```
SpawnManager:
  _minRelics      = 3
  _maxRelics      = 5
  _relicPrefabPool = [
    Prefabs/Relics/GoldenDuckRelic,
    Prefabs/Relics/CrystalCupRelic,
    Prefabs/Relics/GreatStoneSlabRelic,
    Prefabs/Relics/SingingVaseRelic,
    Prefabs/Relics/FloatingSphereRelic,
    Prefabs/Relics/TwinStatueRelic,
    Prefabs/Relics/SlipperyFishStatueRelic,
    Prefabs/Relics/MagneticHelmetRelic
  ]
  _hazardDensity  = 0.4
  _routeOpenChance = 0.5
```

---

## 5. RouteGate コンポーネント設定

各RouteGateに `RouteGate` コンポーネントをアタッチ。

```
RouteGate_Z2_Shortcut:
  _routeName    = "Zone2 Shortcut"
  _defaultOpen  = true          ← SpawnManagerが上書きする
  _blockers     = [岩プリミティブ / 木コンテナ等]

RouteGate_Z3_Bridge:
  _routeName    = "Zone3 Bridge"
  _defaultOpen  = true
  _blockers     = [崩れた橋Mesh / Cube等]

RouteGate_Z4_East:
  _routeName    = "Zone4 East Pass"
  _defaultOpen  = true
  _blockers     = [岩崩れCube]

RouteGate_Z5_Couloir:
  _routeName    = "Zone5 Couloir"
  _defaultOpen  = true
  _blockers     = [雪崩Cube]
```

**Blockerオブジェクトは RouteGate の子に配置し、BoxCollider を持たせる（通行阻害用）。**

---

## 6. 岩（GrappableRocks）の作成仕様

**プリミティブで代替**（アセット不要）。全岩に以下を設定。

```
各Rock GameObject:
  MeshFilter: Sphere または Cube (不規則に見えるようScaleを歪める)
  MeshRenderer: Material = URP/Lit (グレー〜茶色、Color = #7A7060)
  Collider: SphereCollider または MeshCollider (convex)
  Rigidbody: isKinematic = true, useGravity = false
  Tag: "Grappable"
  Layer: "Grappable"
  Scale目安: (1.2, 0.8, 1.5) ← バラバラに見えるよう各自変える
```

**スクリプトで一括生成する方法（推奨）：**

```csharp
// Editor専用スクリプト: GrappableRockPlacer.cs
// Assets/Sandbox/Script/Editor/ に配置

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public class GrappableRockPlacer : MonoBehaviour
{
    [ContextMenu("Place Rocks on Terrain")]
    void PlaceRocks()
    {
        // Zone別の配置エリアを定義
        var zones = new (string name, Vector3 center, Vector2 size, int count)[]
        {
            ("Z1", new Vector3(0, 0, -80), new Vector2(60, 40), 10),
            ("Z2", new Vector3(0, 0, -30), new Vector2(50, 35), 15),
            ("Z3", new Vector3(0, 0, +20), new Vector2(40, 25), 10),
            ("Z4", new Vector3(0, 0, +55), new Vector2(35, 20),  8),
            ("Z5", new Vector3(0, 0, +90), new Vector2(30, 18),  7),
            ("Z6", new Vector3(0, 0, +130), new Vector2(20, 12), 3),
        };

        var parent = GameObject.Find("GrappableRocks");
        if (parent == null) { Debug.LogError("GrappableRocks not found"); return; }

        var terrain = Terrain.activeTerrain;
        int totalCreated = 0;

        foreach (var zone in zones)
        {
            for (int i = 0; i < zone.count; i++)
            {
                float rx = zone.center.x + Random.Range(-zone.size.x / 2, zone.size.x / 2);
                float rz = zone.center.z + Random.Range(-zone.size.y / 2, zone.size.y / 2);
                float ry = terrain != null ? terrain.SampleHeight(new Vector3(rx, 0, rz)) : 0;

                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = $"Rock_{zone.name}_{i+1:00}";
                go.transform.SetParent(parent.transform);
                go.transform.position = new Vector3(rx, ry, rz);
                go.transform.localScale = new Vector3(
                    Random.Range(0.8f, 2.0f),
                    Random.Range(0.5f, 1.2f),
                    Random.Range(0.8f, 1.8f));
                go.transform.rotation = Quaternion.Euler(
                    Random.Range(-15f, 15f), Random.Range(0, 360f), Random.Range(-15f, 15f));

                go.tag = "Grappable";
                var rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;

                // グレー系マテリアル
                var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = new Color(
                    Random.Range(0.35f, 0.55f),
                    Random.Range(0.32f, 0.48f),
                    Random.Range(0.28f, 0.42f));
                go.GetComponent<Renderer>().sharedMaterial = mat;

                totalCreated++;
            }
        }
        Debug.Log($"[RockPlacer] 岩 {totalCreated} 個を配置");
    }
}
#endif
```

---

## 7. IcePatch の作成仕様（Zone5〜6用）

```
各IcePatch GameObject:
  Scale: (3.0, 0.1, 3.0) ← 平らな板
  MeshRenderer: Material = URP/Lit (水色 #A8DFFF, Smoothness=0.9, Metallic=0.1)
  BoxCollider: isTrigger = false (物理的な床)
  IcePatch コンポーネント:
    frictionOverride = 0.05  ← 非常に滑りやすい
    slideForce = 8.0
```

IcePatch.cs の `frictionOverride` / `slideForce` の実際のフィールド名は
`Assets/Sandbox/Script/Hazard/IcePatch.cs` を確認して合わせること。

---

## 8. SummitGoalTrigger スクリプト（新規作成）

**作成先:** `Assets/Sandbox/Script/System/SummitGoalTrigger.cs`

```csharp
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §2.1 — 山頂到達トリガー。
/// プレイヤーが Zone6 のゴールエリアに入ったら遠征終了。
/// </summary>
public class SummitGoalTrigger : MonoBehaviour
{
    [Header("演出")]
    [SerializeField] private ParticleSystem _celebrationParticles;
    [SerializeField] private float          _activationDelay = 1.5f;  // 演出後に帰還フロー

    private bool _triggered;

    private void Awake()
    {
        // トリガーコライダーが必要
        var col = GetComponent<Collider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
            ((SphereCollider)col).radius = 8f;
        }
        col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_triggered) return;
        if (!other.CompareTag("Player")) return;

        _triggered = true;
        Debug.Log("[SummitGoal] 山頂到達！");

        // 演出
        if (_celebrationParticles != null)
            _celebrationParticles.Play();
        PPAudioManager.Instance?.PlaySE(SoundId.Summit, transform.position);

        // 帰還フロー開始（遅延あり）
        Invoke(nameof(TriggerReturn), _activationDelay);
    }

    private void TriggerReturn()
    {
        ExpeditionManager.Instance?.ReturnToBase(allSurvived: true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0f, 0.4f);
        Gizmos.DrawSphere(transform.position, 8f);
        Gizmos.DrawIcon(transform.position + Vector3.up * 3f, "console.warnicon");
    }
}
```

---

## 9. ZoneCheckpoint スクリプト（新規作成）

**作成先:** `Assets/Sandbox/Script/System/ZoneCheckpoint.cs`

```csharp
using UnityEngine;

/// <summary>
/// GDD §2.1 — ゾーン境界チェックポイント。
/// プレイヤーが通過したら ExpeditionManager に記録する。
/// </summary>
public class ZoneCheckpoint : MonoBehaviour
{
    [Header("チェックポイント設定")]
    [SerializeField] private int   _checkpointIndex = 0;   // 0始まり
    [SerializeField] private float _triggerRadius   = 5f;

    [Header("演出")]
    [SerializeField] private ParticleSystem _passParticles;

    private bool _passed;

    private void Awake()
    {
        var col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.radius = _triggerRadius;
        }
        col.isTrigger = true;
        gameObject.tag = "Checkpoint";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_passed) return;
        if (!other.CompareTag("Player")) return;

        _passed = true;
        ExpeditionManager.Instance?.OnCheckpointReached(_checkpointIndex);

        if (_passParticles != null) _passParticles.Play();
        Debug.Log($"[Checkpoint] Checkpoint {_checkpointIndex + 1} 通過");
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.DrawSphere(transform.position, _triggerRadius);
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, _triggerRadius);
    }
}
```
