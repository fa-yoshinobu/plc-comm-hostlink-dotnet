# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
