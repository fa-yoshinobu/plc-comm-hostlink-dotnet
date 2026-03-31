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
    [InlineData("DM100.F")]
    [InlineData("INVALID123")]
    public void ParseDevice_InvalidInput_ThrowsException(string input)
    {
        Assert.Throws<HostLinkProtocolError>(() => KvHostLinkDevice.ParseDevice(input));
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

    [Theory]
    [InlineData("dm100", "DM100")]
    [InlineData("dm100:f", "DM100:F")]
    [InlineData("dm100.a", "DM100.A")]
    [InlineData("100", "R100")]
    public void KvHostLinkAddress_Normalize_ReturnsCanonicalText(string input, string expected)
    {
        Assert.Equal(expected, KvHostLinkAddress.Normalize(input));
    }

    [Theory]
    [InlineData(".U", 1)]
    [InlineData(".U", 1000)]
    [InlineData(".D", 1)]
    [InlineData(".D", 500)]
    public void ValidateExpansionBufferCount_ValidInput_DoesNotThrow(string format, int count)
    {
        KvHostLinkDevice.ValidateExpansionBufferCount(format, count);
    }

    [Theory]
    [InlineData(".U", 0)]
    [InlineData(".U", 1001)]
    [InlineData(".D", 0)]
    [InlineData(".D", 501)]
    [InlineData(".L", 501)]
    public void ValidateExpansionBufferCount_InvalidInput_Throws(string format, int count)
    {
        Assert.Throws<HostLinkProtocolError>(() => KvHostLinkDevice.ValidateExpansionBufferCount(format, count));
    }

    [Theory]
    [InlineData("DM", 65533, ".D", 1)]
    [InlineData("DM", 65534, ".U", 1)]
    [InlineData("B", 0x7FFE, ".U", 2)]
    public void ValidateDeviceSpan_ValidInput_DoesNotThrow(string deviceType, int startNumber, string format, int count)
    {
        KvHostLinkDevice.ValidateDeviceSpan(deviceType, startNumber, format, count);
    }

    [Theory]
    [InlineData("DM", 65534, ".D", 1)]
    [InlineData("DM", 65534, ".L", 1)]
    [InlineData("DM", 65534, ".U", 2)]
    [InlineData("B", 0x7FFF, ".U", 2)]
    public void ValidateDeviceSpan_InvalidInput_Throws(string deviceType, int startNumber, string format, int count)
    {
        Assert.Throws<HostLinkProtocolError>(() => KvHostLinkDevice.ValidateDeviceSpan(deviceType, startNumber, format, count));
    }

    [Theory]
    [InlineData(59999, ".U", 1)]
    [InlineData(59998, ".D", 1)]
    public void ValidateExpansionBufferSpan_ValidInput_DoesNotThrow(int address, string format, int count)
    {
        KvHostLinkDevice.ValidateExpansionBufferSpan(address, format, count);
    }

    [Theory]
    [InlineData(59999, ".D", 1)]
    [InlineData(59999, ".U", 2)]
    public void ValidateExpansionBufferSpan_InvalidInput_Throws(int address, string format, int count)
    {
        Assert.Throws<HostLinkProtocolError>(() => KvHostLinkDevice.ValidateExpansionBufferSpan(address, format, count));
    }

}
