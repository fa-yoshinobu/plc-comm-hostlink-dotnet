namespace PlcComm.KvHostLink;

/// <summary>
/// Explicit connection options for a Host Link session.
/// </summary>
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
    public TimeSpan EffectiveTimeout => Timeout == default ? TimeSpan.FromSeconds(3) : Timeout;
}
