[![CI](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/PlcComm.KvHostLink.svg)](https://www.nuget.org/packages/PlcComm.KvHostLink/)
[![Documentation](https://img.shields.io/badge/docs-GitHub_Pages-blue.svg)](https://fa-yoshinobu.github.io/plc-comm-hostlink-dotnet/)
[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Static Analysis: dotnet format](https://img.shields.io/badge/Lint-dotnet%20format-blue.svg)](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)

# KV Host Link Protocol for .NET

![Illustration](https://raw.githubusercontent.com/fa-yoshinobu/plc-comm-hostlink-dotnet/main/docsrc/assets/kv.png)

Modern .NET library for KEYENCE KV series PLCs using the Host Link
(Upper Link) protocol.

This README intentionally covers the recommended high-level API only:

- `OpenAndConnectAsync`
- `ReadTypedAsync`
- `WriteTypedAsync`
- `WriteBitInWordAsync`
- `ReadNamedAsync`
- `PollAsync`
- `ReadWordsAsync`
- `ReadDWordsAsync`

Low-level token-oriented methods and protocol details are kept in maintainer
documentation.

## Key Features

- Async-first .NET API
- High-level typed read/write helpers
- Mixed snapshots with `ReadNamedAsync`
- Polling with `PollAsync`
- Contiguous block helpers for `ushort[]` and `uint[]`
- Hardware-verified against KV-7500

## Quick Start

### Installation

- Package page: https://www.nuget.org/packages/PlcComm.KvHostLink/

```powershell
dotnet add package PlcComm.KvHostLink
```

Or add a package reference directly:

```xml
<PackageReference Include="PlcComm.KvHostLink" Version="0.1.4" />
```

You can also reference `src/PlcComm.KvHostLink/PlcComm.KvHostLink.csproj` directly during local development.

### High-level example

```csharp
using PlcComm.KvHostLink;

await using var client = await KvHostLinkClientExtensions.OpenAndConnectAsync(
    "192.168.250.100",
    8501);

ushort dm0 = (ushort)await client.ReadTypedAsync("DM0", "U");
await client.WriteTypedAsync("DM10", "U", dm0);

var snapshot = await client.ReadNamedAsync(
    new[] { "DM0", "DM1:S", "DM2:D", "DM4:F", "DM10.0" });

Console.WriteLine(string.Join(", ", snapshot.Select(kv => $"{kv.Key}={kv.Value}")));
```

## Common Workflows

Typed block reads:

```csharp
ushort[] words = await client.ReadWordsAsync("DM100", 10);
uint[] dwords = await client.ReadDWordsAsync("DM200", 4);
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

## Sample Projects

Buildable sample projects are under `samples/`:

- `PlcComm.KvHostLink.HighLevelSample`
- `PlcComm.KvHostLink.BasicReadWriteSample`
- `PlcComm.KvHostLink.NamedPollingSample`

API and workflow to sample mapping:

| API / workflow | Primary sample | Purpose |
|---|---|---|
| `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `ReadWordsAsync`, `ReadDWordsAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, `PollAsync` | `samples/PlcComm.KvHostLink.HighLevelSample/PlcComm.KvHostLink.HighLevelSample.csproj` | End-to-end walkthrough of the full helper surface |
| `ReadTypedAsync`, `WriteTypedAsync`, `ReadWordsAsync`, `ReadDWordsAsync` | `samples/PlcComm.KvHostLink.BasicReadWriteSample/PlcComm.KvHostLink.BasicReadWriteSample.csproj` | Focused typed and block-read example |
| `ReadNamedAsync`, `WriteBitInWordAsync`, `PollAsync` | `samples/PlcComm.KvHostLink.NamedPollingSample/PlcComm.KvHostLink.NamedPollingSample.csproj` | Mixed snapshot and monitoring example |

Run examples:

```powershell
dotnet run --project samples/PlcComm.KvHostLink.HighLevelSample -- 192.168.250.100 8501
dotnet run --project samples/PlcComm.KvHostLink.BasicReadWriteSample -- 192.168.250.100 8501
dotnet run --project samples/PlcComm.KvHostLink.NamedPollingSample -- 192.168.250.100 8501
```

## Documentation

User documentation:

- [User Guide](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/user/USER_GUIDE.md)
- [Device Handling](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/user/DEVICE_HANDLING.md)
- [Sample Projects](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/samples/README.md)

Maintainer and QA documentation:

- [QA Reports](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/tree/main/docsrc/validation/reports)
- [Protocol Specification](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/maintainer/PROTOCOL_SPEC.md)
- [API Unification Policy](https://github.com/fa-yoshinobu/plc-comm-hostlink-dotnet/blob/main/docsrc/maintainer/API_UNIFICATION_POLICY.md)

## Development and CI

```powershell
run_ci.bat
release_check.bat
```

`run_ci.bat` builds the library, tests it, checks formatting, builds all
user-facing sample projects, verifies XML docs coverage for the public
high-level API, and checks sample references in the docs.

`release_check.bat` runs `run_ci.bat` and then rebuilds the published docs.

Pack the NuGet package locally:

```powershell
dotnet pack src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj -c Release
```

## License

Distributed under the MIT License.
