using System;

/// <summary>
/// 遠征フェーズに関するドメインイベントを集約する静的イベントバス。
/// ExpeditionManager（発火元）と ExpeditionHUD・UI 層（購読側）を完全に分離する。
/// </summary>
public static class ExpeditionEvents
{
    // ── 遠征ライフサイクル ────────────────────────────────────
    /// <summary>遠征が開始されたとき（タイマー開始のトリガー）。</summary>
    public static event Action OnExpeditionStarted;

    /// <summary>遠征が終了したとき（タイマー停止のトリガー）。</summary>
    public static event Action OnExpeditionEnded;

    // ── チェックポイント ──────────────────────────────────────
    /// <summary>
    /// チェックポイントに到達したとき。
    /// <param name="current">通過済みチェックポイント番号（1始まり）。</param>
    /// <param name="total">チェックポイントの総数。</param>
    /// </summary>
    public static event Action<int, int> OnCheckpointReached;

    // ── 発火メソッド（ExpeditionManager から呼ぶ）────────────
    public static void RaiseExpeditionStarted()              => OnExpeditionStarted?.Invoke();
    public static void RaiseExpeditionEnded()                => OnExpeditionEnded?.Invoke();
    public static void RaiseCheckpointReached(int cur, int total) => OnCheckpointReached?.Invoke(cur, total);
}
