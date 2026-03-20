# Changelog

All notable changes to this project will be documented in this file.

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
