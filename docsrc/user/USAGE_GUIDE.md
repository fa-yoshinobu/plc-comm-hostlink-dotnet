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
| `ReadWordsChunkedAsync` | Read a large 16-bit word block by explicit chunks. |
| `ReadDWordsChunkedAsync` | Read a large 32-bit value block by explicit chunks. |
| `WriteWordsChunkedAsync` | Write a large 16-bit word block by explicit chunks. |
| `WriteDWordsChunkedAsync` | Write a large 32-bit value block by explicit chunks. |
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
    PlcProfile: "keyence:kv-8000",
    Port: 8501,
    Timeout: TimeSpan.FromSeconds(3),
    Transport: HostLinkTransportMode.Tcp,
    AppendLfOnSend: false);

await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
Console.WriteLine($"Connected: {client.IsOpen}");
```

`KvHostLinkConnectionOptions` requires one canonical PLC profile and defaults to TCP, port `8501`, a 3-second effective timeout, and no LF appended after CR.

## Read a single value

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", "keyence:kv-8000", 8501);
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

## Write a single value

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", "keyence:kv-8000", 8501);
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

var options = new KvHostLinkConnectionOptions("192.168.250.100", "keyence:kv-8000", 8501);
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

var options = new KvHostLinkConnectionOptions("192.168.250.100", "keyence:kv-8000", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

ushort[] words = await client.ReadWordsSingleRequestAsync("DM200", 8);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM300", 4);
ushort[] largeWords = await client.ReadWordsChunkedAsync("DM1000", 128, maxWordsPerRequest: 64);
uint[] largeDWords = await client.ReadDWordsChunkedAsync("DM2000", 64, maxDwordsPerRequest: 32);

Console.WriteLine($"Words: {words.Length}, DWords: {dwords.Length}");
Console.WriteLine($"Chunked words: {largeWords.Length}, chunked DWords: {largeDWords.Length}");
```

Single-request methods send one PLC command. Chunked methods split only where you explicitly choose a chunk size.

## Bit in word

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", "keyence:kv-8000", 8501);
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

var options = new KvHostLinkConnectionOptions("192.168.250.100", "keyence:kv-8000", 8501);
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

## Timer/counter helpers

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", "keyence:kv-8000", 8501);
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

Use `string label = await client.ReadCommentsAsync("DM0");` after connecting to read the PLC device comment label for `DM0`.

## Expansion unit buffer

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", "keyence:kv-8000", 8501);
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
