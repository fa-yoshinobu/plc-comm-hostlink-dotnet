[![CI](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.KvHostLink.svg)](https://www.nuget.org/packages/PlcComm.KvHostLink/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/LICENSE)

# KEYENCE KV Host Link for .NET

.NET library for KEYENCE KV Host Link PLC communication.

Connections are IPv4-only. Use an IPv4 literal or a hostname with an IPv4
result. The ordinary `KvHostLinkClient` includes arrival-order FIFO admission,
so one instance can be shared by asynchronous callers without a wrapper.

## PLC Comm Family

This library is part of the plc-comm family. See the [package matrix](https://fa-yoshinobu.github.io/plc-comm-docs-site/package-matrix/) for protocol, language, registry, and install-command mapping.

## Supported PLC profiles

The maintained profile table is in [PLC profiles](https://fa-yoshinobu.github.io/plc-comm-docs-site/hostlink/dotnet/PROFILES/). Choose one exact canonical PLC profile from that table.

## Supported device types

The shared device and range tables are in the [KV Host Link Device Ranges](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/kv/device-ranges/) page. Use that page for supported device families, address syntax, and profile-specific notes.

## Installation

```powershell
dotnet add package PlcComm.KvHostLink
```

## Quick example

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions(
    "192.168.250.100", 8501, HostLinkTransportMode.Tcp, "keyence:kv-8000");
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
var value = await client.ReadTypedAsync("DM0", "U");
Console.WriteLine($"DM0 = {value}");
```

## Documentation

| Page | Use it for |
|---|---|
| [Getting started](https://fa-yoshinobu.github.io/plc-comm-docs-site/hostlink/dotnet/GETTING_STARTED/) | Install the package, connect to your PLC, and run your first read/write. |
| [Usage guide](https://fa-yoshinobu.github.io/plc-comm-docs-site/hostlink/dotnet/USAGE_GUIDE/) | Use the high-level API and common Host Link workflows. |
| [API reference](https://fa-yoshinobu.github.io/plc-comm-docs-site/hostlink/dotnet/API_REFERENCE/) | Browse generated public .NET signatures and XML documentation comments. |
| [PLC profiles](https://fa-yoshinobu.github.io/plc-comm-docs-site/hostlink/dotnet/PROFILES/) | Choose the profile that matches your PLC model and device ranges. |
| [KV Host Link Device Ranges](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/kv/device-ranges/) | Check shared device families, address notation, and range tables. |
| [KV Host Link Troubleshooting & Codes](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/kv/troubleshooting-codes/) | Troubleshoot common port, profile, address, write-permission, and PLC error-code symptoms. |
| [Gotchas](https://fa-yoshinobu.github.io/plc-comm-docs-site/hostlink/dotnet/GOTCHAS/) | Check whether this library has any current library-specific caveats. |
| [Full documentation site](https://fa-yoshinobu.github.io/plc-comm-docs-site/) | Unified docs for all PLC communication libraries. |
| [Performance](https://fa-yoshinobu.github.io/plc-comm-docs-site/performance/) | See measured latency, throughput, and long-run soak results from real PLC hardware. |
| [Choosing a Language](https://fa-yoshinobu.github.io/plc-comm-docs-site/choosing-a-language/) | Compare the .NET, Python, Rust, C++, and Node-RED implementations before you pick one. |
| [Examples](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/samples/README.md) | Run maintained .NET samples: `samples\PlcComm.KvHostLink.HighLevelSample\PlcComm.KvHostLink.HighLevelSample.csproj`, `samples\PlcComm.KvHostLink.BasicReadWriteSample\PlcComm.KvHostLink.BasicReadWriteSample.csproj`, `samples\PlcComm.KvHostLink.NamedPollingSample\PlcComm.KvHostLink.NamedPollingSample.csproj`. |

For a zero-code connectivity check, see [PLC Scope](https://github.com/fa-yoshinobu/plc-scope-dotnet) (Windows).

## License and registry

| Item | Value |
| --- | --- |
| License | [MIT](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/LICENSE) |
| Registry | [NuGet](https://www.nuget.org/packages/PlcComm.KvHostLink/) |
| Package | `PlcComm.KvHostLink` |

## Commercial support

If you plan to embed this library in a paid or commercial product, please consider a separate support agreement or supporting the project as a sponsor.

Contact: <https://fa-labo.com/contact.html>
