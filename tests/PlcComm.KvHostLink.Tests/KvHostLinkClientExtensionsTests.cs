using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PlcComm.KvHostLink;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkClientExtensionsTests
{
    private const string TestPlcProfile = "keyence:kv-8000";

    [Fact]
    public async Task ATWrites_AreRejectedBeforeSendingWrOrWrs()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);
        int[] values = [3533, 5543];

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteAsync("AT0", 3533, "D"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteConsecutiveAsync("AT0", values, "D"));

        Assert.Empty(server.ReceivedCommands);
    }

    [Fact]
    public async Task ReadNamedAsync_BatchesContiguousWordReads()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS DM100.U 8" => "1025 65535 2 1 57920 1 0 16712",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

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
    public async Task ReadNamedAsync_ReadsCommentAddressesThroughSequentialFallback()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD DM100.U" => "1025",
            "RDC DM101" => "MAIN COMMENT                    ",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        var result = await client.ReadNamedAsync(["DM100", "DM101:COMMENT"]);

        Assert.Equal((ushort)1025, Assert.IsType<ushort>(result["DM100"]));
        Assert.Equal("MAIN COMMENT", Assert.IsType<string>(result["DM101:COMMENT"]));
        Assert.Equal(["RD DM100.U", "RDC DM101"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_BatchesBitBankDirectBitsAcrossDisplayBankBoundary()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS CR3614 4" => "0 1 0 1",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        var result = await client.ReadNamedAsync(["CR3614", "CR3615", "CR3700", "CR3701"]);

        Assert.False(Assert.IsType<bool>(result["CR3614"]));
        Assert.True(Assert.IsType<bool>(result["CR3615"]));
        Assert.False(Assert.IsType<bool>(result["CR3700"]));
        Assert.True(Assert.IsType<bool>(result["CR3701"]));
        Assert.Equal(["RDS CR3614 4"], server.ReceivedCommands.ToArray());
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

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        var value = await client.ReadTypedAsync("DM200", "F");
        await client.WriteTypedAsync("DM200", "F", 12.5f);

        Assert.Equal(12.5f, Assert.IsType<float>(value));
        Assert.Equal(["RDS DM200.U 2", "WRS DM200.U 2 0 16712"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadTypedAsync_TimerCounterCompositeReadReturnsSetValue()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD T0.D" => "0,0000000010,0000000020",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        var value = await client.ReadTypedAsync("T0", "D");

        Assert.Equal((uint)20, Assert.IsType<uint>(value));
        Assert.Equal(["RD T0.D"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadTypedAsync_TimerCounter16BitCompositeReadReturnsSetValue()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD T0.U" => "0,00010,00020",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        var value = await client.ReadTypedAsync("T0", "U");

        Assert.Equal((ushort)20, Assert.IsType<ushort>(value));
        Assert.Equal(["RD T0.U"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_Native32BitZUsesNativeDwordRead()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD Z1.D" => "0000070000",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        var result = await client.ReadNamedAsync(["Z1:D"]);

        Assert.Equal((uint)70_000, Assert.IsType<uint>(result["Z1:D"]));
        Assert.Equal(["RD Z1.D"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task SetTimeAsync_UsesSundayBasedWeekday()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        await client.SetTimeAsync(new DateTime(2026, 3, 15, 1, 2, 3));
        await client.SetTimeAsync(new DateTime(2026, 3, 16, 1, 2, 3));
        await client.SetTimeAsync(new DateTime(2026, 3, 21, 1, 2, 3));

        Assert.Equal(
            [
                "WRT 26 03 15 01 02 03 0",
                "WRT 26 03 16 01 02 03 1",
                "WRT 26 03 21 01 02 03 6",
            ],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_TimerCounterCompositeReadReturnsSetValue()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD T10.D" => "0,0000000010,0000000020",
            "RD C10.D" => "0,0000000000,0000000030",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        var result = await client.ReadNamedAsync(["T10", "C10"]);

        Assert.Equal((uint)20, Assert.IsType<uint>(result["T10"]));
        Assert.Equal((uint)30, Assert.IsType<uint>(result["C10"]));
        Assert.Equal(["RD T10.D", "RD C10.D"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadTimerCounterAsync_ReturnsStatusCurrentAndPreset()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD T10.D" => "1,0000000010,0000000020",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        var result = await client.ReadTimerCounterAsync("T10");

        Assert.Equal((uint)1, result.Status);
        Assert.Equal((uint)10, result.Current);
        Assert.Equal((uint)20, result.Preset);
        Assert.Equal(["RD T10.D"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task OpenAndConnectAsync_ReturnsQueuedClientThatUsesQueuedHelperOverloads()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD DM10.U" => "123",
            _ => "E1",
        });

        await using var client = await KvHostLinkClientExtensions.OpenAndConnectAsync("127.0.0.1", TestPlcProfile, server.Port);
        var value = await client.ReadTypedAsync("DM10", "U");

        Assert.True(client.IsOpen);
        Assert.Equal((ushort)123, Assert.IsType<ushort>(value));
        Assert.Equal(["RD DM10.U"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task QueuedClient_ReadCommentsAsync_UsesRdcCommand()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDC DM10" => "ALARM TEXT                      ",
            _ => "E1",
        });

        await using var client = await KvHostLinkClientExtensions.OpenAndConnectAsync("127.0.0.1", TestPlcProfile, server.Port);
        var comment = await client.ReadCommentsAsync("DM10");

        Assert.Equal("ALARM TEXT", comment);
        Assert.Equal(["RDC DM10"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadCommentsAsync_AcceptsXymAliasDeviceTypes()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDC D10" => "DM COMMENT                      ",
            "RDC M20" => "MR COMMENT                      ",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);
        var dataMemoryComment = await client.ReadCommentsAsync("D10");
        var auxiliaryRelayComment = await client.ReadCommentsAsync("M20");

        Assert.Equal("DM COMMENT", dataMemoryComment);
        Assert.Equal("MR COMMENT", auxiliaryRelayComment);
        Assert.Equal(["RDC D10", "RDC M20"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task CommandDeviceSets_FollowManualAndXymAliases()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "ST X100" => "OK",
            "RS M100" => "OK",
            "STS L100 4" => "OK",
            "MWS D100.U E100.U F100.U MR100 LR100" => "OK",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        await client.ForcedSetAsync("X100");
        await client.ForcedResetAsync("M100");
        await client.ForcedSetConsecutiveAsync("L100", 4);
        await client.RegisterMonitorWordsAsync(["D100", "E100", "F100", "MR100", "LR100"]);
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.RegisterMonitorWordsAsync(["M100"]));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.RegisterMonitorWordsAsync(["L100"]));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ForcedSetConsecutiveAsync("T100", 4));

        Assert.Equal(
            ["ST X100", "RS M100", "STS L100 4", "MWS D100.U E100.U F100.U MR100 LR100"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task WssTimerCounterCountLimit_IsEnforcedBeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.WriteSetValueConsecutiveAsync("T0", Enumerable.Repeat(0, 121)));

        Assert.Empty(server.ReceivedCommands);
    }

    [Fact]
    public async Task HexWrite_FormatsNonIntIntegralTypesAsHex()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "WR DM10.H ABCD" => "OK",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        await client.WriteAsync("DM10", (ushort)0xABCD, ".H");

        Assert.Equal(["WR DM10.H ABCD"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ConfirmOperatingModeAsync_RejectsUnknownModeValues()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "?M" => "2",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ConfirmOperatingModeAsync());

        Assert.Equal(["?M"], server.ReceivedCommands.ToArray());
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

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

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

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);
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

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);
        await client.WriteDWordsChunkedAsync("DM200", new uint[] { 65537, 131074, 196611 }, 1);

        Assert.Equal(
            ["WRS DM200.U 2 1 1", "WRS DM202.U 2 2 2", "WRS DM204.U 2 3 3"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadAsync_Rejects32BitDeviceEndCrossingBeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.ReadAsync("DM65534", ".D"));

        Assert.Empty(server.ReceivedCommands);
    }

    [Fact]
    public async Task ExpansionUnitBufferAsync_UsesAddressSuffixCommandForm()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "URD 01 100.U 2" => "123 456",
            "UWR 02 200.S 2 7 8" => "OK",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

        string[] values = await client.ReadExpansionUnitBufferAsync(1, 100, 2);
        int[] valuesToWrite = [7, 8];
        await client.WriteExpansionUnitBufferAsync(2, 200, valuesToWrite, ".S");

        Assert.Equal(["123", "456"], values);
        Assert.Equal(["URD 01 100.U 2", "UWR 02 200.S 2 7 8"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadExpansionUnitBufferAsync_Rejects32BitBufferEndCrossingBeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", TestPlcProfile, server.Port);

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
