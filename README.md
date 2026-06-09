# Peak Plunder / Peak Idiots

ドタバタ山岳 Co-op ロープアクション（Unity 6.3 / URP）。  
物理ロープで山を登り、遺物を運び、山頂を目指す一人称 Co-op アクション。

**ドキュメント索引:** [docs/README.md](docs/README.md)

---

## 操作方法

| 入力 | 動作 |
|------|------|
| WASD | 移動 |
| Space | ジャンプ |
| 左クリック | ロープ発射（スイング） |
| 右クリック | ロープ発射（引っ張り） |
| R | ロープ解放 |
| マウス | 視点操作 |
| ESC | ポーズ / カーソル |

---

## シーン構成

ゲームループは `GameFlow`（`Assets/Sandbox/Script/System/GameFlow.cs`）が管理します。
本番フローは **StartMenu → SandboxOfflineCombined → Shop** の 3 シーンです。

| 区分 | シーン | パス | 役割 |
|------|--------|------|------|
| フロー | StartMenu | `Assets/Sandbox/Scenes/StartMenu.unity` | フロー入口（起動シーン） |
| フロー | SandboxOfflineCombined | `Assets/Sandbox/Scenes/SandboxOfflineCombined.unity` | 遠征本編（`GameFlow.InGameScene`） |
| フロー | Shop | `Assets/Sandbox/Scenes/Shop.unity` | ベースキャンプ準備 |
| 補助 | MainMenu | `Assets/Sandbox/Scenes/MainMenu.unity` | メインメニュー |
| 検証 | Sandbox | `Assets/Sandbox/Scenes/Sandbox.unity` | 手続き地形の検証用 |
| 検証 | OfflineTestScene | `Assets/Sandbox/Scenes/OfflineTestScene.unity` | オフライン検証用 |
| MAP開発 | Gameplay / Mountain01 | `Assets/Sandbox/Scenes/Gameplay.unity` | Stage01 MAP 開発用（Editor 生成・本番フロー未登録） |

> **Stage01 MAP（`Gameplay.unity`）** は `Peak Plunder > Stage01 > Build Gameplay Scene` で生成する
> マップ設計検証用シーンで、現状の本番ループには組み込まれていません。詳細は [docs/map-stage01/](docs/map-stage01/)。
> Build Settings の登録状況は [docs/setup/manual-setup.md](docs/setup/manual-setup.md) を参照。

---

## ビルド

メニュー **Peak Plunder** から実行:

- **Build macOS** → `Builds/PeakIdiots_v0.1/macOS/PeakIdiots.app`
- **Build Windows x64** → `Builds/PeakIdiots_v0.1/Windows/PeakIdiots.exe`

---

## 開発クイックスタート

1. Unity **6000.3.x** でプロジェクトを開く
2. [docs/setup/manual-setup.md](docs/setup/manual-setup.md) のチェックリストを確認
3. Stage01: `Peak Plunder > Stage01 > Build Gameplay Scene`
4. エージェント作業時は [memory/PROJECT_MEMORY.md](memory/PROJECT_MEMORY.md) を参照

---

## 技術スタック

- Unity 6.3 / URP 17.3
- Input System
- Netcode for GameObjects + Relay/Lobby
- 実装本体: `Assets/Sandbox/`

---

## 関連ドキュメント

| 用途 | パス |
|------|------|
| ゲーム設計（GDD） | [docs/design/GDD.md](docs/design/GDD.md) |
| Stage01 マップ | [docs/map-stage01/](docs/map-stage01/) |
| アーキテクチャ | [docs/architecture/](docs/architecture/) |
| Claude Code Step 計画 | [CLAUDE.md](CLAUDE.md) |
