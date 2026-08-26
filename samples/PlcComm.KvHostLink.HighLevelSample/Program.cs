// PlcComm.KvHostLink.HighLevelSample
// ===================================
// Demonstrates all high-level KEYENCE KV Host Link APIs:
//   KvHostLinkClientFactory.OpenAndConnectAsync, ReadTypedAsync,
//   WriteTypedAsync, ReadWordsSingleRequestAsync,
//   ReadWordsAsync, ReadDWordsAsync,
//   ReadNamedAsync, PollAsync, and KvHostLinkAddress.Normalize.
//
// Usage:
//   dotnet run --project samples/PlcComm.KvHostLink.HighLevelSample -- <host> <port> <transport> <plc-profile> [--allow-writes]

using PlcComm.KvHostLink;

if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/PlcComm.KvHostLink.HighLevelSample -- <host> <port> <transport> <plc-profile> [--allow-writes]");
    Console.Error.WriteLine("Example: dotnet run --project samples/PlcComm.KvHostLink.HighLevelSample -- 192.168.250.100 8501 tcp keyence:kv-8000");
    return;
}

var host = args[0];
var port = int.Parse(args[1]);
var transport = args[2].ToLowerInvariant() switch
{
    "tcp" => HostLinkTransportMode.Tcp,
    "udp" => HostLinkTransportMode.Udp,
    _ => throw new ArgumentException("transport must be tcp or udp."),
};
var plcProfile = args[3];
var allowWrites = args.Skip(4).Contains("--allow-writes", StringComparer.Ordinal);

// -------------------------------------------------------------------------
// 1. OpenAndConnectAsync  (recommended entry point)
//
// Creates and opens the connected client used by the helper API.
//
// Parameters:
//   host - KEYENCE KV PLC IP address or hostname
//   port - explicitly configured KV Ethernet module port
//   transport - tcp or udp, matching the PLC connection settings
//   ct   - CancellationToken
//
// Use case: simplest way to establish a connection for normal application code.
// -------------------------------------------------------------------------
Console.WriteLine($"Connecting to {host}:{port} ({plcProfile}) ...");
var options = new KvHostLinkConnectionOptions(host, port, transport, plcProfile);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
Console.WriteLine($"[OpenAndConnectAsync] Connected to {host}:{port} ({client.PlcProfile})");

// Normalize an address before storing or displaying it; see docsrc/user/GOTCHAS.md for address-format pitfalls.
string normalized = KvHostLinkAddress.Normalize("dm50.a");
Console.WriteLine($"[Normalize] dm50.a -> {normalized}");

// -------------------------------------------------------------------------
// Timeout is still configurable on the connected client.
// -------------------------------------------------------------------------
client.Timeout = TimeSpan.FromSeconds(5);

// -------------------------------------------------------------------------
// 2. ReadTypedAsync / WriteTypedAsync
//
// Read or write a single device with automatic type conversion.
// device - device address string, e.g. "DM100"
// dtype  - "U" unsigned-16, "S" signed-16,
//          "D" unsigned-32, "L" signed-32, "F" float32
//
// Use case: reading a signed 32-bit production counter from DM200-DM201, or writing
//           a signed 16-bit error reset code to DM100.
// -------------------------------------------------------------------------
// Read typed values from individual devices.
var valU = await client.ReadTypedAsync("DM100", "U");
var valL = await client.ReadTypedAsync("DM200", "L");
var valF = await client.ReadTypedAsync("DM300", "F");
Console.WriteLine($"[ReadTypedAsync] DM100(U)={valU}  DM200(L)={valL}  DM300(F)={valF}");

var originalDm100 = (ushort)valU;
var originalDm200 = (int)valL;
var originalDm300 = (float)valF;

try
{
    if (allowWrites)
    {
        // The opt-in write path uses random test values and restores the saved values in finally.
        ushort testDm100 = (ushort)Random.Shared.Next(0, ushort.MaxValue + 1);
        int testDm200 = (int)Random.Shared.NextInt64(int.MinValue, (long)int.MaxValue + 1);
        float testDm300 = (Random.Shared.NextSingle() * 20_000f) - 10_000f;
        await client.WriteTypedAsync("DM100", "U", testDm100);
        await client.WriteTypedAsync("DM200", "L", testDm200);
        await client.WriteTypedAsync("DM300", "F", testDm300);
        Console.WriteLine(
            $"[WriteTypedAsync] Wrote random test values {testDm100}->DM100, {testDm200}->DM200, {testDm300}->DM300");
    }
    else
    {
        Console.WriteLine("[Read-only] Write examples skipped. Add --allow-writes only for controlled test addresses.");
    }

    // -------------------------------------------------------------------------
    // 3. ReadWordsSingleRequestAsync
    //
    // Reads count consecutive word devices starting at device.
    // Returns ushort[].
    //
    // Use case: reading a parameter table in DM0-DM9 in one round-trip.
    // -------------------------------------------------------------------------
    // Read consecutive 16-bit words in one PLC request.
    ushort[] words = await client.ReadWordsSingleRequestAsync("DM0", 10);
    Console.WriteLine($"[ReadWordsSingleRequestAsync] DM0-DM9 = [{string.Join(", ", words)}]");

    // -------------------------------------------------------------------------
    // 4. ReadWordsAsync / ReadDWordsAsync
    //
    // Reads count consecutive DWord (32-bit unsigned) values starting at device.
    // DWord reads use the native .D Host Link request format. Both helpers send
    // exactly one request and reject counts above the protocol limit.
    // -------------------------------------------------------------------------
    // Read consecutive 32-bit values in one PLC request.
    uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM0", 4);
    Console.WriteLine($"[ReadDWordsSingleRequestAsync] DM0-DM7 as uint32[4] = [{string.Join(", ", dwords)}]");

    ushort[] largeWords = await client.ReadWordsSingleRequestAsync("DM1000", 200);
    uint[] largeDwords = await client.ReadDWordsAsync("DM2000", 40);
    Console.WriteLine($"[ReadWordsAsync] DM1000 block words = {largeWords.Length}");
    Console.WriteLine($"[ReadDWordsAsync] DM2000 block dwords = {largeDwords.Length}");

    // -------------------------------------------------------------------------
    // 6. ReadNamedAsync
    //
    // Reads multiple devices by address string with explicit type suffix.
    // Returns IReadOnlyDictionary<string, object>.
    //
    // Address notation:
    //   "DM100:U"  unsigned 16-bit (ushort)
    //   "DM100:S"  signed 16-bit (short)
    //   "DM100:D"  unsigned 32-bit (uint)
    //   "DM100:L"  signed 32-bit (int)
    //   "DM100.3"  bit 3 inside DM100 (bool); index is hexadecimal
    //   "DM100.A"  bit 10 inside DM100 (bool); A = 0x0A = decimal 10
    //
    // Use case: reading a mixed-type, explicitly non-atomic aggregate (int32
    //           counter, signed error code, bool alarm) in one public call.
    // -------------------------------------------------------------------------
    string[] aggregateAddresses = ["DM100:U", "DM200:L", "DM300:F", "DM50.3", "DM50.A"];
    // Read a named mixed-type aggregate. Internal requests keep input order.
    var aggregate = await client.ReadNamedAsync(aggregateAddresses);
    foreach (var (addr, value) in aggregate)
        Console.WriteLine($"[ReadNamedAsync] {addr} = {value}");

    // -------------------------------------------------------------------------
    // 7. PollAsync
    //
    // Async iterator that yields a non-atomic aggregate dict every interval.
    // Use CancellationToken to stop polling.
    //
    // Use case: asyncio-style polling loop in a .NET application; feeds a
    //           live dashboard or a data historian at a fixed sample rate.
    // -------------------------------------------------------------------------
    Console.WriteLine("\nPolling 3 named read results (1 s interval):");
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var pollCount = 0;
    string[] pollAddresses = ["DM100:U", "DM200:L", "DM300:F", "DM50.3"];
    // Poll a repeated named aggregate until this sample has printed three rows.
    await foreach (var readResult in client.PollAsync(
        pollAddresses,
        TimeSpan.FromSeconds(1),
        cts.Token))
    {
        Console.WriteLine(
            $"  [{++pollCount}] DM100:U={readResult["DM100:U"]}  DM200:L={readResult["DM200:L"]}  " +
            $"DM300:F={readResult["DM300:F"]}  DM50.3={readResult["DM50.3"]}");
        if (pollCount >= 3)
            break;
    }

    Console.WriteLine("Done.");
}
finally
{
    if (allowWrites)
    {
        await client.WriteTypedAsync("DM100", "U", originalDm100);
        await client.WriteTypedAsync("DM200", "L", originalDm200);
        await client.WriteTypedAsync("DM300", "F", originalDm300);
        Console.WriteLine("[Restore] Restored DM100/DM200/DM300");
    }
}
