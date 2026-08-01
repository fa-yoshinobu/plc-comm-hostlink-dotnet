using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkTransportTests
{
    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task TcpEofBeforeTerminatorRejectsPartialResponseAndClosesTransport()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
            await stream.WriteAsync(Encoding.ASCII.GetBytes("PARTIAL"));
            await stream.FlushAsync();
            accepted.Client.Shutdown(SocketShutdown.Send);
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, "keyence:kv-8000")
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        await client.OpenAsync();
        var error = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => client.SendRawAsync("READ"));

        Assert.Equal(HostLinkOutcomeUnknownReason.TransportFailure, error.Reason);
        Assert.Contains("before the response terminator", error.InnerException!.Message, StringComparison.Ordinal);
        Assert.False(client.IsOpen);
        Assert.Equal(0UL, client.TrafficStats.RxBytes);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task TcpTimeoutDoesNotCountAnIncompleteResponse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
            await Task.Delay(250);
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, "keyence:kv-8000")
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };
        await client.OpenAsync();

        var error = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => client.SendRawAsync("READ"));
        Assert.Equal(HostLinkOutcomeUnknownReason.Timeout, error.Reason);
        Assert.IsType<HostLinkTimeoutError>(error.InnerException);
        Assert.Equal(0UL, client.TrafficStats.RxBytes);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task TcpConsumesCrThatArrivesAfterAnLfTerminatedResponse()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            for (int request = 0; request < 2; request++)
            {
                while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
                byte[] response = request == 0
                    ? "FIRST\n"u8.ToArray()
                    : "\rSECOND\r"u8.ToArray();
                await stream.WriteAsync(response);
                await stream.FlushAsync();
            }
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, "keyence:kv-8000");
        await client.OpenAsync();

        Assert.Equal("FIRST"u8.ToArray(), await client.SendRawAsync("ONE"));
        Assert.Equal("SECOND"u8.ToArray(), await client.SendRawAsync("TWO"));
        Assert.Equal(13UL, client.TrafficStats.RxBytes);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    public async Task TcpRejectsExtraNonEmptyResponseBufferedAfterTerminator()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
            await stream.WriteAsync("1\r2\r"u8.ToArray());
            await stream.FlushAsync();
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, "keyence:kv-8000");
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync("DM0", ".U"));
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    public async Task TcpRejectsDelayedUnownedResponseBeforeSendingNextCommand()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var extraSent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
            await stream.WriteAsync("1\r"u8.ToArray());
            await stream.FlushAsync();
            await Task.Delay(20);
            await stream.WriteAsync("2\r"u8.ToArray());
            await stream.FlushAsync();
            extraSent.SetResult();

            using var noSecondRequest = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            var buffer = new byte[1];
            try
            {
                return await stream.ReadAsync(buffer, noSecondRequest.Token) != 0;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, "keyence:kv-8000");
        await client.OpenAsync();

        Assert.Equal(["1"], await client.ReadAsync("DM0", ".U"));
        await extraSent.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(20);
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteAsync("DM1", 2, ".U"));
        Assert.False(client.IsOpen);
        Assert.False(await serverTask.WaitAsync(TimeSpan.FromSeconds(5)));
        listener.Stop();
    }

    [Fact]
    public async Task TcpOversizePartialResponseDoesNotIncrementReceiveStats()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
            await stream.WriteAsync(new byte[65_537]);
            await stream.FlushAsync();
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, "keyence:kv-8000");
        await client.OpenAsync();

        var error = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => client.SendRawAsync("READ"));
        Assert.Equal(HostLinkOutcomeUnknownReason.InvalidResponse, error.Reason);
        Assert.IsType<HostLinkProtocolError>(error.InnerException);
        Assert.Equal(0UL, client.TrafficStats.RxBytes);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task UdpTimeoutClosesTransportAndDelayedResponseIsNotReused()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;

        var serverTask = Task.Run(async () =>
        {
            UdpReceiveResult first = await server.ReceiveAsync();
            await Task.Delay(150);
            byte[] stale = Encoding.ASCII.GetBytes("FIRST\r");
            await server.SendAsync(stale, stale.Length, first.RemoteEndPoint);

            UdpReceiveResult second = await server.ReceiveAsync();
            byte[] current = Encoding.ASCII.GetBytes("SECOND\r");
            await server.SendAsync(current, current.Length, second.RemoteEndPoint);
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1",
            port,
            HostLinkTransportMode.Udp,
            "keyence:kv-8000")
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        await client.OpenAsync();
        var timeout = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => client.SendRawAsync("FIRST"));
        Assert.Equal(HostLinkOutcomeUnknownReason.Timeout, timeout.Reason);
        Assert.IsType<HostLinkTimeoutError>(timeout.InnerException);
        Assert.False(client.IsOpen);
        Assert.Equal(new HostLinkTrafficStats(1, 6, 0), client.TrafficStats);

        client.Timeout = TimeSpan.FromSeconds(2);
        await Assert.ThrowsAsync<HostLinkNotConnectedError>(() => client.SendRawAsync("SECOND"));
        await client.OpenAsync();
        Assert.Equal(Encoding.ASCII.GetBytes("SECOND"), await client.SendRawAsync("SECOND"));
        Assert.Equal(new HostLinkTrafficStats(2, 13, 7), client.TrafficStats);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task UdpCancellationClosesTransport()
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
            "127.0.0.1",
            port,
            HostLinkTransportMode.Udp,
            "keyence:kv-8000")
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        using var cancellation = new CancellationTokenSource();
        await client.OpenAsync();
        Task request = client.SendRawAsync("CANCEL", cancellation.Token);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        var error = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => request);
        Assert.Equal(HostLinkOutcomeUnknownReason.CallerCancellation, error.Reason);
        Assert.IsType<OperationCanceledException>(error.InnerException);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task UdpUsesDedicatedSocketGenerationForEachSuccessfulRequest()
    {
        using var server = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)server.Client.LocalEndPoint!).Port;
        var endpoints = new List<IPEndPoint>();
        var secondReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSecondResponse = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var serverTask = Task.Run(async () =>
        {
            UdpReceiveResult first = await server.ReceiveAsync();
            endpoints.Add(first.RemoteEndPoint);
            await server.SendAsync("FIRST\r"u8.ToArray(), first.RemoteEndPoint);

            UdpReceiveResult second = await server.ReceiveAsync();
            endpoints.Add(second.RemoteEndPoint);
            secondReceived.SetResult();
            await releaseSecondResponse.Task;
            await server.SendAsync("STALE\r"u8.ToArray(), first.RemoteEndPoint);
            await server.SendAsync("SECOND\r"u8.ToArray(), second.RemoteEndPoint);
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Udp, "keyence:kv-8000");
        await client.OpenAsync();

        Assert.Equal("FIRST"u8.ToArray(), await client.SendRawAsync("ONE"));
        Assert.True(client.IsOpen);
        using (var duplicateBind = new UdpClient(AddressFamily.InterNetwork))
        {
            Assert.Throws<SocketException>(() =>
                duplicateBind.Client.Bind(new IPEndPoint(IPAddress.Any, endpoints[0].Port)));
        }

        Task<byte[]> secondRequest = client.SendRawAsync("TWO");
        await secondReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotEqual(endpoints[0].Port, endpoints[1].Port);
        using var releasedPredecessorPort = new UdpClient(
            new IPEndPoint(IPAddress.Any, endpoints[0].Port));
        releaseSecondResponse.SetResult();

        Assert.Equal("SECOND"u8.ToArray(), await secondRequest);
        Assert.True(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(2, endpoints.Count);
        Assert.NotEqual(endpoints[0].Port, endpoints[1].Port);
    }

    [Fact]
    public async Task CommandBeforeExplicitOpenFailsWithoutConnecting()
    {
        await using var client = new KvHostLinkClient(
            "invalid.invalid", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");

        await Assert.ThrowsAsync<HostLinkNotConnectedError>(() => client.SendRawAsync("?K"));
        Assert.False(client.IsOpen);
    }
}
