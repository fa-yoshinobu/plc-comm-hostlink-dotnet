using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkLifecycleTests
{
    private const string TestProfile = "keyence:kv-8000";

    [Fact]
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

        var error = await Assert.ThrowsAsync<OperationCanceledException>(() => request);
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.IsNotType<HostLinkTimeoutError>(error);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
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
        var activeError = await Assert.ThrowsAsync<HostLinkConnectionError>(() => request);
        var waitingError = await Assert.ThrowsAsync<HostLinkConnectionError>(() => waiting);
        Assert.IsNotType<HostLinkTimeoutError>(activeError);
        Assert.IsNotType<HostLinkTimeoutError>(waitingError);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
        Assert.False(client.IsOpen);

        await client.CloseAsync();
        await client.OpenAsync();
        Assert.Equal("OK"u8.ToArray(), await client.SendRawAsync("AGAIN"));
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
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
        var error = await Assert.ThrowsAsync<HostLinkConnectionError>(() => request);
        Assert.IsNotType<HostLinkTimeoutError>(error);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task QueuedCloseInterruptsActiveIoAndRejectsQueuedWork()
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

        using var inner = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile)
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        await using var queued = new QueuedKvHostLinkClient(inner);
        await queued.OpenAsync();
        Task<byte[]> active = queued.SendRawAsync("BLOCK");
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<int> waiting = queued.ExecuteAsync(_ => Task.FromResult(1));

        await queued.CloseAsync().WaitAsync(TimeSpan.FromSeconds(2));

        var activeError = await Assert.ThrowsAsync<HostLinkConnectionError>(() => active);
        var waitingError = await Assert.ThrowsAsync<HostLinkConnectionError>(() => waiting);
        Assert.IsNotType<HostLinkTimeoutError>(activeError);
        Assert.IsNotType<HostLinkTimeoutError>(waitingError);
        Assert.False(queued.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    public async Task ConcurrentCloseAndDisposeCallsAreIdempotent()
    {
        var client = new KvHostLinkClient(
            "127.0.0.1", 8501, HostLinkTransportMode.Tcp, TestProfile);
        var queued = new QueuedKvHostLinkClient(client);

        await Task.WhenAll(queued.CloseAsync(), queued.CloseAsync());
        await Task.WhenAll(queued.DisposeAsync().AsTask(), queued.DisposeAsync().AsTask());

        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued.OpenAsync());
        await queued.CloseAsync();
    }

    [Fact]
    public async Task QueuedTimeoutWinsBeforeLaterCallerCancellation()
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

        using var inner = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile)
        {
            Timeout = TimeSpan.FromMilliseconds(50),
        };
        await using var queued = new QueuedKvHostLinkClient(inner);
        using var callerCancellation = new CancellationTokenSource();
        await queued.OpenAsync();

        await Assert.ThrowsAsync<HostLinkTimeoutError>(
            () => queued.SendRawAsync("TIMEOUT", callerCancellation.Token));

        Assert.False(callerCancellation.IsCancellationRequested);
        callerCancellation.Cancel();
        Assert.True(callerCancellation.IsCancellationRequested);
        Assert.False(queued.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }
}
