using System.ComponentModel;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PlcComm.KvHostLink;

/// <summary>
/// A low-level Host Link (Upper Link) client for KEYENCE KV series PLCs.
/// </summary>
/// <remarks>
/// Public operations enter one arrival-order FIFO queue. One client therefore owns at most one
/// active wire transaction, and an explicitly aggregate read retains the same turn for its complete
/// plan. Queue waiting does not consume the transaction timeout. Waiting cancellation sends nothing.
/// Recursive entry on the same client is rejected. For application code, prefer
/// <see cref="KvHostLinkClientFactory.OpenAndConnectAsync(KvHostLinkConnectionOptions, CancellationToken)"/>.
/// </remarks>
public sealed class KvHostLinkClient : IDisposable, IAsyncDisposable
{
    private const int MaxResponseBodyLength = 65_536;
    private readonly string _host;
    private readonly int _port;
    private readonly HostLinkTransportMode _transportMode;
    private TcpClient? _tcp;
    private NetworkStream? _tcpStream;
    private UdpClient? _udp;
    private readonly object _lifecycleSync = new();
    private readonly object _operationSync = new();
    private readonly LinkedList<OperationWaiter> _operationWaiters = new();
    private readonly AsyncLocal<OperationContext?> _operationContext = new();
    private OperationGeneration _operationGeneration = new();
    private OperationGeneration? _activeOperationGeneration;
    private TaskCompletionSource? _operationIdleCompletion;
    private bool _operationActive;
    private Task? _closeTask;
    private int _closing;
    private int _disposed;
    private byte[] _rxBuf = new byte[4096];
    private int _rxStart;
    private int _rxCount;
    private bool _skipLeadingSeparators;
    private readonly byte[] _tcpReadBuf = new byte[8192];
    private TimeSpan _timeout = TimeSpan.FromSeconds(3);
    private int _monitorBitCount;
    private string[] _monitorWordFormats = [];
    private long _requestCount;
    private long _txBytes;
    private long _rxBytes;

    private sealed class OperationGeneration
    {
        private int _cancellationDisposed;

        internal CancellationTokenSource Cancellation { get; } = new();
        internal bool ClientDisposed { get; set; }
        internal bool IsRetired => Cancellation.IsCancellationRequested;

        internal Exception CreateFailure(object client)
            => ClientDisposed
                ? new ObjectDisposedException(client.GetType().FullName)
                : new HostLinkClosedError();

        internal void DisposeCancellation()
        {
            if (Interlocked.Exchange(ref _cancellationDisposed, 1) == 0)
                Cancellation.Dispose();
        }
    }

    private sealed class OperationWaiter(OperationGeneration generation, TimeSpan timeout)
    {
        internal OperationGeneration Generation { get; } = generation;
        internal TimeSpan Timeout { get; } = timeout;
        internal TaskCompletionSource<OperationLease> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal LinkedListNode<OperationWaiter>? Node { get; set; }
        internal CancellationTokenRegistration CancellationRegistration { get; set; }
    }

    private readonly record struct OperationLease(
        OperationGeneration Generation,
        TimeSpan Timeout);

    private sealed record OperationContext(
        KvHostLinkClient Client,
        OperationGeneration Generation,
        TimeSpan Timeout);

    public KvHostLinkClient(
        string host,
        int port,
        HostLinkTransportMode transportMode,
        string plcProfile)
    {
        host = KvHostLinkNetwork.ValidateIpv4Host(host, nameof(host));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be in the range 1-65535.");
        if (!Enum.IsDefined(transportMode))
            throw new ArgumentOutOfRangeException(nameof(transportMode), "Transport must be TCP or UDP.");
        _host = host;
        _port = port;
        _transportMode = transportMode;
        PlcProfile = KvHostLinkPlcProfiles.NormalizeName(plcProfile);
    }

    public string PlcProfile { get; }
    /// <summary>Gets an immutable snapshot of cumulative traffic for this client lifetime.</summary>
    public HostLinkTrafficStats TrafficStats => new(
        unchecked((ulong)Interlocked.Read(ref _requestCount)),
        unchecked((ulong)Interlocked.Read(ref _txBytes)),
        unchecked((ulong)Interlocked.Read(ref _rxBytes)));
    /// <summary>Gets or sets the operation timeout from 1 through <see cref="int.MaxValue"/> milliseconds.</summary>
    public TimeSpan Timeout
    {
        get
        {
            lock (_operationSync)
                return _timeout;
        }
        set
        {
            TimeSpan validated = KvHostLinkTimeout.Validate(value, nameof(value));
            lock (_operationSync)
                _timeout = validated;
        }
    }

    /// <summary>
    /// Optional maintainer hook called once for every exact raw frame sent and received.
    /// Hook failures are isolated from communication behavior.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Action<HostLinkTraceFrame>? TraceHook { get; set; }

    /// <summary>Gets whether the selected TCP or UDP transport is currently open.</summary>
    public bool IsOpen
    {
        get
        {
            lock (_lifecycleSync)
                return _transportMode == HostLinkTransportMode.Tcp ? _tcpStream is not null : _udp is not null;
        }
    }

    /// <summary>Opens the configured transport without retrying.</summary>
    /// <remarks>
    /// An internal connect timeout throws <see cref="HostLinkTimeoutError"/>;
    /// caller cancellation throws <see cref="OperationCanceledException"/>.
    /// </remarks>
    public Task OpenAsync(CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => OpenCoreAsync(cancellationToken), cancellationToken);

    private async Task OpenCoreAsync(CancellationToken cancellationToken)
    {
        if (IsOpen) return;

        OperationContext context = RequireOperationContext();
        using var operationCancellation = new KvHostLinkOperationCancellation(
            cancellationToken,
            context.Generation.Cancellation.Token,
            context.Timeout);
        if (_transportMode == HostLinkTransportMode.Tcp)
        {
            var tcp = new TcpClient(AddressFamily.InterNetwork);
            try
            {
                IPAddress remoteAddress = await KvHostLinkNetwork.ResolveIpv4AddressAsync(
                    _host,
                    operationCancellation.Token).ConfigureAwait(false);
                await tcp.ConnectAsync(remoteAddress, _port, operationCancellation.Token).ConfigureAwait(false);
                tcp.NoDelay = true;
                NetworkStream stream = tcp.GetStream();
                lock (_lifecycleSync)
                {
                    if (_disposed != 0 || _closing != 0 ||
                        context.Generation.IsRetired ||
                        operationCancellation.Token.IsCancellationRequested)
                    {
                        throw operationCancellation.Translate(
                            new OperationCanceledException(operationCancellation.Token),
                            "TCP connect");
                    }
                    _tcp = tcp;
                    _tcpStream = stream;
                    ResetProtocolStateNoLock();
                }
            }
            catch (Exception error)
            {
                tcp.Dispose();
                throw operationCancellation.Translate(error, "TCP connect");
            }
            return;
        }

        var udp = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            IPAddress remoteAddress = await KvHostLinkNetwork.ResolveIpv4AddressAsync(
                _host,
                operationCancellation.Token).ConfigureAwait(false);
            operationCancellation.Token.ThrowIfCancellationRequested();
            udp.Connect(new IPEndPoint(remoteAddress, _port));
            lock (_lifecycleSync)
            {
                if (_disposed != 0 || _closing != 0 ||
                    context.Generation.IsRetired ||
                    operationCancellation.Token.IsCancellationRequested)
                {
                    throw operationCancellation.Translate(
                        new OperationCanceledException(operationCancellation.Token),
                        "UDP connect");
                }
                _udp = udp;
                ResetProtocolStateNoLock();
            }
        }
        catch (Exception error)
        {
            udp.Dispose();
            throw operationCancellation.Translate(error, "UDP connect");
        }
    }

    /// <summary>Opens the configured transport synchronously without retrying.</summary>
    public void Open() => OpenAsync().GetAwaiter().GetResult();

    /// <summary>Closes the transport and interrupts active I/O.</summary>
    public void Close()
        => BeginClose(disposing: false).GetAwaiter().GetResult();

    private void CloseTransport()
    {
        lock (_lifecycleSync)
            CloseTransportNoLock();
    }

    private void CloseTransportNoLock()
    {
        _tcpStream?.Dispose();
        _tcpStream = null;
        _tcp?.Close();
        _tcp = null;
        _udp?.Dispose();
        _udp = null;
        ResetProtocolStateNoLock();
    }

    private void ResetProtocolStateNoLock()
    {
        _rxStart = 0; _rxCount = 0;
        _skipLeadingSeparators = false;
        _monitorBitCount = 0;
        _monitorWordFormats = [];
    }

    /// <summary>Closes the transport, promptly interrupts active I/O, and asynchronously awaits cleanup.</summary>
    public Task CloseAsync() => BeginClose(disposing: false);

    /// <summary>Closes the transport, interrupts active I/O, and disposes the client.</summary>
    public void Dispose()
        => BeginClose(disposing: true).GetAwaiter().GetResult();

    /// <summary>Closes the transport, interrupts active I/O, and asynchronously disposes the client.</summary>
    public ValueTask DisposeAsync() => new(BeginClose(disposing: true));

    private Task BeginClose(bool disposing)
    {
        OperationContext? nested = _operationContext.Value;
        if (nested is not null && ReferenceEquals(nested.Client, this))
            throw new HostLinkReentrancyError();

        TaskCompletionSource? completion = null;
        lock (_lifecycleSync)
        {
            if (disposing)
            {
                if (_disposed != 0)
                    return _closeTask ?? Task.CompletedTask;
                _disposed = 1;
            }
            else if (_disposed != 0)
                return _closeTask ?? Task.CompletedTask;

            if (_closeTask is not null)
                return _closeTask;

            _closing = 1;
            completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            _closeTask = completion.Task;
        }

        RetireOperationGeneration(disposing);
        CloseTransport();
        _ = FinishCloseAsync(completion, disposing);
        return completion.Task;
    }

    private async Task FinishCloseAsync(TaskCompletionSource completion, bool disposing)
    {
        try
        {
            await WaitForOperationIdleAsync().ConfigureAwait(false);
            CloseTransport();
            lock (_lifecycleSync)
            {
                _closing = 0;
                _closeTask = null;
            }
            if (disposing || Volatile.Read(ref _disposed) != 0)
            {
                lock (_operationSync)
                    _operationGeneration.DisposeCancellation();
            }
            completion.SetResult();
        }
        catch (Exception error)
        {
            lock (_lifecycleSync)
            {
                _closing = 0;
                _closeTask = null;
            }
            completion.SetException(error);
        }
    }

    private ValueTask<OperationLease> EnterOperationAsync(CancellationToken cancellationToken)
    {
        ThrowIfOperationUnavailable();
        OperationContext? nested = _operationContext.Value;
        if (nested is not null && ReferenceEquals(nested.Client, this))
            throw new HostLinkReentrancyError();

        lock (_operationSync)
        {
            ThrowIfOperationUnavailable();
            cancellationToken.ThrowIfCancellationRequested();
            OperationGeneration generation = _operationGeneration;
            TimeSpan timeout = _timeout;
            if (!_operationActive)
            {
                _operationActive = true;
                _activeOperationGeneration = generation;
                _operationIdleCompletion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                return ValueTask.FromResult(new OperationLease(generation, timeout));
            }

            var waiter = new OperationWaiter(generation, timeout);
            waiter.Node = _operationWaiters.AddLast(waiter);
            if (cancellationToken.CanBeCanceled)
            {
                CancellationTokenRegistration registration = cancellationToken.UnsafeRegister(
                    static state =>
                    {
                        var (client, queued, token) =
                            ((KvHostLinkClient, OperationWaiter, CancellationToken))state!;
                        lock (client._operationSync)
                        {
                            if (queued.Node is null)
                                return;
                            client._operationWaiters.Remove(queued.Node);
                            queued.Node = null;
                            queued.Completion.TrySetCanceled(token);
                        }
                    },
                    (this, waiter, cancellationToken));
                waiter.CancellationRegistration = registration;
                if (waiter.Node is null)
                    registration.Dispose();
            }
            return new ValueTask<OperationLease>(AwaitOperationWaiterAsync(waiter));
        }
    }

    private static async Task<OperationLease> AwaitOperationWaiterAsync(OperationWaiter waiter)
    {
        try
        {
            return await waiter.Completion.Task.ConfigureAwait(false);
        }
        finally
        {
            waiter.CancellationRegistration.Dispose();
        }
    }

    private void ExitOperation(OperationLease lease)
    {
        OperationWaiter? next = null;
        TaskCompletionSource? idle = null;
        lock (_operationSync)
        {
            if (_operationWaiters.First is { } first)
            {
                next = first.Value;
                _operationWaiters.RemoveFirst();
                next.Node = null;
                _activeOperationGeneration = next.Generation;
            }
            else
            {
                _operationActive = false;
                _activeOperationGeneration = null;
                idle = _operationIdleCompletion;
                _operationIdleCompletion = null;
            }
        }

        if (next is not null)
        {
            next.CancellationRegistration.Dispose();
            next.Completion.TrySetResult(new OperationLease(next.Generation, next.Timeout));
        }
        else
        {
            idle?.TrySetResult();
        }
        if (lease.Generation.IsRetired)
            lease.Generation.DisposeCancellation();
    }

    private void RetireOperationGeneration(bool disposed)
    {
        OperationGeneration retired;
        OperationWaiter[] rejected;
        bool activeUsesRetiredGeneration;
        lock (_operationSync)
        {
            retired = _operationGeneration;
            retired.ClientDisposed |= disposed;
            _operationGeneration = new OperationGeneration();
            activeUsesRetiredGeneration = ReferenceEquals(_activeOperationGeneration, retired);
            rejected = _operationWaiters
                .Where(waiter => ReferenceEquals(waiter.Generation, retired))
                .ToArray();
            foreach (OperationWaiter waiter in rejected)
            {
                if (waiter.Node is not null)
                {
                    _operationWaiters.Remove(waiter.Node);
                    waiter.Node = null;
                }
            }
        }

        retired.Cancellation.Cancel();
        foreach (OperationWaiter waiter in rejected)
        {
            waiter.CancellationRegistration.Dispose();
            waiter.Completion.TrySetException(retired.CreateFailure(this));
        }
        if (!activeUsesRetiredGeneration)
            retired.DisposeCancellation();
    }

    private Task WaitForOperationIdleAsync()
    {
        lock (_operationSync)
            return _operationActive
                ? _operationIdleCompletion!.Task
                : Task.CompletedTask;
    }

    private void ThrowIfOperationUnavailable()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Volatile.Read(ref _closing) != 0)
            throw new HostLinkClosedError();
    }

    private void FireTrace(HostLinkTraceDirection direction, byte[] data)
    {
        try
        {
            TraceHook?.Invoke(new HostLinkTraceFrame(direction, data.ToArray(), DateTime.UtcNow));
        }
        catch
        {
            // Diagnostic hooks must not change frame bytes, retries, timeout, or command results.
        }
    }

    /// <summary>Sends one maintainer raw command and returns response body bytes without terminators.</summary>
    /// <remarks>
    /// This is a single-request operation. Because arbitrary raw commands cannot be classified as
    /// read-only, any post-send failure is conservatively reported as
    /// <see cref="HostLinkOutcomeUnknownError"/>.
    /// </remarks>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<byte[]> SendRawAsync(string body, CancellationToken cancellationToken = default)
    {
        byte[] frame = KvHostLinkProtocol.BuildFrame(body);
        return ExecuteExclusiveAsync(
            () => SendRawCoreAsync(frame, stateChanging: true, cancellationToken),
            cancellationToken);
    }

    internal async Task<T> ExecuteExclusiveAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        OperationLease lease = await EnterOperationAsync(cancellationToken).ConfigureAwait(false);
        OperationContext? priorContext = _operationContext.Value;
        _operationContext.Value = new OperationContext(this, lease.Generation, lease.Timeout);
        try
        {
            T result = await operation().ConfigureAwait(false);
            if (lease.Generation.IsRetired)
                throw lease.Generation.CreateFailure(this);
            return result;
        }
        finally
        {
            _operationContext.Value = priorContext;
            ExitOperation(lease);
        }
    }

    internal async Task ExecuteExclusiveAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        await ExecuteExclusiveAsync(
            async () =>
            {
                await operation().ConfigureAwait(false);
                return true;
            },
            cancellationToken).ConfigureAwait(false);
    }

    private OperationContext RequireOperationContext()
    {
        OperationContext? context = _operationContext.Value;
        return context is not null && ReferenceEquals(context.Client, this)
            ? context
            : throw new InvalidOperationException("Host Link core operation requires an admitted client turn.");
    }

    private async Task<byte[]> SendRawCoreAsync(
        byte[] frame,
        bool stateChanging,
        CancellationToken cancellationToken)
    {
        OperationContext context = RequireOperationContext();
        if (!IsOpen)
            throw new HostLinkNotConnectedError();

        using var operationCancellation = new KvHostLinkOperationCancellation(
            cancellationToken,
            context.Generation.Cancellation.Token,
            context.Timeout);
        return await SendRawTransportCoreAsync(
            frame,
            stateChanging,
            operationCancellation).ConfigureAwait(false);
    }

    private async Task<byte[]> SendRawTransportCoreAsync(
        byte[] frame,
        bool stateChanging,
        KvHostLinkOperationCancellation operationCancellation)
    {
        bool sendMayHaveStarted = false;
        if (_transportMode == HostLinkTransportMode.Tcp)
        {
            try
            {
                operationCancellation.Token.ThrowIfCancellationRequested();
                sendMayHaveStarted = true;
                await _tcpStream!.WriteAsync(frame, operationCancellation.Token).ConfigureAwait(false);
                RecordSend(frame.Length);
                FireTrace(HostLinkTraceDirection.Send, frame);
                var response = await RecvTcpFrameAsync(operationCancellation.Token).ConfigureAwait(false);
                RecordReceive(response.CountedLength);
                FireTrace(HostLinkTraceDirection.Receive, response.Frame);
                return response.Body;
            }
            catch (Exception error)
            {
                CloseTransport();
                Exception translated = operationCancellation.Translate(error, "TCP exchange");
                throw stateChanging && sendMayHaveStarted
                    ? CreateOutcomeUnknown(translated)
                    : translated;
            }
        }

        try
        {
            operationCancellation.Token.ThrowIfCancellationRequested();
            sendMayHaveStarted = true;
            await _udp!.SendAsync(frame, operationCancellation.Token).ConfigureAwait(false);
            RecordSend(frame.Length);
            FireTrace(HostLinkTraceDirection.Send, frame);
            var result = await _udp.ReceiveAsync(operationCancellation.Token).ConfigureAwait(false);
            RecordReceive(result.Buffer.Length);
            FireTrace(HostLinkTraceDirection.Receive, result.Buffer);
            byte[] responseBody = KvHostLinkProtocol.ExtractBody(result.Buffer);
            if (responseBody.Length > MaxResponseBodyLength)
                throw new HostLinkProtocolError($"Response body exceeds {MaxResponseBodyLength} bytes");
            return responseBody;
        }
        catch (Exception error)
        {
            // Host Link has no transaction ID. A failed datagram exchange
            // must never leave a delayed response for the next request.
            CloseTransport();
            Exception translated = operationCancellation.Translate(error, "UDP exchange");
            throw stateChanging && sendMayHaveStarted
                ? CreateOutcomeUnknown(translated)
                : translated;
        }
    }

    private static HostLinkOutcomeUnknownError CreateOutcomeUnknown(Exception cause)
    {
        HostLinkOutcomeUnknownReason reason = cause switch
        {
            HostLinkTimeoutError => HostLinkOutcomeUnknownReason.Timeout,
            OperationCanceledException => HostLinkOutcomeUnknownReason.CallerCancellation,
            HostLinkClosedError => HostLinkOutcomeUnknownReason.ConnectionClosed,
            HostLinkProtocolError => HostLinkOutcomeUnknownReason.InvalidResponse,
            _ => HostLinkOutcomeUnknownReason.TransportFailure,
        };
        return new HostLinkOutcomeUnknownError(
            "The state-changing Host Link request may have reached the PLC, but its outcome is unknown. Do not retry automatically.",
            reason,
            cause);
    }

    private void RecordSend(int length)
    {
        Interlocked.Increment(ref _requestCount);
        Interlocked.Add(ref _txBytes, length);
    }

    private void RecordReceive(int length) => Interlocked.Add(ref _rxBytes, length);

    private async Task<(byte[] Body, byte[] Frame, int CountedLength)> RecvTcpFrameAsync(
        CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_skipLeadingSeparators && _rxCount > 0)
            {
                while (_rxCount > 0 && (_rxBuf[_rxStart] == '\r' || _rxBuf[_rxStart] == '\n'))
                {
                    _rxStart++;
                    _rxCount--;
                }
                if (_rxCount > 0)
                    _skipLeadingSeparators = false;
            }

            int foundIdx = -1;
            for (int i = 0; i < _rxCount; i++)
            {
                byte b = _rxBuf[_rxStart + i];
                if (b == '\r' || b == '\n') { foundIdx = i; break; }
            }

            if (foundIdx >= 0)
            {
                if (foundIdx > MaxResponseBodyLength)
                    throw new HostLinkProtocolError($"Response body exceeds {MaxResponseBodyLength} bytes");

                int frameLength = foundIdx + 1;
                while (frameLength < _rxCount && (_rxBuf[_rxStart + frameLength] == '\r' || _rxBuf[_rxStart + frameLength] == '\n'))
                    frameLength++;
                _skipLeadingSeparators = true;
                byte[] body = _rxBuf.AsSpan(_rxStart, foundIdx).ToArray();
                byte[] receivedFrame = _rxBuf.AsSpan(_rxStart, frameLength).ToArray();
                _rxStart += frameLength;
                _rxCount -= frameLength;
                if (_rxStart > _rxBuf.Length / 2)
                {
                    _rxBuf.AsSpan(_rxStart, _rxCount).CopyTo(_rxBuf);
                    _rxStart = 0;
                }
                return (body, receivedFrame, foundIdx + 1);
            }

            if (_rxCount > MaxResponseBodyLength)
                throw new HostLinkProtocolError($"Response body exceeds {MaxResponseBodyLength} bytes");

            int read = await _tcpStream!.ReadAsync(_tcpReadBuf, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                bool hadPartialResponse = _rxCount > 0;
                CloseTransport();
                string message = hadPartialResponse
                    ? "Connection closed by PLC before the response terminator"
                    : "Connection closed by PLC";
                throw new HostLinkConnectionError(message);
            }

            if (_rxStart + _rxCount + read > _rxBuf.Length)
            {
                if (_rxCount > 0)
                    _rxBuf.AsSpan(_rxStart, _rxCount).CopyTo(_rxBuf);
                _rxStart = 0;
                if (_rxCount + read > _rxBuf.Length)
                {
                    var grown = new byte[Math.Max(_rxBuf.Length * 2, _rxCount + read)];
                    _rxBuf.AsSpan(0, _rxCount).CopyTo(grown);
                    _rxBuf = grown;
                }
            }
            _tcpReadBuf.AsSpan(0, read).CopyTo(_rxBuf.AsSpan(_rxStart + _rxCount));
            _rxCount += read;
        }
    }

    private Task<string> SendSemanticAsync(string body, CancellationToken cancellationToken)
        => ExecuteExclusiveAsync(
            () => SendSemanticCoreAsync(body, cancellationToken, stateChanging: false),
            cancellationToken);

    internal async Task<string> SendSemanticCoreAsync(
        string body,
        CancellationToken cancellationToken,
        bool stateChanging = false)
    {
        byte[] frame = KvHostLinkProtocol.BuildFrame(body);
        OperationContext context = RequireOperationContext();
        if (!IsOpen)
            throw new HostLinkNotConnectedError();

        using var operationCancellation = new KvHostLinkOperationCancellation(
            cancellationToken,
            context.Generation.Cancellation.Token,
            context.Timeout);
        byte[] response = await SendRawTransportCoreAsync(
            frame,
            stateChanging,
            operationCancellation).ConfigureAwait(false);
        try
        {
            string decoded = KvHostLinkProtocol.DecodeSemanticResponse(response);
            operationCancellation.Token.ThrowIfCancellationRequested();
            return decoded;
        }
        catch (HostLinkProtocolError error)
        {
            CloseTransport();
            throw stateChanging ? CreateOutcomeUnknown(error) : error;
        }
        catch (OperationCanceledException error)
        {
            CloseTransport();
            Exception translated = operationCancellation.Translate(error, "response decode");
            throw stateChanging ? CreateOutcomeUnknown(translated) : translated;
        }
    }

    private Task ExpectOkAsync(string body, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ExpectOkCoreAsync(body, cancellationToken), cancellationToken);

    internal async Task ExpectOkCoreAsync(string body, CancellationToken cancellationToken)
    {
        var response = await SendSemanticCoreAsync(
            body,
            cancellationToken,
            stateChanging: true).ConfigureAwait(false);
        if (response != "OK")
        {
            CloseTransport();
            var error = new HostLinkProtocolError(
                $"Expected 'OK' but received '{response}' for command '{body}'");
            throw CreateOutcomeUnknown(error);
        }
    }

    private void InvalidateProtocolState()
        => CloseTransport();


    // --- Commands ---

    public async Task ChangeModeAsync(KvPlcMode mode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), "Mode must be Program or Run.");
        await ExpectOkAsync($"M{(int)mode}", cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearErrorAsync(CancellationToken cancellationToken = default)
    {
        await ExpectOkAsync("ER", cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> CheckErrorNoAsync(CancellationToken cancellationToken = default)
    {
        return await SendSemanticAsync("?E", cancellationToken).ConfigureAwait(false);
    }

    public async Task<KvModelInfo> QueryModelAsync(CancellationToken cancellationToken = default)
    {
        string code = await SendSemanticAsync("?K", cancellationToken).ConfigureAwait(false);
        return new KvModelInfo(code, KvHostLinkModels.ModelCodes.GetValueOrDefault(code, "Unknown"));
    }

    public Task<KvPlcMode> ConfirmOperatingModeAsync(CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            string response = await SendSemanticCoreAsync("?M", cancellationToken).ConfigureAwait(false);
            if (response == "0") return KvPlcMode.Program;
            if (response == "1") return KvPlcMode.Run;

            CloseTransport();
            throw new HostLinkProtocolError($"Unsupported PLC mode response: {response}");
        }, cancellationToken);

    /// <summary>Sets the PLC clock from an explicit local calendar value in years 2000 through 2099.</summary>
    public async Task SetTimeAsync(DateTime value, CancellationToken cancellationToken = default)
    {
        if (value.Year is < 2000 or > 2099)
            throw new ArgumentOutOfRangeException(nameof(value), "Host Link clock year must be in the range 2000..2099.");

        int year = value.Year - 2000;
        int week = (int)value.DayOfWeek; // Sun=0, Mon=1..Sat=6 matches HostLink encoding directly

        string cmd = $"WRT {year:D2} {value.Month:D2} {value.Day:D2} {value.Hour:D2} {value.Minute:D2} {value.Second:D2} {week}";
        await ExpectOkAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    public async Task ForcedSetAsync(string device, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("ST", addr.DeviceType, KvHostLinkModels.ForceDeviceTypes);
        await ExpectOkAsync($"ST {addr.ToText()}", cancellationToken).ConfigureAwait(false);
    }

    public async Task ForcedResetAsync(string device, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("RS", addr.DeviceType, KvHostLinkModels.ForceDeviceTypes);
        await ExpectOkAsync($"RS {addr.ToText()}", cancellationToken).ConfigureAwait(false);
    }

    public Task<string[]> ReadAsync(string device, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ReadCoreAsync(device, null, 1, false, cancellationToken), cancellationToken);

    public Task<string[]> ReadAsync(string device, string dataFormat, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ReadCoreAsync(device, dataFormat, 1, false, cancellationToken), cancellationToken);

    public Task<string[]> ReadConsecutiveAsync(string device, int count, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ReadCoreAsync(device, null, count, true, cancellationToken), cancellationToken);

    public Task<string[]> ReadConsecutiveAsync(
        string device,
        int count,
        string dataFormat,
        CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ReadCoreAsync(device, dataFormat, count, true, cancellationToken), cancellationToken);

    internal async Task<string[]> ReadCoreAsync(
        string device,
        string? dataFormat,
        int count,
        bool consecutive,
        CancellationToken cancellationToken)
    {
        var address = KvHostLinkDevice.RequireBaseDevice(device);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(address, dataFormat);
        if (consecutive)
            KvHostLinkDevice.ValidateDeviceCount(address.DeviceType, suffix, count);
        else if (count != 1)
            throw new HostLinkProtocolError("A single-device read must request exactly one value.");
        KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix, count);

        var target = address with { Suffix = suffix };
        string command = consecutive ? $"RDS {target.ToText()} {count}" : $"RD {target.ToText()}";
        string response = await SendSemanticCoreAsync(command, cancellationToken).ConfigureAwait(false);
        string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
        int expectedCount = consecutive
            ? count
            : KvHostLinkDevice.ReadResponseTokenCount(address.DeviceType, suffix);
        try
        {
            KvHostLinkProtocol.ValidateResponseTokens(tokens, suffix, expectedCount);
        }
        catch (HostLinkProtocolError)
        {
            InvalidateProtocolState();
            throw;
        }
        return tokens;
    }

    public Task WriteAsync<T>(string device, T value, CancellationToken cancellationToken = default)
        where T : IFormattable
    {
        string command = BuildWriteCommand(device, value, null);
        return ExecuteExclusiveAsync(
            () => ExpectOkCoreAsync(command, cancellationToken),
            cancellationToken);
    }

    /// <summary>Writes one direct bit in one request using the exact Boolean-only bit-value contract.</summary>
    public Task WriteAsync(
        string device,
        bool value,
        CancellationToken cancellationToken = default)
    {
        string command = BuildBitWriteCommand(device, value);
        return ExecuteExclusiveAsync(
            () => ExpectOkCoreAsync(command, cancellationToken),
            cancellationToken);
    }

    public Task WriteAsync<T>(
        string device,
        T value,
        string dataFormat,
        CancellationToken cancellationToken = default)
        where T : IFormattable
    {
        string command = BuildWriteCommand(device, value, dataFormat);
        return ExecuteExclusiveAsync(
            () => ExpectOkCoreAsync(command, cancellationToken),
            cancellationToken);
    }

    private static string BuildWriteCommand<T>(
        string device,
        T value,
        string? dataFormat) where T : IFormattable
    {
        var address = KvHostLinkDevice.RequireBaseDevice(device);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(address, dataFormat);
        KvHostLinkDevice.ValidateDeviceType("WR", address.DeviceType, KvHostLinkModels.WrDeviceTypes);
        KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix);
        string valueText = FormatValue(value, suffix);
        return $"WR {(address with { Suffix = suffix }).ToText()} {valueText}";
    }

    private static string BuildBitWriteCommand(string device, bool value)
    {
        var address = KvHostLinkDevice.RequireBaseDevice(device);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(address, null);
        if (suffix.Length != 0)
            throw new HostLinkProtocolError("Boolean writes require a direct bit device.");
        KvHostLinkDevice.ValidateDeviceType("WR", address.DeviceType, KvHostLinkModels.WrDeviceTypes);
        KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix);
        return $"WR {address.ToText()} {(value ? 1 : 0)}";
    }

    public Task WriteConsecutiveAsync<T>(
        string device,
        IEnumerable<T> values,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        T[] valueSnapshot = values.ToArray();
        string command = BuildWriteConsecutiveCommand(device, valueSnapshot, null);
        return ExecuteExclusiveAsync(
            () => ExpectOkCoreAsync(command, cancellationToken),
            cancellationToken);
    }

    /// <summary>Writes consecutive direct bits in one request from an immutable Boolean-value snapshot.</summary>
    public Task WriteConsecutiveAsync(
        string device,
        IEnumerable<bool> values,
        CancellationToken cancellationToken = default)
    {
        bool[] valueSnapshot = values.ToArray();
        if (valueSnapshot.Length == 0)
            throw new HostLinkProtocolError("values must not be empty");
        var address = KvHostLinkDevice.RequireBaseDevice(device);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(address, null);
        if (suffix.Length != 0)
            throw new HostLinkProtocolError("Boolean writes require a direct bit device.");
        KvHostLinkDevice.ValidateDeviceType("WRS", address.DeviceType, KvHostLinkModels.WrDeviceTypes);
        KvHostLinkDevice.ValidateDeviceCount(address.DeviceType, suffix, valueSnapshot.Length);
        KvHostLinkDevice.ValidateDeviceSpan(
            address.DeviceType,
            address.Number,
            suffix,
            valueSnapshot.Length);
        string payload = string.Join(' ', valueSnapshot.Select(static value => value ? "1" : "0"));
        string command = $"WRS {address.ToText()} {valueSnapshot.Length} {payload}";
        return ExecuteExclusiveAsync(
            () => ExpectOkCoreAsync(command, cancellationToken),
            cancellationToken);
    }

    public Task WriteConsecutiveAsync<T>(
        string device,
        IEnumerable<T> values,
        string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        T[] valueSnapshot = values.ToArray();
        string command = BuildWriteConsecutiveCommand(device, valueSnapshot, dataFormat);
        return ExecuteExclusiveAsync(
            () => ExpectOkCoreAsync(command, cancellationToken),
            cancellationToken);
    }

    private static string BuildWriteConsecutiveCommand<T>(
        string device,
        IEnumerable<T> values,
        string? dataFormat) where T : IFormattable
    {
        var valueList = values.ToList();
        if (valueList.Count == 0)
            throw new HostLinkProtocolError("values must not be empty");

        var address = KvHostLinkDevice.RequireBaseDevice(device);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(address, dataFormat);
        KvHostLinkDevice.ValidateDeviceType("WRS", address.DeviceType, KvHostLinkModels.WrDeviceTypes);
        KvHostLinkDevice.ValidateDeviceCount(address.DeviceType, suffix, valueList.Count);
        KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix, valueList.Count);
        string payload = BuildValuePayload(valueList, suffix);
        return $"WRS {(address with { Suffix = suffix }).ToText()} {valueList.Count} {payload}";
    }

    public Task RegisterMonitorBitsAsync(
        IEnumerable<string> devices,
        CancellationToken cancellationToken = default)
    {
        string[] targets = devices.ToArray();
        if (targets.Length == 0) throw new HostLinkProtocolError("At least one device is required");
        if (targets.Length > 120) throw new HostLinkProtocolError("Maximum 120 devices can be registered");

        var command = new StringBuilder("MBS");
        foreach (var device in targets)
        {
            var address = KvHostLinkDevice.RequireBaseDevice(device);
            KvHostLinkDevice.ValidateDeviceType("MBS", address.DeviceType, KvHostLinkModels.MbsDeviceTypes);
            command.Append(' ');
            command.Append(address.ToText());
        }
        string commandSnapshot = command.ToString();
        return ExecuteExclusiveAsync(async () =>
        {
            await ExpectOkCoreAsync(commandSnapshot, cancellationToken).ConfigureAwait(false);
            _monitorBitCount = targets.Length;
        }, cancellationToken);
    }

    public Task RegisterMonitorWordsAsync(
        IEnumerable<KvMonitorWordTarget> devices,
        CancellationToken cancellationToken = default)
    {
        KvMonitorWordTarget[] targets = devices.ToArray();
        if (targets.Length == 0) throw new HostLinkProtocolError("At least one device is required");
        if (targets.Length > 120) throw new HostLinkProtocolError("Maximum 120 devices can be registered");

        var command = new StringBuilder("MWS");
        var formats = new List<string>(targets.Length);
        foreach (var target in targets)
        {
            ArgumentNullException.ThrowIfNull(target);
            var address = KvHostLinkDevice.RequireBaseDevice(target.Device);
            KvHostLinkDevice.ValidateDeviceType("MWS", address.DeviceType, KvHostLinkModels.MwsDeviceTypes);
            string suffix = KvHostLinkDevice.RequireExplicitFormat(address, target.DataFormat);
            KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix);
            command.Append(' ');
            command.Append((address with { Suffix = suffix }).ToText());
            formats.Add(suffix);
        }
        string commandSnapshot = command.ToString();
        string[] formatSnapshot = formats.ToArray();
        return ExecuteExclusiveAsync(async () =>
        {
            await ExpectOkCoreAsync(commandSnapshot, cancellationToken).ConfigureAwait(false);
            _monitorWordFormats = formatSnapshot;
        }, cancellationToken);
    }

    public Task<string[]> ReadMonitorBitsAsync(CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            if (_monitorBitCount == 0)
                throw new HostLinkProtocolError("Monitor bits must be registered before reading them.");
            string response = await SendSemanticCoreAsync("MBR", cancellationToken).ConfigureAwait(false);
            string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
            try
            {
                KvHostLinkProtocol.ValidateResponseTokens(tokens, "", _monitorBitCount);
            }
            catch (HostLinkProtocolError)
            {
                InvalidateProtocolState();
                throw;
            }
            return tokens;
        }, cancellationToken);

    public Task<string[]> ReadMonitorWordsAsync(CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            if (_monitorWordFormats.Length == 0)
                throw new HostLinkProtocolError("Monitor words must be registered before reading them.");
            string response = await SendSemanticCoreAsync("MWR", cancellationToken).ConfigureAwait(false);
            string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
            try
            {
                if (tokens.Length != _monitorWordFormats.Length)
                    throw new HostLinkProtocolError(
                        $"Response contained {tokens.Length} values; expected {_monitorWordFormats.Length}.");
                for (int index = 0; index < tokens.Length; index++)
                    KvHostLinkProtocol.ValidateResponseTokens([tokens[index]], _monitorWordFormats[index], 1);
            }
            catch (HostLinkProtocolError)
            {
                InvalidateProtocolState();
                throw;
            }
            return tokens;
        }, cancellationToken);

    /// <summary>Consecutively force-sets up to 16 bit devices starting at <paramref name="device"/> (STS command).</summary>
    public async Task ForcedSetConsecutiveAsync(
        string device, int count, CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(count), "count must be 1-16.");
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("STS", addr.DeviceType, KvHostLinkModels.ForceConsecutiveDeviceTypes);
        await ExpectOkAsync($"STS {addr.ToText()} {count}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Consecutively force-resets up to 16 bit devices starting at <paramref name="device"/> (RSS command).</summary>
    public async Task ForcedResetConsecutiveAsync(
        string device, int count, CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(count), "count must be 1-16.");
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("RSS", addr.DeviceType, KvHostLinkModels.ForceConsecutiveDeviceTypes);
        await ExpectOkAsync($"RSS {addr.ToText()} {count}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads consecutive devices using the legacy RDE command.
    /// Prefer <see cref="ReadConsecutiveAsync(string, int, string, CancellationToken)"/> on current models.
    /// </summary>
    public Task<string[]> ReadConsecutiveLegacyAsync(
        string device, int count, string dataFormat, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            var addr = KvHostLinkDevice.RequireBaseDevice(device);
            string effectiveFormat = KvHostLinkDevice.RequireExplicitFormat(addr, dataFormat);
            KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, effectiveFormat, count);
            KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, effectiveFormat, count);
            var target = addr with { Suffix = effectiveFormat };
            string response = await SendSemanticCoreAsync($"RDE {target.ToText()} {count}", cancellationToken)
                .ConfigureAwait(false);
            string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
            try
            {
                KvHostLinkProtocol.ValidateResponseTokens(tokens, effectiveFormat, count);
            }
            catch (HostLinkProtocolError)
            {
                InvalidateProtocolState();
                throw;
            }
            return tokens;
        }, cancellationToken);

    /// <summary>
    /// Writes consecutive devices using the legacy WRE command.
    /// Prefer <see cref="WriteConsecutiveAsync{T}(string, IEnumerable{T}, string, CancellationToken)"/> on current models.
    /// </summary>
    public async Task WriteConsecutiveLegacyAsync<T>(
        string device, IEnumerable<T> values, string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        string effectiveFormat = KvHostLinkDevice.RequireExplicitFormat(addr, dataFormat);
        KvHostLinkDevice.ValidateDeviceType("WRE", addr.DeviceType, KvHostLinkModels.WrDeviceTypes);
        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, effectiveFormat, valList.Count);
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, effectiveFormat, valList.Count);
        var target = addr with { Suffix = effectiveFormat };
        string payload = BuildValuePayload(valList, effectiveFormat);
        await ExpectOkAsync($"WRE {target.ToText()} {valList.Count} {payload}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a set-value (preset) for a timer or counter device (WS command).
    /// Supported device types: T, C.
    /// </summary>
    public async Task WriteSetValueAsync<T>(
        string device, T value, string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("WS", addr.DeviceType, KvHostLinkModels.WsDeviceTypes);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(addr, dataFormat);
        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, suffix, 1);
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, suffix);
        var target = addr with { Suffix = suffix };
        string valStr = FormatValue(value, suffix);
        await ExpectOkAsync($"WS {target.ToText()} {valStr}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes set-values (presets) for consecutive timer or counter devices (WSS command).
    /// Supported device types: T, C.
    /// </summary>
    public async Task WriteSetValueConsecutiveAsync<T>(
        string device, IEnumerable<T> values, string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("WSS", addr.DeviceType, KvHostLinkModels.WsDeviceTypes);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(addr, dataFormat);
        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, suffix, valList.Count);
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, suffix, valList.Count);
        var target = addr with { Suffix = suffix };
        string payload = BuildValuePayload(valList, suffix);
        await ExpectOkAsync($"WSS {target.ToText()} {valList.Count} {payload}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Switches the active data bank (BE command). Valid range: 0–15.</summary>
    public async Task SwitchBankAsync(int bankNo, CancellationToken cancellationToken = default)
    {
        if (bankNo is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bankNo), "bankNo must be 0-15.");
        await ExpectOkAsync($"BE {bankNo}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads buffer memory from an expansion unit (URD command).
    /// </summary>
    /// <param name="unitNo">Unit number (0–48).</param>
    /// <param name="address">Buffer address (0–59999).</param>
    /// <param name="count">Number of values to read.</param>
    /// <param name="dataFormat">Required data format suffix, e.g. ".U" or ".S".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<string[]> ReadExpansionUnitBufferAsync(
        int unitNo, int address, int count,
        string dataFormat,
        CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            if (unitNo is < 0 or > 48)
                throw new ArgumentOutOfRangeException(nameof(unitNo), "unitNo must be 0-48.");
            if (address is < 0 or > 59999)
                throw new ArgumentOutOfRangeException(nameof(address), "address must be 0-59999.");

            if (string.IsNullOrWhiteSpace(dataFormat))
                throw new HostLinkProtocolError("dataFormat is required.");
            string suffix = KvHostLinkDevice.NormalizeSuffix(dataFormat);
            KvHostLinkDevice.ValidateExpansionBufferCount(suffix, count);
            KvHostLinkDevice.ValidateExpansionBufferSpan(address, suffix, count);

            string cmd = $"URD {unitNo:D2} {address}{suffix} {count}";
            string response = await SendSemanticCoreAsync(cmd, cancellationToken).ConfigureAwait(false);
            string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
            try
            {
                KvHostLinkProtocol.ValidateResponseTokens(tokens, suffix, count);
            }
            catch (HostLinkProtocolError)
            {
                InvalidateProtocolState();
                throw;
            }
            return tokens;
        }, cancellationToken);

    /// <summary>
    /// Writes buffer memory to an expansion unit (UWR command).
    /// </summary>
    /// <param name="unitNo">Unit number (0–48).</param>
    /// <param name="address">Buffer address (0–59999).</param>
    /// <param name="values">Values to write.</param>
    /// <param name="dataFormat">Required data format suffix, e.g. ".U" or ".S".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WriteExpansionUnitBufferAsync<T>(
        int unitNo, int address, IEnumerable<T> values,
        string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");
        if (unitNo is < 0 or > 48)
            throw new ArgumentOutOfRangeException(nameof(unitNo), "unitNo must be 0-48.");
        if (address is < 0 or > 59999)
            throw new ArgumentOutOfRangeException(nameof(address), "address must be 0-59999.");

        if (string.IsNullOrWhiteSpace(dataFormat))
            throw new HostLinkProtocolError("dataFormat is required.");
        string suffix = KvHostLinkDevice.NormalizeSuffix(dataFormat);
        KvHostLinkDevice.ValidateExpansionBufferCount(suffix, valList.Count);
        KvHostLinkDevice.ValidateExpansionBufferSpan(address, suffix, valList.Count);

        string payload = BuildValuePayload(valList, suffix);
        string cmd = $"UWR {unitNo:D2} {address}{suffix} {valList.Count} {payload}";
        await ExpectOkAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Reads one RDC device comment as exact response-body bytes.</summary>
    /// <param name="device">Base device whose comment is read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// The exact RDC payload after the Host Link CR/LF frame terminator is removed.
    /// Trailing ASCII padding spaces are retained.
    /// </returns>
    public Task<byte[]> ReadCommentBytesAsync(
        string device,
        CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(
            () => ReadCommentBytesCoreAsync(device, cancellationToken),
            cancellationToken);

    /// <summary>Reads and strictly decodes one RDC device comment.</summary>
    /// <param name="device">Base device whose comment is read.</param>
    /// <param name="encoding">
    /// Explicit text encoding. <see cref="HostLinkCommentEncoding.Cp932"/> is
    /// CP932/Windows-31J and is the compatibility selection for KEYENCE material
    /// that describes text as Shift_JIS.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Decoded comment text with trailing ASCII padding spaces removed.</returns>
    /// <remarks>
    /// Decoding is strict. Malformed input raises <see cref="HostLinkProtocolError"/>
    /// and retires the connection; the library never guesses or falls back to another encoding.
    /// </remarks>
    public Task<string> ReadCommentsAsync(
        string device,
        HostLinkCommentEncoding encoding,
        CancellationToken cancellationToken = default)
    {
        KvHostLinkProtocol.ValidateCommentEncoding(encoding);
        return ExecuteExclusiveAsync(
            () => ReadCommentsCoreAsync(device, encoding, cancellationToken),
            cancellationToken);
    }

    internal async Task<byte[]> ReadCommentBytesCoreAsync(
        string device,
        CancellationToken cancellationToken)
    {
        var address = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("RDC", address.DeviceType, KvHostLinkModels.RdcDeviceTypes);
        byte[] frame = KvHostLinkProtocol.BuildFrame($"RDC {address.ToText()}");
        byte[] response = await SendRawCoreAsync(
            frame,
            stateChanging: false,
            cancellationToken).ConfigureAwait(false);
        KvHostLinkProtocol.EnsureCommentSuccess(response);
        return response;
    }

    internal async Task<string> ReadCommentsCoreAsync(
        string device,
        HostLinkCommentEncoding encoding,
        CancellationToken cancellationToken)
    {
        KvHostLinkProtocol.ValidateCommentEncoding(encoding);
        byte[] response = await ReadCommentBytesCoreAsync(device, cancellationToken).ConfigureAwait(false);
        try
        {
            return KvHostLinkProtocol.DecodeCommentResponse(response, encoding);
        }
        catch (HostLinkProtocolError)
        {
            CloseTransport();
            throw;
        }
    }

    private static string BuildValuePayload<T>(List<T> values, string dataFormat) where T : IFormattable
    {
        var sb = new StringBuilder();
        foreach (var v in values)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(FormatValue(v, dataFormat));
        }
        return sb.ToString();
    }

    private static string FormatValue<T>(T value, string dataFormat) where T : IFormattable
    {
        object boxed = value;
        if (dataFormat.Length == 0)
        {
            throw new HostLinkProtocolError(
                "Direct bit writes require a Boolean value; numeric 0/1 compatibility inputs are not accepted.");
        }
        if (boxed is not byte and not sbyte and not short and not ushort and not int and not uint and not long and not ulong)
            throw new HostLinkProtocolError("Host Link numeric writes require an integral CLR value.");

        if (boxed is ulong unsignedValue)
        {
            if (dataFormat is ".S" or ".L" || unsignedValue > uint.MaxValue)
                throw new HostLinkProtocolError($"Value {unsignedValue} is out of range for data format '{dataFormat}'.");
            return FormatUnsigned(unsignedValue, dataFormat);
        }

        if (boxed is uint uintValue)
            return FormatUnsigned(uintValue, dataFormat);
        if (boxed is ushort ushortValue)
            return FormatUnsigned(ushortValue, dataFormat);
        if (boxed is byte byteValue)
            return FormatUnsigned(byteValue, dataFormat);

        long signedValue = Convert.ToInt64(boxed, CultureInfo.InvariantCulture);
        return FormatSigned(signedValue, dataFormat);
    }

    private static string FormatUnsigned(ulong value, string dataFormat)
        => dataFormat switch
        {
            ".U" when value <= ushort.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".D" when value <= uint.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".H" when value <= ushort.MaxValue => value.ToString("X", CultureInfo.InvariantCulture),
            ".S" when value <= (ulong)short.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".L" when value <= int.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            _ => throw new HostLinkProtocolError($"Value {value} is out of range for data format '{dataFormat}'."),
        };

    private static string FormatSigned(long value, string dataFormat)
        => dataFormat switch
        {
            ".U" when value is >= ushort.MinValue and <= ushort.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".S" when value is >= short.MinValue and <= short.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".D" when value is >= uint.MinValue and <= uint.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".L" when value is >= int.MinValue and <= int.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".H" when value is >= ushort.MinValue and <= ushort.MaxValue => value.ToString("X", CultureInfo.InvariantCulture),
            _ => throw new HostLinkProtocolError($"Value {value} is out of range for data format '{dataFormat}'."),
        };
}
