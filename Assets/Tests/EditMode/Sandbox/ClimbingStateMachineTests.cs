using NUnit.Framework;

public sealed class ClimbingStateMachineTests
{
    [Test]
    public void TryGrab_TransitionsIdleToClimbing()
    {
        var sm = new ClimbingStateMachine();

        Assert.That(sm.TryGrab(), Is.True);
        Assert.That(sm.IsClimbing, Is.True);
    }

    [Test]
    public void TryRelease_ReturnsToIdle()
    {
        var sm = new ClimbingStateMachine();
        sm.TryGrab();

        Assert.That(sm.TryRelease(), Is.True);
        Assert.That(sm.Current, Is.EqualTo(ClimbingState.Idle));
    }

    [Test]
    public void TryGrab_RejectsWhenAlreadyClimbing()
    {
        var sm = new ClimbingStateMachine();
        sm.TryGrab();

        Assert.That(sm.TryGrab(), Is.False);
    }
}
