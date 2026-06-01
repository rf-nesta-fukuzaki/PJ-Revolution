using NUnit.Framework;
using UnityEngine;

public sealed class SettingsApplierTests
{
    [Test]
    public void VolumeToDb_Silence_ReturnsMinus80()
    {
        Assert.That(SettingsApplier.VolumeToDb(0), Is.EqualTo(-80f).Within(0.001f));
    }

    [Test]
    public void VolumeToDb_FullScale_ReturnsZeroDb()
    {
        Assert.That(SettingsApplier.VolumeToDb(100), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void VolumeToDb_HalfScale_IsNegative()
    {
        float db = SettingsApplier.VolumeToDb(50);
        Assert.That(db, Is.LessThan(0f));
        Assert.That(db, Is.GreaterThan(-80f));
    }

    [Test]
    public void ApplyShadowQuality_Disable_TurnsShadowsOff()
    {
        SettingsApplier.ApplyShadowQuality(0);
        Assert.That(QualitySettings.shadows, Is.EqualTo(ShadowQuality.Disable));
    }
}
