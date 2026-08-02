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
        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var result = await client.ReadNamedAsync(
            ["DM100:U", "DM100.0", "DM100.A", "DM101:S", "DM102:D", "DM104:L", "DM106:F"]);

        Assert.Equal((ushort)1025, Assert.IsType<ushort>(result["DM100:U"]));
        Assert.True(Assert.IsType<bool>(result["DM100.0"]));
        Assert.True(Assert.IsType<bool>(result["DM100.A"]));
        Assert.Equal((short)-1, Assert.IsType<short>(result["DM101:S"]));
        Assert.Equal((uint)65538, Assert.IsType<uint>(result["DM102:D"]));
        Assert.Equal(123456, Assert.IsType<int>(result["DM104:L"]));
        Assert.Equal(12.5f, Assert.IsType<float>(result["DM106:F"]));

        Assert.Equal(["RDS DM100.U 8"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_GroupsByFirstAppearanceSortsWithinGroupAndPreservesResultOrder()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS EM1.U 2" => "11 12",
            "RDS DM1.U 3" => "10 20 30",
            "RD Z1.D" => "70000",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        string[] addresses = ["EM2:U", "DM3:U", "Z1:D", "DM1:H", "DM2:S", "EM1:U"];

        IReadOnlyDictionary<string, object> result = await client.ReadNamedAsync(addresses);

        Assert.Equal(addresses, result.Keys);
        Assert.Equal((ushort)12, result["EM2:U"]);
        Assert.Equal((ushort)30, result["DM3:U"]);
        Assert.Equal((uint)70_000, result["Z1:D"]);
        Assert.Equal("000A", result["DM1:H"]);
        Assert.Equal((short)20, result["DM2:S"]);
        Assert.Equal((ushort)11, result["EM1:U"]);
        Assert.Equal(
            ["RDS EM1.U 2", "RDS DM1.U 3", "RD Z1.D"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_AlternatingDeviceTypesDoNotSplitACompatibleGroup()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS DM10.U 2" => "10 11",
            "RDS MR000 1" => "1",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        string[] addresses = ["DM11:U", "MR0", "DM10:U"];

        IReadOnlyDictionary<string, object> result = await client.ReadNamedAsync(addresses);

        Assert.Equal(addresses, result.Keys);
        Assert.Equal((ushort)11, result["DM11:U"]);
        Assert.True(Assert.IsType<bool>(result["MR0"]));
        Assert.Equal((ushort)10, result["DM10:U"]);
        Assert.Equal(["RDS DM10.U 2", "RDS MR000 1"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_BatchesCompatibleReadsWhenCommentIsAlsoPresent()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS DM100.U 1" => "1025",
            "RDC DM101" => "MAIN COMMENT                    ",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var result = await client.ReadNamedAsync(
            ["DM100:U", "DM101:COMMENT"],
            HostLinkCommentEncoding.Utf8);

        Assert.Equal((ushort)1025, Assert.IsType<ushort>(result["DM100:U"]));
        Assert.Equal("MAIN COMMENT", Assert.IsType<string>(result["DM101:COMMENT"]));
        Assert.Equal(["RDS DM100.U 1", "RDC DM101"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_BatchesBitBankDirectBitsAcrossDisplayBankBoundary()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS CR3614 4" => "0 1 0 1",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var result = await client.ReadNamedAsync(["CR3614", "CR3615", "CR3700", "CR3701"]);

        Assert.False(Assert.IsType<bool>(result["CR3614"]));
        Assert.True(Assert.IsType<bool>(result["CR3615"]));
        Assert.False(Assert.IsType<bool>(result["CR3700"]));
        Assert.True(Assert.IsType<bool>(result["CR3701"]));
        Assert.Equal(["RDS CR3614 4"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_AcceptsExplicitBitLogicalTypeForDirectBits()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS R5000 3" => "0 1 0",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var result = await client.ReadNamedAsync(["R5000:BIT", "R5001:BIT", "R5002:BIT"]);

        Assert.False(Assert.IsType<bool>(result["R5000:BIT"]));
        Assert.True(Assert.IsType<bool>(result["R5001:BIT"]));
        Assert.False(Assert.IsType<bool>(result["R5002:BIT"]));
        Assert.Equal(["RDS R5000 3"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task DirectBitWordReadsPackEveryReturnedBitInProtocolOrder()
    {
        static string Bits(params int[] setBits) => string.Join(
            ' ',
            Enumerable.Range(0, setBits.Contains(31) ? 32 : 16)
                .Select(bit => setBits.Contains(bit) ? "1" : "0"));

        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD R000.U" => Bits(0, 3, 15),
            "RD R000.S" => Bits(0, 3, 15),
            "RD R000.D" => Bits(0, 16, 31),
            "RD R000.L" => Bits(0, 16, 31),
            "RD R000.H" => Bits(0, 3, 15),
            _ => "E1",
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        Assert.Equal((ushort)0x8009, Assert.IsType<ushort>(await client.ReadTypedAsync("R0", "U")));
        Assert.Equal(unchecked((short)0x8009), Assert.IsType<short>(await client.ReadTypedAsync("R0", "S")));
        Assert.Equal(0x8001_0001u, Assert.IsType<uint>(await client.ReadTypedAsync("R0", "D")));
        Assert.Equal(unchecked((int)0x8001_0001u), Assert.IsType<int>(await client.ReadTypedAsync("R0", "L")));
        Assert.Equal("8009", Assert.IsType<string>(await client.ReadTypedAsync("R0", "H")));
    }

    [Fact]
    public async Task DirectBitBitInWordReadPreservesEveryOtherBit()
    {
        string initialBits = string.Join(
            ' ',
            Enumerable.Range(0, 16).Select(bit => bit is 0 or 3 or 15 ? "1" : "0"));
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD R000.U" => initialBits,
            _ => "E1",
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var named = await client.ReadNamedAsync(["R0.0", "R0.3", "R0.F"]);
        Assert.True(Assert.IsType<bool>(named["R0.0"]));
        Assert.True(Assert.IsType<bool>(named["R0.3"]));
        Assert.True(Assert.IsType<bool>(named["R0.F"]));

        Assert.Equal(
            ["RD R000.U", "RD R000.U", "RD R000.U"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadNamedAsync_SplitsContiguousRdsPlanAtPointLimit()
    {
        await using var server = new ScriptedHostLinkServer(command =>
        {
            string[] parts = command.Split(' ');
            return parts is ["RDS", _, var count]
                ? string.Join(' ', Enumerable.Repeat("7", int.Parse(count, System.Globalization.CultureInfo.InvariantCulture)))
                : "E1";
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        string[] addresses = Enumerable.Range(0, 2001).Select(index => $"DM{index}:U").ToArray();

        var result = await client.ReadNamedAsync(addresses);

        Assert.Equal(2001, result.Count);
        Assert.All(addresses, address => Assert.Equal((ushort)7, Assert.IsType<ushort>(result[address])));
        Assert.Equal(
            ["RDS DM0.U 1000", "RDS DM1000.U 1000", "RDS DM2000.U 1"],
            server.ReceivedCommands.ToArray());
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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var value = await client.ReadTypedAsync("DM200", "F");
        await client.WriteTypedAsync("DM200", "F", 12.5f);

        Assert.Equal(12.5f, Assert.IsType<float>(value));
        Assert.Equal(["RDS DM200.U 2", "WRS DM200.U 2 0 16712"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task WriteTypedAsync_Float32RequiresFiniteBinary32BeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await client.WriteTypedAsync("DM0", "F", float.MaxValue);
        await client.WriteTypedAsync("DM2", "F", -float.MaxValue);
        int acceptedCommands = server.ReceivedCommands.Count;

        foreach (double invalid in new[]
        {
            double.MaxValue,
            double.MinValue,
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
        })
        {
            await Assert.ThrowsAsync<HostLinkProtocolError>(
                () => client.WriteTypedAsync("DM4", "F", invalid));
        }

        Assert.Equal(2, acceptedCommands);
        Assert.Equal(acceptedCommands, server.ReceivedCommands.Count);
    }

    [Theory]
    [InlineData("R0")]
    [InlineData("B0")]
    [InlineData("MR0")]
    [InlineData("LR0")]
    [InlineData("CR0")]
    [InlineData("VB0")]
    [InlineData("X0")]
    [InlineData("Y0")]
    [InlineData("M0")]
    [InlineData("L0")]
    public async Task WriteTypedAsync_RejectsFloatForEveryDirectBitFamilyBeforeSend(string device)
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.WriteTypedAsync(device, "F", 12.5f));

        Assert.Empty(server.ReceivedCommands);
    }

    [Fact]
    public async Task NormalClientWriteTypedAsync_RejectsDirectBitFloatBeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.WriteTypedAsync("R0", "F", 12.5f));

        Assert.Empty(server.ReceivedCommands);
    }

    [Theory]
    [InlineData("R0")]
    [InlineData("T0")]
    [InlineData("C0")]
    [InlineData("AT0")]
    [InlineData("Z0")]
    public async Task Float32TypedNamedAndPollingEntriesRejectIneligibleFamiliesBeforeTransport(string device)
    {
        await using var client = new KvHostLinkClient(
            "127.0.0.1", 1, HostLinkTransportMode.Tcp, TestPlcProfile);
        string address = $"{device}:F";

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadTypedAsync(device, "F"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.WriteTypedAsync(device, "F", 12.5f));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadNamedAsync([address]));
        await using var polling = client.PollAsync([address], TimeSpan.FromMilliseconds(1)).GetAsyncEnumerator();
        await Assert.ThrowsAsync<HostLinkProtocolError>(async () => await polling.MoveNextAsync());

        Assert.False(client.IsOpen);
        Assert.Equal(default, client.TrafficStats);
    }

    [Fact]
    public async Task Float32RejectionOccursBeforeWaitingForTheClientFifo()
    {
        using var requestReceived = new ManualResetEventSlim();
        using var releaseResponse = new ManualResetEventSlim();
        await using var server = new ScriptedHostLinkServer(command =>
        {
            if (command == "RD DM0.U")
            {
                requestReceived.Set();
                releaseResponse.Wait();
                return "1";
            }

            return "E1";
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        Task<string[]> active = client.ReadAsync("DM0", ".U");
        Assert.True(requestReceived.Wait(TimeSpan.FromSeconds(2)));
        try
        {
            TimeSpan admissionDeadline = TimeSpan.FromSeconds(1);
            await Assert.ThrowsAsync<HostLinkProtocolError>(
                () => client.ReadTypedAsync("T0", "F").WaitAsync(admissionDeadline));
            await Assert.ThrowsAsync<HostLinkProtocolError>(
                () => client.WriteTypedAsync("C0", "F", 12.5f).WaitAsync(admissionDeadline));
            await Assert.ThrowsAsync<HostLinkProtocolError>(
                () => client.ReadNamedAsync(["AT0:F"]).WaitAsync(admissionDeadline));
            await using var polling = client.PollAsync(["R0:F"], TimeSpan.FromMilliseconds(1)).GetAsyncEnumerator();
            await Assert.ThrowsAsync<HostLinkProtocolError>(
                async () => await polling.MoveNextAsync().AsTask().WaitAsync(admissionDeadline));

            Assert.Equal(["RD DM0.U"], server.ReceivedCommands.ToArray());
        }
        finally
        {
            releaseResponse.Set();
        }

        Assert.Equal(["1"], await active);
        Assert.Equal(1UL, client.TrafficStats.RequestCount);
    }

    [Fact]
    public async Task ReadTypedAsync_WriteTypedAsync_And_ReadNamedAsync_SupportHexSuffix()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD DM210.H" => "f",
            "WR DM210.H FF" => "OK",
            "WR DM211.H AA" => "OK",
            "RDS DM212.U 1" => "10",
            "RD DM213.H" => "f",
            "RDS DM214.H 2" => "0 ff",
            "RDE DM216.H 2" => "a ffff",
            "URD 01 10.H 2" => "1 b",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var value = await client.ReadTypedAsync("DM210", "H");
        await client.WriteTypedAsync("DM210", "H", (ushort)0x00FF);
        await client.WriteTypedAsync("DM211", "H", (ushort)0x00AA);
        var named = await client.ReadNamedAsync(["DM212:H"]);
        var lowLevel = await client.ReadAsync("DM213", ".H");
        var consecutive = await client.ReadConsecutiveAsync("DM214", 2, ".H");
        var legacy = await client.ReadConsecutiveLegacyAsync("DM216", 2, ".H");
        var expansion = await client.ReadExpansionUnitBufferAsync(1, 10, 2, ".H");

        Assert.Equal("000F", Assert.IsType<string>(value));
        Assert.Equal("000A", Assert.IsType<string>(named["DM212:H"]));
        Assert.Equal(["000F"], lowLevel);
        Assert.Equal(["0000", "00FF"], consecutive);
        Assert.Equal(["000A", "FFFF"], legacy);
        Assert.Equal(["0001", "000B"], expansion);
        Assert.Equal(
            [
                "RD DM210.H", "WR DM210.H FF", "WR DM211.H AA", "RDS DM212.U 1", "RD DM213.H",
                "RDS DM214.H 2", "RDE DM216.H 2", "URD 01 10.H 2"
            ],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task MonitorWordReadUsesEachRegisteredFormat()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "MWS DM0.U DM1.S DM2.H" => "OK",
            "MWR" => "1,-2,00ff",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await client.RegisterMonitorWordsAsync(
        [
            new("DM0", ".U"),
            new("DM1", ".S"),
            new("DM2", ".H"),
        ]);
        string[] values = await client.ReadMonitorWordsAsync();

        Assert.Equal(["1", "-2", "00FF"], values);
        Assert.Equal(["MWS DM0.U DM1.S DM2.H", "MWR"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task FactoryPreservesOpenFailure()
    {
        var options = new KvHostLinkConnectionOptions(
            "127.0.0.1", 1, HostLinkTransportMode.Tcp, TestPlcProfile);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => KvHostLinkClientFactory.OpenAndConnectAsync(options, cancellation.Token));

        Assert.Equal(cancellation.Token, error.CancellationToken);
    }

    [Fact]
    public async Task ReadTypedAsync_TimerCounterCompositeReadReturnsSetValue()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD T0.D" => "0,0000000010,0000000020",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var result = await client.ReadNamedAsync(["Z1:D"]);

        Assert.Equal((uint)70_000, Assert.IsType<uint>(result["Z1:D"]));
        Assert.Equal(["RD Z1.D"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task SetTimeAsync_UsesSundayBasedWeekday()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

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
    public async Task SetTimeAsync_RejectsYearsOutside2000Through2099BeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SetTimeAsync(new DateTime(1999, 12, 31)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.SetTimeAsync(new DateTime(2100, 1, 1)));

        Assert.Empty(server.ReceivedCommands);
    }

    [Fact]
    public async Task ReadAsync_ValidatesDeviceDerivedResponseTokenCount()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD R000.U" => string.Join(' ', Enumerable.Repeat("0", 16)),
            "RD R000.D" => string.Join(' ', Enumerable.Repeat("0", 32)),
            "RD DM0.U" => "123",
            "RD R001" => "ON",
            "RD R002" => "GARBAGE",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        Assert.Equal(16, (await client.ReadAsync("R0", ".U")).Length);
        Assert.Equal(32, (await client.ReadAsync("R0", ".D")).Length);
        Assert.Equal(["123"], await client.ReadAsync("DM0", ".U"));
        Assert.Equal(["ON"], await client.ReadAsync("R1"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync("R2"));
        Assert.False(client.IsOpen);
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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var result = await client.ReadNamedAsync(["T10:D", "C10:D"]);

        Assert.Equal((uint)20, Assert.IsType<uint>(result["T10:D"]));
        Assert.Equal((uint)30, Assert.IsType<uint>(result["C10:D"]));
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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var result = await client.ReadTimerCounterAsync("T10");

        Assert.Equal((uint)1, result.Status);
        Assert.Equal((uint)10, result.Current);
        Assert.Equal((uint)20, result.Preset);
        Assert.Equal(["RD T10.D"], server.ReceivedCommands.ToArray());
    }

    [Theory]
    [InlineData("RD T0.D", "2,10,20", "D")]
    [InlineData("RD C0.L", "-1,10,20", "L")]
    public async Task TimerCounterStatusMustBeZeroOrOneInSharedReadParser(
        string command,
        string response,
        string dtype)
    {
        await using var server = new ScriptedHostLinkServer(received => received == command ? response : "E1");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        string device = command.Contains(" T", StringComparison.Ordinal) ? "T0" : "C0";
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadTypedAsync(device, dtype));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task OpenAndConnectAsync_ReturnsNormalClientWithIntegratedFifo()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD DM10.U" => "123",
            _ => "E1",
        });

        await using var client = await KvHostLinkClientExtensions.OpenAndConnectAsync("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        var value = await client.ReadTypedAsync("DM10", "U");

        Assert.True(client.IsOpen);
        Assert.Equal((ushort)123, Assert.IsType<ushort>(value));
        Assert.Equal(["RD DM10.U"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task NormalClient_ReadCommentsAsync_UsesRdcCommand()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDC DM10" => "ALARM TEXT                      ",
            _ => "E1",
        });

        await using var client = await KvHostLinkClientExtensions.OpenAndConnectAsync("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        var comment = await client.ReadCommentsAsync("DM10", HostLinkCommentEncoding.Utf8);

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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        var dataMemoryComment = await client.ReadCommentsAsync("D10", HostLinkCommentEncoding.Utf8);
        var auxiliaryRelayComment = await client.ReadCommentsAsync("M20", HostLinkCommentEncoding.Utf8);

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
            "MWS D100.U E100.U F100.U MR100.U LR100.U" => "OK",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await client.ForcedSetAsync("X100");
        await client.ForcedResetAsync("M100");
        await client.ForcedSetConsecutiveAsync("L100", 4);
        await client.RegisterMonitorWordsAsync(
        [
            new("D100", ".U"),
            new("E100", ".U"),
            new("F100", ".U"),
            new("MR100", ".U"),
            new("LR100", ".U"),
        ]);
        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.RegisterMonitorWordsAsync([new("M100", "")]));
        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.RegisterMonitorWordsAsync([new("L100.U", ".U")]));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ForcedSetConsecutiveAsync("T100", 4));

        Assert.Equal(
            ["ST X100", "RS M100", "STS L100 4", "MWS D100.U E100.U F100.U MR100.U LR100.U"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task WssTimerCounterCountLimit_IsEnforcedBeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.WriteSetValueConsecutiveAsync("T0", Enumerable.Repeat(0, 121), ".D"));

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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        var snapshots = new List<IReadOnlyDictionary<string, object>>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await foreach (var snapshot in client.PollAsync(
            ["DM100:U", "DM100.0", "DM101:F"],
            TimeSpan.FromMilliseconds(1),
            cts.Token))
        {
            snapshots.Add(snapshot);
            if (snapshots.Count >= 2)
                break;
        }

        Assert.Equal(2, snapshots.Count);
        Assert.Equal((ushort)1, Assert.IsType<ushort>(snapshots[0]["DM100:U"]));
        Assert.True(Assert.IsType<bool>(snapshots[0]["DM100.0"]));
        Assert.Equal(1.5f, Assert.IsType<float>(snapshots[0]["DM101:F"]));
        Assert.Equal((ushort)3, Assert.IsType<ushort>(snapshots[1]["DM100:U"]));
        Assert.True(Assert.IsType<bool>(snapshots[1]["DM100.0"]));
        Assert.Equal(2.5f, Assert.IsType<float>(snapshots[1]["DM101:F"]));

        Assert.Equal(
            ["RDS DM100.U 3", "RDS DM100.U 3"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task PollAsync_ReleasesFifoDuringCompletionDelay()
    {
        int pollResponse = 0;
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS DM0.U 1" => (++pollResponse).ToString(System.Globalization.CultureInfo.InvariantCulture),
            "RD DM9.U" => "9",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await using var polling = client.PollAsync(
            ["DM0:U"], TimeSpan.FromMilliseconds(100), cancellation.Token).GetAsyncEnumerator();

        Assert.True(await polling.MoveNextAsync());
        Task<bool> secondCycle = polling.MoveNextAsync().AsTask();
        Assert.Equal(["9"], await client.ReadAsync("DM9", ".U"));
        Assert.True(await secondCycle);

        Assert.Equal(
            ["RDS DM0.U 1", "RD DM9.U", "RDS DM0.U 1"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task PollAsync_RejectsNonPositiveIntervalBeforeCommunication()
    {
        await using var client = new KvHostLinkClient(
            "127.0.0.1", 8501, HostLinkTransportMode.Tcp, TestPlcProfile);

        foreach (TimeSpan interval in new[] { TimeSpan.Zero, TimeSpan.FromMilliseconds(-1) })
        {
            await using var polling = client.PollAsync(["DM0:U"], interval).GetAsyncEnumerator();
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await polling.MoveNextAsync());
        }

        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task PollAsync_CommentsRequireExplicitEncodingBeforeFirstSend()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDC DM100" => "COMMENT                       ",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1",
            server.Port,
            HostLinkTransportMode.Tcp,
            TestPlcProfile);
        await client.OpenAsync();

        await using (var implicitCodec = client.PollAsync(
            ["DM100:COMMENT"],
            TimeSpan.FromMilliseconds(1)).GetAsyncEnumerator())
        {
            await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
                implicitCodec.MoveNextAsync().AsTask());
        }
        Assert.Empty(server.ReceivedCommands);

        await using var explicitCodec = client.PollAsync(
            ["DM100:COMMENT"],
            TimeSpan.FromMilliseconds(1),
            HostLinkCommentEncoding.Utf8).GetAsyncEnumerator();
        Assert.True(await explicitCodec.MoveNextAsync());
        Assert.Equal("COMMENT", Assert.IsType<string>(explicitCodec.Current["DM100:COMMENT"]));
        Assert.Equal(["RDC DM100"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadDWordsAsync_UsesOneNativeDwordRequest()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RDS DM200.D 3" => "65537 131074 196611",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        var values = await client.ReadDWordsAsync("DM200", 3);

        Assert.Equal(new uint[] { 65537, 131074, 196611 }, values);
        Assert.Equal(
            ["RDS DM200.D 3"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task WriteDWordsAsync_UsesOneNativeDwordRequest()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "WRS DM200.D 3 65537 131074 196611" => "OK",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        await client.WriteDWordsSingleRequestAsync("DM200", new uint[] { 65537, 131074, 196611 });

        Assert.Equal(
            ["WRS DM200.D 3 65537 131074 196611"],
            server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadAsync_DoesNotReject32BitDeviceAtCatalogUpperBoundary()
    {
        await using var server = new ScriptedHostLinkServer(command => command == "RD DM65534.D" ? "1" : "E1");
        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        Assert.Equal(["1"], await client.ReadAsync("DM65534", ".D"));

        Assert.Equal(["RD DM65534.D"], server.ReceivedCommands.ToArray());
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

        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        string[] values = await client.ReadExpansionUnitBufferAsync(1, 100, 2, ".U");
        int[] valuesToWrite = [7, 8];
        await client.WriteExpansionUnitBufferAsync(2, 200, valuesToWrite, ".S");

        Assert.Equal(["123", "456"], values);
        Assert.Equal(["URD 01 100.U 2", "UWR 02 200.S 2 7 8"], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task ReadExpansionUnitBufferAsync_Rejects32BitBufferEndCrossingBeforeSend()
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient("127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

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
