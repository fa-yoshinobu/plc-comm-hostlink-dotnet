namespace PlcComm.KvHostLink.Tests;

public class KvHostLinkDeviceTests
{
    [Theory]
    [InlineData("DM100", "DM", 100, "")]
    [InlineData("DM100.S", "DM", 100, ".S")]
    [InlineData("R0", "R", 0, "")]
    [InlineData("100", "R", 100, "")]
    [InlineData("MR500.U", "MR", 500, ".U")]
    [InlineData("B100", "B", 0x100, "")]
    public void ParseDevice_ValidInput_ReturnsExpected(string input, string expectedType, int expectedNumber, string expectedSuffix)
    {
        var result = KvHostLinkDevice.ParseDevice(input);
        Assert.Equal(expectedType, result.DeviceType);
        Assert.Equal(expectedNumber, result.Number);
        Assert.Equal(expectedSuffix, result.Suffix);
    }

    [Theory]
    [InlineData("DM65535")] // Out of range
    [InlineData("Z13")] // Out of range
    [InlineData("INVALID123")]
    public void ParseDevice_InvalidInput_ThrowsException(string input)
    {
        Assert.Throws<HostLinkProtocolException>(() => KvHostLinkDevice.ParseDevice(input));
    }

    [Theory]
    [InlineData("DM100", "DM100")]
    [InlineData("DM100.S", "DM100.S")]
    [InlineData("100", "R100")]
    [InlineData("B100", "B100")]
    public void ToText_ReturnsNormalizedString(string input, string expected)
    {
        var addr = KvHostLinkDevice.ParseDevice(input);
        Assert.Equal(expected, addr.ToText());
    }
}
