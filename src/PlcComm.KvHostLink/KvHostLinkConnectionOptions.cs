namespace PlcComm.KvHostLink;

/// <summary>
/// Explicit connection options for a Host Link session.
/// </summary>
/// <remarks>
/// This type is intended for the unified high-level connection flow so generated documentation
/// can describe transport, timeout, and framing behavior in one place.
/// </remarks>
/// <param name="Host">PLC IP address or hostname.</param>
/// <param name="Port">Host Link port number. Defaults to 8501.</param>
/// <param name="Timeout">Operation timeout. A zero value falls back to the library default.</param>
/// <param name="Transport">Transport protocol.</param>
/// <param name="AppendLfOnSend">Whether to append LF after CR on send.</param>
public sealed record KvHostLinkConnectionOptions(
    string Host,
    int Port = 8501,
    TimeSpan Timeout = default,
    HostLinkTransportMode Transport = HostLinkTransportMode.Tcp,
    bool AppendLfOnSend = false)
{
    /// <summary>Gets the effective timeout used for a new client instance.</summary>
    /// <remarks>
    /// Host Link callers may leave <see cref="Timeout"/> at its default value and use this property
    /// when they need the resolved timeout that will be applied to the client.
    /// </remarks>
    public TimeSpan EffectiveTimeout => Timeout == default ? TimeSpan.FromSeconds(3) : Timeout;
}
