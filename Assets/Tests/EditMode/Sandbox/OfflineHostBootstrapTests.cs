using NUnit.Framework;

public sealed class OfflineHostBootstrapTests
{
    [Test]
    public void FindAvailableUdpPort_ReturnsPortInSearchRange()
    {
        ushort port = OfflineHostBootstrap.FindAvailableUdpPort(7777);

        Assert.That(port, Is.GreaterThanOrEqualTo((ushort)7777));
        Assert.That(port, Is.LessThan((ushort)7797));
    }
}
