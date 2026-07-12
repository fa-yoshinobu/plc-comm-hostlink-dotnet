using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace PlcComm.KvHostLink.Tests;

public sealed class QualityOverhaulContractTests
{
    private const string TestProfile = "keyence:kv-8000";

    [Fact]
    public void ConnectionContractRejectsInvalidExplicitValues()
    {
        Assert.Throws<ArgumentException>(() =>
            new KvHostLinkClient(" ", 8501, HostLinkTransportMode.Tcp, TestProfile));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KvHostLinkClient("127.0.0.1", 0, HostLinkTransportMode.Tcp, TestProfile));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new KvHostLinkClient("127.0.0.1", 8501, (HostLinkTransportMode)99, TestProfile));

        using var client = new KvHostLinkClient("127.0.0.1", 8501, HostLinkTransportMode.Tcp, TestProfile);
        Assert.Throws<ArgumentOutOfRangeException>(() => client.Timeout = TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => client.Timeout = TimeSpan.FromMilliseconds(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => client.Timeout = TimeSpan.FromTicks(1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            client.Timeout = TimeSpan.FromMilliseconds((double)int.MaxValue + 1));
        client.Timeout = TimeSpan.FromMilliseconds(1);
        Assert.Equal(TimeSpan.FromMilliseconds(1), client.Timeout);
        client.Timeout = TimeSpan.FromMilliseconds(int.MaxValue);
        Assert.Equal(TimeSpan.FromMilliseconds(int.MaxValue), client.Timeout);
    }

    [Fact]
    public void PublicSurfaceRemovesLfChunkAndCompatibilityOptions()
    {
        Assert.Null(typeof(KvHostLinkClient).GetProperty("AppendLfOnSend"));
        Assert.Null(typeof(QueuedKvHostLinkClient).GetProperty("AppendLfOnSend"));
        Assert.DoesNotContain(
            typeof(KvHostLinkClientExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => method.Name.Contains("Chunked", StringComparison.Ordinal));
        Assert.Null(typeof(KvHostLinkDevice).GetMethod(
            nameof(KvHostLinkDevice.ParseDevice), [typeof(string), typeof(bool)]));
        Assert.Null(typeof(KvHostLinkDevice).GetMethod("ParseDeviceText", BindingFlags.Public | BindingFlags.Static));
        Assert.Null(typeof(KvHostLinkDevice).GetMethod("ResolveEffectiveFormat", BindingFlags.Public | BindingFlags.Static));

        MethodInfo raw = Assert.Single(
            typeof(KvHostLinkClient).GetMethods(),
            method => method.Name == nameof(KvHostLinkClient.SendRawAsync));
        Assert.Equal(typeof(Task<byte[]>), raw.ReturnType);
        ParameterInfo time = typeof(KvHostLinkClient).GetMethod(
            nameof(KvHostLinkClient.SetTimeAsync))!.GetParameters()[0];
        Assert.False(time.IsOptional);
        Assert.Equal(typeof(DateTime), time.ParameterType);

        using var client = new KvHostLinkClient(
            "127.0.0.1", 8501, HostLinkTransportMode.Tcp, TestProfile);
        Assert.Null(client.TraceHook);
    }

    [Fact]
    public async Task RawApiPreservesPlcErrorBytesWithoutSemanticTranslation()
    {
        await using var server = new RawContractServer(_ => "E1\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal("E1"u8.ToArray(), await client.SendRawAsync("UNKNOWN"));
        await Assert.ThrowsAsync<HostLinkError>(() => client.QueryModelAsync());
    }

    [Fact]
    public async Task RawApiPreservesEmptyAndNonAsciiBodies()
    {
        await using var server = new RawContractServer(command => command switch
        {
            "EMPTY" => [(byte)'\r'],
            "NONASCII" => [0x80, (byte)'\r'],
            _ => "E1\r"u8.ToArray(),
        });
        await using var client = await OpenClientAsync(server.Port);

        Assert.Empty(await client.SendRawAsync("EMPTY"));
        Assert.Equal([0x80], await client.SendRawAsync("NONASCII"));
    }

    [Fact]
    public async Task RawApiExcludesCrLfAndCrLfTerminators()
    {
        await using var server = new RawContractServer(command => command switch
        {
            "CR" => "A\r"u8.ToArray(),
            "LF" => "B\n"u8.ToArray(),
            "CRLF" => "C\r\n"u8.ToArray(),
            _ => "E1\r"u8.ToArray(),
        });
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal("A"u8.ToArray(), await client.SendRawAsync("CR"));
        Assert.Equal("B"u8.ToArray(), await client.SendRawAsync("LF"));
        Assert.Equal("C"u8.ToArray(), await client.SendRawAsync("CRLF"));
    }

    [Fact]
    public async Task CommentDecoderRemovesOnlyTrailingAsciiSpaces()
    {
        byte[] response = [.. Encoding.UTF8.GetBytes("A B\t　  "), (byte)'\r'];
        await using var server = new RawContractServer(_ => response);
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal("A B\t　", await client.ReadCommentsAsync("DM100"));
    }

    [Fact]
    public async Task CommentDecoderRejectsBytesInvalidInUtf8AndShiftJis()
    {
        await using var server = new RawContractServer(_ => [0x81, 0x00, (byte)'\r']);
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadCommentsAsync("DM100"));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task NumericFormatAndRangeErrorsAreRejectedBeforeSend()
    {
        await using var server = new RawContractServer(_ => "OK\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync("DM100"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync("DM100.U", ".U"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync("DM100", ""));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteAsync("DM100", -1, ".U"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteAsync("DM100", 65_536, ".U"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteAsync("DM100", 1.5, ".U"));

        Assert.Empty(server.Commands);
    }

    [Fact]
    public async Task TypedBitSupportsDirectBitDevicesAndRejectsAmbiguousValues()
    {
        await using var server = new RawContractServer(command => command switch
        {
            "RD R5000" => "ON\r"u8.ToArray(),
            "WR R5000 1" => "OK\r"u8.ToArray(),
            _ => "E1\r"u8.ToArray(),
        });
        await using var client = await OpenClientAsync(server.Port);

        Assert.True(Assert.IsType<bool>(await client.ReadTypedAsync("R5000", "BIT")));
        await client.WriteTypedAsync("R5000", "BIT", true);
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteTypedAsync("R5000", "BIT", 2));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadTypedAsync("DM100", "BIT"));

        Assert.Equal(["RD R5000", "WR R5000 1"], server.Commands.ToArray());
    }

    [Fact]
    public async Task ResponseCountMismatchInvalidatesTransport()
    {
        await using var server = new RawContractServer(_ => "1 2\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync("DM100", ".U"));
        Assert.False(client.IsOpen);
        await Assert.ThrowsAsync<HostLinkNotConnectedError>(() => client.ReadAsync("DM100", ".U"));
        Assert.Single(server.Commands);
    }

    [Fact]
    public async Task InvalidOperatingModeResponseInvalidatesTransport()
    {
        await using var server = new RawContractServer(_ => "2\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ConfirmOperatingModeAsync());
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task MonitorRegistrationDoesNotSurviveReconnect()
    {
        await using var server = new RawContractServer(command => command switch
        {
            "MBS R5000" => "OK\r"u8.ToArray(),
            "MBR" => "1\r"u8.ToArray(),
            _ => "E1\r"u8.ToArray(),
        });
        await using var client = await OpenClientAsync(server.Port);

        await client.RegisterMonitorBitsAsync(["R5000"]);
        client.Close();
        await client.OpenAsync();

        HostLinkProtocolError error = await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.ReadMonitorBitsAsync());
        Assert.Contains("registered", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["MBS R5000"], server.Commands.ToArray());
    }

    [Fact]
    public async Task ResponseBodyOneByteOverAbsoluteCapInvalidatesTransport()
    {
        byte[] oversized = [.. Enumerable.Repeat((byte)'1', 65_537), (byte)'\r'];
        await using var server = new RawContractServer(_ => oversized);
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.SendRawAsync("OVERSIZED"));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task ResponseBodyAtAbsoluteCapIsAccepted()
    {
        byte[] boundary = [.. Enumerable.Repeat((byte)'1', 65_536), (byte)'\r'];
        await using var server = new RawContractServer(_ => boundary);
        await using var client = await OpenClientAsync(server.Port);

        byte[] body = await client.SendRawAsync("BOUNDARY");

        Assert.Equal(65_536, body.Length);
        Assert.True(client.IsOpen);
    }

    [Fact]
    public async Task DwordHelpersUseOneNativeRequestAndRejectLimitOverflowBeforeSend()
    {
        await using var server = new RawContractServer(command => command switch
        {
            "RDS DM200.D 3" => "1 2 3\r"u8.ToArray(),
            "WRS DM200.D 3 1 2 3" => "OK\r"u8.ToArray(),
            _ => "E1\r"u8.ToArray(),
        });
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal(new uint[] { 1, 2, 3 }, await client.ReadDWordsAsync("DM200", 3));
        await client.WriteDWordsSingleRequestAsync("DM200", new uint[] { 1, 2, 3 });
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadDWordsAsync("DM200", 501));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteDWordsSingleRequestAsync("DM200", new uint[501]));

        Assert.Equal(["RDS DM200.D 3", "WRS DM200.D 3 1 2 3"], server.Commands.ToArray());
    }

    [Fact]
    public async Task WordHelperRejectsLimitOverflowBeforeSend()
    {
        await using var server = new RawContractServer(_ => "OK\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadWordsAsync("DM0", 1001));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteWordsSingleRequestAsync("DM0", new ushort[1001]));
        Assert.Empty(server.Commands);
    }

    [Fact]
    public async Task ExpansionFormatsRejectMissingUnknownAndOutOfRangeValuesBeforeSend()
    {
        await using var server = new RawContractServer(_ => "OK\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadExpansionUnitBufferAsync(1, 0, 1, ""));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadExpansionUnitBufferAsync(1, 0, 1, ".X"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteExpansionUnitBufferAsync(1, 0, [-1], ".U"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteExpansionUnitBufferAsync(1, 0, [32768], ".S"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteExpansionUnitBufferAsync(1, 0, [-1], ".D"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteExpansionUnitBufferAsync(1, 0, [2_147_483_648L], ".L"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteExpansionUnitBufferAsync(1, 0, [65_536], ".H"));

        Assert.Empty(server.Commands);
    }

    [Theory]
    [InlineData(".U", "-1")]
    [InlineData(".S", "32768")]
    [InlineData(".D", "-1")]
    [InlineData(".L", "2147483648")]
    [InlineData(".H", "G")]
    public async Task ExpansionFormatsRejectInvalidResponseTokens(string format, string token)
    {
        await using var server = new RawContractServer(_ => Encoding.ASCII.GetBytes($"{token}\r"));
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadExpansionUnitBufferAsync(1, 0, 1, format));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task ConcurrentBitInWordUpdatesHoldOneClientLockAcrossReadModifyWrite()
    {
        ushort word = 0;
        await using var server = new RawContractServer(command =>
        {
            if (command == "RD DM100.U")
                return Encoding.ASCII.GetBytes($"{word}\r");
            if (command.StartsWith("WR DM100.U ", StringComparison.Ordinal))
            {
                word = ushort.Parse(command[11..], System.Globalization.CultureInfo.InvariantCulture);
                return "OK\r"u8.ToArray();
            }
            return "E1\r"u8.ToArray();
        });
        await using var client = await OpenClientAsync(server.Port);

        await Task.WhenAll(
            client.WriteBitInWordAsync("DM100", 0, true),
            client.WriteBitInWordAsync("DM100", 1, true));

        Assert.Equal((ushort)3, word);
        Assert.Equal(4, server.Commands.Count);
    }

    [Fact]
    public async Task TraceHookObservesExactFramesOnceAndCannotBreakCommand()
    {
        await using var server = new RawContractServer(_ => "57\r\n"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);
        var frames = new List<HostLinkTraceFrame>();
        client.TraceHook = frame =>
        {
            frames.Add(frame);
            throw new InvalidOperationException("diagnostic failure");
        };

        KvModelInfo model = await client.QueryModelAsync();

        Assert.Equal("57", model.Code);
        Assert.Equal(2, frames.Count);
        Assert.Equal("?K\r"u8.ToArray(), frames[0].Data);
        Assert.Equal("57\r\n"u8.ToArray(), frames[1].Data);
    }

    [Fact]
    public async Task QueuedGateHonorsCallerCancellationWhileWaiting()
    {
        using var inner = new KvHostLinkClient("127.0.0.1", 8501, HostLinkTransportMode.Tcp, TestProfile);
        await using var queued = new QueuedKvHostLinkClient(inner);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task holder = queued.ExecuteAsync(async _ =>
        {
            entered.SetResult();
            await release.Task;
        });
        await entered.Task;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            queued.ExecuteAsync(_ => Task.CompletedTask, cancellation.Token));
        release.SetResult();
        await holder;
    }

    private static async Task<KvHostLinkClient> OpenClientAsync(int port)
    {
        var client = new KvHostLinkClient("127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile);
        await client.OpenAsync();
        return client;
    }

    private sealed class RawContractServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource _stop = new();
        private readonly Func<string, byte[]> _responseFactory;
        private readonly Task _loop;

        public RawContractServer(Func<string, byte[]> responseFactory)
        {
            _responseFactory = responseFactory;
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _loop = Task.Run(RunAsync);
        }

        public int Port { get; }
        public ConcurrentQueue<string> Commands { get; } = new();

        private async Task RunAsync()
        {
            try
            {
                while (!_stop.IsCancellationRequested)
                {
                    using TcpClient accepted = await _listener.AcceptTcpClientAsync(_stop.Token);
                    NetworkStream stream = accepted.GetStream();
                    var body = new List<byte>();
                    var buffer = new byte[4096];
                    while (!_stop.IsCancellationRequested)
                    {
                        int read = await stream.ReadAsync(buffer, _stop.Token);
                        if (read == 0)
                            break;
                        for (int index = 0; index < read; index++)
                        {
                            byte value = buffer[index];
                            if (value == '\r')
                            {
                                string command = Encoding.ASCII.GetString([.. body]);
                                body.Clear();
                                Commands.Enqueue(command);
                                await stream.WriteAsync(_responseFactory(command), _stop.Token);
                            }
                            else if (value != '\n')
                            {
                                body.Add(value);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested)
            {
            }
            catch (ObjectDisposedException) when (_stop.IsCancellationRequested)
            {
            }
            catch (SocketException) when (_stop.IsCancellationRequested)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            _stop.Cancel();
            _listener.Stop();
            try
            {
                await _loop;
            }
            catch (IOException) when (_stop.IsCancellationRequested)
            {
            }
            _stop.Dispose();
        }
    }
}
