using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkTransportTests
{
    [Fact]
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

        await using var client = new KvHostLinkClient("127.0.0.1", "keyence:kv-8000", port)
        {
            Timeout = TimeSpan.FromSeconds(2)
        };
        var error = await Assert.ThrowsAsync<HostLinkConnectionError>(() => client.SendRawAsync("READ"));

        Assert.Contains("before the response terminator", error.Message, StringComparison.Ordinal);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
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
            "keyence:kv-8000",
            port,
            HostLinkTransportMode.Udp)
        {
            Timeout = TimeSpan.FromMilliseconds(50)
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendRawAsync("FIRST"));
        Assert.False(client.IsOpen);

        client.Timeout = TimeSpan.FromSeconds(2);
        Assert.Equal("SECOND", await client.SendRawAsync("SECOND"));
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
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
            "keyence:kv-8000",
            port,
            HostLinkTransportMode.Udp)
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        using var cancellation = new CancellationTokenSource();
        Task request = client.SendRawAsync("CANCEL", cancellation.Token);
        await received.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
