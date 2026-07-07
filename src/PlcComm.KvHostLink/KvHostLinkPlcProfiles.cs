namespace PlcComm.KvHostLink;

public sealed record KvHostLinkPlcProfile(string Name, string DisplayName);

internal sealed record KvHostLinkPlcProfileDefinition(
    string Name,
    string DisplayName,
    string SourceLabel)
{
    public KvHostLinkPlcProfile ToPublicProfile()
    {
        return new KvHostLinkPlcProfile(Name, DisplayName);
    }
}

public static class KvHostLinkPlcProfiles
{
    private static readonly KvHostLinkPlcProfileDefinition KvNanoDefinition = new(
        "keyence:kv-nano",
        "KEYENCE KV-NANO",
        "KV-NANO");

    private static readonly KvHostLinkPlcProfileDefinition KvNanoXymDefinition = new(
        "keyence:kv-nano-xym",
        "KEYENCE KV-NANO (XYM)",
        "KV-NANO(XYM)");

    private static readonly KvHostLinkPlcProfileDefinition Kv3000Definition = new(
        "keyence:kv-3000",
        "KEYENCE KV-3000",
        "KV-3000");

    private static readonly KvHostLinkPlcProfileDefinition Kv3000XymDefinition = new(
        "keyence:kv-3000-xym",
        "KEYENCE KV-3000 (XYM)",
        "KV-3000(XYM)");

    private static readonly KvHostLinkPlcProfileDefinition Kv5000Definition = new(
        "keyence:kv-5000",
        "KEYENCE KV-5000",
        "KV-5000");

    private static readonly KvHostLinkPlcProfileDefinition Kv5000XymDefinition = new(
        "keyence:kv-5000-xym",
        "KEYENCE KV-5000 (XYM)",
        "KV-5000(XYM)");

    private static readonly KvHostLinkPlcProfileDefinition Kv7000Definition = new(
        "keyence:kv-7000",
        "KEYENCE KV-7000",
        "KV-7000");

    private static readonly KvHostLinkPlcProfileDefinition Kv7000XymDefinition = new(
        "keyence:kv-7000-xym",
        "KEYENCE KV-7000 (XYM)",
        "KV-7000(XYM)");

    private static readonly KvHostLinkPlcProfileDefinition Kv8000Definition = new(
        "keyence:kv-8000",
        "KEYENCE KV-8000",
        "KV-8000");

    private static readonly KvHostLinkPlcProfileDefinition Kv8000XymDefinition = new(
        "keyence:kv-8000-xym",
        "KEYENCE KV-8000 (XYM)",
        "KV-8000(XYM)");

    private static readonly KvHostLinkPlcProfileDefinition KvX500Definition = new(
        "keyence:kv-x500",
        "KEYENCE KV-X500",
        "KV-X500");

    private static readonly KvHostLinkPlcProfileDefinition KvX500XymDefinition = new(
        "keyence:kv-x500-xym",
        "KEYENCE KV-X500 (XYM)",
        "KV-X500(XYM)");

    public static KvHostLinkPlcProfile KvNano { get; } = KvNanoDefinition.ToPublicProfile();

    public static KvHostLinkPlcProfile KvNanoXym { get; } = KvNanoXymDefinition.ToPublicProfile();

    public static KvHostLinkPlcProfile Kv3000 { get; } = Kv3000Definition.ToPublicProfile();

    public static KvHostLinkPlcProfile Kv3000Xym { get; } = Kv3000XymDefinition.ToPublicProfile();

    public static KvHostLinkPlcProfile Kv5000 { get; } = Kv5000Definition.ToPublicProfile();

    public static KvHostLinkPlcProfile Kv5000Xym { get; } = Kv5000XymDefinition.ToPublicProfile();

    public static KvHostLinkPlcProfile Kv7000 { get; } = Kv7000Definition.ToPublicProfile();

    public static KvHostLinkPlcProfile Kv7000Xym { get; } = Kv7000XymDefinition.ToPublicProfile();

    public static KvHostLinkPlcProfile Kv8000 { get; } = Kv8000Definition.ToPublicProfile();

    public static KvHostLinkPlcProfile Kv8000Xym { get; } = Kv8000XymDefinition.ToPublicProfile();

    public static KvHostLinkPlcProfile KvX500 { get; } = KvX500Definition.ToPublicProfile();

    public static KvHostLinkPlcProfile KvX500Xym { get; } = KvX500XymDefinition.ToPublicProfile();

    private static readonly KvHostLinkPlcProfileDefinition[] Profiles =
    [
        KvNanoDefinition,
        KvNanoXymDefinition,
        Kv3000Definition,
        Kv3000XymDefinition,
        Kv5000Definition,
        Kv5000XymDefinition,
        Kv7000Definition,
        Kv7000XymDefinition,
        Kv8000Definition,
        Kv8000XymDefinition,
        KvX500Definition,
        KvX500XymDefinition,
    ];

    private static readonly string[] ProfileNames = Profiles.Select(profile => profile.Name).ToArray();

    public static IReadOnlyList<string> GetNames()
    {
        return ProfileNames;
    }

    public static string NormalizeName(string plcProfile)
    {
        return FromName(plcProfile).Name;
    }

    public static string GetDisplayName(string plcProfile)
    {
        return FromName(plcProfile).DisplayName;
    }

    public static KvHostLinkPlcProfile FromName(string plcProfile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plcProfile);

        var normalized = NormalizeText(plcProfile);
        foreach (var profile in Profiles)
        {
            if (string.Equals(profile.Name, normalized, StringComparison.Ordinal))
            {
                return profile.ToPublicProfile();
            }
        }

        var supported = string.Join(", ", ProfileNames);
        throw new HostLinkProtocolError(
            $"Unsupported PLC profile '{plcProfile}'. Supported PLC profiles: {supported}.");
    }

    internal static IReadOnlyList<KvHostLinkPlcProfileDefinition> GetProfiles()
    {
        return Profiles;
    }

    private static string NormalizeText(string text)
    {
        return text.Trim().TrimEnd('\0');
    }
}
