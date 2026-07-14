# Usage guide

## Recommended entry points

| Method | Use it for |
|---|---|
| `OpenAndConnectAsync` | Create and open the recommended queued client. |
| `ReadTypedAsync` | Read one typed value. |
| `WriteTypedAsync` | Write one typed value. |
| `ReadNamedAsync` | Read a mixed snapshot by address strings. |
| `PollAsync` | Read repeated snapshots on a fixed interval. |
| `ReadWordsSingleRequestAsync` | Read contiguous 16-bit words in one PLC request. |
| `ReadDWordsSingleRequestAsync` | Read contiguous 32-bit values in one PLC request. |
| `WriteWordsSingleRequestAsync` | Write contiguous 16-bit words in one PLC request. |
| `WriteDWordsSingleRequestAsync` | Write contiguous 32-bit values in one PLC request. |
| `WriteBitInWordAsync` | Set or clear one bit inside a word device. |
| `ReadTimerCounterAsync` | Read timer or counter status, current value, and preset. |
| `ReadTimerAsync` | Read a timer as status, current value, and preset. |
| `ReadCounterAsync` | Read a counter as status, current value, and preset. |
| `ReadCommentsAsync` | Read a PLC device comment label. |
| `ReadExpansionUnitBufferAsync` | Read expansion unit buffer memory. |
| `WriteExpansionUnitBufferAsync` | Write expansion unit buffer memory. |

## Connection

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions(
    Host: "192.168.250.100",
    Port: 8501,
    Transport: HostLinkTransportMode.Tcp,
    PlcProfile: "keyence:kv-8000",
    Timeout: TimeSpan.FromSeconds(3));

await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
Console.WriteLine($"Connected: {client.IsOpen}");
```

`Host`, `Port`, `Transport`, and the canonical PLC profile are required. Only
`Timeout` may be omitted; its default is 3 seconds. Explicit values must be
from 1 through `Int32.MaxValue` milliseconds. Sub-millisecond, zero, negative,
or larger timeouts are rejected. Normal Host Link command frames always end in CR.

`SetTimeAsync` requires an explicit `DateTime` whose year is 2000 through
2099. Years outside that PLC clock range are rejected before communication;
the library never folds another century into a two-digit year.

Read responses are validated against the issued command. Direct-bit responses
accept only `0`, `1`, `OFF`, or `ON`; numeric reads of direct-bit devices require the
corresponding 16- or 32-point response. A malformed response shape invalidates
the session before another request.

## Performance notes

Choose TCP or UDP explicitly for every endpoint. TCP provides stream delivery;
UDP avoids stream state but does not provide retransmission. The TCP transport
disables Nagle buffering for small Host Link command frames.

Reuse one connected client for repeated reads and writes. Prefer
`ReadWordsSingleRequestAsync`, `ReadDWordsSingleRequestAsync`, or
`ReadNamedAsync` over many individual `ReadTypedAsync` calls when one
application snapshot can be read as one request.

## Connection reuse and concurrent requests

Keep one `QueuedKvHostLinkClient` open for repeated reads, writes, and polling.
The factory returns a queued client, so multiple async callers can share that
client without interleaving Host Link frames on the same PLC connection.

Do not call `InnerClient` concurrently. When custom access is needed, use
`ExecuteAsync` so the operation stays inside the same queue. Commands never
open or reconnect implicitly. After `CloseAsync`, timeout, cancellation, EOF,
or transport failure, call `OpenAsync` explicitly before the next command. A
failed command is never retried automatically.

## Read a single value

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

ushort unsignedWord = (ushort)await client.ReadTypedAsync("DM0", "U");
short signedWord = (short)await client.ReadTypedAsync("DM1", "S");
uint unsignedDWord = (uint)await client.ReadTypedAsync("DM2", "D");
int signedDWord = (int)await client.ReadTypedAsync("DM4", "L");
float floatValue = (float)await client.ReadTypedAsync("DM6", "F");

Console.WriteLine($"{unsignedWord}, {signedWord}, {unsignedDWord}, {signedDWord}, {floatValue}");
```

| Suffix | Meaning | Returned .NET type |
|---|---|---|
| `U` | Unsigned 16-bit word | `ushort` |
| `S` | Signed 16-bit word | `short` |
| `D` | Unsigned 32-bit double word | `uint` |
| `L` | Signed 32-bit double word | `int` |
| `F` | IEEE 754 32-bit floating point | `float` |
| `H` | Hexadecimal 16-bit word text | `string` |
| `BIT` | Direct bit device | `bool` |

## Write a single value

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

const string address = "DM100";
ushort original = (ushort)await client.ReadTypedAsync(address, "U");

try
{
    await client.WriteTypedAsync(address, "U", (ushort)42);
    ushort readback = (ushort)await client.ReadTypedAsync(address, "U");
    Console.WriteLine($"{address} readback = {readback}");
}
finally
{
    await client.WriteTypedAsync(address, "U", original);
}
```

This is a matched read/write/readback pattern. Keep it on a test address until you know the register is safe for your machine.

## Named snapshot read

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

string[] addresses = ["DM0:U", "DM1:S", "DM2:D", "DM4:F", "DM10.A", "DM0:COMMENT"];
var snapshot = await client.ReadNamedAsync(addresses);

foreach (var (address, value) in snapshot)
{
    Console.WriteLine($"{address} = {value}");
}
```

Use `ReadNamedAsync` when one application snapshot mixes unsigned words, signed words, double words, floats, PLC comment strings, and bit-in-word values.

## Contiguous block reads

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

ushort[] words = await client.ReadWordsSingleRequestAsync("DM200", 8);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM300", 4);

Console.WriteLine($"Words: {words.Length}, DWords: {dwords.Length}");
```

Both methods send exactly one PLC command. Word requests accept at most 1000
values and native `.D` Dword requests accept at most 500 values. The library
does not split larger operations: application code must make each request,
timing boundary, retry decision, and partial-write consequence explicit.

## Bit in word

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

await client.WriteBitInWordAsync("DM50", bitIndex: 10, value: true);
var snapshot = await client.ReadNamedAsync(["DM50.A"]);

Console.WriteLine($"DM50.A = {snapshot["DM50.A"]}");
```

The `.n` notation uses hexadecimal bit indexes from `0` through `F`; `.A` means bit 10.

## Polling

```csharp
using System;
using System.Threading;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

string[] addresses = ["DM0:U", "DM1:S", "DM4:F"];
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var count = 0;

await foreach (var snapshot in client.PollAsync(addresses, TimeSpan.FromSeconds(1), cts.Token))
{
    Console.WriteLine($"DM0:U={snapshot["DM0:U"]}, DM1:S={snapshot["DM1:S"]}, DM4:F={snapshot["DM4:F"]}");
    if (++count >= 3)
    {
        break;
    }
}
```

`PollAsync` yields a dictionary snapshot on each interval until cancellation or until your loop exits.

## Operational recipes

The samples include two read-only operational recipes for repeatable collection:

- `PlcComm.KvHostLink.MultiPlcMonitorSample` monitors multiple PLC endpoints at
  the same time. Each PLC has its own task, connection, and reconnect loop, so
  one offline PLC does not block healthy PLC reads.
- `PlcComm.KvHostLink.ConfigPollingSample` runs periodic collection from a JSON
  config file and can append long-form CSV rows as
  `timestamp,plc,tag,value`.

Both samples use the same reconnect states as the polling reconnect sample:
`connected`, `lost`, `reconnecting`, and `recovered`, with 1 second initial
backoff, exponential delay, and a 30 second default maximum. YAML config is
available only in the Python sample; the .NET sample uses JSON.

```powershell
dotnet run --project samples/PlcComm.KvHostLink.MultiPlcMonitorSample -- --plc line-a=192.168.250.100,keyence:kv-8000,8501,tcp --plc line-b=192.168.250.101,keyence:kv-8000,8501,tcp --tag dm100=DM100:U
dotnet run --project samples/PlcComm.KvHostLink.ConfigPollingSample -- --config samples/PlcComm.KvHostLink.ConfigPollingSample/config_polling.example.json --dry-run
```

## Timer/counter helpers

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

KvTimerCounterValue timer = await client.ReadTimerAsync("T0");
KvTimerCounterValue counter = await client.ReadCounterAsync("C0");
KvTimerCounterValue generic = await client.ReadTimerCounterAsync("T0");

Console.WriteLine($"T0 status={timer.Status}, current={timer.Current}, preset={timer.Preset}");
Console.WriteLine($"C0 status={counter.Status}, current={counter.Current}, preset={counter.Preset}");
Console.WriteLine($"Generic T0 preset={generic.Preset}");
```

`ReadTimerCounterAsync` returns `Status`, `Current`, and `Preset`. `ReadTimerAsync` accepts timer devices, and `ReadCounterAsync` accepts counter devices.

> **Caution:** Timer/Counter preset writes (`WS`/`WSS`) are only supported on KV-8000/7000-series PLCs. Other models return error `E1`.

## Device comments

Use `string label = await client.ReadCommentsAsync("DM0");` after connecting to
read the PLC device comment label for `DM0`. The result removes only trailing
ASCII space padding; tabs, full-width spaces, and embedded spaces are preserved.

## Expansion unit buffer

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

string[] bufferWords = await client.ReadExpansionUnitBufferAsync(
    unitNo: 0,
    address: 0,
    count: 4,
    dataFormat: ".U");

await client.WriteExpansionUnitBufferAsync(
    unitNo: 0,
    address: 10,
    values: new ushort[] { 1, 2, 3, 4 },
    dataFormat: ".U");

Console.WriteLine($"Read {bufferWords.Length} expansion buffer values.");
```

Expansion unit buffer methods access module buffer memory by unit number, buffer address, count, and data format.
The data format is mandatory and must be `.U`, `.S`, `.D`, `.L`, or `.H`.

## Low-level numeric addresses

Low-level numeric methods require a base device and a separate data format:

```csharp
string[] values = await client.ReadConsecutiveAsync("DM100", 4, ".U");
await client.WriteAsync("DM200", 123u, ".D");
```

Do not pass `DM100.U` or another suffix inside the device argument. Suffix input
is rejected even when it matches the separate format. Direct bit devices are
the only format-free low-level access because the device family fixes the bit
unit. In high-level named syntax, `DM100.D` means bit 13 while `DM100:D` means
an unsigned Dword.

## Address reference table

| Form | Example | Meaning |
|---|---|---|
| `:U` | `DM100:U` | Unsigned 16-bit view. |
| `:S` | `DM100:S` | Signed 16-bit view. |
| `:D` | `DM100:D` | Unsigned 32-bit view. |
| `:L` | `DM100:L` | Signed 32-bit view. |
| `:F` | `DM100:F` | IEEE 754 32-bit float view. |
| `:BIT` | `R200:BIT` | Direct bit device view. |
| `:COMMENT` | `DM100:COMMENT` | PLC device comment text. |
| `.n` | `DM100.A` | One bit inside a word; `n` is hexadecimal `0` to `F`. |

For `ReadNamedAsync` and `PollAsync`, include the intended type. Use `DM100:U` instead of plain `DM100` for an unsigned word.

## Runnable samples

The `samples/` directory contains ready-to-run projects for the most common high-level workflows.

| Project | What it demonstrates |
|---|---|
| `samples\PlcComm.KvHostLink.HighLevelSample\PlcComm.KvHostLink.HighLevelSample.csproj` | Full high-level API: typed reads/writes, block reads, bit-in-word, named snapshots, and polling. |
| `samples\PlcComm.KvHostLink.BasicReadWriteSample\PlcComm.KvHostLink.BasicReadWriteSample.csproj` | Basic typed read/write for unsigned, signed, double-word, and float values. |
| `samples\PlcComm.KvHostLink.NamedPollingSample\PlcComm.KvHostLink.NamedPollingSample.csproj` | Named polling with `PollAsync`. |

## Traffic statistics

Read `client.TrafficStats` (also available on the queued client) for cumulative `RequestCount`, `TxBytes`, and `RxBytes`.
For TCP, a received line counts its body plus the first CR/LF terminator; extra CR/LF separators
are consumed but not counted. For UDP, the complete response datagram is counted.
