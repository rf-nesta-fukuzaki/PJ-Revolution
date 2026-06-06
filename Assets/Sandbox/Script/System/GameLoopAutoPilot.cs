using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Play モードでの全ループ自動スモークテスト用ドライバ。
///
/// タイトル → インゲーム → リザルト → ショップ → 購入 → 再出発 を実プレイの導線
/// （ボタン onClick・公開 API）で自動的に進め、各マイルストーンを Debug.Log で出力する。
/// 通常プレイでは生成されず、エディタの
/// 「Peak Plunder > Game Loop > Run Loop Smoke Test」からのみ起動する。
/// </summary>
public sealed class GameLoopAutoPilot : MonoBehaviour
{
    private const string Tag = "[AutoPilot]";

    /// <summary>
    /// コンソールログ取得が不安定な環境向けに、ステップ履歴を静的バッファへ保持する。
    /// Play セッション内であれば AutoPilot 破棄後も RunCommand から参照できる。
    /// </summary>
    public static readonly List<string> Trace = new();

    /// <summary>テスト完走の有無（最終結果の判定に使う）。</summary>
    public static bool Completed;

    [Tooltip("ショップ経由のループを何周検証するか。")]
    public int targetLoops = 2;

    [Tooltip("各操作前の待機秒数（演出・初期化の安定待ち）。")]
    public float stepDelay = 2f;

    [Tooltip("状態待ちのタイムアウト秒数。")]
    public float timeout = 30f;

    private const string PurchaseItemId = "shop.short_rope_10m";

    private int _loopsCompleted;

    /// <summary>エディタから一度だけ生成する。</summary>
    public static GameLoopAutoPilot Spawn(int loops = 2)
    {
        Trace.Clear();
        Completed = false;
        var go = new GameObject("GameLoopAutoPilot");
        DontDestroyOnLoad(go);
        var pilot = go.AddComponent<GameLoopAutoPilot>();
        pilot.targetLoops = loops;
        return pilot;
    }

    private void Start() => StartCoroutine(RunLoop());

    private IEnumerator RunLoop()
    {
        Log($"ARMED. targetLoops={targetLoops}  startScene='{ActiveScene}'");

        // 1) タイトル → インゲーム
        if (ActiveScene == GameFlow.TitleScene)
        {
            yield return Wait(stepDelay);
            Log("STEP 1: タイトル → インゲーム（ロビー出発を模擬）");
            GameFlow.GoToInGame();
            yield return WaitForScene(GameFlow.InGameScene);
        }

        // メインループ: インゲーム → リザルト → ショップ → 再出発
        while (_loopsCompleted < targetLoops)
        {
            int loopIndex = _loopsCompleted + 1;

            // 2) インゲーム: 遠征開始を待ち、帰還してリザルトを出す
            yield return EnsureInGameAndReturn(loopIndex);

            // 3) リザルト → ショップ（「ベースに戻る」ボタンを模擬）
            yield return ResultToShop(loopIndex);

            // 4) ショップ: 購入して出発 → インゲーム
            yield return ShopPurchaseAndDepart(loopIndex);

            _loopsCompleted++;
            Log($"=== ループ {_loopsCompleted}/{targetLoops} 完了 ===");
        }

        // 最後にインゲームの遠征を開始確認してからタイトルへ戻る
        yield return WaitForExpeditionClimbing();
        Log("STEP FINAL: 全ループ完了 → タイトルへ戻る");
        yield return Wait(stepDelay);
        GameFlow.GoToTitle();
        yield return WaitForScene(GameFlow.TitleScene);

        Completed = true;
        Log("=== AUTOPILOT DONE: 通しスモークテスト成功 ===");
        Destroy(gameObject);
    }

    // ── インゲーム: 遠征開始 → 帰還 ───────────────────────────
    private IEnumerator EnsureInGameAndReturn(int loopIndex)
    {
        if (ActiveScene != GameFlow.InGameScene)
        {
            Log($"STEP 2-{loopIndex}: インゲームへ遷移");
            GameFlow.GoToInGame();
            yield return WaitForScene(GameFlow.InGameScene);
        }

        yield return WaitForExpeditionClimbing();
        Log($"STEP 2-{loopIndex}: 遠征 Climbing 開始を確認。帰還を実行");
        yield return Wait(stepDelay);

        var expedition = GameServices.Expedition;
        if (expedition == null)
        {
            LogError("GameServices.Expedition が null。帰還できません。");
            yield break;
        }
        expedition.ReturnToBase(true);
        Log($"STEP 2-{loopIndex}: ReturnToBase 実行（リザルト演出開始）");
    }

    // ── リザルト → ショップ ───────────────────────────────────
    private IEnumerator ResultToShop(int loopIndex)
    {
        // リザルト画面が出るのを少し待つ
        yield return Wait(stepDelay * 2f);

        var result = Object.FindFirstObjectByType<ResultScreen>();
        var button = GetPrivateButton(result, "_returnBaseButton");
        if (button != null)
        {
            Log($"STEP 3-{loopIndex}: リザルト「ベースに戻る」ボタンを押下");
            button.onClick.Invoke();
        }
        else
        {
            Log($"STEP 3-{loopIndex}: ボタン未取得のため GameFlow.GoToShop() を直接呼び出し");
            GameFlow.GoToShop();
        }

        yield return WaitForScene(GameFlow.ShopScene);
    }

    // ── ショップ: 購入 → 出発 ─────────────────────────────────
    private IEnumerator ShopPurchaseAndDepart(int loopIndex)
    {
        BasecampShop shop = null;
        float t = 0f;
        while (shop == null && t < timeout)
        {
            shop = Object.FindFirstObjectByType<BasecampShop>();
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (shop == null)
        {
            LogError("BasecampShop が見つかりません。");
            yield break;
        }

        // ショップ UI 初期化（Start）が回り切るのを待つ
        yield return Wait(stepDelay);

        bool bought = shop.TryPurchase(PurchaseItemId);
        Log($"STEP 4-{loopIndex}: 購入 '{PurchaseItemId}' = {bought}");

        yield return Wait(stepDelay);
        Log($"STEP 4-{loopIndex}: ショップ出発（→インゲーム）");
        shop.DepartNow();

        yield return WaitForScene(GameFlow.InGameScene);

        // 持ち越しが適用されたかの目安をログ
        Log($"STEP 4-{loopIndex}: 再出発完了。RunLoadout.HasPending={RunLoadout.HasPending}");
    }

    // ── 待機ヘルパー ─────────────────────────────────────────
    private IEnumerator WaitForExpeditionClimbing()
    {
        float t = 0f;
        while (t < timeout)
        {
            var exp = GameServices.Expedition;
            if (exp != null && exp.Phase == ExpeditionPhase.Climbing) yield break;
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        LogError("遠征が Climbing になりませんでした（タイムアウト）。");
    }

    private IEnumerator WaitForScene(string sceneName)
    {
        float t = 0f;
        while (ActiveScene != sceneName && t < timeout)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        if (ActiveScene != sceneName)
            LogError($"シーン '{sceneName}' へ遷移できませんでした（現在: '{ActiveScene}'）。");
        else
            Log($"  -> シーン到達: '{sceneName}'");
        // シーン Awake/Start 安定待ち
        yield return Wait(0.5f);
    }

    private static IEnumerator Wait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static Button GetPrivateButton(object target, string fieldName)
    {
        if (target == null) return null;
        var field = target.GetType().GetField(
            fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        return field?.GetValue(target) as Button;
    }

    private static string ActiveScene => SceneManager.GetActiveScene().name;

    private static void Log(string msg)
    {
        Trace.Add($"{Time.realtimeSinceStartup:000.0}s {msg}");
        Debug.Log($"{Tag} {msg}");
    }

    private static void LogError(string msg)
    {
        Trace.Add($"{Time.realtimeSinceStartup:000.0}s ERROR: {msg}");
        Debug.LogError($"{Tag} {msg}");
    }
}
