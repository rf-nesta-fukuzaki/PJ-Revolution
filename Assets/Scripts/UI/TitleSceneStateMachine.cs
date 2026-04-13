public enum TitleSceneState
{
    Boot,
    IntroPlaying,
    Ready,
    ModalOpen,
    Transitioning
}

public enum TitleSceneTrigger
{
    IntroStart,
    IntroComplete,
    SetReady,
    OpenModal,
    CloseModal,
    StartGame
}

public sealed class TitleSceneStateMachine
{
    public TitleSceneState Current { get; private set; } = TitleSceneState.Boot;

    public bool TryFire(TitleSceneTrigger trigger)
    {
        switch (Current)
        {
            case TitleSceneState.Boot:
                if (trigger == TitleSceneTrigger.IntroStart)
                {
                    Current = TitleSceneState.IntroPlaying;
                    return true;
                }

                if (trigger == TitleSceneTrigger.SetReady)
                {
                    Current = TitleSceneState.Ready;
                    return true;
                }
                break;
            case TitleSceneState.IntroPlaying:
                if (trigger == TitleSceneTrigger.IntroComplete || trigger == TitleSceneTrigger.SetReady)
                {
                    Current = TitleSceneState.Ready;
                    return true;
                }
                break;
            case TitleSceneState.Ready:
                if (trigger == TitleSceneTrigger.OpenModal)
                {
                    Current = TitleSceneState.ModalOpen;
                    return true;
                }

                if (trigger == TitleSceneTrigger.StartGame)
                {
                    Current = TitleSceneState.Transitioning;
                    return true;
                }
                break;
            case TitleSceneState.ModalOpen:
                if (trigger == TitleSceneTrigger.CloseModal || trigger == TitleSceneTrigger.SetReady)
                {
                    Current = TitleSceneState.Ready;
                    return true;
                }

                if (trigger == TitleSceneTrigger.StartGame)
                {
                    Current = TitleSceneState.Transitioning;
                    return true;
                }
                break;
            case TitleSceneState.Transitioning:
                break;
        }

        return false;
    }

    public bool CanAcceptMenuInput()
    {
        return Current == TitleSceneState.Ready || Current == TitleSceneState.ModalOpen;
    }

    public void EnterIntro()
    {
        TryFire(TitleSceneTrigger.IntroStart);
    }

    public void MarkReady()
    {
        TryFire(TitleSceneTrigger.SetReady);
    }

    public void OpenModal()
    {
        TryFire(TitleSceneTrigger.OpenModal);
    }

    public void CloseModal()
    {
        TryFire(TitleSceneTrigger.CloseModal);
    }

    public void BeginTransition()
    {
        TryFire(TitleSceneTrigger.StartGame);
    }
}
