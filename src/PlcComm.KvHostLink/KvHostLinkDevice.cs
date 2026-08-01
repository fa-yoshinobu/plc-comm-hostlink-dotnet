using System.Globalization;
using System.Text.RegularExpressions;

namespace PlcComm.KvHostLink;

public record KvDeviceAddress(string DeviceType, int Number, string Suffix = "")
{
    public string ToText()
    {
        if (!KvHostLinkModels.DeviceNumberBases.TryGetValue(DeviceType, out var numberBase))
            throw new HostLinkProtocolError($"Unsupported device type: {DeviceType}");

        string numberStr = UsesBitBankAddress(DeviceType)
            ? FormatBitBankNumber(Number)
            : UsesXymBitAddress(DeviceType) ? FormatXymBitNumber(Number)
            : numberBase == 16 ? Number.ToString("X", CultureInfo.InvariantCulture) : Number.ToString(CultureInfo.InvariantCulture);
        return $"{DeviceType}{numberStr}{Suffix}";
    }

    private static bool UsesBitBankAddress(string deviceType) =>
        deviceType is "R" or "MR" or "LR" or "CR";

    private static bool UsesXymBitAddress(string deviceType) =>
        deviceType is "X" or "Y";

    private static string FormatBitBankNumber(int number)
    {
        int bank = number / 100;
        int bit = number % 100;
        return $"{bank}{bit:D2}";
    }

    private static string FormatXymBitNumber(int number)
    {
        int bank = number / 16;
        int bit = number % 16;
        return $"{bank}{bit:X}";
    }
}

public static class KvHostLinkDevice
{
    private static readonly HashSet<string> SupportedFormats = new() { "", ".U", ".S", ".D", ".L", ".H" };
    private static readonly Regex DeviceRegex;

    static KvHostLinkDevice()
    {
        var types = KvHostLinkModels.DeviceNumberBases.Keys.OrderByDescending(k => k.Length);
        var pattern = $"^(?<type>{string.Join("|", types)})(?<number>[0-9A-F]+)(?<suffix>\\.[USDLH])?$";
        DeviceRegex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    }

    public static string NormalizeSuffix(string? suffix)
    {
        if (string.IsNullOrEmpty(suffix)) return "";
        var s = suffix.ToUpperInvariant();
        if (!s.StartsWith('.')) s = "." + s;
        if (!SupportedFormats.Contains(s))
            throw new HostLinkProtocolError($"Unsupported data format suffix: {suffix}");
        return s;
    }

    /// <summary>Parses a Host Link device token with an explicit device type.</summary>
    public static KvDeviceAddress ParseDevice(string text) => ParseDeviceCore(text);

    private static KvDeviceAddress ParseDeviceCore(string text)
    {
        var raw = text.Trim().ToUpperInvariant();
        var match = DeviceRegex.Match(raw);

        if (!match.Success)
        {
            var validTypes = string.Join(", ", KvHostLinkModels.DeviceNumberBases.Keys.OrderBy(k => k));
            throw new HostLinkProtocolError(
                $"Invalid device string '{text}'. " +
                $"Valid device types: {validTypes}.");
        }

        string deviceType = match.Groups["type"].Value;
        string numberText = match.Groups["number"].Value;
        string suffix = NormalizeSuffix(match.Groups["suffix"].Value);

        if (!KvHostLinkModels.DeviceNumberBases.TryGetValue(deviceType, out var numberBase))
        {
            var validTypes = string.Join(", ", KvHostLinkModels.DeviceNumberBases.Keys.OrderBy(k => k));
            throw new HostLinkProtocolError(
                $"Unknown device type '{deviceType}' in '{text}'. " +
                $"Valid types: {validTypes}.");
        }

        try
        {
            int number = UsesXymBitAddress(deviceType)
                ? ParseXymBitNumber(deviceType, numberText)
                : Convert.ToInt32(numberText, numberBase);
            // PROFILE_RANGE_NOT_A_TRANSPORT_GUARD: profile/device catalog upper bounds are
            // application metadata, not a reason for the communication library to block a send.
            // Keep only syntax, supported-family, non-negative, and wire/text representation checks here.
            if (number < 0)
                throw new HostLinkProtocolError($"Device number must not be negative: {deviceType}{numberText}");
            if (UsesBitBankAddress(deviceType) && number % 100 > 15)
                throw new HostLinkProtocolError($"Invalid bit-bank device number: {deviceType}{numberText} (lower two digits must be 00..15)");

            return new KvDeviceAddress(deviceType, number, suffix);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new HostLinkProtocolError($"Invalid device number for {deviceType}: {numberText}", ex);
        }
    }

    internal static string ResolveEffectiveFormat(string deviceType, string suffix)
    {
        if (!string.IsNullOrEmpty(suffix)) return suffix;
        return KvHostLinkModels.DefaultFormatByDeviceType.GetValueOrDefault(deviceType, "");
    }

    internal static void ValidateFloat32DeviceType(string deviceType, string? address = null)
    {
        if (KvHostLinkModels.Float32DeviceTypes.Contains(deviceType))
            return;

        string subject = string.IsNullOrEmpty(address)
            ? $"Device family '{deviceType}'"
            : $"Address '{address}'";
        throw new HostLinkProtocolError(
            $"{subject} uses Float32 on ineligible device family '{deviceType}'; " +
            "direct bit and special-response families are excluded, and Float32 requires " +
            "an ordinary one-word family with consecutive two-word access.");
    }

    internal static int ReadResponseTokenCount(string deviceType, string dataFormat)
    {
        if (deviceType is "T" or "C") return 3;
        if (!KvHostLinkModels.DirectBitDeviceTypes.Contains(deviceType)) return 1;
        return dataFormat switch
        {
            ".U" or ".S" or ".H" => 16,
            ".D" or ".L" => 32,
            _ => 1,
        };
    }

    public static string RequireExplicitFormat(KvDeviceAddress address, string? dataFormat)
    {
        if (!string.IsNullOrEmpty(address.Suffix))
            throw new HostLinkProtocolError(
                $"Device '{address.ToText()}' must not include a data-format suffix. " +
                "Pass the base device and dataFormat separately.");

        if (dataFormat is null && KvHostLinkModels.DirectBitDeviceTypes.Contains(address.DeviceType))
            return "";

        if (string.IsNullOrWhiteSpace(dataFormat))
            throw new HostLinkProtocolError($"Data format is required for device '{address.ToText()}'.");

        string suffix = NormalizeSuffix(dataFormat);
        if (string.IsNullOrEmpty(suffix))
            throw new HostLinkProtocolError($"Data format is required for device '{address.ToText()}'.");
        return suffix;
    }

    internal static KvDeviceAddress RequireBaseDevice(string device)
    {
        KvDeviceAddress address = ParseDevice(device);
        if (!string.IsNullOrEmpty(address.Suffix))
            throw new HostLinkProtocolError(
                $"Device '{device}' must not include a data-format suffix. " +
                "Pass the base device and dataFormat separately.");
        return address;
    }

    internal static bool UsesBitBankAddress(string deviceType) =>
        deviceType is "R" or "MR" or "LR" or "CR";

    private static bool UsesXymBitAddress(string deviceType) =>
        deviceType is "X" or "Y";

    private static int ParseXymBitNumber(string deviceType, string numberText)
    {
        var bankText = numberText.Length == 1 ? "0" : numberText[..^1];
        if (bankText.Any(character => character is < '0' or > '9'))
            throw new HostLinkProtocolError($"Invalid X/Y device number: {deviceType}{numberText} (bank digits must be decimal and bit digit must be 0..F)");

        var bank = int.Parse(bankText, NumberStyles.None, CultureInfo.InvariantCulture);
        var bit = int.Parse(numberText[^1..], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return checked(bank * 16 + bit);
    }

    private static string FormatDeviceNumber(string deviceType, int number)
    {
        if (UsesBitBankAddress(deviceType))
            return FormatBitBankNumber(number);
        if (UsesXymBitAddress(deviceType))
            return FormatXymBitNumber(number);
        if (!KvHostLinkModels.DeviceNumberBases.TryGetValue(deviceType, out var numberBase))
            return number.ToString(CultureInfo.InvariantCulture);

        return numberBase == 16
            ? number.ToString("X", CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);
    }

    private static string FormatBitBankNumber(int number)
    {
        int bank = number / 100;
        int bit = number % 100;
        return $"{bank}{bit:D2}";
    }

    internal static int BitBankLogicalNumber(int number)
    {
        checked
        {
            return (number / 100) * 16 + (number % 100);
        }
    }

    internal static int BitBankNumberFromLogical(int number)
    {
        checked
        {
            return (number / 16) * 100 + (number % 16);
        }
    }

    private static string FormatXymBitNumber(int number)
    {
        int bank = number / 16;
        int bit = number % 16;
        return $"{bank}{bit:X}";
    }

    public static void ValidateDeviceType(string command, string deviceType, HashSet<string> allowedTypes)
    {
        if (!allowedTypes.Contains(deviceType))
        {
            var supported = string.Join(", ", allowedTypes.OrderBy(x => x));
            throw new HostLinkProtocolError(
                $"Command '{command}' does not support device type '{deviceType}'. " +
                $"Supported types: {supported}.");
        }
    }

    public static void ValidateDeviceCount(string deviceType, string effectiveFormat, int count)
    {
        bool is32Bit = effectiveFormat is ".D" or ".L";
        int lo = 1, hi;

        switch (deviceType)
        {
            case "TM":
                hi = is32Bit ? 256 : 512;
                break;
            case "Z":
                hi = 12;
                break;
            case "AT":
                hi = 8;
                break;
            case "T":
            case "TC":
            case "TS":
            case "C":
            case "CC":
            case "CS":
            case "CTH":
            case "CTC":
                hi = 120;
                break;
            default:
                hi = is32Bit ? 500 : 1000;
                break;
        }

        if (count < lo || count > hi)
            throw new HostLinkProtocolError(
                $"Count {count} is out of range for device type '{deviceType}' with format '{effectiveFormat}' " +
                $"(allowed: {lo}..{hi}).");
    }

    public static void ValidateDeviceSpan(string deviceType, int startNumber, string effectiveFormat, int count = 1)
    {
        if (!KvHostLinkModels.DeviceNumberBases.ContainsKey(deviceType))
            throw new HostLinkProtocolError($"Unsupported device type: {deviceType}");
        if (count < 1)
            throw new HostLinkProtocolError($"count out of range: {count} (allowed: 1..)");

        int deviceWidth = DeviceSpanWidth(deviceType, effectiveFormat);
        int startSpanNumber = UsesBitBankAddress(deviceType)
            ? BitBankLogicalNumber(startNumber)
            : startNumber;
        try
        {
            _ = checked(startSpanNumber + (count * deviceWidth) - 1);
        }
        catch (OverflowException ex)
        {
            throw new HostLinkProtocolError("Device span exceeds the supported numeric representation.", ex);
        }
    }

    private static int DeviceSpanWidth(string deviceType, string effectiveFormat)
    {
        if (KvHostLinkModels.DirectBitDeviceTypes.Contains(deviceType))
        {
            return effectiveFormat switch
            {
                ".U" or ".S" or ".H" => 16,
                ".D" or ".L" => 32,
                _ => 1
            };
        }

        return effectiveFormat is ".D" or ".L" && !KvHostLinkModels.Native32BitDeviceTypes.Contains(deviceType)
            ? 2
            : 1;
    }

    public static void ValidateExpansionBufferCount(string effectiveFormat, int count)
    {
        bool is32Bit = effectiveFormat is ".D" or ".L";
        int lo = 1;
        int hi = is32Bit ? 500 : 1000;

        if (count < lo || count > hi)
            throw new HostLinkProtocolError(
                $"Count {count} is out of range for expansion buffer format '{effectiveFormat}' " +
                $"(allowed: {lo}..{hi}).");
    }

    public static void ValidateExpansionBufferSpan(int address, string effectiveFormat, int count)
    {
        if (count < 1)
            throw new HostLinkProtocolError($"count out of range: {count} (allowed: 1..)");

        bool is32Bit = effectiveFormat is ".D" or ".L";
        int endAddress = address + (count * (is32Bit ? 2 : 1)) - 1;
        if (address < 0 || address > 59999 || endAddress > 59999)
            throw new HostLinkProtocolError(
                $"Expansion buffer span out of range: {address}..{endAddress} " +
                $"with format '{effectiveFormat}'");
    }
}
