using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace PlcComm.KvHostLink.Tests;

public sealed class PerformanceOptimizationTests
{
    private static FieldInfo Field(string name) =>
        typeof(KvHostLinkClient).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException($"Missing private performance-contract field {name}.");

    private static T? Value<T>(KvHostLinkClient client, string name) => (T?)Field(name).GetValue(client);

    [Fact]
    public async Task TransportBuffersAreLazySeparatedReusedAndReleased()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int tcpPort = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var tcpClient = new KvHostLinkClient(
            "127.0.0.1", tcpPort, HostLinkTransportMode.Tcp, "keyence:kv-8000");

        Assert.Null(Value<byte[]>(tcpClient, "_rxBuf"));
        Assert.Null(Value<byte[]>(tcpClient, "_tcpReadBuf"));
        Assert.Null(Value<byte[]>(tcpClient, "_udpReadBuf"));
        Assert.Equal(0L, Value<long>(tcpClient, "_transportBufferAllocationCount"));

        Task<TcpClient> acceptedTask = listener.AcceptTcpClientAsync();
        await tcpClient.OpenAsync();
        using TcpClient accepted = await acceptedTask;
        Assert.Equal(4096, Value<byte[]>(tcpClient, "_rxBuf")!.Length);
        Assert.Equal(8192, Value<byte[]>(tcpClient, "_tcpReadBuf")!.Length);
        Assert.Null(Value<byte[]>(tcpClient, "_udpReadBuf"));
        Assert.Equal(1L, Value<long>(tcpClient, "_transportBufferAllocationCount"));

        await tcpClient.OpenAsync();
        Assert.Equal(1L, Value<long>(tcpClient, "_transportBufferAllocationCount"));
        await tcpClient.CloseAsync();
        Assert.Null(Value<byte[]>(tcpClient, "_rxBuf"));
        Assert.Null(Value<byte[]>(tcpClient, "_tcpReadBuf"));
        Assert.Null(Value<byte[]>(tcpClient, "_udpReadBuf"));
        listener.Stop();

        using var udpPeer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int udpPort = ((IPEndPoint)udpPeer.Client.LocalEndPoint!).Port;
        await using var udpClient = new KvHostLinkClient(
            "127.0.0.1", udpPort, HostLinkTransportMode.Udp, "keyence:kv-8000");
        await udpClient.OpenAsync();
        Assert.Null(Value<byte[]>(udpClient, "_rxBuf"));
        Assert.Null(Value<byte[]>(udpClient, "_tcpReadBuf"));
        Assert.Equal(65_538, Value<byte[]>(udpClient, "_udpReadBuf")!.Length);
        Assert.Equal(1L, Value<long>(udpClient, "_transportBufferAllocationCount"));
        await udpClient.CloseAsync();
        Assert.Null(Value<byte[]>(udpClient, "_udpReadBuf"));
    }

    [Fact]
    public async Task MaximumBodyWithOneByteReadsHasLinearScanAndCopyCounts()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task server = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            NetworkStream stream = accepted.GetStream();
            var one = new byte[1];
            do
            {
                Assert.Equal(1, await stream.ReadAsync(one));
            }
            while (one[0] != '\r');

            var response = new byte[65_537];
            Array.Fill(response, (byte)'A', 0, 65_536);
            response[^1] = (byte)'\r';
            await stream.WriteAsync(response);
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, "keyence:kv-8000")
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
        await client.OpenAsync();
        Field("_tcpReadBuf").SetValue(client, new byte[1]);

        byte[] body = await client.SendRawAsync("?E");

        Assert.Equal(65_536, body.Length);
        Assert.Equal(65_537L, Value<long>(client, "_tcpScanBytes"));
        Assert.InRange(Value<long>(client, "_tcpCopyBytes"), 65_537L, 65_537L * 4);
        await server.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    [Fact]
    public async Task UdpSocketReplacementReusesTheSessionReceiveBuffer()
    {
        using var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        int port = ((IPEndPoint)peer.Client.LocalEndPoint!).Port;
        Task server = Task.Run(async () =>
        {
            UdpReceiveResult malformedRequest = await peer.ReceiveAsync();
            byte[] malformed = [0xFF, (byte)'\r'];
            await peer.SendAsync(malformed, malformed.Length, malformedRequest.RemoteEndPoint);
            UdpReceiveResult recoveredRequest = await peer.ReceiveAsync();
            byte[] valid = "58\r"u8.ToArray();
            await peer.SendAsync(valid, valid.Length, recoveredRequest.RemoteEndPoint);
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Udp, "keyence:kv-8000");
        await client.OpenAsync();
        byte[] allocatedBuffer = Value<byte[]>(client, "_udpReadBuf")!;

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.QueryModelAsync());
        Assert.True(client.IsOpen);
        Assert.Same(allocatedBuffer, Value<byte[]>(client, "_udpReadBuf"));
        Assert.Equal(1L, Value<long>(client, "_transportBufferAllocationCount"));

        KvModelInfo recovered = await client.QueryModelAsync();
        Assert.Equal("58", recovered.Code);
        Assert.Same(allocatedBuffer, Value<byte[]>(client, "_udpReadBuf"));
        Assert.Equal(1L, Value<long>(client, "_transportBufferAllocationCount"));
        await server.WaitAsync(TimeSpan.FromSeconds(5));
    }
}
