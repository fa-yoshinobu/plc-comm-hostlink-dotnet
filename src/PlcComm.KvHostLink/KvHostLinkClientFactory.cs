namespace PlcComm.KvHostLink;

/// <summary>
/// Factory helpers for opening ready-to-use Host Link clients.
/// </summary>
/// <remarks>
/// The factory centralizes validation of host, port, transport, and timeout behavior so
/// samples and generated docs can point to one explicit connection entry point.
/// </remarks>
public static class KvHostLinkClientFactory
{
    /// <summary>
    /// Creates, configures, and opens a Host Link client with built-in FIFO admission.
    /// </summary>
    /// <param name="options">Explicit connection options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A connected Host Link client.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The host is empty or is an unsupported IPv6 literal.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The configured port is outside the valid TCP/UDP range.</exception>
    /// <remarks>
    /// The ordinary client owns the one FIFO queue used by all low-level and high-level operations.
    /// Hostname resolution selects IPv4 only and never falls back to IPv6.
    /// </remarks>
    public static async Task<KvHostLinkClient> OpenAndConnectAsync(
        KvHostLinkConnectionOptions options,
        CancellationToken cancellationToken = default)
        => await OpenAndConnectOwnedAsync(
            options,
            static value => new KvHostLinkClient(
                value.Host,
                value.Port,
                value.Transport,
                value.PlcProfile)
            {
                Timeout = value.EffectiveTimeout,
            },
            static (client, token) => client.OpenAsync(token),
            static client => client.DisposeAsync(),
            cancellationToken).ConfigureAwait(false);

    internal static async Task<KvHostLinkClient> OpenAndConnectOwnedAsync(
        KvHostLinkConnectionOptions options,
        Func<KvHostLinkConnectionOptions, KvHostLinkClient> createClient,
        Func<KvHostLinkClient, CancellationToken, Task> openClient,
        Func<KvHostLinkClient, ValueTask> disposeClient,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(createClient);
        ArgumentNullException.ThrowIfNull(openClient);
        ArgumentNullException.ThrowIfNull(disposeClient);
        if (string.IsNullOrWhiteSpace(options.Host))
            throw new ArgumentException("Host must not be empty.", nameof(options));
        if (options.Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(options), "Port must be in the range 1-65535.");

        KvHostLinkClient inner = createClient(options);

        try
        {
            await openClient(inner, cancellationToken).ConfigureAwait(false);
            return inner;
        }
        catch
        {
            try
            {
                await disposeClient(inner).ConfigureAwait(false);
            }
            catch
            {
                // Cleanup must never replace the connection failure reported to the caller.
            }
            throw;
        }
    }
}
