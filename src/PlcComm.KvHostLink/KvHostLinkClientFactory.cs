namespace PlcComm.KvHostLink;

/// <summary>
/// Factory helpers for opening ready-to-use Host Link clients.
/// </summary>
public static class KvHostLinkClientFactory
{
    /// <summary>
    /// Creates, configures, and opens a queued Host Link client.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected queued client.</returns>
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
