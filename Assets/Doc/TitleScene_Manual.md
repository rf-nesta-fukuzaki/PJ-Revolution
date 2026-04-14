# TitleScene Manual

## 1. システム全体像（アーキテクチャ）

TitleScene は以下の3層で構成されています。

1. **Presentation Layer**
- `CozyCaveTitleController`
- `CozyTitleButtonFx`
- `TitleSceneDeluxeFx`
- `TitleSceneReferenceResolver`
- `TitleSceneViewFacade`

2. **Application Layer**
- `TitleMenuInteractor`
- `TitleCommand`

3. **Domain Layer**
- `TitleSceneStateMachine`
- `TitleMenuAction`
- `TitleSceneTrigger`

加えて、TMP表示安定化専用として `TitleSceneTmpStabilizer` を導入しています。

## 2. 主要クラスの役割

### `CozyCaveTitleController`
- TitleScene のメインオーケストレータ。
- 設定読み込み、参照解決、イントロ再生、メニューイベント、シーン遷移を管理。
- 起動時に `TitleSceneTmpStabilizer` を呼び、壊れたフォントアトラスを検知した場合は安全フォントへ再バインド。

### `TitleSceneTmpStabilizer`
- TMP表示不具合（□化）を回避する安全化ユーティリティ。
- 主な機能:
- アトラス健全性判定（過剰不透明アトラス検知）
- フォールバックフォント解決（`TMP Settings` fallback優先）
- 表示不能フォントの一括置換

### `CozyTitleButtonFx`
- 各メニューボタンのホバー/プレス演出とSE制御。
- `Configure` 時に旧 `onClick` 購読を解除してから再登録し、重複購読を防止。

### `TitleSceneDeluxeFx`
- 背景パララックス、グロー、スパークル等の演出。
- `LateUpdate` に早期リターンを追加し、パララックス無効時の不要計算を回避。

### `TitleMenuInteractor` / `TitleSceneStateMachine`
- メニュー操作を状態遷移へ変換する中核ロジック。
- 表示層から遷移ルールを分離し、テスト容易性を確保。

## 3. TMP表示不具合の根因と対策

### 根因
- `TitleRef_RoundedBold SDF` のアトラスが壊れており、グリフ矩形が実質的にブロック描画される状態だった。
- その結果、TitleScene テキストが編集直後や再生時に四角化（豆腐化）しやすい状態になっていた。

### 対策
1. `TitleRef_RoundedBold SDF` を英字専用文字セットで動的アトラス再構築（`ClearFontAssetData` + `TryAddCharacters`）し、正常なSDFへ復旧。
2. `TitleSceneConfig.DefaultPreloadCharacters` から日本語文字を除去し、英字運用へ統一。
3. `TitleRef_RoundedBold SDF` の fallback 依存を除去し、TitleScene では英字フォント単体で表示を完結。
4. `TMP Settings` の fallback 依存を外し、日本語フォントへの自動切り替えを行わない方針へ変更。

## 4. セットアップ手順

1. `Assets/Resources/Title/DefaultTitleSceneConfig.asset` を設定。
2. `Assets/UI/Title/Fonts/TitleRef_RoundedBold SDF.asset` が英字専用文字セットで再構築済みであることを確認。
3. `TitleScece.unity` 上の `CozyCaveTitleController` が有効であることを確認。
4. 再生して、メニュー文字が四角化せず表示されることを確認。

## 5. 検証・テスト戦略

### EditMode（実装済み）
- `TitleSceneEditModeTests`
- 設定アセット妥当性
- BuildSettings整合性
- メニュー参照バインド整合性
- TMP fallback 設定整合性
- TMP安定化ロジック適用確認
- `TitleSceneTmpStabilizerTests`
- アトラス健全性判定ロジックの境界テスト

### PlayMode（推奨シナリオ）
- TitleScene 起動後、可視TMPが四角化しないこと。
- メニュー操作（Start/Settings/Credits/Exit）中にNREが発生しないこと。
- イントロ中連打時でも状態遷移が破綻しないこと。
- 低FPS時でも `TitleSceneDeluxeFx` の演出が破綻しないこと。

## 6. 運用メモ

- フォントアセットを差し替える場合は、`TitleSceneTmpStabilizer` の判定で誤検知しないかを再確認する。
- 新規TMPラベル追加時は、`menuEntries` と `TitleSceneReferenceResolver` の自動解決前提を崩さないこと。
- 大規模改修時は `TitleMenuInteractor` / `TitleSceneStateMachine` の責務境界を維持する。
