using UnityEngine;

/// <summary>
/// ゲーム全体のサービスへのアクセスを一元化する静的サービスロケーター。
/// 消費側は <c>.Instance</c> ではなく本クラス経由で依存関係を解決する。
///
/// 【横断ガードレール】新規の横断サービスに <c>static Instance</c>（Singleton）を追加せず、
/// ここにスロットを足して取得を一本化する。手順と背景は Assets/Doc/ServiceLocatorPolicy.md。
/// Singleton の増殖は EditMode テスト ServiceLocatorPolicyTest が Test Runner で凍結する。
/// </summary>
public static class GameServices
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Reset()
    {
        _score           = null;
        _save            = null;
        _expedition      = null;
        _hints           = null;
        _weather         = null;
        _helicopter      = null;
        _ropes           = null;
        _spawner         = null;
        _cosmetics       = null;
        _voiceChat       = null;
        _checkpoints     = null;
        _timer           = null;
        _sceneFade       = null;
        _settings        = null;
        _colorBlind      = null;
        _relicDiscovery  = null;
        _audio           = null;
    }

    private static IScoreService              _score;
    private static SaveManager                _save;
    private static IExpeditionService         _expedition;
    private static IHintService               _hints;
    private static IWeatherService            _weather;
    private static IHelicopterService         _helicopter;
    private static RopeManager                _ropes;
    private static SpawnManager               _spawner;
    private static CosmeticManager            _cosmetics;
    private static ProximityVoiceChat         _voiceChat;
    private static ICheckpointProgressService _checkpoints;
    private static IExpeditionTimerService    _timer;
    private static ISceneFadeService          _sceneFade;
    private static ISettingsService           _settings;
    private static IColorBlindPaletteService  _colorBlind;
    private static IRelicDiscoveryNotifier      _relicDiscovery;
    private static IAudioService                _audio;

    public static IScoreService              Score          => _score          ??= Object.FindFirstObjectByType<ScoreTracker>();
    public static SaveManager                Save           => _save           ??= Object.FindFirstObjectByType<SaveManager>();
    public static IExpeditionService         Expedition     => _expedition     ??= Object.FindFirstObjectByType<ExpeditionManager>();
    public static IHintService               Hints          => _hints          ??= Object.FindFirstObjectByType<HintManager>();
    public static IHelicopterService         Helicopter     => _helicopter     ??= Object.FindFirstObjectByType<HelicopterController>();
    public static RopeManager                 Ropes          => _ropes          ??= Object.FindFirstObjectByType<RopeManager>();
    public static SpawnManager                Spawner        => _spawner        ??= Object.FindFirstObjectByType<SpawnManager>();
    public static CosmeticManager            Cosmetics      => _cosmetics      ??= Object.FindFirstObjectByType<CosmeticManager>();
    public static ProximityVoiceChat         VoiceChat      => _voiceChat      ??= Object.FindFirstObjectByType<ProximityVoiceChat>();
    public static ICheckpointProgressService Checkpoints    => _checkpoints    ??= Object.FindFirstObjectByType<Sandbox.World.CheckpointSystem>();
    public static IExpeditionTimerService    Timer          => _timer          ??= Object.FindFirstObjectByType<Sandbox.UI.TimerDisplay>();
    public static ISettingsService           Settings       => _settings       ??= Object.FindFirstObjectByType<SettingsManager>();
    public static IColorBlindPaletteService  ColorBlind     => _colorBlind     ??= Object.FindFirstObjectByType<ColorBlindPaletteService>();
    public static IRelicDiscoveryNotifier      RelicDiscovery => _relicDiscovery ??= Object.FindFirstObjectByType<RelicDiscoveryNotifier>();
    public static IAudioService                Audio          => _audio          ??= Object.FindFirstObjectByType<PeakPlunder.Audio.AudioManager>();

    public static ISceneFadeService SceneFade =>
        _sceneFade ??= Object.FindFirstObjectByType<Sandbox.UI.IrisTransition>() as ISceneFadeService
                    ?? Sandbox.UI.IrisTransition.Instance;

    public static IWeatherService Weather
    {
        get
        {
            if (_weather != null) return _weather;
            _weather = Object.FindFirstObjectByType<WeatherSystem>();
            return _weather;
        }
    }

    public static void Register(IScoreService score) => _score = score;
    public static void Register(SaveManager save) => _save = save;
    public static void Register(IExpeditionService expedition) => _expedition = expedition;
    public static void Register(IHintService hints) => _hints = hints;
    public static void Register(IWeatherService weather) => _weather = weather;
    public static void Register(IHelicopterService helicopter) => _helicopter = helicopter;
    public static void Register(ICheckpointProgressService checkpoints) => _checkpoints = checkpoints;
    public static void Register(IExpeditionTimerService timer) => _timer = timer;
    public static void Register(ISceneFadeService sceneFade) => _sceneFade = sceneFade;
    public static void Register(ISettingsService settings) => _settings = settings;
    public static void Register(IColorBlindPaletteService colorBlind) => _colorBlind = colorBlind;
    public static void Register(IRelicDiscoveryNotifier relicDiscovery) => _relicDiscovery = relicDiscovery;
    public static void Register(IAudioService audio) => _audio = audio;
    public static void Register(RopeManager ropes) => _ropes = ropes;
    public static void Register(SpawnManager spawner) => _spawner = spawner;
    public static void Register(CosmeticManager cosmetics) => _cosmetics = cosmetics;
}
