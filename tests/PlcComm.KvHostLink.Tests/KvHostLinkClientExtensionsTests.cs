using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PlcComm.KvHostLink;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkClientExtensionsTests
{
    [Fact]
    public async Task ReadNamedAsync_BatchesContiguousWordReads()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS DM100.U 8" => "1025 65535 2 1 57920 1 0 16712",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port);

        var result = await client.ReadNamedAsync(
            ["DM100", "DM100.0", "DM100.A", "DM101:S", "DM102:D", "DM104:L", "DM106:F"]);

        Assert.Equal((ushort)1025, Assert.IsType<ushort>(result["DM100"]));
        Assert.True(Assert.IsType<bool>(result["DM100.0"]));
        Assert.True(Assert.IsType<bool>(result["DM100.A"]));
        Assert.Equal((short)-1, Assert.IsType<short>(result["DM101:S"]));
        Assert.Equal((uint)65538, Assert.IsType<uint>(result["DM102:D"]));
        Assert.Equal(123456, Assert.IsType<int>(result["DM104:L"]));
        Assert.Equal(12.5f, Assert.IsType<float>(result["DM106:F"]));

        Assert.Equal(["RDS DM100.U 8"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadTypedAsync_And_WriteTypedAsync_SupportFloatSuffix()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS DM200.U 2" => "0 16712",
            "WRS DM200.U 2 0 16712" => "OK",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port);

        var value = await client.ReadTypedAsync("DM200", "F");
        await client.WriteTypedAsync("DM200", "F", 12.5f);

        Assert.Equal(12.5f, Assert.IsType<float>(value));
        Assert.Equal(["RDS DM200.U 2", "WRS DM200.U 2 0 16712"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task OpenAndConnectAsync_ReturnsQueuedClientThatUsesQueuedHelperOverloads()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD DM10.U" => "123",
            _ => "E1",
        });

        await using var client = await KvHostLinkClientExtensions.OpenAndConnectAsync("127.0.0.1", server.Port);
        var value = await client.ReadTypedAsync("DM10", "U");

        Assert.True(client.IsOpen);
        Assert.Equal((ushort)123, Assert.IsType<ushort>(value));
        Assert.Equal(["RD DM10.U"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task PollAsync_ReusesCompiledReadPlanForEachCycle()
    {
        int responses = 0;
        await using var server = new ScriptedHostLinkServer(command =>
        {
            Assert.Equal("RDS DM100.U 3", command);
            return responses++ == 0 ? "1 0 16320" : "3 0 16416";
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port);

        var snapshots = new List<IReadOnlyDictionary<string, object>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var snapshot in client.PollAsync(
            ["DM100", "DM100.0", "DM101:F"],
            TimeSpan.FromMilliseconds(1),
            cts.Token))
        {
            snapshots.Add(snapshot);
            if (snapshots.Count >= 2)
                break;
        }

        Assert.Equal(2, snapshots.Count);
        Assert.Equal((ushort)1, Assert.IsType<ushort>(snapshots[0]["DM100"]));
        Assert.True(Assert.IsType<bool>(snapshots[0]["DM100.0"]));
        Assert.Equal(1.5f, Assert.IsType<float>(snapshots[0]["DM101:F"]));
        Assert.Equal((ushort)3, Assert.IsType<ushort>(snapshots[1]["DM100"]));
        Assert.True(Assert.IsType<bool>(snapshots[1]["DM100.0"]));
        Assert.Equal(2.5f, Assert.IsType<float>(snapshots[1]["DM101:F"]));

        Assert.Equal(
            ["RDS DM100.U 3", "RDS DM100.U 3"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadDWordsChunkedAsync_AdvancesByWholeDwordBoundaries()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS DM200.U 2" => "1 1",
            "RDS DM202.U 2" => "2 2",
            "RDS DM204.U 2" => "3 3",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port);
        var values = await client.ReadDWordsChunkedAsync("DM200", 3, 1);

        Assert.Equal(new uint[] { 65537, 131074, 196611 }, values);
        Assert.Equal(
            ["RDS DM200.U 2", "RDS DM202.U 2", "RDS DM204.U 2"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task WriteDWordsChunkedAsync_AdvancesByWholeDwordBoundaries()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "WRS DM200.U 2 1 1" => "OK",
            "WRS DM202.U 2 2 2" => "OK",
            "WRS DM204.U 2 3 3" => "OK",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port);
        await client.WriteDWordsChunkedAsync("DM200", new uint[] { 65537, 131074, 196611 }, 1);

        Assert.Equal(
            ["WRS DM200.U 2 1 1", "WRS DM202.U 2 2 2", "WRS DM204.U 2 3 3"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadAsync_Rejects32BitDeviceEndCrossingBeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.ReadAsync("DM65534", ".D"));

        Assert.Empty(server.ReceivedCommands);
    }

    [Fact]
    public async Task ReadExpansionUnitBufferAsync_Rejects32BitBufferEndCrossingBeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.ReadExpansionUnitBufferAsync(1, 59999, 1, ".D"));

        Assert.Empty(server.ReceivedCommands);
    }

    private sealed class ScriptedHostLinkServer : IAsyncDisposable
    {
        private readonly TcpListener _listener;
        private readonly Func<string, string> _responseFactory;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;

        public ConcurrentQueue<string> ReceivedCommands { get; } = new();

        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;

        public ScriptedHostLinkServer(Func<string, string> responseFactory)
        {
            _responseFactory = responseFactory;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _serverTask = Task.Run(RunAsync);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                await _serverTask.ConfigureAwait(false);
            }
            catch
            {
                // Listener shutdown is expected during disposal.
            }
            _cts.Dispose();
        }

        private async Task RunAsync()
        {
            try
            {
                using var client = await _listener.AcceptTcpClientAsync(_cts.Token).ConfigureAwait(false);
                using var stream = client.GetStream();
                var buffer = new byte[4096];
                var partial = new List<byte>();

                while (!_cts.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                    if (read == 0)
                        break;

                    for (int i = 0; i < read; i++)
                    {
                        byte current = buffer[i];
                        if (current == (byte)'\r' || current == (byte)'\n')
                        {
                            if (partial.Count == 0)
                                continue;

                            string command = Encoding.ASCII.GetString([.. partial]);
                            partial.Clear();
                            ReceivedCommands.Enqueue(command);

                            string response = _responseFactory(command);
                            byte[] payload = Encoding.ASCII.GetBytes(response + "\r\n");
                            await stream.WriteAsync(payload, _cts.Token).ConfigureAwait(false);
                        }
                        else
                        {
                            partial.Add(current);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected during disposal.
            }
            catch (ObjectDisposedException)
            {
                // Expected during disposal.
            }
            catch (SocketException)
            {
                // Expected when the listener is stopped.
            }
        }
    }
}
