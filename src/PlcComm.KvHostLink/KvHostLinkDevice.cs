using System.Text.RegularExpressions;

namespace PlcComm.KvHostLink;

public record KvDeviceAddress(string DeviceType, int Number, string Suffix = "")
{
    public string ToText()
    {
        if (!KvHostLinkModels.DeviceRanges.TryGetValue(DeviceType, out var range))
            throw new HostLinkProtocolException($"Unsupported device type: {DeviceType}");

        string numberStr = range.Base == 16 ? Number.ToString("X") : Number.ToString();
        return $"{DeviceType}{numberStr}{Suffix}";
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
            throw new HostLinkProtocolException($"Unsupported data format suffix: {suffix}");
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
            throw new HostLinkProtocolException(
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
            throw new HostLinkProtocolException(
                $"Unknown device type '{deviceType}' in '{text}'. " +
                $"Valid types: {validTypes}.");
        }

        try
        {
            int number = Convert.ToInt32(numberText, range.Base);
            if (number < range.Lo || number > range.Hi)
                throw new HostLinkProtocolException($"Device number out of range: {deviceType}{numberText} (allowed: {range.Lo}..{range.Hi})");

            return new KvDeviceAddress(deviceType, number, suffix);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            throw new HostLinkProtocolException($"Invalid device number for {deviceType}: {numberText}", ex);
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

    public static void ValidateDeviceType(string command, string deviceType, HashSet<string> allowedTypes)
    {
        if (!allowedTypes.Contains(deviceType))
        {
            var supported = string.Join(", ", allowedTypes.OrderBy(x => x));
            throw new HostLinkProtocolException(
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
            throw new HostLinkProtocolException(
                $"Count {count} is out of range for device type '{deviceType}' with format '{effectiveFormat}' " +
                $"(allowed: {lo}..{hi}).");
    }
}
