[![CI](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml) [![NuGet](https://img.shields.io/nuget/v/PlcComm.KvHostLink.svg)](https://www.nuget.org/packages/PlcComm.KvHostLink/) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/LICENSE)

# KV Host Link Protocol for .NET

KEYENCE KV series PLC communication library for .NET via the Host Link (Upper Link) protocol.

## Supported PLC models

| Model | Notes |
|---|---|
| `keyence:kv-nano` | KV-NANO family profile with standard device names. |
| `keyence:kv-nano-xym` | KV-NANO family profile with XYM alias names. |
| `keyence:kv-3000-5000` | KV-3000, KV-5000, and KV-5500 family profile with EM, FM, ZF, VM, VB, CTH, CTC, and AT ranges. |
| `keyence:kv-3000-5000-xym` | KV-3000, KV-5000, and KV-5500 family profile with XYM alias names. |
| `keyence:kv-7000` | KV-7000, KV-7300, and KV-7500 family profile with large R, MR, DM, EM, FM, ZF, VM, VB, and AT ranges. |
| `keyence:kv-7000-xym` | KV-7000, KV-7300, and KV-7500 family profile with XYM alias names. |
| `keyence:kv-8000` | KV-8000 family profile with the largest VM range in the embedded catalog. |
| `keyence:kv-8000-xym` | KV-8000 family profile with XYM alias names. |
| `keyence:kv-x500` | KV-X500, KV-X520, KV-X530, KV-X550, and KV-X310 family profile. AT, VM, VB, CTH, and CTC are not available in this profile. |
| `keyence:kv-x500-xym` | KV-X500 family profile with XYM alias names. AT, VM, VB, CTH, and CTC are not available in this profile. |

## Supported device types

| Device | What you use it for |
|---|---|
| `DM` | General data memory words, usually the safest first read target. |
| `EM` | Extended data memory words on models that provide EM ranges. |
| `FM` | File memory words on models that provide FM ranges. |
| `R` | Relay bit devices using KEYENCE two-digit bit notation. |
| `MR` | Internal relay bit devices using two-digit bit notation. |
| `T` | Timer current, status, and preset values. |
| `C` | Counter current, status, and preset values. |
| `X` / `Y` | Input and output aliases in XYM profiles, using decimal-bank plus hex-bit notation. |

See [Supported registers](docsrc/user/SUPPORTED_REGISTERS.md) for the full table.

## Installation

```powershell
dotnet add package PlcComm.KvHostLink
```

## Quick example

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("DM0", "U");
Console.WriteLine($"DM0 = {value}");
```

## Documentation

| Page | Use it for |
|---|---|
| [Getting started](docsrc/user/GETTING_STARTED.md) | Install the package, connect to your PLC, and run your first read/write. |
| [Usage guide](docsrc/user/USAGE_GUIDE.md) | Use typed reads, writes, snapshots, blocks, bit-in-word updates, polling, timers, comments, and expansion buffer access. |
| [Supported registers](docsrc/user/SUPPORTED_REGISTERS.md) | Check supported device families and address forms. |
| [PLC profiles](docsrc/user/PROFILES.md) | Choose the profile that matches your PLC model and device ranges. |
| [Examples](samples/README.md) | Run sample projects that exercise the high-level API. |

## Hardware verified

Physical communication has been verified with `KV-7500`.

## License and registry

Distributed under the MIT License.

NuGet package: https://www.nuget.org/packages/PlcComm.KvHostLink/
