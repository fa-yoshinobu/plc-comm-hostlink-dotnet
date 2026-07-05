using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace PlcComm.KvHostLink;

public enum KvDeviceRangeNotation
{
    [SuppressMessage("Naming", "CA1720:Identifier contains type name", Justification = "Matches the Rust HostLink public API.")]
    Decimal,
    Hexadecimal,
}

public enum KvDeviceRangeCategory
{
    Bit,
    Word,
    TimerCounter,
    Index,
    FileRegister,
}

public sealed record KvDeviceRangeSegment(
    string Device,
    KvDeviceRangeCategory Category,
    bool IsBitDevice,
    KvDeviceRangeNotation Notation,
    uint LowerBound,
    uint? UpperBound,
    uint? PointCount,
    string AddressRange);

public sealed record KvDeviceRangeEntry(
    string Device,
    string DeviceType,
    KvDeviceRangeCategory Category,
    bool IsBitDevice,
    KvDeviceRangeNotation Notation,
    bool Supported,
    uint LowerBound,
    uint? UpperBound,
    uint? PointCount,
    string? AddressRange,
    string Source,
    string? Notes,
    IReadOnlyList<KvDeviceRangeSegment> Segments);

public sealed record KvDeviceRangeCatalog(
    string PlcProfile,
    string ModelCode,
    bool HasModelCode,
    string RequestedPlcProfile,
    string ResolvedPlcProfile,
    IReadOnlyList<KvDeviceRangeEntry> Entries)
{
    public KvDeviceRangeEntry? Entry(string deviceType)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deviceType);

        var wanted = deviceType.Trim();
        return Entries
            .FirstOrDefault(entry => string.Equals(entry.DeviceType, wanted, StringComparison.OrdinalIgnoreCase))
            ?? Entries.FirstOrDefault(entry => string.Equals(entry.Device, wanted, StringComparison.OrdinalIgnoreCase))
            ?? Entries.FirstOrDefault(entry => entry.Segments.Any(segment =>
                string.Equals(segment.Device, wanted, StringComparison.OrdinalIgnoreCase)));
    }
}

public static class KvHostLinkDeviceRanges
{
    private static readonly Lazy<RangeTable> ParsedRangeTable = new(CreateRangeTable);

    private static RangeTable CreateRangeTable() => new(
        [
            new("KV-NANO", "keyence:kv-nano"),
            new("KV-NANO(XYM)", "keyence:kv-nano-xym"),
            new("KV-3000", "keyence:kv-3000"),
            new("KV-3000(XYM)", "keyence:kv-3000-xym"),
            new("KV-5000", "keyence:kv-5000"),
            new("KV-5000(XYM)", "keyence:kv-5000-xym"),
            new("KV-7000", "keyence:kv-7000"),
            new("KV-7000(XYM)", "keyence:kv-7000-xym"),
            new("KV-8000", "keyence:kv-8000"),
            new("KV-8000(XYM)", "keyence:kv-8000-xym"),
            new("KV-X500", "keyence:kv-x500"),
            new("KV-X500(XYM)", "keyence:kv-x500-xym"),
        ],
        [
            Row("R", KvDeviceRangeNotation.Decimal, "R00000-R59915", "X0-599F,Y0-599F", "R00000-R99915", "X0-999F,Y0-999F", "R00000-R99915", "X0-999F,Y0-999F", "R00000-R199915", "X0-1999F,Y0-1999F", "R00000-R199915", "X0-1999F,Y0-1999F", "R00000-R199915", "X0-1999F,Y0-1999F"),
            Row("B", KvDeviceRangeNotation.Hexadecimal, "B0000-B1FFF", "B0000-B1FFF", "B0000-B3FFF", "B0000-B3FFF", "B0000-B3FFF", "B0000-B3FFF", "B0000-B7FFF", "B0000-B7FFF", "B0000-B7FFF", "B0000-B7FFF", "B0000-B7FFF", "B0000-B7FFF"),
            Row("MR", KvDeviceRangeNotation.Decimal, "MR00000-MR59915", "M0-9599", "MR00000-MR99915", "M0-15999", "MR00000-MR99915", "M0-15999", "MR000000-MR399915", "M000000-M63999", "MR000000-MR399915", "M000000-M63999", "MR000000-MR399915", "M000000-M63999"),
            Row("LR", KvDeviceRangeNotation.Decimal, "LR00000-LR19915", "L0-3199", "LR00000-LR99915", "L0-15999", "LR00000-LR99915", "L0-15999", "LR00000-LR99915", "L00000-L15999", "LR00000-LR99915", "L00000-L15999", "LR00000-LR99915", "L00000-L15999"),
            Row("CR", KvDeviceRangeNotation.Decimal, "CR0000-CR8915", "CR0000-CR8915", "CR0000-CR3915", "CR0000-CR3915", "CR0000-CR3915", "CR0000-CR3915", "CR0000-CR7915", "CR0000-CR7915", "CR0000-CR7915", "CR0000-CR7915", "CR0000-CR7915", "CR0000-CR7915"),
            Row("CM", KvDeviceRangeNotation.Decimal, "CM0000-CM8999", "CM0000-CM8999", "CM0000-CM5999", "CM0000-CM5999", "CM0000-CM5999", "CM0000-CM5999", "CM0000-CM5999", "CM0000-CM5999", "CM0000-CM7599", "CM0000-CM7599", "CM0000-CM7599", "CM0000-CM7599"),
            Row("T", KvDeviceRangeNotation.Decimal, "T0000-T0511", "T0000-T0511", "T0000-T3999", "T0000-T3999", "T0000-T3999", "T0000-T3999", "T0000-T3999", "T0000-T3999", "T0000-T3999", "T0000-T3999", "T0000-T3999", "T0000-T3999"),
            Row("TC", KvDeviceRangeNotation.Decimal, "TC0000-TC0511", "TC0000-TC0511", "TC0000-TC3999", "TC0000-TC3999", "TC0000-TC3999", "TC0000-TC3999", "TC0000-TC3999", "TC0000-TC3999", "TC0000-TC3999", "TC0000-TC3999", "TC0000-TC3999", "TC0000-TC3999"),
            Row("TS", KvDeviceRangeNotation.Decimal, "TS0000-TS0511", "TS0000-TS0511", "TS0000-TS3999", "TS0000-TS3999", "TS0000-TS3999", "TS0000-TS3999", "TS0000-TS3999", "TS0000-TS3999", "TS0000-TS3999", "TS0000-TS3999", "TS0000-TS3999", "TS0000-TS3999"),
            Row("C", KvDeviceRangeNotation.Decimal, "C0000-C0255", "C0000-C0255", "C0000-C3999", "C0000-C3999", "C0000-C3999", "C0000-C3999", "C0000-C3999", "C0000-C3999", "C0000-C3999", "C0000-C3999", "C0000-C3999", "C0000-C3999"),
            Row("CC", KvDeviceRangeNotation.Decimal, "CC0000-CC0255", "CC0000-CC0255", "CC0000-CC3999", "CC0000-CC3999", "CC0000-CC3999", "CC0000-CC3999", "CC0000-CC3999", "CC0000-CC3999", "CC0000-CC3999", "CC0000-CC3999", "CC0000-CC3999", "CC0000-CC3999"),
            Row("CS", KvDeviceRangeNotation.Decimal, "CS0000-CS0255", "CS0000-CS0255", "CS0000-CS3999", "CS0000-CS3999", "CS0000-CS3999", "CS0000-CS3999", "CS0000-CS3999", "CS0000-CS3999", "CS0000-CS3999", "CS0000-CS3999", "CS0000-CS3999", "CS0000-CS3999"),
            Row("DM", KvDeviceRangeNotation.Decimal, "DM00000-DM32767", "D0-32767", "DM00000-DM65534", "D0-65534", "DM00000-DM65534", "D0-65534", "DM00000-DM65534", "D00000-D65534", "DM00000-DM65534", "D00000-D65534", "DM00000-DM65534", "D00000-D65534"),
            Row("EM", KvDeviceRangeNotation.Decimal, "-", "-", "EM00000-EM65534", "E0-65534", "EM00000-EM65534", "E0-65534", "EM00000-EM65534", "E00000-E65534", "EM00000-EM65534", "E00000-E65534", "EM00000-EM65534", "E00000-E65534"),
            Row("FM", KvDeviceRangeNotation.Decimal, "-", "-", "FM00000-FM32767", "F0-32767", "FM00000-FM32767", "F0-32767", "FM00000-FM32767", "F00000-F32767", "FM00000-FM32767", "F00000-F32767", "FM00000-FM32767", "F00000-F32767"),
            Row("ZF", KvDeviceRangeNotation.Decimal, "-", "-", "ZF000000-ZF131071", "ZF000000-ZF131071", "ZF000000-ZF131071", "ZF000000-ZF131071", "ZF000000-ZF524287", "ZF000000-ZF524287", "ZF000000-ZF524287", "ZF000000-ZF524287", "ZF000000-ZF524287", "ZF000000-ZF524287"),
            Row("W", KvDeviceRangeNotation.Hexadecimal, "W0000-W3FFF", "W0000-W3FFF", "W0000-W3FFF", "W0000-W3FFF", "W0000-W3FFF", "W0000-W3FFF", "W0000-W7FFF", "W0000-W7FFF", "W0000-W7FFF", "W0000-W7FFF", "W0000-W7FFF", "W0000-W7FFF"),
            Row("TM", KvDeviceRangeNotation.Decimal, "TM000-TM511", "TM000-TM511", "TM000-TM511", "TM000-TM511", "TM000-TM511", "TM000-TM511", "TM000-TM511", "TM000-TM511", "TM000-TM511", "TM000-TM511", "TM000-TM511", "TM000-TM511"),
            Row("VM", KvDeviceRangeNotation.Decimal, "VM0-9499", "VM0-9499", "VM0-49999", "VM0-49999", "VM0-49999", "VM0-49999", "VM0-63999", "VM0-63999", "VM0-589823", "VM0-589823", "-", "-"),
            Row("VB", KvDeviceRangeNotation.Hexadecimal, "VB0-1FFF", "VB0-1FFF", "VB0-3FFF", "VB0-3FFF", "VB0-3FFF", "VB0-3FFF", "VB0-F9FF", "VB0-F9FF", "VB0-F9FF", "VB0-F9FF", "-", "-"),
            Row("Z", KvDeviceRangeNotation.Decimal, "Z1-12", "Z1-12", "Z1-12", "Z1-12", "Z1-12", "Z1-12", "Z1-12", "Z1-12", "Z1-12", "Z1-12", "Z1-10", "Z1-10"),
            Row("CTH", KvDeviceRangeNotation.Decimal, "CTH0-3", "CTH0-3", "CTH0-1", "CTH0-3", "CTH0-1", "CTH0-3", "-", "-", "-", "-", "-", "-"),
            Row("CTC", KvDeviceRangeNotation.Decimal, "CTC0-7", "CTC0-7", "CTC0-3", "CTC0-3", "CTC0-3", "CTC0-3", "-", "-", "-", "-", "-", "-"),
            Row("AT", KvDeviceRangeNotation.Decimal, "-", "-", "AT0-7", "AT0-7", "AT0-7", "AT0-7", "AT0-7", "AT0-7", "AT0-7", "AT0-7", "-", "-"),
        ]);

    private static RangeRow Row(string deviceType, KvDeviceRangeNotation notation, params string[] ranges) =>
        new(deviceType, notation, ranges);

    public static IReadOnlyList<string> AvailablePlcProfiles()
    {
        return ParsedRangeTable.Value.Profiles.Select(profile => profile.PlcProfile).ToArray();
    }

    public static string GetDisplayName(string plcProfile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plcProfile);

        var normalized = NormalizePlcProfile(plcProfile);
        _ = RangeProfileForPlcProfile(ParsedRangeTable.Value, normalized);
        return normalized switch
        {
            "keyence:kv-nano" => "KEYENCE KV-NANO",
            "keyence:kv-nano-xym" => "KEYENCE KV-NANO (XYM)",
            "keyence:kv-3000" => "KEYENCE KV-3000",
            "keyence:kv-3000-xym" => "KEYENCE KV-3000 (XYM)",
            "keyence:kv-5000" => "KEYENCE KV-5000",
            "keyence:kv-5000-xym" => "KEYENCE KV-5000 (XYM)",
            "keyence:kv-7000" => "KEYENCE KV-7000",
            "keyence:kv-7000-xym" => "KEYENCE KV-7000 (XYM)",
            "keyence:kv-8000" => "KEYENCE KV-8000",
            "keyence:kv-8000-xym" => "KEYENCE KV-8000 (XYM)",
            "keyence:kv-x500" => "KEYENCE KV-X500",
            "keyence:kv-x500-xym" => "KEYENCE KV-X500 (XYM)",
            _ => throw new HostLinkProtocolError($"Unsupported PLC profile '{plcProfile}'."),
        };
    }

    public static KvDeviceRangeCatalog DeviceRangeCatalogForPlcProfile(string plcProfile)
    {
        return BuildCatalog(plcProfile, modelCode: null);
    }

    private static KvDeviceRangeCatalog BuildCatalog(string plcProfile, string? modelCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plcProfile);

        var requestedPlcProfile = NormalizePlcProfile(plcProfile);
        var table = ParsedRangeTable.Value;
        var resolvedProfile = RangeProfileForPlcProfile(table, requestedPlcProfile);
        var modelIndex = table.Profiles.IndexOf(resolvedProfile);

        var entries = table.Rows
            .Select(row => BuildEntry(row, modelIndex, resolvedProfile.DisplayName))
            .ToArray();

        return new KvDeviceRangeCatalog(
            resolvedProfile.PlcProfile,
            modelCode ?? string.Empty,
            modelCode is not null,
            requestedPlcProfile,
            resolvedProfile.PlcProfile,
            entries);
    }

    private static KvDeviceRangeEntry BuildEntry(RangeRow row, int modelIndex, string resolvedModel)
    {
        var rangeText = row.Ranges[modelIndex].Trim();
        var supported = rangeText.Length > 0 && rangeText != "-";
        var addressRange = supported ? rangeText : null;
        var segments = addressRange is null ? [] : ParseSegments(row, addressRange);
        var primaryDevice = PrimaryDeviceName(row, segments);
        var (category, isBitDevice) = DeviceMetadata(primaryDevice);
        var notation = EntryNotation(row.Notation, segments);
        var (lowerBound, upperBound, pointCount) = SummarizeEntryBounds(segments);

        return new KvDeviceRangeEntry(
            primaryDevice,
            row.DeviceType,
            category,
            isBitDevice,
            notation,
            supported,
            lowerBound,
            upperBound,
            pointCount,
            addressRange,
            $"Embedded device range table ({resolvedModel})",
            segments.Length > 1
                ? "Published address range expands to multiple alias devices; inspect segments."
                : null,
            segments);
    }

    private static KvDeviceRangeSegment[] ParseSegments(RangeRow row, string rangeText)
    {
        return rangeText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(segment =>
            {
                var device = SegmentDevice(segment);
                if (device.Length == 0)
                {
                    device = row.DeviceType;
                }

                var (category, isBitDevice) = DeviceMetadata(device);
                var notation = NotationForDevice(row.Notation, device);
                var (lowerBound, upperBound, pointCount) = ParseSegmentBounds(segment, notation, device);
                return new KvDeviceRangeSegment(
                    device,
                    category,
                    isBitDevice,
                    notation,
                    lowerBound,
                    upperBound,
                    pointCount,
                    segment);
            })
            .ToArray();
    }

    private static string SegmentDevice(string segment)
    {
        var builder = new StringBuilder();
        foreach (var ch in segment)
        {
            if (!char.IsAsciiLetter(ch))
            {
                break;
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }

    private static string PrimaryDeviceName(RangeRow row, KvDeviceRangeSegment[] segments)
    {
        var uniqueDevices = new List<string>();
        foreach (var segment in segments)
        {
            if (!uniqueDevices.Any(device => string.Equals(device, segment.Device, StringComparison.OrdinalIgnoreCase)))
            {
                uniqueDevices.Add(segment.Device);
            }
        }

        return uniqueDevices.Count == 1 ? uniqueDevices[0] : row.DeviceType;
    }

    private static (uint LowerBound, uint? UpperBound, uint? PointCount) SummarizeEntryBounds(
        KvDeviceRangeSegment[] segments)
    {
        if (segments.Length == 0)
        {
            return (0, null, null);
        }

        var first = segments[0];
        var allSame = segments.Skip(1).All(segment =>
            segment.LowerBound == first.LowerBound &&
            segment.UpperBound == first.UpperBound &&
            segment.PointCount == first.PointCount);
        return allSame
            ? (first.LowerBound, first.UpperBound, first.PointCount)
            : (first.LowerBound, null, null);
    }

    private static KvDeviceRangeNotation EntryNotation(
        KvDeviceRangeNotation fallback,
        KvDeviceRangeSegment[] segments)
    {
        if (segments.Length == 0)
        {
            return fallback;
        }

        var first = segments[0];
        return segments.Skip(1).All(segment => segment.Notation == first.Notation)
            ? first.Notation
            : fallback;
    }

    private static (uint LowerBound, uint? UpperBound, uint? PointCount) ParseSegmentBounds(
        string segment,
        KvDeviceRangeNotation notation,
        string defaultDevice)
    {
        var parts = segment.Split('-', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new HostLinkProtocolError($"Invalid device range segment '{segment}': missing '-' separator.");
        }

        var lower = ParseSegmentNumber(parts[0], notation, defaultDevice)
            ?? throw new HostLinkProtocolError($"Invalid device range start '{parts[0]}' in segment '{segment}'.");
        var upper = ParseSegmentNumber(parts[1], notation, defaultDevice)
            ?? throw new HostLinkProtocolError($"Invalid device range end '{parts[1]}' in segment '{segment}'.");
        if (upper < lower || upper - lower == uint.MaxValue)
        {
            throw new HostLinkProtocolError($"Invalid device range bounds in segment '{segment}'.");
        }

        return (lower, upper, upper - lower + 1);
    }

    private static uint? ParseSegmentNumber(
        string text,
        KvDeviceRangeNotation notation,
        string defaultDevice)
    {
        var normalized = text.Trim();
        var trimmed = normalized.StartsWith(defaultDevice, StringComparison.Ordinal)
            ? normalized[defaultDevice.Length..]
            : normalized;
        trimmed = TrimLeadingAsciiLetters(trimmed);
        if (trimmed.Length == 0)
        {
            return null;
        }

        if (defaultDevice is "X" or "Y")
        {
            return ParseXymSegmentNumber(trimmed);
        }

        return uint.TryParse(
            trimmed,
            notation == KvDeviceRangeNotation.Hexadecimal ? NumberStyles.HexNumber : NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : null;
    }

    private static uint? ParseXymSegmentNumber(string text)
    {
        var bankText = text.Length == 1 ? string.Empty : text[..^1];
        if (bankText.Any(character => character is < '0' or > '9'))
        {
            return null;
        }

        var bitText = text[^1..];
        if (!uint.TryParse(bitText, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bit))
        {
            return null;
        }

        var bank = bankText.Length == 0
            ? 0
            : uint.Parse(bankText, NumberStyles.Integer, CultureInfo.InvariantCulture);
        return checked((bank * 16) + bit);
    }

    private static string TrimLeadingAsciiLetters(string value)
    {
        var index = 0;
        while (index < value.Length && char.IsAsciiLetter(value[index]))
        {
            index++;
        }

        return value[index..];
    }

    private static (KvDeviceRangeCategory Category, bool IsBitDevice) DeviceMetadata(string deviceType)
    {
        if (deviceType == "Z")
        {
            return (KvDeviceRangeCategory.Index, false);
        }

        if (deviceType == "ZF")
        {
            return (KvDeviceRangeCategory.FileRegister, false);
        }

        if (deviceType is "T" or "C" or "AT" or "CTH" or "CTC")
        {
            return (KvDeviceRangeCategory.TimerCounter, false);
        }

        if (IsDirectBitDeviceType(deviceType))
        {
            return (KvDeviceRangeCategory.Bit, true);
        }

        return KvHostLinkModels.DefaultFormatByDeviceType.TryGetValue(deviceType, out var format) && format == string.Empty
            ? (KvDeviceRangeCategory.Bit, true)
            : (KvDeviceRangeCategory.Word, false);
    }

    private static bool IsDirectBitDeviceType(string deviceType)
    {
        return deviceType is "R" or "B" or "MR" or "LR" or "CR" or "VB" or "X" or "Y" or "M" or "L";
    }

    private static KvDeviceRangeNotation NotationForDevice(
        KvDeviceRangeNotation fallback,
        string deviceType)
    {
        return deviceType is "B" or "W" or "VB" or "X" or "Y"
            ? KvDeviceRangeNotation.Hexadecimal
            : fallback;
    }

    private static RangeProfile RangeProfileForPlcProfile(RangeTable table, string plcProfile)
    {
        var normalized = NormalizePlcProfile(plcProfile);
        var direct = table.Profiles.FirstOrDefault(profile =>
            string.Equals(profile.PlcProfile, normalized, StringComparison.Ordinal));
        if (direct is not null)
        {
            return direct;
        }

        var supported = string.Join(", ", AvailablePlcProfiles());
        throw new HostLinkProtocolError(
            $"Unsupported PLC profile '{plcProfile}'. Supported PLC profiles: {supported}.");
    }

    private static string NormalizePlcProfile(string text)
    {
        return text.Trim().TrimEnd('\0');
    }

    private sealed record RangeTable(List<RangeProfile> Profiles, List<RangeRow> Rows);

    private sealed record RangeProfile(string DisplayName, string PlcProfile);

    private sealed record RangeRow(
        string DeviceType,
        KvDeviceRangeNotation Notation,
        IReadOnlyList<string> Ranges);
}
