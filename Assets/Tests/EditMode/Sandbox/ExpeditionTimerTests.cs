using NUnit.Framework;

public sealed class ExpeditionTimerTests
{
    [Test]
    public void Tick_WhenRunning_IncreasesElapsedAndFormatsTime()
    {
        var timer = new ExpeditionTimer();
        string lastFormatted = null;
        timer.OnFormattedTimeChanged += formatted => lastFormatted = formatted;

        timer.Start();
        timer.Tick(65.5f);

        Assert.That(timer.ElapsedSeconds, Is.EqualTo(65.5f).Within(0.001f));
        Assert.That(lastFormatted, Is.EqualTo("01:05.50"));
    }

    [Test]
    public void Tick_WhenStopped_DoesNotAdvance()
    {
        var timer = new ExpeditionTimer();
        timer.Start();
        timer.Stop();
        timer.Tick(10f);

        Assert.That(timer.ElapsedSeconds, Is.EqualTo(0f));
    }
}

public sealed class ExpeditionPhaseStateMachineTests
{
    [Test]
    public void TryTransition_AllowsValidFlow()
    {
        var sm = new ExpeditionPhaseStateMachine();

        Assert.That(sm.TryTransition(ExpeditionPhase.Climbing), Is.True);
        Assert.That(sm.TryTransition(ExpeditionPhase.Returning), Is.True);
        Assert.That(sm.TryTransition(ExpeditionPhase.Result), Is.True);
        Assert.That(sm.Current, Is.EqualTo(ExpeditionPhase.Result));
    }

    [Test]
    public void TryTransition_RejectsInvalidTransition()
    {
        var sm = new ExpeditionPhaseStateMachine();

        Assert.That(sm.TryTransition(ExpeditionPhase.Result), Is.False);
        Assert.That(sm.Current, Is.EqualTo(ExpeditionPhase.Basecamp));
    }
}
