# ccc（Peak Plunder）— 手動導入が必要な項目レポート

**作成日:** 2026-04-15  
**対象ディレクトリ:** `Assets/Sandbox`  
**参照GDD:** `Assets/claude-manual/GDD_ccc.md`

---

## 目次
1. [調査サマリー](#1-調査サマリー)
2. [必須パッケージ — Unity Gaming Services (UGS)](#2-必須パッケージ--unity-gaming-services-ugs)
3. [必須設定 — UGS ダッシュボード連携](#3-必須設定--ugs-ダッシュボード連携)
4. [必須設定 — シーン登録 (Build Settings)](#4-必須設定--シーン登録-build-settings)
5. [必須設定 — TextMesh Pro Essential Resources](#5-必須設定--textmesh-pro-essential-resources)
6. [必須設定 — Input System 設定 (Active Input Handling)](#6-必須設定--input-system-設定-active-input-handling)
7. [推奨パッケージ — ProBuilder（レベルデザイン）](#7-推奨パッケージ--probuilderレベルデザイン)
8. [オプション — ボイスチャット SDK (Vivox / Photon Voice)](#8-オプション--ボイスチャット-sdk-vivox--photon-voice)
9. [オプション — Post Processing / URP Volume](#9-オプション--post-processing--urp-volume)
10. [設定 — Layer & Tag 設定](#10-設定--layer--tag-設定)
11. [設定 — NetworkManager コンフィグ](#11-設定--networkmanager-コンフィグ)
12. [設定 — DOTween 初期化](#12-設定--dotween-初期化)
13. [設定 — Unity Cloud Project ID & Organization](#13-設定--unity-cloud-project-id--organization)
14. [クイックスタートチェックリスト](#14-クイックスタートチェックリスト)

---

## 1. 調査サマリー

`Assets/Sandbox` 配下のスクリプト・プレハブ・シーンを GDD と照合し、以下のカテゴリで手動導入が必要な項目を洗い出しました。

| カテゴリ | 状態 | 緊急度 |
|---------|------|--------|
| UGS パッケージ（Relay, Lobby, Auth） | ✅ manifest.json に記載済み | — |
| Netcode for GameObjects | ✅ 導入済み (v2.9.2) | — |
| URP | ✅ 導入済み (v17.3.0) | — |
| Input System | ✅ 導入済み (v1.18.0) | — |
| UGS ダッシュボード設定 | ⚠️ 手動設定が必要 | **必須** |
| シーンの Build Settings 登録 | ⚠️ 手動設定が必要 | **必須** |
| TextMesh Pro Essential Resources | ⚠️ 初回インポート必要 | **必須** |
| Active Input Handling 確認 | ⚠️ 確認・設定が必要 | **必須** |
| ProBuilder | ❌ 未導入 | 推奨 |
| Vivox / Photon Voice | ❌ 未導入（フォールバック動作） | オプション |
| Post Processing Volume | ❌ 未導入（天候演出） | オプション |
| Layer / Tag 設定 | ⚠️ 手動設定が必要 | **必須** |
| NetworkManager Inspector 設定 | ⚠️ 手動設定が必要 | **必須** |
| DOTween 初期化 | ⚠️ Scripting Define あり | 確認 |
| Cloud Project ID | ⚠️ 確認が必要 | **必須** |

---

## 2. 必須パッケージ — Unity Gaming Services (UGS)

### 現状
`Packages/manifest.json` には以下が記載済みです:

```json
"com.unity.services.authentication": "3.3.3",
"com.unity.services.core": "1.13.0",
"com.unity.services.lobby": "1.2.2",
"com.unity.services.relay": "1.1.1",
"com.unity.netcode.gameobjects": "2.9.2"
```

**→ パッケージ自体の追加作業は不要です。**  
ただし、サービスの有効化（ダッシュボード設定）は別途必要です（項目3を参照）。

---

## 3. 必須設定 — UGS ダッシュボード連携

### 何が必要か
`NetworkBootstrap.cs` が `UnityServices.InitializeAsync()` と `AuthenticationService.Instance.SignInAnonymouslyAsync()` を呼びます。  
`LobbyManager.cs` が Unity Relay と Unity Lobby を使用します。  
これらが動作するには、**Unity Cloud Dashboard でのサービス有効化が必須**です。

### 手順

#### 3.1 Unity Cloud Dashboard でプロジェクトをリンク
1. ブラウザで [Unity Cloud Dashboard](https://cloud.unity.com/) にアクセス
2. Organization: `nesta_fukuzaki`（ProjectSettings から確認済み）でログイン
3. プロジェクト一覧からプロジェクト `P-REVO-CCC` を探す
   - 存在しない場合は「Create Project」で新規作成

#### 3.2 サービスの有効化
ダッシュボードのプロジェクトページで以下のサービスを有効化:

| サービス | 用途 | 有効化手順 |
|---------|------|-----------|
| **Authentication** | 匿名サインイン | Services → Authentication → Enable |
| **Relay** | NAT越えP2P通信 | Services → Relay → Enable |
| **Lobby** | ルームコード制マッチング | Services → Lobby → Enable |

#### 3.3 Unity Editor でプロジェクトをリンク
1. Unity Editor を開く
2. `Edit > Project Settings > Services`
3. 「Link to Unity Cloud Project」をクリック
4. Organization と Project を選択してリンク
5. Project ID が `ProjectSettings.asset` の `cloudProjectId` に自動で書き込まれる

> **現在の状態:**  
> `cloudProjectId: c868cb91-257c-4b69-a35a-8ce101294cc4`（設定済み）  
> `organizationId: nesta_fukuzaki`（設定済み）  
> ダッシュボード側のサービス有効化状況は未確認のため、上記を実施してください。

---

## 4. 必須設定 — シーン登録 (Build Settings)

### 何が必要か
`LobbyManager.StartGameAsync()` は `NetworkManager.Singleton.SceneManager.LoadScene("Mountain01", ...)` を呼びます。  
NGO の SceneManager はビルド設定に含まれたシーンのみロード可能です。

### 手順
1. `File > Build Settings`
2. 以下のシーンを Scenes In Build に追加（ドラッグ&ドロップ）:

| 順序 | シーンパス | 用途 |
|:---:|-----------|------|
| 0 | `Assets/Sandbox/Scene/MainMenu.unity` | メインメニュー（起動シーン） |
| 1 | `Assets/Sandbox/Scene/Mountain01.unity` | ゲーム本体（遠征マップ） |
| 2 | `Assets/Sandbox/Scene/Sandbox.unity` | テスト用（任意） |

3. MainMenu をインデックス0に設定（起動シーン）
4. **Build Target が Standalone (Windows/Mac) になっていることを確認**（GDD: Steam PC）

---

## 5. 必須設定 — TextMesh Pro Essential Resources

### 何が必要か
以下のスクリプトが `TMPro` 名前空間を使用しています:
- `MainMenuManager.cs` — `TMP_InputField`, `TMP_Text`
- `BasecampShop.cs` — `TextMeshProUGUI`
- `ExpeditionHUD.cs` — `TextMeshProUGUI`
- `ResultScreen.cs` — `TextMeshProUGUI`
- `WeatherHudIndicator.cs` — `TextMeshProUGUI`
- `PlayerResultRow.cs`, `RelicHudEntry.cs`, `TitleRowEntry.cs`

`Assets/TextMesh Pro` ディレクトリは存在しますが、**Essential Resources が正しくインポートされているか確認が必要**です。

### 手順
1. Unity Editor を開く
2. `Window > TextMeshPro > Import TMP Essential Resources`
3. Import ダイアログが表示されたら「Import」をクリック
4. `Assets/TextMesh Pro/Resources/` 配下にフォントアセットとシェーダーが展開されることを確認

> **注:** TMP を使う UI 要素を含むシーンを初めて開いた際に自動的にインポートダイアログが表示されるケースもあります。

---

## 6. 必須設定 — Input System 設定 (Active Input Handling)

### 何が必要か
`com.unity.inputsystem` (v1.18.0) がインストール済みです。  
`ProjectSettings.asset` では `activeInputHandler: 1` が設定されています。

| 値 | 意味 |
|:--:|------|
| 0 | Old (Legacy) Input Manager のみ |
| 1 | **New Input System のみ** |
| 2 | Both (両方有効) |

### 現状の問題
複数のスクリプトが **Legacy Input (`Input.GetKeyDown`, `Input.GetAxis`)** を直接使用しています:
- `PlayerInteraction.cs` — `Input.GetKeyDown(KeyCode.E/F/G)`
- `ClimbingController.cs` — `Input.GetKeyDown(KeyCode.E)`, `Input.GetAxis("Vertical")`
- `ExplorerController.cs` — `Input.GetAxis`, `Input.GetButtonDown`
- `GhostSystem.cs` — `#if ENABLE_INPUT_SYSTEM` で条件分岐あり

`activeInputHandler: 1`（New のみ）の場合、**Legacy Input API がランタイムで例外を投げます**。

### 対応策（いずれかを選択）

#### 方法A: Both モードに変更（推奨）
1. `Edit > Project Settings > Player > Other Settings`
2. `Active Input Handling` を `Both` に変更
3. エディタ再起動を求められるので「Yes」

#### 方法B: 全 Legacy Input をリファクタリング
- `PlayerInteraction.cs`, `ClimbingController.cs`, `ExplorerController.cs` の `Input.GetKeyDown` / `Input.GetAxis` を新 Input System の `InputAction` に置き換える
- `Assets/InputSystem_Actions.inputactions` が既に存在するため、これを活用可能

**→ 方法A が最も低コストで、既存コードの互換性を維持できます。**

---

## 7. 推奨パッケージ — ProBuilder（レベルデザイン）

### 何が必要か
GDD §7.3:
> 固定地形：Unity Terrain + **ProBuilder**。AI生成後に必要に応じて微調整。

`MountainTerrainGenerator.cs` は Unity Terrain で地形を生成しますが、洞窟・遺跡・建物の内部構造は ProBuilder でモデリングする設計です。

### 現状
manifest.json に `com.unity.probuilder` は含まれていません。

### 手順
1. Unity Editor で `Window > Package Manager`
2. 「Unity Registry」タブを選択
3. 検索欄に `ProBuilder` と入力
4. `ProBuilder` を選択して「Install」

```
パッケージ名: com.unity.probuilder
推奨バージョン: 6.x（Unity 6 対応の最新版）
```

> GDD §11.4 に明記されているため推奨ですが、初期動作確認には不要です。

---

## 8. オプション — ボイスチャット SDK (Vivox / Photon Voice)

### 何が必要か
GDD §3.3:
> プロキシミティボイスチャット（R.E.P.O.型）  
> 距離が離れると声が小さくなる

`VivoxVoiceService.cs` のファイルヘッダに詳細手順が記載されています。  
`VoiceBackendFactory.Create()` は SDK 未検出時に `null` を返し、`AudioSource` シミュレーションにフォールバックします。

### 現在の状態
- `UNITY_VIVOX` / `PHOTON_VOICE_DEFINED` は Scripting Define Symbols に未設定
- SDK パッケージは未インストール
- **→ AudioSource フォールバックで動作します（リアルボイスチャットは無効）**

### Vivox 導入手順（UGS 統合）
1. Unity Dashboard でプロジェクトに **Vivox サービスを有効化**
2. Package Manager → `com.unity.services.vivox` をインストール
3. `Edit > Project Settings > Player > Scripting Define Symbols` に `UNITY_VIVOX` を追加
4. Dashboard で Vivox の AppID / SecretKey を確認
5. ビルドして動作確認

### Photon Voice 2 導入手順（代替）
1. [Photon Voice 2 SDK](https://www.photonengine.com/voice) をダウンロード
2. `.unitypackage` をインポート
3. `Scripting Define Symbols` に `PHOTON_VOICE_DEFINED` を追加
4. Photon Dashboard で Voice AppID を取得
5. `PhotonServerSettings` に設定

> **EA版ではフォールバック（AudioSource シミュレーション）で開発を進め、後から Vivox を統合する方針が妥当です。**

---

## 9. オプション — Post Processing / URP Volume

### 何が必要か
GDD §7.3:
> 天候：Particle System + **Post Processing**。風はRigidbodyへのForce適用。

GDD §11.4:
> Particle System + Post Processing（天候）

### 現状
- URP v17.3.0 が導入済み（URP にはビルトイン Post Processing = Volume が組み込み）
- `WeatherSystem.cs` は `RenderSettings.fog` / `RenderSettings.fogDensity` を使用（基本的なフォグのみ）
- URP Volume（ブルーム、被写界深度、カラーグレーディングなど）は未使用

### 手順（天候演出の強化時）
1. シーンに `Volume` オブジェクトを作成（`GameObject > Volume > Global Volume`）
2. Volume Profile を作成
3. 以下のオーバーライドを追加:
   - **Bloom** — 雪の反射、フレアガンの光
   - **Vignette** — 吹雪・高山病の視界制限
   - **Color Adjustments** — 天候による色温度変化
   - **Depth of Field** — 霧時のぼかし演出
4. `WeatherSystem.cs` から `Volume.profile` を動的に変更する処理を追加

> **追加パッケージのインストールは不要です（URP 内蔵）。Volume コンポーネントの手動配置と Profile 設定が必要です。**

---

## 10. 設定 — Layer & Tag 設定

### 何が必要か
`ClimbingController.cs` が `_grabLayer` (LayerMask) を使用してグラブポイントを検出しています。  
`GrabPoint.cs` のオブジェクトがこのレイヤーに属している必要があります。

### 手順
1. `Edit > Project Settings > Tags and Layers`
2. User Layer に以下を追加（空き番号に割り当て）:

| レイヤー名 | 用途 | 使用箇所 |
|-----------|------|---------|
| `GrabPoint` | 登攀可能ポイント | `ClimbingController._grabLayer` |
| `Relic` | 遺物オブジェクト | Raycast フィルタリング（将来拡張） |
| `Ghost` | 幽霊プレイヤー | 物理干渉無効化 |

3. シーン内の GrabPoint オブジェクトに `GrabPoint` レイヤーを設定
4. `ClimbingController` Inspector で `_grabLayer` に `GrabPoint` レイヤーを選択
5. Physics 衝突マトリクス（`Edit > Project Settings > Physics`）で `Ghost` レイヤーの衝突を全無効化

---

## 11. 設定 — NetworkManager コンフィグ

### 何が必要か
NGO のマルチプレイが動作するには、シーン上に以下のコンポーネントが正しく設定された `NetworkManager` オブジェクトが必要です。

### 手順

#### 11.1 NetworkManager オブジェクト
1. MainMenu シーンに `GameObject` を作成、名前を `NetworkManager` に
2. 以下のコンポーネントを追加:
   - `NetworkManager`
   - `UnityTransport`（Relay 用トランスポート）

#### 11.2 NetworkManager Inspector 設定
| 設定項目 | 値 | 備考 |
|---------|-----|------|
| Player Prefab | `Assets/Sandbox/Prefabs/PlayerPrefab.prefab` | **手動アサイン** |
| Network Prefabs List | `Assets/Sandbox/Prefabs/PeakPlunderNetworkPrefabs.asset` | **手動アサイン** |
| Protocol Type | Unity Transport | デフォルト |
| Tick Rate | 30 | 推奨 |
| Run in Background | ✅ 有効 | `PlayerSettings` で設定 |

#### 11.3 Network Prefabs List の内容確認
`PeakPlunderNetworkPrefabs.asset` に以下のプレハブが登録されていることを確認:
- `PlayerPrefab.prefab`
- 全遺物プレハブ（8種）: `GoldenDuckRelic.prefab`, `CrystalCupRelic.prefab` 等

#### 11.4 Run in Background の有効化
1. `Edit > Project Settings > Player > Resolution and Presentation`
2. `Run In Background` を ✅ チェック

> **注:** 現在 `runInBackground: 0`（無効）になっています。マルチプレイではバックグラウンド実行が必須です。

---

## 12. 設定 — DOTween 初期化

### 現状
`ProjectSettings.asset` の `scriptingDefineSymbols` に `DOTWEEN` が全プラットフォームで設定されています。  
しかし `Assets/Sandbox` 内のスクリプトでは `DOTween` を使用しているコードは見つかりませんでした。

### 確認事項
1. DOTween アセットが `Assets/Plugins/DOTween` 等にインポートされているか確認
2. インポートされている場合: `Tools > Demigiant > DOTween Utility Panel > Setup DOTween` を実行
3. 使用していない場合: Scripting Define Symbols から `DOTWEEN` を削除しても問題なし

---

## 13. 設定 — Unity Cloud Project ID & Organization

### 現状
```yaml
cloudProjectId: c868cb91-257c-4b69-a35a-8ce101294cc4
organizationId: nesta_fukuzaki
projectName: P-REVO-CCC
```

### 確認事項
- Unity Cloud Dashboard にログインし、上記 `cloudProjectId` がダッシュボード上のプロジェクトと一致するか確認
- 一致しない場合、`Edit > Project Settings > Services` から再リンク
- **UGS サービス（Relay, Lobby, Auth）が有効になっているか Dashboard で確認**

---

## 14. クイックスタートチェックリスト

実機テストまでに必要な手順を優先度順にリストアップします。

### ✅ Phase 1: 最低限動作させる（必須）

- [ ] **Unity Cloud Dashboard でサービス有効化**
  - Authentication 有効化
  - Relay 有効化
  - Lobby 有効化
- [ ] **Editor で Services をリンク**
  - `Edit > Project Settings > Services` → Link Project
- [ ] **TextMesh Pro Essential Resources のインポート**
  - `Window > TextMeshPro > Import TMP Essential Resources`
- [ ] **Active Input Handling を `Both` に変更**
  - `Edit > Project Settings > Player > Other Settings > Active Input Handling → Both`
  - エディタ再起動
- [ ] **Build Settings にシーンを登録**
  - `MainMenu.unity` → Index 0
  - `Mountain01.unity` → Index 1
- [ ] **Layer の追加**
  - `GrabPoint` レイヤーを追加
  - GrabPoint オブジェクトに設定
  - `ClimbingController` の `_grabLayer` にアサイン
- [ ] **NetworkManager の設定**
  - MainMenu シーンに `NetworkManager` + `UnityTransport` を追加
  - Player Prefab に `PlayerPrefab.prefab` をアサイン
  - Network Prefabs List に `PeakPlunderNetworkPrefabs.asset` をアサイン
- [ ] **Run in Background を有効化**
  - `Edit > Project Settings > Player → Run In Background` を ✅

### ✅ Phase 2: ゲーム品質向上（推奨）

- [ ] **ProBuilder のインストール**
  - Package Manager → `com.unity.probuilder` をインストール
- [ ] **URP Volume の配置**
  - Mountain01 シーンに Global Volume を追加
  - 天候演出用の Volume Profile を作成
- [ ] **DOTween の確認・Setup**
  - DOTween Utility Panel で Setup 実行

### ✅ Phase 3: 製品版準備（オプション）

- [ ] **Vivox SDK の導入**
  - `com.unity.services.vivox` インストール
  - `UNITY_VIVOX` Scripting Define 追加
  - Dashboard で Vivox サービス設定
- [ ] **Build Target を Standalone (Windows) に確定**
- [ ] **Steamworks SDK の統合**（GDD: Steam Early Access）

---

## 付録: スクリプト一覧と依存関係マップ

### ネットワーク層 (要 UGS 設定)
| スクリプト | 依存サービス |
|-----------|-------------|
| `NetworkBootstrap.cs` | UGS Core, Authentication |
| `LobbyManager.cs` | UGS Lobby, Relay, Authentication |
| `NetworkPlayerSpawner.cs` | NGO |
| `NetworkExpeditionSync.cs` | NGO |
| `NetworkRelicSync.cs` | NGO, NetworkTransform, NetworkRigidbody |
| `NetworkStretcherSync.cs` | NGO |
| `ProximityVoiceChat.cs` | NGO, (Vivox / Photon Voice optional) |
| `VivoxVoiceService.cs` | (Vivox / Photon Voice, 条件付きコンパイル) |

### UI 層 (要 TMP)
| スクリプト | TMP 使用 |
|-----------|---------|
| `MainMenuManager.cs` | `TMP_InputField`, `TMP_Text` |
| `BasecampShop.cs` | `TextMeshProUGUI` |
| `ExpeditionHUD.cs` | `TextMeshProUGUI` |
| `ResultScreen.cs` | `TextMeshProUGUI` |
| `WeatherHudIndicator.cs` | `TextMeshProUGUI` |
| `PlayerResultRow.cs` | `TextMeshProUGUI` |
| `RelicHudEntry.cs` | `TextMeshProUGUI` |
| `TitleRowEntry.cs` | `TextMeshProUGUI` |

### ゲームプレイ層 (要 Layer/Physics 設定)
| スクリプト | 必要設定 |
|-----------|---------|
| `ClimbingController.cs` | `_grabLayer` LayerMask |
| `GrabPoint.cs` | GrabPoint レイヤー |
| `GhostSystem.cs` | Ghost レイヤー（物理非干渉） |

---

*このレポートは `Assets/Sandbox` 配下のスクリプトとプロジェクト設定を静的解析した結果に基づきます。*
