# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 6 (6000.3.8f1) プロジェクト。Universal Render Pipeline (URP 17.3.0) を使用。
プロジェクト名: **P-REVO-CCC** — シングルプレイ専用サバイバルゲーム（NGO 除去済み、全コンポーネント MonoBehaviour ベース）。

## Unity Version

Unity 6000.3.8f1 — この正確なバージョンで開く必要がある（`ProjectSettings/ProjectVersion.txt` 参照）。

## Build & Test Commands

```bash
# コマンドラインビルド (バッチモード)
Unity -quit -batchmode -projectPath . -buildTarget <platform> -executeMethod BuildScript.Build

# Edit Mode テスト実行
Unity -quit -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml

# Play Mode テスト実行
Unity -quit -batchmode -projectPath . -runTests -testPlatform PlayMode -testResults results.xml

# 特定テストのみ実行
Unity -quit -batchmode -projectPath . -runTests -testFilter <TestClassName.MethodName>
```

テストフレームワーク: Unity Test Framework v1.6.0 (NUnit ベース) + Performance Testing v3.2.0
テストファイルはまだ存在しない。追加する場合は `Assets/Tests/` に配置する。

## Architecture

### スクリプト構成 (`Assets/Scripts/`)

```
Scripts/
├── PlayerMovement.cs          # Rigidbody 物理移動、登攀モード切替 (SetClimbingMode)
├── PlayerInputController.cs   # 入力ハブ (WASD/ジャンプ/たいまつ)
├── PlayerStateManager.cs      # キャラクター状態管理（移動・登攀・インタラクト連携）
│                              # CurrentState プロパティ (PlayerState enum)
├── FirstPersonLook.cs         # マウス視点・カーソルロック
├── SurvivalStats.cs           # HP/酸素/空腹/ダウン状態管理。
│                              # public float Health/Oxygen/Hunger { get; private set; }
│                              # public bool IsDowned { get; private set; }
│                              # イベント: OnHealthChanged(float,float) / OnOxygenChanged /
│                              #          OnHungerChanged / OnIsDownedChanged(bool,bool) / OnDowned
│                              # API: ApplyStatModification(StatType, float)
│                              # StatType enum はここで定義
├── TorchSystem.cs             # 燃料消費・光強度・明滅エフェクト
├── UIManager.cs               # 燃料ゲージ・HP/酸素/空腹表示、インタラクトプロンプト
│                              # BindToPlayer(TorchSystem, SurvivalStats) で紐付け
├── GameManager.cs             # ゲーム状態管理（進行・勝敗判定）Singleton
├── ResultUI.cs                # リザルト画面 UI 制御
├── EscapeGate.cs              # 脱出ゲート判定（洞窟クリア条件）
├── Game/
│   └── SimpleSpawner.cs       # CaveGenerator.OnCaveGenerated 後にプレイヤーを
│                              # StartWorldPosition へ移動させる。
│                              # Inspector: _caveGenerator / _playerTransform を設定
├── Climbing/
│   ├── PlayerClimbing.cs       # ロープ登攀 (IsClimbing bool プロパティ)、スタミナ消費
│   └── RopeController.cs       # ロープ設置 (IsDeployed bool プロパティ)、LineRenderer
├── Cave/
│   ├── CaveGenerator.cs        # 洞窟生成統合コンポーネント（Cellular2D / MarchingCubes3D を
│   │                           # Inspector で切替）。Generate() 末尾で CaveContentPlacer を呼ぶ。
│   │                           # 公開 API: UsedSeed / StartWorldPosition / GoalCenterPosition /
│   │                           # Chunks / NoiseConfig / CenterOffset
│   ├── CaveContentPlacer.cs    # 洞窟生成後にクリスタル・食料・キノコを自動配置。
│   │                           # System.Random(seed) 使用（UnityEngine.Random 禁止）。
│   │                           # 床面検出は CaveChunk.GetScalar() によるスカラー場直接参照
│   │                           # （上からの Raycast は天井の岩で止まり床面に届かないため不使用）
│   ├── GlowCrystal.cs          # Point Light を sin カーブでパルス明滅させるクリスタル制御
│   ├── CaveVisualizer.cs       # 洞窟の可視化（Gizmos）
│   ├── MeshGenerator.cs        # ブロックメッシュ生成（2D 洞窟用）
│   ├── NoiseSettings.cs        # Serializable struct（ノイズパラメータ、isoLevel を含む）
│   ├── MarchingCubesTable.cs   # 256 パターン定数テーブル（Paul Bourke）
│   ├── CaveNoiseGenerator.cs   # 3D Perlin Noise + 重力バイアス（床形成を促進）
│   └── CaveChunk.cs            # 16×16×16 チャンク単位の Marching Cubes Mesh 生成。
│                               # GetScalar(lx,ly,lz) で空洞判定が可能
├── Interaction/
│   ├── IInteractable.cs            # Interact(GameObject interactor) と GetPromptText() のインターフェース
│   ├── ResourceItemType.cs         # enum: Food/OxygenTank/Medkit/FuelCanister
│   ├── PlayerInteractor.cs         # Raycast 検出。直接 _currentTarget.Interact(gameObject) 呼び出し
│   ├── ResourceItem.cs             # MonoBehaviour アイテム（Destroy で消える）
│   ├── PlacedResourceItem.cs       # MonoBehaviour アイテム（Instantiate 配置、Destroy で消える）
│   │                               # Collider が必須。なければ PlayerInteractor の Raycast が当たらない。
│   ├── CollectibleGem.cs           # MonoBehaviour 宝石。Interact() で GameManager.AddGem() を呼び Destroy。
│   └── DownedReviveInteractable.cs # ダウン中プレイヤーへのアダプター。IsDowned==true のときのみ
│                                   # プロンプトを返す。PlayerPrefab 子オブジェクトにアタッチして使用。
├── Enemy/
│   ├── BatAI.cs               # コウモリ型 AI ステートマシン (Sleeping→Alerted→Chasing→Attacking→Fleeing)。
│   │                          # Transform を直接操作（NavMesh 不使用）。
│   ├── BatPerception.cs       # 感知ロジック（起床/追尾/攻撃/退散の距離判定）。BatAI から利用される。
│   │                          # AddTarget/RemoveTarget でプレイヤー参照を管理。
│   └── BatSpawner.cs          # コウモリを SpawnPoints リストからスポーンするコンポーネント。
│                              # RegisterPlayerExternal(GameObject) で外部からプレイヤーを登録可。
├── Audio/
│   ├── FootstepAudio.cs        # ProximityAudioSource 経由で足音 SE を再生。速度連動間隔。
│   ├── ProximityAudioManager.cs # シーン全体のプレイヤー近接音声を一括管理。距離減衰・エコー。
│   └── ProximityAudioSource.cs  # MonoBehaviour。個別プレイヤー音源、3D スペーシャル・リバーブ制御。
├── Cosmetics/
│   ├── CosmeticDatabase.cs     # ScriptableObject。コスメアイテム定義一覧（Hat/Pickaxe/TorchSkin/Accessory）。
│   ├── CosmeticShopUI.cs       # コスメショップ UI（カテゴリ切替・購入・装備）。
│   ├── PlayerCosmetics.cs      # MonoBehaviour。装備 ID をローカル string で管理
│   │                           # (EquippedHat/Pickaxe/TorchSkin/Accessory プロパティ)。
│   └── PlayerCosmeticSaveData.cs # PlayerPrefs ベースの宝石数・解放済みアイテム保存。
├── Editor/
│   └── PrefabApplyHelper.cs    # Editor 専用: プレハブ変更適用ヘルパー。
└── UI/
    ├── HUDAnimator.cs          # コルーチンベース HUD 演出（点滅・フラッシュ・ビネット）。
    │                           # BindToPlayer(SurvivalStats, TorchSystem) で紐付け
    ├── LobbyUI.cs              # ロビー画面（ソロ開始のみ）。
    ├── OptionsUI.cs            # マウス感度・音量・解像度設定（PlayerPrefs 保存）。
    ├── PauseManager.cs         # ESC キーによる一時停止 UI 制御（PauseManager.IsPaused 静的プロパティ）。
    ├── TitleScreenUI.cs        # タイトル画面（Start/Options/Quit）。
    └── UIFlowController.cs     # Singleton。UIScreen (Title/Lobby/Playing/Result/CosmeticShop) 管理。
```

### 主要アセット

- **シーン**: `Assets/Scenes/SampleScene.unity`（メインシーン）
- **プレハブ**:
  - `Assets/Prefab/PlayerPrefab.prefab` / `RopePrefab.prefab`
  - `Assets/Prefab/Enemy/BatPrefab.prefab`
  - `Assets/Prefab/Cave/GlowCrystalPrefab.prefab` / `CrystalPrefab.prefab`
  - `Assets/Prefab/Item/`: `FoodItem.prefab` / `Food.prefab` / `Mushroom.prefab` / `OxygenItem.prefab` / `MedkitItem.prefab` / `FuelCanister.prefab` / `CollectibleGem.prefab` / `CrystalBlue.prefab` / `CrystalPurple.prefab` / `CrystalOrange.prefab`

### PlayerPrefab コンポーネント階層

```
PlayerPrefab (Root)
├── Rigidbody, CapsuleCollider
├── PlayerMovement / PlayerInputController / FirstPersonLook
├── PlayerStateManager / SurvivalStats
├── PlayerInteractor / PlayerClimbing
├── PlayerCosmetics
├── CameraRig (子オブジェクト)
│   └── Camera + AudioListener  ← 複数存在すると Unity 警告
└── TorchPivot (子オブジェクト)
    └── Light / TorchSystem
```

### コンテンツ配置システム

`CaveGenerator` と同じ GameObject に `CaveContentPlacer` をアタッチする。`Generate()` 末尾で自動呼び出しされ、Inspector の `[ContextMenu("Regenerate Cave")]` 再実行でも配置がリセット・再実行される。

**床面検出の重要な設計制約**:
3D モードで上から下への `Physics.Raycast` は洞窟の天井（岩の上面）でヒットして止まり、洞窟床面に届かない。そのため `CaveChunk.GetScalar(lx, ly, lz)` でスカラー場を直接参照し、「空洞（`< isoLevel`）の直下が岩（`>= isoLevel`）」となるセルを床面と判定する方式を採用している。

**左偏り修正**:
チャンクを x=0 から順に処理すると maxCrystals の上限が左側で消費されてしまう。全候補を先に収集し Fisher-Yates シャッフル（`System.Random` 使用）してから配置することで均一分布を実現している。

**OnCaveGenerated イベントのサブスクライバー**:
`CaveGenerator.OnCaveGenerated` は `CaveContentPlacer`・`BatSpawner`・`SimpleSpawner` がサブスクライブする。洞窟生成完了後に依存処理を追加する場合は必ずこのイベントを使う（`Generate()` 直後に直接呼ぶと配置・スポーンの順序が壊れる）。

### インタラクション設計

`IInteractable.Interact(GameObject interactor)` シグネチャ。`PlayerInteractor` は MonoBehaviour で、`_currentTarget.Interact(gameObject)` を直接呼ぶ。

### 入力

Input System パッケージ (1.18.0) がインストールされているが、スクリプトは**旧 Input API**（`Input.GetAxis`, `Input.GetKeyDown` 等）を使用している。`Assets/Scripts/InputSystem_Actions.inputactions` は現状未使用。

### レンダリング

- URP デュアルレンダラー構成: `Assets/Settings/PC_RPAsset.asset` (高品質) と `Assets/Settings/Mobile_RPAsset.asset` (最適化版)
- ポストプロセシング: `Assets/Settings/DefaultVolumeProfile.asset`

### 主要パッケージ

- **Input System** (1.18.0): インストール済みだが未移行
- **AI Navigation** (2.0.10): パスファインディング
- **Timeline** (1.8.10): シネマティクス / アニメーションシーケンス
- **Visual Scripting** (1.9.9): ビジュアルプログラミング
- **uGUI** (2.0.0): UI フレームワーク

## Notes

- C# スクリプトを編集する際は対応する `.meta` ファイルを手動で変更しない
- Editor 専用スクリプトは `Editor/` サブフォルダに配置する
- ScriptableObject を設定データの格納に使用するパターン
- `.claudeignore` でバイナリアセット (png, jpg, fbx 等)、Library/, Temp/, Build/ を除外済み
- 配置・生成に使うランダムは必ず `new System.Random(seed)` を使う（`UnityEngine.Random` は再現性がなく禁止）
- `PlacedResourceItem` を使う Prefab には Collider が必須（なければ E キーが反応しない）
- ロープ登攀の状態遷移: `isKinematic = true` をセット**してから**ロープへのスナップ処理を行うこと（逆順だとプレイヤーが壁に埋まる）。正確な切替順序: 「通常 → 登攀」は `velocity = zero` → `isKinematic = true`。「登攀 → 通常」は `isKinematic = false` → `AddForce(up, VelocityChange)`（micro-lift で壁埋まり防止）
- `SurvivalStats.ApplyStatModification(StatType, float)` はステータスを直接変更するときに使う（`ResourceItem`, `BatAI`, `DownedReviveInteractable` から呼ばれる）
- `BatSpawner.RegisterPlayerExternal(GameObject)` / `UnregisterPlayerExternal(GameObject)` でプレイヤーを手動登録・解除できる
- `BatSpawner` の天井候補シャッフルには `new System.Random(UsedSeed + 12345)` でシードオフセットを与える。`CaveContentPlacer`（オフセットなし）と同じ乱数列になると重複配置になるため
- `GameManager.RefreshPlayerCache()` はプレイヤーが動的スポーンされた後に呼ぶこと。スポーン時に自動呼び出しはされない
- `GameManager.AddGem(int)` は `CollectibleGem.Interact()` から呼ばれる。`CollectedGems` プロパティで合計収集数を参照可能
- `UIFlowController.GoTo(UIScreen)` が Canvas 表示の唯一の権限。他スクリプトから Canvas の `SetActive()` を直接呼ばないこと（状態不整合になる）。Options は `ToggleOptions()` / `SetOptionsVisible(bool)` を使う
- `UIFlowController` は `DontDestroyOnLoad` を設定していないため、シーンリロード時に再取得が必要（現状は1シーン設計のため意図的）
- `CosmeticDatabase` の各カテゴリには `_isDefault = true` のアイテムが最低 1 つ必要（`PlayerCosmeticSaveData.LoadData()` がデフォルトを初期適用するため）
- `PlayerCosmetics` は PlayerPrefab ルートにアタッチし、各スロット（`_hatSlot` / `_pickaxeSlot` / `_torchSkinSlot` / `_accessorySlot`）を Inspector で設定する
