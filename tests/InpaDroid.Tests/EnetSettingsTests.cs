using InpaDroid.Diag;

namespace InpaDroid.Tests;

public class EnetSettingsTests
{
    [Theory]
    [InlineData("auto")]
    [InlineData("AUTO")]
    [InlineData("auto:all")]
    [InlineData("  auto  ")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("169.254.12.34")]
    [InlineData("192.168.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("auto:wlan0")]
    [InlineData("192.168.68.10:HSFZ")]
    [InlineData("192.168.68.10:DoIP:13400:3496")]
    [InlineData("192.168.68.10:6801:6811")]
    public void IsValid_AcceptsKnownForms(string? raw) => Assert.True(EnetHostRule.IsValid(raw));

    [Theory]
    [InlineData("256.1.1.1")]
    [InlineData("1.2.3")]
    [InlineData("1.2.3.4.5")]
    [InlineData("1234.1.1.1")]
    [InlineData("1..2.3")]
    [InlineData("a b")]
    [InlineData("-bad")]
    [InlineData("bad-.com")]
    [InlineData("host_name")]
    [InlineData("http://host")]
    [InlineData("host:6801")]
    [InlineData("bmw-car")]
    [InlineData("car.local")]
    [InlineData("auto:")]
    [InlineData("192.168.0.1:70000")]
    [InlineData("192.168.0.1:1:2:3")]
    [InlineData("192.168.0.1:FTP")]
    [InlineData(".")]
    public void IsValid_RejectsJunk(string raw) => Assert.False(EnetHostRule.IsValid(raw));

    [Theory]
    [InlineData(null, "auto")]
    [InlineData("", "auto")]
    [InlineData("   ", "auto")]
    [InlineData(" 169.254.1.1 ", "169.254.1.1")]
    [InlineData("auto:all", "auto:all")]
    public void Normalize_TrimsAndDefaultsToAuto(string? raw, string expected) =>
        Assert.Equal(expected, EnetHostRule.Normalize(raw));

    [Theory]
    [InlineData(false, ConnectionFault.EnetNoNetwork)]
    [InlineData(null, ConnectionFault.EnetNoReply)]
    [InlineData(true, ConnectionFault.EnetNoReply)]
    public void Diagnose_Enet_NoReplyUsesNetworkCauses(bool? networkUp, ConnectionFault expected) =>
        Assert.Equal(expected, SetupCheck.Diagnose(true, true, null, null, enet: true, networkUp: networkUp));

    [Fact]
    public void Diagnose_Enet_KeepsOtherFaultsAndOk()
    {
        Assert.Equal(ConnectionFault.Adapter, SetupCheck.Diagnose(false, true, null, null, true, false));
        Assert.Equal(ConnectionFault.IgnitionOff, SetupCheck.Diagnose(true, true, 12.1, false, true, true));
        Assert.Equal(ConnectionFault.None, SetupCheck.Diagnose(true, true, 12.1, true, true, true));
    }

    [Fact]
    public void Diagnose_NonEnet_IgnoresNetwork() =>
        Assert.Equal(ConnectionFault.NoReply, SetupCheck.Diagnose(true, true, null, null, enet: false, networkUp: false));
}
