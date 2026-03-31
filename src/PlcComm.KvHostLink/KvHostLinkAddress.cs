using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace PlcComm.KvHostLink;

/// <summary>
/// A normalized logical Host Link address used by the high-level helper layer.
/// </summary>
/// <param name="BaseAddress">Base word device address without a logical suffix.</param>
/// <param name="DataType">Logical data type code such as <c>U</c>, <c>S</c>, <c>D</c>, <c>L</c>, or <c>F</c>.</param>
/// <param name="BitIndex">Bit index inside the base word when the logical address targets a bit-in-word.</param>
public readonly record struct KvLogicalAddress(KvDeviceAddress BaseAddress, string DataType, int? BitIndex)
{
    /// <summary>Gets a value indicating whether this logical address targets a bit inside a word.</summary>
    public bool IsBitInWord => BitIndex.HasValue;

    /// <summary>Formats the logical address using the public helper contract.</summary>
    public string ToText()
    {
        string baseText = KvHostLinkAddress.Format(BaseAddress with { Suffix = string.Empty });
        if (IsBitInWord)
            return $"{baseText}.{BitIndex.GetValueOrDefault().ToString("X", CultureInfo.InvariantCulture)}";

        return DataType == "U"
            ? baseText
            : $"{baseText}:{DataType}";
    }
}

/// <summary>
/// Public address helpers for Host Link device strings and logical helper addresses.
/// </summary>
public static class KvHostLinkAddress
{
    /// <summary>Parses a base device address.</summary>
    public static KvDeviceAddress Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        return KvHostLinkDevice.ParseDevice(text.Trim());
    }

    /// <summary>Attempts to parse a base device address.</summary>
    public static bool TryParse(string text, [NotNullWhen(true)] out KvDeviceAddress? address)
    {
        try
        {
            address = Parse(text);
            return true;
        }
        catch (Exception ex) when (ex is HostLinkProtocolError or ArgumentException)
        {
            address = default;
            return false;
        }
    }

    /// <summary>Formats a base device address to canonical text.</summary>
    public static string Format(KvDeviceAddress address) => address.ToText();

    /// <summary>
    /// Normalizes either a base device address or a logical helper address.
    /// </summary>
    public static string Normalize(string text)
    {
        if (TryParse(text, out var address))
            return Format(address);

        return NormalizeLogical(text);
    }

    /// <summary>Parses a logical helper address such as <c>DM100:F</c> or <c>DM100.A</c>.</summary>
    public static KvLogicalAddress ParseLogical(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var raw = text.Trim();
        int colonIndex = raw.IndexOf(':');
        if (colonIndex >= 0)
        {
            string baseText = raw[..colonIndex];
            string dtype = NormalizeDType(raw[(colonIndex + 1)..]);
            return new KvLogicalAddress(Parse(baseText) with { Suffix = string.Empty }, dtype, null);
        }

        int dotIndex = raw.LastIndexOf('.');
        if (dotIndex > 0 && TryParseBitIndex(raw[(dotIndex + 1)..], out var bitIndex))
        {
            string baseText = raw[..dotIndex];
            return new KvLogicalAddress(Parse(baseText) with { Suffix = string.Empty }, "BIT_IN_WORD", bitIndex);
        }

        return new KvLogicalAddress(Parse(raw) with { Suffix = string.Empty }, "U", null);
    }

    /// <summary>Attempts to parse a logical helper address.</summary>
    public static bool TryParseLogical(string text, out KvLogicalAddress address)
    {
        try
        {
            address = ParseLogical(text);
            return true;
        }
        catch (Exception ex) when (ex is HostLinkProtocolError or ArgumentException)
        {
            address = default;
            return false;
        }
    }

    /// <summary>Normalizes a logical helper address to canonical text.</summary>
    public static string NormalizeLogical(string text) => ParseLogical(text).ToText();

    private static string NormalizeDType(string text)
    {
        var dtype = text.Trim().TrimStart('.').ToUpperInvariant();
        return dtype switch
        {
            "U" => "U",
            "S" => "S",
            "D" => "D",
            "L" => "L",
            "F" => "F",
            _ => throw new HostLinkProtocolError($"Unsupported logical data type '{text}'."),
        };
    }

    private static bool TryParseBitIndex(string text, out int bitIndex)
    {
        if (int.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out bitIndex) &&
            bitIndex is >= 0 and <= 15)
        {
            return true;
        }

        bitIndex = default;
        return false;
    }
}
