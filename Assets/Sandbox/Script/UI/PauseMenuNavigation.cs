namespace Sandbox.UI
{
    /// <summary>ポーズメニューが表示しているページ。</summary>
    public enum PausePage
    {
        None,         // 非表示（ゲームプレイ中）
        Menu,         // ルートメニュー（戻る／設定／離脱）
        Settings,     // 設定パネル
        ConfirmLeave, // 離脱確認ダイアログ
    }

    /// <summary>Cancel/Esc 押下時に取るべきアクション。</summary>
    public enum PauseNavAction
    {
        None,     // 何もしない
        Resume,   // ポーズを解除してゲームへ戻る
        GoToMenu, // ルートメニューへ戻る
    }

    /// <summary>
    /// ポーズメニューのページ遷移を司る純粋ロジック。
    /// MonoBehaviour から切り離してユニットテスト可能にしている。
    /// Esc / Cancel は「常に 1 段だけ戻る」挙動を保証する（GDD §17.3）。
    /// </summary>
    public static class PauseMenuNavigation
    {
        /// <summary>Esc / Cancel を押したときに取るべきアクションを返す。</summary>
        public static PauseNavAction OnCancel(PausePage page)
        {
            switch (page)
            {
                case PausePage.Menu:
                    return PauseNavAction.Resume;
                case PausePage.Settings:
                case PausePage.ConfirmLeave:
                    return PauseNavAction.GoToMenu;
                default:
                    return PauseNavAction.None;
            }
        }

        /// <summary>そのページでゲームを開いた状態として扱うか（＝ポーズ中か）。</summary>
        public static bool IsOpen(PausePage page) => page != PausePage.None;
    }
}
