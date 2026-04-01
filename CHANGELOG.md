# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.5] - 2026-04-01

### Changed
- Refreshed the README, user docs, XML comments, and generated DocFX output to describe the explicit `SingleRequest` and `Chunked` helper split consistently.
- Extended regression coverage so write-side contiguous helper limits and dword chunking behavior stay locked to the documented high-level contract.

## [0.1.4] - 2026-03-28

### Changed
- Switched the README illustration to a GitHub raw URL so it renders on the NuGet package page.

## [0.1.3] - 2026-03-28

### Added
- Added `scripts/check_high_level_docs.ps1` to validate XML documentation coverage for the public high-level helper API.
- Added `scripts/check_sample_inventory.ps1` to ensure every user-facing sample project is referenced by the published docs.
- Added `release_check.bat` as a one-step release gate that runs CI and then rebuilds the published docs.
- Added two focused user-facing sample projects: `samples/PlcComm.KvHostLink.BasicReadWriteSample` and `samples/PlcComm.KvHostLink.NamedPollingSample`.
- Added `KvHostLinkClientExtensionsTests` coverage for high-level helper behavior.

### Changed
- Updated README, user docs, and high-level samples to match the current helper API behavior.
- Fixed the `WriteTypedAsync` Quick Start parameter order in the README.
- Removed stale float32 helper examples from the published documentation.
- Added helper-level `.F` float32 conversion for typed and named reads/writes.
- Updated `run_ci.bat` to build the library and tests by project, build all user-facing sample projects, and run XML-doc and sample-inventory gates.
- Expanded the README, user guide, and samples README with API-to-sample mapping tables for the recommended helper workflows.
- Updated `.gitignore` to ignore binary logs and common coverage artifacts.

## [0.1.2] - 2026-03-22

### Changed
- Unified `Directory.Build.props` with `TreatWarningsAsErrors`, `EnableNETAnalyzers`, and `AnalysisLevel=latest-recommended`.
- Enriched NuGet package metadata: added `PackageTags`, `PackageProjectUrl`, `PackageReadmeFile`, symbol package settings (`snupkg`), and source-link support.
- Fixed `README.md` Quick Start example to use correct namespace (`PlcComm.KvHostLink`) and class name (`KvHostLinkClient`).

## [0.1.0] - 2026-03-20

### Added
- Initial .NET 9.0 implementation of KEYENCE Host Link (Upper Link) protocol.
- `KvHostLinkClient`: Low-level protocol client supporting TCP and UDP.
- `KvHostLinkDevice`: Device address parsing and validation for KV series.
- `KvHostLinkProtocol`: ASCII frame building and response parsing.
- Support for core commands: RD, WR, RDS, WRS, ST, RS, STS, RSS, ?K, ?M, ?E, M, ER, WRT.
- Support for monitoring commands: MBS, MWS, MBR, MWR.
- Support for comment reading: RDC.
- Unit tests for device parsing and validation.
- Workspace-compliant project structure and distribution rules.
