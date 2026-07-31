# TODO: Host Link Communication .NET

Current active TODOs only.

## Current Status

The six approved implementation items are complete in the working tree. The
evidence-dependent comment-encoding decision remains open, and no
comment-decoder implementation change is authorized until `HL-EVAL-TODO-006`
is approved.

### Verification evidence — 2026-08-01

- Current-worktree CI passed 201 tests on each of .NET 8, .NET 9, and .NET 10,
  formatting, generated API reference, all six sample builds, and release-tool checks.
- A synthetic current-worktree Git tree produced a self-contained source
  archive; its clean extracted gate passed 603 test executions plus restore,
  build, format, documentation, all-sample, and package-content checks.
- The independent NuGet guard passed with the approved 12-file minimal package.
- Codex reviewed the actual diff, public API, validation and exception order,
  lifecycle races, tests, samples, generated docs, packaging, and cross-language contracts.
- These deterministic validation and packaging corrections do not require
  live PLC communication. `HL-EVAL-TODO-006` is intentionally still open.

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

Deferred by the user on 2026-08-01 for evidence investigation followed by implementation in the next Host Link implementation cycle. The current UTF-8-first/Shift_JIS-fallback behavior is not approved as the final contract. Do not change the decoder in the current implementation batch; investigate the exact profile-specific byte contract first, present the resulting target contract one item at a time, and implement only after explicit approval.

### Implementation scope

- .NET `RDC` device-comment decoding and ordinary client/extension APIs
- Cross-language comparison with the Python, Rust, and Node-RED Host Link implementations
- Shared Host Link user documentation where the resulting behavior is common

### Target state

The encoding of `RDC` device-comment response bytes is defined from direct KEYENCE Host Link evidence for every affected PLC profile. The .NET implementation does not infer a target contract merely from successful decoding, a general KV string-encoding statement, or existing UTF-8-first/Shift_JIS-fallback behavior.

Until the evidence is complete and the resulting target contract is explicitly approved, the comment-encoding behavior remains undecided and no implementation change is authorized.

### Compatibility impact

Undecided. The investigation must identify whether the approved result preserves the current UTF-8-first/Shift_JIS-fallback behavior, fixes one encoding, selects encoding by PLC profile, or introduces an explicit API setting. Any public API, default, decoding, error, or migration impact must be recorded before implementation.

### Acceptance criteria

1. Official KEYENCE communication documentation is checked for the `RDC` response encoding for KV-NANO, KV-3000/KV-5000, KV-7000/KV-8000, and KV-X500 families; evidence is recorded per profile rather than inferred across families.
2. The exact codec contract is identified, including whether “Shift_JIS” means strict Shift_JIS, Windows-31J/CP932-compatible decoding, or another defined mapping.
3. Ambiguous byte sequences that are valid under both UTF-8 and Shift_JIS are included in deterministic decoder vectors, and the expected result follows the approved evidence rather than decoder ordering.
4. If official documentation does not settle a profile, that profile remains `unverified` until an exact live-PLC evidence plan is written with the PLC/profile, endpoint, address, read intent, registered comment value, purpose, expected raw-byte evidence, and restoration requirement, then separately approved by the user with `OK` before communication.
5. A maintainer decision record defines the encoding selection mechanism, malformed-byte behavior, connection invalidation behavior, public API impact, compatibility impact, and cross-language mapping before source implementation begins.
6. User documentation, tests, generated API reference, and migration notes agree with the approved contract in every affected implementation.

### Evidence and completion checklist

- [ ] Official `RDC` encoding evidence recorded for every affected PLC family/profile.
- [ ] Shift_JIS versus Windows-31J/CP932 mapping resolved for all four language runtimes.
- [ ] Ambiguous and malformed byte vectors defined with evidence-backed expected results.
- [ ] Need for live PLC verification decided; any required exact live batch is separately documented and approved.
- [ ] Target contract and compatibility impact explicitly approved by the user.
- [ ] Implementation completed in every affected repository.
- [ ] Tests added or updated for every acceptance criterion.
- [ ] Relevant static checks, unit tests, integration tests, examples, and package/build checks passed.
- [ ] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [ ] Required live-PLC checks passed, or each unavailable check has an explicit release disposition.
- [ ] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [ ] Final acceptance criteria verified and the item marked complete.

### Current evidence boundary

The current implementations try UTF-8 first and fall back to Shift_JIS. KEYENCE material stating that KV-series strings use Shift_JIS is relevant but does not by itself establish the byte contract of every Host Link `RDC` response. It is supporting evidence only, not approval of a Shift_JIS-only implementation.

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
