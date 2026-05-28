# Stage 01 MAP設計書 — スポーンポイント配置テーブル

**このドキュメントの用途:** Cursor がシーンに SpawnPoint / RouteGate コンポーネントを
一括セットアップする際の設定値リファレンス。

---

## 1. SpawnPoint 配置テーブル（全スポーンポイント一覧）

### L3: 遺物スポーン（RelicSpawnPoints 親の下）

| GameObject名 | World座標 (X, Z) | ZoneId | activateChance | 備考 |
|------------|----------------|--------|--------------|------|
| RelicSpawn_Z1_A | (−10, −95) | 1 | 0.60 | 低価値遺物優先 |
| RelicSpawn_Z1_B | (+18, −72) | 1 | 0.50 | 森の奥まった場所 |
| RelicSpawn_Z2_A | (−15, −40) | 2 | 0.65 | 岩場の窪み |
| RelicSpawn_Z2_B | (+20, −20) | 2 | 0.55 | 崖の出っ張り |
| RelicSpawn_Z3_A | (−10, +20) | 3 | 0.60 | 崖の棚 |
| RelicSpawn_Z4_A | (−5,  +60) | 4 | 0.70 | 神殿内部（高価値） |
| RelicSpawn_Z4_B | (+5,  +68) | 4 | 0.60 | 神殿奥の祭壇 |
| RelicSpawn_Z5_A | (0,   +95) | 5 | 0.50 | 氷壁の窪み |
| RelicSpawn_Z6_A | (0,  +130) | 6 | 0.40 | 山頂祭壇（最高価値） |

**設定値（各SpawnPointに共通）:**
```
Component: SpawnPoint
  _layer = SpawnLayer.Relic
  _pickRandom = true
  _spawnPrefabs = []  ← SpawnManagerがランタイムで注入（Inspector空でOK）
```

### L5: ハザードスポーン（HazardSpawnPoints 親の下）

| GameObject名 | World座標 (X, Z) | Y高さ | ZoneId | ハザード種別 |
|------------|----------------|------|--------|-----------|
| Hazard_Z1_Rock_01 | (0, −75)   | terrain+50 | 1 | RockfallTrigger |
| Hazard_Z2_Rock_01 | (+5, −35)  | terrain+60 | 2 | RockfallTrigger |
| Hazard_Z2_Rock_02 | (−10, −15) | terrain+70 | 2 | RockfallTrigger |
| Hazard_Z3_Collapse_01 | (0, +10)  | terrain    | 3 | CollapsiblePlatform |
| Hazard_Z3_Collapse_02 | (−15, +30)| terrain    | 3 | CollapsiblePlatform |
| Hazard_Z4_Trap_01 | (0, +58)   | terrain    | 4 | TempleTraps |
| Hazard_Z5_Ice_01  | (0, +88)   | terrain+30 | 5 | RockfallTrigger（氷塊） |
| Hazard_Z5_Ice_02  | (−5, +110) | terrain+35 | 5 | RockfallTrigger |
| Hazard_Z6_Rock_01 | (+5, +122) | terrain+20 | 6 | RockfallTrigger |

**設定値:**
```
Component: SpawnPoint
  _layer = SpawnLayer.Hazard
  _activateChance = 0.4   ← SpawnManagerの _hazardDensity と同じ確率で起動
  _pickRandom = false      ← ハザードは指定種別固定
  _spawnPrefabs = [対応するハザードPrefab]
```

### L5: 山中ドロップアイテム（HazardSpawnPoints 親の下）

前の遠征チームの遺留品として、低耐久のアイテムが落ちている。

| GameObject名 | World座標 (X, Z) | ZoneId | 備考 |
|------------|----------------|--------|------|
| ItemDrop_Z1_A | (−5, −100) | 1 | ショートロープ（耐久30） |
| ItemDrop_Z2_A | (+12, −38) | 2 | アイスアックス（耐久20） |
| ItemDrop_Z3_A | (−8, +15)  | 3 | アンカーボルト残1（耐久50） |
| ItemDrop_Z4_A | (+15, +55) | 4 | 食料×1（耐久100） |
| ItemDrop_Z5_A | (−18, +92) | 5 | 酸素タンク（耐久25） |

**設定値:**
```
Component: SpawnPoint
  _layer = SpawnLayer.Item
  _activateChance = 0.5
  _spawnPrefabs = [対応するアイテムPrefab]
```

---

## 2. RouteGate 設定テーブル

| GameObject名 | World座標 (X, Z) | 向き | defaultOpen | routeName |
|------------|----------------|-----|-------------|-----------|
| RouteGate_Z2_Shortcut | (+25, −65) | 東向き | true | "Forest Shortcut" |
| RouteGate_Z3_Bridge   | (−20, −5)  | 西向き | true | "West Bridge" |
| RouteGate_Z4_East     | (+25, +40) | 東向き | true | "East Cliff Pass" |
| RouteGate_Z5_Couloir  | (−10, +100)| 西向き | true | "Snow Couloir" |

**各RouteGateのBlocker設定（子オブジェクトとして配置）:**

```
RouteGate_Z2_Shortcut/Blocker:
  構成: Cube×3（岩崩れ）
  Scale: (3,2,2), (2,3,1.5), (2,2,2.5)
  Color: #7A6B5A
  BoxCollider あり、Tag="Untagged"

RouteGate_Z3_Bridge/Blocker:
  構成: Cube1枚（橋が落ちた状態）
  Scale: (8, 0.5, 2)
  Position: 橋の隙間（橋として必要な場所を塞ぐ）
  Color: #8B7355

RouteGate_Z4_East/Blocker:
  構成: Cube×2（大岩）
  Scale: (4,4,3), (3,3,4)
  Color: #6A6050

RouteGate_Z5_Couloir/Blocker:
  構成: Cube1枚（雪崩の雪）
  Scale: (8,10,3)
  Color: #E8F4FF（白に近い水色）
```

---

## 3. チェックポイント設定テーブル

`Checkpoints` 親の下に配置する Transform 参照用オブジェクト（コンポーネントなし）。
`ExpeditionManager._checkpoints` 配列に設定する。

| GameObject名 | World座標 | 用途 |
|------------|---------|------|
| Checkpoint_01 | (0, terrain, −80) | Zone1 中腹（リスポーン位置） |
| Checkpoint_02 | (0, terrain, −25) | Zone2/3 境界 |
| Checkpoint_03 | (0, terrain, +25) | Zone3/4 境界 |
| Checkpoint_04 | (0, terrain, +115)| Zone5/6 境界 |

**併せて配置する ZoneCheckpoint トリガー（別オブジェクト）:**

`World/Zone*/Checkpoint_ZoneN` に ZoneCheckpoint コンポーネント付きで配置。
（詳細は `MAP_02_シーン構成.md` 参照）

---

## 4. ReviveShrine 配置テーブル

山中の復活の祠。プレイヤーが探し出すことで1回だけ復活できる。

| GameObject名 | World座標 (X, Z) | 高さ目安 | 視認性 |
|------------|----------------|---------|-------|
| Zone1_Shrine  | (−20, −70) | ~35m | 木の陰に隠れている |
| Zone2_Shrine  | (−25, −10) | ~90m | 岩の裏 |
| Zone3_Shrine  | (+20, +30) | ~130m | 崖の横 |
| Zone4_Shrine  | (+18, +55) | ~155m | 神殿の隅 |
| Zone5_Shrine  | (+22, +90) | ~185m | 氷壁の岩影 |
| Summit_Shrine | (−10, +125)| ~215m | 山頂直前 |

**各ShrineのReviveShrine設定:**
```
Component: ReviveShrine（既存）
  _availableColor = Color.cyan  (#00FFFF)
  _usedColor = Color.gray       (#808080)
  _ambientParticles = (ParticleSystem — 小さな光の粒)
  _reviveParticles  = (ParticleSystem — 復活時の爆発エフェクト)
```

**Visual（アセットなしのPrimitive代替）:**
```
親GameObject: Zone*_Shrine
  └─ ShrineBase: Cube Scale(1.2, 0.2, 1.2), Color=#404040（石の台座）
  └─ ShrinePillar: Cylinder Scale(0.3, 1.5, 0.3), Color=#505070（柱）
  └─ ShrineCrystal: Sphere Scale(0.6, 0.6, 0.6), Color=Cyan, Emission あり
      （Shrine本体に ReviveShrine コンポーネント）
```

---

## 5. プレイヤースポーン設定

**ネットワーク対応（NetworkPlayerSpawner と連携）**

```
PlayerSpawnPoints（親）:
  └─ PlayerSpawn_Basecamp:
       Position: (0, 2, −130)
       Component: SpawnPoint
         _layer = SpawnLayer.Route  （Item でも Route でも可、SpawnManagerはここを特別扱い）
         _activateChance = 1.0
```

`NetworkPlayerSpawner` が `PlayerSpawnPoints` 以下を自動的に使用するか、
直接 `SpawnAnchor_Basecamp` を Transform 参照で渡すかは既存実装を確認。

---

## 6. IcePatch 追加配置（Zone6）

Zone5 の12個に加えて Zone6 にも少数配置：

| GameObject名 | World座標 (X, Z) | Scale |
|------------|----------------|-------|
| IcePatch_Z6_01 | (−8, +122) | (5, 0.15, 5) |
| IcePatch_Z6_02 | (+12, +128) | (4, 0.15, 4) |

---

## 7. SpawnManager のランダム性まとめ

毎ランで変わる要素：

| 要素 | 変動内容 | 担当コンポーネント |
|-----|---------|----------------|
| 遺物の出現場所 | 9個のRelicSpawnPointから3〜5個をランダム選択 | SpawnManager |
| 遺物の種類 | 8種プールからランダム選択 | SpawnPoint._pickRandom |
| ルートの開閉 | 4本のRouteGateを50%確率で開閉 | SpawnManager |
| ハザードの出現 | 9個のHazardSpawnPointを40%確率でアクティベート | SpawnManager |
| 遺留品の有無 | 5個のItemDropを50%確率でアクティベート | SpawnManager |

**理論上の組み合わせ数:** 2^4（ルート） × C(9,4)（遺物） = 16 × 126 = **2016通り**

---

## 8. ゾーン環境設定（WeatherSystem / EnvironmentHazardConfig との連携）

各ゾーンの環境条件をコードで制御する。`EnvironmentHazardConfigSO` に設定。

| ゾーン | 標高 | 高山病 | 凍傷 | 風力 |
|------|-----|-------|-----|-----|
| Zone1 | 5〜135m | なし | なし | なし |
| Zone2 | 135〜315m | なし | なし | 弱（0.1） |
| Zone3 | 315〜425m | 弱（開始） | なし | 弱（0.2） |
| Zone4 | 380〜470m | 中（0.5x） | なし | 中（0.3） |
| Zone5 | 455〜505m | 強（1.0x） | あり | 強（0.6） |
| Zone6 | 505〜520m | 最大（1.5x）| あり | 最大（0.8） |

`EnvironmentHazardConfigSO.asset` に以下の値を設定：
```
altitudeSicknessThreshold = 1200  (m)  ← Zone3 から発動
frostbiteThreshold = 1800              ← Zone5 から発動
```

**※ 実際の標高値は `ExplorerController` が Y 座標から計算している場合は**
**そちらに合わせること。`AltitudeSicknessEffect.cs` のコードを確認。**
