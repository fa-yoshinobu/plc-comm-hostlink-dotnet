// PlcComm.KvHostLink.HighLevelSample
// ===================================
// Demonstrates all high-level KEYENCE KV Host Link APIs:
//   KvHostLinkClientFactory.OpenAndConnectAsync, ReadTypedAsync,
//   WriteTypedAsync, WriteBitInWordAsync, ReadWordsSingleRequestAsync,
//   ReadDWordsSingleRequestAsync, ReadWordsChunkedAsync, ReadDWordsChunkedAsync,
//   ReadNamedAsync, PollAsync, and KvHostLinkAddress.Normalize.
//
// Usage:
//   dotnet run --project samples/PlcComm.KvHostLink.HighLevelSample -- [host] [port]
//
// Default port: 8501  (KV Ethernet module default, configurable in KV Studio)

using PlcComm.KvHostLink;

var host = args.Length > 0 ? args[0] : "192.168.250.100";
var port = args.Length > 1 ? int.Parse(args[1]) : 8501;

// -------------------------------------------------------------------------
// 1. OpenAndConnectAsync  (recommended entry point)
//
// Creates and opens the connected client used by the helper API.
//
// Parameters:
//   host - KEYENCE KV PLC IP address or hostname
//   port - KV Ethernet module port (default 8501; configure in KV Studio
//          under Ethernet settings)
//   ct   - CancellationToken
//
// Use case: simplest way to establish a connection for normal application code.
// -------------------------------------------------------------------------
Console.WriteLine($"Connecting to {host}:{port} ...");
var options = new KvHostLinkConnectionOptions(host, port);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
Console.WriteLine($"[OpenAndConnectAsync] Connected to {host}:{port}");

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
var valU = await client.ReadTypedAsync("DM100", "U");
var valL = await client.ReadTypedAsync("DM200", "L");
var valF = await client.ReadTypedAsync("DM300", "F");
Console.WriteLine($"[ReadTypedAsync] DM100(U)={valU}  DM200(L)={valL}  DM300(F)={valF}");

await client.WriteTypedAsync("DM100", "U", (ushort)99);
await client.WriteTypedAsync("DM200", "L", 123456);
await client.WriteTypedAsync("DM300", "F", 12.5f);
Console.WriteLine("[WriteTypedAsync] Wrote 99->DM100, 123456->DM200, 12.5->DM300");

// -------------------------------------------------------------------------
// 3. ReadWordsSingleRequestAsync
//
// Reads count consecutive word devices starting at device.
// Returns ushort[].
//
// Use case: reading a parameter table in DM0-DM9 in one round-trip.
// -------------------------------------------------------------------------
ushort[] words = await client.ReadWordsSingleRequestAsync("DM0", 10);
Console.WriteLine($"[ReadWordsSingleRequestAsync] DM0-DM9 = [{string.Join(", ", words)}]");

// -------------------------------------------------------------------------
// 4. ReadDWordsSingleRequestAsync / ReadWordsChunkedAsync / ReadDWordsChunkedAsync
//
// Reads count consecutive DWord (32-bit unsigned) values starting at device.
// Each DWord is assembled from two consecutive word registers (lo, hi).
// Returns uint[].
//
// Use case: choosing explicitly between one-request reads and multi-request
//           chunked reads.
// -------------------------------------------------------------------------
uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM0", 4);
Console.WriteLine($"[ReadDWordsSingleRequestAsync] DM0-DM7 as uint32[4] = [{string.Join(", ", dwords)}]");

ushort[] largeWords = await client.ReadWordsChunkedAsync("DM1000", 200, maxWordsPerRequest: 64);
uint[] largeDwords = await client.ReadDWordsChunkedAsync("DM2000", 40, maxDwordsPerRequest: 32);
Console.WriteLine($"[ReadWordsChunkedAsync] DM1000 block words = {largeWords.Length}");
Console.WriteLine($"[ReadDWordsChunkedAsync] DM2000 block dwords = {largeDwords.Length}");

// -------------------------------------------------------------------------
// 5. WriteBitInWordAsync
//
// Sets or clears a single bit inside a word device (read-modify-write).
// bitIndex 0 = LSB, 15 = MSB.
// Bits 10-15 can also be specified as hex (A-F) in address notation.
//
// Use case: toggling an individual machine enable flag in a shared status
//           word without disturbing the other 15 bits.
// -------------------------------------------------------------------------
await client.WriteBitInWordAsync("DM50", bitIndex: 4, value: true);
Console.WriteLine("[WriteBitInWordAsync] Set   bit 4 of DM50");
await client.WriteBitInWordAsync("DM50", bitIndex: 4, value: false);
Console.WriteLine("[WriteBitInWordAsync] Clear bit 4 of DM50");

// -------------------------------------------------------------------------
// 6. ReadNamedAsync
//
// Reads multiple devices by address string with optional type suffix.
// Returns IReadOnlyDictionary<string, object>.
//
// Address notation:
//   "DM100"    unsigned 16-bit (ushort)
//   "DM100:S"  signed 16-bit (short)
//   "DM100:D"  unsigned 32-bit (uint)
//   "DM100:L"  signed 32-bit (int)
//   "DM100.3"  bit 3 inside DM100 (bool); index is hexadecimal
//   "DM100.A"  bit 10 inside DM100 (bool); A = 0x0A = decimal 10
//
// Use case: reading a mixed-type process snapshot (int32 counter, signed
//           error code, bool alarm) in a single dictionary-valued call.
// -------------------------------------------------------------------------
string[] snapshotAddresses = ["DM100", "DM200:L", "DM300:F", "DM50.3", "DM50.A"];
var snapshot = await client.ReadNamedAsync(snapshotAddresses);
foreach (var (addr, value) in snapshot)
    Console.WriteLine($"[ReadNamedAsync] {addr} = {value}");

// -------------------------------------------------------------------------
// 7. PollAsync
//
// Async iterator that yields a snapshot dict every interval.
// Use CancellationToken to stop polling.
//
// Use case: asyncio-style polling loop in a .NET application; feeds a
//           live dashboard or a data historian at a fixed sample rate.
// -------------------------------------------------------------------------
Console.WriteLine("\nPolling 3 snapshots (1 s interval):");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var pollCount = 0;
string[] pollAddresses = ["DM100", "DM200:L", "DM300:F", "DM50.3"];
await foreach (var snap in client.PollAsync(
    pollAddresses,
    TimeSpan.FromSeconds(1),
    cts.Token))
{
    Console.WriteLine(
        $"  [{++pollCount}] DM100={snap["DM100"]}  DM200:L={snap["DM200:L"]}  " +
        $"DM300:F={snap["DM300:F"]}  DM50.3={snap["DM50.3"]}");
    if (pollCount >= 3)
        break;
}

Console.WriteLine("Done.");
