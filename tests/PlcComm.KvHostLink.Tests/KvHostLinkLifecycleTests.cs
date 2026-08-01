using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkLifecycleTests
{
    private const string TestProfile = "keyence:kv-8000";

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task TcpCallerCancellationWinsBeforeConfiguredTimeout()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
            received.SetResult();
            int trailingBytes = await stream.ReadAsync(new byte[1]);
            Assert.Equal(0, trailingBytes);
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile)
        {
            Timeout = TimeSpan.FromSeconds(5),
        };
        using var cancellation = new CancellationTokenSource();
        await client.OpenAsync();
        Task request = client.SendRawAsync("CANCEL", cancellation.Token);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var error = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => request);
        Assert.Equal(HostLinkOutcomeUnknownReason.CallerCancellation, error.Reason);
        Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(error.InnerException).CancellationToken);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task TcpCloseAsyncInterruptsPendingReceiveAndCanReopen()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var firstReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using (TcpClient first = await listener.AcceptTcpClientAsync())
            {
                NetworkStream stream = first.GetStream();
                while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
                firstReceived.SetResult();
                int trailingBytes = await stream.ReadAsync(new byte[1]);
                Assert.Equal(0, trailingBytes);
            }

            using TcpClient second = await listener.AcceptTcpClientAsync();
            NetworkStream secondStream = second.GetStream();
            while (secondStream.ReadByte() is int value && value >= 0 && value != '\r') { }
            await secondStream.WriteAsync("OK\r"u8.ToArray());
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile)
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        await client.OpenAsync();
        Task<byte[]> request = client.SendRawAsync("BLOCK");
        await firstReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<byte[]> waiting = client.SendRawAsync("MUST-NOT-SEND");

        var stopwatch = Stopwatch.StartNew();
        await client.CloseAsync().WaitAsync(TimeSpan.FromSeconds(2));
        stopwatch.Stop();
        var activeError = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => request);
        var waitingError = await Assert.ThrowsAsync<HostLinkClosedError>(() => waiting);
        Assert.Equal(HostLinkOutcomeUnknownReason.ConnectionClosed, activeError.Reason);
        Assert.IsType<HostLinkClosedError>(activeError.InnerException);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.False(client.IsOpen);

        await client.CloseAsync();
        await client.OpenAsync();
        Assert.Equal("OK"u8.ToArray(), await client.SendRawAsync("AGAIN"));
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task UdpCloseAsyncInterruptsPendingReceive()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            await server.ReceiveAsync();
            received.SetResult();
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Udp, TestProfile)
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        await client.OpenAsync();
        Task<byte[]> request = client.SendRawAsync("BLOCK");
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await client.CloseAsync().WaitAsync(TimeSpan.FromSeconds(2));
        var error = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => request);
        Assert.Equal(HostLinkOutcomeUnknownReason.ConnectionClosed, error.Reason);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task NormalClientCloseInterruptsActiveIoAndRejectsQueuedWork()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
            received.SetResult();
            int trailingBytes = await stream.ReadAsync(new byte[1]);
            Assert.Equal(0, trailingBytes);
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile)
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        await client.OpenAsync();
        Task<byte[]> active = client.SendRawAsync("BLOCK");
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<string[]> waiting = client.ReadAsync("DM0", ".U");

        await client.CloseAsync().WaitAsync(TimeSpan.FromSeconds(2));

        var activeError = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => active);
        await Assert.ThrowsAsync<HostLinkClosedError>(() => waiting);
        Assert.Equal(HostLinkOutcomeUnknownReason.ConnectionClosed, activeError.Reason);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    public async Task ConcurrentCloseAndDisposeCallsAreIdempotent()
    {
        var client = new KvHostLinkClient(
            "127.0.0.1", 8501, HostLinkTransportMode.Tcp, TestProfile);

        await Task.WhenAll(client.CloseAsync(), client.CloseAsync());
        await Task.WhenAll(client.DisposeAsync().AsTask(), client.DisposeAsync().AsTask());

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.OpenAsync());
        await client.CloseAsync();
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task StateChangingTimeoutWinsBeforeLaterCallerCancellation()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
            int trailingBytes = await stream.ReadAsync(new byte[1]);
            Assert.Equal(0, trailingBytes);
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile)
        {
            Timeout = TimeSpan.FromMilliseconds(50),
        };
        using var callerCancellation = new CancellationTokenSource();
        await client.OpenAsync();

        var error = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(
            () => client.SendRawAsync("TIMEOUT", callerCancellation.Token));
        Assert.Equal(HostLinkOutcomeUnknownReason.Timeout, error.Reason);
        Assert.IsType<HostLinkTimeoutError>(error.InnerException);

        Assert.False(callerCancellation.IsCancellationRequested);
        callerCancellation.Cancel();
        Assert.True(callerCancellation.IsCancellationRequested);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task TcpConnectFailureRetiresCandidateAndLaterOpenCanConnect()
    {
        var portReservation = new TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        int port = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile)
        {
            Timeout = TimeSpan.FromMilliseconds(250),
        };

        Exception connectError = await Record.ExceptionAsync(() => client.OpenAsync());
        Assert.True(
            connectError is SocketException or HostLinkTimeoutError,
            $"Expected connection refusal or a bounded connect timeout, got {connectError.GetType().FullName}.");
        Assert.False(client.IsOpen);

        var listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        try
        {
            Task<TcpClient> acceptedTask = listener.AcceptTcpClientAsync();
            await client.OpenAsync();
            using TcpClient accepted = await acceptedTask.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(client.IsOpen);
            await client.CloseAsync();
            Assert.False(client.IsOpen);
        }
        finally
        {
            listener.Stop();
        }
    }
}
