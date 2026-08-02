namespace PlcComm.KvHostLink;

/// <summary>
/// Immutable lifetime traffic counters for one Host Link client. TCP receive bytes count the body
/// and first CR/LF terminator; UDP receive bytes count the complete datagram.
/// </summary>
public readonly record struct HostLinkTrafficStats(ulong RequestCount, ulong TxBytes, ulong RxBytes);

/// <summary>
/// Information about a PLC model.
/// </summary>
public record KvModelInfo(string Code, string Model);

/// <summary>One base device and optional data format used by word monitoring.</summary>
/// <param name="Device">The base device without a data-format suffix.</param>
/// <param name="DataFormat">
/// The explicit numeric data format, or <see langword="null"/> only for a direct-bit device whose
/// bare MWS/MWR representation is an unsigned packed 16-bit word.
/// </param>
public sealed record KvMonitorWordTarget(string Device, string? DataFormat = null);

internal static class KvHostLinkModels
{
    public static readonly Dictionary<string, string> ModelCodes = new()
    {
        { "134", "KV-N24nn" },
        { "133", "KV-N40nn" },
        { "132", "KV-N60nn" },
        { "128", "KV-NC32T" },
        { "63", "KV-X550" },
        { "61", "KV-X530" },
        { "60", "KV-X520" },
        { "62", "KV-X500" },
        { "59", "KV-X310" },
        { "58", "KV-8000A" },
        { "57", "KV-8000" },
        { "55", "KV-7500" },
        { "54", "KV-7300" },
        { "53", "KV-5500" },
        { "52", "KV-5000" },
        { "51", "KV-3000" },
        { "50", "KV-1000" },
        { "49", "KV-700 (With expansion memory)" },
        { "48", "KV-700 (No expansion memory)" }
    };

    // Syntax-level number bases only. PLC/profile address ranges belong to the public
    // device-range catalog and must never be reused as transport send guards.
    public static readonly Dictionary<string, int> DeviceNumberBases = new()
    {
        { "R", 10 }, { "B", 16 }, { "MR", 10 }, { "LR", 10 }, { "CR", 10 }, { "VB", 16 },
        { "DM", 10 }, { "EM", 10 }, { "FM", 10 }, { "ZF", 10 }, { "W", 16 }, { "TM", 10 },
        { "Z", 10 }, { "T", 10 }, { "TC", 10 }, { "TS", 10 }, { "C", 10 }, { "CC", 10 },
        { "CS", 10 }, { "CTH", 10 }, { "CTC", 10 }, { "AT", 10 }, { "CM", 10 }, { "VM", 10 },
        { "X", 10 }, { "Y", 10 }, { "M", 10 }, { "L", 10 }, { "D", 10 }, { "E", 10 }, { "F", 10 }
    };

    public static readonly HashSet<string> Native32BitDeviceTypes = new() { "T", "TC", "TS", "C", "CC", "CS", "CTH", "CTC", "Z", "AT" };
    public static readonly HashSet<string> DirectBitDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "VB", "X", "Y", "M", "L" };
    public static readonly HashSet<string> ForceDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "T", "C", "CTH", "CTC", "VB", "X", "Y", "M", "L" };
    public static readonly HashSet<string> ForceConsecutiveDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "VB", "X", "Y", "M", "L" };
    public static readonly HashSet<string> MbsDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "T", "C", "CTH", "CTC", "VB", "X", "Y", "M", "L" };
    public static readonly HashSet<string> MwsDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "VB", "X", "Y", "DM", "EM", "FM", "D", "E", "F", "W", "TM", "Z", "TC", "TS", "CC", "CS", "CM", "VM" };
    public static readonly HashSet<string> RdcDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "DM", "EM", "FM", "ZF", "W", "TM", "Z", "T", "C", "CTH", "CTC", "CM", "X", "Y", "M", "L", "D", "E", "F" };
    public static readonly HashSet<string> WrDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "VB", "DM", "EM", "FM", "ZF", "W", "TM", "Z", "T", "TC", "TS", "C", "CC", "CS", "CTH", "CTC", "CM", "VM", "X", "Y", "M", "L", "D", "E", "F" };
    public static readonly HashSet<string> WsDeviceTypes = new() { "T", "C", "CTH", "CTC" };

    public static readonly Dictionary<string, string> DefaultFormatByDeviceType = new()
    {
        { "R", "" }, { "B", "" }, { "MR", "" }, { "LR", "" }, { "CR", "" }, { "VB", "" },
        { "DM", ".U" }, { "EM", ".U" }, { "FM", ".U" }, { "ZF", ".U" }, { "W", ".U" }, { "TM", ".U" },
        { "Z", ".U" }, { "AT", ".D" }, { "CM", ".U" }, { "VM", ".U" }, { "T", ".D" }, { "TC", ".D" },
        { "TS", ".D" }, { "C", ".D" }, { "CC", ".D" }, { "CS", ".D" }, { "CTH", ".D" }, { "CTC", ".D" },
        { "X", "" }, { "Y", "" }, { "M", "" }, { "L", "" }, { "D", ".U" }, { "E", ".U" }, { "F", ".U" }
    };

    public static readonly IReadOnlySet<string> Float32DeviceTypes =
        DefaultFormatByDeviceType
            .Where(static pair => pair.Value == ".U" && pair.Key != "Z")
            .Select(static pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);
}
