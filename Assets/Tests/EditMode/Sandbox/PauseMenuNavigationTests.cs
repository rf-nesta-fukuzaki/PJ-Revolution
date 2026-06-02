using NUnit.Framework;
using Sandbox.UI;

public sealed class PauseMenuNavigationTests
{
    [Test]
    public void OnCancel_FromMenu_Resumes()
    {
        Assert.That(PauseMenuNavigation.OnCancel(PausePage.Menu), Is.EqualTo(PauseNavAction.Resume));
    }

    [Test]
    public void OnCancel_FromSettings_GoesBackToMenu()
    {
        Assert.That(PauseMenuNavigation.OnCancel(PausePage.Settings), Is.EqualTo(PauseNavAction.GoToMenu));
    }

    [Test]
    public void OnCancel_FromConfirmLeave_GoesBackToMenu()
    {
        Assert.That(PauseMenuNavigation.OnCancel(PausePage.ConfirmLeave), Is.EqualTo(PauseNavAction.GoToMenu));
    }

    [Test]
    public void OnCancel_FromNone_DoesNothing()
    {
        Assert.That(PauseMenuNavigation.OnCancel(PausePage.None), Is.EqualTo(PauseNavAction.None));
    }

    [Test]
    public void IsOpen_OnlyNoneIsClosed()
    {
        Assert.That(PauseMenuNavigation.IsOpen(PausePage.None), Is.False);
        Assert.That(PauseMenuNavigation.IsOpen(PausePage.Menu), Is.True);
        Assert.That(PauseMenuNavigation.IsOpen(PausePage.Settings), Is.True);
        Assert.That(PauseMenuNavigation.IsOpen(PausePage.ConfirmLeave), Is.True);
    }
}
