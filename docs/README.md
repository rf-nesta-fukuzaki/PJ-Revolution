# Peak Plunder / Peak Idiots — ドキュメント索引

プロジェクト内の設計・実装ドキュメントの**正本（canonical）**はすべて `docs/` 配下に集約しています。

## クイックナビ

| 目的 | ドキュメント |
|------|-------------|
| ゲーム全体の設計（GDD） | [design/GDD.md](design/GDD.md) |
| Stage01 マップ仕様 | [map-stage01/](map-stage01/) |
| エージェント向け作業メモ | [../memory/PROJECT_MEMORY.md](../memory/PROJECT_MEMORY.md) |
| プロジェクト概要・操作・ビルド | [../README.md](../README.md) |
| Unity 初回セットアップ | [setup/manual-setup.md](setup/manual-setup.md) |
| サービス取得ポリシー | [architecture/service-locator-policy.md](architecture/service-locator-policy.md) |

## シーン構成（要点）

ゲームループは `GameFlow`（`Assets/Sandbox/Script/System/GameFlow.cs`）が管理。

- **本番フロー:** `StartMenu` → `SandboxOfflineCombined`（遠征本編）→ `Shop`
- **Stage01 MAP:** `Gameplay.unity` は `Peak Plunder > Stage01 > Build Gameplay Scene` で生成する
  マップ開発用シーン。現状の本番フローには未登録（詳細は [map-stage01/](map-stage01/)）。

Build Settings の登録状況は [setup/manual-setup.md](setup/manual-setup.md) 項目4を参照。

---

## ディレクトリ構成

```
docs/
├── README.md                 ← このファイル（索引）
├── design/                   ゲームデザイン
├── map-stage01/              Stage01 Mountain01 マップ設計（正本）
├── architecture/             アーキテクチャ・監査・技術設計
├── setup/                    環境構築・手動設定
├── tools/                    AI 開発ツール向けマニュアル
└── archive/                  完了済み計画・旧仕様・セッション引き継ぎ
```

---

## design/ — ゲームデザイン

| ファイル | 内容 |
|---------|------|
| [GDD.md](design/GDD.md) | **ccc / Peak Plunder** 正式 GDD v5.0。コアループ、遺物、Co-op、UI、音響、EA スコープ |

---

## map-stage01/ — Stage01 マップ設計

Stage01（`Assets/Sandbox/Scenes/Gameplay.unity`）の**正本仕様**。Cursor ルール `.cursor/rules/stage01-map.mdc` もここを参照します。

| ファイル | 内容 |
|---------|------|
| [MAP_00_クイックリファレンス.md](map-stage01/MAP_00_クイックリファレンス.md) | よく使うパス・enum・タグ・検証コマンド |
| [MAP_01_概要とスコープ.md](map-stage01/MAP_01_概要とスコープ.md) | ステージコンセプト・ゾーン構成・スコープ |
| [MAP_02_シーン構成.md](map-stage01/MAP_02_シーン構成.md) | Hierarchy 構造・GameManager 構成 |
| [MAP_03_ゾーン詳細.md](map-stage01/MAP_03_ゾーン詳細.md) | 7 ゾーンの詳細設計 |
| [MAP_04_スポーン配置テーブル.md](map-stage01/MAP_04_スポーン配置テーブル.md) | SpawnPoint / RouteGate 配置 |
| [MAP_05_実装手順.md](map-stage01/MAP_05_実装手順.md) | ビルド・検証・実装手順 |
| [MAP_06_実装メモ.md](map-stage01/MAP_06_実装メモ.md) | Unity 環境メモ・Editor メニュー・検証ログ |

**Editor メニュー:** `Peak Plunder > Stage01 > Build Gameplay Scene` / `Validate Gameplay Scene`

---

## architecture/ — アーキテクチャ

| ファイル | 内容 |
|---------|------|
| [service-locator-policy.md](architecture/service-locator-policy.md) | `GameServices` 一本化ポリシー（EditMode テストで強制） |
| [code-audit-report.md](architecture/code-audit-report.md) | Sandbox スクリプト監査レポート（2026-04-15） |
| [item-phase-b-winch-stretcher.md](architecture/item-phase-b-winch-stretcher.md) | 担架・ウインチ NGO 同期設計（Phase B） |

---

## setup/ — 環境構築

| ファイル | 内容 |
|---------|------|
| [manual-setup.md](setup/manual-setup.md) | UGS / NGO / Build Settings / Layer&Tag 等の手動設定チェックリスト |

---

## tools/ — AI 開発ツール

| ファイル | 内容 |
|---------|------|
| [claude-code-manual.md](tools/claude-code-manual.md) | Claude Code 操作マニュアル |
| [codex-manual.md](tools/codex-manual.md) | Codex 操作マニュアル |
| [templates/AGENTS.md](tools/templates/AGENTS.md) | プロジェクト用 AGENTS テンプレート |

---

## archive/ — 履歴・アーカイブ

完了済み・参照専用。新規作業の正本にしないこと。

| ファイル | 内容 |
|---------|------|
| [claude-step-plan.md](archive/claude-step-plan.md) | 初期プロトタイプ Step 1–8 計画（旧 CLAUDE.md 本文・**旧 `Assets/Scripts` 前提**） |
| [ngo-removal-plan.md](archive/ngo-removal-plan.md) | NGO 除去計画（当時は完了。**現在は NGO 再統合済み** — 履歴参照） |
| [session-handoff-sandbox-terrain.md](archive/session-handoff-sandbox-terrain.md) | 手続き地形 Chunk システムのセッション引き継ぎ |
| [title-scene-manual.md](archive/title-scene-manual.md) | 旧 TitleScene マニュアル（**現行コードと不一致・無効**） |

---

## ルート直下のその他ファイル

| ファイル | 役割 |
|---------|------|
| [../README.md](../README.md) | プレイヤー向け概要・操作・ビルド |
| [../CLAUDE.md](../CLAUDE.md) | AI エージェント向けクイックガイド（現行版） |
| [../memory/PROJECT_MEMORY.md](../memory/PROJECT_MEMORY.md) | Cursor エージェント向けコンパクト作業メモ |

---

## ドキュメント優先順位（矛盾時）

1. **現行コード** — 実装が最終真実
2. **docs/map-stage01/** — Stage01 マップ仕様
3. **docs/design/GDD.md** — プロダクト全体設計
4. **memory/PROJECT_MEMORY.md** — エージェント作業コンテキスト
5. **CLAUDE.md** — AI エージェント向けクイックガイド（上記への入口）

> 旧 Step 1–8 計画は [archive/claude-step-plan.md](archive/claude-step-plan.md) に退避済み（歴史的参照）。

矛盾を見つけたら、最小限の正本を更新し、索引（このファイル）から辿れるようにする。
