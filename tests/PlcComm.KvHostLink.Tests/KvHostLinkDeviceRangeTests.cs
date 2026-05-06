using PlcComm.KvHostLink;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkDeviceRangeTests
{
    [Fact]
    public void AvailableDeviceRangeModels_IncludesXymColumns()
    {
        var models = KvHostLinkDeviceRanges.AvailableDeviceRangeModels();

        Assert.Contains("KV-7000", models);
        Assert.Contains("KV-7000(XYM)", models);
    }

    [Fact]
    public void DeviceRangeCatalogForModel_ResolvesKnownRuntimeModelNames()
    {
        var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-8000A");

        Assert.Equal("KV-8000", catalog.Model);
        Assert.Equal("", catalog.ModelCode);
        Assert.False(catalog.HasModelCode);
        Assert.Equal("KV-8000", catalog.ResolvedModel);
        Assert.Equal("DM00000-DM65534", catalog.Entry("DM")!.AddressRange);

        var xCatalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-X530");
        Assert.Equal("KV-X500", xCatalog.ResolvedModel);
        Assert.Equal("ZF000000-ZF524287", xCatalog.Entry("ZF")!.AddressRange);

        var tm = catalog.Entry("TM")!;
        Assert.Equal(KvDeviceRangeCategory.Word, tm.Category);
        Assert.False(tm.IsBitDevice);
        Assert.Equal("TM000-TM511", tm.AddressRange);
    }

    [Fact]
    public void DeviceRangeCatalogForModel_XymCatalogSplitsAliasRanges()
    {
        var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-3000/5000(XYM)");
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

        var kv8000 = KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-8000(XYM)");
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
    public void DeviceRangeCatalogForModel_PublishesCorrectedRanges()
    {
        var nano = KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-N24nn");
        Assert.Equal("CM0000-CM8999", nano.Entry("CM")!.AddressRange);

        var xym = KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-3000/5000(XYM)");
        Assert.Equal("CR0000-CR3915", xym.Entry("CR")!.AddressRange);
    }

    [Fact]
    public void DeviceRangeCatalogForModel_KeepsSingleDevicePrefixes()
    {
        var nano = KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-N24nn");

        Assert.Equal("VM0-9499", nano.Entry("VM")!.AddressRange);
        Assert.Equal("VB0-1FFF", nano.Entry("VB")!.AddressRange);
        Assert.Equal("CTC0-7", nano.Entry("CTC")!.AddressRange);

        var kv3000 = KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-3000/5000");
        Assert.Equal("AT0-7", kv3000.Entry("AT")!.AddressRange);
        Assert.Equal("CTH0-1", kv3000.Entry("CTH")!.AddressRange);
    }

    [Fact]
    public void DeviceRangeCatalogForModel_UnsupportedEntriesRemainPresent()
    {
        var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-N24nn");
        var em = catalog.Entry("EM")!;

        Assert.False(em.Supported);
        Assert.Null(em.AddressRange);
        Assert.Empty(em.Segments);
    }

    [Fact]
    public void DeviceRangeCatalogForModel_UnsupportedModelThrows()
    {
        Assert.Throws<HostLinkProtocolError>(() => KvHostLinkDeviceRanges.DeviceRangeCatalogForModel("KV-1000"));
    }
}
