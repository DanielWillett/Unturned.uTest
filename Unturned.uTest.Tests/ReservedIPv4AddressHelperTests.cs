using NUnit.Framework;
using System.Runtime.CompilerServices;
using Unturned.SystemEx;
using uTest;

namespace uTest_Test;

[TestFixture]
public class ReservedIPv4AddressHelperTests
{
    [NUnit.Framework.Test]
    [Order(1)]
    public void TestInitIPv4Helpers()
    {
        RuntimeHelpers.RunClassConstructor(typeof(ReservedIPv4AddressHelper).TypeHandle);
    }

    [NUnit.Framework.Test]
    [Ignore("Takes a while")]
    public void TestDoesntFallInRange()
    {
        GameThread.Setup();
        Parallel.For(0L, ReservedIPv4AddressHelper.RangeSize, i =>
        {
            IPv4Address addr = ReservedIPv4AddressHelper.GetMappedPublicAddress((uint)i);

            Assert.That(ReservedIPv4AddressHelper.IsReserved(addr), Is.False);
        });
    }

    [NUnit.Framework.Test]
    [TestCase("0.0.0.0")] // IANA - Local Identification
    [TestCase("0.0.1.0")]
    [TestCase("0.0.0.1")]
    [TestCase("0.1.0.0")]
    [TestCase("0.0.255.0")]
    [TestCase("0.0.0.255")]
    [TestCase("0.255.0.0")]
    [TestCase("0.0.255.255")]
    [TestCase("0.255.0.255")]
    [TestCase("0.255.255.0")]
    [TestCase("0.255.255.255")]
    [TestCase("10.0.0.0")] // Private-Use Networks
    [TestCase("10.0.1.0")]
    [TestCase("10.0.0.1")]
    [TestCase("10.1.0.0")]
    [TestCase("10.0.255.0")]
    [TestCase("10.0.0.255")]
    [TestCase("10.255.0.0")]
    [TestCase("10.0.255.255")]
    [TestCase("10.255.0.255")]
    [TestCase("10.255.255.0")]
    [TestCase("10.255.255.255")]
    [TestCase("100.64.0.0")] // Shared Address Space
    [TestCase("100.67.255.255")]
    [TestCase("127.0.0.0")] // IANA - Loopback
    [TestCase("127.0.0.1")]
    [TestCase("127.255.255.255")]
    [TestCase("169.254.0.0")] // Link Local
    [TestCase("169.254.255.255")]
    [TestCase("172.16.0.0")] // Private-Use Networks
    [TestCase("172.16.240.255")]
    [TestCase("192.0.0.0")] // IANA IPv4 Special Purpose Address Registry
    [TestCase("192.0.0.255")]
    [TestCase("192.0.2.0")] // TEST-NET-1
    [TestCase("192.0.2.255")]
    [TestCase("192.88.99.0")] // 6to4 Relay Anycast
    [TestCase("192.88.99.255")]
    [TestCase("192.88.99.2")] // 6a44 Relay Anycast
    [TestCase("192.168.0.0")] // Private-Use Networks
    [TestCase("192.168.255.255")] // Private-Use Networks
    [TestCase("198.18.0.0")] // Network Interconnect Device Benchmark Testing
    [TestCase("198.19.255.255")]
    [TestCase("198.51.100.0")] // TEST-NET-2
    [TestCase("198.51.100.255")]
    [TestCase("203.0.113.0")] // TEST-NET-3
    [TestCase("203.0.113.255")]
    [TestCase("224.0.0.0")] // Multicast
    [TestCase("224.0.1.0")]
    [TestCase("224.0.0.1")]
    [TestCase("224.1.0.0")]
    [TestCase("224.0.255.0")]
    [TestCase("226.0.0.255")]
    [TestCase("228.255.0.0")]
    [TestCase("230.0.255.255")]
    [TestCase("233.255.0.255")]
    [TestCase("236.255.255.0")]
    [TestCase("239.255.255.255")]
    [TestCase("240.0.0.0")] // Future use
    [TestCase("240.0.1.0")]
    [TestCase("240.0.0.1")]
    [TestCase("240.1.0.0")]
    [TestCase("240.0.255.0")]
    [TestCase("242.0.0.255")]
    [TestCase("244.255.0.0")]
    [TestCase("246.0.255.255")]
    [TestCase("248.255.0.255")]
    [TestCase("250.255.255.0")]
    [TestCase("254.255.255.255")]
    [TestCase("255.0.0.0")]
    [TestCase("255.255.255.255")] // limited broadcast
    public void TestInvalidRanges(string ip)
    {
        Assert.That(IPv4Address.TryParse(ip, out IPv4Address addr));

        Assert.That(ReservedIPv4AddressHelper.IsReserved(addr));
    }
}
