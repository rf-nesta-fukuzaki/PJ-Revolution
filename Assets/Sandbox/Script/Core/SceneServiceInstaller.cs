using UnityEngine;

/// <summary>
/// シーン内 MonoBehaviour サービスを <see cref="GameServices"/> に登録・検証する軽量 DI ブートストラップ。
/// VContainer 等の外部 DI なしで Scene スコープの依存関係を明示化する。
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class SceneServiceInstaller : MonoBehaviour
{
    [SerializeField] private bool _logValidation = true;

    private void Awake()
    {
        RegisterDiscoveredServices();
    }

    private void Start()
    {
        ValidateRequiredServices();
    }

    private static void RegisterDiscoveredServices()
    {
        RegisterIfMissing(Object.FindFirstObjectByType<ScoreTracker>(), s => GameServices.Register((IScoreService)s));
        RegisterIfMissing(Object.FindFirstObjectByType<SaveManager>(), s => GameServices.Register(s));
        RegisterIfMissing(Object.FindFirstObjectByType<ExpeditionManager>(), s => GameServices.Register((IExpeditionService)s));
        RegisterIfMissing(Object.FindFirstObjectByType<HintManager>(), s => GameServices.Register((IHintService)s));
        RegisterIfMissing(Object.FindFirstObjectByType<HelicopterController>(), s => GameServices.Register((IHelicopterService)s));
        RegisterIfMissing(Object.FindFirstObjectByType<RopeManager>(), s => GameServices.Register(s));
        RegisterIfMissing(Object.FindFirstObjectByType<SpawnManager>(), s => GameServices.Register(s));
        RegisterIfMissing(Object.FindFirstObjectByType<CosmeticManager>(), s => GameServices.Register(s));
        RegisterIfMissing(Object.FindFirstObjectByType<WeatherSystem>(), s => GameServices.Register((IWeatherService)s));
        RegisterIfMissing(Object.FindFirstObjectByType<Sandbox.World.CheckpointSystem>(), s => GameServices.Register((ICheckpointProgressService)s));
        RegisterIfMissing(Object.FindFirstObjectByType<Sandbox.UI.TimerDisplay>(), s => GameServices.Register((IExpeditionTimerService)s));
        RegisterIfMissing(Object.FindFirstObjectByType<SettingsManager>(), s => GameServices.Register((ISettingsService)s));
        RegisterIfMissing(Object.FindFirstObjectByType<ColorBlindPaletteService>(), s => GameServices.Register((IColorBlindPaletteService)s));
        RegisterIfMissing(Object.FindFirstObjectByType<RelicDiscoveryNotifier>(), s => GameServices.Register((IRelicDiscoveryNotifier)s));
        RegisterIfMissing(Object.FindFirstObjectByType<PeakPlunder.Audio.AudioManager>(), s => GameServices.Register((IAudioService)s));

        var fade = Object.FindFirstObjectByType<Sandbox.UI.IrisTransition>();
        if (fade != null)
            GameServices.Register((ISceneFadeService)fade);
    }

    private void ValidateRequiredServices()
    {
        bool ok = GameServices.Score != null;

        if (_logValidation)
            Debug.Log(ok
                ? "[SceneServiceInstaller] コアサービス検証 OK"
                : "[SceneServiceInstaller] 警告: IScoreService が未登録です");
    }

    private static void RegisterIfMissing<T>(T service, System.Action<T> register) where T : Object
    {
        if (service == null) return;
        register(service);
    }
}
