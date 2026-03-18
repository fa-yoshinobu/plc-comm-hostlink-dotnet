# Agent Guide: Host Link Communication .NET

This repository is part of the PLC Communication Workspace and follows the global standards defined in `D:\PLC_COMM_PROJ\AGENTS.md`.

## 1. Project-Specific Context
- **Protocol**: Host Link Communication
- **Target Hardware**: KEYENCE KV series (KV-8000, KV-7500, etc.)
- **Language**: .NET 9.0 (C#)
- **Role**: Core Communication Library for KEYENCE Upper Link protocol.

## 2. Mandatory Rules (Global Standards)
- **Language**: All code, comments, and documentation MUST be in **English**.
- **Encoding**: Use **UTF-8 (without BOM)** for all files to prevent Mojibake.
- **Mandatory Static Analysis**:
  - All changes must pass `dotnet format` and Roslyn analyzers (StyleCop/SonarAnalyzer).
  - Use `dotnet build` to verify compliance.
- **Documentation Structure**: Follow the Modern Documentation Policy:
  - `docs/user/`: User manuals and API guides. [DIST]
  - `docs/maintainer/`: Protocol specs and internal logic. [REPO]
  - `docs/validation/`: Hardware QA reports and bug analysis. [REPO]
- **Distribution Control**: Ensure `.csproj` excludes `docs/maintainer/`, `docs/validation/`, `tests/`, and `TODO.md` from NuGet packages (`.nupkg`).

## 3. Reference Materials
- **Official Specs**: Refer to `local_folder/kv/HOST LINK.pdf` for the authoritative English manual (Local only).
- **Evidence**: Check `docs/validation/reports/` for verified communication results with KEYENCE KV-series PLCs.

## 4. Development Workflow
- **Issue Tracking**: Log remaining tasks in `TODO.md`.
- **Change Tracking**: Update `CHANGELOG.md` for every fix or feature.
- **QA Requirement**: Every hardware-related fix must include an evidence report in `docs/validation/reports/`.

## 5. API Naming Policy

Detailed naming policy lives in `docs/maintainer/API_UNIFICATION_POLICY.md`.

Public API rules:

- Reserve `KvHostLinkClient` for the low-level protocol client.
- Reserve `KvHostLinkDeviceClient` for the future high-level string-device client.
- Reserve `AsyncKvHostLinkClient` and `AsyncKvHostLinkDeviceClient` for future async parity.
- Do not use plain `HostLinkClient` or `HostLinkHighLevelClient` as canonical public names because `Host Link` is ambiguous across PLC vendors.
- High-level operations should follow `Read`, `Write`, `ReadMany`, `WriteMany`, and async `*Async` naming.
- 32-bit helpers should use `ReadDWord`, `WriteDWord`, `ReadDWords`, `WriteDWords`, `ReadFloat32`, and `WriteFloat32` style names.

Private or helper naming rules:

- Avoid vague names like `ReadOne`, `WriteOne`, `DoRead`, or `Offset`.
- Prefer names that describe the resolved device or batch role, such as `ReadResolvedDevice`, `WriteResolvedBatch`, or `NormalizeResolvedWriteItems`.
- 32-bit codec helpers should include both type and word order, for example `PackUInt32LowWordFirst` or `UnpackFloat32LowWordFirst`.
