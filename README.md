[![CI](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.KvHostLink.svg)](https://www.nuget.org/packages/PlcComm.KvHostLink/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

# KEYENCE KV Host Link for .NET

.NET library for KEYENCE KV Host Link PLC communication.

## Supported PLC profiles

The maintained profile table is in [PLC profiles](docsrc/user/PROFILES.md). Choose one exact canonical PLC profile from that table.

## Supported device types

The maintained device and range tables are in [Supported registers](docsrc/user/SUPPORTED_REGISTERS.md). Use that page for supported device families, address syntax, and profile-specific notes.

## Installation

```powershell
dotnet add package PlcComm.KvHostLink
```

## Quick example

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", "keyence:kv-8000", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("DM0", "U");
Console.WriteLine($"DM0 = {value}");
```

## Documentation

| Page | Use it for |
|---|---|
| [Getting started](docsrc/user/GETTING_STARTED.md) | Install the package, connect to your PLC, and run your first read/write. |
| [Usage guide](docsrc/user/USAGE_GUIDE.md) | Use the high-level API and common Host Link workflows. |
| [Supported registers](docsrc/user/SUPPORTED_REGISTERS.md) | Check supported device families and address forms. |
| [PLC profiles](docsrc/user/PROFILES.md) | Choose the profile that matches your PLC model and device ranges. |
| [Gotchas](docsrc/user/GOTCHAS.md) | Troubleshoot common address, profile, port, and timer/counter issues. |
| [Full documentation site](https://fa-yoshinobu.github.io/plc-comm-docs-site/) | Unified docs for all PLC communication libraries. |
| [Examples](samples/README.md) | Run maintained .NET samples: `samples\PlcComm.KvHostLink.HighLevelSample\PlcComm.KvHostLink.HighLevelSample.csproj`, `samples\PlcComm.KvHostLink.BasicReadWriteSample\PlcComm.KvHostLink.BasicReadWriteSample.csproj`, `samples\PlcComm.KvHostLink.NamedPollingSample\PlcComm.KvHostLink.NamedPollingSample.csproj`. |

## License and registry

| Item | Value |
| --- | --- |
| License | [MIT](LICENSE) |
| Registry | [NuGet](https://www.nuget.org/packages/PlcComm.KvHostLink/) |
| Package | `PlcComm.KvHostLink` |

## Commercial support

If you plan to embed this library in a paid or commercial product, please consider a separate support agreement or supporting the project as a sponsor.

Contact: <https://fa-labo.com/contact.html>
