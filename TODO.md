# TODO: Host Link Communication .NET

This file tracks the remaining tasks and issues for the Host Link Communication (Keyence KV) .NET library.

## 1. Project Scaffold
- [x] **Create the Actual Library Project**: Initial project structure for `src/PlcComm.KvHostLink`.
- [x] **Fix the Canonical API Shape**: Implemented `KvHostLinkClient` and `KvHostLinkDevice`.

## 2. Protocol and Feature Work
- [x] **Frame / Parser Core**: Implemented Keyence KV Host Link frame builder, parser, and transport handling.
- [x] **Async Surface**: Implemented modern async-first API (TAP).
- [ ] **High-Level Device Client**: Create `KvHostLinkDeviceClient` for string-based device operations (as specified in AGENTS.md).

## 3. Testing and Validation
- [x] **Unit Test Base**: Added `PlcComm.KvHostLink.Tests` for device parsing and validation.
- [ ] **Hardware Evidence**: Validate against a real KV-8000 / KV-7500 class target and write reports in `docs/validation/reports/`.

## 4. Documentation and Packaging
- [x] **User Guide**: Created `docs/user/USER_GUIDE.md`.
- [x] **NuGet Packaging Rules**: Applied strict distribution exclusions in `.csproj` (docs/maintainer, docs/validation, tests, etc.).
