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
- `TitleRef_RoundedBold SDF` のアトラスが全面不透明（opaqueRatio=1.000）となり、起動時に不正アトラス扱いされていた。
- その結果、`CozyCaveTitleController` の安全化処理で毎回 `NotoSansJP_Rebuilt SDF` へ差し替えられ、見た目の崩れと□化リスクが残っていた。

### 対策
1. `TitleRef_RoundedBold SDF` を動的アトラス再構築（`ClearFontAssetData` + `TryAddCharacters`）し、正常なグリフテーブルへ復旧。
2. `TitleRef_RoundedBold_Fixed.mat` / `TitleRef_RoundedBold_Footer.mat` の `_MainTex` を復旧後アトラスへ再バインド。
3. `TitleScece.unity` の `readableFallbackFontAsset` を復旧済みタイトルフォントへ変更（Noto は fallback テーブルで保持）。
4. 起動時プリウォームは継続し、必要グリフを先読みして初回描画欠けを予防。

## 4. セットアップ手順

1. `Assets/Resources/Title/DefaultTitleSceneConfig.asset` を設定。
2. `Assets/TextMesh Pro/Resources/TMP Settings.asset` の fallback に `NotoSansJP_Rebuilt SDF` が含まれていることを確認。
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
