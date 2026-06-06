using UnityEngine;
using UnityEngine.SceneManagement;
using Sandbox.UI;

/// <summary>
/// タイトル→ロビー→インゲーム→リザルト→ショップ の一連のゲームループ遷移を一元管理する。
///
/// シーン構成（3 シーン）:
///   - <see cref="TitleScene"/>   = StartMenu              … タイトル＋ソロロビー
///   - <see cref="InGameScene"/>  = SandboxOfflineCombined … 遠征本編（地形＋ループ層）
///   - <see cref="ShopScene"/>    = Shop                   … ベースキャンプ準備（次の遠征の買い物）
///
/// 遷移:
///   タイトル → (ロビー) → インゲーム            : <see cref="GoToInGame"/>
///   インゲーム → リザルト → ショップ            : <see cref="GoToShop"/>
///   ショップ → インゲーム（ループ）             : <see cref="GoToInGame"/>
///   インゲーム / ショップ → タイトル（中断・帰宅）: <see cref="GoToTitle"/>
///
/// インゲームシーンに入ったら遠征を即時開始するため、<see cref="ConsumeAutoStart"/> で
/// ExpeditionManager に「ショップ/ロビーから来たので自動出発してよい」ことを伝える。
/// 静的フィールドは <see cref="RuntimeInitializeOnLoadMethod"/> ではなく、明示的に
/// <see cref="ResetRun"/> でクリアする（ドメインリロード無効環境でも安定動作させるため）。
/// </summary>
public static class GameFlow
{
    public const string TitleScene  = "StartMenu";
    public const string InGameScene = "SandboxOfflineCombined";
    public const string ShopScene   = "Shop";

    /// <summary>インゲームシーンで遠征を自動開始してよいか（ショップ/ロビー出発で立つ）。</summary>
    private static bool _autoStartPending;

    /// <summary>現在のループで完了した遠征回数（ショップ表示の文言などに利用可能）。</summary>
    public static int RunCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStaticsOnLaunch()
    {
        _autoStartPending = false;
        RunCount = 0;
    }

    // ── 遷移 API ─────────────────────────────────────────────

    /// <summary>タイトル（StartMenu）へ戻る。ラン状態を完全リセットする。</summary>
    public static void GoToTitle()
    {
        ResetRun();
        Time.timeScale = 1f;
        GameplayCursorPolicy.SetMenuMode();
        Load(TitleScene);
    }

    /// <summary>
    /// インゲーム（本編）を開始/再開する。ロビー出発・ショップ出発・リザルトのリトライから呼ぶ。
    /// 到着後に ExpeditionManager が <see cref="ConsumeAutoStart"/> を見て自動出発する。
    /// </summary>
    public static void GoToInGame()
    {
        _autoStartPending = true;
        Time.timeScale = 1f;
        GameplayCursorPolicy.SetGameplayMode();
        Load(InGameScene);
    }

    /// <summary>ショップ（ベースキャンプ準備）へ。リザルト終了後の導線。</summary>
    public static void GoToShop()
    {
        RunCount++;
        Time.timeScale = 1f;
        GameplayCursorPolicy.SetMenuMode();
        Load(ShopScene);
    }

    // ── 自動出発フラグ ───────────────────────────────────────

    /// <summary>自動出発フラグを 1 度だけ取得して消費する（インゲーム到着時に true なら自動出発）。</summary>
    public static bool ConsumeAutoStart()
    {
        bool v = _autoStartPending;
        _autoStartPending = false;
        return v;
    }

    /// <summary>外部からの参照用（消費しない）。</summary>
    public static bool AutoStartPending => _autoStartPending;

    // ── ラン状態 ─────────────────────────────────────────────

    /// <summary>タイトルへ戻る際などにラン横断状態（買い物の持ち越し等）を初期化する。</summary>
    public static void ResetRun()
    {
        _autoStartPending = false;
        RunCount = 0;
        RunLoadout.Clear();
    }

    // ── 内部: フェード付きロード ─────────────────────────────

    private static void Load(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[GameFlow] sceneName が空です。");
            return;
        }

        // ビルド設定未登録でも落ちないように検証してから遷移する。
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogError(
                $"[GameFlow] シーン '{sceneName}' が Build Settings に登録されていません。" +
                "Peak Plunder > Game Loop > Setup Game Loop Scenes を実行してください。");
            return;
        }

        var fade = GameServices.SceneFade;
        if (fade is IrisTransition iris)
        {
            iris.LoadScene(sceneName);
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
