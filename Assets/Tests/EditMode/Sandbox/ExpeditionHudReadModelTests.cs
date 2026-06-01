using NUnit.Framework;

public sealed class ExpeditionHudReadModelTests
{
    [Test]
    public void BuildSnapshot_FormatsCheckpointAndAltitude()
    {
        var model = new ExpeditionHudReadModel();

        var snapshot = model.BuildSnapshot("01:23.45", 1, 4, 128.7f);

        Assert.That(snapshot.FormattedTime, Is.EqualTo("01:23.45"));
        Assert.That(snapshot.CheckpointLabel, Is.EqualTo("Checkpoint 2/4"));
        Assert.That(snapshot.AltitudeLabel, Is.EqualTo("Alt: 129m"));
    }

    [Test]
    public void SetTransientMessage_ExpiresAfterTick()
    {
        var model = new ExpeditionHudReadModel();
        model.SetTransientMessage("Checkpoint 1!", 3f);

        var active = model.BuildSnapshot("00:00.00", -1, 0, 0f);
        Assert.That(active.TransientMessage, Is.EqualTo("Checkpoint 1!"));

        bool changed = model.TickTransientMessage(3.5f);
        var expired = model.BuildSnapshot("00:00.00", -1, 0, 0f);

        Assert.That(changed, Is.True);
        Assert.That(expired.TransientMessage, Is.Empty);
    }

    [Test]
    public void Publish_RaisesSnapshotChanged()
    {
        var model = new ExpeditionHudReadModel();
        ExpeditionHudSnapshot received = default;
        model.OnSnapshotChanged += snapshot => received = snapshot;

        model.Publish("00:10.00", 0, 3, 50f);

        Assert.That(received.FormattedTime, Is.EqualTo("00:10.00"));
        Assert.That(received.CheckpointLabel, Is.EqualTo("Checkpoint 1/3"));
    }
}
