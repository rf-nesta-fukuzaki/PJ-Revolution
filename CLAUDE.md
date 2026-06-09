# CLAUDE.md — Peak Plunder / Peak Idiots

ドタバタ山岳 Co-op ロープアクション（Unity 6.3 / URP）。
このファイルは Claude Code 等の AI エージェント向けクイックガイドです。

> **ドキュメント索引:** [docs/README.md](docs/README.md)
> **現行実装の正本:** `Assets/Sandbox/` + [docs/design/GDD.md](docs/design/GDD.md) + [docs/map-stage01/](docs/map-stage01/)
> **エージェント作業メモ:** [memory/PROJECT_MEMORY.md](memory/PROJECT_MEMORY.md)
> 初期プロトタイプの Step 1–8 計画は [docs/archive/claude-step-plan.md](docs/archive/claude-step-plan.md) に退避（歴史的参照）。

---

## まず読むもの

1. [memory/PROJECT_MEMORY.md](memory/PROJECT_MEMORY.md) — アーキテクチャ・既存システム・命名・実装ルール
2. [docs/README.md](docs/README.md) — 全ドキュメントの索引
3. 着手領域に応じて [docs/design/GDD.md](docs/design/GDD.md) / [docs/map-stage01/](docs/map-stage01/)

---

## 作業の基本ルール

- **日本語で応答**する（コード識別子・パス・コマンドは英語のまま）。
- **既存システムを優先**して再利用する（`ExpeditionManager` / `SpawnManager` / `RouteGate` /
  `GameServices` / `RopeManager` / `AudioManager` / `SoundId` など）。重複マネージャを作らない。
- **外部アセット不要** — Unity プリミティブ・生成マテリアル・プロシージャル音で代替する。
- **コンパイルエラー 0 件**を維持する。Unity が無い環境では Unity 側検証を「保留」と明記する。
- **既存のユーザー変更を保全**する。無関係な差分を巻き戻さない。
- **コミットはユーザーが明示的に依頼したときのみ**行う。

詳細なコーディング規約は `.cursor/rules/unity-csharp.mdc`、横断サービス方針は
[docs/architecture/service-locator-policy.md](docs/architecture/service-locator-policy.md) を参照。

---

## ゲーム概要

| 項目 | 内容 |
|------|------|
| タイトル | Peak Plunder / Peak Idiots（仮） |
| ジャンル | Co-op 物理アクション / トレジャーハント（山岳ロープ） |
| プレイ人数 | 2〜4人（Co-op）。ソロ動作も確認する |
| 視点 | 一人称（FPS） |
| トーン | カジュアル・コミカル（失敗が笑える） |
| コアループ | 登攀 → 遺物発見 → 物理運搬 → 帰還判断 → リザルト |
| エンジン | Unity 6.3 / URP / Input System / Netcode for GameObjects |

詳細は [docs/design/GDD.md](docs/design/GDD.md)（GDD v5.0）。

---

## シーン構成

ゲームループは `GameFlow`（`Assets/Sandbox/Script/System/GameFlow.cs`）が管理する。

- 本番フロー: `StartMenu` → `SandboxOfflineCombined`（遠征本編）→ `Shop`
- Stage01 MAP: `Gameplay.unity`（`Peak Plunder > Stage01 > Build Gameplay Scene` で生成、本番フロー未登録）

Build Settings の現状は [docs/setup/manual-setup.md](docs/setup/manual-setup.md) 項目4を参照。

---

## リポジトリ

- URL: https://github.com/rf-nesta-fukuzaki/PJ-Revolution.git
- ブランチ: main
- Unity: 6.3 URP
