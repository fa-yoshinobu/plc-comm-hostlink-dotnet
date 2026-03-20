# (KEYENCE KV) Host Link Communication .NET

[![.NET 9.0](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/download/dotnet/9.0)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Static Analysis: dotnet format](https://img.shields.io/badge/Lint-dotnet%20format-blue.svg)](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format)

A modern .NET client library for KEYENCE KV series PLCs using the **Host Link Communication** protocol. Designed for performance and reliability on .NET 9.0.

## Key Features

- **Keyence Focused**: Specialized for KV-8000, KV-7500, and other KV series Upper Link communication.
- **Async Native**: Built using modern C# asynchronous patterns (TAP).
- **Single-File Distribution**: Supports self-contained publishing.
- **Zero Mojibake**: English-only documentation and strict UTF-8 standards.
- **CI-Ready**: Built-in quality checks and publishing via `run_ci.bat`.

## Quick Start

### Basic Usage
```csharp
using HostLink;

// Connect to a KEYENCE KV PLC
using var client = new HostLinkClient("192.168.1.10", 8501);

// Read D100 (Word)
int val = await client.ReadWordAsync("D100");
Console.WriteLine($"Value: {val}");
```

## Documentation

Follows the workspace-wide hierarchical documentation policy:

- [**User Guide**](docsrc/user/USER_GUIDE.md): Setup procedures and API examples.
- [**QA Reports**](docsrc/validation/reports/): Verified results with real Keyence hardware.
- [**Developer Notes**](docsrc/maintainer/PROTOCOL_SPEC.md): Internal implementation of the Host Link protocol.

## Development & CI

Quality is managed via `run_ci.bat`.

### Local CI & Publish
```bash
run_ci.bat
```
Validates the code and publishes a self-contained Single-File EXE to the `publish/` directory.

## License

Distributed under the [MIT License](LICENSE).


