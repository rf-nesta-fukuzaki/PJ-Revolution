# Stage 01 MAP設計書 — ゾーン別詳細設計

**座標系:** Unity World座標。地形中心がX=0, Z=0。山はZ+ 方向に延びる。
**高さ:** MountainTerrainGenerator の Perlin Noiseで自動決定。SampleHeight()で取得。

---

## ゾーン0: ベースキャンプ（Basecamp）

### 位置・規模
| 項目 | 値 |
|------|-----|
| World Z | -150 〜 -110 |
| 標高 | 0 〜 5m（ほぼ平坦） |
| エリア広さ | 60m × 40m |
| GameObject名 | `Basecamp` |

### 地形特徴
- 完全に平坦な出発エリア
- `MountainTerrainGenerator` の `nz < 0.09f` で自動的に平坦化されている
- 山に向かって徐々に傾斜が始まる

### 配置するオブジェクト

**① ショップエリア（既存スクリプト活用）**
```
Position: (−15, 1, −135)
Scale: −
GameObject: BasecampShopArea
Component: BasecampShop（既存）
Visual: Cube 3個で簡易カウンターを表現
        - Counter_01: Scale(4,1,1) → 商品棚
        - Counter_02: Scale(4,1,1) → 装備ロッカー
```

**② 出発ゲート**
```
Position: (0, 1, −115)      ← Zone1への進入路上
Scale: (8, 6, 1)
GameObject: DeparturePoint
Component: DepartureGate（既存）
Visual: 2本のCylinder柱 + 上にCube横棒
        柱Color: #4A6741（緑系）
```

**③ 帰還ゾーン**
```
Position: (0, 1, −140)      ← スタート地点より後方
GameObject: ReturnPoint
Component: ReturnZone（既存）
Collider: BoxCollider isTrigger=true, Size(30, 5, 10)
Visual: 半透明Cubeまたは非表示
```

**④ プレイヤースポーン**
```
Position: (0, 2, −130)
GameObject: SpawnAnchor_Basecamp
（コンポーネントなし。SpawnManagerがここを初期スポーン位置として使用）
```

**⑤ 天気/ルートボード**
```
Position: (10, 2, −130)
Scale: (0.1, 3, 2)
GameObject: WeatherBoard
Component: WeatherBoardManager（既存）
Visual: Cube + TextMeshPro テキスト
```

### プレイ設計
- チームが合流してアイテムを購入するための十分な広さを確保
- 出発ゲートを通らないと Zone1 に進めない構造（DepartureGate）

---

## ゾーン1: 森林帯（Zone1_Forest）

### 位置・規模
| 項目 | 値 |
|------|-----|
| World Z | -110 〜 -50 |
| 標高 | 5 〜 66m |
| 勾配 | 緩やか（約15°） |
| GameObject名 | `Zone1_Forest` |

### 地形特徴
- 緩やかな登り勾配
- 木々が密集しているエリアと開けたエリアが交互
- ロープが初めて使えるグラップルポイントが数か所

### 配置するオブジェクト

**① 木（Grappableタグ付き）: 20本**
```
配置エリア: Z -110 〜 -60、X ±30m の範囲にランダム
各Treeの仕様:
  GameObject名: Tree_Z1_[01〜20]
  Primitive: Cylinder（幹） + Sphere（葉）の2オブジェクト構成
  幹: Scale(0.5, 3, 0.5), Color=#4A3728（茶色）
  葉: Scale(3, 2, 3),    Color=#2D5A27（緑）, Y=幹top
  Tag: "Grappable"（幹オブジェクトに設定）
  Rigidbody: isKinematic=true
  
  配置ランダム座標例:
    Tree_Z1_01: (-22, terrain, -105)
    Tree_Z1_02: (+15, terrain,  -98)
    Tree_Z1_03: (-8,  terrain,  -91)
    ... （残り17本は GrappableRockPlacer 相当のEditorスクリプトで自動配置）
```

**② チェックポイント #0**
```
Position: (0, terrain, -80)    ← Zone1 中腹
GameObject: Checkpoint_Zone1
Component: ZoneCheckpoint
  _checkpointIndex = 0
  _triggerRadius = 8.0
Visual: Cylinder(0.3, 2, 0.3) Color=#FFD700（黄色）
```

**③ 復活の祠 #1**
```
Position: (−20, terrain, −70)  ← 少し脇に外れた場所
GameObject: Zone1_Shrine
Component: ReviveShrine（既存）
Visual: Cube(1,2,1) Color=#00FFFF（シアン） + ParticleSystem
```

**④ RouteGate: Z1→Z2 ショートカット**
```
Position: (25, terrain, -65)   ← 右側の崖沿い
GameObject: RouteGate_Z2_Shortcut (→ RouteGates 親の下)
Component: RouteGate
  _routeName = "Forest Shortcut"
  _defaultOpen = true
  _blockers = [RouteGate_Z2_Shortcut/Blocker_Rocks]

Blocker_Rocks（RouteGateの子）:
  Cube × 3個で岩崩れ表現
  BoxCollider あり（通行阻害）
  Scale: (4,3,2), (3,2,3), (2,4,2) をランダム配置
```

**⑤ 遺物スポーンポイント 2箇所**
```
RelicSpawn_Z1_A:
  Position: (-10, terrain, -95)
  Component: SpawnPoint
    Layer = Relic
    ZoneId = 1
    activateChance = 0.6
    _pickRandom = true

RelicSpawn_Z1_B:
  Position: (+18, terrain, -72)
  Component: SpawnPoint
    Layer = Relic
    ZoneId = 1
    activateChance = 0.5
    _pickRandom = true
```

**⑥ ハザードスポーンポイント（落石）**
```
Hazard_Z2_Rock_01 (→ HazardSpawnPoints 親の下):
  Position: (0, terrain+50, -75)   ← 崖の上に配置
  Component: SpawnPoint
    Layer = Hazard
    ZoneId = 1
    _spawnPrefabs = [RockfallTrigger_Prefab]
```

### プレイ設計
- 木にロープを引っかけてスイングする体験を初めて味わうエリア
- ルートは1本のメインルート + 右側のショートカット（Zone2に直接繋がる）
- ハザードなし（または軽微な落石のみ）

---

## ゾーン2: 岩場帯（Zone2_RockySlope）

### 位置・規模
| 項目 | 値 |
|------|-----|
| World Z | -50 〜 +5 |
| 標高 | 66 〜 121m |
| 勾配 | 中程度（約25°） |
| GameObject名 | `Zone2_RockySlope` |

### 地形特徴
- 岩が多く転がりやすい斜面
- 最初の本格的なルート分岐（RouteGate 2本）
- 一部に「足場が崩れる」CollapsiblePlatform を配置

### 配置するオブジェクト

**① 岩（GrappableRocks親の下）: 15個**
```
Zone2用岩の配置エリア: Z -50 〜 0, X ±25m
各岩のScale: より大きく (1.5〜3.0, 0.8〜1.5, 1.5〜2.5)
Color: #655040（茶色がかったグレー）
```

**② チェックポイント #1**
```
Position: (0, terrain, -25)
GameObject: Checkpoint_Zone2
Component: ZoneCheckpoint
  _checkpointIndex = 1
  _triggerRadius = 10.0
```

**③ 復活の祠 #2**
```
Position: (−25, terrain, −10)
GameObject: Zone2_Shrine
```

**④ ルート分岐（RouteGate 2本）**
```
RouteGate_Z2_Main:
  Position: (0, terrain, −15)      ← メインルート上
  _routeName = "Rocky Main Pass"
  _blockers = [崩れた岩Cube ×3]
  
RouteGate_Z3_Bridge:
  Position: (−20, terrain, −5)     ← 西の橋ルート
  _routeName = "West Bridge"
  _blockers = [Bridge_Blocker: Scale(8,0.5,2) の板Cube]
  ※ Bridge_Blocker は橋そのものを折れた状態で表現
```

**⑤ 崩れ足場（CollapsiblePlatform）**
```
Zone3/2境界付近に2か所:

Bridge_Z3_A:
  Position: (−20, terrain+2, 0)
  Scale: (4, 0.4, 6)
  Component: CollapsiblePlatform（既存）
  Color: #8B7355（茶色）

Bridge_Z3_B:
  Position: (+10, terrain+3, 5)
  Scale: (3, 0.4, 5)
  Component: CollapsiblePlatform（既存）
```

**⑥ 遺物スポーン 2箇所**
```
RelicSpawn_Z2_A: Position(-15, terrain, -40), ZoneId=2, activateChance=0.65
RelicSpawn_Z2_B: Position(+20, terrain, -20), ZoneId=2, activateChance=0.55
```

**⑦ ハザードスポーン**
```
Hazard_Z2_Rock_01: Position(5, terrain+60, -35),  ZoneId=2
Hazard_Z2_Rock_02: Position(-10, terrain+70, -15), ZoneId=2
（落石スポーン — 高い位置に配置して真下に落ちる）
```

---

## ゾーン3: 急壁（Zone3_CliffWall）

### 位置・規模
| 項目 | 値 |
|------|-----|
| World Z | +5 〜 +45 |
| 標高 | 121 〜 141m |
| 勾配 | 急峻（40〜60°、一部垂直） |
| GameObject名 | `Zone3_CliffWall` |

### 地形特徴
- 実質的な崖エリア。グラップルなしでは登りにくい
- 複数の段差（TerrainのPerlinNoiseで自動的に凸凹）
- ロープ必須の岩棚が2〜3か所

### 配置するオブジェクト

**① 垂直岩壁プロップ（大岩）: 10個**
```
Zone3用岩はより大きく、グラップルポイントとして機能する必然性を持たせる
Scale: (2〜4, 3〜6, 2〜4) — 壁から出っ張った形に
配置: Z +5 〜 +40, X ±20m の崖沿い

Rock_Z3_LedgeA: Position(−8, terrain+10, +15), Scale(3,5,2.5) ← 大きな岩棚
Rock_Z3_LedgeB: Position(+12, terrain+8, +25), Scale(3.5,4,3)
Rock_Z3_LedgeC: Position(0, terrain+15, +35), Scale(4,6,3.5)
（残り7個はEditor自動配置）
```

**② チェックポイント #2**
```
Position: (0, terrain, +25)
GameObject: Checkpoint_Zone3
Component: ZoneCheckpoint
  _checkpointIndex = 2
  _triggerRadius = 8.0
```

**③ 復活の祠 #3**
```
Position: (20, terrain, +30)
GameObject: Zone3_Shrine
```

**④ ルートゲート**
```
RouteGate_Z4_East:
  Position: (25, terrain, +40)    ← Zone4東ルート入口
  _routeName = "East Cliff Pass"
  _blockers = [大岩Cube ×2]
```

**⑤ 遺物スポーン**
```
RelicSpawn_Z3_A: Position(−10, terrain+5, +20), ZoneId=3, activateChance=0.6
```

**⑥ ハザードスポーン（崩れ足場 + 落石）**
```
Hazard_Z3_Collapse_01: Position(0, terrain, +10), ZoneId=3
  → CollapsiblePlatform Prefab
Hazard_Z3_Collapse_02: Position(-15, terrain, +30), ZoneId=3
  → CollapsiblePlatform Prefab
```

---

## ゾーン4: 神殿遺跡（Zone4_TempleRuins）

### 位置・規模
| 項目 | 値 |
|------|-----|
| World Z | +45 〜 +80 |
| 標高 | 141 〜 180m |
| 勾配 | やや急（約30°） |
| GameObject名 | `Zone4_TempleRuins` |

### 地形特徴
- 古代文明の神殿が廃墟として存在（Cube/Cylinder で表現）
- 内部を通り抜ける構造（壁に囲まれたルート）
- トラップが設置されている（TempleTraps）
- 最も高価値の遺物が出やすいゾーン

### 配置するオブジェクト

**① 神殿建物（ProBuilderまたはPrimitiveで構成）**

```
Temple_Main（神殿本体の親）:
  Position: (0, terrain, +62)

  -- 壁 --
  TempleWall_N: Scale(20, 8, 1), Position(0, y, +72), Color=#C8B89A
  TempleWall_S: Scale(20, 8, 1), Position(0, y, +52), Color=#C8B89A
  TempleWall_E: Scale(1, 8, 20), Position(+10, y, +62), Color=#C8B89A
  TempleWall_W: Scale(1, 8, 20), Position(-10, y, +62), Color=#C8B89A
  （各壁にBoxCollider。Tag="Grappable" で壁面グラップル可能に）
  
  -- 柱 --
  TempleColumn_01: Cylinder Scale(0.8,5,0.8), Position(-8, y, +52), Color=#B5A48A
  TempleColumn_02: Cylinder Scale(0.8,5,0.8), Position(+8, y, +52)
  TempleColumn_03: Cylinder Scale(0.8,5,0.8), Position(-8, y, +72)
  TempleColumn_04: Cylinder Scale(0.8,5,0.8), Position(+8, y, +72)
  （柱にTag="Grappable"）
  
  -- 屋根（部分的に崩れた表現）--
  TempleRoof_A: Cube Scale(15, 0.5, 8), Position(-2, y+8, +62), Color=#A08870
  （屋根の上にも乗れる設計）
```

**② 神殿トラップ**
```
Temple_Traps:
  Position: (0, terrain+1, +60)
  Component: TempleTraps（既存）
  TempleTraps内部の設定は既存コンポーネントに従う
  （槍トラップ、落とし穴など）
```

**③ チェックポイント #3**
```
Position: (0, terrain+5, +75)   ← 神殿出口付近
GameObject: Checkpoint_Zone4
Component: ZoneCheckpoint
  _checkpointIndex = 3
  _triggerRadius = 8.0
```

**④ 復活の祠 #4**
```
Position: (18, terrain, +55)
GameObject: Zone4_Shrine
（神殿の隅に隠された感じに）
```

**⑤ 遺物スポーン 2箇所（高価値）**
```
RelicSpawn_Z4_A: Position(-5, terrain+2, +60), ZoneId=4, activateChance=0.70
RelicSpawn_Z4_B: Position(+5, terrain+2, +68), ZoneId=4, activateChance=0.60
（神殿内部に配置 → 運び出すのが大変）
```

**⑥ ハザードスポーン**
```
Hazard_Z4_Trap_01: Position(0, terrain, +58), ZoneId=4
  → TempleTraps Prefab
```

---

## ゾーン5: 氷壁（Zone5_IceWall）

### 位置・規模
| 項目 | 値 |
|------|-----|
| World Z | +80 〜 +120 |
| 標高 | 180 〜 210m |
| 勾配 | 急（35〜50°） |
| GameObject名 | `Zone5_IceWall` |

### 地形特徴
- 雪と氷に覆われたエリア
- IcePatch が地面に多数あり非常に滑りやすい
- 強風が吹く（WeatherSystem連携）
- AltitudeSicknessEffect が発動し始める高度

### 配置するオブジェクト

**① 氷の岩・壁プロップ**
```
IceFormation_01: Cube Scale(5,8,3), Position(-5,terrain+4,+95), Color=#A8DFFF, Smoothness=0.8
IceFormation_02: Cube Scale(3,6,4), Position(+8,terrain+3,+105), 同上
（Grappable タグ付き — アイスアックスで使用）
```

**② IcePatch（滑り床）: 12個**

IcePatch は `IcePatches` 親の下に配置。

```
IcePatch_Z5_01〜10 の座標例:
  IcePatch_Z5_01: Position(−15, terrain+0.1, +83),  Scale(4, 0.15, 4)
  IcePatch_Z5_02: Position(+10, terrain+0.1, +88),  Scale(3, 0.15, 5)
  IcePatch_Z5_03: Position(−5,  terrain+0.1, +93),  Scale(5, 0.15, 3)
  IcePatch_Z5_04: Position(+18, terrain+0.1, +97),  Scale(4, 0.15, 4)
  IcePatch_Z5_05: Position(0,   terrain+0.1, +102), Scale(6, 0.15, 4)
  IcePatch_Z5_06: Position(−12, terrain+0.1, +107), Scale(3, 0.15, 6)
  IcePatch_Z5_07: Position(+5,  terrain+0.1, +112), Scale(4, 0.15, 3)
  IcePatch_Z5_08: Position(−20, terrain+0.1, +116), Scale(5, 0.15, 5)
  IcePatch_Z5_09: Position(+15, terrain+0.1, +118), Scale(3, 0.15, 4)
  IcePatch_Z5_10: Position(0,   terrain+0.1, +120), Scale(7, 0.15, 4)
各Component:
  IcePatch コンポーネント（既存スクリプト）
  Material: URP/Lit, Color=#B8E8FF, Smoothness=0.95, Metallic=0.1
  BoxCollider: isTrigger=false（プレイヤーが乗れる）
```

**③ ルートゲート（クーロワール）**
```
RouteGate_Z5_Couloir:
  Position: (−10, terrain, +100)  ← 雪崩ルート
  _routeName = "Snow Couloir"
  _blockers = [雪崩Cube: Scale(8,10,3), Color=#FFFFFF]
```

**④ チェックポイント #4**
```
Position: (0, terrain, +115)
GameObject: Checkpoint_Zone5
Component: ZoneCheckpoint
  _checkpointIndex = 4
  _triggerRadius = 8.0
```

**⑤ 復活の祠 #5**
```
Position: (22, terrain, +90)
GameObject: Zone5_Shrine
```

**⑥ 遺物スポーン**
```
RelicSpawn_Z5_A: Position(0, terrain+2, +95), ZoneId=5, activateChance=0.50
（高難度エリアのため確率は低め）
```

**⑦ ハザードスポーン**
```
Hazard_Z5_Ice_01: Position(0, terrain+30, +88), ZoneId=5   ← 氷の塊が落ちてくる
Hazard_Z5_Ice_02: Position(-5, terrain+35, +110), ZoneId=5
```

---

## ゾーン6: 山頂遺跡（Zone6_Summit）

### 位置・規模
| 項目 | 値 |
|------|-----|
| World Z | +120 〜 +150 |
| 標高 | 210 〜 220m |
| 勾配 | 緩やか（到達したら山頂台地） |
| GameObject名 | `Zone6_Summit` |

### 地形特徴
- 最高難度の遺物
- 視界が霧で制限（WeatherSystem: 常に霧かかっている）
- 到達した瞬間は達成感を出す開けたエリア

### 配置するオブジェクト

**① 山頂フラッグ（ゴールの目印）**
```
SummitFlag:
  位置: (0, 220, +135)        ← 山の最高点付近
  Cylinder（幹）: Scale(0.15, 5, 0.15), Color=#C0C0C0（銀色の旗竿）
  Cube（旗）: Scale(2, 1.2, 0.05), Position(1, 5, 0), Color=#FFD700（金色）
  Tag="Grappable"（旗竿をグラップルポイントに）
```

**② 山頂ゴールトリガー**
```
SummitGoal:
  Position: (0, 218, +133)
  Component: SummitGoalTrigger（新規スクリプト）
    _activationDelay = 2.0
  Collider: SphereCollider radius=10, isTrigger=true
  Visual: なし（または薄い金色の半透明Sphere）
```

**③ 山頂遺跡プロップ**
```
SummitRuins（ベース岩）:
  Cube Scale(20, 2, 20), Position(0, 215, +130), Color=#9090A0 ← 山頂の平坦な石床
  
SummitAltar（祭壇）:
  Cube Scale(3, 1, 3), Position(0, 217, +130), Color=#C0B090 ← 遺物を置くための台
  Tag="Grappable"
```

**④ 復活の祠 #6（最後の救済）**
```
Position: (−10, terrain, +125)
GameObject: Summit_Shrine
（山頂直前の最後の復活ポイント）
```

**⑤ 遺物スポーン（最高価値）**
```
RelicSpawn_Z6_A:
  Position: (0, 217, +130)     ← 祭壇の上
  ZoneId=6
  activateChance=0.40          ← 最も出にくいが最も高価値
```

**⑥ ハザードスポーン**
```
Hazard_Z6_Rock_01: Position(5, terrain+20, +122), ZoneId=6
（山頂直前の最後のハザード）
```

---

## ゾーン間接続の設計（登山ルート）

### メインルート（常に通行可能）
```
Basecamp → Zone1(中央の森) → Zone2(中央の岩場) → Zone3(崖直登) 
  → Zone4(神殿正面) → Zone5(氷壁中央) → Zone6(山頂)
```

### サブルートA（RouteGate: Forest Shortcut 開放時）
```
Zone1 右側 → Zone2 上部に直結（中間の岩場をスキップ）
短距離だが急斜面
```

### サブルートB（RouteGate: West Bridge 開放時）
```
Zone2 → Zone3 西側の橋経由
橋は CollapsiblePlatform で2人同時に乗ると崩れる
```

### サブルートC（RouteGate: East Cliff Pass 開放時）
```
Zone3 → Zone4 東の崖沿い
グラップル必須の垂直ルート（上級者向け）
```

### サブルートD（RouteGate: Snow Couloir 開放時）
```
Zone5 西の沢（クーロワール）経由
IcePatch が多く非常に危険
```

---

## 遺物配置の標高帯戦略

| 遺物 | 出やすいゾーン | 理由 |
|------|------------|------|
| 黄金のアヒル像 | Zone1〜2 | 入門遺物。転がっても取り返せる |
| クリスタルの杯 | Zone2〜3 | 壊れやすい。まだ帰りやすい距離 |
| 大石板 | Zone3〜4 | 重い。急斜面の運搬カオスを体験させる |
| 歌う壺 | Zone3〜4 | 音が出る。神殿に合う設定 |
| 浮遊する球体 | Zone4〜5 | 高地の風で飛ばされる体験 |
| 双子像 | Zone4 | 神殿に文脈が合う。2人必要な場面 |
| ぬるぬる魚像 | Zone5 | 氷の上で滑る×魚が滑るの二重カオス |
| 磁力の兜 | Zone5〜6 | 最高難度。装備が引き寄せられて最悪 |
