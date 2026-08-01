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
- Comment reads: exact `ReadCommentBytesAsync` payloads and strict explicit-codec `ReadCommentsAsync` decoding
- Extension methods: `ReadTypedAsync`, `WriteTypedAsync`, `ReadNamedAsync`, `PollAsync`

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

The authoritative complete gate remains the Windows job across .NET 8, 9, and
10. A separate Linux/.NET 10 job runs only tests carrying the
`CrossOsLifecycle` trait. That bounded job uses fake or IPv4 loopback peers and
does not certify every target framework, build/package path, or live PLC.

## Documented API diff

`scripts/check_documented_api_diff.py` compares the candidate net8.0, net9.0,
and net10.0 assemblies independently with the matching assemblies in the
immutable `PlcComm.KvHostLink` 3.2.1 NuGet baseline recorded under
`internal_docs/maintainer/api_baselines`. The package URL, entries, and SHA-256
are fixed; set `PLC_COMM_API_BASELINE_PACKAGE` to an already downloaded exact
package when running offline. A digest mismatch always fails. The prior stable
README, four standard user pages, generated API reference, and maintained
samples are separately pinned to full commit
`19df212d9bbed545d137e1e6d71b8afb30237628` and per-file Git blob IDs. Set
`PLC_COMM_API_BASELINE_CONTRACT_ROOT` only to an exact local checkout of that
commit when an offline run must replace the immutable raw-file downloads.

Every added, removed, or changed exact symbol must occur once for all three
target frameworks in
`documented_api_diff_classifications.json` as `documented-contract`,
`undocumented-public`, `additive`, or `generated-or-noncontract`. Missing,
duplicate, wildcard, stale, and before/after full-signature drift records fail.
Documented/undocumented decisions search only the immutable prior stable
contract files, rather than mutable candidate documentation. The release workflow additionally
rejects a documented incompatible change until the package major version is
greater than the baseline major version.

The inspector compares externally accessible public, protected, and protected-
internal types and members, including editor-hidden and compiler-generated
accessible surface. It records inheritance/interfaces, parameter and return
types including nullable metadata, default values, generic constraints,
operators, indexers, init-only setters, enum underlying types, const values,
and canonical profile-name and descriptor values. Duplicate API IDs fail
instead of being overwritten. XML/generated API documentation is
kept current by the separate generated-reference gate and is mandatory
evidence for documented classifications. Reflection cannot prove behavioral
exception changes or interpret prose semantics, so self-review must still
inspect source error paths, XML comments, generated API pages, README/user
guides, maintained samples, package contents, and every actual classification.
