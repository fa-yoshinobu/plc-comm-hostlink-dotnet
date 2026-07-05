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

### Changed
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
