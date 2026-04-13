using System;

public enum TitleCommandType
{
    None,
    LoadScene,
    ToggleSettings,
    ToggleCredits,
    Quit
}

public enum TitleCommandFailure
{
    None,
    InvalidState,
    StartSceneNameMissing,
    StartSceneNotInBuildSettings
}

public readonly struct TitleCommand
{
    public TitleCommandType Type { get; }
    public string SceneName { get; }
    public TitleCommandFailure Failure { get; }

    private TitleCommand(TitleCommandType type, string sceneName, TitleCommandFailure failure)
    {
        Type = type;
        SceneName = sceneName;
        Failure = failure;
    }

    public static TitleCommand None(TitleCommandFailure failure = TitleCommandFailure.None)
    {
        return new TitleCommand(TitleCommandType.None, string.Empty, failure);
    }

    public static TitleCommand LoadScene(string sceneName)
    {
        return new TitleCommand(TitleCommandType.LoadScene, sceneName, TitleCommandFailure.None);
    }

    public static TitleCommand ToggleSettings()
    {
        return new TitleCommand(TitleCommandType.ToggleSettings, string.Empty, TitleCommandFailure.None);
    }

    public static TitleCommand ToggleCredits()
    {
        return new TitleCommand(TitleCommandType.ToggleCredits, string.Empty, TitleCommandFailure.None);
    }

    public static TitleCommand Quit()
    {
        return new TitleCommand(TitleCommandType.Quit, string.Empty, TitleCommandFailure.None);
    }
}

public interface ITitleMenuInteractor
{
    TitleCommand Handle(TitleMenuAction action);
}

public sealed class TitleMenuInteractor : ITitleMenuInteractor
{
    private readonly TitleSceneStateMachine _stateMachine;
    private readonly ITitleSceneNavigator _sceneNavigator;
    private readonly string _startSceneName;

    public TitleMenuInteractor(
        TitleSceneStateMachine stateMachine,
        ITitleSceneNavigator sceneNavigator,
        string startSceneName)
    {
        _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
        _sceneNavigator = sceneNavigator ?? throw new ArgumentNullException(nameof(sceneNavigator));
        _startSceneName = startSceneName;
    }

    public TitleCommand Handle(TitleMenuAction action)
    {
        if (_stateMachine.Current == TitleSceneState.Transitioning)
        {
            return TitleCommand.None(TitleCommandFailure.InvalidState);
        }

        if (_stateMachine.Current == TitleSceneState.IntroPlaying &&
            !_stateMachine.TryFire(TitleSceneTrigger.IntroComplete))
        {
            return TitleCommand.None(TitleCommandFailure.InvalidState);
        }

        if (!_stateMachine.CanAcceptMenuInput())
        {
            return TitleCommand.None(TitleCommandFailure.InvalidState);
        }

        return action switch
        {
            TitleMenuAction.StartGame when string.IsNullOrWhiteSpace(_startSceneName)
                => TitleCommand.None(TitleCommandFailure.StartSceneNameMissing),
            TitleMenuAction.StartGame when !_sceneNavigator.Exists(_startSceneName)
                => TitleCommand.None(TitleCommandFailure.StartSceneNotInBuildSettings),
            TitleMenuAction.StartGame when _stateMachine.TryFire(TitleSceneTrigger.StartGame)
                => TitleCommand.LoadScene(_startSceneName),
            TitleMenuAction.StartGame
                => TitleCommand.None(TitleCommandFailure.InvalidState),
            TitleMenuAction.Settings
                => TitleCommand.ToggleSettings(),
            TitleMenuAction.Credits
                => TitleCommand.ToggleCredits(),
            TitleMenuAction.Exit
                => TitleCommand.Quit(),
            _
                => TitleCommand.None()
        };
    }
}
