# Everything Claude Code (ECC) 完全マニュアル

**バージョン:** ECC 1.10.0 / Claude Code 2.1.90  
**作成日:** 2026-04-10  
**リポジトリ:** https://github.com/affaan-m/everything-claude-code  
**プラグイン所在:** `~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/`

---

## 目次

1. [Claude Code CLIリファレンス](#1-claude-code-cliリファレンス)
2. [Claude Code 組み込みコマンド](#2-claude-code-組み込みコマンド)
3. [ECC概要と特徴](#3-ecc概要と特徴)
4. [インストール状況確認](#4-インストール状況確認)
5. [コンポーネント構成](#5-コンポーネント構成)
6. [ECCコマンド早見表（全79コマンド）](#6-eccコマンド早見表全79コマンド)
7. [エージェント一覧（全47エージェント）](#7-エージェント一覧全47エージェント)
8. [スキル一覧（全181スキル）](#8-スキル一覧全181スキル)
9. [フック（Hooks）システム](#9-フックhooksシステム)
10. [ルール（Rules）システム](#10-ルールrulesシステム)
11. [コンテキストモード](#11-コンテキストモード)
12. [実践的なワークフロー](#12-実践的なワークフロー)
13. [環境変数・設定リファレンス](#13-環境変数設定リファレンス)
14. [MCP設定](#14-mcp設定)
15. [プラグイン管理](#15-プラグイン管理)
16. [キーボードショートカット](#16-キーボードショートカット)
17. [トラブルシューティング](#17-トラブルシューティング)
18. [ECC 2.0 アルファ](#18-ecc-20-アルファ)

---

## 1. Claude Code CLIリファレンス

`claude` コマンドの全オプション（`claude --help` より検証済み）。

### 基本構文

```bash
claude [options] [command] [prompt]
```

### 主要オプション

| オプション | 説明 |
|-----------|------|
| `-p, --print` | 非インタラクティブ出力（パイプ用）。終了後にセッションを閉じる |
| `-c, --continue` | 現在のディレクトリの最新会話を継続 |
| `-r, --resume [value]` | セッションIDで会話を再開。値なしでピッカーを開く |
| `-n, --name <name>` | セッションに表示名をつける（`/resume` やターミナルタイトルに表示） |
| `-v, --version` | バージョン番号を表示 |
| `-h, --help` | ヘルプを表示 |
| `-d, --debug [filter]` | デバッグモード。カテゴリフィルタ可（例: `"api,hooks"` や `"!1p,!file"`） |
| `--verbose` | 設定のverboseモードを上書き |

### モデル・パフォーマンス

| オプション | 説明 |
|-----------|------|
| `--model <model>` | モデル指定。エイリアス可（`sonnet`, `opus`）またはフルID（`claude-sonnet-4-6`） |
| `--effort <level>` | 努力レベル: `low`, `medium`, `high`, `max` |
| `--fallback-model <model>` | デフォルトモデルが過負荷の場合のフォールバック（`--print`のみ） |
| `--max-budget-usd <amount>` | API呼び出しの最大費用（`--print`のみ） |

### ツール制御

| オプション | 説明 |
|-----------|------|
| `--tools <tools...>` | 利用可能ツールのリスト。`""` で全無効、`"default"` で全有効 |
| `--allowedTools <tools...>` | 許可するツール（例: `"Bash(git:*) Edit"`） |
| `--disallowedTools <tools...>` | 拒否するツール |
| `--add-dir <directories...>` | ツールアクセスを許可する追加ディレクトリ |
| `--permission-mode <mode>` | 権限モード: `acceptEdits`, `bypassPermissions`, `default`, `dontAsk`, `plan`, `auto` |
| `--dangerously-skip-permissions` | 全権限チェックをバイパス（サンドボックス専用） |

### エージェント・コンテキスト

| オプション | 説明 |
|-----------|------|
| `--agent <agent>` | セッション用エージェントを指定 |
| `--agents <json>` | カスタムエージェントをJSONで定義 |
| `--system-prompt <prompt>` | セッションのシステムプロンプトを指定 |
| `--append-system-prompt <prompt>` | デフォルトシステムプロンプトに追記 |
| `--mcp-config <configs...>` | JSONファイルまたは文字列からMCPサーバーをロード |
| `--strict-mcp-config` | `--mcp-config` のMCPのみ使用。他の設定は無視 |
| `--plugin-dir <path>` | このセッションのみプラグインをロード（繰り返し可） |
| `--settings <file-or-json>` | 追加設定のJSONファイルまたはJSON文字列 |
| `--setting-sources <sources>` | ロードする設定ソース（`user`, `project`, `local`） |

### セッション管理

| オプション | 説明 |
|-----------|------|
| `--session-id <uuid>` | 特定セッションIDを使用（有効なUUID必須） |
| `--fork-session` | 再開時に新しいセッションIDを作成（`--resume`/`--continue`と組み合わせ） |
| `--no-session-persistence` | セッションをディスクに保存しない（`--print`のみ） |
| `--from-pr [value]` | PR番号/URLで紐づいたセッションを再開、またはピッカーを開く |

### 出力フォーマット

| オプション | 説明 |
|-----------|------|
| `--output-format <format>` | 出力形式（`--print`のみ）: `text`（デフォルト）, `json`, `stream-json` |
| `--input-format <format>` | 入力形式（`--print`のみ）: `text`（デフォルト）, `stream-json` |
| `--include-partial-messages` | 到着したチャンクを含める（`--print` + `stream-json`のみ） |
| `--include-hook-events` | フックイベントを出力ストリームに含める（`stream-json`のみ） |
| `--replay-user-messages` | ユーザーメッセージをstdoutに再出力 |
| `--json-schema <schema>` | 構造化出力バリデーション用JSONスキーマ |

### 特殊モード

| オプション | 説明 |
|-----------|------|
| `--bare` | 最小モード: フック・LSP・プラグイン同期・帰属等をスキップ |
| `--ide` | 起動時にIDEに自動接続（有効なIDEが1つの場合） |
| `--chrome` | Chrome統合を有効化 |
| `--worktree [name]` / `-w` | このセッション用に新しいgit worktreeを作成 |
| `--tmux` | worktree用にtmuxセッションを作成（`--worktree`が必要） |
| `--brief` | エージェントからユーザーへの通信用`SendUserMessage`ツールを有効化 |
| `--disable-slash-commands` | 全スキルを無効化 |
| `--betas <betas...>` | APIリクエストに含めるベータヘッダー（APIキーユーザーのみ） |
| `--file <specs...>` | 起動時にダウンロードするファイルリソース |

### サブコマンド

| コマンド | 説明 |
|---------|------|
| `claude agents` | 設定済みエージェントを一覧表示 |
| `claude auth` | 認証を管理 |
| `claude auto-mode` | オートモードクラシファイア設定を検査 |
| `claude doctor` | Claude Codeの自動アップデーター健康チェック |
| `claude install [target]` | Claude Codeネイティブビルドをインストール（`stable`, `latest`, 特定バージョン） |
| `claude mcp` | MCPサーバーを設定・管理 |
| `claude plugin` / `claude plugins` | プラグインを管理 |
| `claude setup-token` | 長期認証トークンを設定 |
| `claude update` / `claude upgrade` | アップデートをチェックしてインストール |

### 使用例

```bash
# 通常起動（インタラクティブ）
claude

# 特定プロンプトを非インタラクティブで実行
claude -p "このコードのバグを見つけて修正してください"

# 前回のセッションを継続
claude -c

# Opusモデルを使用
claude --model opus

# ストリーミングJSON出力
claude -p "分析して" --output-format stream-json

# ビルドエラーを自動修正（ツール制限あり）
claude -p "/build-fix" --allowedTools "Bash,Read,Edit"

# git worktreeで独立セッション
claude -w feature-branch

# tmux付きworktree
claude -w feature-branch --tmux
```

---

## 2. Claude Code 組み込みコマンド

セッション内で使える組み込みスラッシュコマンド。ECCのコマンドとは別物。

| コマンド | 説明 |
|---------|------|
| `/help` | ヘルプを表示 |
| `/clear` | 会話履歴をクリア |
| `/compact [instructions]` | コンテキストを圧縮。カスタム圧縮指示を指定可 |
| `/config` | Claude Code設定を表示・変更 |
| `/cost` | セッションのトークン使用量とコストを表示 |
| `/doctor` | セットアップの問題を診断 |
| `/exit` | Claude Codeを終了 |
| `/fast` | Fastモードのトグル（より高速な出力） |
| `/ide` | IDEインテグレーションを管理 |
| `/init` | CLAUDE.mdを作成してプロジェクトを初期化 |
| `/login` | Anthropicアカウントにサインイン |
| `/logout` | Anthropicアカウントからサインアウト |
| `/mcp` | MCPサーバーの接続状況を確認 |
| `/memory` | メモリファイルを表示・編集 |
| `/model` | AIモデルを切り替え |
| `/permissions` | 権限設定を表示 |
| `/plugins` | プラグインブラウザを開く |
| `/pr-comments` | PRコメントを取得 |
| `/release-notes` | 最新のリリースノートを表示 |
| `/resume` | 前のセッションを再開 |
| `/review` | コードレビューを実行 |
| `/status` | ステータスラインの表示設定を管理 |
| `/terminal-setup` | シェル統合をインストール |
| `/vim` | Vimモード（テキスト入力のVimキーバインド） |

---

## 3. ECC概要と特徴

Everything Claude Code (ECC) は Claude Code の**パフォーマンス最適化システム**。スキル・エージェント・フック・ルール・継続学習を包括した設定バンドル。

**実績:**
- Anthropic Hackathon Winner
- 10ヶ月以上の実プロダクト開発で磨き上げた設定

**5つのコア原則:**

| 原則 | 内容 |
|------|------|
| Agent-First | 専門エージェントにドメインタスクを委譲 |
| Test-Driven | 実装前にテストを書く、カバレッジ80%以上必須 |
| Security-First | セキュリティを妥協しない、全入力を検証 |
| Immutability | 常に新しいオブジェクトを作成、既存オブジェクトを変更しない |
| Plan Before Execute | コード前にプランを立てる |

---

## 4. インストール状況確認

```bash
# プラグイン一覧（Claude Code内）
/plugins

# インストール済みプラグイン確認
cat ~/.claude/plugins/installed_plugins.json

# ECCバージョン確認
cat ~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/VERSION

# 設定確認
cat ~/.claude/settings.json
```

### 現在の環境設定 (`~/.claude/settings.json`)

```json
{
  "extraKnownMarketplaces": {
    "everything-claude-code": {
      "source": { "source": "github", "repo": "affaan-m/everything-claude-code" }
    }
  },
  "enabledPlugins": {
    "everything-claude-code@everything-claude-code": true
  },
  "effortLevel": "max"
}
```

**インストール情報:**
- バージョン: `1.10.0`
- インストール日: 2026-04-02
- 最終更新: 2026-04-10
- コミット: `194bf605c216547319af6aae1e069e2f79acf3fe`

---

## 5. コンポーネント構成

```
~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/
├── agents/          — 47専門サブエージェント
├── skills/          — 181ワークフロースキル
├── commands/        — 79スラッシュコマンド
├── hooks/           — hooks.json（トリガーベース自動化）
├── rules/           — 常に従うガイドライン（言語別）
├── scripts/         — Node.jsユーティリティ（クロスプラットフォーム）
├── mcp-configs/     — MCPサーバー設定
├── contexts/        — コンテキストモード（dev/research/review）
├── tests/           — テストスイート
├── ecc2/            — ECC 2.0アルファ（Rustコントロールプレーン）
└── .claude/
    ├── commands/    — ECC固有コマンド（database-migration等）
    ├── rules/       — ガードレール
    ├── skills/      — ECCスキル
    ├── research/    — リサーチプレイブック
    ├── team/        — チーム設定
    └── enterprise/  — エンタープライズコントロール
```

---

## 6. ECCコマンド早見表（全79コマンド）

### コアワークフロー

| コマンド | 説明 | 使いどき |
|---------|------|---------|
| `/plan` | 要件確認→リスク評価→実装計画（コード変更前に確認待ち） | 新機能開始時 |
| `/tdd` | TDD強制: インターフェース→失敗テスト→実装→カバレッジ80%+ | バグ修正・新機能 |
| `/code-review` | コード品質・セキュリティ・保守性の全レビュー | コード作成後 |
| `/build-fix` | ビルドエラー検出・修正（言語自動検出） | ビルド失敗時 |
| `/verify` | フル検証ループ: ビルド→lint→テスト→型チェック | コミット前 |
| `/quality-gate` | プロジェクト基準に対する品質ゲートチェック | リリース前 |

### テスト系

| コマンド | 説明 |
|---------|------|
| `/tdd` | 汎用TDDワークフロー（全言語対応） |
| `/e2e` | Playwright E2Eテスト生成・実行・スクショ/動画/トレース取得 |
| `/test-coverage` | テストカバレッジレポート・ギャップ特定 |
| `/go-test` | Go TDD（テーブル駆動、`go test -cover` で80%+） |
| `/kotlin-test` | Kotlin TDD（Kotest + Kover） |
| `/rust-test` | Rust TDD（cargo test、統合テスト） |
| `/cpp-test` | C++ TDD（GoogleTest + gcov/lcov） |
| `/flutter-test` | Flutter/Dart TDD |

### コードレビュー系

| コマンド | 説明 |
|---------|------|
| `/code-review` | 汎用コードレビュー |
| `/python-review` | Python — PEP8、型ヒント、セキュリティ |
| `/go-review` | Go — イディオム、並行安全性、エラーハンドリング |
| `/kotlin-review` | Kotlin — null安全、コルーチン安全性 |
| `/rust-review` | Rust — 所有権、ライフタイム、unsafe使用 |
| `/cpp-review` | C++ — メモリ安全性、モダンイディオム |
| `/flutter-review` | Flutter/Dart — ウィジェット、状態管理 |
| `/review-pr` | PRの総合的なマルチエージェントレビュー |

### ビルドエラー修正系

| コマンド | 説明 |
|---------|------|
| `/build-fix` | 言語自動検出してビルドエラー修正 |
| `/go-build` | Go ビルドエラー・`go vet` 警告修正 |
| `/kotlin-build` | Kotlin/Gradle コンパイラエラー修正 |
| `/rust-build` | Rust ビルド + 借用チェッカー問題修正 |
| `/cpp-build` | C++ CMake・リンカー問題修正 |
| `/gradle-build` | Android/KMP 向け Gradle エラー修正 |
| `/flutter-build` | Dart analyzerエラー・Flutter ビルド失敗を修正 |

### プランニング・アーキテクチャ系

| コマンド | 説明 |
|---------|------|
| `/plan` | 実装計画 + リスク評価 |
| `/multi-plan` | マルチモデル協調プランニング |
| `/multi-workflow` | マルチモデル協調開発 |
| `/multi-backend` | バックエンド特化マルチモデル開発 |
| `/multi-frontend` | フロントエンド特化マルチモデル開発 |
| `/multi-execute` | マルチモデル協調実行 |
| `/orchestrate` | tmux/worktreeマルチエージェントオーケストレーションガイド |
| `/devfleet` | DevFleet経由での並列Claudeエージェントオーケストレーション |

### PRPワークフロー系（Product Requirements Prompts）

| コマンド | 説明 |
|---------|------|
| `/prp-prd` | PRD（製品要件書）を生成 |
| `/prp-plan` | コードベース分析・パターン抽出込みの実装計画を作成 |
| `/prp-implement` | PRPに基づいて実装を実行 |
| `/prp-commit` | PRPワークフローのコミット操作 |
| `/prp-pr` | PRPワークフローのPR操作 |

### セッション管理系

| コマンド | 説明 |
|---------|------|
| `/save-session` | 現在のセッション状態を `~/.claude/session-data/` に保存 |
| `/resume-session` | 最新の保存済みセッションをロードして再開 |
| `/sessions` | セッション履歴の閲覧・検索・管理 |
| `/checkpoint` | 現在のセッションにチェックポイントを設定 |
| `/aside` | 現在のタスクコンテキストを失わずに短い質問に回答 |
| `/context-budget` | コンテキストウィンドウ使用量分析・最適化 |

### 学習・改善系

| コマンド | 説明 |
|---------|------|
| `/learn` | 現在のセッションから再利用可能なパターンを抽出 |
| `/learn-eval` | パターン抽出 + 品質自己評価してから保存 |
| `/evolve` | 学習済み本能を分析、進化したスキル構造を提案 |
| `/promote` | プロジェクトスコープの本能をグローバルスコープに昇格 |
| `/instinct-status` | 全学習済み本能を信頼度スコアと共に表示 |
| `/instinct-export` | 本能をファイルにエクスポート |
| `/instinct-import` | ファイルまたはURLから本能をインポート |
| `/prune` | 30日以上経過した未昇格のペンディング本能を削除 |
| `/skill-create` | ローカルgit履歴を分析 → 再利用可能なスキルを生成 |
| `/skill-health` | スキルポートフォリオのヘルスダッシュボード+分析 |
| `/rules-distill` | スキルをスキャン、横断的原則を抽出してルールに凝縮 |

### Hookify系（フック作成ツール）

| コマンド | 説明 |
|---------|------|
| `/hookify` | 会話でフックを作成（動作の説明を自然言語で） |
| `/hookify-configure` | フックを設定 |
| `/hookify-help` | Hookifyのヘルプを表示 |
| `/hookify-list` | 設定済みフックを一覧表示 |

### リファクタリング・クリーンアップ系

| コマンド | 説明 |
|---------|------|
| `/refactor-clean` | デッドコード除去、重複統合、構造クリーンアップ |
| `/prompt-optimize` | ドラフトプロンプトを分析してECC最適化版を出力 |
| `/agent-sort` | エージェントソートスキル（レガシーシム） |

### ドキュメント・リサーチ系

| コマンド | 説明 |
|---------|------|
| `/docs` | Context7経由で現在のライブラリ/APIドキュメントを検索 |
| `/update-docs` | プロジェクトドキュメントを更新 |
| `/update-codemaps` | コードベースのコードマップを再生成 |
| `/jira` | Jiraチケットの取得・ステータス更新・コメント追加 |

### ループ・自動化系

| コマンド | 説明 |
|---------|------|
| `/loop-start` | 定期的なエージェントループをインターバルで開始 |
| `/loop-status` | 実行中ループのステータス確認 |
| `/claw` | NanoClaw v2 開始 — モデルルーティング・スキルホットロード・分岐・メトリクス |
| `/santa-loop` | サンタループ（特殊な自動化ループ） |

### GANデザイン系

| コマンド | 説明 |
|---------|------|
| `/gan-design` | GAN型2エージェントループで高品質フロントエンドデザインを生成 |
| `/gan-build` | GAN型ビルドハーネス |

### プロジェクト・インフラ系

| コマンド | 説明 |
|---------|------|
| `/projects` | 既知プロジェクト一覧と本能統計 |
| `/harness-audit` | エージェントハーネス設定の信頼性・コスト監査 |
| `/eval` | 評価ハーネスを実行 |
| `/model-route` | タスクを適切なモデル（Haiku/Sonnet/Opus）にルーティング |
| `/pm2` | PM2プロセスマネージャー初期化 |
| `/setup-pm` | パッケージマネージャー設定（npm/pnpm/yarn/bun） |

### ECC固有コマンド（このリポジトリ用）

| コマンド | 説明 |
|---------|------|
| `/feature-dev` | 標準フィーチャー実装ワークフロー |

### クイック判断ガイド

```
新機能を始める?            → /plan 先に、次に /tdd
コードを書いた直後?        → /code-review
ビルドが壊れた?            → /build-fix
ライブドキュメントが必要?  → /docs <ライブラリ名>
セッションが終わりそう?    → /save-session または /learn-eval
翌日再開する?              → /resume-session
コンテキストが重くなった?  → /context-budget 次に /checkpoint
学習を抽出したい?          → /learn-eval 次に /evolve
繰り返しタスクを実行?      → /loop-start
PRをレビューしたい?        → /review-pr [PR番号]
フックを作りたい?          → /hookify <説明>
PRDから実装まで一気に?     → /prp-prd → /prp-plan → /prp-implement
```

---

## 7. エージェント一覧（全47エージェント）

### 汎用・コアエージェント

| エージェント | 目的 | 使うべき状況 |
|------------|------|------------|
| `planner` | 実装計画 | 複雑な機能、リファクタリング |
| `architect` | システム設計・スケーラビリティ | アーキテクチャ決定 |
| `tdd-guide` | テスト駆動開発 | 新機能、バグ修正 |
| `code-reviewer` | コード品質・保守性 | コード作成・変更後 |
| `security-reviewer` | 脆弱性検出 | コミット前、センシティブなコード |
| `build-error-resolver` | ビルド/型エラー修正 | ビルド失敗時 |
| `e2e-runner` | Playwright E2Eテスト | クリティカルなユーザーフロー |
| `refactor-cleaner` | デッドコードクリーンアップ | コードメンテナンス |
| `doc-updater` | ドキュメント・コードマップ | ドキュメント更新 |
| `docs-lookup` | Context7経由のドキュメント検索 | API/ドキュメント質問 |

### 言語特化レビューエージェント

| エージェント | 対象言語 |
|------------|---------|
| `typescript-reviewer` | TypeScript / JavaScript |
| `python-reviewer` | Python |
| `go-reviewer` | Go |
| `kotlin-reviewer` | Kotlin / Android / KMP |
| `java-reviewer` | Java / Spring Boot |
| `rust-reviewer` | Rust |
| `cpp-reviewer` | C / C++ |
| `csharp-reviewer` | C# |
| `flutter-reviewer` | Flutter / Dart |

### 言語特化ビルドエラー修正エージェント

| エージェント | 対象 |
|------------|-----|
| `go-build-resolver` | Go ビルドエラー |
| `kotlin-build-resolver` | Kotlin/Gradle ビルドエラー |
| `java-build-resolver` | Java/Maven/Gradle ビルドエラー |
| `rust-build-resolver` | Rust ビルドエラー |
| `cpp-build-resolver` | C++ CMake/リンカーエラー |
| `pytorch-build-resolver` | PyTorch/CUDA/トレーニングエラー |
| `dart-build-resolver` | Dart/Flutter ビルドエラー |

### 専門エージェント

| エージェント | 目的 | 使うべき状況 |
|------------|------|------------|
| `database-reviewer` | PostgreSQL/Supabase専門家 | スキーマ設計、クエリ最適化 |
| `loop-operator` | 自律ループ実行 | ループの安全実行・停止・監視 |
| `harness-optimizer` | ハーネス設定チューニング | 信頼性・コスト・スループット改善 |
| `performance-optimizer` | パフォーマンス最適化 | ボトルネック分析 |
| `code-architect` | コードアーキテクチャ | 設計パターン決定 |
| `code-explorer` | コードベース探索 | 大規模コードベースの理解 |
| `code-simplifier` | コード簡略化 | リファクタリング |
| `seo-specialist` | SEO最適化 | Web SEO |
| `healthcare-reviewer` | 医療コード審査 | HIPAA、PHI準拠 |

### GANエージェント

| エージェント | 目的 |
|------------|------|
| `gan-planner` | GAN型設計の計画 |
| `gan-generator` | GAN型生成エージェント |
| `gan-evaluator` | GAN型評価エージェント |

### オープンソース管理エージェント

| エージェント | 目的 |
|------------|------|
| `opensource-forker` | OSプロジェクトのフォーク |
| `opensource-packager` | OSプロジェクトのパッケージング |
| `opensource-sanitizer` | OSプロジェクトのクリーニング |

### オーケストレーション用エージェント

| エージェント | 目的 |
|------------|------|
| `chief-of-staff` | マルチエージェント調整 |
| `pr-test-analyzer` | PRテスト分析 |
| `silent-failure-hunter` | サイレント障害検出 |
| `type-design-analyzer` | 型設計分析 |
| `comment-analyzer` | コードコメント分析 |
| `conversation-analyzer` | 会話パターン分析 |

### エージェント使用の基本ルール

```
複雑な機能リクエスト     → planner を使う
コード作成・変更後       → code-reviewer を使う
バグ修正・新機能         → tdd-guide を使う
アーキテクチャ決定       → architect を使う
セキュリティ敏感なコード → security-reviewer を使う
自律ループ/ループ監視    → loop-operator を使う
ハーネス設定の信頼性     → harness-optimizer を使う
```

**並列実行** — 独立した操作には常に複数エージェントを同時起動。

---

## 8. スキル一覧（全181スキル）

スキルはワークフローの主要サーフェス。`~/.claude/skills/` に配置する再利用可能なプロンプトバンドル。スラッシュコマンドのベースとなる実体。

### 言語別ルール・スキル

```
rules/
├── common/          基本ルール（全プロジェクト共通）
├── typescript/      TypeScript特化
├── python/          Python特化
├── golang/          Go特化
├── kotlin/          Kotlin/Android特化
├── java/            Java特化
├── rust/            Rust特化
├── cpp/             C/C++特化
├── csharp/          C#特化
├── dart/            Dart/Flutter特化
├── php/             PHP特化
├── swift/           Swift特化
├── perl/            Perl特化
└── web/             Webフロントエンド特化
```

各ディレクトリには: `coding-style.md`, `hooks.md`, `patterns.md`, `security.md`, `testing.md`

### 主要スキルカテゴリ

**開発ワークフロー:**
`tdd-workflow`, `e2e-testing`, `verification-loop`, `coding-standards`, `git-workflow`,
`continuous-learning`, `continuous-learning-v2`, `search-first`

**フロントエンド:**
`frontend-patterns`, `frontend-slides`, `frontend-design`, `design-system`, `liquid-glass-design`

**バックエンド:**
`backend-patterns`, `api-design`, `database-migrations`, `docker-patterns`, `hexagonal-architecture`

**セキュリティ:**
`security-review`, `security-scan`（AgentShield）, `hipaa-compliance`, `defi-amm-security`,
`llm-trading-agent-security`

**リサーチ・ドキュメント:**
`deep-research`, `documentation-lookup`, `exa-search`, `market-research`

**AI/ML特化:**
`pytorch-patterns`, `eval-harness`, `ai-first-engineering`, `autonomous-loops`,
`autonomous-agent-harness`, `agentic-engineering`, `cost-aware-llm-pipeline`

**コンテンツ・ビジネス:**
`article-writing`, `content-engine`, `investor-materials`, `investor-outreach`,
`brand-voice`, `manim-video`, `remotion-video-creation`

**インフラ・オペレーション:**
`deployment-patterns`, `github-ops`, `google-workspace-ops`, `email-ops`, `terminal-ops`

---

## 9. フック（Hooks）システム

フックはツール呼び出しとライフサイクルイベントに応じたトリガーベースの自動化。

### フックタイプ

| タイプ | タイミング | 主な用途 |
|-------|----------|---------|
| `PreToolUse` | ツール実行前 | バリデーション、リマインダー、ブロック |
| `PostToolUse` | ツール完了後 | フォーマット、フィードバックループ |
| `PostToolUseFailure` | ツール失敗後 | エラー処理、再試行ロジック |
| `PreCompact` | コンテキスト圧縮前 | 状態保存 |
| `SessionStart` | セッション開始時 | コンテキストロード、環境検出 |
| `Stop` | Claudeが応答完了時 | バッチ処理、通知 |
| `SessionEnd` | セッション終了時 | クリーンアップ |

### PreToolUseフック

| フックID | 説明 |
|---------|------|
| `pre:bash:block-no-verify` | `git --no-verify` フラグをブロック（pre-commitフック保護） |
| `pre:bash:auto-tmux-dev` | devサーバーをtmuxで自動起動（ディレクトリベースのセッション名） |
| `pre:bash:tmux-reminder` | 長時間実行コマンドでtmux使用リマインダー |
| `pre:bash:git-push-reminder` | git push前に変更レビューリマインダー |
| `pre:bash:commit-quality` | コミット前品質チェック（lint、メッセージ形式、console.log/secrets検出） |
| `pre:write:doc-file-warning` | 非標準ドキュメントファイル作成の警告 |
| `pre:edit-write:suggest-compact` | 論理的な区切りでの手動圧縮提案 |
| `pre:observe:continuous-learning` | 継続学習のためのツール使用状況キャプチャ（非同期） |
| `pre:governance-capture` | ガバナンスイベントキャプチャ（secrets、ポリシー違反） |
| `pre:config-protection` | lint/formatter設定ファイルへの変更をブロック |
| `pre:mcp-health-check` | MCP実行前にサーバーヘルスチェック |

### PostToolUseフック

| フックID | 説明 |
|---------|------|
| `post:bash:command-log-audit` | 全bashコマンドを `~/.claude/bash-commands.log` に監査ログ |
| `post:bash:command-log-cost` | bashツール使用をタイムスタンプ付きでコストトラッカーに記録 |
| `post:bash:pr-created` | PR作成後にURLをログ、レビューコマンドを提供 |
| `post:bash:build-complete` | ビルド完了後の非同期分析（バックグラウンド実行） |
| `post:quality-gate` | ファイル編集後に品質ゲートチェックを非同期実行 |
| `post:edit:design-quality-check` | フロントエンド編集がテンプレートっぽいUIになっていないか警告 |
| `post:edit:accumulator` | バッチ処理のためにJS/TSファイルパスを蓄積 |
| `post:edit:console-warn` | 編集後のconsole.log文を警告 |
| `post:governance-capture` | ツール出力からガバナンスイベントをキャプチャ |
| `post:session-activity-tracker` | セッションごとのツール呼び出し・ファイル活動を追跡 |
| `post:observe:continuous-learning` | 継続学習のためのツール使用結果キャプチャ（非同期） |

### StopフックとPreCompactフック

| フックID | 説明 |
|---------|------|
| `stop:format-typecheck` | このレスポンスで編集した全JS/TSファイルをバッチフォーマット + 型チェック |
| `stop:check-console-log` | 変更ファイルのconsole.logを確認 |
| `stop:session-end` | 各レスポンス後にセッション状態を永続化 |
| `stop:evaluate-session` | 抽出可能なパターンのセッション評価 |
| `stop:cost-tracker` | セッションごとのトークン・コストメトリクス追跡 |
| `stop:desktop-notify` | Claude応答時にmacOS/WSLデスクトップ通知 |
| `pre:compact` | コンテキスト圧縮前に状態保存 |
| `session:start` | 前回コンテキストロード + パッケージマネージャー自動検出 |

### フック設定の場所

```
~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/hooks/hooks.json
```

### フック環境変数

```bash
ECC_HOOK_PROFILE=minimal    # 最小限のフックのみ
ECC_HOOK_PROFILE=standard   # 標準フック（デフォルト）
ECC_HOOK_PROFILE=strict     # 全フック有効

ECC_DISABLED_HOOKS=pre:bash:tmux-reminder,post:edit:console-warn
ECC_GOVERNANCE_CAPTURE=1    # ガバナンスキャプチャを有効化
```

### 実用フック例（JSON）

```json
{
  "PostToolUse": [
    {
      "matcher": "Edit && (.ts|.tsx|.js|.jsx)$",
      "hooks": [{ "type": "command", "command": "pnpm prettier --write \"$FILE_PATH\"" }]
    },
    {
      "matcher": "Edit && .ts$",
      "hooks": [{ "type": "command", "command": "pnpm tsc --noEmit" }]
    }
  ],
  "Stop": [
    {
      "matcher": "*",
      "hooks": [{ "type": "command", "command": "grep -rn 'console\\.log' --include='*.ts' . || true" }]
    }
  ]
}
```

---

## 10. ルール（Rules）システム

ルールはClaudeが**常に従う**ガイドライン。`~/.claude/rules/` に配置。

### あなたの環境のルール構成

```
~/.claude/rules/
├── coding-style.md          — イミュータビリティ、KISS/DRY/YAGNI
├── security.md              — コミット前必須チェック、シークレット管理
├── testing.md               — TDDワークフロー、80%カバレッジ必須
├── git-workflow.md          — Conventional Commits、PRワークフロー
├── development-workflow.md  — 機能実装の全ステップ
├── agents.md                — サブエージェント委譲ルール
├── patterns.md              — リポジトリパターン、API応答フォーマット
├── performance.md           — モデル選択戦略、コンテキスト管理
├── code-review.md           — コードレビュー基準
├── hooks.md                 — フックシステムのドキュメント
├── typescript/
│   ├── coding-style.md, hooks.md, patterns.md, security.md, testing.md
└── web/
    ├── coding-style.md, design-quality.md, hooks.md
    ├── patterns.md, performance.md, security.md, testing.md
```

---

## 11. コンテキストモード

`contexts/` ディレクトリのモードでClaudeの振る舞いを調整。

### 開発モード (`dev.md`)

```
フォーカス: 実装・コーディング・機能構築
優先順位: 動かす → 正しくする → 綺麗にする
優先ツール: Edit, Write, Bash, Grep, Glob
```

### リサーチモード (`research.md`)

```
フォーカス: 探索・調査・学習
プロセス: 理解 → コード探索 → 仮説 → 検証 → まとめ
優先ツール: Read, Grep, Glob, WebSearch, WebFetch, Explore agent
```

### コードレビューモード (`review.md`)

```
フォーカス: 品質・セキュリティ・保守性
レビュー順: critical → high → medium → low
出力: ファイルごとにグループ化、重大度が先
```

---

## 12. 実践的なワークフロー

### 新機能開発の標準フロー

```
1. リサーチ    → gh search repos/code → 公式ドキュメント → npm/PyPI
2. プランニング → /plan → 要件確認 → リスク評価 → フェーズ分割
3. TDD         → /tdd → テスト先書き(RED) → 実装(GREEN) → リファクタ → 80%+
4. コードレビュー → /code-review → CRITICAL/HIGH修正
5. コミット    → Conventional Commits形式
```

### PRPワークフロー（PRP = Product Requirements Prompt）

PRDから実装まで一気通貫のワークフロー:

```bash
/prp-prd "ユーザー認証機能"   # 1. PRD生成
/prp-plan path/to/prd.md      # 2. コードベース分析+実装計画
/prp-implement                # 3. 計画に基づいて実装
/prp-commit                   # 4. コミット
/prp-pr                       # 5. PR作成
```

### セッション管理フロー

```bash
# セッション開始時
/resume-session    # 前回の続きから

# 作業中
/checkpoint        # 重要なポイントでチェックポイント

# コンテキストが重くなったら
/context-budget    # 使用量確認
/compact           # 手動圧縮（組み込み）

# セッション終了前
/learn-eval        # パターン抽出 + 品質評価
/save-session      # 状態保存
```

### マルチエージェント並列実行

```bash
# 独立したタスクに並列エージェントを使用
# 例: セキュリティ分析 + パフォーマンスレビュー + 型チェック を同時実行
/multi-plan        # マルチモデル協調プランニング
/orchestrate       # tmux/worktreeオーケストレーションガイド
```

### モデル選択ガイド

| モデル | 用途 | コスト効率 |
|-------|------|-----------|
| Haiku 4.5 | 軽量エージェント、頻繁呼び出し、ワーカーエージェント | 3x節約 |
| Sonnet 4.6 | メイン開発作業、複雑なコーディング、オーケストレーション | 標準 |
| Opus 4.6 | 複雑なアーキテクチャ決定、最大の推論が必要な場面 | 高コスト |

---

## 13. 環境変数・設定リファレンス

| 環境変数 | 説明 | 例 |
|---------|------|---|
| `CLAUDE_PLUGIN_ROOT` | ECCプラグインルートパス | `~/.claude/plugins/ecc` |
| `ECC_HOOK_PROFILE` | フックプロファイル | `minimal` / `standard` / `strict` |
| `ECC_DISABLED_HOOKS` | 無効化するフックID（カンマ区切り） | `pre:bash:tmux-reminder` |
| `ECC_GOVERNANCE_CAPTURE` | ガバナンスキャプチャ有効化 | `1` |
| `CLAUDE_PACKAGE_MANAGER` | パッケージマネージャー強制指定 | `npm` / `pnpm` / `yarn` / `bun` |
| `MAX_THINKING_TOKENS` | Extended Thinkingのトークン上限 | `10000` |
| `ANTHROPIC_API_KEY` | APIキー（bareモード等で使用） | `sk-ant-...` |

### インストールプロファイル

| プロファイル | 含まれるパッケージ |
|------------|-----------------|
| `minimal` | runtime-core |
| `standard` | runtime-core + workflow-pack |
| `full` | 全パッケージ（現在の設定） |
| `enterprise` | 全パッケージ（tier=enterprise） |

現在の設定: **full profile / enterprise tier**

---

## 14. MCP設定

### 推奨MCP設定例 (`~/.claude/settings.json` の `mcpServers` に追加)

```json
{
  "mcpServers": {
    "github": { "command": "npx", "args": ["-y", "@modelcontextprotocol/server-github"] },
    "memory": { "command": "npx", "args": ["-y", "@modelcontextprotocol/server-memory"] },
    "sequential-thinking": { "command": "npx", "args": ["-y", "@modelcontextprotocol/server-sequential-thinking"] },
    "firecrawl": { "command": "npx", "args": ["-y", "firecrawl-mcp"] },
    "supabase": { "command": "npx", "args": ["-y", "@supabase/mcp-server-supabase@latest", "--project-ref=YOUR_REF"] },
    "vercel": { "type": "http", "url": "https://mcp.vercel.com" },
    "railway": { "command": "npx", "args": ["-y", "@railway/mcp-server"] }
  }
}
```

### MCPコンテキスト管理のルール

- 20〜30個設定してもOK、**有効化は同時に10個未満**に
- ツール数が80を超えるとパフォーマンスが著しく低下
- 200kトークンのコンテキストウィンドウが過多なMCPで70kまで削られることがある

### MCP操作コマンド

```bash
# Claude Code内
/mcp              # 接続状況確認

# CLI
claude mcp list   # 設定済みMCP一覧
claude mcp add    # MCPサーバーを追加
```

---

## 15. プラグイン管理

```bash
# Claude Code内
/plugins          # プラグインブラウザを開く

# CLI
claude plugin list                                     # インストール済みプラグイン一覧
claude plugin install typescript-lsp@claude-plugins-official
claude plugin install mgrep@Mixedbread-Grep
claude plugin install hookify@claude-plugins-official
```

### 推奨プラグイン

| プラグイン | カテゴリ | 説明 |
|-----------|---------|------|
| `typescript-lsp` | 開発 | TypeScript言語インテリジェンス |
| `pyright-lsp` | 開発 | Python型チェック |
| `hookify` | ワークフロー | 会話形式でフック作成 |
| `mgrep` | 検索 | 高度な検索（ripgrepより優秀） |
| `context7` | 検索 | ライブドキュメント検索 |
| `code-simplifier` | 品質 | リファクタリング |
| `commit-commands` | Git | Gitワークフロー |

### プラグインファイルの場所

```
~/.claude/plugins/
├── cache/                    # ダウンロード済みプラグイン
├── installed_plugins.json    # インストール済みリスト
├── known_marketplaces.json   # 追加済みマーケットプレイス
└── marketplaces/             # マーケットプレイスデータ
```

---

## 16. キーボードショートカット

| ショートカット | 動作 |
|--------------|------|
| `Ctrl+U` | 行全体を削除 |
| `Shift+Enter` | 複数行入力 |
| `Tab` | 思考（thinking）表示のトグル |
| `Esc` (1回) | 実行中の処理を中断 |
| `Esc Esc` (2回) | Claudeを中断 / コードを復元 |
| `Option+T` (macOS) | Extended Thinkingをトグル |
| `Alt+T` (Win/Linux) | Extended Thinkingをトグル |
| `Ctrl+O` | Verboseモード（thinking出力を表示） |
| `Shift+Tab` | Plan Mode / AutoMode のサイクル |
| `!` | クイックbashコマンドプレフィックス |
| `@` | ファイル検索プレフィックス |
| `/` | スラッシュコマンド起動 |

### Permission Mode（Shift+Tab でサイクル）

| モード | 動作 |
|-------|------|
| `default` | 破壊的操作は確認を求める |
| `acceptEdits` | ファイル編集は自動承認 |
| `plan` | 計画のみ立案、実行しない |
| `auto` | 全操作を自動承認 |

---

## 17. トラブルシューティング

### ビルド失敗時

```bash
/build-fix     # 言語自動検出して修正

# 言語を指定
/go-build      # Go
/rust-build    # Rust
/cpp-build     # C++
/flutter-build # Flutter/Dart
/kotlin-build  # Kotlin/Gradle
```

### テスト失敗時

1. `tdd-guide` エージェントを使用
2. テストの独立性を確認
3. モックが正しいか検証
4. 実装を修正（テストが正しい場合）

### コンテキストウィンドウが重い時

```bash
/context-budget    # 使用量分析
/compact           # 手動圧縮（組み込みコマンド）
/checkpoint        # 現状をチェックポイント
# コンテキストウィンドウの最後20%では大規模リファクタを避ける
```

### MCP関連の問題

フック `pre:mcp-health-check` が自動的にMCPサーバーのヘルスを確認。
障害のあるMCP呼び出しをブロックし、`post:mcp-health-check` が失敗した
ツール呼び出しを追跡して再接続を試みる。

### フックが期待通りに動作しない時

```bash
export ECC_HOOK_PROFILE=strict           # 全フック有効
export ECC_DISABLED_HOOKS=pre:bash:tmux-reminder  # 特定フックを無効化
# セッション再起動で変更を反映
```

### ECCルートが見つからない時

ECCプラグインルートの自動解決順序:
1. `CLAUDE_PLUGIN_ROOT` 環境変数
2. `~/.claude/`
3. `~/.claude/plugins/ecc`
4. `~/.claude/plugins/everything-claude-code`
5. `~/.claude/plugins/cache/everything-claude-code/...`（現在の場所）

---

## 18. ECC 2.0 アルファ

`ecc2/` ディレクトリにRust製コントロールプレーンのプロトタイプが存在。
ローカルビルド可能（アルファ版・一般リリース未定）:

```bash
ecc2 dashboard   # ダッシュボード
ecc2 start       # 起動
ecc2 sessions    # セッション一覧
ecc2 status      # ステータス確認
ecc2 stop        # 停止
ecc2 resume      # 再開
ecc2 daemon      # デーモン起動
```

---

*調査環境: Claude Code 2.1.90 / ECC 1.10.0 / macOS Darwin 25.2.0*  
*実ファイルを確認して作成: `~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/`*
