namespace PlcComm.KvHostLink;

/// <summary>
/// Explicit connection options for a Host Link session.
/// </summary>
/// <remarks>
/// This type is intended for the unified high-level connection flow so generated documentation
/// can describe transport, timeout, profile, and framing behavior in one place.
/// </remarks>
/// <param name="Host">PLC IP address or hostname.</param>
/// <param name="Transport">Transport protocol.</param>
/// <param name="PlcProfile">Canonical KEYENCE KV PLC profile for the session.</param>
/// <param name="Port">Host Link port number.</param>
/// <param name="Timeout">Operation timeout. Omit it to use three seconds.</param>
public sealed record KvHostLinkConnectionOptions(
    string Host,
    int Port,
    HostLinkTransportMode Transport,
    string PlcProfile,
    TimeSpan? Timeout = null)
{
    private string _host = ValidateHost(Host);
    private int _port = ValidatePort(Port);
    private HostLinkTransportMode _transport = ValidateTransport(Transport);
    private string _plcProfile = NormalizePlcProfile(PlcProfile);
    private TimeSpan? _timeout = ValidateTimeout(Timeout);

    /// <summary>Gets the validated PLC IP address or hostname.</summary>
    public string Host
    {
        get => _host;
        init => _host = ValidateHost(value);
    }

    /// <summary>Gets the validated Host Link port.</summary>
    public int Port
    {
        get => _port;
        init => _port = ValidatePort(value);
    }

    /// <summary>Gets the explicitly selected transport.</summary>
    public HostLinkTransportMode Transport
    {
        get => _transport;
        init => _transport = ValidateTransport(value);
    }

    /// <summary>Gets or sets the canonical KEYENCE KV PLC profile for the session.</summary>
    public string PlcProfile
    {
        get => _plcProfile;
        init => _plcProfile = NormalizePlcProfile(value);
    }

    /// <summary>Gets the optional positive communication timeout.</summary>
    public TimeSpan? Timeout
    {
        get => _timeout;
        init => _timeout = ValidateTimeout(value);
    }

    /// <summary>Gets the effective timeout used for a new client instance.</summary>
    /// <remarks>
    /// Host Link callers may leave <see cref="Timeout"/> at its default value and use this property
    /// when they need the resolved timeout that will be applied to the client.
    /// </remarks>
    public TimeSpan EffectiveTimeout
    {
        get
        {
            return Timeout ?? TimeSpan.FromSeconds(3);
        }
    }

    private static string ValidateHost(string host)
        => !string.IsNullOrWhiteSpace(host)
            ? host
            : throw new ArgumentException("Host must not be empty.", nameof(host));

    private static int ValidatePort(int port)
        => port is >= 1 and <= 65535
            ? port
            : throw new ArgumentOutOfRangeException(nameof(port), "Port must be in the range 1-65535.");

    private static HostLinkTransportMode ValidateTransport(HostLinkTransportMode transport)
        => Enum.IsDefined(transport)
            ? transport
            : throw new ArgumentOutOfRangeException(nameof(transport), "Transport must be TCP or UDP.");

    private static TimeSpan? ValidateTimeout(TimeSpan? timeout)
        => timeout is null || timeout > TimeSpan.Zero
            ? timeout
            : throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be greater than zero.");

    private static string NormalizePlcProfile(string plcProfile)
        => KvHostLinkPlcProfiles.NormalizeName(plcProfile);
}
