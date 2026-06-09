# Stage 01 「Mountain01」 MAP設計書 — 概要とスコープ

**対象シーン:** `Assets/Sandbox/Scenes/Gameplay.unity`（Stage01 MAP 開発用・本番フロー未登録）
**ビルド/検証:** `Peak Plunder > Stage01 > Build Gameplay Scene` / `Validate Gameplay Scene`
**実装ツール:** Cursor（Unity エディター操作 + C# スクリプト補完）
**参考タイトル:** PEAK（Landfall Games）Stage 1 の登攀体験

---

## 1. ステージコンセプト

「下から上への一本道に見えるが、実は複数ルートが存在する」山岳ステージ。

PEAKのStage 1を参考に：
- **低地は広く緩やか**（チームが動きを掴む入門エリア）
- **中腹で地形が険しく**なり、ロープ必須の場面が出てくる
- **高地は視界が狭く**なりながら最終目標（山頂）が近づく
- **毎回3箇所以上ルートが変わる**（RouteGate ランダム開閉）

### 山の全体スペック
| 項目 | 値 | 備考 |
|------|----|----|
| 地形サイズ | 300m × 300m | MountainTerrainGenerator設定 |
| 最高標高 | 520m | terrainHeight。fBm山岳生成、岩壁帯、稜線張り出し、巨大岩峰を追加 |
| 解像度 | 513 | 2^9 + 1 |
| 山頂座標（目安） | (0, terrain, +129) | World座標。YはTerrain SampleHeightで決定 |
| ベースキャンプ座標 | (0, ~2, -138) | World座標（平坦エリア） |

### PEAK系の山岳シルエット方針

- Terrain単体ではなく、`PeakScaleLandmarks` の巨大岩壁・外周連峰・山頂尖塔で「見上げる山」を作る
- `PeakAscentSetpieces` の細い岩棚、ロープ壁、氷のナイフリッジで「登るのが怖い」ルート感を作る
- 中央ルートは完全な平坦道にせず、休憩棚と壁登りを交互に配置して高度感を出す
- 山頂直下は背面尖塔と最終壁で、到達前に圧迫感が出る構図にする

---

## 2. ゾーン構成（7ゾーン）

GDDの6ゾーン＋ベースキャンプ。地形ジェネレーターの `s_zoneNz / s_zoneH` 配列に対応。

| ゾーン名 | GameObject名 | World Z（目安） | 標高（目安） | 特徴 |
|---------|------------|--------------|------------|------|
| ベースキャンプ | `Basecamp` | Z ≈ -138 | 0〜5m | 平坦、安全地帯、出発ゲート |
| Zone1 森林帯 | `Zone1_Forest` | Z ≈ -96 | 5〜135m | 木・緩斜面、入門ロープ場面 |
| Zone2 岩場帯 | `Zone2_RockySlope` | Z ≈ -45 | 135〜315m | 岩多め、最初のルート分岐 |
| Zone3 急壁 | `Zone3_CliffWall` | Z ≈ +6 | 315〜425m | 垂直に近い崖、ロープ必須 |
| Zone4 神殿遺跡 | `Zone4_TempleRuins` | Z ≈ +45 | 380〜470m | 遺跡建物、トラップ、高価値遺物 |
| Zone5 氷壁 | `Zone5_IceWall` | Z ≈ +84 | 455〜505m | 氷面・強風、IcePatch多数 |
| Zone6 山頂遺跡 | `Zone6_Summit` | Z ≈ +129 | 505〜520m | ゴールエリア、最高難度遺物 |

---

## 3. 実装スコープ（Cursorへの作業範囲）

### ✅ このドキュメントセットでカバーする内容

| カテゴリ | ドキュメント |
|---------|-----------|
| シーン階層構造 + 必須コンポーネント | `MAP_02_シーン構成.md` |
| ゾーン別地形・プロップ配置 | `MAP_03_ゾーン詳細.md` |
| スポーンポイント配置テーブル | `MAP_04_スポーン配置.md` |
| 実装ステップ・検証チェックリスト | `MAP_05_実装手順.md` |

### ❌ このドキュメントセットではカバーしない

- 3Dアセット（モデル）の作成（Primitive代替を使用）
- ネットワーク同期の詳細調整
- BGM / SE のアセット制作
- ポストプロセッシング設定（後ステップ）

---

## 4. 既存スクリプト活用マップ

Cursorが再利用すべき既存スクリプト。**新規スクリプト作成は最小限に。**

| スクリプト | 用途 | ファイルパス |
|-----------|------|-----------|
| `MountainTerrainGenerator` | 地形生成（ContextMenuで実行） | `Script/System/MountainTerrainGenerator.cs` |
| `SpawnManager` | L2〜L5 ランダムスポーン制御 | `Script/System/SpawnManager.cs` |
| `SpawnPoint` | 個別スポーンポイント | `Script/System/SpawnPoint.cs` |
| `RouteGate` | ルート開閉ゲート | `Script/System/RouteGate.cs` |
| `ReviveShrine` | 復活の祠 | `Script/System/ReviveShrine.cs` |
| `RockfallTrigger` | 落石ハザード | `Script/Hazard/RockfallTrigger.cs` |
| `IcePatch` | 氷ハザード | `Script/Hazard/IcePatch.cs` |
| `CollapsiblePlatform` | 崩れ足場 | `Script/Hazard/CollapsiblePlatform.cs` |
| `TempleTraps` | 神殿トラップ | `Script/Hazard/TempleTraps.cs` |
| `ExpeditionManager` | 遠征フロー管理（既存） | `Script/System/ExpeditionManager.cs` |
| `DepartureGate` | 出発ゲート | `Script/System/DepartureGate.cs` |
| `ReturnZone` | 帰還ゾーン | `Script/System/ReturnZone.cs` |
| `CheckpointSystem` | チェックポイント | `Script/System/CheckpointSystem.cs` |（※参照あり、実装確認要）

---

## 5. 新規作成が必要なスクリプト

既存スクリプトで対応できない部分のみ新規作成する。

### `SummitGoalTrigger.cs`（新規）
山頂到達判定。既存の `ReturnZone` か `ReviveShrine` パターンを参考に作成。

```csharp
// Assets/Sandbox/Script/System/SummitGoalTrigger.cs
// - OnTriggerEnter でプレイヤー到達を検出
// - ExpeditionManager.Instance.ReturnToBase(allSurvived: true) を呼ぶ
// - 到達演出（パーティクル + SE）を再生
// - GDD §2.1 の「帰還判断」として機能
```

### `ZoneCheckpoint.cs`（新規）
ゾーン境界チェックポイント。ExpeditionManager のチェックポイント配列と連携。

```csharp
// Assets/Sandbox/Script/System/ZoneCheckpoint.cs
// - OnTriggerEnter でプレイヤーを検出
// - ExpeditionManager.Instance.OnCheckpointReached(checkpointIndex) を呼ぶ
// - チェックポイント番号は Inspector で設定（0始まり）
// - 1回だけ発火（2回目以降は無視）
```

---

## 6. タグ・レイヤー設定（必須前提作業）

UnityエディターのProject Settings > Tags and Layersで追加。

### 追加するタグ
| タグ名 | 用途 |
|-------|------|
| `Grappable` | グラップル可能な岩・木・遺跡壁面 |
| `Checkpoint` | ゾーンチェックポイントトリガー |
| `ReturnZone` | 帰還ゾーントリガー |
| `SummitGoal` | 山頂到達トリガー |

### 追加するレイヤー
| レイヤー名 | 用途 |
|----------|------|
| `Ground` | 地形（Terrain） |
| `Grappable` | グラップル対象オブジェクト |
| `Hazard` | ハザードオブジェクト |

---

## 7. プレイアブル体験ゴール（完了基準）

以下がすべて動作すること：

- [ ] ベースキャンプからスタートし、山頂（Zone6）まで歩いて登れる
- [ ] 各ゾーンに `Grappable` タグの岩/木が配置され、グラップルが引っかかる
- [ ] `SpawnManager` が毎回異なる遺物・ハザード配置を生成する
- [ ] Zone2以上でルート分岐（RouteGate）が機能し、開閉がランダムになる
- [ ] Zone3〜5で落石・氷・崩れ足場のハザードに遭遇する
- [ ] Zone4の神殿エリアでトラップに触れる
- [ ] Zone5で `IcePatch` によりキャラクターが滑る
- [ ] Zone6山頂で `SummitGoalTrigger` が起動し帰還フローに入る
- [ ] 3〜5個のチェックポイントが機能し、死亡時にリスポーンできる
- [ ] 4〜6個の復活の祠（ReviveShrine）が山中に存在する
