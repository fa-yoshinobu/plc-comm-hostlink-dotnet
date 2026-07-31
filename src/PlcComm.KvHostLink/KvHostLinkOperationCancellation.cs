namespace PlcComm.KvHostLink;

internal enum KvHostLinkCancellationOrigin
{
    None,
    Caller,
    Timeout,
    Lifetime,
}

internal sealed class KvHostLinkOperationCancellation : IDisposable
{
    private readonly CancellationToken _callerToken;
    private readonly CancellationToken _lifetimeToken;
    private readonly TimeSpan? _timeout;
    private readonly CancellationTokenSource? _timeoutSource;
    private readonly CancellationTokenSource _linkedSource;
    private readonly CancellationTokenRegistration _callerRegistration;
    private readonly CancellationTokenRegistration _lifetimeRegistration;
    private readonly CancellationTokenRegistration _timeoutRegistration;
    private int _origin;

    internal KvHostLinkOperationCancellation(
        CancellationToken callerToken,
        CancellationToken lifetimeToken,
        TimeSpan? timeout = null)
    {
        _callerToken = callerToken;
        _lifetimeToken = lifetimeToken;
        _timeout = timeout;

        _callerRegistration = callerToken.Register(
            static state => ((KvHostLinkOperationCancellation)state!).SetOrigin(KvHostLinkCancellationOrigin.Caller),
            this);
        _lifetimeRegistration = lifetimeToken.Register(
            static state => ((KvHostLinkOperationCancellation)state!).SetOrigin(KvHostLinkCancellationOrigin.Lifetime),
            this);

        if (timeout.HasValue)
        {
            _timeoutSource = new CancellationTokenSource();
            _timeoutRegistration = _timeoutSource.Token.Register(
                static state => ((KvHostLinkOperationCancellation)state!).SetOrigin(KvHostLinkCancellationOrigin.Timeout),
                this);
            _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                callerToken,
                lifetimeToken,
                _timeoutSource.Token);
            _timeoutSource.CancelAfter(timeout.Value);
        }
        else
        {
            _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(callerToken, lifetimeToken);
        }
    }

    internal CancellationToken Token => _linkedSource.Token;

    internal Exception Translate(Exception error, string operation)
    {
        var origin = (KvHostLinkCancellationOrigin)Volatile.Read(ref _origin);
        if (origin == KvHostLinkCancellationOrigin.None)
        {
            if (_callerToken.IsCancellationRequested)
                origin = KvHostLinkCancellationOrigin.Caller;
            else if (_lifetimeToken.IsCancellationRequested)
                origin = KvHostLinkCancellationOrigin.Lifetime;
            else if (_timeoutSource?.IsCancellationRequested == true)
                origin = KvHostLinkCancellationOrigin.Timeout;
        }

        return origin switch
        {
            KvHostLinkCancellationOrigin.Caller => new OperationCanceledException(
                $"Host Link {operation} was cancelled by the caller.",
                error,
                _callerToken),
            KvHostLinkCancellationOrigin.Timeout => new HostLinkTimeoutError(
                $"Host Link {operation} timed out after {_timeout!.Value.TotalMilliseconds:0} ms.",
                error),
            KvHostLinkCancellationOrigin.Lifetime => new HostLinkClosedError(
                $"Host Link {operation} was interrupted because the connection was closed.",
                error),
            _ => error,
        };
    }

    private void SetOrigin(KvHostLinkCancellationOrigin origin)
        => Interlocked.CompareExchange(ref _origin, (int)origin, (int)KvHostLinkCancellationOrigin.None);

    public void Dispose()
    {
        _linkedSource.Dispose();
        _timeoutRegistration.Dispose();
        _timeoutSource?.Dispose();
        _lifetimeRegistration.Dispose();
        _callerRegistration.Dispose();
    }
}
