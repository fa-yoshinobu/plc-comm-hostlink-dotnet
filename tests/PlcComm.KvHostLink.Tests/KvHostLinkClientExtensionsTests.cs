using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using PlcComm.KvHostLink;

namespace PlcComm.KvHostLink.Tests;

public sealed class KvHostLinkClientExtensionsTests
{
    private const string TestPlcProfile = "keyence:kv-8000";

    [Theory]
    [InlineData(true, "0", "WR DM100.U 8")]
    [InlineData(true, "8", "WR DM100.U 8")]
    [InlineData(false, "8", "WR DM100.U 0")]
    public async Task WriteBitInWordAsync_AlwaysReadsThenWritesOneWord(
        bool value,
        string readValue,
        string expectedWrite)
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD DM100.U" => readValue,
            var write when write == expectedWrite => "OK",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await client.WriteBitInWordAsync("DM100", 3, value);

        Assert.Equal(["RD DM100.U", expectedWrite], server.ReceivedCommands.ToArray());
    }

    [Fact]
    public async Task WriteBitInWordAsync_CoversEveryExistingCompleteWordRouteWithoutFallback()
    {
        string[] devices = ["DM0", "EM0", "FM0", "ZF0", "W0", "TM0", "Z0", "CM0", "VM0", "D0", "E0", "F0"];
        await using var server = new ScriptedHostLinkServer(command =>
            command.StartsWith("RD ", StringComparison.Ordinal) ? "0" : "OK");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        foreach (string device in devices)
            await client.WriteBitInWordAsync(device, 0, false);

        Assert.Equal(
            devices.SelectMany(device => new[] { $"RD {device}.U", $"WR {device}.U 0" }),
            server.ReceivedCommands);
    }

    [Theory]
    [InlineData("R0", 0)]
    [InlineData("T0", 0)]
    [InlineData("AT0", 0)]
    [InlineData("DM100.0", 0)]
    [InlineData("DM100", -1)]
    [InlineData("DM100", 16)]
    public async Task WriteBitInWordAsync_RejectsInvalidInputBeforeTransport(string device, int bitIndex)
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.WriteBitInWordAsync(device, bitIndex, true));

        Assert.Empty(server.ReceivedCommands);
    }

    [Fact]
    public async Task WriteBitInWordAsync_MalformedReadSendsNoWriteAndRetiresConnection()
    {
        await using var server = new ScriptedHostLinkServer(_ => "NOT_A_WORD");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteBitInWordAsync("DM100", 3, true));

        Assert.Equal(["RD DM100.U"], server.ReceivedCommands.ToArray());
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task WriteBitInWordAsync_CompletePlcWriteErrorIsDefinitiveAndConnectionRemainsReusable()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD DM100.U" => "0",
            "WR DM100.U 1" => "E1",
            "RD DM1.U" => "7",
            _ => "E2",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        HostLinkError error = await Assert.ThrowsAsync<HostLinkError>(() =>
            client.WriteBitInWordAsync("DM100", 0, true));

        Assert.IsNotType<HostLinkOutcomeUnknownError>(error);
        Assert.Equal(["7"], await client.ReadAsync("DM1", ".U"));
        Assert.Equal(
            ["RD DM100.U", "WR DM100.U 1", "RD DM1.U"],
            server.ReceivedCommands.ToArray());
    }

    [Theory]
    [InlineData(true, "0", "UWR 01 100.U 1 8")]
    [InlineData(true, "8", "UWR 01 100.U 1 8")]
    [InlineData(false, "8", "UWR 01 100.U 1 0")]
    public async Task WriteBitInExpansionUnitBufferAsync_AlwaysUsesOneImmutableUrdUwrRoute(
        bool value,
        string readValue,
        string expectedWrite)
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "URD 01 100.U 1" => readValue,
            var write when write == expectedWrite => "OK",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await client.WriteBitInExpansionUnitBufferAsync(1, 100, 3, value);

        Assert.Equal(["URD 01 100.U 1", expectedWrite], server.ReceivedCommands.ToArray());
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(49, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 60000, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 16)]
    public async Task WriteBitInExpansionUnitBufferAsync_RejectsInvalidPlanBeforeTransport(
        int unitNo,
        int address,
        int bitIndex)
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAnyAsync<Exception>(() =>
            client.WriteBitInExpansionUnitBufferAsync(unitNo, address, bitIndex, true));

        Assert.Empty(server.ReceivedCommands);
    }

    [Fact]
    public async Task WriteBitInExpansionUnitBufferAsync_MalformedReadSendsNoWriteAndRetiresConnection()
    {
        await using var server = new ScriptedHostLinkServer(_ => "NOT_A_WORD");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(() =>
            client.WriteBitInExpansionUnitBufferAsync(1, 100, 3, true));

        Assert.Equal(["URD 01 100.U 1"], server.ReceivedCommands.ToArray());
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task WriteBitInExpansionUnitBufferAsync_PlcWriteErrorIsDefinitiveAndReusable()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "URD 01 100.U 1" => "0",
            "UWR 01 100.U 1 1" => "E1",
            "RD DM1.U" => "7",
            _ => "E2",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        HostLinkError error = await Assert.ThrowsAsync<HostLinkError>(() =>
            client.WriteBitInExpansionUnitBufferAsync(1, 100, 0, true));

        Assert.IsNotType<HostLinkOutcomeUnknownError>(error);
        Assert.Equal(["7"], await client.ReadAsync("DM1", ".U"));
        Assert.Equal(
            ["URD 01 100.U 1", "UWR 01 100.U 1 1", "RD DM1.U"],
            server.ReceivedCommands.ToArray());
    }

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
    public async Task DirectBitWordReadsAcceptOnePackedScalarTokenInTheRequestedFormat()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD R000.U" => "32777",
            "RD R000.S" => "+00000",
            "RD R000.D" => "2147549185",
            "RD R000.L" => "+0000000000",
            "RD R000.H" => "8009",
            _ => "E1",
        });

        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        Assert.Equal((ushort)0x8009, Assert.IsType<ushort>(await client.ReadTypedAsync("R0", "U")));
        Assert.Equal((short)0, Assert.IsType<short>(await client.ReadTypedAsync("R0", "S")));
        Assert.Equal(0x8001_0001u, Assert.IsType<uint>(await client.ReadTypedAsync("R0", "D")));
        Assert.Equal(0, Assert.IsType<int>(await client.ReadTypedAsync("R0", "L")));
        Assert.Equal("8009", Assert.IsType<string>(await client.ReadTypedAsync("R0", "H")));
    }

    [Fact]
    public async Task DirectBitBitInWordReadPreservesEveryOtherBit()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "RD R000.U" => "32777",
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

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("13")]
    [InlineData("00000")]
    [InlineData("00002")]
    [InlineData("00013")]
    [InlineData("65535")]
    public async Task BareDirectBitMonitorWordUsesExactWireAndPackedUnsigned16Response(string response)
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "MWS R5000" => "OK",
            "MWR" => response,
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await client.RegisterMonitorWordsAsync([new KvMonitorWordTarget("R5000")]);

        Assert.Equal([response], await client.ReadMonitorWordsAsync());
        Assert.Equal(["MWS R5000", "MWR"], server.ReceivedCommands.ToArray());
        Assert.True(client.IsOpen);
    }

    [Fact]
    public async Task MixedMonitorWordTargetsPreserveOrderAndIndependentFormats()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "MWS R5000 DM0.U DM1.S DM2.H DM3.D DM5.L R5100" => "OK",
            "MWR" => "00002 2 +00002 000f 0000000002 +000000002 00013",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await client.RegisterMonitorWordsAsync(
        [
            new("R5000"),
            new("DM0", ".U"),
            new("DM1", ".S"),
            new("DM2", ".H"),
            new("DM3", ".D"),
            new("DM5", ".L"),
            new("R5100"),
        ]);

        Assert.Equal(
            ["00002", "2", "+00002", "000F", "0000000002", "+000000002", "00013"],
            await client.ReadMonitorWordsAsync());
        Assert.True(client.IsOpen);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("00\t02")]
    [InlineData("00 02")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("65536")]
    [InlineData("00A02")]
    [InlineData("2.0")]
    [InlineData("000000")]
    public async Task InvalidBareDirectBitMonitorWordResponseRetiresConnection(string response)
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "MWS R5000" => "OK",
            "MWR" => response,
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        await client.RegisterMonitorWordsAsync([new("R5000")]);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadMonitorWordsAsync());

        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task OmittedOrBlankMonitorFormatIsRejectedExceptForDirectBitNull(string? dataFormat)
    {
        await using var server = new ScriptedHostLinkServer(_ => "OK");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        KvMonitorWordTarget target = dataFormat is null
            ? new("DM0")
            : new("R5000", dataFormat);
        await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.RegisterMonitorWordsAsync([target]));

        Assert.Empty(server.ReceivedCommands);
        Assert.True(client.IsOpen);
    }

    [Fact]
    public async Task ScalarAndBitMonitorReadsRemainStrictBits()
    {
        await using var scalarServer = new ScriptedHostLinkServer(command =>
            command == "RD R5000" ? "00002" : "E1");
        await using var scalarClient = new KvHostLinkClient(
            "127.0.0.1", scalarServer.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await scalarClient.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => scalarClient.ReadAsync("R5000"));
        Assert.False(scalarClient.IsOpen);

        await using var monitorServer = new ScriptedHostLinkServer(command => command switch
        {
            "MBS R5000" => "OK",
            "MBR" => "00002",
            _ => "E1",
        });
        await using var monitorClient = new KvHostLinkClient(
            "127.0.0.1", monitorServer.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await monitorClient.OpenAsync();
        await monitorClient.RegisterMonitorBitsAsync(["R5000"]);

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => monitorClient.ReadMonitorBitsAsync());
        Assert.False(monitorClient.IsOpen);
    }

    [Fact]
    public async Task FailedMonitorWordReregistrationDoesNotReuseOldDecoderMetadata()
    {
        await using var server = new ScriptedHostLinkServer(command => command switch
        {
            "MWS R5000" => "OK",
            "MWS R5100" => "E1",
            "MWR" => "00002",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        await client.RegisterMonitorWordsAsync([new("R5000")]);

        await Assert.ThrowsAsync<HostLinkError>(
            () => client.RegisterMonitorWordsAsync([new("R5100")]));
        HostLinkProtocolError error = await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.ReadMonitorWordsAsync());

        Assert.Contains("registered", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["MWS R5000", "MWS R5100"], server.ReceivedCommands.ToArray());
        Assert.True(client.IsOpen);
    }

    [Fact]
    public async Task ReopenDoesNotReuseMonitorWordDecoderMetadata()
    {
        await using var server = new ScriptedHostLinkServer(command =>
            command == "MWS R5000" ? "OK" : "E1");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();
        await client.RegisterMonitorWordsAsync([new("R5000")]);
        client.Close();
        await client.OpenAsync();

        HostLinkProtocolError error = await Assert.ThrowsAsync<HostLinkProtocolError>(
            () => client.ReadMonitorWordsAsync());

        Assert.Contains("registered", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["MWS R5000"], server.ReceivedCommands.ToArray());
        Assert.True(client.IsOpen);
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
            "RD R000.U" => "00000",
            "RD R000.D" => "0000000000",
            "RD DM0.U" => "123",
            "RD R001" => "ON",
            "RD R002" => "GARBAGE",
            _ => "E1",
        });
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        Assert.Equal(["00000"], await client.ReadAsync("R0", ".U"));
        Assert.Equal(["0000000000"], await client.ReadAsync("R0", ".D"));
        Assert.Equal(["123"], await client.ReadAsync("DM0", ".U"));
        Assert.Equal(["ON"], await client.ReadAsync("R1"));
        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync("R2"));
        Assert.False(client.IsOpen);
    }

    [Fact]
    public async Task DirectBitFormattedReadRejectsMultipleScalarTokensAndRetiresConnection()
    {
        await using var server = new ScriptedHostLinkServer(command =>
            command == "RD R000.H" ? "0000 0000" : "E1");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync("R0", ".H"));

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
    [InlineData("RD T0.H", "0000,270F,270F", "H")]
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
    public async Task TimerCounterHexReadKeepsRawStatusAndFormatsOnlyCurrentAndPreset()
    {
        await using var server = new ScriptedHostLinkServer(command =>
            command == "RD T0.H" ? "0,270F,270F" : "E1");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        Assert.Equal(["0", "270F", "270F"], await client.ReadAsync("T0", ".H"));
        Assert.Equal("270F", Assert.IsType<string>(await client.ReadTypedAsync("T0", "H")));
    }

    [Theory]
    [InlineData("T0", ".U", "0,1,65535", "0", "1", "65535")]
    [InlineData("C0", ".U", "1,0,1", "1", "0", "1")]
    [InlineData("C0", ".S", "1,+00001,-00002", "1", "+00001", "-00002")]
    [InlineData("T0", ".S", "0,-00001,+00002", "0", "-00001", "+00002")]
    [InlineData("T0", ".H", "0,f,a", "0", "000F", "000A")]
    [InlineData("C0", ".H", "1,10,ff", "1", "0010", "00FF")]
    [InlineData("C0", ".D", "1,1,4294967295", "1", "1", "4294967295")]
    [InlineData("T0", ".D", "0,0,1", "0", "0", "1")]
    [InlineData("T0", ".L", "0,+000000001,-000000002", "0", "+000000001", "-000000002")]
    [InlineData("C0", ".L", "1,-000000001,+000000002", "1", "-000000001", "+000000002")]
    public async Task TimerCounterAllFormatsKeepRawStatusAndFormatOnlyCurrentAndPreset(
        string device,
        string format,
        string response,
        string expectedStatus,
        string expectedCurrent,
        string expectedPreset)
    {
        await using var server = new ScriptedHostLinkServer(_ => response);
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        Assert.Equal(
            [expectedStatus, expectedCurrent, expectedPreset],
            await client.ReadAsync(device, format));
        Assert.True(client.IsOpen);
    }

    [Theory]
    [InlineData("T0", ".U", "0,1")]
    [InlineData("C0", ".H", "1,F,10,20")]
    public async Task TimerCounterMissingOrExtraTokensAreProtocolErrorsAndRetireTransport(
        string device,
        string format,
        string response)
    {
        await using var server = new ScriptedHostLinkServer(_ => response);
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync(device, format));
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData("T0", ".U", "0,INVALID,1")]
    [InlineData("C0", ".S", "0,1,INVALID")]
    [InlineData("T0", ".H", "0,G,0001")]
    [InlineData("C0", ".D", "0,1,INVALID")]
    [InlineData("T0", ".L", "0,INVALID,1")]
    public async Task TimerCounterInvalidCurrentOrPresetIsProtocolErrorAndRetiresTransport(
        string device,
        string format,
        string response)
    {
        await using var server = new ScriptedHostLinkServer(_ => response);
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync(device, format));
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData("T0", ".U", "0,65536,0")]
    [InlineData("C0", ".S", "0,0,-32769")]
    [InlineData("T0", ".H", "0,10000,0")]
    [InlineData("C0", ".D", "0,0,4294967296")]
    [InlineData("T0", ".L", "0,-2147483649,0")]
    public async Task TimerCounterEveryFormatRejectsOverflowAndRetiresTransport(
        string device,
        string format,
        string response)
    {
        await using var server = new ScriptedHostLinkServer(_ => response);
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync(device, format));
        Assert.False(client.IsOpen);
    }

    [Theory]
    [InlineData("T0", "00")]
    [InlineData("C0", "01")]
    [InlineData("T0", "+0")]
    [InlineData("C0", "+1")]
    [InlineData("T0", "ON")]
    [InlineData("C0", "OFF")]
    [InlineData("T0", "2")]
    [InlineData("C0", "-1")]
    public async Task TimerCounterNonExactStatusIsProtocolErrorAndRetiresTransport(
        string device,
        string status)
    {
        await using var server = new ScriptedHostLinkServer(_ => $"{status},000F,0010");
        await using var client = new KvHostLinkClient(
            "127.0.0.1", server.Port, HostLinkTransportMode.Tcp, TestPlcProfile);
        await client.OpenAsync();

        await Assert.ThrowsAsync<HostLinkProtocolError>(() => client.ReadAsync(device, ".H"));
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
