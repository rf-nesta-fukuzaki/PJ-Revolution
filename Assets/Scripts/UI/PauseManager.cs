using UnityEngine;

/// <summary>
/// ESC キーでオプション画面をトグルするポーズ管理コンポーネント。
///
/// [責務]
///   - ゲームプレイ中に ESC でオプション画面を開閉する
///   - オプション表示中はカーソルを表示・ロック解除する
///   - オプション非表示時はカーソルを非表示・ロックする
///   - オプション表示中は PlayerInputController の入力を無効化するフラグを立てる
///
/// [注意]
///   - FirstPersonLook の HandleCursorLock() が ESC を検知してカーソルを解放するため、
///     PauseManager は Playing 状態のときのみ ESC を処理する。
///   - Time.timeScale は変更しない（サバイバルゲームの特性上、ポーズ中も時間を止めない）。
///   - UIFlowController が Playing 状態のときのみ動作する。
/// </summary>
public class PauseManager : MonoBehaviour
{
    // ─────────────── 静的プロパティ ───────────────

    /// <summary>
    /// オプション画面が表示中かどうか。
    /// PlayerInputController から参照して入力をブロックするために使う。
    /// </summary>
    public static bool IsPaused { get; private set; }

    // ─────────────── Unity Lifecycle ───────────────

    private void Update()
    {
        // Playing 状態（HUD が表示中）のときのみポーズトグルを受け付ける
        if (UIFlowController.Instance == null) return;
        if (UIFlowController.Instance.CurrentScreen != UIScreen.Playing) return;

        if (Input.GetKeyDown(KeyCode.Escape))
            TogglePause();
    }

    private void OnDisable()
    {
        // このコンポーネントが無効化されたときはポーズを解除する
        if (IsPaused)
            SetPaused(false);
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// 外部からポーズ状態を指定して変更する。
    /// </summary>
    public void SetPaused(bool paused)
    {
        IsPaused = paused;

        UIFlowController.Instance?.SetOptionsVisible(paused);

        if (paused)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }
    }

    // ─────────────── 内部処理 ───────────────

    private void TogglePause()
    {
        SetPaused(!IsPaused);
    }
}
