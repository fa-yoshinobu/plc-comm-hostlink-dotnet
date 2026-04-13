[![CI](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.KvHostLink.svg)](https://www.nuget.org/packages/PlcComm.KvHostLink/)
[![Documentation](https://img.shields.io/badge/docs-GitHub_Pages-blue.svg)](https://fa-yoshinobu.github.io/plc-comm-hostlink-dotnet/)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/LICENSE)
[![Static Analysis: dotnet format](https://img.shields.io/badge/Lint-dotnet%20format-blue.svg)](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)

# KV Host Link Protocol for .NET

![Illustration](https://raw.githubusercontent.com/fa-yoshinobu/plc-comm-hostlink-dotnet/main/docsrc/assets/kv.png)

Modern .NET library for KEYENCE KV series PLCs using the Host Link (Upper Link) protocol.

This README intentionally covers the recommended high-level API only:

- `KvHostLinkClientFactory.OpenAndConnectAsync`
- `KvHostLinkConnectionOptions`
- `ReadTypedAsync`
- `WriteTypedAsync`
- `WriteBitInWordAsync`
- `ReadNamedAsync`
- `PollAsync`
- `ReadWordsSingleRequestAsync` / `ReadDWordsSingleRequestAsync`
- `ReadWordsChunkedAsync` / `ReadDWordsChunkedAsync`
- `KvHostLinkAddress.Normalize`

## Quick Start

### Installation

- Package page: <https://www.nuget.org/packages/PlcComm.KvHostLink/>

```powershell
dotnet add package PlcComm.KvHostLink
```

Or add a package reference directly:

```xml
<PackageReference Include="PlcComm.KvHostLink" Version="0.1.4" />
```

### High-Level Example

```csharp
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

ushort dm0 = (ushort)await client.ReadTypedAsync("DM0", "U");
await client.WriteTypedAsync("DM10", "U", dm0);

var snapshot = await client.ReadNamedAsync(
    new[] { "DM0", "DM1:S", "DM2:D", "DM4:F", "DM10.0" });

Console.WriteLine(string.Join(", ", snapshot.Select(kv => $"{kv.Key}={kv.Value}")));
```

## Supported PLC Registers

Start with these public high-level families first:

- word devices: `DM`, `EM`, `FM`, `W`, `ZF`, `TM`, `Z`
- bit devices: `R`, `MR`, `LR`, `CR`, `X`, `Y`, `M`, `L`
- typed forms: `DM100:S`, `DM100:D`, `DM100:L`, `DM100:F`
- bit-in-word forms: `DM100.3`, `DM100.A`
- timer/counter scalar forms: `T10:D`, `C10:D`

See the full public table in [Supported PLC Registers](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/user/SUPPORTED_REGISTERS.md).

## Public Documentation

- [Getting Started](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/user/GETTING_STARTED.md)
- [Supported PLC Registers](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/user/SUPPORTED_REGISTERS.md)
- [Latest Communication Verification](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/user/LATEST_COMMUNICATION_VERIFICATION.md)
- [User Guide](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/user/USER_GUIDE.md)
- [Device Handling](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/user/DEVICE_HANDLING.md)
- [Sample Projects](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/samples/README.md)
- [High-Level API Contract](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/HIGH_LEVEL_API_CONTRACT.md)

Maintainer-only notes and retained evidence live under `internal_docs/`.

## Common Workflows

Typed block reads:

```csharp
ushort[] words = await client.ReadWordsSingleRequestAsync("DM100", 10);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM200", 4);
```

Explicit chunked reads:

```csharp
ushort[] longWords = await client.ReadWordsChunkedAsync("DM1000", 200, maxWordsPerRequest: 64);
uint[] longDwords = await client.ReadDWordsChunkedAsync("DM2000", 40, maxDwordsPerRequest: 32);
```

Bit-in-word update:

```csharp
await client.WriteBitInWordAsync("DM50", bitIndex: 3, value: true);
```

Polling:

```csharp
await foreach (var snapshot in client.PollAsync(
    new[] { "DM100", "DM101:L", "DM50.3" },
    TimeSpan.FromSeconds(1)))
{
    Console.WriteLine(snapshot["DM100"]);
}
```

## Development and CI

```powershell
run_ci.bat
release_check.bat
```

Pack the NuGet package locally:

```powershell
dotnet pack src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj -c Release
```

## License

Distributed under the MIT License.
