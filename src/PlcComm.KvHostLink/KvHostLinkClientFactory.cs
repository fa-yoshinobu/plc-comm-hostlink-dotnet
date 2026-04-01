namespace PlcComm.KvHostLink;

/// <summary>
/// Factory helpers for opening ready-to-use Host Link clients.
/// </summary>
/// <remarks>
/// The factory centralizes validation of host, port, timeout, and line-ending behavior so
/// samples and generated docs can point to one explicit connection entry point.
/// </remarks>
public static class KvHostLinkClientFactory
{
    /// <summary>
    /// Creates, configures, and opens a queued Host Link client.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected queued client.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The host name is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured port is outside the valid TCP/UDP range.</exception>
    /// <remarks>
    /// The returned client uses queued access so higher-level read, write, and polling helpers can
    /// share one Host Link session predictably.
    /// </remarks>
    public static async Task<QueuedKvHostLinkClient> OpenAndConnectAsync(
        KvHostLinkConnectionOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("Host must not be empty.", nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be in the range 1-65535.");

        var inner = new KvHostLinkClient(options.Host, options.Port, options.Transport)
        {
            Timeout = options.EffectiveTimeout,
            AppendLfOnSend = options.AppendLfOnSend,
        };

        var queued = new QueuedKvHostLinkClient(inner);
        await queued.OpenAsync(cancellationToken).ConfigureAwait(false);
        return queued;
    }
}
