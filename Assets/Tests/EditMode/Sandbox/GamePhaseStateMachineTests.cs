using NUnit.Framework;
using PeakPlunder.Game;
using UnityEngine.TestTools;

public sealed class GamePhaseStateMachineTests
{
    [Test]
    public void TryTransition_FollowsBootToMainMenuPath()
    {
        var sm = new GamePhaseStateMachine();

        Assert.That(sm.TryTransition(GameState.Splash), Is.True);
        Assert.That(sm.TryTransition(GameState.TitleScreen), Is.True);
        Assert.That(sm.TryTransition(GameState.MainMenu), Is.True);
        Assert.That(sm.Current, Is.EqualTo(GameState.MainMenu));
    }

    [Test]
    public void TryTransition_RejectsSkipTransition()
    {
        LogAssert.Expect(UnityEngine.LogType.Error, "[Contract] GamePhaseStateMachine: 不正な遷移 Boot → Expedition");

        var sm = new GamePhaseStateMachine();

        Assert.That(sm.TryTransition(GameState.Expedition), Is.False);
        Assert.That(sm.Current, Is.EqualTo(GameState.Boot));
    }
}
