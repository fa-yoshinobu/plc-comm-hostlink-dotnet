# TODO: Host Link Communication .NET

Current active TODOs only.

## Current Status

The six earlier approved implementation items are complete in the working tree.
`HL-EVAL-TODO-006` is approved and its .NET implementation is complete in this
working tree; family completion remains independent for each affected runtime.

### Verification evidence — 2026-08-01

- Current-worktree CI passed 221 tests on each of .NET 8, .NET 9, and .NET 10,
  formatting, generated API reference, all six sample builds, and release-tool checks.
- A synthetic current-worktree Git tree produced a self-contained source
  archive; its clean extracted gate passed 663 test executions plus restore,
  build, format, documentation, all-sample, and package-content checks.
- The independent NuGet guard passed with the approved 12-file minimal package
  and an isolated consumer that compiled the explicit codec/raw API.
- Codex reviewed the actual diff, public API, validation and exception order,
  lifecycle races, tests, samples, generated docs, packaging, and cross-language contracts.
- These deterministic validation and packaging corrections do not require
  live PLC communication. The separate comment-encoding evidence used to
  approve `HL-EVAL-TODO-006` is recorded below.

## HL-EVAL-001 — Reject Float32 writes to direct bit devices before transport

### Implementation scope

- .NET extension/high-level Float32 write planning in the ordinary FIFO client
- Every direct bit device family accepted by the address parser, including `Y`, `R`, `B`, `MR`, `LR`, `CR`, `VB`, `X`, `M`, and `L`

### Target contract

Float32 (`F`) writes are supported only for word devices. A direct bit target is rejected with the documented argument exception before frame construction or transport; the implementation must not reinterpret, split, retry, or send the Float32 bit pattern as consecutive bit writes.

### Compatibility impact

Calls that previously could emit unintended multi-bit writes now fail before communication. This is an intentional safety correction; no compatibility alias or fallback is retained.

### Acceptance criteria

1. `Y0:F` and `R0:F` writes fail with the documented argument exception before any transport call.
2. Every supported direct bit family follows the same rejection path, while valid word-device Float32 writes retain their defined two-word encoding.
3. Direct, queued, named, and extension write paths cannot bypass the validation.
4. Regression tests prove zero sends for rejected writes; live PLC writes are not required for this safety guard.

### Completion checklist

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Live-PLC verification is recorded as not required, or each required check has evidence or an explicit release disposition.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

## HL-EVAL-005 — Normalize banked bit ranges before calculating bounds and point counts

### Implementation scope

- .NET profile/device-range metadata for `R`, `MR`, `LR`, and `CR`
- Public lower-bound, upper-bound, point-count, and display-range properties

### Target contract

Banked bit addresses are parsed as a decimal bank plus a final bit field `00..15`, and their logical index is `bank * 16 + bit`. Numeric bounds and point counts use the logical index, while the public display range preserves PLC notation. Profile catalog ranges remain descriptive metadata and are not communication-library pre-send address guards.

### Compatibility impact

Incorrect numeric bounds and point counts change to their logical values. Display addresses remain in PLC notation, and no new transport-side range rejection is introduced.

### Acceptance criteria

1. All catalog rows for `R`, `MR`, `LR`, and `CR` produce logical lower/upper indices and exact point counts from `bank * 16 + bit`.
2. KV-8000 `R00000..R199915` reports 32,000 points and `MR00000..MR399915` reports 64,000 points.
3. Invalid final bit fields outside `00..15` are rejected by catalog parsing/tests.
4. Address-range display text remains unchanged and transport APIs do not enforce profile catalog bounds.
5. Equivalent vectors agree with the Python and Rust implementations.

### Completion checklist

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Live-PLC verification is recorded as not required, or each required check has evidence or an explicit release disposition.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

## HL-EVAL-TODO-006 — Determine the Host Link device-comment encoding contract

### User disposition

The target contract was approved by the user on 2026-08-01. An `RDC` comment encoding must not be fixed by the library or PLC profile and must not be guessed by UTF-8-first/Shift_JIS-fallback decoding. Text decoding requires an explicit caller-selected encoding, and exact raw comment payload bytes remain available. The user subsequently authorized the .NET implementation in this Host Link implementation cycle.

### Implementation scope

- .NET `RDC` device-comment decoding and ordinary client/extension APIs
- Cross-language comparison with the Python, Rust, and Node-RED Host Link implementations
- Shared Host Link user documentation where the resulting behavior is common

### Target state

An `RDC` response is first treated as an exact byte payload. A caller that requests text explicitly selects the supported encoding used for that decode. The .NET implementation performs no heuristic UTF-8-first fallback, PLC-profile selection, write-source inference, or silent replacement of malformed bytes. A public raw-byte path exposes the undecoded comment payload.

The public codec selection is `HostLinkCommentEncoding` with exactly `Utf8` and `Cp932`. `Utf8` means strict UTF-8. `Cp932` means strict Windows code page 932 / Windows-31J and is documented as the compatibility selection for KEYENCE material that calls the encoding "Shift_JIS". There is no separate strict-Shift_JIS, automatic, profile-derived, or fallback selection because the supported runtimes do not expose one consistent cross-language strict-Shift_JIS mapping distinct from CP932 that is justified by the available Host Link evidence.

The shared strict CP932 subset preserves ASCII `00..7F` byte-for-code-unit,
accepts halfwidth `A1..DF`, and accepts every assigned double-byte mapping shared
by Python CP932 and Node WHATWG Shift_JIS. It rejects singleton `80`, `A0`, and
`FD..FF`, incomplete sequences, invalid trails, and unassigned pairs. .NET's
ordinary exception-fallback decoder incorrectly rejects 398 mapped Windows
extension pairs, so .NET validates the shared assigned set before decoding with
the default CP932 mapping rather than treating those pairs as malformed.

In .NET, the existing plural `ReadCommentsAsync` name remains but its text
overload requires `HostLinkCommentEncoding`; there is no no-codec compatibility
overload. Singular `ReadCommentBytesAsync` returns the terminator-free exact
response body including trailing ASCII-space padding. The text path removes
only trailing ASCII `0x20` before strict decoding. No-codec `ReadNamedAsync` and
`PollAsync` remain available for non-comment aggregates, but reject any
`:COMMENT` item during complete preflight with zero sends; their explicit-codec
overloads require at least one comment entry. Supplying an explicit but unused
comment encoding to a non-comment or empty aggregate is an argument error during
complete preflight with zero sends.

### Compatibility impact

This is an intentional breaking change. Existing string APIs that silently try UTF-8 and then Shift_JIS must require an explicit encoding selection, while callers that cannot assert an encoding use the raw-byte API. Migration notes must identify the required selection and the removal of heuristic decoding.

### Acceptance criteria

1. Every public `RDC` text-decoding path requires an explicit supported encoding and has no automatic or profile-selected codec.
2. A public raw-byte path returns the undecoded `RDC` comment payload.
3. The exact codec mapping is defined consistently across all four runtimes, including whether Shift_JIS and Windows-31J/CP932 are separate selections.
4. Ambiguous byte sequences valid under multiple codecs decode only according to the caller's selection; malformed sequences fail without fallback or replacement.
5. Decoder failure and connection-state behavior are explicit and consistent with the library's protocol-error contract.
6. User documentation, tests, generated API reference, changelog, and migration notes agree with the approved contract in every affected implementation.

### Evidence and completion checklist

- [x] Evidence sufficient to reject a universal or profile-fixed `RDC` codec is recorded.
- [x] Shift_JIS versus Windows-31J/CP932 mapping resolved for all four language runtimes.
- [x] Ambiguous and malformed byte vectors defined with evidence-backed expected results.
- [x] Further profile-by-profile live verification is not required to select the explicit-codec/raw-byte contract.
- [x] Target contract and compatibility impact explicitly approved by the user.
- [x] Implementation completed in every affected repository.
  - [x] Host Link .NET implementation completed.
  - [x] Host Link Python implementation completed and independently evidenced.
  - [x] Host Link Rust implementation completed and independently evidenced.
  - [x] Host Link Node-RED implementation completed and independently evidenced.
- [x] Tests added or updated for every acceptance criterion.
  - [x] Host Link .NET tests added and passed for every .NET acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
  - [x] Host Link .NET CI, source archive, samples, docs, and package consumer passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
  - [x] Host Link .NET self-review completed; accepted findings were corrected and reverified.
- [x] Required live-PLC checks passed, or each unavailable check has an explicit release disposition.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
  - [x] Host Link .NET documentation, migration, changelog, XML, and generated API reference agree.
- [x] Final acceptance criteria verified and the item marked complete.
  - [x] Final Host Link .NET acceptance criteria and independent four-runtime family completion verified.

### Current evidence boundary

Before this implementation cycle, the reviewed implementations tried UTF-8 first and fell back to Shift_JIS. The located KEYENCE material says that KV-8000 strings use Shift_JIS in a specific EtherNet/IP connection-guide context, but it does not define the Host Link `RDC` response encoding: <https://www.keyence.co.jp/support/user/controls/plc/connection_guide/kv_iv4/>.

The deterministic vectors are `C2 A2`, which decodes as `¢` under strict UTF-8
and `ﾂ｢` under strict CP932; CP932 controls `1A`, `1C`, and `7F`; Windows
extension mappings `8790` → `U+2252`, `ED40` → `U+7E8A`, and `FA4A` →
`U+2160`; `EF BB BF 41`, which UTF-8 preserves as `U+FEFF` plus `A` and CP932
rejects; malformed UTF-8 `C2`; forbidden CP932 singletons `80`, `A0`, and
`FD..FF`; and malformed/unassigned CP932 `81`, `81 00`, `81 7F`, and `81 AD`.
Each malformed vector must raise `HostLinkProtocolError`, perform no replacement
or alternate-codec fallback, and retire the connection.

On 2026-08-01, after the user's explicit `OK`, a read-only live check used KEYENCE KV-X500 / `keyence:kv-x500` at `192.168.250.100:8501`. `RDC R000` returned `E38182E38184E38186E38188E3818A` (UTF-8 `あいうえお`) and `RDC R001` returned `E3818BE3818DE3818FE38191E38193` (UTF-8 `かきくけこ`). Both payloads fail strict Shift_JIS and CP932 decoding. This proves that a universal Shift_JIS assumption is unsafe; it does not prove that all `RDC` comments are UTF-8 or identify how the comment-writing path determines stored bytes. The approved explicit-selection/raw-byte contract therefore does not depend on resolving that mechanism.

## HL-EVAL-017 — Distinguish .NET internal timeouts from caller cancellation

### Implementation scope

- TCP and UDP connect, send, and receive operations
- Ordinary FIFO client exception propagation and connection invalidation

### Target contract

An internal library timeout throws a dedicated `HostLinkTimeoutError` derived from `HostLinkConnectionError`. `OperationCanceledException` is reserved for cancellation requested through the caller's token. Timeout handling is consistent for connect, send, and receive, invalidates the affected connection, and never retries automatically.

### Compatibility impact

Internal timeouts no longer appear as caller cancellation. Consumers may catch the dedicated timeout type or its `HostLinkConnectionError` base; migration notes must describe the distinction.

### Acceptance criteria

1. Internal TCP and UDP connect/send/receive timeouts throw `HostLinkTimeoutError` and leave the client disconnected.
2. A caller-cancelled token throws `OperationCanceledException`, not `HostLinkTimeoutError`, even when linked with the internal timeout mechanism.
3. Deterministic race tests cover caller cancellation before timeout, timeout before caller cancellation, and disposal/close interruption.
4. The ordinary FIFO client preserves the leaf exception contract and does not automatically reconnect or retry.

### Completion checklist

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Live-PLC verification is recorded as not required, or each required check has evidence or an explicit release disposition.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

## HL-EVAL-018 — Make .NET close and asynchronous disposal promptly interrupt I/O

### Implementation scope

- `Close`, `CloseAsync`, `Dispose`, and `DisposeAsync` in the ordinary FIFO client
- Active I/O, queued work, socket lifetime, gate acquisition, and error classification

### Target contract

Beginning close atomically blocks new work, invalidates the lifetime, cancels active I/O, and closes the socket before asynchronously waiting for gate/cleanup completion. Close and disposal are idempotent and safe under concurrent calls. A close-induced interruption is `HostLinkConnectionError`; caller cancellation remains `OperationCanceledException`; an internal timeout remains `HostLinkTimeoutError`. No operation is retried or reconnected automatically.

### Compatibility impact

Close/dispose no longer waits for the full configured response timeout before interrupting I/O. In-flight operations receive the documented connection/cancellation/timeout distinction.

### Acceptance criteria

1. Once close begins, new ordinary-client operations fail without socket send.
2. Active TCP and UDP reads are unblocked promptly by lifetime cancellation/socket close, after which `CloseAsync`/`DisposeAsync` await cleanup without synchronous gate blocking.
3. Concurrent and repeated close/dispose calls complete idempotently without double-dispose faults or orphaned work.
4. Deterministic tests distinguish close interruption, caller cancellation, and internal timeout in the ordinary FIFO client.
5. No queued request survives close into a later connection and no request is retried automatically.

### Completion checklist

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Live-PLC verification is recorded as not required, or each required check has evidence or an explicit release disposition.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

## HL-EVAL-019 — Return an actually immutable .NET profile-name list

### Implementation scope

- `KvHostLinkPlcProfiles.GetNames()` backing storage and any other public profile descriptor collection backed by shared mutable arrays

### Target contract

`GetNames()` keeps its `IReadOnlyList<string>` return type but exposes storage that cannot be cast or otherwise used to mutate library state. Names and ordering remain unchanged. The implementation may cache an immutable/read-only collection and must not expose the backing array or create unnecessary per-call copies. Other shared profile arrays receive the same mutability audit.

### Compatibility impact

Supported read-only enumeration is unchanged. Code that relied on casting and mutating internal arrays loses that unsupported capability.

### Acceptance criteria

1. A caller cannot cast the returned object to a mutable backing array or mutate later `GetNames()` results through any exposed collection interface.
2. Repeated calls return the same documented names and order without avoidable allocation when a safe cached object is suitable.
3. Tests attempt mutation through array casts and mutable collection interfaces and prove subsequent profile queries are unchanged.
4. Every other shared profile descriptor array exposed through a read-only type is audited and protected equivalently.

### Completion checklist

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Live-PLC verification is recorded as not required, or each required check has evidence or an explicit release disposition.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

## HL-EVAL-024 — Make the GitHub source archive self-contained for standard build and test commands

### Implementation scope

- Git attributes/archive rules, .NET test projects and fixtures, solution/project references, and source-archive release gate

### Target contract

The GitHub source archive includes the repository tests and all fixtures required by them. From a clean extracted archive, the documented standard solution build and test commands complete without references to intentionally omitted projects. NuGet packages remain minimal and follow their separate package-content contract.

### Compatibility impact

GitHub source archives become larger because test projects/assets are included; published NuGet package contents do not expand as a consequence.

### Acceptance criteria

1. An archive produced from repository HEAD contains every test project and fixture referenced by the solution or test code.
2. Restore, build, test, formatting/static checks, documentation generation, and package checks run from the extracted archive with the expected nonzero test set.
3. The release gate creates a fresh archive, extracts it, and verifies those commands without checkout-only files.
4. NuGet package-content checks independently enforce the approved minimal registry package.

### Completion checklist

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Live-PLC verification is recorded as not required, or each required check has evidence or an explicit release disposition.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.
