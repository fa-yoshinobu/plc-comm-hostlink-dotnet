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

## [4.0.0] - 2026-08-07

- Release: Bumped .NET package metadata to `4.0.0` for the approved breaking-contract release.
- Library: Restored the explicit Boolean-only `WriteBitInWordAsync` operation for ordinary 16-bit word devices. It validates the complete plan before FIFO admission, always performs one word read followed by one word write in one client turn, and uses one absolute deadline for both requests after activation. The operation is intentionally not PLC-atomic and performs no write fallback, retry, or success readback.
- Library: Added `WriteBitInExpansionUnitBufferAsync` for the existing URD/UWR route. It fixes the route to one unit/address and `.U` word, applies the same Boolean-only preflight, FIFO, absolute-deadline, non-PLC-atomic, and outcome-unknown contract, and never falls back to another route.
- Library: Made FIFO activation strongly exception-safe: cancellation/deadline setup failure now disposes every partially constructed registration/source and always releases the admitted lease so later operations cannot remain queued indefinitely.
- Docs: Documented the bit-in-word write concurrency, cancellation, timeout, and outcome-unknown contract and the required migration from manual read/write sequences.

- Library: **Breaking:** Low-level formatted timer/counter reads now preserve the structural first response token as exact `0` or `1` and apply `.U`, `.S`, `.H`, `.D`, or `.L` only to current and preset values. In particular, `.H` no longer exposes the erroneous synthesized status `0000` or `0001`; use `0` or `1`.
- Library: Correct formatted single reads of direct-bit devices to accept the PLC's one packed scalar response token instead of expecting 16 or 32 separate bit tokens. Signed `.S` and `.L` responses accept the PLC's explicit leading `+`; bare bit reads remain strict `0`/`1`/`ON`/`OFF` reads. Public signatures are unchanged.
- Library: **Breaking:** `KvMonitorWordTarget.DataFormat` is now nullable with a `null` default. Only bare direct-bit MWS targets may omit it; their MWR fields accept one through five ASCII decimal digits, including leading zeros, over the unsigned 16-bit range while the transmitted registration remains bare. Consumers that read or deconstruct `DataFormat` must now handle `null`. Empty or whitespace formats remain invalid, and scalar RD plus MBS/MBR retain strict bit semantics.
- Library: A replacement MWS invalidates the previous monitor-word decoder metadata when its active wire turn begins, so a failed registration or later reconnect cannot reuse a stale response plan.
- Tests: Added bare MWS/MWR live vectors, unsigned boundaries and invalid grammar, mixed-format ordering, preflight, strict scalar/bit-monitor separation, protocol-retirement, failed-registration, and reconnect coverage.
- Docs: Documented that Host Link TCP has no request identifier, the residual pre-send-check-to-response association race, and the decision to preserve healthy connection reuse instead of adding per-request connection latency without improving correlation.
- Docs: Clarified that the maintainer raw API requires one non-empty ASCII command and rejects an empty body before FIFO, connection state, DNS, socket work, or send.
- Tests: Added TCP/UDP public raw empty-input preflight coverage with unchanged connection state, traffic counters, and trace activity.
- Tests: Limited test-server shutdown exception handling to accept/read/write transport operations, preserved pre-shutdown handler failures, and added deterministic accept-start and failure-propagation coverage.
- Library: Transport receive buffers are now allocated only for the selected transport after open admission, reused for the logical session, and released on explicit close. TCP framing scans incrementally, UDP receives into its session buffer, and tracing creates an owned receive snapshot only while a hook is installed.
- Tests: Added deterministic transport-buffer allocation/reuse/release checks, maximum-size one-byte-fragment TCP scan/copy bounds, and hostile trace-callback ownership coverage.
- Library: **Breaking:** Reject bracketed IPv4 host input such as `[127.0.0.1]` during construction; use an unbracketed IPv4 address.
- Library: **Breaking:** Limit raw ASCII command bodies to 65,506 bytes and complete CR-terminated request frames to 65,507 bytes for both TCP and UDP, rejecting oversized input before transport access.
- Tests: Prevent the asynchronous TCP test server shutdown race by treating `InvalidOperationException` as expected only after cancellation, with repeated immediate-disposal coverage.
- Docs: Corrected generated `init` accessors, distinguished waiting cancellation from active-operation session retirement, and made general expansion-buffer and low-level examples read-only with explicit recovery guidance.
- Tests: Added real mutable/init-only generator fixtures and documentation-contract checks for lifecycle, generated summaries, and state-changing example safety.
- Library: TCP rejects and retires the connection when one receive already contains non-empty data after the first terminated response, preventing response misassociation.
- Library: UDP open now creates one connected socket and successful exchanges reuse it. Timeout, cancellation, I/O, malformed-response, protocol, extra-response, or pre-send unowned-datagram anomalies discard that socket while retaining the resolved logical endpoint; the next request creates one replacement without DNS resolution or retrying the failed request.
- Library: The connected-client factory disposes a client whose open operation fails without replacing the original failure.
- Library: **Breaking:** Every semantic hexadecimal read, including low-level and monitor reads, returns exactly four uppercase digits; raw body bytes and write wire formatting are unchanged.
- Library: **Breaking:** Float32 parsing, normalization, formatting, typed reads and writes, named reads, and polling now accept only the canonical ordinary one-word families `DM`, `EM`, `FM`, `ZF`, `W`, `TM`, `CM`, `VM`, `D`, `E`, and `F`; `Z`, direct-bit, and special-response families such as `R`, `T`, `C`, and `AT` are rejected before FIFO admission and transport.
- Library: **Breaking:** Typed Float32 writes now reject NaN, infinities, and values that overflow binary32 before transport while accepting the finite binary32 boundary values.
- Library: **Breaking:** Timer/counter composite response status must be exactly `0` or `1`; any other numeric status is an invalid response and retires the connection.
- Library: **Breaking:** `PollAsync` now requires a strictly positive interval and continues to reject intervals above `Int32.MaxValue` milliseconds before communication.
- Library: **Breaking:** Named aggregate reads now reject semantically duplicate keys after device, address, dtype, bit-index, and scalar-count normalization; spelling variants no longer create two keys, while distinct dtype views, bit indices, and overlapping spans remain valid.
- Samples: Retargeted all six repository samples from `net9.0` to the current LTS `net10.0`; building or running these samples now requires the .NET 10 SDK, while library and test projects retain `net8.0;net9.0;net10.0` multi-targeting.
- Library: **Breaking:** Device-comment text reads now require an explicit `HostLinkCommentEncoding.Utf8` or `.Cp932`; removed UTF-8-first/Shift_JIS-fallback decoding, added exact `ReadCommentBytesAsync` payload access, and reject comment aggregates without a codec before transport.
- Library: Comment aggregate overloads are disjoint: the ordinary overload rejects `:COMMENT`, while the explicit-codec overload requires at least one `:COMMENT` and rejects an unused codec with an argument error before transport.
- Library: Strict comment decoding now rejects malformed selected text without replacement or fallback and retires the connection; `Cp932` is Windows-31J/code page 932 compatibility for KEYENCE "Shift_JIS" terminology, accepts the shared mapped Windows-extension pairs, and rejects forbidden singleton bytes and unassigned pairs consistently across runtimes.
- Tests: Added raw comment payload, explicit ambiguous-codec, malformed-input, aggregate preflight, invalid-enum, PLC-error, padding, and connection-retirement coverage.
- Docs: Documented explicit device-comment codec selection, raw payload semantics, aggregate requirements, and migration impact in user and maintainer references.
- CI: The packed-consumer gate now uses an isolated NuGet cache so an existing same-version global package cannot mask the candidate, uses a neutral disposable MSBuild project to avoid IDE discovery locks, and retries transient Windows cleanup locks separately from the package result.
- Docs: Corrected the final current-worktree verification evidence to 221 tests per target framework (663 extracted-archive test executions).
- Library: **Breaking:** Removed `QueuedKvHostLinkClient`; the ordinary `KvHostLinkClient` now owns exact FIFO admission for all low- and high-level operations, rejects same-client reentrancy, snapshots admitted inputs and timeout configuration, starts the absolute transaction deadline only on activation, and retires active/queued generations on close.
- Library: **Breaking:** Removed the multi-request `WriteBitInWordAsync` read-modify-write helper. Single-request writes remain available, and individual direct-bit writes accept only Boolean scalar/collection values; numeric `0`/`1` compatibility inputs are rejected before transport.
- Library: **Breaking:** TCP and UDP endpoints are IPv4-only. IPv6 literals are rejected before socket creation, and hostname resolution selects IPv4 without IPv6 fallback.
- Library: Added dedicated `HostLinkClosedError`, `HostLinkNotConnectedError`, `HostLinkReentrancyError`, and structured `HostLinkOutcomeUnknownError`. State-changing post-send timeout, cancellation, close, transport, or invalid-response failures are outcome-unknown and are never retried automatically.
- Library: `ReadNamedAsync` and each `PollAsync` cycle now prevalidate and snapshot the complete read-only aggregate, group wire-compatible device families by first appearance, sort within each group, merge contiguous spans to protocol limits, keep non-batchable native reads without disabling other batching, preserve input-order results, keep multiword values inside one request, retain one FIFO turn through decode, and return no partial result after failure.
- Library: `PollAsync` compiles its fixed plan once, reuses the same plan for every non-overlapping cycle, and applies its completion-delay interval outside the client FIFO turn without catch-up scheduling.
- Release: Aligned artifact roles so the registry package contains consumer runtime, native API metadata, license, README, and ecosystem-native examples where applicable while excluding repository tests and maintainer tooling; the GitHub source archive retains tracked non-hardware validation and maintainer inputs.
- Library: Added `HostLinkTimeoutError` to distinguish a known-outcome absolute transaction timeout from caller-requested `OperationCanceledException`.
- Library: `Close`, `CloseAsync`, `Dispose`, and `DisposeAsync` now invalidate the connection lifetime and promptly interrupt active I/O; queued work from the old generation is rejected and never replayed.
- Library: Float32 writes to every direct bit device family are rejected before transport instead of being emitted as consecutive bit writes.
- Library: Corrected `R`, `MR`, `LR`, and `CR` catalog bounds and point counts by decoding the final decimal `00..15` bit field as `bank * 16 + bit` while preserving PLC display notation.
- Library: Protected the cached profile-name and profile-descriptor collections from mutation through backing-array or mutable-interface casts.
- Samples: Made every runnable sample read-only by default; write demonstrations now require `--allow-writes`, use changing test values, and restore captured values.
- Tests: Added deterministic FIFO, timeout activation, caller-cancellation, close-generation, reentrancy, cross-client parallelism, Boolean-only bit, IPv4-only, outcome-unknown, aggregate preflight/order/boundary/failure, direct-bit Float, banked-range, immutable-collection, and sample-safety coverage.
- CI: GitHub source archives now include tests, fixtures, and the scripts needed for restore/build/test/format/documentation/package verification; a separate guard keeps NuGet packages minimal.
- CI: The NuGet package guard now restores and runs an isolated net8.0 consumer using only the generated local package, in addition to inspecting package contents.
- CI: The NuGet guard now rejects CI, cache/build, source, maintainer, release-output, tools, and credential-like material in addition to its consumer-file allowlist.
- CI: Source-archive validation can now synthesize the complete current worktree so pre-commit review includes new files, modifications, and deletions instead of stale `HEAD` contents.
- Docs: README documentation links now include the shared Performance and Choosing a Language pages, and package registry metadata was expanded for discoverability. No functional change.

## [3.2.1] - 2026-07-29

- Release: Bumped .NET package metadata to `3.2.1`.
- Release: GitHub Release drafts now prepend this version's changelog section to generated notes and repair a missing section on workflow reruns.

### Changed

- Library: Profile/device catalog upper bounds no longer reject sends. The parser retains only supported-family, syntax, non-negative, text-representation, and command-count validation; its internal device table now stores number bases rather than profile limits.

### Fixed

- Library: Parse hexadecimal profile range endpoints such as `VB0-F9FF` without discarding valid leading hexadecimal letters.
- Library: Report device-span arithmetic overflow as the stable `HostLinkProtocolError` contract instead of leaking `OverflowException`.
- Library: Direct-bit numeric reads and writes pack/unpack the complete 16- or 32-bit token set, including bit-in-word access, instead of treating each response as one scalar token.
- Library: Consecutive RDS requests are split at the command-specific count limit, including TM and Z routes.
- Tests: Added permissive-address, direct-bit packing, bit-in-word preservation, and split-boundary coverage.

## [3.2.0] - 2026-07-17

- Release: Bumped .NET package metadata to `3.2.0`.
- CI: Excluded maintainer-only files, tests, and release tooling from generated source archives while retaining the complete sample set, and added source-archive contract checks to local, CI, and release gates.

- Library: Added immutable client-lifetime traffic snapshots through `TrafficStats` on direct and queued clients.
- Library: Made TCP receive-byte accounting independent of CR/LF segmentation by counting the response body and first terminator only; UDP datagram accounting is unchanged.

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
