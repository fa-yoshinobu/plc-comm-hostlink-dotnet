using System.Globalization;
using System.Text.RegularExpressions;

namespace PlcComm.KvHostLink;

public record KvDeviceAddress(string DeviceType, int Number, string Suffix = "")
{
    public string ToText()
    {
        if (!KvHostLinkModels.DeviceRanges.TryGetValue(DeviceType, out var range))
            throw new HostLinkProtocolError($"Unsupported device type: {DeviceType}");

        string numberStr = UsesBitBankAddress(DeviceType)
            ? FormatBitBankNumber(Number)
            : UsesXymBitAddress(DeviceType) ? FormatXymBitNumber(Number)
            : range.Base == 16 ? Number.ToString("X", CultureInfo.InvariantCulture) : Number.ToString(CultureInfo.InvariantCulture);
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
        var types = KvHostLinkModels.DeviceRanges.Keys.OrderByDescending(k => k.Length);
        var pattern = $"^(?<type>{string.Join("|", types)})?(?<number>[0-9A-F]+)(?<suffix>\\.[USDLH])?$";
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

    public static KvDeviceAddress ParseDevice(string text, bool allowOmittedType = true)
    {
        var raw = text.Trim().ToUpperInvariant();
        var match = DeviceRegex.Match(raw);

        if (!match.Success)
        {
            if (allowOmittedType && int.TryParse(raw, out _))
                return ParseDevice("R" + raw, false);
            var validTypes = string.Join(", ", KvHostLinkModels.DeviceRanges.Keys.OrderBy(k => k));
            throw new HostLinkProtocolError(
                $"Invalid device string '{text}'. " +
                $"Valid device types: {validTypes}.");
        }

        string deviceType = match.Groups["type"].Value;
        if (string.IsNullOrEmpty(deviceType)) deviceType = "R";

        string numberText = match.Groups["number"].Value;
        string suffix = NormalizeSuffix(match.Groups["suffix"].Value);

        if (!KvHostLinkModels.DeviceRanges.TryGetValue(deviceType, out var range))
        {
            var validTypes = string.Join(", ", KvHostLinkModels.DeviceRanges.Keys.OrderBy(k => k));
            throw new HostLinkProtocolError(
                $"Unknown device type '{deviceType}' in '{text}'. " +
                $"Valid types: {validTypes}.");
        }

        try
        {
            int number = UsesXymBitAddress(deviceType)
                ? ParseXymBitNumber(deviceType, numberText)
                : Convert.ToInt32(numberText, range.Base);
            if (number < range.Lo || number > range.Hi)
                throw new HostLinkProtocolError($"Device number out of range: {deviceType}{numberText} (allowed: {FormatDeviceNumber(deviceType, range.Lo)}..{FormatDeviceNumber(deviceType, range.Hi)})");
            if (UsesBitBankAddress(deviceType) && number % 100 > 15)
                throw new HostLinkProtocolError($"Invalid bit-bank device number: {deviceType}{numberText} (lower two digits must be 00..15)");

            return new KvDeviceAddress(deviceType, number, suffix);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new HostLinkProtocolError($"Invalid device number for {deviceType}: {numberText}", ex);
        }
    }

    public static string ParseDeviceText(string text, string defaultSuffix = "")
    {
        var addr = ParseDevice(text);
        string suffix = !string.IsNullOrEmpty(defaultSuffix) ? NormalizeSuffix(defaultSuffix) : addr.Suffix;
        if (suffix != addr.Suffix)
            addr = addr with { Suffix = suffix };
        return addr.ToText();
    }

    public static string ResolveEffectiveFormat(string deviceType, string suffix)
    {
        if (!string.IsNullOrEmpty(suffix)) return suffix;
        return KvHostLinkModels.DefaultFormatByDeviceType.GetValueOrDefault(deviceType, "");
    }

    private static bool UsesBitBankAddress(string deviceType) =>
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
        if (!KvHostLinkModels.DeviceRanges.TryGetValue(deviceType, out var range))
            return number.ToString(CultureInfo.InvariantCulture);

        return range.Base == 16
            ? number.ToString("X", CultureInfo.InvariantCulture)
            : number.ToString(CultureInfo.InvariantCulture);
    }

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
        if (!KvHostLinkModels.DeviceRanges.TryGetValue(deviceType, out var range))
            throw new HostLinkProtocolError($"Unsupported device type: {deviceType}");
        if (count < 1)
            throw new HostLinkProtocolError($"count out of range: {count} (allowed: 1..)");

        bool is32Bit = effectiveFormat is ".D" or ".L";
        int endNumber = startNumber + (count * (is32Bit ? 2 : 1)) - 1;
        if (startNumber < range.Lo || startNumber > range.Hi || endNumber > range.Hi)
        {
            string startText = FormatDeviceNumber(deviceType, startNumber);
            string endText = FormatDeviceNumber(deviceType, endNumber);
            throw new HostLinkProtocolError(
                $"Device span out of range: {deviceType}{startText}..{deviceType}{endText} " +
                $"with format '{effectiveFormat}'");
        }
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
