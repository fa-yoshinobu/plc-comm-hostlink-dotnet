# Getting started

## Start here

This page gets you from an empty .NET project to your first KEYENCE KV Host Link read. You will connect to your PLC at `192.168.250.100:8501`, read `DM0`, then write and restore a test register.

## Prerequisites

| Requirement | Value |
|---|---|
| .NET SDK | .NET 8, 9, or 10 SDK for consuming the package; the .NET 10 SDK is required to build or run this repository's samples. |
| PLC network | Your KV PLC must be reachable from your PC. |
| Host Link port | Use port `8501` for TCP or UDP unless your PLC connection node is configured differently. |

## Install

```powershell
dotnet add package PlcComm.KvHostLink
```

## Connect

```csharp
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
```

This opens the ordinary client, which is the recommended surface for normal application code.
It includes arrival-order FIFO admission; no queued wrapper is needed.
The port, TCP/UDP transport, and canonical PLC profile are all explicit. The
factory performs the network connection; constructing options does not.
Host Link endpoints are IPv4-only. An IPv6 literal and a bracketed IPv4 literal
such as `[192.168.250.100]` are rejected before socket creation. Write IPv4
addresses without brackets; a hostname must resolve to IPv4.

## First read (step by step)

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
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

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

const string testAddress = "DM100";
ushort original = (ushort)await client.ReadTypedAsync(testAddress, "U");
ushort testValue = (ushort)Random.Shared.Next(0, ushort.MaxValue + 1);

try
{
    await client.WriteTypedAsync(testAddress, "U", testValue);
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
| The connection fails immediately. | Confirm that the explicitly configured port and TCP/UDP transport match the PLC connection settings. |
| An IPv6 endpoint is rejected. | Use the PLC's IPv4 address or a hostname with an IPv4 result. IPv6 is intentionally unsupported. |
| A bracketed IPv4 endpoint is rejected. | Remove the brackets: use `192.168.250.100`, not `[192.168.250.100]`. |
| A command reports `HostLinkNotConnectedError`. | Call `OpenAsync` after construction or after a timeout, cancellation, close, EOF, or transport failure. |
| A read reports `HostLinkTimeoutError`. | The one transaction deadline expired and invalidated the connection. Check the endpoint and timeout, then explicitly call `OpenAsync` before another command. |
| A write reports `HostLinkOutcomeUnknownError`. | Transmission may have begun, but no definitive result was received. Inspect `Reason`, do not retry automatically, determine PLC state safely, then explicitly reopen if appropriate. |
| Reads fail while you are trying the first example. | Start with `DM` word reads; do not start with timer/counter or expansion buffer access. |
| Timer/counter preset writes return `E1`. | Timer/Counter preset writes (`WS`/`WSS`) are only supported on KV-8000/7000-series. |
