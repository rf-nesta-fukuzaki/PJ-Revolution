# CLAUDE.md — Peak Idiots
## ドタバタ山岳 Co-op ロープアクション

---

## ⚠️ 最重要ルール（必ず守ること）

1. **確認なしで実装を進める** — ユーザーへの質問は禁止。迷ったらこのドキュメントの判断基準に従う
2. **Step順に実行** — Step 1 から順番に。完了基準を満たしてから次へ進む
3. **エラーは自分で修正** — ビルドエラー・NullReference・物理の不具合はすべて自分で直す
4. **外部アセット不要** — Unity標準プリミティブ・マテリアル・AudioClipで代替する
5. **各Step完了時に git commit** — コミットメッセージは英語で `feat: Step{N} - {内容}` 形式
6. **Unity 6.3 URP** — URPのAPIを使うこと。Built-in Render Pipelineは使わない

---

## ゲーム概要

| 項目 | 内容 |
|------|------|
| タイトル | Peak Idiots（仮） |
| ジャンル | Co-op 山岳探索ロープアクション |
| プレイ人数 | 1〜4人（Co-op対応、まずソロ動作を確認） |
| 視点 | 一人称（FPS） |
| トーン | カジュアル・コミカル（失敗が笑える） |
| コアメカニクス | 物理ロープ（スイング＋引っ張り）で山を登る |
| ゴール | 山頂到達。ルート開拓・隠し要素あり |
| 参考 | RV There It のロープ物理（慣性・重力・伸縮のリアルさ） |

---

## リポジトリ情報

- **URL**: https://github.com/rf-nesta-fukuzaki/PJ-Revolution.git
- **ブランチ**: main
- **Unity**: 6.3 URP

### 既存コードの扱い方針

| ファイル | 対応 |
|---------|------|
| `Assets/Scripts/PlayerMovement.cs` | ベース流用・大幅改造（ロープ中の挙動追加） |
| `Assets/Scripts/PlayerInputController.cs` | 流用・ロープ操作入力を追加 |
| `Assets/Scripts/FirstPersonLook.cs` | 流用・Update→LateUpdateに変更 |
| `Assets/Scripts/PlayerStateManager.cs` | 流用・Swinging/Climbingステート追加 |
| `Assets/Scripts/SurvivalStats.cs` | **削除** — サバイバル要素不要 |
| `Assets/Scripts/TorchSystem.cs` | **削除** — 洞窟要素不要 |
| `Assets/Scripts/SimpleSpawner.cs` | **削除** — 不要 |
| Marching Cubes 関連スクリプト | **全削除** — 洞窟生成不要 |
| NGO関連コード | **全削除** — 後のStepで再統合 |

---

## アーキテクチャ設計

### 新規作成スクリプト一覧

```
Assets/Scripts/
├── Player/
│   ├── PlayerMovement.cs        (既存を大幅改造)
│   ├── PlayerInputController.cs (既存を改造)
│   ├── FirstPersonLook.cs       (既存を改造)
│   ├── PlayerStateManager.cs    (既存を改造)
│   └── ClimbingController.cs    (新規)
├── Rope/
│   ├── RopeSystem.cs            (新規・最重要)
│   ├── GrappleHook.cs           (新規)
│   └── RopeRenderer.cs          (新規)
├── World/
│   ├── MountainGenerator.cs     (新規)
│   ├── CheckpointSystem.cs      (新規)
│   └── SummitGoal.cs            (新規)
├── UI/
│   ├── HudManager.cs            (新規)
│   └── TimerDisplay.cs          (新規)
├── Audio/
│   └── AudioManager.cs          (新規)
└── Game/
    ├── GameManager.cs           (新規)
    └── CoopManager.cs           (新規・後のStep)
```

---

## 物理ロープ設計仕様（RopeSystem.cs の実装指針）

RV There It のロープを参考にした物理ロープ。以下を必ず実装する。

### ロープの物理モデル
- **実装方式**: Verlet積分によるロープシミュレーション（`LineRenderer` + `GameObject[]` ノード配列）
- **ノード数**: 16〜24個（パフォーマンスと見た目のバランス）
- **制約解決**: 各フレームで隣接ノード間の距離制約を10回イテレーションで解く
- **重力**: 各ノードに `Physics.gravity` を適用
- **風の影響**: わずかなランダム揺れを加えてリアル感を出す

### ロープの2つの使い方

**① スイング（Swing）**
- 左クリック（またはRトリガー）でロープを発射
- 岩・木・地形にヒットしたらアンカーポイントを設定
- プレイヤーはアンカーを支点に振り子運動
- ロープ長を短くする（巻き取る）入力あり
- スイング中は空中制御を制限（慣性を活かす）

**② 引っ張り（Pull）**
- 右クリック（またはLトリガー）で引っ張りモード
- 岩・木・Co-opプレイヤーにヒットしたら引っ張る
- 引っ張り力: 500N（Rigidbodyに `AddForce`）
- 自分も引き寄せられる（質量比で力を分配）

### GrappleHook.cs の仕様
- Raycastで照準方向に発射（最大距離: 30m）
- ヒット可能タグ: `Grappable`（岩・木・地形・他プレイヤー）
- ヒット不可: `Player`（自分自身）
- ヒット演出: パーティクル + SE

---

## シーン構成

```
Scenes/
├── MainMenu      (タイトル・スタートボタン)
├── TestScene     (現在の開発用シーン → Mountain01に改名)
└── Mountain01    (メインゲームシーン)
```

### Mountain01 シーン構成

```
[Hierarchy]
├── GameManager
├── AudioManager
├── Mountain
│   ├── Terrain (ProceduralMesh or Unity Terrain)
│   ├── RockFormations (Grappableタグ付き)
│   ├── Trees (Grappableタグ付き)
│   └── Summit (SummitGoalコンポーネント)
├── Player
│   ├── PlayerMovement
│   ├── PlayerInputController
│   ├── FirstPersonLook
│   ├── GrappleHook
│   └── RopeSystem
├── Checkpoints
│   ├── Checkpoint_01
│   ├── Checkpoint_02
│   └── Checkpoint_03
├── UI
│   ├── HUD Canvas
│   └── TimerDisplay
└── Lighting
    ├── Directional Light
    └── Sky Volume (URP)
```

---

## Step 1: クリーンアップ＆プロジェクトセットアップ

### 作業内容

1. **不要ファイルの削除**
   - `SurvivalStats.cs` を削除
   - `TorchSystem.cs` を削除
   - `SimpleSpawner.cs` を削除
   - Marching Cubes関連スクリプトを全削除（ファイル名に "Marching", "Cave", "Voxel" を含むもの）
   - 削除後にコンパイルエラーがあれば全て修正する

2. **フォルダ構成を整理**
   - `Assets/Scripts/Player/` フォルダを作成
   - `Assets/Scripts/Rope/` フォルダを作成
   - `Assets/Scripts/World/` フォルダを作成
   - `Assets/Scripts/UI/` フォルダを作成
   - `Assets/Scripts/Audio/` フォルダを作成
   - `Assets/Scripts/Game/` フォルダを作成
   - 既存スクリプトを適切なフォルダに移動

3. **Tagの追加**
   - `Grappable` タグをプロジェクトに追加（Edit > Project Settings > Tags and Layers）
   - `Checkpoint` タグを追加

4. **URP設定確認**
   - URP Asset が設定されていることを確認
   - Universal Render Pipeline がGraphics Settingsに設定されていることを確認

### 完了基準
- [ ] コンパイルエラーが0件
- [ ] フォルダ構成が上記通りに整理されている
- [ ] `Grappable` タグが存在する
- [ ] git commit 済み

---

## Step 2: プレイヤー基本移動の改善

### 作業内容

既存の `PlayerMovement.cs` を以下の仕様で全面書き換えする。

**PlayerMovement.cs 仕様:**

```csharp
// 必須パラメータ
[Header("Movement")]
float moveSpeed = 5f;
float acceleration = 20f;       // 加速度（慣性あり）
float deceleration = 15f;       // 減速度
float airControlFactor = 0.3f;  // 空中制御係数

[Header("Jump")]
float jumpForce = 6f;
float coyoteTime = 0.12f;       // コヨーテタイム
float jumpBufferTime = 0.1f;    // ジャンプバッファ
float fallMultiplier = 2.5f;    // 落下時の追加重力倍率
float lowJumpMultiplier = 2f;   // 低ジャンプ時の追加重力倍率

[Header("Ground Check")]
float groundCheckRadius = 0.3f;
LayerMask groundLayer;          // 未設定時は自レイヤー以外を自動設定

[Header("Slope")]
float maxSlopeAngle = 45f;      // これ以上の斜面でスライド

[Header("Step Climb")]
float stepHeight = 0.4f;        // 登れる段差の最大高さ
float stepSmooth = 0.1f;        // ステップ補正のスムーズ係数

// 公開プロパティ（FirstPersonLookから参照）
public float SmoothStepOffset { get; private set; }
public bool IsGrounded { get; private set; }
public bool IsSwinging { get; set; }   // RopeSystemから設定される
```

**PlayerInputController.cs 改造内容:**
- `JumpRelease()` メソッドを追加（Input System の canceled イベントに接続）
- `FireRope()` — 左クリック/Rトリガー → GrappleHook.FireSwing() を呼ぶ
- `PullRope()` — 右クリック/Lトリガー → GrappleHook.FirePull() を呼ぶ
- `ReleaseRope()` — ロープ解放入力 → GrappleHook.Release() を呼ぶ

**FirstPersonLook.cs 改造内容:**
- `Update` → `LateUpdate` に変更
- `PlayerMovement.SmoothStepOffset` を参照してカメラローカルYを補正

**PlayerStateManager.cs 改造内容:**
- ステート追加: `Swinging`, `Climbing`, `Falling`
- 各ステートの遷移条件を定義

### 完了基準
- [ ] WASDで慣性のある移動ができる
- [ ] スペースでジャンプできる（コヨーテタイム・バッファあり）
- [ ] 段差を自動で登れる
- [ ] コンパイルエラーが0件
- [ ] git commit 済み

---

## Step 3: 物理ロープシステム実装（最重要Step）

### 作業内容

`Assets/Scripts/Rope/RopeSystem.cs` を新規作成する。

**RopeSystem.cs 完全仕様:**

```
クラス: RopeSystem : MonoBehaviour

フィールド:
  [Header("Rope Physics")]
  int ropeNodeCount = 20          // ロープのノード数
  float segmentLength = 0.5f      // 各セグメントの自然長
  float ropeStiffness = 0.8f      // 制約の硬さ (0〜1)
  int constraintIterations = 10   // 制約解決のイテレーション数
  float ropeMass = 0.1f           // 各ノードの質量
  float damping = 0.99f           // 速度の減衰（空気抵抗）
  float windStrength = 0.05f      // 風による揺れの強さ

  [Header("Swing")]
  float maxRopeLength = 30f       // ロープの最大長
  float reelSpeed = 3f            // 巻き取り速度 (m/s)
  float swingForce = 10f          // スイング時のプレイヤーへの力

  [Header("Pull")]
  float pullForce = 500f          // 引っ張り力 (N)
  float maxPullDistance = 25f     // 引っ張りの最大距離

  [Header("References")]
  LineRenderer lineRenderer       // Inspectorで設定
  Transform ropeStartPoint        // ロープの根元（カメラ前方）

メソッド:
  void SimulateRope()             // Verlet積分でロープ物理を更新
  void SolveConstraints()         // ノード間の距離制約を解く
  void ApplyPlayerForce()         // スイング時にプレイヤーへ力を加える
  void UpdateLineRenderer()       // LineRendererにノード位置を反映
  
  public void AttachSwing(Vector3 anchorPoint)  // スイングアンカーを設定
  public void AttachPull(Rigidbody target)       // 引っ張りターゲットを設定
  public void Release()                          // ロープを解放
  public void ReelIn(float amount)               // ロープを巻き取る
  
  bool IsAttached { get; }        // ロープが接続中か
  RopeMode CurrentMode { get; }  // Swing or Pull

enum RopeMode { None, Swing, Pull }
```

**GrappleHook.cs 完全仕様:**

```
クラス: GrappleHook : MonoBehaviour

フィールド:
  float maxGrappleDistance = 30f
  LayerMask grappableLayer        // "Grappable" タグのレイヤー
  float hookSpeed = 50f           // フック飛翔速度（演出用）
  GameObject hookProjectilePrefab // フックの見た目（なければSphere Primitiveで代替）

メソッド:
  public void FireSwing()   // カメラ正面方向にRaycast → ヒットしたらRopeSystem.AttachSwing()
  public void FirePull()    // カメラ正面方向にRaycast → ヒットしたらRopeSystem.AttachPull()
  public void Release()     // RopeSystem.Release() を呼ぶ

  void OnDrawGizmos()       // ロープの射程範囲をGizmosで表示（開発用）
```

**RopeRenderer.cs 仕様:**
- LineRendererの設定（太さ0.02〜0.05m、グラデーションで先端を細く）
- ロープのマテリアル（URP/Lit、茶色 or ベージュ）
- ロープ接続時のパーティクル演出（スパーク or 砂埃）

### 完了基準
- [ ] 左クリックでロープを発射できる
- [ ] `Grappable` タグのついた岩・地形にロープが引っかかる
- [ ] 引っかかった状態でスイング（振り子運動）ができる
- [ ] 右クリックで引っ張りモードが動作する
- [ ] ロープがLineRendererで視覚的に表示される
- [ ] ロープ解放で通常移動に戻る
- [ ] コンパイルエラーが0件
- [ ] git commit 済み

---

## Step 4: 山の地形生成

### 作業内容

`Assets/Scripts/World/MountainGenerator.cs` を新規作成する。

**地形設計:**

```
山の構造:
  - 高さ: 約200m
  - 形状: 不規則な岩山（Unity Terrainを使う）
  - ルート: 明確なメインルート + 隠しルート2〜3本
  - チェックポイント: 3〜4箇所（Checkpoint01〜04）
  - 山頂: 旗が立っている（SummitGoalコンポーネント）

地形生成方法:
  Unity Terrain を使用（Procedural Mesh は使わない）
  - TerrainData をコードで生成
  - Perlin Noiseで高さマップを生成
  - 岩場エリア: Terrain Detail Meshとして配置
  - Grappableタグ付きの岩オブジェクトを地形上にランダム配置（最低50個）
  - 木オブジェクト（Grappableタグ付き）を適度に配置（最低30本）
```

**MountainGenerator.cs 仕様:**
```
void GenerateTerrain()     // Perlin Noiseで高さマップ生成
void PlaceRocks()          // Grappableタグ付き岩を配置
void PlaceTrees()          // Grappableタグ付き木を配置
void PlaceCheckpoints()    // チェックポイントを配置
void PlaceSummit()         // 山頂ゴールを配置
```

**CheckpointSystem.cs 仕様:**
- プレイヤーがチェックポイントに触れたら記録
- 死亡（落下）時に最後のチェックポイントからリスポーン
- HUDにチェックポイント通過を表示

**SummitGoal.cs 仕様:**
- プレイヤーが山頂に到達したらゲームクリア
- クリアタイムを記録・表示
- "SUMMIT REACHED!" のUIを表示

### 完了基準
- [ ] 山の地形が生成される
- [ ] Grappableな岩・木が50個以上配置されている
- [ ] チェックポイントが3〜4個ある
- [ ] 山頂にゴールがある
- [ ] ロープで岩に引っかかって登れる
- [ ] git commit 済み

---

## Step 5: UI実装

### 作業内容

**HudManager.cs 仕様:**
```
表示要素:
  - タイマー（経過時間 mm:ss.ff形式）
  - 現在チェックポイント表示（"Checkpoint 2/4"）
  - ロープ状態インジケーター（接続中 / 未接続）
  - 十字線（クロスヘア）
  - ロープ射程範囲インジケーター（Grappableオブジェクトを照準したとき強調）
  - ミニマップ or 高度計（プレイヤーの高度を表示）
  - "SUMMIT REACHED!" クリア画面（タイム表示 + リトライボタン）
```

**TimerDisplay.cs:**
- ゲーム開始から山頂到達までの時間を計測
- mm:ss.ff 形式で表示
- 山頂到達時に停止・ベストタイムをPlayerPrefsに保存

**照準インジケーター:**
- 通常時: 小さな白い十字
- Grappableを照準中: 輪が現れる（色: 緑）
- ロープ接続中: 輪が塗りつぶされる（色: オレンジ）

### 完了基準
- [ ] タイマーが動いている
- [ ] チェックポイント進捗が表示される
- [ ] ロープ照準インジケーターが動作する
- [ ] クリア画面が表示される
- [ ] git commit 済み

---

## Step 6: サウンド実装

### 作業内容

**AudioManager.cs 仕様（シングルトン）:**
```csharp
// BGM
void PlayBGM(AudioClip clip, float volume = 0.5f)
void StopBGM()

// SE（Placeholder — AudioClipが無ければコードで波形生成）
void PlaySE(string name)
  // "rope_fire"    — ロープ発射音
  // "rope_attach"  — ロープ接触音
  // "rope_swing"   — スイング中の風切り音（ループ）
  // "footstep"     — 足音
  // "jump"         — ジャンプ音
  // "land"         — 着地音
  // "checkpoint"   — チェックポイント通過音
  // "summit"       — 山頂到達のファンファーレ
```

**AudioClipがない場合の代替:**
- `AudioClip` を `AudioClip.Create()` でコードから生成（サイン波 or ノイズ）
- BGMは `AudioSource` にProceduralなサイン波シーケンスを流す

### 完了基準
- [ ] ロープ発射・接続時にSEが鳴る
- [ ] 足音・ジャンプ・着地にSEがある
- [ ] 山頂到達時に演出音がある
- [ ] git commit 済み

---

## Step 7: ポリッシュ・ゲームフィール改善

### 作業内容

1. **ロープ物理のチューニング**
   - スイングの勢いが気持ちよく感じるまでパラメータ調整
   - ロープが岩に引っかかる瞬間のカメラシェイクを追加（0.1秒・振幅0.1）
   - スイング中の風切りエフェクト（LineRendererの太さを速度に連動）

2. **視覚的ポリッシュ**
   - 岩・地形のマテリアルをURP/Litで整える（グレー・茶色系）
   - 空のURP Volume設定（昼間の山の青空）
   - フォグ（遠景に薄霧）
   - プレイヤーの影

3. **ゲームフィール**
   - 落下死判定（Y < -10 でチェックポイントにリスポーン）
   - リスポーン演出（フェードアウト→フェードイン）
   - スイング成功時の軽い画面揺れ
   - 山頂到達時のパーティクル（紙吹雪 or 星）

4. **パフォーマンス**
   - ターゲット: 60fps（PC）
   - `Application.targetFrameRate = 60`
   - カメラのFarClipPlane: 500m（山の全景が見える距離）

### 完了基準
- [ ] スイングが「気持ちいい」と感じるレベルに調整されている
- [ ] 落下→リスポーンが機能する
- [ ] 60fps動作
- [ ] git commit 済み

---

## Step 8: Co-op準備とビルド

### 作業内容

1. **Co-op基盤（ローカルマルチの準備）**
   - `CoopManager.cs` を作成
   - PlayerPrefabを2〜4人分インスタンス化できる準備
   - 各プレイヤーが独立したカメラ・ロープを持てる構造に
   - ※ネットワーク（NGO）統合はこのStepではやらない。ローカルのみでOK

2. **MainMenuシーン**
   - タイトルロゴ（テキストでOK）
   - "PLAY" ボタン → Mountain01シーンをロード
   - "QUIT" ボタン

3. **ビルド設定**
   - Build Settings にシーンを追加（MainMenu, Mountain01）
   - Windows x64 でビルド
   - `Builds/PeakIdiots_v0.1/` に出力

4. **README.md を作成**
   ```markdown
   # Peak Idiots
   山頂を目指すCo-opロープアクションゲーム
   
   ## 操作方法
   - WASD: 移動
   - Space: ジャンプ
   - 左クリック: ロープ発射（スイング）
   - 右クリック: ロープ発射（引っ張り）
   - R: ロープ解放
   - マウス: 視点操作
   
   ## ゴール
   山頂に到達する。チェックポイントを経由して最速タイムを目指せ！
   ```

### 完了基準
- [ ] MainMenuからゲームが始まる
- [ ] Windows x64ビルドが成功する
- [ ] README.mdが存在する
- [ ] 最終コミット: `feat: v0.1 build complete`

---

## トラブルシューティング（よくある問題と対処）

| 問題 | 対処 |
|------|------|
| ロープがすり抜ける | Rigidbodyの`Collision Detection`をContinuousに変更 |
| スイングで飛びすぎる | `swingForce` を下げる。または速度にclampを設定 |
| ロープがビヨンビヨンしすぎる | `damping` を上げる（0.99→0.95）、`constraintIterations` を増やす |
| Terrain上のRaycastが通らない | TerrainのLayer設定を確認。`Physics.Raycast`のlayerMaskを確認 |
| LineRendererが見えない | URPのMaterialが設定されているか確認。`Sprites/Default`ではなく`Universal Render Pipeline/Lit`を使う |
| NullReferenceException | Start()での参照取得を確認。`GetComponent<>()`の結果をnullチェック |
| コンパイルエラーが残る | 削除したスクリプトへの参照があれば全て除去する |

---

## 判断基準（迷ったときのルール）

- **見た目よりも動作優先** — まず動くこと。綺麗さは後
- **プレイヤーが楽しい方** — 2択で迷ったら「スイングが気持ちいい方」を選ぶ
- **シンプルに** — 複雑な実装より動くシンプルな実装を優先
- **Primitiveで代替** — アセットがなければCube・Sphere・Cylinderで代替する
- **ユーザーに聞かない** — このドキュメントに答えが書いてある。書いていないことは最善と思う方を選ぶ

---

*このファイルをUnityプロジェクトのルート（`PJ-Revolution/CLAUDE.md`）に置いて、Claude Codeで `claude` を起動してください。*