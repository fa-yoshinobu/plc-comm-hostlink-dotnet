namespace PlcComm.KvHostLink;

/// <summary>
/// Information about a PLC model.
/// </summary>
public record KvModelInfo(string Code, string Model);

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

    public static readonly Dictionary<string, (int Lo, int Hi, int Base)> DeviceRanges = new()
    {
        { "R", (0, 199915, 10) },
        { "B", (0, 0x7FFF, 16) },
        { "MR", (0, 399915, 10) },
        { "LR", (0, 99915, 10) },
        { "CR", (0, 7915, 10) },
        { "VB", (0, 0xF9FF, 16) },
        { "DM", (0, 65534, 10) },
        { "EM", (0, 65534, 10) },
        { "FM", (0, 32767, 10) },
        { "ZF", (0, 524287, 10) },
        { "W", (0, 0x7FFF, 16) },
        { "TM", (0, 511, 10) },
        { "Z", (1, 12, 10) },
        { "T", (0, 3999, 10) },
        { "TC", (0, 3999, 10) },
        { "TS", (0, 3999, 10) },
        { "C", (0, 3999, 10) },
        { "CC", (0, 3999, 10) },
        { "CS", (0, 3999, 10) },
        { "AT", (0, 7, 10) },
        { "CM", (0, 7599, 10) },
        { "VM", (0, 589823, 10) },
        { "X", (0, 1999 * 16 + 15, 10) },
        { "Y", (0, 63999 * 16 + 15, 10) },
        { "M", (0, 63999, 10) },
        { "L", (0, 15999, 10) },
        { "D", (0, 65534, 10) },
        { "E", (0, 65534, 10) },
        { "F", (0, 32767, 10) }
    };

    public static readonly HashSet<string> ForceDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "T", "C", "VB" };
    public static readonly HashSet<string> MbsDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "T", "C", "VB", "X", "Y", "M", "L" };
    public static readonly HashSet<string> MwsDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "VB", "X", "Y", "DM", "EM", "FM", "W", "TM", "Z", "TC", "TS", "CC", "CS", "CM", "VM" };
    public static readonly HashSet<string> RdcDeviceTypes = new() { "R", "B", "MR", "LR", "CR", "DM", "EM", "FM", "ZF", "W", "TM", "Z", "T", "C", "CM", "X", "Y", "M", "L", "D", "E", "F" };
    public static readonly HashSet<string> WsDeviceTypes = new() { "T", "C" };

    public static readonly Dictionary<string, string> DefaultFormatByDeviceType = new()
    {
        { "R", "" }, { "B", "" }, { "MR", "" }, { "LR", "" }, { "CR", "" }, { "VB", "" },
        { "DM", ".U" }, { "EM", ".U" }, { "FM", ".U" }, { "ZF", ".U" }, { "W", ".U" }, { "TM", ".U" },
        { "Z", ".U" }, { "AT", ".U" }, { "CM", ".U" }, { "VM", ".U" }, { "T", ".D" }, { "TC", ".D" },
        { "TS", ".D" }, { "C", ".D" }, { "CC", ".D" }, { "CS", ".D" },
        { "X", "" }, { "Y", "" }, { "M", "" }, { "L", "" }, { "D", ".U" }, { "E", ".U" }, { "F", ".U" }
    };
}
