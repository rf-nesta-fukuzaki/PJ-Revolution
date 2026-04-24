# Everything Codex マニュアル（ローカル環境）

- 生成日時: **2026-04-11 00:15 JST (+0900)**
- ホスト: **Darwin arm64 (macOS 15 / Kernel 25.2.0)**
- Codex CLI: **codex-cli 0.119.0-alpha.11**
- Codex 実行ファイル: `/Users/R00088/.antigravity/extensions/openai.chatgpt-26.406.31014-darwin-arm64/bin/macos-aarch64/codex`
- ログイン状態: **Logged in using ChatGPT**
- ECC 連携バージョン: **1.10.0**
- ECC プラグインコミット: `194bf605c216547319af6aae1e069e2f79acf3fe`

---

## 1. エグゼクティブサマリー

この環境は **Codex + Everything Claude Code（ECC） が統合済みで実運用可能** であることを検証済みです。

**2026年4月11日（JST）時点**の確認結果:
- `ecc-check-codex` 結果: **PASS**（`checks=24, warnings=0, failures=0`）
- ECC 指示は `~/.codex/AGENTS.md` にマージ済み
- ECC ベースライン設定は `~/.codex/config.toml` にマージ済み
- ECC プロンプトは `~/.codex/prompts` 配下に生成済み（**87 ファイル**）
- ECC ロール設定は `~/.codex/agents` 配下に存在（**3 ファイル**）
- ECC スキルは `~/.agents/skills` 配下で利用可能（**34 スキル**）
- ECC グローバル Git Hooks は `~/.codex/git-hooks` に導入済み

---

## 2. 調査・検証した内容

### 2.1 Codex ランタイム
- `codex --version`
- `codex --help`
- `codex login status`
- `codex features list`
- `codex mcp list`
- `codex mcp --help` および各サブコマンドヘルプ（`add/get/remove/login`）

### 2.2 ECC と Codex の連携
- Claude 側プラグイン有効化状態（`~/.claude/settings.json`）
- ECC インストール記録（`~/.claude/plugins/installed_plugins.json`）
- ECC プラグインキャッシュ構造:
  - `~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/`
- Codex 側同期成果物（`~/.codex/`）
- スキル自動ロード先（`~/.agents/skills`）

### 2.3 運用健全性
- `ecc-check-codex` の完全検証実行
- グローバル hooksPath とフック実行権限
- プロンプト生成マニフェストと生成数
- セッション永続化ファイル（`~/.codex/sessions`, `~/.codex/session_index.jsonl`）

---

## 3. アーキテクチャ: Codex と ECC の接続方式

### 3.1 ソースと反映先
- ECC ソースルート:
  - `~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/`
- Codex ランタイムルート:
  - `~/.codex/`

### 3.2 同期フロー
ECC 同期で以下が配置・更新されます:
1. `~/.codex/AGENTS.md`
   - ECC 全体指示 + Codex 補助指示
2. `~/.codex/config.toml`
   - ベースライン設定、profiles、roles、MCP を add-only でマージ
3. `~/.codex/agents/*.toml`
   - ロール設定（`explorer`, `reviewer`, `docs-researcher`）
4. `~/.codex/prompts/*.md`
   - コマンドプロンプト 79 + 拡張プロンプト 8
5. `~/.codex/git-hooks/`
   - `pre-commit`, `pre-push` グローバルフック
6. `~/.agents/skills/`
   - Codex 自動ロード用 ECC スキル

---

## 4. 検証済み環境スナップショット

### 4.1 Claude 側 ECC 設定
`~/.claude/settings.json` で確認:
- `enabledPlugins.everything-claude-code@everything-claude-code = true`

`~/.claude/plugins/installed_plugins.json` で確認:
- version: `1.10.0`
- install path: `~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0`
- lastUpdated: `2026-04-10T18:00:00.000Z`
- git commit: `194bf605c216547319af6aae1e069e2f79acf3fe`

### 4.2 ECC パッケージ構造（件数検証）
- `agents`: 47
- `skills`: 181
- `commands`: 79
- `hooks`: 2
- `rules`: 16
- `contexts`: 3
- `tests`: 9
- `mcp-configs`: 1
- `.codex`: 3
- `.agents`: 2

### 4.3 Codex 側ランタイム状態
- `~/.codex/AGENTS.md`: 非空（13,231 bytes）
- `~/.codex/prompts/ecc-prompts-manifest.txt`: 79 エントリ
- `~/.codex/prompts/ecc-extension-prompts-manifest.txt`: 8 エントリ
- `~/.codex/prompts/ecc-*.md`: 87 ファイル
- `~/.codex/agents/*.toml`: 3 ファイル
- `~/.agents/skills/*`: 34 ディレクトリ
- `~/.codex/sessions/rollout-*.jsonl`: 52 ファイル

---

## 5. Codex 基本利用ガイド

### 5.1 主要コマンド

| コマンド | 目的 |
|---|---|
| `codex` | 対話型 TUI セッション |
| `codex exec` | 非対話実行 |
| `codex review` | 非対話レビュー |
| `codex resume` | 過去セッション再開 |
| `codex fork` | 過去セッション分岐 |
| `codex mcp` | MCP サーバー管理 |
| `codex features` | Feature Flag 管理 |
| `codex login` | ログイン/認証管理 |
| `codex app` | Codex アプリ起動 |

### 5.2 重要オプション

| オプション | 意味 |
|---|---|
| `-p, --profile <name>` | `~/.codex/config.toml` の profile を使用 |
| `-s, --sandbox <mode>` | `read-only`, `workspace-write`, `danger-full-access` |
| `-a, --ask-for-approval <policy>` | 承認ポリシー（`on-request`, `never` など） |
| `-C, --cd <dir>` | 作業ディレクトリを指定 |
| `--search` | ライブ Web 検索ツールを有効化 |
| `-c key=value` | 一時的な設定上書き |

### 5.3 実用例

```bash
# プロジェクトディレクトリで Codex を開始
codex -C /path/to/repo

# 標準入力プロンプトで非対話実行
codex exec -C /path/to/repo - <<'PROMPT'
Review this repository for security risks first.
PROMPT

# 最新セッションを再開
codex resume --last

# 最新セッションをフォーク
codex fork --last

# strict プロファイルで実行（read-only）
codex -p strict

# Web検索を有効化して実行
codex --search -C /path/to/repo
```

---

## 6. Codex 上の ECC ランタイム設定

`~/.codex/config.toml` より（検証済み）:
- `model = "gpt-5.3-codex"`
- `model_reasoning_effort = "xhigh"`
- `approval_policy = "on-request"`
- `sandbox_mode = "workspace-write"`
- `web_search = "live"`
- `persistent_instructions` 設定あり
- `features.multi_agent = true`

Profiles:
- `strict`: read-only sandbox + cached web search
- `yolo`: auto-approval（`never`）+ workspace-write + live search

エージェントロール対応:
- `[agents.explorer] -> agents/explorer.toml`
- `[agents.reviewer] -> agents/reviewer.toml`
- `[agents.docs_researcher] -> agents/docs-researcher.toml`

---

## 7. この環境の MCP

### 7.1 有効サーバー（`codex mcp list`）
- `context7`
- `github`
- `memory`
- `playwright`
- `sequential-thinking`
- `supabase`
- `exa`（HTTP）
- `unity_mcp`（既存ローカル設定）

### 7.2 MCP コマンド早見表

```bash
codex mcp list
codex mcp get <name>
codex mcp add <name> -- <command> [args...]
codex mcp add <name> --url <https://...>
codex mcp login <name>
codex mcp remove <name>
```

---

## 8. ECC ヘルパーコマンド（PATH 導入済み）

シェル初期化後（`source ~/.zshrc`）に以下ラッパーが使えます:

| コマンド | 目的 |
|---|---|
| `ecc-sync-codex` | ECC 同期を `~/.codex` に再実行 |
| `ecc-install-git-hooks` | ECC グローバル Git Hooks を導入/更新 |
| `ecc-check-codex` | ECC 全体の健全性チェック |

ラッパー実体:
- `~/.codex/bin/ecc-sync-codex`
- `~/.codex/bin/ecc-install-git-hooks`
- `~/.codex/bin/ecc-check-codex`

`~/.zshrc` には `~/.codex/bin` の PATH ブートストラップ設定が含まれます。

---

## 9. Codex で利用可能な ECC スキル（34）

現在 `~/.agents/skills` に存在するスキル:

- `agent-introspection-debugging`
- `agent-sort`
- `api-design`
- `article-writing`
- `backend-patterns`
- `brand-voice`
- `bun-runtime`
- `claude-api`
- `coding-standards`
- `content-engine`
- `crosspost`
- `deep-research`
- `dmux-workflows`
- `documentation-lookup`
- `e2e-testing`
- `eval-harness`
- `everything-claude-code`
- `exa-search`
- `fal-ai-media`
- `frontend-design`
- `frontend-patterns`
- `frontend-slides`
- `investor-materials`
- `investor-outreach`
- `market-research`
- `mcp-server-patterns`
- `nextjs-turbopack`
- `product-capability`
- `security-review`
- `strategic-compact`
- `tdd-workflow`
- `verification-loop`
- `video-editing`
- `x-api`

### 9.1 スキル利用パターン
Codex チャットで明示的に指示します。例:
- 「この実装は `tdd-workflow` を使って進めてください。」
- 「最終出力前に `security-review` を実行してください。」
- 「`verification-loop` を使い、失敗を先に報告してください。」

---

## 10. Codex における ECC プロンプト運用

ECC コマンドプロンプトは `~/.codex/prompts` に生成されます。

- コマンドプロンプト: 79（`ecc-prompts-manifest.txt`）
- 拡張プロンプト: 8（`ecc-extension-prompts-manifest.txt`）

### 10.1 プロンプト実行方法

対話型:
1. `codex -C /path/to/repo`
2. `~/.codex/prompts/ecc-*.md` の内容を貼り付け

非対話型:

```bash
codex exec -C /path/to/repo - < ~/.codex/prompts/ecc-plan.md
```

---

## 11. ECC コマンド早見表（79）

| コマンド | プロンプトファイル | 説明 |
|---|---|---|
| `/agent-sort` | `ecc-agent-sort.md` | `agent-sort` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/aside` | `ecc-aside.md` | 現在のタスク文脈を失わずに短い寄り道質問へ回答し、回答後に自動で作業へ復帰。 |
| `/build-fix` | `ecc-build-fix.md` | （front-matter に説明が定義されていません） |
| `/checkpoint` | `ecc-checkpoint.md` | （front-matter に説明が定義されていません） |
| `/claw` | `ecc-claw.md` | `nanoclaw-repl` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/code-review` | `ecc-code-review.md` | コードレビュー（ローカル未コミット差分 or GitHub PR。PR番号/URL 指定で PR モード）。 |
| `/context-budget` | `ecc-context-budget.md` | `context-budget` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/cpp-build` | `ecc-cpp-build.md` | C++ ビルドエラー、CMake 問題、リンカ問題を段階的に修正。最小・局所修正のため `cpp-build-resolver` エージェントを呼び出し。 |
| `/cpp-review` | `ecc-cpp-review.md` | メモリ安全性、モダンC++、並行性、安全性を対象にした包括的 C++ コードレビュー。`cpp-reviewer` エージェントを呼び出し。 |
| `/cpp-test` | `ecc-cpp-test.md` | C++ 向け TDD を強制。GoogleTest を先に作成し、その後実装。`gcov/lcov` でカバレッジ確認。 |
| `/devfleet` | `ecc-devfleet.md` | `claude-devfleet` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/docs` | `ecc-docs.md` | `documentation-lookup` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/e2e` | `ecc-e2e.md` | `e2e-testing` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/eval` | `ecc-eval.md` | `eval-harness` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/evolve` | `ecc-evolve.md` | 直感（instinct）を分析し、発展した構造を提案または生成。 |
| `/feature-dev` | `ecc-feature-dev.md` | コードベース理解とアーキテクチャ重視で機能開発をガイド。 |
| `/flutter-build` | `ecc-flutter-build.md` | Dart アナライザーエラーと Flutter ビルド失敗を段階的に修正。最小・局所修正のため `dart-build-resolver` を呼び出し。 |
| `/flutter-review` | `ecc-flutter-review.md` | Flutter/Dart の慣用パターン、Widget ベストプラクティス、状態管理、性能、アクセシビリティ、安全性をレビュー。`flutter-reviewer` を呼び出し。 |
| `/flutter-test` | `ecc-flutter-test.md` | Flutter/Dart テストを実行し、失敗を報告・段階修正。unit/widget/golden/integration を対象。 |
| `/gan-build` | `ecc-gan-build.md` | （front-matter に説明が定義されていません） |
| `/gan-design` | `ecc-gan-design.md` | （front-matter に説明が定義されていません） |
| `/go-build` | `ecc-go-build.md` | Go ビルドエラー、`go vet` 警告、linter 問題を段階的に修正。`go-build-resolver` を呼び出し。 |
| `/go-review` | `ecc-go-review.md` | 慣用パターン、並行安全性、エラーハンドリング、安全性を対象にした包括的 Go コードレビュー。`go-reviewer` を呼び出し。 |
| `/go-test` | `ecc-go-test.md` | Go 向け TDD を強制。テーブル駆動テストを先に書き、その後実装。`go test -cover` で 80%+ を確認。 |
| `/gradle-build` | `ecc-gradle-build.md` | Android / KMP プロジェクト向け Gradle ビルドエラーを修正。 |
| `/harness-audit` | `ecc-harness-audit.md` | （front-matter に説明が定義されていません） |
| `/hookify-configure` | `ecc-hookify-configure.md` | hookify ルールを対話的に有効化/無効化。 |
| `/hookify-help` | `ecc-hookify-help.md` | hookify システムのヘルプ表示。 |
| `/hookify-list` | `ecc-hookify-list.md` | 設定済み hookify ルール一覧を表示。 |
| `/hookify` | `ecc-hookify.md` | 会話分析や明示指示に基づき、望ましくない挙動を防ぐフックを作成。 |
| `/instinct-export` | `ecc-instinct-export.md` | project/global スコープの instinct をファイルへエクスポート。 |
| `/instinct-import` | `ecc-instinct-import.md` | ファイルまたは URL から instinct を project/global スコープへインポート。 |
| `/instinct-status` | `ecc-instinct-status.md` | 学習済み instinct（project + global）を信頼度付きで表示。 |
| `/jira` | `ecc-jira.md` | Jira チケット取得、要件分析、ステータス更新、コメント追加。`jira-integration` スキルと MCP/REST API を使用。 |
| `/kotlin-build` | `ecc-kotlin-build.md` | Kotlin/Gradle ビルドエラー、コンパイラ警告、依存問題を段階的に修正。`kotlin-build-resolver` を呼び出し。 |
| `/kotlin-review` | `ecc-kotlin-review.md` | 慣用パターン、null 安全性、コルーチン安全性、安全性を対象にした包括的 Kotlin コードレビュー。`kotlin-reviewer` を呼び出し。 |
| `/kotlin-test` | `ecc-kotlin-test.md` | Kotlin 向け TDD を強制。Kotest を先に書き、その後実装。Kover で 80%+ を確認。 |
| `/learn-eval` | `ecc-learn-eval.md` | セッションから再利用可能パターンを抽出し、保存前に自己評価し、保存先（Global/Project）を判定。 |
| `/learn` | `ecc-learn.md` | （front-matter に説明が定義されていません） |
| `/loop-start` | `ecc-loop-start.md` | （front-matter に説明が定義されていません） |
| `/loop-status` | `ecc-loop-status.md` | （front-matter に説明が定義されていません） |
| `/model-route` | `ecc-model-route.md` | （front-matter に説明が定義されていません） |
| `/multi-backend` | `ecc-multi-backend.md` | （front-matter に説明が定義されていません） |
| `/multi-execute` | `ecc-multi-execute.md` | （front-matter に説明が定義されていません） |
| `/multi-frontend` | `ecc-multi-frontend.md` | （front-matter に説明が定義されていません） |
| `/multi-plan` | `ecc-multi-plan.md` | （front-matter に説明が定義されていません） |
| `/multi-workflow` | `ecc-multi-workflow.md` | （front-matter に説明が定義されていません） |
| `/orchestrate` | `ecc-orchestrate.md` | `dmux-workflows` と `autonomous-agent-harness` 用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/plan` | `ecc-plan.md` | 要件の再整理、リスク評価、実装手順計画を作成。**コード変更前にユーザー確認を待機**。 |
| `/pm2` | `ecc-pm2.md` | （front-matter に説明が定義されていません） |
| `/projects` | `ecc-projects.md` | 既知プロジェクト一覧と instinct 統計を表示。 |
| `/promote` | `ecc-promote.md` | project スコープの instinct を global へ昇格。 |
| `/prompt-optimize` | `ecc-prompt-optimize.md` | `prompt-optimizer` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/prp-commit` | `ecc-prp-commit.md` | 自然言語で対象ファイルを指定して素早くコミット（平文で「何をコミットするか」を記述）。 |
| `/prp-implement` | `ecc-prp-implement.md` | 厳密な検証ループ付きで実装計画を実行。 |
| `/prp-plan` | `ecc-prp-plan.md` | コードベース分析とパターン抽出に基づく包括的機能実装計画を作成。 |
| `/prp-pr` | `ecc-prp-pr.md` | 現在ブランチの未 push コミットから GitHub PR を作成（テンプレート探索・差分分析・push を実行）。 |
| `/prp-prd` | `ecc-prp-prd.md` | 対話型 PRD 生成（課題起点・仮説駆動・往復質問あり）。 |
| `/prune` | `ecc-prune.md` | 30日超で昇格されていない pending instinct を削除。 |
| `/python-review` | `ecc-python-review.md` | PEP 8、型ヒント、安全性、Pythonic 慣用を対象にした包括的 Python コードレビュー。`python-reviewer` を呼び出し。 |
| `/quality-gate` | `ecc-quality-gate.md` | （front-matter に説明が定義されていません） |
| `/refactor-clean` | `ecc-refactor-clean.md` | （front-matter に説明が定義されていません） |
| `/resume-session` | `ecc-resume-session.md` | `~/.claude/session-data/` の最新セッションファイルを読み込み、前回終了時点の文脈で再開。 |
| `/review-pr` | `ecc-review-pr.md` | 専門エージェントを用いた包括的 PR レビュー。 |
| `/rules-distill` | `ecc-rules-distill.md` | `rules-distill` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/rust-build` | `ecc-rust-build.md` | Rust ビルドエラー、borrow checker 問題、依存問題を段階的に修正。`rust-build-resolver` を呼び出し。 |
| `/rust-review` | `ecc-rust-review.md` | 所有権、ライフタイム、エラーハンドリング、unsafe 使用、慣用パターンを対象にした包括的 Rust コードレビュー。`rust-reviewer` を呼び出し。 |
| `/rust-test` | `ecc-rust-test.md` | Rust 向け TDD を強制。テスト先行で実装し、`cargo-llvm-cov` で 80%+ を確認。 |
| `/santa-loop` | `ecc-santa-loop.md` | 敵対的デュアルレビュー収束ループ。独立した2モデルレビュアーの両承認まで出荷不可。 |
| `/save-session` | `ecc-save-session.md` | 現在セッション状態を `~/.claude/session-data/` の日付付きファイルへ保存し、将来セッションで完全文脈再開可能にする。 |
| `/sessions` | `ecc-sessions.md` | Claude Code のセッション履歴、エイリアス、メタデータを管理。 |
| `/setup-pm` | `ecc-setup-pm.md` | 使用するパッケージマネージャ（npm/pnpm/yarn/bun）を設定。 |
| `/skill-create` | `ecc-skill-create.md` | ローカル Git 履歴を分析してコーディングパターンを抽出し、`SKILL.md` を生成。Skill Creator GitHub App のローカル版。 |
| `/skill-health` | `ecc-skill-health.md` | スキルポートフォリオ健全性ダッシュボード（チャート/分析）を表示。 |
| `/tdd` | `ecc-tdd.md` | `tdd-workflow` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |
| `/test-coverage` | `ecc-test-coverage.md` | （front-matter に説明が定義されていません） |
| `/update-codemaps` | `ecc-update-codemaps.md` | （front-matter に説明が定義されていません） |
| `/update-docs` | `ecc-update-docs.md` | （front-matter に説明が定義されていません） |
| `/verify` | `ecc-verify.md` | `verification-loop` スキル用の旧スラッシュ導線です。スキル直接利用を推奨。 |

---

## 12. ECC 拡張プロンプト早見表（8）

| プロンプトファイル | 目的 |
|---|---|
| `ecc-rules-pack-common.md` | このセッションに ECC 共通エンジニアリングルールを適用。指定ファイル群を正とする。 |
| `ecc-rules-pack-golang.md` | ECC 共通ルール + Go 専用ルールをこのセッションに適用。 |
| `ecc-rules-pack-python.md` | ECC 共通ルール + Python 専用ルールをこのセッションに適用。 |
| `ecc-rules-pack-swift.md` | ECC 共通ルール + Swift 専用ルールをこのセッションに適用。 |
| `ecc-rules-pack-typescript.md` | ECC 共通ルール + TypeScript 専用ルールをこのセッションに適用。 |
| `ecc-tool-check-coverage.md` | カバレッジを解析し、80%（または指定しきい値）と比較。 |
| `ecc-tool-run-tests.md` | パッケージマネージャ自動判定でリポジトリのテスト一式を実行し、簡潔に報告。 |
| `ecc-tool-security-audit.md` | 実践的セキュリティ監査を実行（依存脆弱性 + 秘密情報スキャン + 高リスクコードパターン）。 |

---

## 13. グローバル Git Hooks（ECC）

グローバル hook path:
- `git config --global --get core.hooksPath`
- 現在値: `~/.codex/git-hooks`

導入済みフック:
- `pre-commit`
  - ステージ済み追加差分に対する高確度シークレットパターンをブロック
- `pre-push`
  - 軽量検証（プロジェクト種別に応じて scripts/tests/audit）を実行

意図的にバイパスする方法（必要時のみ）:
- `ECC_SKIP_PRECOMMIT=1`
- `ECC_SKIP_PREPUSH=1`
- `ECC_SKIP_GIT_HOOKS=1`
- リポジトリルートに `.ecc-hooks-disable` ファイルを置く

---

## 14. 運用ランブック

### 14.1 日次ヘルスチェック

```bash
source ~/.zshrc
codex --version
codex login status
codex mcp list
ecc-check-codex
```

### 14.2 ECC 更新後

```bash
ecc-sync-codex --dry-run
ecc-sync-codex
ecc-check-codex
```

### 14.3 ECC MCP ブロックを強制更新

```bash
ecc-sync-codex --update-mcp
codex mcp list
```

### 14.4 セッション継続

```bash
codex resume --last
codex fork --last
```

---

## 15. この環境での既知メモ

1. `codex` 起動時に現在この警告が出ます:
   - `WARNING: proceeding, even though we could not update PATH: Operation not permitted (os error 1)`
   - この環境では **非致命** です。

2. `codex mcp list` の現行表示では `Auth: Unsupported` と出ます。
   - 一覧表示・有効化自体は動作します。
   - OAuth 認証が必要な場合は `codex mcp login <name>` を使用してください。

3. 同期バックアップが存在します:
   - `~/.codex/backups/ecc-20260411-000150/`

---

## 16. 重要ファイルマップ

### Codex ランタイム
- `~/.codex/config.toml`
- `~/.codex/AGENTS.md`
- `~/.codex/agents/*.toml`
- `~/.codex/prompts/*.md`
- `~/.codex/git-hooks/pre-commit`
- `~/.codex/git-hooks/pre-push`
- `~/.codex/sessions/**/rollout-*.jsonl`
- `~/.codex/session_index.jsonl`

### ECC ソース
- `~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/.codex/`
- `~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/.agents/skills/`
- `~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/scripts/sync-ecc-to-codex.sh`
- `~/.claude/plugins/cache/everything-claude-code/everything-claude-code/1.10.0/scripts/codex/check-codex-global-state.sh`

### スキル自動ロード先
- `~/.agents/skills/`

---

## 17. 再検証チェックリスト（そのまま実行可）

```bash
source ~/.zshrc
codex --version
codex login status
codex features list | rg 'multi_agent|plugins|shell_tool|unified_exec'
codex mcp list
ecc-check-codex
```

期待値:
- Codex コマンドが実行できる
- ログインが有効
- 主要ランタイム機能が有効
- MCP サーバー一覧が表示される
- ECC 健全性チェックが PASS

---

このマニュアルは **2026年4月11日（JST）時点**で、このマシンを対象に **実機でのローカル調査・検証結果** をもとに作成しています。
