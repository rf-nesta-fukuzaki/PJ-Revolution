using UnityEngine;

/// <summary>
/// ゲーム全体のサービスへのアクセスを一元化する静的サービスロケーター。
///
/// 既存の Singleton パターン（ScoreTracker.Instance 等）の代わりにこのクラスを使うことで:
///   1. サービスアクセスポイントが一箇所に集約され依存関係が明示化される
///   2. シーンリロード時のキャッシュ無効化を確実に行える
///   3. テスト時にサービスを差し替えやすくなる
///
/// 利用例:
///   GameServices.Score.RecordRopePlacement(id);
///   GameServices.Weather.IsBlizzard;
/// </summary>
public static class GameServices
{
    // ── キャッシュクリア（シーンロード時） ────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Reset()
    {
        _score      = null;
        _save       = null;
        _expedition = null;
        _hints      = null;
        _weather    = null;
        _helicopter = null;
        _ropes      = null;
        _spawner    = null;
        _cosmetics  = null;
        _voiceChat  = null;
    }

    // ── サービス参照（遅延初期化） ────────────────────────────
    private static IScoreService       _score;
    private static SaveManager         _save;
    private static ExpeditionManager   _expedition;
    private static HintManager         _hints;
    private static IWeatherService     _weather;
    private static IHelicopterService  _helicopter;
    private static RopeManager         _ropes;
    private static SpawnManager        _spawner;
    private static CosmeticManager     _cosmetics;
    private static ProximityVoiceChat  _voiceChat;

    public static IScoreService       Score      => _score      ??= Object.FindFirstObjectByType<ScoreTracker>();
    public static SaveManager         Save       => _save       ??= Object.FindFirstObjectByType<SaveManager>();
    public static ExpeditionManager   Expedition => _expedition ??= Object.FindFirstObjectByType<ExpeditionManager>();
    public static HintManager         Hints      => _hints      ??= Object.FindFirstObjectByType<HintManager>();
    public static IHelicopterService  Helicopter => _helicopter ??= Object.FindFirstObjectByType<HelicopterController>();
    public static RopeManager         Ropes      => _ropes      ??= Object.FindFirstObjectByType<RopeManager>();
    public static SpawnManager        Spawner    => _spawner    ??= Object.FindFirstObjectByType<SpawnManager>();
    public static CosmeticManager     Cosmetics  => _cosmetics  ??= Object.FindFirstObjectByType<CosmeticManager>();
    public static ProximityVoiceChat  VoiceChat  => _voiceChat  ??= Object.FindFirstObjectByType<ProximityVoiceChat>();

    /// <summary>
    /// 天候サービス。WeatherSystem が実装する IWeatherService 経由でアクセス。
    /// </summary>
    public static IWeatherService Weather
    {
        get
        {
            if (_weather != null) return _weather;
            _weather = Object.FindFirstObjectByType<WeatherSystem>();
            return _weather;
        }
    }

    // ── テスト用: サービスを差し替える ────────────────────────
    /// <summary>
    /// テストやモック用にサービスを手動で設定する。
    /// ゲームコードからの呼び出しは禁止。
    /// </summary>
    public static void Register(IWeatherService    weather)    => _weather    = weather;
    public static void Register(IScoreService      score)      => _score      = score;
    public static void Register(SaveManager        save)       => _save       = save;
    public static void Register(IHelicopterService helicopter) => _helicopter = helicopter;
}
