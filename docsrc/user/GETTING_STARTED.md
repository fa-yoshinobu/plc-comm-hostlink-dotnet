# Getting started

## Start here

This page gets you from an empty .NET project to your first KEYENCE KV Host Link read. You will connect to your PLC at `192.168.250.100:8501`, read `DM0`, then write and restore a test register.

## Prerequisites

| Requirement | Value |
|---|---|
| .NET SDK | .NET 9 SDK, matching the package target framework `net9.0`. |
| PLC network | Your KV PLC must be reachable from your PC. |
| Host Link port | Use port `8501` for TCP or UDP unless your PLC connection node is configured differently. |

## Install

```powershell
dotnet add package PlcComm.KvHostLink
```

## Connect

```csharp
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
```

This opens a queued client, which is the recommended surface for normal application code.

## First read (step by step)

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

ushort dm0 = (ushort)await client.ReadTypedAsync("DM0", "U");
Console.WriteLine($"DM0 = {dm0}");
```

Expected output:

```text
DM0 = 123
```

Your number will match the current value stored in `DM0` on your PLC.

## First write

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

const string testAddress = "DM100";
ushort original = (ushort)await client.ReadTypedAsync(testAddress, "U");

try
{
    await client.WriteTypedAsync(testAddress, "U", (ushort)1234);
    ushort readback = (ushort)await client.ReadTypedAsync(testAddress, "U");
    Console.WriteLine($"{testAddress} = {readback}");
}
finally
{
    await client.WriteTypedAsync(testAddress, "U", original);
}
```

Only write to a test address that is safe for your machine and program.

## Confirm success

1. The connection opens without a timeout.
2. The first read prints a value for `DM0`.
3. The write example prints the value written to `DM100`.
4. The `finally` block restores the original test-register value.

## If it does not work

| Symptom | Check |
|---|---|
| The connection fails immediately. | Default port is `8501`, not `1025`; double-check the connection node port. |
| Reads fail while you are trying the first example. | Start with `DM` word reads; do not start with timer/counter or expansion buffer access. |
| Timer/counter preset writes return `E1`. | Timer/Counter preset writes (`WS`/`WSS`) are only supported on KV-8000/7000-series. |

## Next pages

Continue with [Usage guide](USAGE_GUIDE.md), then check [Supported registers](SUPPORTED_REGISTERS.md) for device families and address forms.
