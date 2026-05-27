# Stage 01 MAP設計書 — Cursor向け実装手順

**このドキュメントの用途:** Cursor が Unity エディターでMAP実装を行う際の
ステップバイステップ手順書。上から順に実行する。

---

## 前提確認（作業開始前チェック）

- [ ] Unity 6.3 URP プロジェクトが開いている
- [ ] `Gameplay.unity` シーンが開いている
- [ ] `Assets/Sandbox/Script/` 以下のスクリプトがコンパイルエラーなし
- [ ] `Assets/Sandbox/Prefabs/Relics/` に遺物プレハブ8種が存在する
- [ ] 作業ブランチ: `claude/map-development-peak-vfBva`

---

## Step 1: タグ・レイヤー追加

**Edit > Project Settings > Tags and Layers**

### タグ追加
```
Tags:
  + "Grappable"
  + "Checkpoint"
  + "ReturnZone"
  + "SummitGoal"
```

### レイヤー追加（User Layer 8〜10）
```
User Layer 8: Ground
User Layer 9: Grappable
User Layer 10: Hazard
```

### 確認
- [ ] `Grappable` タグが Project Settings に存在する
- [ ] コンパイルエラーなし

---

## Step 2: 新規スクリプト作成（2ファイル）

### 2-A: SummitGoalTrigger.cs
```
作成先: Assets/Sandbox/Script/System/SummitGoalTrigger.cs
内容: MAP_02_シーン構成.md の「8. SummitGoalTrigger スクリプト」セクションを参照
```

### 2-B: ZoneCheckpoint.cs
```
作成先: Assets/Sandbox/Script/System/ZoneCheckpoint.cs
内容: MAP_02_シーン構成.md の「9. ZoneCheckpoint スクリプト」セクションを参照
```

### 2-C: GrappableRockPlacer.cs（Editor用）
```
作成先: Assets/Sandbox/Script/Editor/GrappableRockPlacer.cs
内容: MAP_02_シーン構成.md の「6. 岩の作成仕様」のEditorスクリプトセクションを参照
```

### 確認
- [ ] コンパイルエラーなし（3スクリプト全て）
- [ ] Script Inspector で SummitGoalTrigger / ZoneCheckpoint が表示される

---

## Step 3: シーン基本構造の作成

Gameplay.unity で以下の GameObject を作成する。

### 3-A: World 親オブジェクト
```
GameObject名: World
  └─ Mountain（MountainTerrainGeneratorをアタッチ）
  └─ Basecamp（空のGameObject — ゾーンマーカー）
  └─ Zone1_Forest（同上）
  └─ Zone2_RockySlope（同上）
  └─ Zone3_CliffWall（同上）
  └─ Zone4_TempleRuins（同上）
  └─ Zone5_IceWall（同上）
  └─ Zone6_Summit（同上）
```

### 3-B: スポーン親オブジェクト（4つ）
```
シーンルートに直接配置:
  GrappableRocks（空のGameObject）
  IcePatches（空のGameObject）
  Checkpoints（空のGameObject）
  RouteGates（空のGameObject）
  RelicSpawnPoints（空のGameObject）
  PlayerSpawnPoints（空のGameObject）
  HazardSpawnPoints（空のGameObject）
```

⚠️ **名前は完全一致が必要（MountainTerrainGeneratorが名前で参照）**

### 確認
- [ ] Hierarchy に World 以下の Zone* が7つ存在する
- [ ] GrappableRocks / IcePatches / Checkpoints / RouteGates 等が存在する

---

## Step 4: MountainTerrainGenerator 設定と地形生成

### 4-A: コンポーネント設定
```
World/Mountain に MountainTerrainGenerator コンポーネントをアタッチ
設定値は MAP_02_シーン構成.md の「2. MountainTerrainGenerator コンポーネント設定」を参照
```

### 4-B: 地形生成実行
```
Inspector で Mountain を選択
右クリック > "Generate Mountain Terrain" を実行
```

地形生成後、コンソールに `[MountainTerrain] 生成完了` が表示される。

### 確認
- [ ] Hierarchy に `MountainTerrain` が Mountain の子として生成された
- [ ] Scene View で山の地形が表示されている
- [ ] 地形が 300×300m のサイズ
- [ ] ベースキャンプ側（Z負方向）が平坦で、Z正方向が高くなっている

---

## Step 5: ゾーンマーカー位置設定

地形生成後、各ゾーンマーカー GameObject を適切な位置に移動。
MountainTerrainGenerator が自動的にスナップするが、念のため確認。

| GameObject | 目標 World Z | 目標 World Y（目安） |
|-----------|------------|-----------------|
| Basecamp | -138 | ~2m |
| Zone1_Forest | -96 | ~15m |
| Zone2_RockySlope | -45 | ~75m |
| Zone3_CliffWall | +6 | ~125m |
| Zone4_TempleRuins | +45 | ~145m |
| Zone5_IceWall | +84 | ~185m |
| Zone6_Summit | +129 | ~215m |

**Xは全て0（山の中心線）**

### 確認
- [ ] Zone6_Summit が最も高い位置にある
- [ ] 各ゾーンが階段状に上がっている

---

## Step 6: GrappableRocks の配置

### 6-A: Editor スクリプトで自動配置
```
Hierarchy で GrappableRocks を選択
Inspector > GrappableRockPlacer コンポーネントをアタッチ
右クリック > "Place Rocks on Terrain" を実行
コンポーネントを削除（実行後は不要）
```

**目標: GrappableRocks の子に Rock_Z1_01〜Rock_Z6_03 が合計53個以上**

### 6-B: 手動調整（必要に応じて）
一部の岩が地形から浮いていたり埋まっていたりする場合：
```
対象岩を選択 > Inspector で Y 値を地形高さに合わせて調整
または: Mountain を選択 > "Snap Objects to Terrain" を実行
```

### 確認
- [ ] GrappableRocks の子が 50個以上
- [ ] 全ての岩に Tag="Grappable" が設定されている
- [ ] 全ての岩に Rigidbody (isKinematic=true) が設定されている
- [ ] 岩が地形表面に沿って配置されている

---

## Step 7: ベースキャンプエリアの作成

`MAP_03_ゾーン詳細.md` の「ゾーン0: ベースキャンプ」を参照。

### 7-A: DepartureGate 設置
```
Basecamp の子に GameObject "DeparturePoint" を作成
Position: (0, 1, -115)
Component: DepartureGate（既存スクリプト）
Visual: Cylinder × 2 + Cube の手動配置（MAP_03参照）
```

### 7-B: ReturnZone 設置
```
Basecamp の子に GameObject "ReturnPoint" を作成
Position: (0, 1, -140)
Component: ReturnZone（既存スクリプト）
BoxCollider: isTrigger=true, Size(30, 5, 10)
```

### 7-C: プレイヤースポーン設置
```
PlayerSpawnPoints の子に "PlayerSpawn_Basecamp" を作成
Position: (0, 2, -130)
（コンポーネント不要 — ExpeditionManager が直接参照）
```

### 確認
- [ ] DepartureGate が Zone1 への入口に設置されている
- [ ] ReturnZone がベースキャンプ後方にある

---

## Step 8: ゾーン別プロップ配置

各ゾーンを `MAP_03_ゾーン詳細.md` に従って設定する。

### 8-A: Zone1 森林帯
```
Zone1_Forest の子に Forest_Trees_Root を作成
木を20本配置（Cylinder+Sphere、Grappable タグ）
Checkpoint_Zone1 を配置（ZoneCheckpoint, index=0）
Zone1_Shrine を配置（ReviveShrine）
```

### 8-B: Zone2 岩場帯
```
RouteGates の子に RouteGate_Z2_Shortcut を配置（RouteGate）
RouteGates の子に RouteGate_Z3_Bridge を配置（RouteGate）
Zone2_Shrine を配置
Checkpoint_Zone2 を配置（ZoneCheckpoint, index=1）
```

### 8-C: Zone3 急壁
```
Zone3_CliffWall の子に CliffBridges を作成
  Bridge_Z3_A, Bridge_Z3_B (CollapsiblePlatform)
RouteGates の子に RouteGate_Z4_East を配置
Zone3_Shrine / Checkpoint_Zone3 (ZoneCheckpoint, index=2)
```

### 8-D: Zone4 神殿遺跡
```
Zone4_TempleRuins の子に Temple_Geometry を作成（Primitive組み合わせ）
Temple_Traps (TempleTraps コンポーネント)
Zone4_Shrine / Checkpoint_Zone4 (ZoneCheckpoint, index=3)
```

### 8-E: Zone5 氷壁
```
IcePatches の子に IcePatch_Z5_01〜10 を作成（IcePatch コンポーネント）
RouteGates の子に RouteGate_Z5_Couloir を配置
Zone5_Shrine / Checkpoint_Zone5 (ZoneCheckpoint, index=4)
```

### 8-F: Zone6 山頂遺跡
```
Zone6_Summit の子に SummitFlag, SummitRuins を作成
SummitGoal (SummitGoalTrigger コンポーネント)
Summit_Shrine (ReviveShrine)
```

### 確認
- [ ] 各ゾーンに ReviveShrine が1個ずつ（計6個）
- [ ] ZoneCheckpoint が0〜4の5個
- [ ] RouteGate が4個
- [ ] CollapsiblePlatform が2個（Zone3）
- [ ] TempleTraps が1個（Zone4）
- [ ] IcePatch が合計12個以上（Zone5-6）
- [ ] SummitGoalTrigger が Zone6 山頂に存在

---

## Step 9: スポーンポイント配置

`MAP_04_スポーン配置テーブル.md` のテーブルに従って配置。

### 9-A: RelicSpawnPoints
```
RelicSpawnPoints の子に9個の SpawnPoint を作成
各SpawnPointに SpawnPoint コンポーネントをアタッチ
Layer=Relic, ZoneId=対応ゾーン番号, activateChance=テーブル参照
```

### 9-B: HazardSpawnPoints
```
HazardSpawnPoints の子に9個 + ドロップアイテム5個
Layer=Hazard または Item
_spawnPrefabs に対応するPrefabを設定
```

### 9-C: SnapObjectsToTerrain 実行
```
Mountain を選択 > Inspector > "Snap Objects to Terrain" を実行
全スポーンポイントが地形面にスナップされる
```

### 確認
- [ ] RelicSpawnPoints の子が9個
- [ ] HazardSpawnPoints の子が14個（ハザード9 + ドロップ5）
- [ ] 全スポーンポイントが地形上にある

---

## Step 10: Checkpoints 配列設定

ExpeditionManager の `_checkpoints` 配列を設定。

```
Hierarchy で ExpeditionManager コンポーネントを持つ GameManager を選択
Inspector > ExpeditionManager
  _checkpoints の Size を 4 に設定
  Element 0 = Checkpoints/Checkpoint_01
  Element 1 = Checkpoints/Checkpoint_02
  Element 2 = Checkpoints/Checkpoint_03
  Element 3 = Checkpoints/Checkpoint_04
```

**Checkpoints 親の下に Checkpoint_01〜04 を配置（Transform のみ）:**
```
Checkpoint_01: Position(0, terrain, -80)   ← Zone1中腹
Checkpoint_02: Position(0, terrain, -25)   ← Zone2/3境界
Checkpoint_03: Position(0, terrain, +25)   ← Zone3/4境界
Checkpoint_04: Position(0, terrain, +115)  ← Zone5/6境界
```

### 確認
- [ ] ExpeditionManager._checkpoints に4つの Transform が設定されている

---

## Step 11: SpawnManager 設定

```
GameManager の SpawnManager コンポーネントを選択
  _minRelics = 3
  _maxRelics = 5
  _relicPrefabPool: Size=8 で全遺物Prefabを設定
    Assets/Sandbox/Prefabs/Relics/GoldenDuckRelic
    Assets/Sandbox/Prefabs/Relics/CrystalCupRelic
    Assets/Sandbox/Prefabs/Relics/GreatStoneSlabRelic
    Assets/Sandbox/Prefabs/Relics/SingingVaseRelic
    Assets/Sandbox/Prefabs/Relics/FloatingSphereRelic
    Assets/Sandbox/Prefabs/Relics/TwinStatueRelic
    Assets/Sandbox/Prefabs/Relics/SlipperyFishStatueRelic
    Assets/Sandbox/Prefabs/Relics/MagneticHelmetRelic
  _hazardDensity = 0.4
  _routeOpenChance = 0.5
```

### 確認
- [ ] SpawnManager に8種の遺物Prefabが設定されている

---

## Step 12: 動作確認（プレイテスト）

**PlayMode で以下を確認:**

### 基本移動
- [ ] ベースキャンプからスタートできる
- [ ] WASD で移動、スペースでジャンプできる
- [ ] 全ゾーン（Zone1〜6）を歩いて登れる
- [ ] 地形の穴抜けがない

### グラップル
- [ ] GrappableRocks にロープが引っかかる
- [ ] 木（Zone1）にロープが引っかかる
- [ ] 神殿壁（Zone4）にロープが引っかかる

### スポーンシステム
- [ ] PlayMode開始時にコンソールに `[Spawn L3] 遺物 N 個を配置` が表示される
- [ ] コンソールに `[Spawn L2] ルート閉鎖: ...` が0〜4件表示される
- [ ] 遺物が RelicSpawnPoints 上にスポーンしている

### チェックポイント
- [ ] Zone1中腹を通過すると `[Checkpoint] Checkpoint 1 通過` が出る
- [ ] コンソールに最低1つのチェックポイント通過ログが出る

### ハザード
- [ ] Zone3 の CollapsiblePlatform に乗ると崩れる
- [ ] Zone5 の IcePatch でキャラクターが滑る（摩擦が低くなる）
- [ ] Zone4 の TempleTraps が起動する

### ゴール
- [ ] Zone6 の SummitGoalTrigger に入ると帰還フローが始まる
- [ ] ResultScreen が表示される

---

## Step 13: ライティング設定

### 基本ライティング（URP）
```
GameObject > Light > Directional Light:
  Rotation: (50, -30, 0)    ← 昼間の太陽角度
  Color: #FFF5E0             ← やや暖色
  Intensity: 1.0

GameObject > Volume (URP Volume):
  Profile を作成
  + Fog:
      Mode = Exponential
      Color = #C8D8E8
      Density = 0.003
      Max Distance = 400
  + Sky and Fog Volume（URP Sky）
```

### 確認
- [ ] シーンが明るく見える
- [ ] 遠景に薄い霧がかかっている

---

## Step 14: コミット

```bash
git add -A
git commit -m "feat: Stage01 Mountain01 MAP initial layout

- Add 7 zone hierarchy (Basecamp + Zone1-6)
- Add GrappableRocks (53 rocks), IcePatches (12), ReviveShrines (6)
- Add RouteGates (4), SpawnPoints (Relic:9, Hazard:9, Item:5)
- Add ZoneCheckpoint.cs and SummitGoalTrigger.cs
- Configure MountainTerrainGenerator (300x300m, seed=42)
- Add ZoneCheckpoints (5) and Checkpoints (4) for respawn
- Add Zone4 temple geometry with TempleTraps
- Add Zone5 IcePatches and snow couloir
- Add Zone6 Summit with goal trigger"

git push -u origin claude/map-development-peak-vfBva
```

---

## トラブルシューティング

| 問題 | 対処 |
|-----|------|
| `MountainTerrain` が生成されない | Mountain に `MountainTerrainGenerator` がアタッチされているか確認。`_resolution=513` (2^n+1) であること |
| オブジェクトが地形に埋まる | "Snap Objects to Terrain" を再実行。Y オフセット調整 |
| SpawnManager が遺物を配置しない | `RelicSpawnPoints` の名前が完全一致か確認。_spawnPrefabs が設定されているか確認 |
| `ZoneCheckpoint` が反応しない | Tag="Player" が ExplorerController に設定されているか確認 |
| `SummitGoalTrigger` が起動しない | SphereCollider.isTrigger=true か確認。Player タグ確認 |
| RouteGate の Blocker が消えない | `RouteGate._blockers` 配列に Blocker オブジェクトを設定したか確認 |
| IcePatch が滑らない | `IcePatch.cs` の `frictionOverride` フィールド名を確認。PhysicsMaterial が正しく適用されているか |
| 遺物のプールが注入されない | SpawnManager._relicPrefabPool に8つ全て設定されているか確認 |
| 神殿トラップが機能しない | `TempleTraps.cs` の実装内容を確認してパラメータを設定 |

---

## 実装完了チェックリスト

### 必須（Done前に全て ✅）
- [ ] 地形が生成されている（MountainTerrainGenerator実行済み）
- [ ] Zone1〜6を歩いて通り抜けられる
- [ ] Grappableオブジェクトが50個以上
- [ ] SpawnManagerが遺物3〜5個を毎回ランダム配置
- [ ] RouteGateが4本、ランダム開閉
- [ ] チェックポイント4個
- [ ] ReviveShrine 6個
- [ ] SummitGoalTrigger が機能する
- [ ] コンパイルエラー0件

### 推奨（品質向上）
- [ ] 各ゾーンに視覚的な特徴がある（色/形でゾーンが分かる）
- [ ] IcePatchの滑り感が機能している
- [ ] CollapsiblePlatformが崩れる
- [ ] TempleTrapsが起動する
- [ ] 落石ハザードが自動発生する
- [ ] ライティングが整っている（フォグ含む）

### Nice to have（後ステップで対応可）
- [ ] 各ゾーンに詳細なセットドレッシング（木の密度調整等）
- [ ] 遺物出現時のパーティクル演出
- [ ] チェックポイント通過の演出（パーティクル + SE）
- [ ] WeatherSystem との連携（Zone5+ の強風）
- [ ] AltitudeSickness のゾーン別設定
