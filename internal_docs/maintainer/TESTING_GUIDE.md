# Testing Guide

This document describes the test structure and verification approach for `PlcComm.KvHostLink`.

Related documents:

- [ARCHITECTURE.md](ARCHITECTURE.md)
- [PROTOCOL_SPEC.md](PROTOCOL_SPEC.md)

## Unit / Integration Tests

The automated test suite is under `tests/PlcComm.KvHostLink.Tests/`.

Run with:

```powershell
dotnet test KvHostLink.sln -v normal
```

Expected result: all tests pass, 0 warnings.

## Test Coverage

The test suite covers:

- Frame encoding and decoding for all supported commands
- Device address parsing (`R0`, `DM100`, `B1F`, etc.)
- Error response parsing (`E1`, `E2`, `E3`)
- Multi-device read/write round-trips (mock transport)
- 32-bit value packing (DWord, Float32)
- Extension methods: `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `PollAsync`

## Hardware Verification

Verified hardware targets:

- KEYENCE KV-7500 (TCP and UDP)

For live hardware tests, use the scripts in `plc-comm-hostlink-python/scripts/`.

## Cross-Library Parity

The .NET library is kept semantically aligned with `plc-comm-hostlink-python`.

When adding or changing a method, verify:

1. The equivalent Python operation exists and has the same semantics.
2. Low-level changes are reflected in `KvHostLinkClient`.
3. High-level helper changes are reflected in `KvHostLinkClientExtensions.cs` where applicable.
4. Intentional public API design differences remain documented in `API_UNIFICATION_POLICY.md`.

## CI

CI runs on every push via `.github/workflows/ci.yml`:

```powershell
dotnet build KvHostLink.sln
dotnet test KvHostLink.sln --no-build
```
