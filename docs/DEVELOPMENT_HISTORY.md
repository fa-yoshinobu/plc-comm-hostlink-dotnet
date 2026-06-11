# Development History

## 2026-06-11 Archived Refactor Plan

The previous `refactor-instructions.md` was archived into this history file.

### Scope

- Library: .NET KEYENCE KV Host Link package.
- Primary task: expand `HostLinkFrameVectorTests` command coverage.
- Optional small task: move read-plan internals from `KvHostLinkClientExtensions.cs` into an internal file if low risk.

### Contracts To Preserve

- All public APIs, method signatures, defaults, and NuGet-facing behavior.
- Exact transmitted Host Link frame strings covered by existing vectors.
- Protocol fixed points, including pre-send `AT` write rejection and timer/counter preset restrictions.
- `ReadNamedAsync` read-plan split rules and result ordering.
- `QueuedKvHostLinkClient` serialization semantics used by downstream apps.
- Package ID, version `0.1.11`, and changelog.

### Debt Notes

- D1: frame vectors did not cover enough of the public command surface.
- D2: read-plan internals lived in the extensions file and could be moved internally after D1.
- Other items were documented for report-only follow-up.

### Planned Verification

- Build a public-command to vector-coverage table before and after work.
- Add vectors one command at a time using current transmitted strings.
- Run the full test suite after additions.
- If D2 was attempted, ensure extension tests passed without assertion changes.

### Out Of Scope

- Public API or frame-string changes.
- Version, changelog, package metadata, or downstream app changes.
