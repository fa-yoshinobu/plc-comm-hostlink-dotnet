# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

**Entry labels**

- `Release`: Package/version metadata and publishing preparation.
- `Library`: Runtime behavior, public API, protocol handling, or validation in the distributed library.
- `Docs`: README, user guides, generated API docs, or other documentation-only changes.
- `Samples`: Examples, sample flows, sample scripts, or sample applications.
- `Tests`: Test suites, test fixtures, golden vectors, or verification data.
- `Tooling`: Developer/operator command-line tools and helper utilities.
- `CI`: Release checks, workflow scripts, or automation-only changes.

## [Unreleased]

## [3.1.0] - 2026-07-13

### BREAKING
- Library: Require host, port, TCP/UDP transport, and canonical PLC profile in direct constructors and connection options. Only timeout remains optional, with a three-second default.
- Library: Require explicit `OpenAsync` before commands and after close or transport failure; commands no longer connect, reconnect, or retry implicitly.
- Library: Return terminator-free `byte[]` from the maintainer `SendRawAsync` API without semantic decoding or PLC-error translation.
- Library: Remove `AppendLfOnSend`, comment padding switches, all public chunked helpers, and the ineffective `ParseDevice(string, bool)` compatibility overload.
- Library: Require base devices and separate data formats for numeric access, monitor-word registration, timer/counter set values, and expansion-unit buffer access. Suffix-bearing low-level device input is rejected.
- Library: Require an explicit value in `SetTimeAsync`; the library no longer substitutes the host clock.
- Library: Restrict timeouts to 1 through `Int32.MaxValue` milliseconds before transport creation and restrict PLC clock years to 2000 through 2099.
- Library: Derive semantic read response counts from the command and device width, including 16/32-point direct-bit numeric reads; direct-bit responses accept only documented `0`/`1`/`ON`/`OFF` and malformed shapes invalidate the session.
- Library: Remove the obsolete public `ParseDeviceText` and public format-inference surface; internal logical-address parsing no longer appears as a compatibility API.

### Added
- Library: Added `KvHostLinkPlcProfileDescriptor` and `KvHostLinkPlcProfiles.GetProfileDescriptors()` for canonical Host Link profile metadata.

### Changed
- Library: Fix normal command framing to CR, isolate maintainer trace-hook failures, cap response bodies at 65,536 bytes, and invalidate transport state after timeout, cancellation, malformed response, count mismatch, or overflow.
- Library: Use one native `.D` request for Dword reads and writes, limited to 500 values; word requests remain limited to 1,000 values.
- Library: Hold one client lock across bit-in-word read-modify-write sequences and validate BIT, integer, hexadecimal, signed, and unsigned values without masking or truncation.
- Samples: Require explicit endpoint port and transport in multi-PLC CLI and JSON configuration paths.

- Release: Bumped .NET package metadata to `3.1.0`.

### Deprecated
- Library: Deprecated the ineffective `ParseDevice(string, bool)` compatibility overload; device types remain explicit.

### Fixed
- Library: Corrected ten KV device range cells against live PLC hardware and the KEYENCE simulator, and pinned the canonical profile source to `plc-comm-hostlink-profiles` `v1.2.0`. `VM` widens to `VM0-9999` on KV-NANO and `VM0-59999` on KV-3000/KV-5000; `Z` widens to `Z1-23` on KV-8000. `CTH` narrows to `CTH0-1` on the KV-3000 and KV-5000 XYM profiles, matching their base profiles: `CTH2` and `CTH3` were previously accepted there and are now rejected.
- Library: Apply `Timeout` to UDP receives and discard TCP/UDP transports after an incomplete exchange.
- CI: Require exact-tag checkout and verify tag, manifest, and NuGet artifact versions before a GitHub Release upload.
- Tooling: Render XML `cref` method labels without leaking parameter-type suffixes into the generated API reference.
- Docs: Correct the supported-profile scope, `CTH`/`CTC` parser behavior, and maintainer commands.

### Tests
- Tests: Add contract coverage for required connection values, explicit-open state, raw bytes, comment padding, format and range rejection, response counts and cap, native Dword limits, compound locking, trace isolation, and queued cancellation.
- Tests: Remove library-local cross-implementation frame vectors; cross-language verification is maintained as a separate repository and test concern.

## [3.0.0] - 2026-07-10

### Changed
- Release: Bumped .NET package metadata to `3.0.0`.
- Packaging: Marked samples, CLI, and validation tools non-packable so only the library package is produced.
- Docs: Replaced relative README links with absolute URLs so they resolve on package registry pages.
- Docs: Updated PLC profile documentation and the generated API reference for the new profile API location.
- Tests: Updated PLC profile display-name coverage to assert the profile API instead of device-range APIs.

### BREAKING
- Library: Breaking: Moved PLC profile lookup APIs to `KvHostLinkPlcProfiles`; the old `KvHostLinkDeviceRanges` profile methods are no longer the supported location.
- Migration: Use `KvHostLinkPlcProfiles.GetNames`, `NormalizeName`, `GetDisplayName`, and `FromName`; use `KvHostLinkDeviceRanges` only for the device-range catalog.

## [2.0.0] - 2026-07-06

### BREAKING
- Release: No .NET package ID changed; this package is versioned at `2.0.0` to align with the plc-comm family breaking release wave.

### Changed
- Release: Bumped package metadata to `2.0.0`.
- Docs: Added the plc-comm family package matrix link to the README.
- Tooling: Moved .NET project version metadata to `Directory.Build.props` and added common `plc-comm` package tags.

## [1.3.0] - 2026-07-06

### Added
- Release: Bumped package metadata to `1.3.0` and synced the embedded profile fixture to `plc-comm-hostlink-profiles` `v1.1.0`.
- Library: Added `CTH`/`CTC` (high-speed counter / comparator, codes 04H/05H) device support to the address parser and command device-type sets, treated like the counter (`C`) device. Availability is model/unit dependent (governed by the canonical catalog).
- Library: Synced the embedded KV Host Link device-range catalog with the canonical `TC`/`TS`/`CC`/`CS` (timer/counter current and set value) rows and official `device_name` labels.

### Fixed
- Library: Corrected the misspelled `KvDeviceRangeCategory.FileRefresh` enum member to `FileRegister`. The category is a descriptive label only; device identification uses `DeviceType`/device code and bit/word width uses `IsBitDevice`.

## [1.2.0] - 2026-07-05

### Changed
- Release: Bumped package metadata to `1.2.0`.
- Tooling: Normalized line-ending handling in the canonical profile JSON update script so `-SourceRoot` runs no longer report false changes.
- Library: Synced the embedded KV Host Link device-range fixture to `plc-comm-hostlink-profiles` `v1.0.1`, including `display_name` labels for KEYENCE model families and XYM variants.
- Library: Added `KvHostLinkDeviceRanges.GetDisplayName(plcProfile)` as the public UI-label helper while keeping stored PLC profile values canonical.
- Docs: Documented the profile display-name helper and canonical-ID storage guidance.
- Tests: Added canonical fixture parity coverage for profile `display_name` values.
- Samples: Added read-only multi-PLC monitoring and JSON config polling recipes with independent reconnect loops, dry-run validation, and long-form CSV output.
- Docs: Added generated .NET API reference from the public assembly surface and XML documentation comments, with CI freshness validation.
- Docs: Removed the per-library troubleshooting/code page; shared KV Host Link troubleshooting and code guidance now lives in the PLC Setup Guide.
- Docs: Removed the per-library latest communication verification page and links so user docs stay focused on usage, not verification logs.
- Docs: Removed the manual page-navigation block from Getting Started and rely on site navigation instead.
- Docs: Removed the thin per-library Troubleshooting page after moving common KV Host Link troubleshooting to the PLC Setup Guide.
- Docs: Moved shared KV Host Link gotcha and troubleshooting items to the common PLC Setup Guide and standardized the Gotchas page structure with SLMP.
- Docs: Moved shared supported-register and device-range guidance to the common KV Host Link Device Ranges page and kept the user docs to Getting Started, Usage Guide, PLC Profiles, and Gotchas.

## [1.1.1] - 2026-06-29

### Changed
- Release: Bumped package metadata to `1.1.1`.
- Docs: Documented explicit Host Link value-format requirements in user docs and public XML comments.
- Samples: Updated high-level and polling samples to use explicit value-format suffixes.

## [1.1.0] - 2026-06-29

### Changed
- Release: Bumped package metadata to `1.1.0`.
- Library: Multi-targeted the package for `net8.0`, `net9.0`, and `net10.0`.
- Library: Made Host Link device parsing require explicit device areas and value-format suffixes; numeric-only devices no longer default to `R`, and suffixless named addresses no longer infer a default format.
- Docs: Documented `DM100:COMMENT` named reads in the public .NET XML documentation.
- Docs: Refreshed Host Link supported-register and usage guidance.
- Docs: Updated the SDK prerequisite guidance for the multi-target package.
- Samples: Updated the high-level sample to restore the original PLC values after demonstration writes.
- Tests: Updated `Microsoft.NET.Test.Sdk` to `18.7.0`.
- Tests: Updated Host Link parser, high-level helper, and shared frame-vector coverage for explicit device/value-format requirements.
- Tests: Multi-targeted the library test project for `net8.0`, `net9.0`, and `net10.0`.
- Tooling: Updated the high-level XML documentation coverage check to read the `net10.0` build output.
- CI: Installed .NET 8, .NET 9, and .NET 10 SDKs in CI, sample-build, and release workflows.

### Fixed
- Library: Reject malformed embedded device-range segments while building the KV range catalog instead of silently defaulting invalid lower bounds to `0`.
- Library: Made `BIT_IN_WORD` helper addresses require an explicit bit index such as `DM100.0` through `DM100.F`; `DM100:BIT_IN_WORD` now fails instead of silently reading bit 0.
- Library: Missing Host Link response tokens now raise a protocol error instead of being treated as value `0`.
- Tests: Added coverage for invalid embedded device-range segment parsing.
- Tests: Added coverage for rejecting `BIT_IN_WORD` addresses without an explicit bit index and for missing response tokens.

## [1.0.0] - 2026-06-24

### Changed
- Release: Bumped NuGet and sample project metadata to `1.0.0` for the first stable release line.
