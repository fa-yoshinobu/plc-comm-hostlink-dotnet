using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace PlcComm.KvHostLink.Tests;

public sealed class OverhaulConcurrencyAndAggregateTests
{
    private const string TestProfile = "keyence:kv-8000";

    [Fact]
    public void EndpointContractRejectsIpv6BeforeTransport()
    {
        Assert.Throws<ArgumentException>(() =>
            new KvHostLinkClient("::1", 8501, HostLinkTransportMode.Tcp, TestProfile));
        Assert.Throws<ArgumentException>(() =>
            new KvHostLinkClient("[::1]", 8501, HostLinkTransportMode.Udp, TestProfile));
        Assert.Throws<ArgumentException>(() =>
            new KvHostLinkConnectionOptions("::ffff:127.0.0.1", 8501, HostLinkTransportMode.Tcp, TestProfile));
    }

    [Fact]
    public void EndpointContractRejectsBracketedIpv4DuringConstruction()
    {
        Assert.Throws<ArgumentException>(() =>
            new KvHostLinkClient("[127.0.0.1]", 8501, HostLinkTransportMode.Tcp, TestProfile));
        Assert.Throws<ArgumentException>(() =>
            new KvHostLinkClient("[192.168.250.100]", 8501, HostLinkTransportMode.Udp, TestProfile));
        Assert.Throws<ArgumentException>(() =>
            new KvHostLinkConnectionOptions(
                "[127.0.0.1]", 8501, HostLinkTransportMode.Tcp, TestProfile));

        using var client = new KvHostLinkClient(
            "127.0.0.1", 8501, HostLinkTransportMode.Tcp, TestProfile);
        Assert.Equal(TestProfile, client.PlcProfile);
    }

    [Fact]
    public async Task TestServerCanBeDisposedImmediatelyWithoutShutdownRace()
    {
        var releaseAccept = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var server = new AsyncHostLinkServer(
            (_, _) => Task.FromResult("OK"),
            releaseAccept.Task);

        await server.AcceptGateReached.WaitAsync(TimeSpan.FromSeconds(5));
        Task disposal = server.DisposeAsync().AsTask();
        releaseAccept.TrySetResult();

        await disposal.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task TestServerCanBeDisposedAfterAcceptWaitStarts()
    {
        var server = new AsyncHostLinkServer((_, _) => Task.FromResult("OK"));

        await server.AcceptStarted.WaitAsync(TimeSpan.FromSeconds(5));
        await server.DisposeAsync();
    }

    [Fact]
    public async Task TestServerPropagatesHandlerFailureThatPredatesShutdown()
    {
        var failure = new InvalidOperationException("handler failed before shutdown");
        var server = new AsyncHostLinkServer((_, _) => Task.FromException<string>(failure));
        using var connection = new TcpClient();
        await connection.ConnectAsync(IPAddress.Loopback, server.Port);
        await connection.GetStream().WriteAsync(Encoding.ASCII.GetBytes("TRIGGER\r"));

        InvalidOperationException runFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => server.Completion.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Same(failure, runFailure);

        InvalidOperationException disposeFailure = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await server.DisposeAsync());
        Assert.Same(failure, disposeFailure);
    }

    [Fact]
    public void LiveClientHasNoPerCallProfileOverrideAndProfileIsImmutable()
    {
        PropertyInfo profile = typeof(KvHostLinkClient).GetProperty(nameof(KvHostLinkClient.PlcProfile))!;
        Assert.NotNull(profile.GetMethod);
        Assert.Null(profile.SetMethod);
        Assert.DoesNotContain(
            typeof(KvHostLinkClient).GetMethods(BindingFlags.Instance | BindingFlags.Public),
            method => method.GetParameters().Any(parameter =>
                parameter.Name?.Contains("profile", StringComparison.OrdinalIgnoreCase) == true));
    }

    [Fact]
    public async Task HostnameConnectionSelectsIpv4WithoutIpv6Fallback()
    {
        await using var server = new AsyncHostLinkServer((_, _) => Task.FromResult("1"));
        await using var client = new KvHostLinkClient(
            "localhost", server.Port, HostLinkTransportMode.Tcp, TestProfile);

        await client.OpenAsync();
        Assert.Equal(["1"], await client.ReadAsync("DM0", ".U"));
    }

    [Fact]
    public async Task NormalClientIsFifoAndQueueWaitDoesNotConsumeTransactionTimeout()
    {
        var firstSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new AsyncHostLinkServer(async (command, _) =>
        {
            if (command == "RD DM0.U")
            {
                firstSeen.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
                return "1";
            }
            return command switch
            {
                "RD DM1.U" => "2",
                "RD DM2.U" => "3",
                _ => "E1",
            };
        });
        await using var client = await OpenClientAsync(server.Port);
        client.Timeout = TimeSpan.FromSeconds(2);

        Task<string[]> first = client.ReadAsync("DM0", ".U");
        await firstSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        client.Timeout = TimeSpan.FromMilliseconds(50);
        Task<string[]> second = client.ReadAsync("DM1", ".U");
        Task<string[]> third = client.ReadAsync("DM2", ".U");
        await Task.Delay(150);
        releaseFirst.TrySetResult();

        Assert.Equal(["1"], await first);
        Assert.Equal(["2"], await second);
        Assert.Equal(["3"], await third);
        Assert.Equal(["RD DM0.U", "RD DM1.U", "RD DM2.U"], server.Commands.ToArray());
    }

    [Fact]
    public async Task QueuedWriteSnapshotsBooleanValuesAtAdmission()
    {
        var firstSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new AsyncHostLinkServer(async (command, _) =>
        {
            if (command == "RD DM0.U")
            {
                firstSeen.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
                return "1";
            }
            return "OK";
        });
        await using var client = await OpenClientAsync(server.Port);
        Task<string[]> holder = client.ReadAsync("DM0", ".U");
        await firstSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var values = new List<bool> { true, false };
        Task write = client.WriteConsecutiveAsync("R0", values);
        values[0] = false;
        values[1] = true;
        releaseFirst.TrySetResult();

        await holder;
        await write;
        Assert.Equal(["RD DM0.U", "WRS R000 2 1 0"], server.Commands.ToArray());
    }

    [Fact]
    public async Task DirectBitWritesAreBooleanOnlyAtEveryPublicCoreEntry()
    {
        await using var server = new AsyncHostLinkServer((_, _) => Task.FromResult("OK"));
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteAsync("R0", 1));
        int[] numericBits = [0, 1];
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteConsecutiveAsync("R0", numericBits));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteTypedAsync("R0", "BIT", 1));
        Assert.Empty(server.Commands);

        await client.WriteAsync("R0", true);
        bool[] booleanBits = [false, true];
        await client.WriteConsecutiveAsync("R1", booleanBits);
        await client.WriteTypedAsync("R3", "BIT", false);
        Assert.Equal(["WR R000 1", "WRS R001 2 0 1", "WR R003 0"], server.Commands.ToArray());
    }

    [Fact]
    public async Task SameClientCallbackReentryAndSynchronousCloseAreRejected()
    {
        await using var server = new AsyncHostLinkServer((_, _) => Task.FromResult("7"));
        await using var client = await OpenClientAsync(server.Port);
        Exception? operationError = null;
        Exception? closeError = null;
        client.TraceHook = frame =>
        {
            if (frame.Direction != HostLinkTraceDirection.Send || operationError is not null)
                return;
            operationError = Record.Exception(() => client.ReadAsync("DM1", ".U").GetAwaiter().GetResult());
            closeError = Record.Exception(client.Close);
        };

        Assert.Equal(["7"], await client.ReadAsync("DM0", ".U"));
        Assert.IsType<HostLinkReentrancyError>(operationError);
        Assert.IsType<HostLinkReentrancyError>(closeError);
        Assert.True(client.IsOpen);
        Assert.Equal(["RD DM0.U"], server.Commands.ToArray());
    }

    [Fact]
    public async Task SeparateClientInstancesCanProgressInParallel()
    {
        var seen1 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var seen2 = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server1 = new AsyncHostLinkServer(async (_, _) =>
        {
            seen1.TrySetResult();
            await release.Task.ConfigureAwait(false);
            return "1";
        });
        await using var server2 = new AsyncHostLinkServer(async (_, _) =>
        {
            seen2.TrySetResult();
            await release.Task.ConfigureAwait(false);
            return "2";
        });
        await using var client1 = await OpenClientAsync(server1.Port);
        await using var client2 = await OpenClientAsync(server2.Port);

        Task<string[]> request1 = client1.ReadAsync("DM0", ".U");
        Task<string[]> request2 = client2.ReadAsync("DM0", ".U");
        await Task.WhenAll(
            seen1.Task.WaitAsync(TimeSpan.FromSeconds(5)),
            seen2.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        release.TrySetResult();

        Assert.Equal(["1"], await request1);
        Assert.Equal(["2"], await request2);
    }

    [Fact]
    public async Task AggregateSortsEachWireCompatibleGroupAndBlocksLaterOperations()
    {
        var firstSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new AsyncHostLinkServer(async (command, _) =>
        {
            if (command == "RDS DM0.U 4")
            {
                firstSeen.TrySetResult();
                await releaseFirst.Task.ConfigureAwait(false);
                return "10 20 0 30";
            }
            return command switch
            {
                "RDS DM5.U 1" => "5",
                "RD Z1.D" => "70000",
                "RD DM9.U" => "9",
                _ => "E1",
            };
        });
        await using var client = await OpenClientAsync(server.Port);
        string[] addresses = ["DM5:U", "DM0:U", "DM1:D", "DM3:U", "Z1:D"];
        Task<IReadOnlyDictionary<string, object>> aggregate = client.ReadNamedAsync(addresses);
        await firstSeen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task<string[]> later = client.ReadAsync("DM9", ".U");
        releaseFirst.TrySetResult();

        IReadOnlyDictionary<string, object> result = await aggregate;
        Assert.Equal((ushort)5, result["DM5:U"]);
        Assert.Equal((ushort)10, result["DM0:U"]);
        Assert.Equal((uint)20, result["DM1:D"]);
        Assert.Equal((ushort)30, result["DM3:U"]);
        Assert.Equal((uint)70_000, result["Z1:D"]);
        Assert.Equal(["9"], await later);
        Assert.Equal(
            ["RDS DM0.U 4", "RDS DM5.U 1", "RD Z1.D", "RD DM9.U"],
            server.Commands.ToArray());
    }

    [Fact]
    public async Task AggregatePreflightRejectsAnyInvalidOrDuplicateEntryWithoutSending()
    {
        await using var server = new AsyncHostLinkServer((command, _) => Task.FromResult(
            command == "RDS DM0.U 3" ? "1 2 3" : "1"));
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadNamedAsync(["DM0:U", "DM1:NOT_A_TYPE"]));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadNamedAsync(["DM0:U", "DM0:U"]));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadNamedAsync(["dm0:u", "DM0000:U"]));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadNamedAsync(["R0", "R000:BIT"]));
        Assert.Empty(server.Commands);

        IReadOnlyDictionary<string, object> distinctViews = await client.ReadNamedAsync(
            ["dm0:u", "DM0:S", "DM0.0", "DM0.1"]);
        IReadOnlyDictionary<string, object> overlappingSpans = await client.ReadNamedAsync(
            ["DM0:D", "DM1:D"]);

        Assert.Equal(["dm0:u", "DM0:S", "DM0.0", "DM0.1"], distinctViews.Keys);
        Assert.Equal(["DM0:D", "DM1:D"], overlappingSpans.Keys);
    }

    [Fact]
    public async Task EmptyAggregateReturnsEmptyWithoutSending()
    {
        await using var server = new AsyncHostLinkServer((_, _) => Task.FromResult("1"));
        await using var client = await OpenClientAsync(server.Port);

        IReadOnlyDictionary<string, object> result = await client.ReadNamedAsync([]);

        Assert.Empty(result);
        Assert.Empty(server.Commands);
    }

    [Fact]
    public async Task AggregateNeverSplitsADeclaredDwordAtRequestCapacity()
    {
        await using var server = new AsyncHostLinkServer((command, _) => Task.FromResult(
            command switch
            {
                "RDS DM0.U 999" => string.Join(' ', Enumerable.Repeat("1", 999)),
                "RDS DM999.U 2" => "2 3",
                _ => "E1",
            }));
        await using var client = await OpenClientAsync(server.Port);
        string[] addresses =
        [
            .. Enumerable.Range(0, 999).Select(index => $"DM{index}:U"),
            "DM999:D",
        ];

        IReadOnlyDictionary<string, object> result = await client.ReadNamedAsync(addresses);

        Assert.Equal((uint)(2 | (3 << 16)), result["DM999:D"]);
        Assert.Equal(["RDS DM0.U 999", "RDS DM999.U 2"], server.Commands.ToArray());
    }

    [Fact]
    public async Task AggregateStopsAtFirstFailureAndReturnsNoPartialResult()
    {
        await using var server = new AsyncHostLinkServer((command, _) => Task.FromResult(
            command switch
            {
                "RDS DM0.U 1" => "1",
                "RDS DM2.U 1" => "E1",
                _ => "9",
            }));
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkError>(() =>
            client.ReadNamedAsync(["DM0:U", "DM2:U", "DM4:U"]));
        Assert.Equal(["RDS DM0.U 1", "RDS DM2.U 1"], server.Commands.ToArray());
    }

    [Fact]
    public async Task AggregateCancellationStopsBeforeAnyLaterInternalRequest()
    {
        await using var server = new AsyncHostLinkServer((_, _) => Task.FromResult("1"));
        await using var client = await OpenClientAsync(server.Port);
        using var cancellation = new CancellationTokenSource();
        client.TraceHook = frame =>
        {
            if (frame.Direction == HostLinkTraceDirection.Receive)
                cancellation.Cancel();
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.ReadNamedAsync(["DM0:U", "DM2:U"], cancellation.Token));

        Assert.Equal(["RDS DM0.U 1"], server.Commands.ToArray());
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task ReadTimeoutIsDedicatedWhileWriteTimeoutIsOutcomeUnknown()
    {
        await using var readServer = new AsyncHostLinkServer(async (_, _) =>
        {
            await Task.Delay(500).ConfigureAwait(false);
            return "1";
        });
        await using var reader = await OpenClientAsync(readServer.Port);
        reader.Timeout = TimeSpan.FromMilliseconds(50);
        await Assert.ThrowsAsync<HostLinkTimeoutError>(() => reader.ReadAsync("DM0", ".U"));

        await using var writeServer = new AsyncHostLinkServer(async (_, _) =>
        {
            await Task.Delay(500).ConfigureAwait(false);
            return "OK";
        });
        await using var writer = await OpenClientAsync(writeServer.Port);
        writer.Timeout = TimeSpan.FromMilliseconds(50);
        HostLinkOutcomeUnknownError error = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(
            () => writer.WriteAsync("DM0", 1, ".U"));
        Assert.Equal(HostLinkOutcomeUnknownReason.Timeout, error.Reason);
        Assert.IsType<HostLinkTimeoutError>(error.InnerException);
    }

    [Fact]
    [Trait("Category", "CrossOsLifecycle")]
    public async Task ByteAtATimeTcpProgressCannotRestartAbsoluteDeadline()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Task serverTask = Task.Run(async () =>
        {
            using TcpClient accepted = await listener.AcceptTcpClientAsync();
            using NetworkStream stream = accepted.GetStream();
            while (stream.ReadByte() is int value && value >= 0 && value != '\r') { }
            byte[] response = "12345\r"u8.ToArray();
            foreach (byte value in response)
            {
                await Task.Delay(30);
                try
                {
                    await stream.WriteAsync(new[] { value });
                }
                catch (IOException)
                {
                    break;
                }
            }
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile)
        {
            Timeout = TimeSpan.FromMilliseconds(80),
        };
        await client.OpenAsync();

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await Assert.ThrowsAsync<HostLinkTimeoutError>(() => client.ReadAsync("DM0", ".U"));
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromMilliseconds(220));
        Assert.False(client.IsOpen);
        await serverTask.WaitAsync(TimeSpan.FromSeconds(5));
        listener.Stop();
    }

    private static async Task<KvHostLinkClient> OpenClientAsync(int port)
    {
        var client = new KvHostLinkClient("127.0.0.1", port, HostLinkTransportMode.Tcp, TestProfile);
        await client.OpenAsync();
        return client;
    }

    private sealed class AsyncHostLinkServer : IAsyncDisposable
    {
        private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
        private readonly Func<string, int, Task<string>> _handler;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task? _acceptGate;
        private readonly TaskCompletionSource _acceptGateReached = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _acceptStarted = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly Task _runTask;

        internal AsyncHostLinkServer(
            Func<string, int, Task<string>> handler,
            Task? acceptGate = null)
        {
            _handler = handler;
            _acceptGate = acceptGate;
            _listener.Start();
            _runTask = Task.Run(RunAsync);
        }

        internal int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        internal Task AcceptGateReached => _acceptGateReached.Task;
        internal Task AcceptStarted => _acceptStarted.Task;
        internal Task Completion => _runTask;
        internal ConcurrentQueue<string> Commands { get; } = new();

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            _listener.Stop();
            try
            {
                await _runTask.ConfigureAwait(false);
            }
            finally
            {
                _cts.Dispose();
            }
        }

        private async Task RunAsync()
        {
            _acceptGateReached.TrySetResult();
            if (_acceptGate is not null)
                await _acceptGate.ConfigureAwait(false);

            TcpClient connection;
            try
            {
                ValueTask<TcpClient> accept = _listener.AcceptTcpClientAsync(_cts.Token);
                _acceptStarted.TrySetResult();
                connection = await accept.ConfigureAwait(false);
            }
            catch (Exception error) when (
                _cts.IsCancellationRequested && IsListenerShutdownException(error))
            {
                return;
            }

            using TcpClient ownedConnection = connection;
            using NetworkStream stream = connection.GetStream();
            var pending = new List<byte>();
            var buffer = new byte[4096];
            int index = 0;
            while (!_cts.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = await stream.ReadAsync(buffer, _cts.Token).ConfigureAwait(false);
                }
                catch (Exception error) when (
                    IsConnectedTransportShutdownException(error))
                {
                    return;
                }

                if (read == 0)
                    return;
                for (int offset = 0; offset < read; offset++)
                {
                    byte current = buffer[offset];
                    if (current is (byte)'\r' or (byte)'\n')
                    {
                        if (pending.Count == 0)
                            continue;
                        string command = Encoding.ASCII.GetString([.. pending]);
                        pending.Clear();
                        Commands.Enqueue(command);
                        string response = await _handler(command, index++).ConfigureAwait(false);
                        try
                        {
                            await stream.WriteAsync(
                                Encoding.ASCII.GetBytes(response + "\r"),
                                _cts.Token).ConfigureAwait(false);
                        }
                        catch (Exception error) when (
                            IsConnectedTransportShutdownException(error))
                        {
                            return;
                        }
                    }
                    else
                    {
                        pending.Add(current);
                    }
                }
            }
        }

        private static bool IsListenerShutdownException(Exception error) =>
            error is OperationCanceledException or ObjectDisposedException or
                SocketException or IOException or InvalidOperationException;

        private bool IsConnectedTransportShutdownException(Exception error) =>
            error is SocketException or IOException ||
                (_cts.IsCancellationRequested &&
                    error is OperationCanceledException or ObjectDisposedException or InvalidOperationException);
    }
}
