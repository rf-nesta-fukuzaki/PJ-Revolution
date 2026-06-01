# Peak Idiots

ドタバタ山岳 Co-op ロープアクション（Unity 6.3 / URP）。
手続き生成の山岳地形を、物理ロープでスイング・登攀して山頂を目指す一人称アクション。

## 操作方法

| 入力 | 動作 |
|------|------|
| WASD | 移動（慣性あり） |
| Space | ジャンプ（コヨーテタイム / バッファ） |
| 左クリック | ロープ発射（スイング） |
| 右クリック | ロープ発射（引っ張り） |
| R | ロープ解放 |
| マウス | 視点操作 |
| ESC | カーソル解除 |

## ゴール

山頂の旗に到達するとクリア。チェックポイントを経由し、最速タイムを目指す。
谷へ落下すると最後のチェックポイント（未通過ならスタート地点）にリスポーン。

## シーン構成（Build Settings）

| Index | シーン | 役割 |
|-------|--------|------|
| 0 | `Assets/Sandbox/Scene/StartMenu.unity` | スタートメニュー（PLAY / QUIT） |
| 1 | `Assets/Sandbox/Scene/Sandbox.unity` | メインゲーム（手続き地形 + ゲームループ） |

`StartMenu` の PLAY で `Sandbox` をロードする。`Sandbox` は起動時に地形生成・プレイヤー生成・
HUD/タイマー/チェックポイント/山頂ゴール・環境演出（空/雲海/フォグ/標高帯）を自動構築する。

## ビルド方法

メニュー **Peak Idiots** から実行（Build Settings の有効シーンをそのまま使用）:

- **Build macOS** — 現行 macOS 向け。出力 `Builds/PeakIdiots_v0.1/macOS/PeakIdiots.app`
- **Build Windows x64 (switches active target)** — Windows 向け。実行時に active build target を
  Windows へ切替えるため、初回はフル再インポートが走り時間がかかる。出力 `Builds/PeakIdiots_v0.1/Windows/PeakIdiots.exe`

> 注: Windows ビルドは macOS 上ではターゲット切替（再インポート）を伴う。CI/専用マシンでの実行を推奨。

## 技術メモ

- **地形**: チャンク制の手続き生成（Ridged Multifractal + GPU 水理/熱浸食 + バイオーム分類 + GPU 配置）。
  非同期 `Physics.BakeMesh` でチャンク毎にコライダー生成。
- **環境演出**: 標高帯シェーダ overlay、URP フォグ、プロシージャル空（時刻サイクル）、雲海、URP Volume
  （Bloom / Vignette / Tonemapping / Color Adjustments）。
- **ゲームループ**: `Assets/Sandbox/Script/World/Integration/` の統合コンポーネント群が、既存の
  単機プレイ層（PlayerMovement / RopeSystem / GrappleHook / TimerDisplay / HudManager /
  CheckpointSystem / SummitGoal / AudioManager）を手続き地形上に接続する。
- **Co-op**: NGO 2.11 + Unity Transport 導入済み。シーン側統合は今後の作業（ローカル分割画面 or NGO）。
