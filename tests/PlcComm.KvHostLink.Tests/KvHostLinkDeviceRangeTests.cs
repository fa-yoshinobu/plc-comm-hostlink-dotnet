using PlcComm.KvHostLink;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkDeviceRangeTests
{
    [Fact]
    public void AvailablePlcProfiles_IncludesXymColumns()
    {
        var profiles = KvHostLinkDeviceRanges.AvailablePlcProfiles();

        Assert.Equal(
            [
                "keyence:kv-nano",
                "keyence:kv-nano-xym",
                "keyence:kv-3000",
                "keyence:kv-3000-xym",
                "keyence:kv-5000",
                "keyence:kv-5000-xym",
                "keyence:kv-7000",
                "keyence:kv-7000-xym",
                "keyence:kv-8000",
                "keyence:kv-8000-xym",
                "keyence:kv-x500",
                "keyence:kv-x500-xym",
            ],
            profiles);
    }

    [Fact]
    public void DeviceRangeCatalogForPlcProfile_ResolvesCanonicalProfiles()
    {
        var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-8000");

        Assert.Equal("keyence:kv-8000", catalog.PlcProfile);
        Assert.Equal("", catalog.ModelCode);
        Assert.False(catalog.HasModelCode);
        Assert.Equal("keyence:kv-8000", catalog.ResolvedPlcProfile);
        Assert.Equal("DM00000-DM65534", catalog.Entry("DM")!.AddressRange);

        var xCatalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-x500");
        Assert.Equal("keyence:kv-x500", xCatalog.ResolvedPlcProfile);
        Assert.Equal("ZF000000-ZF524287", xCatalog.Entry("ZF")!.AddressRange);

        var tm = catalog.Entry("TM")!;
        Assert.Equal(KvDeviceRangeCategory.Word, tm.Category);
        Assert.False(tm.IsBitDevice);
        Assert.Equal("TM000-TM511", tm.AddressRange);
    }

    [Fact]
    public void DeviceRangeCatalogForPlcProfile_XymCatalogSplitsAliasRanges()
    {
        var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-3000-xym");
        var entry = catalog.Entry("R")!;

        Assert.Equal("R", entry.Device);
        Assert.Equal(KvDeviceRangeCategory.Bit, entry.Category);
        Assert.True(entry.IsBitDevice);
        Assert.Equal(KvDeviceRangeNotation.Hexadecimal, entry.Notation);
        Assert.Equal((uint)0, entry.LowerBound);
        Assert.Equal((uint?)(999 * 16 + 15), entry.UpperBound);
        Assert.Equal((uint?)(1000 * 16), entry.PointCount);
        Assert.Equal("X0-999F,Y0-999F", entry.AddressRange);
        Assert.Contains("multiple alias devices", Assert.IsType<string>(entry.Notes));
        Assert.Equal(2, entry.Segments.Count);
        Assert.Equal("X", entry.Segments[0].Device);
        Assert.Equal(KvDeviceRangeNotation.Hexadecimal, entry.Segments[0].Notation);
        Assert.Equal((uint)0, entry.Segments[0].LowerBound);
        Assert.Equal((uint?)(999 * 16 + 15), entry.Segments[0].UpperBound);
        Assert.Equal((uint?)(1000 * 16), entry.Segments[0].PointCount);
        Assert.Equal("X0-999F", entry.Segments[0].AddressRange);
        Assert.Equal("Y", entry.Segments[1].Device);
        Assert.Equal(KvDeviceRangeNotation.Hexadecimal, entry.Segments[1].Notation);
        Assert.Equal((uint)0, entry.Segments[1].LowerBound);
        Assert.Equal((uint?)(999 * 16 + 15), entry.Segments[1].UpperBound);
        Assert.Equal((uint?)(1000 * 16), entry.Segments[1].PointCount);
        Assert.Equal("Y0-999F", entry.Segments[1].AddressRange);
        Assert.Equal("R", catalog.Entry("X")!.DeviceType);

        var kv8000 = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-8000-xym");
        var r = kv8000.Entry("R")!;
        Assert.Equal((uint?)(1999 * 16 + 15), r.UpperBound);
        Assert.Equal((uint?)(2000 * 16), r.PointCount);
        Assert.Equal((uint?)(1999 * 16 + 15), r.Segments[0].UpperBound);
        Assert.Equal((uint?)(1999 * 16 + 15), r.Segments[1].UpperBound);

        var dm = catalog.Entry("DM")!;
        Assert.Equal("D", dm.Device);
        Assert.Equal(KvDeviceRangeCategory.Word, dm.Category);
        Assert.False(dm.IsBitDevice);
        Assert.Equal((uint)0, dm.LowerBound);
        Assert.Equal((uint?)65534, dm.UpperBound);
        Assert.Equal((uint?)65535, dm.PointCount);
        Assert.Equal(KvDeviceRangeNotation.Decimal, dm.Notation);
        Assert.Equal("D", dm.Segments[0].Device);
        Assert.Equal("D0-65534", dm.Segments[0].AddressRange);
        Assert.Equal("DM", catalog.Entry("D")!.DeviceType);

        var fm = catalog.Entry("FM")!;
        Assert.Equal("F", fm.Device);
        Assert.Equal("F0-32767", fm.AddressRange);
        Assert.Equal("F", fm.Segments[0].Device);
        Assert.Equal("F0-32767", fm.Segments[0].AddressRange);
    }

    [Fact]
    public void DeviceRangeCatalogForPlcProfile_PublishesCorrectedRanges()
    {
        var nano = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-nano");
        Assert.Equal("CM0000-CM8999", nano.Entry("CM")!.AddressRange);

        var xym = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-5000-xym");
        Assert.Equal("CR0000-CR3915", xym.Entry("CR")!.AddressRange);

        var kvx = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-x500");
        Assert.Equal("Z1-10", kvx.Entry("Z")!.AddressRange);
    }

    [Fact]
    public void DeviceRangeCatalogForPlcProfile_KeepsSingleDevicePrefixes()
    {
        var nano = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-nano");

        Assert.Equal("VM0-9499", nano.Entry("VM")!.AddressRange);
        Assert.Equal("VB0-1FFF", nano.Entry("VB")!.AddressRange);
        Assert.Equal("CTC0-7", nano.Entry("CTC")!.AddressRange);

        var kv3000 = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-3000");
        Assert.Equal("AT0-7", kv3000.Entry("AT")!.AddressRange);
        Assert.Equal("CTH0-1", kv3000.Entry("CTH")!.AddressRange);

        var kv5000 = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-5000");
        Assert.Equal("AT0-7", kv5000.Entry("AT")!.AddressRange);
        Assert.Equal("CTH0-1", kv5000.Entry("CTH")!.AddressRange);
    }

    [Fact]
    public void DeviceRangeCatalogForPlcProfile_UnsupportedEntriesRemainPresent()
    {
        var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-nano");
        var em = catalog.Entry("EM")!;

        Assert.False(em.Supported);
        Assert.Null(em.AddressRange);
        Assert.Empty(em.Segments);
    }

    [Fact]
    public void DeviceRangeCatalogForPlcProfile_UnsupportedProfileThrows()
    {
        Assert.Throws<HostLinkProtocolError>(() =>
            KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-1000"));
        Assert.Throws<HostLinkProtocolError>(() =>
            KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("KV-X500"));
        Assert.Throws<HostLinkProtocolError>(() =>
            KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("KEYENCE:KV-X500"));
    }
}
