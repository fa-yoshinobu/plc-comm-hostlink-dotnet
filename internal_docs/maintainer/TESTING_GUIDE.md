# Testing Guide

This document describes the test structure and verification approach for `PlcComm.KvHostLink`.

## Unit / Integration Tests

The automated test suite is under `tests/PlcComm.KvHostLink.Tests/`.

Run with:

```powershell
call run_ci.bat
```

`run_ci.bat` is the canonical local gate. It builds the library and tests,
runs all target-framework tests, checks formatting and generated API docs, and
builds the documented samples.

## Test Coverage

The test suite covers:

- Frame encoding and decoding for all supported commands
- Device address parsing (`R0`, `DM100`, `B1F`, etc.)
- Error response parsing (`E1`, `E2`, `E3`)
- Multi-device read/write round-trips (mock transport)
- 32-bit value packing (DWord, Float32)
- Extension methods: `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `PollAsync`

## Hardware Checks

Live hardware checks require a separately approved controlled-test plan. Keep
current target support in the profile data, not in this maintainer guide.

## Cross-Library Parity

The .NET library is kept semantically aligned with `plc-comm-hostlink-python`.

When adding or changing a method, verify:

1. The equivalent Python operation exists and has the same semantics.
2. Low-level changes are reflected in `KvHostLinkClient`.
3. High-level helper changes are reflected in `KvHostLinkClientExtensions.cs` where applicable.
4. Intentional public API differences stay covered by tests and public docs.

## CI

CI runs on every push via `.github/workflows/ci.yml`:

The workflow runs the same solution, `PlcComm.KvHostLink.sln`; use
`run_ci.bat` locally to include the repository-specific documentation and
sample gates.
