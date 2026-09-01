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
        Assert.Null(typeof(KvHostLinkClient).Assembly.GetType("PlcComm.KvHostLink.QueuedKvHostLinkClient"));
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
        Assert.Equal(
            [HostLinkCommentEncoding.Utf8, HostLinkCommentEncoding.Cp932],
            Enum.GetValues<HostLinkCommentEncoding>());
        Assert.Null(typeof(KvHostLinkClient).GetMethod(
            nameof(KvHostLinkClient.ReadCommentsAsync),
            [typeof(string), typeof(CancellationToken)]));
        Assert.NotNull(typeof(KvHostLinkClient).GetMethod(
            nameof(KvHostLinkClient.ReadCommentsAsync),
            [typeof(string), typeof(HostLinkCommentEncoding), typeof(CancellationToken)]));
        Assert.NotNull(typeof(KvHostLinkClient).GetMethod(
            nameof(KvHostLinkClient.ReadCommentBytesAsync),
            [typeof(string), typeof(CancellationToken)]));
        ParameterInfo time = typeof(KvHostLinkClient).GetMethod(
            nameof(KvHostLinkClient.SetTimeAsync))!.GetParameters()[0];
        Assert.False(time.IsOptional);
        Assert.Equal(typeof(DateTime), time.ParameterType);

        using var client = new KvHostLinkClient(
            "127.0.0.1", 8501, HostLinkTransportMode.Tcp, TestProfile);
        Assert.Null(client.TraceHook);
    }

    [Fact]
    public void PublicWriteSurfaceRequiresBooleanOrExplicitDataFormat()
    {
        MethodInfo[] methods = typeof(KvHostLinkClient).GetMethods(
            BindingFlags.Instance | BindingFlags.Public);

        MethodInfo scalarGeneric = Assert.Single(
            methods,
            method => method.Name == nameof(KvHostLinkClient.WriteAsync)
                && method.IsGenericMethodDefinition);
        ParameterInfo[] scalarParameters = scalarGeneric.GetParameters();
        Assert.Equal(4, scalarParameters.Length);
        Assert.Equal(typeof(string), scalarParameters[0].ParameterType);
        Assert.True(scalarParameters[1].ParameterType.IsGenericParameter);
        Assert.Equal(typeof(string), scalarParameters[2].ParameterType);
        Assert.Equal(typeof(CancellationToken), scalarParameters[3].ParameterType);

        MethodInfo consecutiveGeneric = Assert.Single(
            methods,
            method => method.Name == nameof(KvHostLinkClient.WriteConsecutiveAsync)
                && method.IsGenericMethodDefinition);
        ParameterInfo[] consecutiveParameters = consecutiveGeneric.GetParameters();
        Assert.Equal(4, consecutiveParameters.Length);
        Assert.Equal(typeof(string), consecutiveParameters[0].ParameterType);
        Assert.Equal(typeof(IEnumerable<>), consecutiveParameters[1].ParameterType.GetGenericTypeDefinition());
        Assert.Equal(typeof(string), consecutiveParameters[2].ParameterType);
        Assert.Equal(typeof(CancellationToken), consecutiveParameters[3].ParameterType);

        Assert.Contains(
            methods,
            method => method.Name == nameof(KvHostLinkClient.WriteAsync)
                && !method.IsGenericMethod
                && method.GetParameters() is { Length: 3 } parameters
                && parameters[1].ParameterType == typeof(bool));
        Assert.Contains(
            methods,
            method => method.Name == nameof(KvHostLinkClient.WriteConsecutiveAsync)
                && !method.IsGenericMethod
                && method.GetParameters() is { Length: 3 } parameters
                && parameters[1].ParameterType == typeof(IEnumerable<bool>));
    }

    [Fact]
    public async Task RawApiPreservesPlcErrorBytesWithoutSemanticTranslation()
    {
        await using var server = new RawContractServer(_ => "E1\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal(default, client.TrafficStats);
        Assert.Equal("E1"u8.ToArray(), await client.SendRawAsync("UNKNOWN"));
        Assert.Equal(new HostLinkTrafficStats(1, 8, 3), client.TrafficStats);
        await Assert.ThrowsAsync<HostLinkError>(() => client.QueryModelAsync());
        Assert.Equal(new HostLinkTrafficStats(2, 11, 6), client.TrafficStats);
        await client.CloseAsync();
        Assert.Equal(new HostLinkTrafficStats(2, 11, 6), client.TrafficStats);
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

    [Theory]
    [InlineData(HostLinkTransportMode.Tcp)]
    [InlineData(HostLinkTransportMode.Udp)]
    public async Task EmptyRawCommandIsRejectedBeforeConnectionQueueStateOrSend(
        HostLinkTransportMode transport)
    {
        await using var client = new KvHostLinkClient(
            "invalid.invalid", 8501, transport, TestProfile);
        int traceCalls = 0;
        client.TraceHook = _ => traceCalls++;

        HostLinkProtocolError error = await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.SendRawAsync(string.Empty));

        Assert.Contains("empty", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
        Assert.Equal(0, traceCalls);
    }

    [Fact]
    public async Task RawRequestFrameLimitIs65507BytesIncludingTerminatingCr()
    {
        string boundaryBody = new('A', 65_506);
        await using var server = new RawContractServer(command =>
            command.Length == boundaryBody.Length ? "OK\r"u8.ToArray() : "E1\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);
        byte[]? sentFrame = null;
        client.TraceHook = frame =>
        {
            if (frame.Direction == HostLinkTraceDirection.Send)
                sentFrame = frame.Data;
        };

        Assert.Equal("OK"u8.ToArray(), await client.SendRawAsync(boundaryBody));
        Assert.NotNull(sentFrame);
        Assert.Equal(65_507, sentFrame.Length);
        Assert.Equal((byte)'\r', sentFrame[^1]);

        foreach (HostLinkTransportMode transport in Enum.GetValues<HostLinkTransportMode>())
        {
            await using var rejectingClient = new KvHostLinkClient(
                "invalid.invalid", 8501, transport, TestProfile);
            await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
                rejectingClient.SendRawAsync(new string('A', 65_507)));
            Assert.Equal(default, rejectingClient.TrafficStats);
        }

        Assert.Single(server.Commands);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TcpTrafficStatsAreIndependentOfCrLfSegmentation(bool splitLf)
    {
        await using var server = new RawContractServer(command => command switch
        {
            "FIRST" => splitLf ? "FIRST\r"u8.ToArray() : "FIRST\r\n"u8.ToArray(),
            "SECOND" => splitLf ? "\nSECOND\n\r"u8.ToArray() : "SECOND\n\r"u8.ToArray(),
            _ => "E1\r"u8.ToArray(),
        });
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal("FIRST"u8.ToArray(), await client.SendRawAsync("FIRST"));
        Assert.Equal("SECOND"u8.ToArray(), await client.SendRawAsync("SECOND"));
        Assert.Equal(new HostLinkTrafficStats(2, 13, 13), client.TrafficStats);
    }

    [Fact]
    public async Task CompletePlcErrorLineIsCountedBeforeSemanticFailure()
    {
        await using var server = new RawContractServer(_ => "E1\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkError>(() => client.ClearErrorAsync());

        Assert.Equal(3UL, client.TrafficStats.RxBytes);
    }

    [Fact]
    public async Task CommentRawPathPreservesExactPayloadIncludingPadding()
    {
        byte[] payload = [0x81, 0x00, 0x20, 0x20];
        await using var server = new RawContractServer(_ => [.. payload, (byte)'\r']);
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal(payload, await client.ReadCommentBytesAsync("DM100"));
        Assert.True(client.IsOpen);
    }

    [Fact]
    public async Task CommentDecoderUsesOnlyExplicitCodecForAmbiguousBytes()
    {
        await using var server = new RawContractServer(_ => [0xC2, 0xA2, (byte)'\r']);
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal("¢", await client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Utf8));
        Assert.Equal("ﾂ｢", await client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Cp932));
    }

    [Fact]
    public async Task CommentBomBytesFollowOnlyTheExplicitCodec()
    {
        await using var server = new RawContractServer(_ =>
            [0xEF, 0xBB, 0xBF, 0x41, (byte)'\r']);
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal(
            "\uFEFFA",
            await client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Utf8));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Cp932));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task CommentCp932PreservesAsciiControlsAndAcceptsHalfwidthBytes()
    {
        await using var server = new RawContractServer(_ =>
            [0x1A, 0x1C, 0x7F, 0xA1, 0xDF, (byte)'\r']);
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal(
            new string(['\u001A', '\u001C', '\u007F', '\uFF61', '\uFF9F']),
            await client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Cp932));
    }

    [Theory]
    [InlineData("8790", "\u2252")]
    [InlineData("ED40", "\u7E8A")]
    [InlineData("FA4A", "\u2160")]
    public async Task CommentCp932AcceptsSharedWindowsExtensionMappings(
        string payloadHex,
        string expected)
    {
        byte[] payload = Convert.FromHexString(payloadHex);
        await using var server = new RawContractServer(_ => [.. payload, (byte)'\r']);
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal(
            expected,
            await client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Cp932));
    }

    [Fact]
    public async Task CommentDecoderRemovesOnlyTrailingAsciiSpaces()
    {
        byte[] response = [.. Encoding.UTF8.GetBytes("A B\t　  "), (byte)'\r'];
        await using var server = new RawContractServer(_ => response);
        await using var client = await OpenClientAsync(server.Port);

        Assert.Equal(
            "A B\t　",
            await client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Utf8));
    }

    [Fact]
    public async Task CommentDecoderRejectsMalformedUtf8WithoutFallbackOrReplacement()
    {
        await using var server = new RawContractServer(_ => [0xC2, (byte)'\r']);
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Utf8));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task CommentDecoderRejectsMalformedCp932WithoutFallbackOrReplacement()
    {
        await using var server = new RawContractServer(_ => [0x81, 0x00, (byte)'\r']);
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Cp932));
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData("80")]
    [InlineData("A0")]
    [InlineData("FD")]
    [InlineData("FE")]
    [InlineData("FF")]
    [InlineData("81")]
    [InlineData("817F")]
    [InlineData("81AD")]
    public async Task CommentCp932RejectsForbiddenSingletonMalformedAndUnassignedBytes(
        string payloadHex)
    {
        byte[] payload = Convert.FromHexString(payloadHex);
        await using var server = new RawContractServer(_ => [.. payload, (byte)'\r']);
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Cp932));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task CommentCodecAndImplicitAggregateErrorsRejectBeforeSend()
    {
        await using var server = new RawContractServer(_ => "COMMENT\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = client.ReadCommentsAsync("DM100", (HostLinkCommentEncoding)99);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = client.ReadNamedAsync(["DM100:COMMENT"], (HostLinkCommentEncoding)99);
        });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            _ = client.PollAsync(
                ["DM100:COMMENT"],
                TimeSpan.FromSeconds(1),
                (HostLinkCommentEncoding)99);
        });
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadNamedAsync(["DM100:COMMENT"]));

        Assert.Empty(server.Commands);
        Assert.True(client.IsOpen);
    }

    [Fact]
    public async Task ExplicitAggregateCommentCodecRequiresACommentBeforeSend()
    {
        await using var server = new RawContractServer(_ => "0\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        ArgumentException namedError = await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadNamedAsync(["DM100:U"], HostLinkCommentEncoding.Utf8));
        Assert.Equal("commentEncoding", namedError.ParamName);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.ReadNamedAsync(Array.Empty<string>(), HostLinkCommentEncoding.Cp932));

        await using (var polling = client.PollAsync(
            ["DM100:U"],
            TimeSpan.FromSeconds(1),
            HostLinkCommentEncoding.Cp932).GetAsyncEnumerator())
        {
            ArgumentException pollError = await Assert.ThrowsAsync<ArgumentException>(() =>
                polling.MoveNextAsync().AsTask());
            Assert.Equal("commentEncoding", pollError.ParamName);
        }

        Assert.Empty(server.Commands);
        Assert.True(client.IsOpen);
    }

    [Fact]
    public async Task CommentRawAndTextPathsTranslatePlcErrorWithoutClosingConnection()
    {
        await using var server = new RawContractServer(_ => "E1\r"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);

        await Assert.ThrowsAsync<HostLinkError>(() => client.ReadCommentBytesAsync("DM100"));
        await Assert.ThrowsAsync<HostLinkError>(() =>
            client.ReadCommentsAsync("DM100", HostLinkCommentEncoding.Utf8));
        Assert.True(client.IsOpen);
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

        var error = await Assert.ThrowsAsync<HostLinkOutcomeUnknownError>(() => client.SendRawAsync("OVERSIZED"));
        Assert.Equal(HostLinkOutcomeUnknownReason.InvalidResponse, error.Reason);
        Assert.IsType<HostLinkProtocolError>(error.InnerException);
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

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadWordsSingleRequestAsync("DM0", 1001));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteWordsSingleRequestAsync("DM0", new ushort[1001]));
        Assert.Empty(server.Commands);
    }

    [Fact]
    public async Task BitAndWordSingleRequestHelpersSendOneRequestOrRejectBeforeSend()
    {
        await using var server = new RawContractServer(command => command switch
        {
            "RDS R5000 3" => "0 1 1\r"u8.ToArray(),
            "WRS R5000 3 0 1 1" => "OK\r"u8.ToArray(),
            "RDS DM0.U 2" => "1 2\r"u8.ToArray(),
            "WRS DM0.U 2 1 2" => "OK\r"u8.ToArray(),
            _ => "E1\r"u8.ToArray(),
        });
        await using var client = await OpenClientAsync(server.Port);

        bool[] actualBits = await client.ReadBitsSingleRequestAsync("R5000", 3);
        Assert.Equal(3, actualBits.Length);
        Assert.False(actualBits[0]);
        Assert.True(actualBits[1]);
        Assert.True(actualBits[2]);
        await client.WriteBitsSingleRequestAsync("R5000", [false, true, true]);
        ushort[] actualWords = await client.ReadWordsSingleRequestAsync("DM0", 2);
        Assert.Equal([1, 2], actualWords);
        await client.WriteWordsSingleRequestAsync("DM0", new ushort[] { 1, 2 });
#pragma warning disable CS0618
        ushort[] aliasWords = await client.ReadWordsAsync("DM0", 2);
#pragma warning restore CS0618
        Assert.Equal([1, 2], aliasWords);
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadBitsSingleRequestAsync("DM0", 1));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.ReadBitsSingleRequestAsync("R5000", 1001));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteBitsSingleRequestAsync("DM0", [true]));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteBitsSingleRequestAsync("R5000", new bool[1001]));

        Assert.Equal(
            ["RDS R5000 3", "WRS R5000 3 0 1 1", "RDS DM0.U 2", "WRS DM0.U 2 1 2", "RDS DM0.U 2"],
            server.Commands.ToArray());
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
    public void PublicSurfaceExposesExplicitBooleanWordBitHelper()
    {
        MethodInfo method = Assert.Single(
            typeof(KvHostLinkClientExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static),
            candidate => candidate.Name == "WriteBitInWordAsync");
        ParameterInfo[] parameters = method.GetParameters();
        Assert.Equal(typeof(KvHostLinkClient), parameters[0].ParameterType);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal(typeof(int), parameters[2].ParameterType);
        Assert.Equal(typeof(bool), parameters[3].ParameterType);
        Assert.Equal(typeof(CancellationToken), parameters[4].ParameterType);

        MethodInfo expansion = Assert.Single(
            typeof(KvHostLinkClientExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static),
            candidate => candidate.Name == "WriteBitInExpansionUnitBufferAsync");
        ParameterInfo[] expansionParameters = expansion.GetParameters();
        Assert.Equal(typeof(KvHostLinkClient), expansionParameters[0].ParameterType);
        Assert.Equal(typeof(int), expansionParameters[1].ParameterType);
        Assert.Equal(typeof(int), expansionParameters[2].ParameterType);
        Assert.Equal(typeof(int), expansionParameters[3].ParameterType);
        Assert.Equal(typeof(bool), expansionParameters[4].ParameterType);
        Assert.Equal(typeof(CancellationToken), expansionParameters[5].ParameterType);
    }

    [Fact]
    public async Task TraceHookObservesExactFramesOnceAndCannotBreakCommand()
    {
        await using var server = new RawContractServer(_ => "57\r\n"u8.ToArray());
        await using var client = await OpenClientAsync(server.Port);
        var frames = new List<HostLinkTraceFrame>();
        client.TraceHook = frame =>
        {
            frames.Add(frame with { Data = frame.Data.ToArray() });
            frame.Data.AsSpan().Fill((byte)'X');
            throw new InvalidOperationException("diagnostic failure");
        };

        KvModelInfo model = await client.QueryModelAsync();

        Assert.Equal("57", model.Code);
        Assert.Equal(2, frames.Count);
        Assert.Equal("?K\r"u8.ToArray(), frames[0].Data);
        Assert.Equal("57\r\n"u8.ToArray(), frames[1].Data);
    }

    [Fact]
    public async Task NormalClientGateHonorsCallerCancellationWhileWaitingWithoutSending()
    {
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var server = new RawContractServer(command =>
        {
            release.Task.GetAwaiter().GetResult();
            return command == "RD DM0.U" ? "1\r"u8.ToArray() : "E1\r"u8.ToArray();
        });
        await using var client = await OpenClientAsync(server.Port);
        Task<string[]> holder = client.ReadAsync("DM0", ".U");
        await Task.Delay(30);
        using var cancellation = new CancellationTokenSource();
        Task<string[]> waiting = client.ReadAsync("DM1", ".U", cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
        release.SetResult();
        await holder;
        Assert.Equal(["RD DM0.U"], server.Commands.ToArray());
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
