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
    FileRefresh,
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
    string Model,
    string ModelCode,
    bool HasModelCode,
    string RequestedModel,
    string ResolvedModel,
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
    private const string RangeCsvData = """
DeviceType,Base,KV-NANO,KV-NANO(XYM),KV-3000/5000,KV-3000/5000(XYM),KV-7000,KV-7000(XYM),KV-8000,KV-8000(XYM),KV-X500,KV-X500(XYM)
R,10,R00000-R59915,"X0-599F,Y0-599F",R00000-R99915,"X0-999F,Y0-999F",R00000-R199915,"X0-1999F,Y0-1999F",R00000-R199915,"X0-1999F,Y0-1999F",R00000-R199915,"X0-1999F,Y0-1999F"
B,16,B0000-B1FFF,B0000-B1FFF,B0000-B3FFF,B0000-B3FFF,B0000-B7FFF,B0000-B7FFF,B0000-B7FFF,B0000-B7FFF,B0000-B7FFF,B0000-B7FFF
MR,10,MR00000-MR59915,M0-9599,MR00000-MR99915,M0-15999,MR000000-MR399915,M000000-M63999,MR000000-MR399915,M000000-M63999,MR000000-MR399915,M000000-M63999
LR,10,LR00000-LR19915,L0-3199,LR00000-LR99915,L0-15999,LR00000-LR99915,L00000-L15999,LR00000-LR99915,L00000-L15999,LR00000-LR99915,L00000-L15999
CR,10,CR0000-CR8915,CR0000-CR8915,CR0000-CR3915,CR0000-CR3915,CR0000-CR7915,CR0000-CR7915,CR0000-CR7915,CR0000-CR7915,CR0000-CR7915,CR0000-CR7915
CM,10,CM0000-CM8999,CM0000-CM8999,CM0000-CM5999,CM0000-CM5999,CM0000-CM5999,CM0000-CM5999,CM0000-CM7599,CM0000-CM7599,CM0000-CM7599,CM0000-CM7599
T,10,T0000-T0511,T0000-T0511,T0000-T3999,T0000-T3999,T0000-T3999,T0000-T3999,T0000-T3999,T0000-T3999,T0000-T3999,T0000-T3999
C,10,C0000-C0255,C0000-C0255,C0000-C3999,C0000-C3999,C0000-C3999,C0000-C3999,C0000-C3999,C0000-C3999,C0000-C3999,C0000-C3999
DM,10,DM00000-DM32767,D0-32767,DM00000-DM65534,D0-65534,DM00000-DM65534,D00000-D65534,DM00000-DM65534,D00000-D65534,DM00000-DM65534,D00000-D65534
EM,10,-,-,EM00000-EM65534,E0-65534,EM00000-EM65534,E00000-E65534,EM00000-EM65534,E00000-E65534,EM00000-EM65534,E00000-E65534
FM,10,-,-,FM00000-FM32767,F0-32767,FM00000-FM32767,F00000-F32767,FM00000-FM32767,F00000-F32767,FM00000-FM32767,F00000-F32767
ZF,10,-,-,ZF000000-ZF131071,ZF000000-ZF131071,ZF000000-ZF524287,ZF000000-ZF524287,ZF000000-ZF524287,ZF000000-ZF524287,ZF000000-ZF524287,ZF000000-ZF524287
W,16,W0000-W3FFF,W0000-W3FFF,W0000-W3FFF,W0000-W3FFF,W0000-W7FFF,W0000-W7FFF,W0000-W7FFF,W0000-W7FFF,W0000-W7FFF,W0000-W7FFF
TM,10,TM000-TM511,TM000-TM511,TM000-TM511,TM000-TM511,TM000-TM511,TM000-TM511,TM000-TM511,TM000-TM511,TM000-TM511,TM000-TM511
VM,10,VM0-9499,VM0-9499,VM0-49999,VM0-49999,VM0-63999,VM0-63999,VM0-589823,VM0-589823,-,-
VB,16,VB0-1FFF,VB0-1FFF,VB0-3FFF,VB0-3FFF,VB0-F9FF,VB0-F9FF,VB0-F9FF,VB0-F9FF,-,-
Z,10,Z1-12,Z1-12,Z1-12,Z1-12,Z1-12,Z1-12,Z1-12,Z1-12,-,-
CTH,10,CTH0-3,CTH0-3,CTH0-1,CTH0-3,-,-,-,-,-,-
CTC,10,CTC0-7,CTC0-7,CTC0-3,CTC0-3,-,-,-,-,-,-
AT,10,-,-,AT0-7,AT0-7,AT0-7,AT0-7,AT0-7,AT0-7,-,-
""";

    private static readonly Lazy<RangeTable> ParsedRangeTable = new(ParseRangeTable);

    public static IReadOnlyList<string> AvailableDeviceRangeModels()
    {
        return ParsedRangeTable.Value.ModelHeaders.ToArray();
    }

    public static KvDeviceRangeCatalog DeviceRangeCatalogForModel(string model)
    {
        return DeviceRangeCatalogForModel(model, modelCode: null);
    }

    public static KvDeviceRangeCatalog DeviceRangeCatalogForModel(string model, string? modelCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var requestedModel = model.Trim();
        var table = ParsedRangeTable.Value;
        var resolvedModel = ResolveModelColumn(table, requestedModel);
        var modelIndex = table.ModelHeaders.FindIndex(header =>
            string.Equals(header, resolvedModel, StringComparison.Ordinal));
        if (modelIndex < 0)
        {
            throw new HostLinkProtocolError(
                $"Resolved model column '{resolvedModel}' was not found in the embedded device range table.");
        }

        var entries = table.Rows
            .Select(row => BuildEntry(row, modelIndex, resolvedModel))
            .ToArray();

        return new KvDeviceRangeCatalog(
            resolvedModel,
            modelCode ?? string.Empty,
            modelCode is not null,
            requestedModel,
            resolvedModel,
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
            return (0, null, null);
        }

        var lower = ParseSegmentNumber(parts[0], notation, defaultDevice);
        var upper = ParseSegmentNumber(parts[1], notation, defaultDevice);
        var pointCount = lower.HasValue && upper.HasValue && upper.Value >= lower.Value
            ? upper.Value - lower.Value + 1
            : (uint?)null;

        return (lower ?? 0, upper, pointCount);
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

        return notation == KvDeviceRangeNotation.Hexadecimal
            ? uint.Parse(trimmed, NumberStyles.HexNumber, CultureInfo.InvariantCulture)
            : uint.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture);
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
            return (KvDeviceRangeCategory.FileRefresh, false);
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

    private static string ResolveModelColumn(RangeTable table, string requestedModel)
    {
        var normalized = NormalizeModelKey(requestedModel);
        var direct = DirectModelMatch(table, normalized);
        if (direct is not null)
        {
            return direct;
        }

        var wantsXym = normalized.EndsWith("(XYM)", StringComparison.Ordinal);
        var baseModel = wantsXym ? normalized[..^"(XYM)".Length] : normalized;
        var resolvedFamily = baseModel switch
        {
            var value when value.StartsWith("KV-NANO", StringComparison.Ordinal) ||
                value.StartsWith("KV-N", StringComparison.Ordinal) => "KV-NANO",
            var value when value.StartsWith("KV-3000", StringComparison.Ordinal) ||
                value.StartsWith("KV-5000", StringComparison.Ordinal) ||
                value.StartsWith("KV-5500", StringComparison.Ordinal) => "KV-3000/5000",
            var value when value.StartsWith("KV-7000", StringComparison.Ordinal) ||
                value.StartsWith("KV-7300", StringComparison.Ordinal) ||
                value.StartsWith("KV-7500", StringComparison.Ordinal) => "KV-7000",
            var value when value.StartsWith("KV-8000", StringComparison.Ordinal) => "KV-8000",
            var value when value.StartsWith("KV-X5", StringComparison.Ordinal) ||
                value.StartsWith("KV-X3", StringComparison.Ordinal) => "KV-X500",
            _ => null,
        };

        if (resolvedFamily is null)
        {
            var supported = string.Join(", ", table.ModelHeaders);
            throw new HostLinkProtocolError(
                $"Unsupported model '{requestedModel}'. Supported range models: {supported}.");
        }

        var resolvedKey = wantsXym ? $"{resolvedFamily}(XYM)" : resolvedFamily;
        return DirectModelMatch(table, resolvedKey) ??
            throw new HostLinkProtocolError(
                $"Resolved model '{resolvedKey}' was not found in the embedded device range table.");
    }

    private static string? DirectModelMatch(RangeTable table, string normalized)
    {
        return table.ModelHeaders.FirstOrDefault(header => NormalizeModelKey(header) == normalized);
    }

    private static string NormalizeModelKey(string text)
    {
        var builder = new StringBuilder();
        foreach (var ch in text.Trim().TrimEnd('\0'))
        {
            if (!char.IsWhiteSpace(ch))
            {
                builder.Append(char.ToUpperInvariant(ch));
            }
        }

        return builder.ToString();
    }

    private static RangeTable ParseRangeTable()
    {
        var lines = RangeCsvData
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0)
        {
            throw new HostLinkProtocolError("Embedded device range table is empty.");
        }

        var headers = ParseCsvLine(lines[0]);
        if (headers.Count < 3)
        {
            throw new HostLinkProtocolError(
                "Embedded device range table must contain at least DeviceType, Base, and one model column.");
        }

        var modelHeaders = headers.Skip(2).Select(static header => header.Trim()).ToList();
        var rows = new List<RangeRow>(lines.Length - 1);
        for (var i = 1; i < lines.Length; i++)
        {
            var fields = ParseCsvLine(lines[i]);
            if (fields.Count != headers.Count)
            {
                throw new HostLinkProtocolError(
                    $"Embedded device range row has {fields.Count} columns but {headers.Count} were expected: {lines[i]}");
            }

            rows.Add(new RangeRow(
                fields[0].Trim(),
                NotationFromBase(fields[1]),
                fields.Skip(2).Select(static value => value.Trim()).ToArray()));
        }

        return new RangeTable(modelHeaders, rows);
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new StringBuilder();
        var inQuote = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuote && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuote = !inQuote;
                }
            }
            else if (ch == ',' && !inQuote)
            {
                fields.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(ch);
            }
        }

        if (inQuote)
        {
            throw new HostLinkProtocolError($"Embedded device range table contains an unterminated quoted field: {line}");
        }

        fields.Add(current.ToString());
        return fields;
    }

    private static KvDeviceRangeNotation NotationFromBase(string baseText)
    {
        var normalized = baseText.Trim();
        if (normalized.StartsWith("10", StringComparison.Ordinal))
        {
            return KvDeviceRangeNotation.Decimal;
        }

        if (normalized.StartsWith("16", StringComparison.Ordinal))
        {
            return KvDeviceRangeNotation.Hexadecimal;
        }

        throw new HostLinkProtocolError($"Unsupported base cell '{baseText}' in the embedded device range table.");
    }

    private sealed record RangeTable(List<string> ModelHeaders, List<RangeRow> Rows);

    private sealed record RangeRow(
        string DeviceType,
        KvDeviceRangeNotation Notation,
        IReadOnlyList<string> Ranges);
}
