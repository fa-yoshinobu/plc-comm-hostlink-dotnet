# Usage guide

## Recommended entry points

| Method | Use it for |
|---|---|
| `OpenAndConnectAsync` | Create and open the ordinary FIFO client. |
| `ReadTypedAsync` | Read one typed value. |
| `WriteTypedAsync` | Write one typed value. |
| `ReadNamedAsync` | Read a non-atomic mixed aggregate by address strings. |
| `PollAsync` | Read repeated non-atomic aggregates on a fixed interval. |
| `ReadWordsSingleRequestAsync` | Read contiguous 16-bit words in one PLC request. |
| `ReadDWordsSingleRequestAsync` | Read contiguous 32-bit values in one PLC request. |
| `WriteWordsSingleRequestAsync` | Write contiguous 16-bit words in one PLC request. |
| `WriteDWordsSingleRequestAsync` | Write contiguous 32-bit values in one PLC request. |
| `ReadTimerCounterAsync` | Read timer or counter status, current value, and preset. |
| `ReadTimerAsync` | Read a timer as status, current value, and preset. |
| `ReadCounterAsync` | Read a counter as status, current value, and preset. |
| `ReadCommentsAsync` | Decode a PLC device comment with an explicit UTF-8 or CP932 selection. |
| `ReadCommentBytesAsync` | Read the exact undecoded PLC device-comment payload bytes. |
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
Connections are IPv4-only. IPv6 literals and bracketed IPv4 literals such as
`[192.168.250.100]` are rejected before socket creation. Write IPv4 addresses
without brackets; hostnames are resolved only to IPv4 and never fall back to IPv6.

The maintainer `SendRawAsync` API accepts at most 65,506 ASCII bytes in the
command body. The terminating CR makes the maximum complete request frame
65,507 bytes. Larger input is rejected before connection-state checks or I/O;
command-specific limits that are smaller still apply.

`SetTimeAsync` requires an explicit `DateTime` whose year is 2000 through
2099. Years outside that PLC clock range are rejected before communication;
the library never folds another century into a two-digit year.

Read responses are validated against the issued command. Direct-bit responses
accept only `0`, `1`, `OFF`, or `ON`; numeric reads of direct-bit devices require the
corresponding 16- or 32-point response. A malformed response shape invalidates
the session before another request.

The configured timeout is one absolute transaction deadline from the first
send attempt through the complete response. FIFO queue waiting does not consume
that deadline. A timed-out read throws `HostLinkTimeoutError`. If a write may
already have reached the PLC, timeout, caller cancellation, close, transport
failure, or an invalid response throws `HostLinkOutcomeUnknownError`; inspect
its `Reason` and do not retry automatically. `HostLinkClosedError`,
`HostLinkNotConnectedError`, and caller `OperationCanceledException` remain
distinct when the outcome is known. `CloseAsync` and `DisposeAsync` promptly
interrupt active I/O, reject queued work from the retired generation, and do
not reconnect or retry it.

## Performance notes

Choose TCP or UDP explicitly for every endpoint. TCP provides stream delivery;
UDP avoids stream state but does not provide retransmission. The TCP transport
disables Nagle buffering for small Host Link command frames.

One TCP request owns one non-empty response line. CR/LF-only separators are
ignored, but an additional non-empty line received before the next send is a
protocol error and retires the transport. A UDP open is a logical session:
it resolves the IPv4 endpoint once and creates one connected socket. Fully
valid exchanges reuse that socket. Timeout, cancellation, I/O, malformed
response, protocol, an extra response, or a queued unowned datagram detected
before send discards the socket but retains the resolved logical endpoint. The next request creates
one replacement socket without DNS resolution and does not retry the failed
request. Because Host Link UDP has no transaction ID, a duplicate datagram that
arrives in the narrow interval after the pre-send check and before the new send
cannot be distinguished from that request's response; choose TCP when strict
response association is required.

Reuse one connected client for repeated reads and writes. Prefer
`ReadWordsSingleRequestAsync`, `ReadDWordsSingleRequestAsync`, or
`ReadNamedAsync` over many individual `ReadTypedAsync` calls when its explicitly
non-atomic aggregate semantics are acceptable.

## Connection reuse and concurrent requests

Keep one `KvHostLinkClient` open for repeated reads, writes, and polling. Its
built-in FIFO admits public operations in arrival order and permits only one
active wire transaction. Cancelling a waiting operation sends nothing; its
transaction timeout starts only when it becomes active, and the open transport
generation remains usable. Recursive use of the same client from a callback is
rejected with `HostLinkReentrancyError`. Separate client instances remain
independent. Commands never open a closed logical session implicitly. TCP
timeout, active-operation cancellation, EOF, protocol failure, or transport
failure retires the connection; call `OpenAsync` explicitly before a new
command when it is safe to do so. UDP exchange anomalies retain `IsOpen` and
the resolved endpoint while discarding the affected socket; the next command
creates a replacement socket. `CloseAsync` closes either transport completely.
A failed command is never retried automatically.

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
Float32 (`F`) reads and writes are available only for the canonical ordinary
one-word families `DM`, `EM`, `FM`, `ZF`, `W`, `TM`, `CM`, `VM`, `D`,
`E`, and `F`, where the value uses two consecutive `.U` words. The native
32-bit `Z` family is not a two-word Float32 route. Direct-bit families and
special-response families such as `R`, `T`, `C`, and `AT` are
rejected before FIFO admission and transport in parsed, normalized, formatted,
typed, named, and polling addresses. Float32 write input must also be finite and
within the binary32 range; NaN, infinities, and finite values that would
overflow to infinity are rejected before transport.

Semantic `H` reads return exactly four uppercase hexadecimal digits, such as
`000F`, through low-level, typed, named, monitor, and polling APIs. Raw response
body APIs preserve the PLC bytes, and hexadecimal writes keep the minimal wire
representation accepted by the PLC.

## Named aggregate read

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

string[] addresses = ["DM0:U", "DM1:S", "DM2:D", "DM4:F", "DM10.A", "DM0:COMMENT"];
var readResult = await client.ReadNamedAsync(addresses, HostLinkCommentEncoding.Utf8);

foreach (var (address, value) in readResult)
{
    Console.WriteLine($"{address} = {value}");
}
```

Use `ReadNamedAsync` when one application aggregate mixes unsigned words, signed words, double words, floats, PLC comment strings, and bit-in-word values.

`ReadNamedAsync` is an explicitly aggregate read. It validates and snapshots
the complete plan before sending. Wire-compatible device families are handled
in first-appearance order; addresses inside each family are sorted and
contiguous spans are merged up to the protocol request limit. A comment,
native-32-bit device, direct-bit word view, or other non-batchable entry keeps
its native single request without disabling batching elsewhere. Result keys
and values remain in declared input order. All sends and response decoding keep
one FIFO turn, stop at the first failure, and expose no partial dictionary; pure
dictionary materialization occurs after that turn. A DWord or Float value is
never split across wire requests. The result is not a simultaneous PLC snapshot
because internal requests may observe different scan times. For coherent data,
use a single-request read or a PLC-side snapshot/handshake design.

Named keys must be semantically unique by device family, numeric address,
dtype, bit index, and scalar count. Case, leading zeros, or an explicit default
dtype do not make a second key distinct. Different dtype views of the same word,
different bit indices, and overlapping multiword spans are valid. Result keys
preserve the original input strings.

An aggregate containing `:COMMENT` must use the overload that supplies a
`HostLinkCommentEncoding`. The overload without that parameter rejects the
complete aggregate before sending anything. Aggregates without comments do not
need an encoding selection and must use the overload without one; supplying an
unused comment encoding is an argument error before transport.

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

## Bit values and bit-in-word reads

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

var snapshot = await client.ReadNamedAsync(["DM50.A"]);

Console.WriteLine($"DM50.A = {snapshot["DM50.A"]}");
```

The `.n` notation uses hexadecimal bit indexes from `0` through `F`; `.A` means
bit 10. Individual direct-bit writes accept only `bool`, including consecutive
bit collections. Numeric `0`/`1` compatibility inputs are rejected before
transport. Explicit low-level `.U`/`.D` operations on a direct-bit bank remain
packed multi-bit representations; they are not individual bit-value inputs.
The former read-modify-write bit-in-word helper was removed because
one state-changing public call required two wire requests and could not provide
a safe all-or-error result. Write the complete word explicitly only when your
application owns that word and has chosen the concurrency semantics.

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

await foreach (var readResult in client.PollAsync(addresses, TimeSpan.FromSeconds(1), cts.Token))
{
    Console.WriteLine($"DM0:U={readResult["DM0:U"]}, DM1:S={readResult["DM1:S"]}, DM4:F={readResult["DM4:F"]}");
    if (++count >= 3)
    {
        break;
    }
}
```

`PollAsync` yields a non-atomic aggregate dictionary on each interval until
cancellation or until your loop exits. It snapshots, validates, and compiles the
fixed plan once when polling starts. Every cycle reuses that plan and one FIFO
turn with the same input-order result, no-interleaving, and no-partial-result
contract as `ReadNamedAsync`. The interval is a delay after a completed cycle
and runs outside the FIFO turn, so other client operations may proceed during
the delay. Cycles never overlap and missed time is not caught up. The interval
must be greater than zero and no more than `Int32.MaxValue` milliseconds;
invalid intervals are rejected before communication.
If the address list contains `:COMMENT`, use the overload that adds an explicit
`HostLinkCommentEncoding` after the interval. If it contains no comment, use the
ordinary overload; an unused comment encoding is rejected before the first send.

## Operational recipes

The samples include three read-only operational recipes for repeatable collection:

- `PlcComm.KvHostLink.PollingReconnectSample` polls one PLC and demonstrates
  bounded reconnect backoff after transport loss.

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

`ReadTimerCounterAsync` returns `Status`, `Current`, and `Preset`. The response
status must be exactly `0` or `1`; any other numeric value is treated as an
invalid response and retires the connection. `ReadTimerAsync` accepts timer
devices, and `ReadCounterAsync` accepts counter devices.

> **Caution:** Timer/Counter preset writes (`WS`/`WSS`) are only supported on KV-8000/7000-series PLCs. Other models return error `E1`.

## Device comments

An `RDC` response does not carry an encoding identifier. Select the encoding
explicitly when requesting text:

```csharp
string utf8Label = await client.ReadCommentsAsync(
    "DM0",
    HostLinkCommentEncoding.Utf8);

string cp932Label = await client.ReadCommentsAsync(
    "DM1",
    HostLinkCommentEncoding.Cp932);

byte[] exactPayload = await client.ReadCommentBytesAsync("DM2");
```

`Utf8` means strict UTF-8. `Cp932` means strict Windows code page 932 /
Windows-31J and is the compatibility selection for KEYENCE material that calls
the encoding "Shift_JIS". CP932 accepts its mapped Windows extension pairs but
rejects forbidden singleton bytes, incomplete sequences, and unassigned pairs.
The library does not guess, fall back, select from the PLC profile, or replace
malformed bytes. Invalid text fails with `HostLinkProtocolError` and retires the
connection.

The text API removes only trailing ASCII `0x20` padding before decoding; tabs,
full-width spaces, and embedded spaces are preserved. The raw API returns the
exact response body without the Host Link CR/LF frame terminator, including any
trailing ASCII padding.

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

Console.WriteLine($"Read {bufferWords.Length} expansion buffer values.");
```

Expansion unit buffer methods access module buffer memory by unit number, buffer address, count, and data format.
The data format is mandatory and must be `.U`, `.S`, `.D`, `.L`, or `.H`.
The general example is intentionally read-only. Use
`WriteExpansionUnitBufferAsync` only with a module buffer prepared for
controlled testing. Save the original values first and restore them afterward.
After an outcome-unknown failure, reopen and reconcile the actual PLC state
before deciding whether restoration or any retry is safe.

## Low-level numeric addresses

Low-level numeric methods require a base device and a separate data format:

```csharp
string[] values = await client.ReadConsecutiveAsync("DM100", 4, ".U");
```

This example is also intentionally read-only. Use `WriteAsync` only with a
controlled test address and the same save, restore, and outcome-reconciliation
rules described above.

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
| `:COMMENT` | `DM100:COMMENT` | PLC device comment text; the aggregate call must select UTF-8 or CP932 explicitly. |
| `.n` | `DM100.A` | One bit inside a word; `n` is hexadecimal `0` to `F`. |

For `ReadNamedAsync` and `PollAsync`, include the intended type. Use `DM100:U`
instead of plain `DM100` for an unsigned word. When any item is `:COMMENT`, use
the overload with an explicit `HostLinkCommentEncoding`.

## Runnable samples

The `samples/` directory contains ready-to-run projects for the most common high-level workflows.

| Project | What it demonstrates |
|---|---|
| `samples\PlcComm.KvHostLink.HighLevelSample\PlcComm.KvHostLink.HighLevelSample.csproj` | Full high-level API with read-only defaults and opt-in write/restore demonstrations. |
| `samples\PlcComm.KvHostLink.BasicReadWriteSample\PlcComm.KvHostLink.BasicReadWriteSample.csproj` | Typed and block reads plus an opt-in random write/readback/restore demonstration. |
| `samples\PlcComm.KvHostLink.NamedPollingSample\PlcComm.KvHostLink.NamedPollingSample.csproj` | Named polling plus opt-in bit-in-word write/restore. |

All six runnable samples are read-only by default. The three write
demonstrations require `--allow-writes` and must be used only with controlled
test addresses; see `samples/README.md` for the exact commands.

## Traffic statistics

Read `client.TrafficStats` for cumulative `RequestCount`, `TxBytes`, and `RxBytes`.
For TCP, a received line counts its body plus the first CR/LF terminator; extra CR/LF separators
are consumed but not counted. For UDP, the complete response datagram is counted.
