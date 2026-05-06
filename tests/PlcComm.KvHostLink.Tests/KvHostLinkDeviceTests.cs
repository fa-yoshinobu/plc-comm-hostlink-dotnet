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
    [InlineData("X390", "X", 39 * 16, "")]
    [InlineData("X400", "X", 40 * 16, "")]
    [InlineData("Y1999F", "Y", 1999 * 16 + 15, "")]
    [InlineData("M63999", "M", 63999, "")]
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
    [InlineData("M64000")]
    [InlineData("X3F0")]
    [InlineData("X3FF")]
    [InlineData("Y19A0")]
    [InlineData("Y20000")]
    public void ParseDevice_InvalidInput_ThrowsException(string input)
    {
        Assert.Throws<HostLinkProtocolError>(() => KvHostLinkDevice.ParseDevice(input));
    }

    [Theory]
    [InlineData("DM100", "DM100")]
    [InlineData("DM100.S", "DM100.S")]
    [InlineData("100", "R100")]
    [InlineData("R0", "R000")]
    [InlineData("R1", "R001")]
    [InlineData("R15", "R015")]
    [InlineData("MR115", "MR115")]
    [InlineData("CR0", "CR000")]
    [InlineData("B100", "B100")]
    [InlineData("X390", "X390")]
    [InlineData("X39F", "X39F")]
    [InlineData("X400", "X400")]
    public void ToText_ReturnsNormalizedString(string input, string expected)
    {
        var addr = KvHostLinkDevice.ParseDevice(input);
        Assert.Equal(expected, addr.ToText());
    }

    [Theory]
    [InlineData("R016")]
    [InlineData("MR116")]
    [InlineData("LR99916")]
    [InlineData("CR7916")]
    public void ParseDevice_InvalidBitBankNumber_ThrowsException(string input)
    {
        Assert.Throws<HostLinkProtocolError>(() => KvHostLinkDevice.ParseDevice(input));
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
    [InlineData("X", 39 * 16, "", 32)]
    [InlineData("Y", 1999 * 16 + 15, "", 1)]
    public void ValidateDeviceSpan_ValidInput_DoesNotThrow(string deviceType, int startNumber, string format, int count)
    {
        KvHostLinkDevice.ValidateDeviceSpan(deviceType, startNumber, format, count);
    }

    [Theory]
    [InlineData("DM", 65534, ".D", 1)]
    [InlineData("DM", 65534, ".L", 1)]
    [InlineData("DM", 65534, ".U", 2)]
    [InlineData("B", 0x7FFF, ".U", 2)]
    [InlineData("X", 1999 * 16 + 15, "", 2)]
    [InlineData("Y", 1999 * 16 + 15, "", 2)]
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
