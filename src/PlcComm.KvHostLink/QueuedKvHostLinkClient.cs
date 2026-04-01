using System.Threading;

namespace PlcComm.KvHostLink;

/// <summary>
/// A wrapper for <see cref="KvHostLinkClient"/> that serializes multi-step operations with a semaphore.
/// </summary>
/// <remarks>
/// Host Link requests often reuse one TCP session and one framing configuration. This wrapper provides
/// a documentation-friendly queued surface for those shared-session scenarios.
/// </remarks>
public sealed class QueuedKvHostLinkClient : IAsyncDisposable, IDisposable
{
    private readonly KvHostLinkClient _client;
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>
    /// Initializes a new instance of the <see cref="QueuedKvHostLinkClient"/> class.
    /// </summary>
    /// <param name="client">The underlying client to wrap.</param>
    public QueuedKvHostLinkClient(KvHostLinkClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>Gets the underlying low-level client.</summary>
    /// <remarks>
    /// Use <see cref="ExecuteAsync{T}(Func{KvHostLinkClient, Task{T}}, CancellationToken)"/> when you need
    /// direct access while preserving serialized request ordering.
    /// </remarks>
    public KvHostLinkClient InnerClient => _client;

    /// <summary>Gets or sets the communication timeout.</summary>
    public TimeSpan Timeout
    {
        get => _client.Timeout;
        set => _client.Timeout = value;
    }

    /// <summary>Gets or sets whether LF is appended after CR on send.</summary>
    public bool AppendLfOnSend
    {
        get => _client.AppendLfOnSend;
        set => _client.AppendLfOnSend = value;
    }

    /// <summary>Gets or sets the raw frame trace hook.</summary>
    public Action<HostLinkTraceFrame>? TraceHook
    {
        get => _client.TraceHook;
        set => _client.TraceHook = value;
    }

    /// <summary>Gets a value indicating whether the client is connected.</summary>
    public bool IsOpen => _client.IsOpen;

    /// <summary>Opens the connection asynchronously with exclusive access.</summary>
    /// <remarks>Call this once after construction or again after an intentional disconnect.</remarks>
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _client.OpenAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Executes a custom async operation with exclusive access to the wrapped client.</summary>
    /// <typeparam name="T">Result type produced by the custom operation.</typeparam>
    /// <param name="operation">Delegate that receives the wrapped <see cref="KvHostLinkClient"/>.</param>
    /// <param name="cancellationToken">Cancellation token used while waiting for exclusive access.</param>
    /// <returns>The value returned by <paramref name="operation"/>.</returns>
    public async Task<T> ExecuteAsync<T>(
        Func<KvHostLinkClient, Task<T>> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation(_client).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Executes a custom async operation with exclusive access to the wrapped client.</summary>
    /// <param name="operation">Delegate that receives the wrapped <see cref="KvHostLinkClient"/>.</param>
    /// <param name="cancellationToken">Cancellation token used while waiting for exclusive access.</param>
    public async Task ExecuteAsync(
        Func<KvHostLinkClient, Task> operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation(_client).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc cref="KvHostLinkClient.SendRawAsync"/>
    public Task<string> SendRawAsync(string body, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.SendRawAsync(body, cancellationToken), cancellationToken);

    /// <inheritdoc cref="KvHostLinkClient.QueryModelAsync"/>
    public Task<KvModelInfo> QueryModelAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.QueryModelAsync(cancellationToken), cancellationToken);

    /// <inheritdoc cref="KvHostLinkClient.ConfirmOperatingModeAsync"/>
    public Task<KvPlcMode> ConfirmOperatingModeAsync(CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ConfirmOperatingModeAsync(cancellationToken), cancellationToken);

    /// <inheritdoc cref="KvHostLinkClient.ReadAsync"/>
    public Task<string[]> ReadAsync(string device, string? dataFormat = null, CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadAsync(device, dataFormat, cancellationToken), cancellationToken);

    /// <inheritdoc cref="KvHostLinkClient.ReadConsecutiveAsync"/>
    public Task<string[]> ReadConsecutiveAsync(
        string device,
        int count,
        string? dataFormat = null,
        CancellationToken cancellationToken = default)
        => ExecuteAsync(client => client.ReadConsecutiveAsync(device, count, dataFormat, cancellationToken), cancellationToken);

    /// <inheritdoc cref="KvHostLinkClient.WriteAsync{T}"/>
    public Task WriteAsync<T>(
        string device,
        T value,
        string? dataFormat = null,
        CancellationToken cancellationToken = default) where T : IFormattable
        => ExecuteAsync(client => client.WriteAsync(device, value, dataFormat, cancellationToken), cancellationToken);

    /// <inheritdoc cref="KvHostLinkClient.WriteConsecutiveAsync{T}"/>
    public Task WriteConsecutiveAsync<T>(
        string device,
        IEnumerable<T> values,
        string? dataFormat = null,
        CancellationToken cancellationToken = default) where T : IFormattable
        => ExecuteAsync(client => client.WriteConsecutiveAsync(device, values, dataFormat, cancellationToken), cancellationToken);

    /// <summary>Disposes the wrapper and the underlying client.</summary>
    public void Dispose()
    {
        _gate.Dispose();
        _client.Dispose();
    }

    /// <summary>Disposes the wrapper and the underlying client asynchronously.</summary>
    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
