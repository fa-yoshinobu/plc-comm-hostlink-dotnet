# User Guide: Host Link Communication .NET

This guide covers the recommended high-level API only.

Use these entry points in normal application code:

- `KvHostLinkClientFactory.OpenAndConnectAsync`
- `KvHostLinkConnectionOptions`
- `ReadTypedAsync`
- `WriteTypedAsync`
- `WriteBitInWordAsync`
- `ReadNamedAsync`
- `PollAsync`
- `ReadWordsSingleRequestAsync`
- `ReadDWordsSingleRequestAsync`
- `ReadWordsChunkedAsync`
- `ReadDWordsChunkedAsync`
- `KvHostLinkAddress.Normalize`

Raw token-oriented reads, writes, and protocol details are intentionally left to
the maintainer documentation.

## Installation

Add a project reference to:

```text
src/PlcComm.KvHostLink/PlcComm.KvHostLink.csproj
```

or reference the compiled assembly from a .NET 9 application.

## Connect Once

```csharp
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
```

The factory returns an open `QueuedKvHostLinkClient` ready for the helper
extension methods below. The shortcut `OpenAndConnectAsync(host, port)` remains
available, but the factory plus options object is the recommended public entry
point.

## Typed Read and Write

Supported dtype codes:

| dtype | Meaning | Width |
|---|---|---|
| `U` | unsigned 16-bit | 1 word |
| `S` | signed 16-bit | 1 word |
| `D` | unsigned 32-bit | 2 words |
| `L` | signed 32-bit | 2 words |
| `F` | IEEE 754 float32 | 2 words |

```csharp
ushort u = (ushort)await client.ReadTypedAsync("DM0", "U");
short s = (short)await client.ReadTypedAsync("DM1", "S");
uint d = (uint)await client.ReadTypedAsync("DM2", "D");
float f = (float)await client.ReadTypedAsync("DM4", "F");

await client.WriteTypedAsync("DM10", "U", u);
await client.WriteTypedAsync("DM11", "S", s);
await client.WriteTypedAsync("DM12", "D", d);
await client.WriteTypedAsync("DM14", "F", f);
```

`F` is implemented in the helper layer by converting two `.U` words as
float32.

## Contiguous Blocks

Use explicit single-request helpers when one PLC request is required.

```csharp
ushort[] words = await client.ReadWordsSingleRequestAsync("DM100", 8);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM200", 4);
```

Use explicit chunked helpers only when splitting is acceptable:

```csharp
ushort[] largeWords = await client.ReadWordsChunkedAsync("DM1000", 200, maxWordsPerRequest: 64);
uint[] largeDwords = await client.ReadDWordsChunkedAsync("DM2000", 40, maxDwordsPerRequest: 32);
```

`*SingleRequestAsync` does not silently split one logical request. If the PLC
or protocol cannot satisfy that request shape, the helper returns an error.

## Bit in Word

Use `WriteBitInWordAsync` for bit updates inside word devices such as `DM`,
`EM`, `FM`, `W`, or `Z`.

```csharp
await client.WriteBitInWordAsync("DM500", bitIndex: 0, value: true);
await client.WriteBitInWordAsync("DM500", bitIndex: 3, value: false);
```

This helper performs a read-modify-write so that the other 15 bits remain
unchanged.

## Mixed Snapshots with `ReadNamedAsync`

Supported address notation:

| Format | Meaning |
|---|---|
| `"DM100"` | unsigned 16-bit |
| `"DM100:S"` | signed 16-bit |
| `"DM100:D"` | unsigned 32-bit |
| `"DM100:L"` | signed 32-bit |
| `"DM100:F"` | float32 |
| `"DM100.3"` | bit 3 inside the word |
| `"DM100.A"` | bit 10 inside the word |

```csharp
var snapshot = await client.ReadNamedAsync(
    new[] { "DM100", "DM101:S", "DM102:D", "DM104:F", "DM200.0", "DM200.A" });
```

Bit indices use hexadecimal notation from `0` to `F`.

## Address Normalization

Use `KvHostLinkAddress.Normalize` when you want one stable string form:

```csharp
string canonical = KvHostLinkAddress.Normalize("dm100.a");
Console.WriteLine(canonical); // DM100.A
```

## Polling

`PollAsync` repeatedly yields the same kind of snapshot dictionary.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

await foreach (var snapshot in client.PollAsync(
    new[] { "DM100", "DM101:L", "DM200.3" },
    TimeSpan.FromSeconds(1),
    cts.Token))
{
    Console.WriteLine(snapshot["DM100"]);
}
```

## Error Handling

| Exception | Meaning |
|---|---|
| `HostLinkError` | PLC returned an error code |
| `HostLinkProtocolError` | Local validation or parsing failure |
| `HostLinkConnectionError` | Connect, disconnect, socket, or timeout failure |

```csharp
try
{
    var value = await client.ReadTypedAsync("DM100", "U");
}
catch (HostLinkProtocolError ex)
{
    Console.WriteLine($"Protocol error: {ex.Message}");
}
catch (HostLinkConnectionError ex)
{
    Console.WriteLine($"Connection error: {ex.Message}");
}
catch (HostLinkError ex)
{
    Console.WriteLine($"PLC error: {ex.Message}");
}
```

## Recommended Samples

| API / workflow | Sample | Purpose |
|---|---|---|
| `KvHostLinkClientFactory.OpenAndConnectAsync`, `KvHostLinkConnectionOptions`, `KvHostLinkAddress.Normalize`, `ReadTypedAsync`, `WriteTypedAsync`, `ReadWordsSingleRequestAsync`, `ReadDWordsSingleRequestAsync`, `ReadWordsChunkedAsync`, `ReadDWordsChunkedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, `PollAsync` | `samples/PlcComm.KvHostLink.HighLevelSample/PlcComm.KvHostLink.HighLevelSample.csproj` | Full helper-layer walkthrough |
| `KvHostLinkClientFactory.OpenAndConnectAsync`, `KvHostLinkConnectionOptions`, `ReadTypedAsync`, `WriteTypedAsync`, `ReadWordsSingleRequestAsync`, `ReadDWordsSingleRequestAsync` | `samples/PlcComm.KvHostLink.BasicReadWriteSample/PlcComm.KvHostLink.BasicReadWriteSample.csproj` | Focused typed and contiguous single-request example |
| `ReadNamedAsync`, `WriteBitInWordAsync`, `PollAsync` | `samples/PlcComm.KvHostLink.NamedPollingSample/PlcComm.KvHostLink.NamedPollingSample.csproj` | Mixed snapshot and polling example |
