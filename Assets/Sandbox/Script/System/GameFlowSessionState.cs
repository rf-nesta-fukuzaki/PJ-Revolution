using UnityEngine;

/// <summary>
/// ゲームループを跨いで保持する「前回遠征のコンテキスト」。
/// インゲーム → リザルト → ショップ で、天気・ルート・スコア等を引き継ぐ。
/// タイトルへ戻る (<see cref="GameFlow.ResetRun"/>) でクリアされる。
/// </summary>
public static class GameFlowSessionState
{
    public static string LastWeatherDisplay { get; private set; } = "☀ 晴れ";
    public static string LastRouteSummary   { get; private set; } = "ルート状況: 調査中...";
    public static int    LastTeamScore      { get; private set; }
    public static float  LastClearTimeSeconds { get; private set; }
    public static bool   LastAllSurvived    { get; private set; } = true;
    public static int    LastRelicIntactCount { get; private set; }
    public static int    LastRelicTotalCount  { get; private set; }

    /// <summary>遠征帰還時に ExpeditionManager から呼ぶ。</summary>
    public static void RecordExpeditionResult(ScoreData score, bool allSurvived)
    {
        if (score != null)
        {
            LastTeamScore         = score.TeamScore;
            LastClearTimeSeconds  = score.ClearTimeSeconds;
            LastRelicTotalCount   = score.Relics?.Count ?? 0;
            LastRelicIntactCount  = 0;
            if (score.Relics != null)
            {
                foreach (var r in score.Relics)
                    if (r != null && r.Condition != RelicCondition.Destroyed)
                        LastRelicIntactCount++;
            }
        }

        LastAllSurvived = allSurvived;

        var weather = GameServices.Weather;
        LastWeatherDisplay = weather == null
            ? "☀ 晴れ"
            : weather.CurrentWeather switch
            {
                WeatherType.Sunny    => "☀ 晴れ",
                WeatherType.Cloudy   => "☁ 曇り",
                WeatherType.Fog      => "🌫 霧",
                WeatherType.Rain     => "🌧 雨",
                WeatherType.Blizzard => "❄ 吹雪",
                _                    => "不明",
            };

        LastRouteSummary = GameServices.Spawner?.GetRouteStatusSummary()
                           ?? "ルート状況: 調査中...";
    }

    /// <summary>ショップヘッダ用の前回遠征サマリー一行。</summary>
    public static string GetLastRunSummaryLine()
    {
        int min = (int)LastClearTimeSeconds / 60;
        float sec = LastClearTimeSeconds % 60f;
        string survive = LastAllSurvived ? "全員生還" : "一部犠牲";
        return $"前回: {min:00}:{sec:00.0} / {LastTeamScore}pt / 遺物 {LastRelicIntactCount}/{LastRelicTotalCount} / {survive}";
    }

    public static void Clear()
    {
        LastWeatherDisplay    = "☀ 晴れ";
        LastRouteSummary      = "ルート状況: 調査中...";
        LastTeamScore         = 0;
        LastClearTimeSeconds  = 0f;
        LastAllSurvived       = true;
        LastRelicIntactCount  = 0;
        LastRelicTotalCount   = 0;
    }
}
