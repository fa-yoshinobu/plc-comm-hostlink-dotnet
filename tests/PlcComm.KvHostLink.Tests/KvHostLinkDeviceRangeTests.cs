using System.Reflection;
using System.Text.Json;
using PlcComm.KvHostLink;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkDeviceRangeTests
{
    [Fact]
    public void ConnectionOptions_RequiresCanonicalPlcProfile()
    {
        var options = new KvHostLinkConnectionOptions(
            "127.0.0.1", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");

        Assert.Equal("keyence:kv-8000", options.PlcProfile);
        Assert.Equal(TimeSpan.FromSeconds(3), options.EffectiveTimeout);
        Assert.Throws<ArgumentException>(() => new KvHostLinkConnectionOptions(
            "127.0.0.1", 8501, HostLinkTransportMode.Tcp, ""));
        Assert.Throws<HostLinkProtocolError>(() => new KvHostLinkConnectionOptions(
            "127.0.0.1", 8501, HostLinkTransportMode.Tcp, "KV-8000"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KvHostLinkConnectionOptions(
            "127.0.0.1", 0, HostLinkTransportMode.Tcp, "keyence:kv-8000"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new KvHostLinkConnectionOptions(
            "127.0.0.1", 8501, (HostLinkTransportMode)99, "keyence:kv-8000"));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KvHostLinkConnectionOptions(
                "127.0.0.1", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000", TimeSpan.Zero)
                .EffectiveTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KvHostLinkConnectionOptions(
                "127.0.0.1", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000", TimeSpan.FromTicks(1)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KvHostLinkConnectionOptions(
                "127.0.0.1", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000",
                TimeSpan.FromMilliseconds((double)int.MaxValue + 1)));
        Assert.Equal(
            TimeSpan.FromMilliseconds(int.MaxValue),
            new KvHostLinkConnectionOptions(
                "127.0.0.1", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000",
                TimeSpan.FromMilliseconds(int.MaxValue)).EffectiveTimeout);
    }

    [Fact]
    public void PlcProfiles_GetNames_IncludesXymColumns()
    {
        var profiles = KvHostLinkPlcProfiles.GetNames();

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
    public void DeviceRangeCatalogForPlcProfile_MatchesCanonicalFixture()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "kv_device_ranges.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var profiles = document.RootElement.GetProperty("profiles");
        var expectedProfileIds = profiles.EnumerateObject().Select(static property => property.Name).ToArray();

        Assert.Equal(expectedProfileIds, KvHostLinkPlcProfiles.GetNames());
        foreach (var profileProperty in profiles.EnumerateObject())
        {
            Assert.Equal(
                profileProperty.Value.GetProperty("display_name").GetString(),
                KvHostLinkPlcProfiles.GetDisplayName(profileProperty.Name));
        }

        var catalogs = expectedProfileIds.ToDictionary(
            static profileId => profileId,
            KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile);
        foreach (var row in document.RootElement.GetProperty("device_range_rows").EnumerateArray())
        {
            var deviceType = row.GetProperty("device_type").GetString()!;
            foreach (var rangeProperty in row.GetProperty("ranges").EnumerateObject())
            {
                var entry = catalogs[rangeProperty.Name].Entry(deviceType);
                Assert.NotNull(entry);
                var expectedRange = rangeProperty.Value.GetString();
                if (expectedRange == "-")
                {
                    Assert.False(entry!.Supported);
                    Assert.Null(entry.AddressRange);
                }
                else
                {
                    Assert.True(entry!.Supported);
                    Assert.Equal(expectedRange, entry.AddressRange);
                }
            }
        }
    }

    [Fact]
    public void PlcProfileDescriptors_MatchCanonicalProfileMetadata()
    {
        var fixturePath = Path.Combine(AppContext.BaseDirectory, "fixtures", "kv_device_ranges.json");
        using var document = JsonDocument.Parse(File.ReadAllText(fixturePath));
        var profiles = document.RootElement.GetProperty("profiles");
        var descriptors = KvHostLinkPlcProfiles.GetProfileDescriptors();

        Assert.Equal(
            profiles.EnumerateObject().Select(static property => property.Name),
            descriptors.Select(static descriptor => descriptor.CanonicalName));
        foreach (var descriptor in descriptors)
        {
            var expected = profiles.GetProperty(descriptor.CanonicalName);
            Assert.Equal(expected.GetProperty("display_name").GetString(), descriptor.DisplayName);
            Assert.True(descriptor.Connectable);
            Assert.Equal(
                expected.TryGetProperty("base_profile", out var baseProfile) ? baseProfile.GetString() : null,
                descriptor.BaseProfile);
        }
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
    public void DeviceRangeCatalogForPlcProfile_InvalidRangeSegmentNumbersAreRejected()
    {
        var method = typeof(KvHostLinkDeviceRanges).GetMethod(
            "ParseSegmentBounds",
            BindingFlags.NonPublic | BindingFlags.Static);

        var error = Assert.Throws<TargetInvocationException>(() =>
            method!.Invoke(null, new object[] { "DMX-DM10", KvDeviceRangeNotation.Decimal, "DM" }));
        var inner = Assert.IsType<HostLinkProtocolError>(error.InnerException);
        Assert.Contains("Invalid device range start", inner.Message);
    }

    [Fact]
    public void DeviceRangeCatalogForPlcProfile_KeepsSingleDevicePrefixes()
    {
        var nano = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-nano");

        Assert.Equal("VM0-9999", nano.Entry("VM")!.AddressRange);
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
