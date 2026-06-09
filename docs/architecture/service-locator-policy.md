# サービス取得ポリシー — Singleton 凍結 / GameServices 一本化

> 横断ガードレール（2026-06-03 アーキテクチャ監査の対応）。
> このルールは EditMode テスト [`ServiceLocatorPolicyTest`](../../Assets/Tests/EditMode/Sandbox/ServiceLocatorPolicyTest.cs) によって **Test Runner で強制**される。

## ルール（MUST）

1. **新規の横断サービスに `static Instance`（Singleton）を追加しない。**
   横断的に参照されるサービスは [`GameServices`](../../Assets/Sandbox/Script/System/GameServices.cs) 経由で解決する。
2. **新規コードからの依存解決は `GameServices.X` を使う。** 具象クラスの `.Instance` を新たに呼ばない。
3. 既存の `static Instance` は段階的に `GameServices` 経由へ移行する（一括置換は不要・触る時に直す）。

## なぜ（背景）

監査時点（2026-06-03）で `Sandbox.Runtime` には **`static Instance` を持つ型が 33 個**あり、一方で
サービスロケータ [`GameServices`](../../Assets/Sandbox/Script/System/GameServices.cs)（参照 279 箇所）と
生 `.Instance`（104 箇所）が**二重に併存**していた。

二重構造は次の負債を生む:

- 「どちらで取るのが正しいか」が現場でブレる（新規参加者が混乱する）
- テスト・モックが困難（`static Instance` は差し替えできない）
- ライフサイクル/初期化順序が型ごとにバラバラになる

`GameServices` はインターフェース（[`IGameplayServices`](../../Assets/Sandbox/Script/Core/IGameplayServices.cs)）越しに
依存を解決できるため、**プレゼンテーションとドメインの分離**（MVP / DI）に沿う。
これ以上 Singleton を増やさず、アクセス経路を一本化していくのが本ルールの目的。

## 新しい横断サービスの追加手順（HOW-TO）

例: `IFooService` を追加する場合。

1. **インターフェースを定義** — [`Core/IGameplayServices.cs`](../../Assets/Sandbox/Script/Core/IGameplayServices.cs) に
   `public interface IFooService { ... }` を追加（UI 層が触るなら読み取り専用 + `event` を基本に）。
2. **実装する MonoBehaviour** に `IFooService` を実装させる（`static Instance` は **付けない**）。
3. **`GameServices` にスロットを追加** — バッキングフィールド・遅延解決プロパティ・`Register(IFooService)` を
   [`System/GameServices.cs`](../../Assets/Sandbox/Script/System/GameServices.cs) に追加。`Reset()` でクリアも忘れずに。
   ```csharp
   private static IFooService _foo;
   public static IFooService Foo => _foo ??= Object.FindFirstObjectByType<FooManager>();
   public static void Register(IFooService foo) => _foo = foo;
   ```
4. **`SceneServiceInstaller` に登録行を追加** —
   [`Core/SceneServiceInstaller.cs`](../../Assets/Sandbox/Script/Core/SceneServiceInstaller.cs) の
   `RegisterDiscoveredServices()` に 1 行。
   ```csharp
   RegisterIfMissing(Object.FindFirstObjectByType<FooManager>(), s => GameServices.Register((IFooService)s));
   ```
5. **消費側は `GameServices.Foo`** で取得する。

## 既存 `.Instance` の移行

- 触るファイルで `SomeManager.Instance.DoX()` を見かけたら、可能なら `GameServices.Some.DoX()` に置換する
  （対応するスロットが無ければ手順 1〜4 で追加）。
- Netcode の `NetworkBehaviour` 系（`NetworkBootstrap` など）は寿命がネットワークセッションに紐づくため、
  無理に移行しない。**新規追加を増やさないこと**を優先する。

## 強制（ラチェット）

- ベースライン: `Assets/Sandbox/Script` 配下で許容する `static Instance` 宣言の上限。
  **現在の値は [`ServiceLocatorPolicyTest.SingletonBaseline`](../../Assets/Tests/EditMode/Sandbox/ServiceLocatorPolicyTest.cs) = 36**
  （監査時点の 33 から、その後の機能追加で更新されている）。
- [`ServiceLocatorPolicyTest`](../../Assets/Tests/EditMode/Sandbox/ServiceLocatorPolicyTest.cs) が宣言数を数え、
  **ベースラインを超えると失敗**する。
- 既存 Singleton を `GameServices` へ移行して数が減ったら、テストのベースライン定数も下げる（ラチェットを締める）。
- どうしても新規 Singleton が必要な場合のみ、ベースラインを上げ、**理由を PR に明記**する。
- このドキュメントには実数を直書きせず、**正値はテストの定数を正本**とする（数値の二重管理を避けるため）。
