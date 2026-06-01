using NUnit.Framework;

public sealed class RelicDurabilityModelTests
{
    [Test]
    public void TryApplyImpact_IgnoresBelowThreshold()
    {
        var model = new RelicDurabilityModel(100f, 2f, 1f);

        bool applied = model.TryApplyImpact(1.5f, out float damage);

        Assert.That(applied, Is.False);
        Assert.That(damage, Is.EqualTo(0f));
        Assert.That(model.CurrentHp, Is.EqualTo(100f));
    }

    [Test]
    public void TryApplyImpact_ReducesHpWhenAboveThreshold()
    {
        var model = new RelicDurabilityModel(100f, 2f, 10f);

        bool applied = model.TryApplyImpact(3f, out float damage);

        Assert.That(applied, Is.True);
        Assert.That(damage, Is.EqualTo(50f));
        Assert.That(model.CurrentHp, Is.EqualTo(50f));
    }

    [Test]
    public void TryApplyDamage_ReducesHpAndUpdatesCondition()
    {
        var model = new RelicDurabilityModel(100f, 2f, 10f);

        bool applied = model.TryApplyDamage(50f, out float damage);

        Assert.That(applied, Is.True);
        Assert.That(damage, Is.EqualTo(50f));
        Assert.That(model.CurrentHp, Is.EqualTo(50f));
        Assert.That(model.Condition, Is.EqualTo(RelicCondition.Damaged));
    }

    [Test]
    public void TryRepair_RestoresHpUpToMax()
    {
        var model = new RelicDurabilityModel(100f, 2f, 1f);
        model.TryApplyDamage(40f, out _);

        bool repaired = model.TryRepair(20f);

        Assert.That(repaired, Is.True);
        Assert.That(model.CurrentHp, Is.EqualTo(80f));
    }

    [Test]
    public void EvaluateCondition_ReturnsDestroyedAtZero()
    {
        Assert.That(RelicDurabilityModel.EvaluateCondition(0f, 100f), Is.EqualTo(RelicCondition.Destroyed));
    }
}
