# HostLink .NET quality-overhaul migration

## Superseding decision: explicit word-bit write (2026-08-07)

The earlier removal decisions recorded below remain historical evidence but no
longer describe the target public surface. `WriteBitInWordAsync` is restored as
an explicit Boolean-only operation for every Host Link device family whose
canonical default representation and `WR` command both provide one complete
16-bit `.U` word. The selected device text remains unchanged across its one read
and one write; there is no alternate route, fallback, resend, or readback.

The full target contract and machine-verifiable acceptance criteria are
GOAL-BIT-002 in `D:\APP\cross_library_bit_write_contract_goal_20260807.md`.
The operation validates before FIFO admission, owns one FIFO turn, starts one
absolute deadline on activation, always sends the write after a successful
read, and remains explicitly non-PLC-atomic.

GOAL-HOSTLINK-EXPANSION-RMW-001 extends the same approved contract to the
existing URD/UWR route through `WriteBitInExpansionUnitBufferAsync`. The unit,
buffer address, and `.U` format remain immutable across both requests; ordinary
device and expansion-unit routes never fall back to one another.

Branch: `quality/2026-07-overhaul`
Scope: approved HostLink decisions D-052 through D-065
Status: the user ran the authorized HostLink Claude review outside Codex on 2026-07-12; .NET findings are corrected and recorded below, with family-level final acceptance still separate.

This record is maintainer-facing. Breaking changes are intentional where the former API hid connection, format, timing, or multi-request behavior.

## D-052

Scope: HostLink .NET constructors, connection options, factories, samples
Target contract: Port and TCP/UDP transport are required; missing or unknown values never fall back to 8501/TCP.
Compatibility impact: Calls that omitted port or transport must pass both explicitly.

Acceptance criteria:

1. Constructor/options signatures have no port or transport default.
2. TCP and UDP explicit values succeed; invalid enum and port values fail before transport creation.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-053

Scope: HostLink .NET timeout configuration and all network operations
Target contract: Timeout omission resolves to three seconds; explicit values must be 1 through `Int32.MaxValue` milliseconds and are propagated to connect/send/receive.
Compatibility impact: Sub-millisecond, zero, negative, and over-range values fail before transport creation.

Acceptance criteria:

1. Omitted timeout equals three seconds.
2. Values below 1 millisecond or above `Int32.MaxValue` milliseconds fail before I/O.
3. Timeout/cancellation invalidates the transport and does not enable lazy reconnect.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-054

Scope: Connection options, client properties, frame builder, samples and docs
Target contract: Normal command frames always end in CR; no LF toggle remains public.
Compatibility impact: AppendLfOnSend callers must remove the option and use CR framing.

Acceptance criteria:

1. No public AppendLfOnSend member or constructor field exists.
2. Sent command fixtures end in exactly 0x0D.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-055

Scope: TCP/UDP receive implementation
Target contract: Receive chunking is internal; response body absolute cap is 65,536 bytes and overflow invalidates transport.
Compatibility impact: No public buffer tuning is introduced.

Acceptance criteria:

1. Body at the accepted boundary is supported.
2. One byte over cap and unterminated/partial frames fail and invalidate transport.
3. Response token counts are validated when command expectations are known.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-056

Scope: Maintainer raw-frame tracing
Target contract: Trace is disabled by default; when enabled it observes send/receive frames once and hook failure cannot change command behavior.
Compatibility impact: Trace remains a diagnostic surface, not a normal user option.

Acceptance criteria:

1. No hook produces no output.
2. Enabled hook receives direction and exact bytes once per frame.
3. Hook exceptions do not trigger failure or retry.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-057

Scope: Python-only auto_connect decision
Target contract: Not applicable to .NET: .NET has no auto_connect argument. Constructor network-I/O prohibition is verified under D-058.
Compatibility impact: No .NET compatibility shim is added.

Acceptance criteria:

1. Public .NET constructors contain no auto_connect argument.
2. Construction performs validation/local state only.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-058

Scope: Direct client, queued client, factory, and all commands
Target contract: Only explicit OpenAsync/Open or the explicitly named connected factory may create transport. Unconnected commands return HostLinkNotConnectedError.
Compatibility impact: Lazy-command connection users must open explicitly before the first command and after failure.

Acceptance criteria:

1. Unconnected raw/read/write fails without DNS/socket/send.
2. Transport failure closes state; next command remains disconnected.
3. Explicit reopen permits later commands without retrying the failed command.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-059

Scope: SetTimeAsync on direct and queued clients
Target contract: The DateTime value is required; no host-current-time substitution occurs.
Compatibility impact: Parameterless/nullable calls no longer compile.

Acceptance criteria:

1. The public parameter is required and non-nullable.
2. The year is 2000 through 2099 and the emitted weekday is derived consistently from the supplied DateTime.
3. No current-clock access occurs inside SetTimeAsync.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-060

Scope: Maintainer SendRawAsync and semantic commands
Target contract: Raw returns terminator-free byte[] without decoding or PLC error translation; semantic APIs use private command decoders.
Compatibility impact: Raw string-return consumers must decode bytes explicitly.

Acceptance criteria:

1. ASCII, PLC error, empty, and non-ASCII bodies are preserved by raw.
2. CR/LF/CRLF terminators are excluded from returned body.
3. Semantic PLC errors and malformed text are handled only by semantic paths.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-061

Scope: Historical direct/queued `ReadCommentsAsync` padding contract. The later
codec/API contract is governed by `HL-EVAL-TODO-006`.
Target contract: No padding option remains; only trailing ASCII 0x20 bytes are
removed before explicitly selected strict comment decoding. Exact padding is
available through the public raw-byte method.
Compatibility impact: Text callers do not retain padding. Callers requiring the
exact payload use `ReadCommentBytesAsync`.

Acceptance criteria:

1. Trailing ASCII spaces are removed.
2. Tabs, full-width spaces, Unicode whitespace, and embedded spaces are preserved.
3. Invalid data under the explicitly selected UTF-8 or CP932 codec produces a
   protocol error rather than replacement text or codec fallback.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-062

Scope: Expansion-unit URD/UWR APIs and wrappers
Target contract: Data format is required and limited to U/S/D/L/H; width, count, span, tokens, and values are validated.
Compatibility impact: Calls relying on implicit .U must pass .U.

Acceptance criteria:

1. Missing/empty/unknown format fails before send.
2. All five formats enforce numeric bounds and response tokens.
3. D/L consume two buffer words and use the 500-value limit.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-063

Scope: Word/Dword helper APIs and documentation
Target contract: All Chunked APIs are removed; word and native-Dword helpers send at most one request.
Compatibility impact: Chunked callers must implement an application loop and own timing/partial-success policy.

Acceptance criteria:

1. No public method name contains Chunked.
2. Word count above 1000 and Dword count above 500 fail before send.
3. One helper invocation sends at most one command.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-064

Scope: Low-level numeric, monitor-word, legacy, set-value, and high-level address paths
Target contract: Numeric low-level access takes base device plus required format; suffix-bearing device strings fail. Direct bit stays format-free. Named .D remains bit 13 and :D remains Dword.
Compatibility impact: Suffix-bearing low-level calls must separate device and format.

Acceptance criteria:

1. Missing/empty format and suffix-bearing low-level device fail before send.
2. Numeric write inputs reject bool/string/fraction/range overflow instead of conversion.
3. Direct BIT and named DM100.D/DM100:D meanings remain distinct; obsolete public parser/format-inference helpers are absent.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## D-065

Scope: All asynchronous client, queued, factory, and extension APIs
Target contract: CancellationToken remains optional and propagates through queue wait, connect, send, and receive; network timeout remains independent.
Compatibility impact: No migration required for omitted cancellation tokens.

Acceptance criteria:

1. Omitted token still uses the three-second network timeout.
2. Pre-cancel and queue-wait cancellation stop the operation.
3. Cancellation during transport invalidates the transport and never reuses delayed/partial response.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## DN-HL-CLAUDE-20260712 — Independent-review corrections

Scope: Claude HostLink findings 6, 7, 10, 17, and 21 for the .NET
repository.

Target contract: every timeout is representable by the underlying cancellation
timer before any transport object is created; clock years cannot silently wrap
centuries; removed D-064 parsing/default-format behavior is not public; and
cross-language vectors are owned only by the separate cross-verification
repository.

Compatibility impact: timeout values below 1 ms or above `Int32.MaxValue` ms,
clock years outside 2000..2099, `KvHostLinkDevice.ParseDeviceText`, and public
`ResolveEffectiveFormat` calls are rejected or no longer compile.

Acceptance criteria:

1. Client and connection-options paths accept exactly 1 ms and
   `Int32.MaxValue` ms and reject both adjacent out-of-range classes before
   transport creation.
2. `SetTimeAsync` accepts 2000..2099 and rejects 1999/2100 before sending.
3. Reflection finds no public `ParseDeviceText` or `ResolveEffectiveFormat`,
   while internal logical parsing continues to work.
4. Generated API reference, user guide, changelog, and migration describe the
   corrected surface and no library-local cross-vector runner/data remains.
5. Single-device reads derive response counts from the issued command.
   Direct-bit numeric reads accept exactly one packed scalar token whose
   `.U`/`.S`/`.H` view spans 16 bits and whose `.D`/`.L` view spans 32 bits;
   direct BIT accepts only `0`/`1`/`ON`/`OFF`. Malformed response shapes
   invalidate the session. This supersedes the former 16/32-token assumption
   using the KV-X500 live response vectors recorded by `LIVE-HL-001`.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Full build, multi-target 158-test runs, format, generated-doc, sample, and package checks passed.
- [x] Codex self-review completed against the corrected contract.
- [x] Claude source review completed; the user ran the authorized batch and its result is preserved in the workspace.
- [x] Codex dispositioned all .NET findings and reran affected checks.
- [x] No additional live-PLC check is required for timeout representation, pre-send year validation, API surface, and test ownership.
- [x] Documentation and migration notes agree with the implementation.
- [x] Final acceptance criteria verified for this repository; HostLink family-level acceptance remains separate.

## LIVE-HL-003 — Timer/counter structural status is not numeric data

Implementation scope: low-level and high-level formatted `RD` reads for timer and counter
devices, shared response validation/normalization, deterministic transport-retirement tests,
and the generated/user-facing contract. Public signatures and request frames are unchanged.

Target contract: a timer/counter response contains exactly three fields. The first is a
structural status field and must be the exact raw token `0` or `1`; it is validated before and
excluded from `.U`, `.S`, `.H`, `.D`, and `.L` parsing or normalization. Only current and preset
use the selected numeric format and its bounds. Hexadecimal current/preset values are normalized
to four uppercase digits. Any non-exact status, wrong token count, invalid data token, or numeric
overflow is a protocol error and retires the supplying transport.

Compatibility impact: high-level return types and all public signatures remain unchanged. The
low-level formatted `ReadAsync` result intentionally changes only its first timer/counter token:
`.H` no longer synthesizes `0000` or `0001` and instead exposes the PLC-semantic `0` or `1`.
Reliance on the erroneous representation is not preserved. Current and preset representations
remain governed by the requested format.

Acceptance criteria:

1. Timer and counter status is validated as exact raw `0` or `1` before numeric parsing.
2. `.U`, `.S`, `.H`, `.D`, and `.L` apply only to current and preset, with status unchanged.
3. Short `.H` current/preset values are padded to four uppercase digits without padding status.
4. Missing/extra tokens, invalid current/preset data, each format's overflow, and non-exact status
   produce `HostLinkProtocolError` and retire the transport.
5. The live response vector `0,270F,270F` is accepted by low-level and high-level reads.
6. User guide, changelog, XML documentation, generated API reference, and implementation agree.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit/integration tests, examples, package/build checks, and generated-document freshness passed.
- [x] Codex self-review completed against validation order, error behavior, transport retirement, public API, compatibility, and cross-language contract.
- [x] The separately approved .NET representative live row passed after the local correction and its evidence is preserved in the workspace.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified for the .NET implementation.

Acceptance evidence reverified on 2026-08-02:

- The focused timer/counter suite passed 39 tests on each of .NET 8, .NET 9, and .NET 10.
- `run_ci.bat` passed all 13 gates with 287 tests on each target framework, zero
  failures/skips, generated API freshness, format, every sample, and isolated NuGet consumer checks.
- Direct deterministic cases cover both exact status values for every numeric format, short
  hexadecimal padding, missing and extra fields, invalid current and preset data, every format's
  overflow, non-exact status spellings, the live `0,270F,270F` vector, and transport retirement.
- The retained live result
  `D:\APP\live-kvx500-20260802\dotnet_hl_kvx500_01_result.json` records
  `status=pass`, `writes=false`, start `2026-08-02T11:11:30.7622588+00:00`,
  finish `2026-08-02T11:11:30.8749583+00:00`, repository HEAD
  `1f3d36638c1ed9877a4e73bfa775a68df30e8e63`, and working-tree diff SHA-256
  `5BA4A835D39E17592C910BB5859E4CF93D360B03349B4AC454564CD1342C70CD`.
- The single client completed all 12 requests without a connection error (`163`
  transmitted and `139` received bytes). `R000.H` returned `0000`; `T0.H`
  returned `[0, "270C", "270F"]`; direct reads returned
  `[0, 0, "0000", 0, 0, 13]`; and the wire-preserved MWR fields
  `["00000", "+00000", "0000", "0000000000", "+0000000000", "00013"]`
  were semantically equivalent, including the final packed value `13`.
- Accepted findings are corrected. Rejected, duplicate, deferred, and new live-PLC findings are none.

## Batch evidence

- Baseline before overhaul: `run_ci.bat` passed with 164 tests on each of net8.0, net9.0, and net10.0.
- Final executable verification: `release_check.bat` passed on 2026-07-11.
  - NuGet registry guard confirmed `PlcComm.KvHostLink` 3.1.0 is not published.
  - Canonical HostLink profile fixture refresh reported no change.
  - Library and tests built without warnings for net8.0, net9.0, and net10.0.
  - 158 tests passed on each target framework; zero failures and zero skips.
  - API-reference generation, format, high-level XML docs, sample inventory, and release identity guards passed.
  - High-level, basic read/write, and named-polling user samples built successfully.
  - NuGet and symbol packages were generated successfully for 3.1.0.
- Sample configuration verification:
  - Multi-PLC and JSON configuration samples accepted explicit host/profile/port/transport in `--dry-run` mode.
  - A Multi-PLC specification omitting port and transport was rejected before any communication.
- Package inspection: the NuGet package contains README, LICENSE, and DLL/XML pairs for net8.0, net9.0, and net10.0.
- Codex self-review inspected the actual diff and found three correctness gaps, all corrected and reverified:
  - response validation for mode, legacy read, expansion read, and monitor state now completes inside the request lock;
  - monitor registration is cleared whenever transport state is closed or invalidated;
  - the public base-address parser now rejects suffix-bearing input rather than silently discarding the suffix in logical paths.
- Live-PLC disposition by decision:

| Decisions | Disposition | Rationale |
|---|---|---|
| D-052–D-054, D-057–D-059, D-063–D-065 | No live PLC required for this batch | These are public-signature, validation, explicit lifecycle, cancellation, framing-output, and single-request policy contracts verified before I/O or with deterministic loopback transport. |
| D-055, D-056, D-060, D-061 | No live PLC required for this batch | Receive limits, exact raw bytes, terminator removal, trace isolation, semantic separation, and decoder behavior are transport/decoder contracts fully exercised by raw TCP/UDP loopback tests. |
| D-062 | No live PLC required for this batch | The change makes the existing URD/UWR format explicit and validates frames, limits, values, spans, and response tokens. PLC/profile support evidence remains governed by the separate profile verification plan and was not inferred here. |

- Claude: the user ran the authorized HostLink batch outside Codex; its result and Codex disposition are preserved in the workspace review record.
- Family-level final acceptance is preserved in the archived workspace record `hostlink_cross_implementation_final_comparison_20260712.md`.

## 2026-07-12 KV-X500 live smoke evidence

- [x] The public factory and typed-read API connected to `keyence:kv-x500` at `192.168.250.100:8501` over TCP and read `DM0:U` once; the result was `5878`.
- [x] No write, retry, or profile／transport fallback was performed.
- [x] The temporary read-only project and generated build artifacts were removed immediately after the test.
- [x] This evidence is limited to that endpoint, profile, device, transport, and operation; it does not verify other device families or the complete profile.

## NR-007: Lifetime traffic statistics

Approved next-release contract: `TrafficStats` returns immutable lifetime counters; only complete
sends and complete response frames/datagrams count, pre-send and partial failures do not, and
close/reconnect does not reset. Implementation and deterministic tests are required; live PLC
verification is unnecessary. Final packaging and publication acceptance completed with `v3.2.0`.

- [x] Public API and transport-boundary implementation completed.
- [x] Deterministic tests, documentation, changelog, and package gate completed.
- [x] Codex final self-review completed.
- [x] Next-release package acceptance completed. Evidence: the `v3.2.0` tag equals repository HEAD,
  the GitHub Release and NuGet `PlcComm.KvHostLink` `3.2.0` package are public, tag-commit checks
  passed, and the final six-runtime family source/API comparison was completed on 2026-07-18.

## QREV-20260714-004: Segmentation-independent TCP receive accounting

Scope: direct and queued TCP receive framing and `HostLinkTrafficStats.RxBytes`.

Family equivalence: all four HostLink implementations count TCP `OK\r`, `OK\n`, coalesced `OK\r\n`, and either split CR/LF ordering as 3 bytes; UDP `OK\r\n` remains 4 bytes. Incomplete oversize/EOF/timeout/cancellation data contributes zero, while a complete PLC error line is counted before semantic decoding. The family comparison is preserved in the archived workspace record `communication_library_quality_review_20260714.md`.

Target contract: one completed TCP response counts its body through the first CR or LF. Additional
CR/LF separator bytes are consumed without changing the counter, whether they arrive together or
in a later TCP read. UDP continues to count the complete accepted response datagram.

Compatibility impact: a coalesced CRLF response previously could count both terminators and now
counts only the first; split CRLF already counted one. The corrected value is independent of TCP chunking.

Acceptance criteria:

1. Equivalent CRLF responses produce the same `RxBytes` when CR and LF are coalesced or split.
2. The separator left after a completed line cannot become an empty or misassociated next response.
3. Complete PLC errors are counted; incomplete oversize, EOF, and timeout paths are not counted. Complete UDP datagram accounting is unchanged.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Profile drift, build, 164 tests on each of net8.0/net9.0/net10.0, format, generated-doc, samples, and package checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Claude source review completed; findings are preserved in the archived workspace record `claude_review_findings_20260714.md`.
- [x] Codex resolved or dispositioned every applicable Claude finding and reran affected checks.
- [x] Live PLC verification is not required for this deterministic local framing and counter contract.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.

## 2026-08 Host Link evaluation corrections

The approved GOAL records and machine-verifiable acceptance criteria are kept
in `TODO.md` under the stable identifiers below. These are intentional contract
corrections; no compatibility aliases or fallback behavior are retained.

### HL-EVAL-001: Float32 direct-bit writes

Scope: `WriteTypedAsync` on direct and queued clients.

Target contract: Float32 writes require a word device and every direct bit
family is rejected before frame construction or transport I/O.

Compatibility impact and migration: code that passed `F` with `R`, `B`, `MR`,
`LR`, `CR`, `VB`, `X`, `Y`, `M`, or `L` must choose a word device or a bit data
type. The former consecutive-bit emission is intentionally unavailable.

### HL-EVAL-005: Banked bit range metadata

Scope: numeric bounds and point counts for `R`, `MR`, `LR`, and `CR` profile rows.

Target contract: the decimal bank and final `00..15` bit field map to
`bank * 16 + bit`; display strings remain in PLC notation and are not transport
guards.

Compatibility impact and migration: consumers that cached the former decimal
interpretation must refresh catalog-derived bounds and counts. Display values
and communication behavior do not change.

### HL-EVAL-017: Timeout exception identity

Scope: direct and queued TCP/UDP connect, send, and receive operations.

Target contract: an internal timeout throws `HostLinkTimeoutError` (derived
from `HostLinkConnectionError`); only the caller's token produces
`OperationCanceledException`. The failed connection is invalidated without
retry.

Compatibility impact and migration: cancellation handling that treated every
`OperationCanceledException` as a timeout must catch `HostLinkTimeoutError`
instead. Existing broad `HostLinkConnectionError` handling continues to catch
the new timeout leaf.

### HL-EVAL-018: Prompt close and disposal

Scope: direct and queued `Close`, `CloseAsync`, `Dispose`, and `DisposeAsync`.

Target contract: close begins by blocking new work, cancelling the connection
lifetime, and closing the socket before awaiting gate cleanup. Old queued work
is never sent on a reopened connection.

Compatibility impact and migration: in-flight work now fails promptly with
`HostLinkConnectionError` instead of waiting for the configured response
timeout. Callers must explicitly reopen and decide whether a new operation is
appropriate; the library never retries it.

### HL-EVAL-019: Immutable profile collections

Scope: `KvHostLinkPlcProfiles.GetNames()` and
`GetProfileDescriptors()` shared backing storage.

Target contract: cached read-only collection objects preserve names and order
without exposing a mutable backing array or mutable collection operations.

Compatibility impact and migration: supported enumeration is unchanged. Code
that cast the returned object and mutated library state must copy it into its
own collection before making application-local changes.

### HL-EVAL-024: GitHub source archive and NuGet separation

Scope: Git attributes, archive validation, test fixtures, documentation tools,
sample builds, and NuGet package-content validation.

Target contract: GitHub source archives are self-contained for restore, build,
nonzero tests, format, documentation, all samples, and package checks. NuGet
packages independently remain limited to assemblies/XML docs, package
metadata, README, and license.

Compatibility impact and migration: source archives are larger because tests
and their required validation scripts are included. NuGet consumers receive no
additional repository-only content.

## GOAL-HL-SERIAL-DEFER-001: Single-request capacity contract

Implementation scope: every public Host Link read/write command and high-level
single-request helper in this repository.

Target contract: an API classified as one protocol request emits at most one
request and rejects a count or logical value that cannot fit before transport.
No single-request write or read silently splits.

Compatibility impact: callers must issue explicitly separate operations when
they accept separate timing or partial-completion boundaries.

Acceptance criteria:

1. Generated API documentation classifies all communication methods.
2. Exact-limit and over-limit tests prove one request or zero requests.
3. No write helper silently compiles to multiple requests.

- [x] Implementation completed in this repository.
- [x] Tests cover the applicable capacity and pre-transport boundaries.
- [x] Static, unit, build, sample, package, and source-archive checks passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC verification is not required; request counts and rejection are deterministic locally.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## REAUDIT-001 — TCP ownership acceptance revalidation

Implementation scope: the existing persistent TCP ownership contract, its deterministic transport
tests, and the user-facing residual-risk explanation. Runtime code and the public API are unchanged.

Accepted self-review finding: the implementation and tests retired observable stale, additional,
timed-out, cancelled, malformed, and failed TCP generations, but the user guide did not state that
Host Link TCP has no request identifier. It also omitted the narrow residual race after the pre-send
input check and the reason a one-request-per-connection policy was rejected. The user guide now
records that such a policy would repeat connection setup and teardown latency without creating a
request identifier; healthy connections remain serialized and reusable.

Acceptance evidence:

- [x] `TcpTrafficStatsAreIndependentOfCrLfSegmentation` proves two normal commands use one accepted TCP stream.
- [x] `NormalClientIsFifoAndQueueWaitDoesNotConsumeTransactionTimeout` proves one active request per client.
- [x] `TcpRejectsDelayedUnownedResponseBeforeSendingNextCommand` proves observable pre-send data causes zero later sends and retires the stream.
- [x] Extra-response, EOF, timeout, cancellation, malformed-response, outcome-unknown, and monitor reconnect tests cover the remaining REAUDIT-001 lifecycle criteria.
- [x] `run_ci.bat` passed all 13 gates with 254 tests on each of .NET 8, .NET 9, and .NET 10, all samples, format/API checks, package construction, and isolated consumer validation.
- [x] Live PLC verification is not required: response ownership, connection reuse, FIFO admission, monitor reset, and anomaly retirement are deterministic local transport/lifecycle behavior and no PLC capability claim changed.
- [x] The user guide, changelog, maintainer record, generated API reference, and implementation now agree; accepted findings are corrected and no rejected, duplicate, or deferred finding remains.

## REAUDIT-005 — Empty raw cross-language acceptance

Implementation scope: the existing .NET raw-frame preflight and new public API regression evidence.
The runtime and public API are unchanged.

Accepted self-review finding: `KvHostLinkProtocol.BuildFrame` already rejected an empty body and
`SendRawAsync` builds the frame before FIFO admission, but the cross-language acceptance suite did
not directly prove this through the public .NET API. TCP and UDP tests now use an unresolved host,
invoke `SendRawAsync("")`, require `HostLinkProtocolError`, and prove the client remains closed with
zero traffic and zero trace activity.

- [x] Public raw empty input is rejected before FIFO, connection state, DNS, socket creation, connect, or send for TCP and UDP.
- [x] The user guide and changelog state the non-empty raw contract and pre-transport boundary.
- [x] The targeted test passed on .NET 8, .NET 9, and .NET 10.
- [x] The final `run_ci.bat`, `git diff --check`, and Codex diff review passed after this correction.
- [x] Live PLC verification is not required because the failure is deterministic before network or protocol traffic.
- [x] Accepted findings are fixed; rejected, duplicate, and deferred findings are none.

## GOAL-HL-SERIAL-DEFER-002: One absolute active-transaction deadline

Implementation scope: TCP/UDP connect and each admitted Host Link exchange.
Serial-port configuration is not applicable because this implementation has no
serial transport.

Target contract: one timeout snapshot is taken at FIFO admission and one
monotonic .NET cancellation deadline is armed immediately before the first
transport attempt after activation. It spans resolution/connect or the complete send-through-response exchange,
does not restart on partial progress, invalidates failed transport state, and
never causes an automatic retry. FIFO waiting consumes none of the deadline.

Compatibility impact: trickled progress cannot extend an operation indefinitely;
queued work receives its complete active timeout budget.

Acceptance criteria:

1. A queued operation can wait longer than its timeout and still succeed when its active exchange fits.
2. TCP and UDP timeout tests close the failed transport without retry.
3. Read and state-changing timeout identities follow GOAL-HL-ERROR-DEFER-001.

- [x] Implementation completed in this repository.
- [x] Deterministic deadline and queue-wait tests added.
- [x] Static, unit, build, sample, package, and source-archive checks passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC verification is not required; deadline behavior is locally deterministic.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## GOAL-HL-SERIAL-DEFER-006: FIFO and close-generation isolation

Implementation scope: `KvHostLinkClient`, factory entry points, high-level
extensions, examples, and removal of `QueuedKvHostLinkClient`.

Target contract: the ordinary client admits operations in exact arrival order,
runs one active transaction, snapshots request inputs and timeout configuration
at admission, cancels a waiting caller without sending, rejects same-client
recursive entry, and allows separate clients to progress independently. Close
retires active and queued work from the old generation; reopening accepts only
fresh work and cannot associate an old response with a new operation.

Compatibility impact: `QueuedKvHostLinkClient`, its overloads, and its custom
execution escape hatch are removed. Callers use `KvHostLinkClient` directly.

Acceptance criteria:

1. FIFO wire order is deterministic under concurrent admission.
2. Waiting cancellation sends nothing and later work can continue.
3. Close rejects active/queued old-generation work and fresh post-reopen work succeeds.
4. Recursive callbacks fail with `HostLinkReentrancyError`; other clients remain independent.

- [x] Implementation completed in this repository.
- [x] Lifecycle, FIFO, cancellation, reentrancy, snapshot, and parallel-instance tests added.
- [x] Static, unit, build, sample, package, and source-archive checks passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC verification is not required; queue and lifecycle behavior is locally deterministic.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## GOAL-HL-ERROR-DEFER-001: Stable known and unknown outcome errors

Implementation scope: every Host Link communication operation and raw
maintainer command.

Target contract: timeout, caller cancellation, explicit close, not-connected,
transport failure, malformed protocol response, and known PLC rejection remain
distinguishable. When state-changing transmission may have begun without a
definitive result, `HostLinkOutcomeUnknownError` carries a structured `Reason`
and original cause. Such operations are never retried automatically. Raw
commands are conservatively treated as potentially state-changing.

Compatibility impact: post-send failures from writes/raw commands now use the
outcome-unknown wrapper. Consumers must inspect the structured reason and must
not treat timeout/cancellation as proof that the PLC did not execute the command.

Acceptance criteria:

1. Dedicated timeout, close, not-connected, reentrancy, and outcome-unknown types are public.
2. Read timeout remains `HostLinkTimeoutError`; write timeout is outcome-unknown with a timeout cause.
3. Caller cancellation, close, transport, and invalid-response outcome reasons are tested.
4. Known PLC error responses are not mislabeled as outcome-unknown.

- [x] Implementation completed in this repository.
- [x] Deterministic error-identity and cause tests added.
- [x] Static, unit, build, sample, package, and source-archive checks passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC verification is not required; error classification is deterministic locally.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## GOAL-HL-AGGREGATE-DEFER-001: Safe read-only aggregate splitting

Historical note: PERF-001 supersedes this record's declared wire-order target. Complete preflight,
input-order results, indivisible values, one FIFO turn, non-atomicity, and no-partial-result behavior
remain in force.

Implementation scope: `ReadNamedAsync`, each `PollAsync` cycle, and removal of
the multi-request state-changing `WriteBitInWordAsync` helper.

Target contract: a read-only aggregate copies and validates its complete plan
before transport, preserves declared input order in internal wire requests and
result mapping, splits only between independent entries, keeps each multiword
value wholly inside one request, retains one FIFO turn, stops at the first
failure, and exposes no partial result. It is explicitly non-atomic. A public
state-changing operation that would need multiple requests is rejected or
removed; one-request writes remain available.

Compatibility impact: named aggregates no longer sort wire reads by address.
`WriteBitInWordAsync` is removed; callers may write an owned complete word in
one request after choosing their application-level concurrency contract.

Acceptance criteria:

1. Invalid or duplicate later entries produce zero wire requests.
2. Discontiguous/reordered input produces wire reads in declared order.
3. A DWord at the 1,000-word boundary remains a two-word request and is never torn.
4. A later same-client operation cannot interleave and intermediate failure returns no dictionary.
5. Documentation directs coherent readers to a single request or PLC-side snapshot/handshake.

- [x] Implementation completed in this repository.
- [x] Preflight, order, boundary, no-interleaving, failure, and existing capacity tests cover the contract.
- [x] Static, unit, build, sample, package, and source-archive checks passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC verification is not required; planner/request ordering is deterministic locally.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## GOAL-HL-BIT-BOOL-AUDIT-001: Boolean-only individual bit writes

Implementation scope: low-level scalar/consecutive direct-bit writes and
`WriteTypedAsync(..., "BIT", ...)`. The originating SLMP record did not list
Host Link, but this repository was explicitly audited for cross-library
consistency.

Target contract: individual bit values enter public Host Link APIs only as
`bool`/`IEnumerable<bool>`. Numeric `0` and `1` are not compatibility inputs.
Packed numeric reads remain a distinct bit-bank read representation and are not
individual bit-write inputs.

Compatibility impact: callers passing numeric direct-bit write values must
convert application data explicitly to `bool`.

Acceptance criteria:

1. Boolean scalar and collection writes emit the exact `1`/`0` wire tokens.
2. Numeric scalar, collection, and typed BIT inputs fail before transport.
3. No truthy or numeric compatibility overload is public.

- [x] Implementation and tests completed in this repository.
- [x] Static, unit, build, sample, package, and source-archive checks passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC verification is not required; input type and pre-transport behavior are deterministic.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## GOAL-HL-IPV4-AUDIT-001: IPv4-only endpoints

Implementation scope: constructor, connection options, factory, TCP connect,
UDP connect, hostname resolution, samples, and documentation.

Target contract: IPv4 literals and hostnames with an IPv4 result are supported.
IPv6 literals are rejected before socket creation, hostname resolution selects
only IPv4, and no fallback or IPv6 UDP bind is permitted.

Compatibility impact: IPv6 endpoints must migrate to the PLC's IPv4 endpoint.

Acceptance criteria:

1. Plain, bracketed, and mapped IPv6 literals fail before transport.
2. TCP and UDP sockets use `AddressFamily.InterNetwork`.
3. Hostname resolution returns only an IPv4 result or a connection error.

- [x] Implementation and deterministic tests completed in this repository.
- [x] Static, unit, build, sample, package, and source-archive checks passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC verification is not required; address-family selection is deterministic locally.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## GOAL-SLMP-DEFER-005 Host Link .NET applicability record

Implementation scope: audit of all public live Host Link .NET methods and
profile-accepting offline device-range catalog methods.

Target contract: a live method with an explicit/profile-bound selector must
match the connection's exact canonical profile before transport; redundant
profile overrides are not added. Offline catalog lookup remains transport-free
and is named/documented as offline behavior.

Compatibility impact: not applicable. Every live method already derives its
only profile from immutable `KvHostLinkClient.PlcProfile`; no live method accepts
a second profile-bearing value. `DeviceRangeCatalogForPlcProfile` is offline.

Acceptance criteria:

1. Reflection/source audit finds no live per-call profile parameter or profile-bound address type.
2. Client profile is immutable and canonicalized at construction.
3. Offline catalog APIs perform no communication and remain clearly separate.

- [x] Applicability audit completed with a machine-checkable not-applicable rationale.
- [x] No runtime implementation change was required or added.
- [x] Existing canonical-profile and offline catalog tests cover the applicable behavior.
- [x] Static, unit, build, sample, package, and source-archive checks passed.
- [x] Codex self-review completed against the approved contract.
- [x] Live PLC verification is not required; this is an API/profile-flow audit.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## 2026-08-01 overhaul verification evidence

- `run_ci.bat`: passed after the final implementation state, including build,
  API-generator helper tests and freshness, 221 tests on each of net8.0,
  net9.0, and net10.0, formatting, all six solution samples plus the two
  separately restored sample projects, high-level XML docs, sample inventory,
  release-workflow guards, and NuGet package inspection.
- NuGet inspection: `PlcComm.KvHostLink.3.2.1.nupkg` contained 12 allowed
  consumer files and no repository tests, samples, scripts, maintainer docs, or
  source inputs. No registry publication was performed.
- Synthetic working-tree source archive: 74 files, all tracked tests and 15
  sample files present; extracted restore/build/format/API/docs/sample/package
  gates passed and 663 test results passed across the three target frameworks.
- Generated API reference: regenerated from 33 public types; the removed queued
  wrapper and multi-request write helper are absent, and the global
  single-request/aggregate classification is present.
- Codex self-review inspected the actual public surface, diff, validation order,
  FIFO state transitions, cancellation and close races, timeout start and decode
  coverage, transport retirement, error causes, aggregate request ordering,
  multiword boundaries, examples, docs, packaging, and profile flow. Accepted
  findings fixed before final verification were pre-send cancellation
  translation, response-decode deadline coverage, cancelled-waiter registration
  cleanup, remaining queued terminology, and user/sample wording that still
  called potentially multi-request aggregate results snapshots. No finding was rejected or
  left deferred.
- Live PLC disposition: not required for these items. They change deterministic
  client admission, validation, socket address-family policy, local error
  classification, and planner behavior without making a new PLC capability
  claim. The device-comment encoding decision was subsequently approved and is
  tracked independently as `HL-EVAL-TODO-006` below.

## HL-EVAL-TODO-006 — Explicit device-comment encoding and raw payload

Implementation scope: .NET `RDC` handling in `KvHostLinkClient`, comment entries
in `ReadNamedAsync` and `PollAsync`, public XML/API documentation, tests, and
the packed-consumer boundary.

Target contract: `HostLinkCommentEncoding` exposes exactly `Utf8` and `Cp932`.
Every public comment-text path requires one of those selections and uses strict
decoding without replacement, fallback, profile selection, or guessing.
`Cp932` means Windows code page 932 / Windows-31J and is the compatibility
selection for KEYENCE material using the name "Shift_JIS"; there is no separate
strict-Shift_JIS selection. `ReadCommentBytesAsync` returns the exact response
body after the Host Link frame terminator is removed, retaining trailing ASCII
space padding. Text reads remove only trailing ASCII `0x20` before decode.
Malformed selected text raises `HostLinkProtocolError` and retires the
connection. Aggregate overloads without a codec remain usable for non-comment
reads, but reject a complete plan containing `:COMMENT` before any send. The
explicit-codec aggregate overloads require at least one `:COMMENT`; an unused
codec on a non-comment or empty aggregate is an argument error before transport.

Strict CP932 uses the cross-runtime shared assigned set: ASCII `00..7F` is
preserved, halfwidth `A1..DF` is accepted, assigned double-byte mappings and the
398 mapped Windows extension pairs are accepted, and `80`, `A0`, `FD..FF`,
incomplete sequences, invalid trails, and unassigned pairs are rejected. The
.NET exception-fallback decoder alone is not the contract because it rejects
those 398 pairs even though default .NET CP932, Python CP932, and Node WHATWG
Shift_JIS map them consistently.

Compatibility impact: this intentionally removes the implicit
UTF-8-first/Shift_JIS-fallback `ReadCommentsAsync` contract. Callers must select
UTF-8 or CP932 explicitly, or consume raw bytes when they cannot assert the
encoding. Comment-containing named/polling aggregates must also move to the
explicit-codec overload.

Acceptance criteria:

1. Reflection and compile-time tests prove the enum contains only `Utf8` and
   `Cp932`, the old no-codec text signature is absent, and the text/raw methods
   have the approved signatures.
2. Raw reads retain every response-body byte, including malformed text bytes
   and trailing ASCII spaces, while excluding the CR/LF frame terminator.
3. Ambiguous `C2 A2` decodes only as selected (`¢` for UTF-8 and `ﾂ｢` for
   CP932); malformed UTF-8 `C2` and CP932 `81 00` never fall back or replace.
   CP932 preserves control bytes `1A`, `1C`, and `7F`, accepts `8790`, `ED40`,
   and `FA4A` with the shared mappings, and rejects `80`, `A0`, `FD..FF`,
   incomplete input, invalid trails, and unassigned `81AD`. `EF BB BF 41` is
   preserved as `U+FEFF` plus `A` only under UTF-8 and is rejected under CP932.
4. Malformed text raises `HostLinkProtocolError` and retires the connection;
   PLC `E0` through `E9` responses retain their existing error classification.
5. Invalid enum values and no-codec comment aggregates fail during complete
   preflight with zero sends. Explicit-codec named and polling reads work in the
   ordinary FIFO turn only when at least one `:COMMENT` is present; an explicit
   unused codec is an argument error with zero sends.
6. User docs, XML/API reference, changelog, tests, package consumer, and this
   migration record agree with the approved behavior.

Evidence checklist:

- [x] User approved the explicit-codec/raw-byte target and compatibility break.
- [x] .NET implementation completed against the approved public surface.
- [x] Deterministic raw, ambiguous-codec, malformed, padding, aggregate,
  invalid-enum, PLC-error, and connection-state tests were added.
- [x] Full static, target-framework, sample, package, documentation, and source-archive gates passed.
- [x] Codex self-review completed and every finding dispositioned and reverified.
- [x] No further live PLC check is required: the prior read-only evidence rules
  out a universal codec, while all new behavior is a deterministic API/decoder
  contract exercised with exact loopback payloads.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final .NET acceptance criteria verified and this repository item marked complete.

Accepted self-review finding: the packed-consumer gate initially restored the
same package version from the global NuGet cache and could therefore compile
against a stale assembly instead of the candidate package. The gate now uses a
fresh isolated package cache and asserts the approved comment enum and raw/text
method signatures. A second source-archive run exposed IDE discovery of the
disposable `.csproj`, which held its directory after the packed consumer had
already passed. The consumer now uses a neutral `.proj` with explicit C# targets,
disables build-server reuse, and retries short-lived cleanup locks. The final
isolated package and source-archive gates both passed.

Final verification evidence: ordinary current-worktree CI passed 221 tests on
each of .NET 8, .NET 9, and .NET 10, formatting, generated API/reference checks,
all six sample builds, release tooling, and the isolated 12-file NuGet consumer.
The synthetic 74-file worktree source archive independently passed 663 test
executions plus restore, build, format, documentation, sample, and package gates.
No live PLC communication or public-registry publication was performed.

Accepted cross-runtime self-review finding: .NET
`DecoderFallback.ExceptionFallback` rejects 398 CP932 Windows-extension pairs
that Python CP932 and Node WHATWG Shift_JIS accept, despite the default .NET
CP932 table producing the same mappings. Treating ExceptionFallback as the
contract would therefore violate family parity. The decoder now prevalidates the
shared assigned byte set, explicitly includes those 398 mapped pairs, rejects
all forbidden/malformed/unassigned input before decoding, and then uses the
default mapping without permitting replacement. Deterministic controls,
extension mappings, UTF-8 BOM preservation/CP932 rejection, singleton,
incomplete, invalid-trail, and unassigned-pair tests cover the corrected boundary.

Accepted cross-runtime self-review finding: allowing an explicit comment codec
on an aggregate with no `:COMMENT` made that public setting silently unused and
diverged from the Node target. The ordinary aggregate overload is now exclusively
for non-comment plans, while the explicit-codec overload requires at least one
comment entry. Non-comment and empty explicit-codec plans raise
`ArgumentException` during complete preflight with zero sends; direct named and
deferred polling paths are both covered.

## GOAL-CROSS-OS-CI-001 — Bounded Linux network-lifecycle smoke

Implementation scope: one Linux/.NET 10 CI job and the focused
`CrossOsLifecycle` test trait in the existing transport/lifecycle suites.

Target contract: the Windows .NET 8/9/10 solution gate remains authoritative.
One independent Linux/.NET 10 job executes only twelve fake/IPv4-loopback
tests covering fragmented TCP receive, refused connection cleanup, connection
and request deadlines, caller cancellation, close while waiting, TCP/UDP
transport retirement, reopen, and rejection of delayed UDP data. It performs
no package, sample, full-matrix, or live-PLC work.

Compatibility impact: none. This is CI and deterministic test coverage only.

Acceptance criteria:

1. CI contains exactly one required Ubuntu lifecycle-smoke job on net10.0.
2. The existing Windows full gate and its three target frameworks remain intact.
3. The selected trait covers each applicable TCP/UDP lifecycle criterion with fake or loopback peers only.
4. The Linux job does not duplicate package, source, sample, format, or complete test gates.

- [x] Implementation completed in this repository.
- [x] Deterministic connection-refusal test added and existing lifecycle tests explicitly selected.
- [x] Windows full gate and Linux bounded smoke passed on the same reviewed source state.
- [x] Codex self-review completed against scope, filter, lifecycle coverage, and test isolation.
- [x] Live PLC verification is not required; all selected endpoints are fake or IPv4 loopback.
- [x] CI and maintainer documentation agree; no user migration or generated API change is required.
- [x] Final acceptance criteria verified and the item marked complete.

Self-review disposition:

- Accepted: no deterministic test directly proved that a refused TCP candidate
  is retired and that the same client can later open successfully. The focused
  loopback test now proves both states.
- Reused: existing deterministic tests cover all other approved lifecycle
  paths, including stale UDP response rejection, so they are trait-selected
  instead of duplicated.
- Rejected: running every TFM, package/source validation, format, or all tests
  on Linux would duplicate the authoritative Windows gate.
- No deferred finding remains for this implementation.

## GOAL-DOCUMENTED-API-DIFF-001 — Immutable API baseline and classification gate

Implementation scope: the net8.0, net9.0, and net10.0 package assemblies, the
immutable 3.2.1 NuGet baseline and prior stable documentation/example
provenance, before/after full-signature classifications, classifier tests,
normal CI, release CI, source-archive validation, and maintainer evidence.

Target contract: each candidate target framework's externally accessible
assembly surface is compared with `PlcComm.KvHostLink` 3.2.1 selected by
package URL, exact matching archive entry, and
SHA-256 `782F605D7D5A45D8402B0D0AE7A61E42BCD2B2C7DD2B101EF424BA53E66B2E28`.
Every exact added, removed, or changed ID in each target framework must have
one non-wildcard classification pinned to the complete before and after
signatures. Missing, duplicate, invalid, signature-drifted, or stale records fail. Release
enforcement also requires a major version above the baseline while documented
incompatible differences remain.

Compatibility impact: this tooling does not change runtime behavior. The
recorded actual differences retain their independently approved compatibility
impact and migration instructions.

Acceptance criteria:

1. Baseline identity, package URL, three target-framework assembly entries, digest, prior stable contract commit, and per-file Git blob IDs are tracked and visible in repository diffs.
2. Public/protected/protected-internal surface (including editor-hidden and compiler-generated accessible symbols), inheritance, parameter/return/nullability metadata, defaults, generic constraints, operators, indexers, init-only setters, enum underlying types, const values, and profile names/descriptors are compared independently for every target framework; duplicate API IDs fail.
3. Every difference is classified exactly once under one of the four approved categories with exact before/after signatures, and stale or signature-drifted classifications fail.
4. Documented breaks require decision, migration, changelog, user/generated documentation, and major-release disposition evidence.
5. Documented and undocumented public records use reproducible presence or absence searches over immutable prior stable README, standard pages, generated API reference, and samples.
6. Synthetic policy tests cover all four categories, three-framework completeness, exact signature drift, duplicate API IDs, special surface kinds, unclassified failure, and same-major release rejection.

- [x] Immutable baseline metadata and exact classifications completed in this repository.
- [x] Classifier and four-category enforcement tests implemented.
- [x] Build, classifier tests, actual API diff, generated docs, package, and source-archive gates passed for the reviewed worktree state.
- [x] Codex self-review completed against actual v3.2.1/current generated API signatures and public source changes.
- [x] Live PLC verification is not required; this gate inspects package metadata and maintained documentation.
- [x] Changelog, migration records, user/generated docs, and version-policy disposition are linked from classification evidence.
- [x] Three-TFM tests, all six net10.0 samples, package consumer, format, and extracted worktree source-archive validation passed.
- [x] The release-major gate correctly rejected current version `3.2.1` because documented incompatible changes require a major above `3`.
- [x] Actual candidate version is `4.0.0`; the complete repository release gate passed on 2026-08-07 with all three target frameworks, API-diff enforcement, samples, source archive, and isolated NuGet consumer validated.

Current actual-diff disposition:

- `documented-contract` (22): removal of the queued client/type-specific
  overloads, the explicit comment-codec break, the monitor-format contract,
  and the two factory return-type changes. Each maps to its recorded approved
  decision and requires a major version before release.
- `additive` (13): approved lifecycle/outcome errors, comment encoding/raw and
  aggregate overloads, Boolean-only direct-bit overloads, and the explicit
  expansion-unit word-bit helper.
- `undocumented-public` and `generated-or-noncontract` (0 current): supported
  and enforced by focused synthetic cases; no empty classification is invented.

Self-review disposition:

- Accepted: a source-regenerated baseline could hide candidate changes. The
  selected stable package identity and digest are independent of candidate
  source, and a baseline/classification edit remains visible in review.
- Accepted: mutable candidate docs cannot establish whether a prior symbol was
  documented. The stable tag resolves to full commit
  `19df212d9bbed545d137e1e6d71b8afb30237628`; every README, standard user page,
  generated API page, and maintained sample input is also pinned by Git blob
  ID before presence/absence classification.
- Accepted: a dictionary comprehension could silently overwrite colliding API
  IDs. Both baseline and candidate surfaces now reject duplicate IDs before
  comparison, and classifications reject duplicates after per-TFM expansion.
- Accepted: public metadata alone does not expose canonical runtime profile
  string values. The inspector records every value returned by
  `KvHostLinkPlcProfiles.GetNames()` and every public descriptor as separate
  exact contract entries.
- Accepted: a normal CI gate cannot require the not-yet-selected release
  version while work is still `Unreleased`. Normal CI requires the explicit
  major-release disposition; release CI additionally enforces the actual major
  version before packaging.
- Rejected: blanket type/namespace suppressions would allow later unrelated
  changes to pass. Only exact IDs without wildcard characters are accepted.
- Detector limitation: reflection does not prove behavioral exception paths or
  interpret prose. Existing generated-reference freshness, XML coverage,
  package checks, and required self-review remain independent evidence.
- No deferred implementation finding remains. On 2026-08-01 the exact comparison passed 96
  per-TFM records (32 distinct differences repeated across net8.0, net9.0, and net10.0), with
  `documented-contract=20`, `additive=12`, and no unclassified or stale item. The verified stable
  package SHA-256 was
  `782F605D7D5A45D8402B0D0AE7A61E42BCD2B2C7DD2B101EF424BA53E66B2E28` from two independent NuGet
  endpoints. The worktree and extracted archive each passed 222 tests per TFM; all six samples,
  generated API freshness, XML coverage, package consumer, format, and archive gates passed. No
  version was changed and no package was published.

## GOAL-DOTNET-SAMPLE-TFM-001 — Six user samples on current LTS

Implementation scope: all six projects under `samples`, sample prerequisites,
sample inventory enforcement, changelog, normal sample CI, and source-archive
sample validation.

Target contract: every user-facing sample targets exactly `net10.0`. The
library and test projects continue to multi-target
`net8.0;net9.0;net10.0`, and the package keeps all three assets.

Compatibility impact: cloning and building a repository sample now requires
the .NET 10 SDK. Existing applications consuming the package do not need to
retarget.

Acceptance criteria:

1. Exactly six discovered sample projects target exactly net10.0.
2. Sample inventory rejects missing, extra, multi-targeted, or non-net10.0 sample projects.
3. Samples README and getting-started prerequisites state the .NET 10 SDK requirement without changing package compatibility.
4. Library/tests and package-content expectations retain net8.0, net9.0, and net10.0.

- [x] All six user-facing sample project files updated.
- [x] Sample inventory and prerequisite documentation updated.
- [x] All six sample builds, inventory, source-archive, format, and package checks passed for the reviewed worktree state.
- [x] Codex self-review completed across all six projects and library/test/package TFM boundaries.
- [x] Live PLC verification is not required; sample build targets make no PLC capability claim.
- [x] Changelog and sample prerequisites agree without inventing a package migration.
- [x] Final sample acceptance criteria verified by the executed worktree and extracted source-archive gates.

Self-review found no additional sample project, no library/test TFM change, and
no maintainer-only project in this repository requiring exclusion. No finding
is deferred.

## Accepted self-review finding — packed NuGet consumer boundary

The package guard previously inspected the generated NuGet entries but did not
prove that a consumer could restore and compile from that artifact alone. It now
creates an isolated net8.0 consumer, restores from only the local package output,
and runs code that references `KvHostLinkClient`. This packed-consumer gate
passed on 2026-08-01; no registry publication was performed.

The first final archive rerun revealed that the old worktree-attribute option
still archived the `HEAD` tree and therefore could miss uncommitted files. The
finding was accepted. The option now creates the review archive from all
non-ignored current-worktree files while honoring deletions and source-artifact
exclusions. The corrected extracted archive passed 603 test results, docs,
samples, formatting, and the packed NuGet consumer gate.

The cross-ecosystem artifact review additionally found incomplete negative
coverage for repository-only NuGet material. The accepted correction now
rejects CI, cache/build, source, maintainer, release-output, tools, and
credential-like paths/files. The hardened 12-file NuGet consumer gate passed.

## REAUDIT-004 — Reject bracketed IPv4 input

Implementation scope: public client construction and
`KvHostLinkConnectionOptions` host validation.

Target contract: IPv4 literals must be unbracketed. Inputs such as
`[127.0.0.1]` fail during construction before DNS resolution, socket creation,
connection, or transmission. Existing unbracketed IPv4, hostname, and IPv6
handling otherwise remains unchanged.

Compatibility impact: remove brackets from IPv4 configuration values before
constructing the client or connection options.

Acceptance criteria:

1. Bracketed IPv4 is rejected by both public construction paths.
2. Rejection occurs before any network operation.
3. Unbracketed IPv4 remains valid.
4. User documentation and the changelog describe the migration.

- [x] Implementation and boundary tests completed.
- [x] Documentation and migration guidance updated.
- [x] All repository verification and final self-review passed.
- [x] Live PLC verification is not required because validation rejects the input before communication.

## REAUDIT-006 — Treat test-listener shutdown as normal completion

Implementation scope: the asynchronous Host Link test server only; runtime library behavior is
unchanged.

Target contract: disposing a newly created test server may race with the listener accept loop.
`InvalidOperationException` is ignored only after the server cancellation source has been
cancelled. The same exception outside shutdown continues to fail the test.

Compatibility impact: none. This changes test infrastructure only.

Acceptance criteria:

1. Immediate construction and disposal completes repeatedly without an intermittent failure.
2. Only shutdown-associated `InvalidOperationException` is ignored.
3. Runtime transport and public API files are unchanged by this item.

- [x] Test infrastructure correction and regression test completed.
- [x] All repository verification and final self-review passed.
- [x] Live PLC verification is not required because this item affects only the loopback test listener.

Acceptance evidence reverified on 2026-08-02:

- The initial broad `DisposeAsync` exception filter was rejected during final
  self-review because cancellation performed immediately before awaiting the
  server task could misclassify an already-faulted handler
  `InvalidOperationException` as shutdown-related.
- The accepted correction handles expected shutdown exceptions only at the
  listener accept and stream read/write operations that can produce them.
  Handler execution remains outside those catches, so a pre-shutdown handler
  failure propagates from both the server completion task and `DisposeAsync`.
- Deterministic tests cover cancellation before accept begins, disposal after
  accept waiting begins, and propagation of the same pre-shutdown handler
  exception instance. Peer disconnects remain normal test-server termination.
- `run_ci.bat` passed all 13 gates with 254 tests on each of .NET 8, .NET 9,
  and .NET 10, zero build warnings/errors, format/API checks, all samples,
  package construction, and isolated consumer validation.
- Accepted findings are fixed. Rejected, duplicate, deferred, and live-PLC
  findings are none; product runtime code and public API were not changed.

## REAUDIT-008 — Uniform raw request frame limit

Implementation scope: the shared raw frame builder used by TCP and UDP.

Target contract: an ASCII raw command body is at most 65,506 bytes and the
terminating CR makes the complete request frame at most 65,507 bytes. Larger
input fails before connection-state checks, DNS, socket creation, or I/O.
Smaller command-specific limits remain authoritative.

Compatibility impact: raw callers sending larger bodies must split work into
valid protocol commands rather than depending on transport failure.

Acceptance criteria:

1. A 65,506-byte body builds and sends as a 65,507-byte frame.
2. A 65,507-byte body fails for both TCP and UDP configurations before network access.
3. Rejected input does not change traffic statistics.
4. XML documentation, user guidance, and the changelog use the same units and limits.

- [x] Implementation and TCP/UDP boundary tests completed.
- [x] Documentation and migration guidance updated.
- [x] All repository verification and final self-review passed.
- [x] Live PLC verification is not required because the absolute transport-frame bound is enforced before communication.

## PERF-001 — Minimal named-read request plan

Implementation scope: `ReadNamedAsync`, the shared poll read plan, typed result decoding,
aggregate tests, user documentation, and generated API documentation. This target supersedes
the declared-wire-order portion of `GOAL-HL-AGGREGATE-DEFER-001`; declared result order remains
unchanged.

Target contract: validate the complete aggregate before transport; group wire-compatible device
families by first appearance; sort addresses inside each group; merge contiguous spans up to each
request limit without tearing a declared multiword value; and retain native single reads for
non-batchable entries without disabling batching elsewhere. Materialize the complete dictionary in
declared input order or return only an error. Multiple requests are explicitly non-atomic.

Compatibility impact: wire requests may no longer follow caller input order. Code must not depend
on internal request order; use the returned input-order mapping. Hex word views may be satisfied by
a `.U` batch and locally formatted as four uppercase digits.

Acceptance criteria:

1. A later invalid or duplicate entry produces zero sends.
2. Groups execute by first appearance and addresses inside a group execute in ascending order.
3. Contiguous compatible values use the minimum legal `RDS` segments, including capacity splits
   that never tear DWord or Float values.
4. COMMENT, native-32-bit, and direct-bit word views do not force unrelated compatible values to
   use individual reads.
5. Returned keys and values preserve declared input order, and any internal failure exposes no
   partial dictionary.

- [x] Implementation completed in this repository.
- [x] Targeted grouping, sorting, mixed-native, capacity, failure, and result-order tests added or updated.
- [x] Full static, unit, integration, sample, package, and current-worktree source-archive checks passed.
- [x] Codex final self-review completed against the approved contract and Host Link family consistency.
- [x] Live PLC verification is not required; planning and result mapping are deterministic loopback behavior.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

Self-review disposition for PERF-001/PERF-002/PERF-008B/PERF-008C:

- Accepted and fixed: UDP framing failures discarded only the socket, but later semantic/shape/comment
  decode failures still closed the complete logical session. All response-validation invalidation now
  discards only the current UDP socket while retaining the resolved endpoint; TCP behavior is unchanged.
- Accepted and fixed: queued duplicates were checked only before the following request. The receive
  path now also rejects an already queued second datagram before accepting the current exchange; the
  documented check-to-send and post-success arrival races remain because Host Link UDP has no request ID.
- Rejected findings: none.
- Duplicate findings: none.
- Deferred findings: none. The final repository-wide static, test, sample, package, documentation,
  and current-worktree source-archive gates passed for the accepted implementation.

## PERF-002 — Healthy UDP socket reuse with anomaly retirement

Implementation scope: UDP open, request send/receive, protocol invalidation, close, lifecycle tests,
user guidance, and generated API documentation.

Target contract: explicit UDP open resolves the IPv4 endpoint once and creates one connected
socket. Fully valid exchanges reuse it. Timeout, caller cancellation, I/O failure, malformed or
protocol-invalid response, and a pre-send unowned datagram discard the affected socket while
retaining the logical session and resolved endpoint. The next request creates one replacement
without DNS resolution and without retrying the failed request. Explicit close removes the socket,
endpoint, and logical session. A duplicate arriving after the pre-send check and before send remains
an unavoidable transaction-ID-free UDP race and is documented.

Compatibility impact: `IsOpen` remains true after a UDP exchange anomaly and the next request may
proceed without `OpenAsync`. Successful UDP requests reuse one local socket instead of selecting a
new source port for every request.

Acceptance criteria:

1. Two successful exchanges use the same connected socket endpoint.
2. Timeout and caller cancellation discard the affected socket, keep `IsOpen`, and allow a later
   request through a replacement socket without another open.
3. Malformed responses and queued pre-send datagrams follow the same discard-and-replace behavior.
4. A pre-send unowned datagram causes no send; no failed exchange is retried automatically.
5. Explicit close interrupts active UDP I/O and clears the complete logical session.

- [x] Implementation completed in this repository.
- [x] Targeted reuse, timeout, cancellation, malformed-response, unowned-datagram, recovery, and close tests added or updated.
- [x] Full static, unit, integration, sample, package, and current-worktree source-archive checks passed.
- [x] Codex final self-review completed against the approved contract and Host Link family consistency.
- [x] Live PLC verification is not required; socket lifecycle and response ownership are deterministic loopback behavior.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

Additional live acceptance — `HL-KVX500-02`:

The separately approved read-only .NET UDP row passed against
`keyence:kv-x500` at `192.168.250.100:8501`. The retained artifact
`D:\APP\live-kvx500-20260802\dotnet_hl_kvx500_02_udp_final_result.json`
has SHA-256
`DEFC9AE43782A4823B1ED2F952DD28B8804663673B01FDB68E9443561C7229E5`
and records `status=pass`, `writes=false`, start
`2026-08-02T11:54:15.489401+00:00`, finish
`2026-08-02T11:54:15.6103748+00:00`, repository HEAD
`1f3d36638c1ed9877a4e73bfa775a68df30e8e63`, and working-tree diff
SHA-256 `B39F955BC2F3645D95F358E0C6705810827F81F658F33D78389B932C6AFF2700`.

Both 11-request cycles used one socket generation and the same local endpoint
`192.168.250.110:60674`. All 22 requests completed with 44 raw trace frames,
316 transmitted bytes, 246 received bytes, one socket create/bind-connect, and
zero DNS resolutions. Direct and normalized monitor-word values were
`[0, 0, "0000", 0, 0, 13]` in both cycles; consecutive and monitored bits were
`["1", "0", "1"]`. Close removed the socket and logical session. The later
same-client `DM120.U` read was rejected by the exact public
`PlcComm.KvHostLink.HostLinkNotConnectedError` before send; raw-frame count,
traffic counters, socket counters, and DNS count remained unchanged.

The earlier .NET NG artifact retained by the central evidence set completed
all 22 read-only PLC requests and closed the socket, then allowed the expected
post-close `HostLinkNotConnectedError` to escape its evidence harness instead
of recording it as a passing rejection. This was a runner-control-flow defect,
not a PLC or library NG; the corrected final artifact above supersedes it for
this live row.

- [x] The `HL-KVX500-02` .NET live row passed and its final artifact, lifecycle evidence, and runner-only NG classification were verified.

Additional controlled UDP failure acceptance — `HL-KVX500-02B`:

The read-only .NET anomaly row passed against `keyence:kv-x500` at
`192.168.250.100:8501`. The controlled failure path physically unplugged and
reconnected the PLC cable between externally gated phases. The retained
artifact
`D:\APP\live-kvx500-20260802\dotnet_hl_kvx500_02b_udp_result.json` has
SHA-256
`153BB2854412B308557D99A7C989E6E5E9F25C38D770EDECF2749AE57A6CC57B`
and records `status=pass` and `writes=false`.

Phase A returned `DM120.U=0` on the original UDP socket. Phase B made exactly
one request, timed out after approximately 2003 ms, performed no retry, and
retired that physical socket while retaining the logical numeric endpoint.
Phase C created exactly one replacement socket and returned `DM120.U=0`.
Across all phases the artifact records exactly three requests, 33 transmitted
bytes, and 14 received bytes. Final close left zero active sockets after two
socket creates and two socket closes; DNS resolution count remained zero.

- [x] The `HL-KVX500-02B` .NET controlled UDP timeout, retirement, one-shot replacement, and final-close live row passed.

## PERF-008B — One FIFO turn for a complete aggregate

Implementation scope: named-read execution staging and FIFO integration.

Target contract: snapshot, validate, and compile before FIFO admission. Once admitted, retain one
FIFO turn through every planned request and the last response decode or all-or-error failure.
Release the turn before pure dictionary materialization. Close may still interrupt the active turn;
metadata-only getters remain outside admission.

Compatibility impact: none to the returned data contract. A later same-client operation cannot
interleave between batchable and native single segments of one aggregate.

Acceptance criteria:

1. A competing same-client operation cannot send until every aggregate segment and decode completes.
2. Batchable and native single segments share the same FIFO turn.
3. Failure or cancellation stops before later segments and returns no partial dictionary.
4. Dictionary materialization occurs after the FIFO lease is released.

- [x] Implementation completed in this repository.
- [x] Targeted mixed-segment FIFO, failure, cancellation, and no-partial-result tests added or updated.
- [x] Full static, unit, integration, sample, package, and current-worktree source-archive checks passed.
- [x] Codex final self-review completed against the approved contract and Host Link family consistency.
- [x] Live PLC verification is not required; admission ordering is deterministic loopback behavior.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## PERF-008C — Compile-once non-overlapping polling

Implementation scope: `PollAsync`, shared compiled named-read plan, polling tests, user guidance,
and generated API documentation.

Target contract: snapshot, validate, and compile the fixed address plan once when enumeration
starts. Every cycle reuses exactly that plan, stages the complete all-or-error result in one FIFO
turn, then releases the turn before its completion-delay interval. Cycles do not overlap and do not
catch up after slow work.

Compatibility impact: the interval is explicitly measured after each completed cycle rather than
as a fixed-rate schedule. Other same-client operations can run during that delay.

Acceptance criteria:

1. Invalid interval or plan data fails before the first send.
2. Every cycle emits the same compiled request plan and preserves input-order results.
3. One cycle cannot overlap another and no missed interval causes catch-up sends.
4. A competing same-client operation can use the FIFO during the completion delay.

- [x] Implementation completed in this repository.
- [x] Targeted plan-reuse, invalid-interval, input-order, and FIFO-release-during-delay tests added or updated.
- [x] Full static, unit, integration, sample, package, and current-worktree source-archive checks passed.
- [x] Codex final self-review completed against the approved contract and Host Link family consistency.
- [x] Live PLC verification is not required; scheduling and plan reuse are deterministic loopback behavior.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

## LIVE-HL-004-DOTNET-API — Optional packed direct-bit monitor format

Implementation scope: public `KvMonitorWordTarget` construction, MWS validation and exact wire
generation, MWR response decoding, monitor metadata lifecycle, deterministic tests, generated API
reference, user guidance, and migration notes in `plc-comm-hostlink-dotnet`.

Target contract: `KvMonitorWordTarget.DataFormat` is nullable and defaults to `null`. Only a
direct-bit MWS target may omit the format, and that target remains bare on the wire. Its MWR field
is one through five ASCII decimal digits representing the unsigned packed 16-bit word beginning at
the target bit, over the complete `0` through `65535` domain. Leading zeros are accepted. Empty,
signed, whitespace-containing, non-decimal, over-five-digit, and overflowing fields are invalid.
Explicit `.U`, `.S`, `.H`, `.D`, and `.L` monitoring is unchanged. Bare scalar RD and MBS/MBR
remain strict bit operations.

Compatibility impact: existing two-argument construction remains binary-compatible and remains
source-compatible when a non-null format is supplied. One-argument construction is newly available,
but `DataFormat` and the generated record deconstruction output are now annotated nullable, so code
that reads either must handle `null`. Valid bare direct-bit MWR responses that older code rejected
are now returned in the existing `string[]` representation. No compatibility alias or implicit
`.U` wire suffix is introduced.

Acceptance criteria:

1. `new KvMonitorWordTarget("R5000")` emits exact `MWS R5000`; null on an ordinary word target and
   empty or whitespace on every target fail before transport.
2. Bare MWR accepts `0`, `2`, `13`, the independent live vectors `00002` and `00013`, plus `00000`
   and `65535`, while empty, signed, whitespace-containing, overflow, non-decimal, and over-five-digit
   fields are protocol errors that retire the supplying transport.
3. Mixed monitor targets preserve registration order and independently apply packed unsigned,
   explicit decimal, signed, hexadecimal, double-word, and long-word decoders.
4. Scalar RD and MBS/MBR do not inherit packed-word semantics.
5. Reopen and failed replacement registration cannot reuse stale monitor-word decoder metadata.
6. Source XML, generated API reference, user guidance, changelog, and this migration record agree.

Live evidence: the approved KV-X500 verification returned `00002` for the adjacent-bit probe and
`00013` for the independently prepared bit pattern. After the exact guarded .NET program was
completed, compiled, reviewed, and separately approved, the public API read `R5000`–`R5015`,
calculated `13`, sent bare `MWS R5000`, and returned preserved monitor string `00013`. Evidence:
`D:\APP\live-kvx500-20260802\dotnet_mwr_semantic_acceptance_result.json`.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Relevant static checks, unit tests, examples, generated documentation, and build/package checks passed.
- [x] Codex self-review completed against the approved contract and cross-language consistency requirements.
- [x] Corrected .NET public-API live acceptance passed against the independently prepared bit pattern.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [x] Final acceptance criteria verified.

Acceptance evidence reverified on 2026-08-02:

- `run_ci.bat` passed all 13 gates with 310 tests on each of .NET 8, .NET 9,
  and .NET 10, zero failures/skips, format, API generation and exact diff
  classification, all six samples, XML documentation, package construction,
  package-content inspection, and the isolated net8.0 consumer.
- The focused extension suite passed 114 tests independently on each target
  framework. It includes `0`, `2`, `13`, `00000`, the live `00002` and `00013`
  vectors, `65535`, every rejected response class, mixed ordering, preflight, strict bit
  separation, failed registration, reconnect, and transport retirement.
- Public API review confirmed that CLR constructor parameter types remain
  `System.String,System.String`; the intentional differences are the null
  default plus nullable constructor, property, and generated deconstruction
  metadata, classified identically for all three target frameworks.
- Final diff review found no validation-order, response-decoding, state,
  cancellation/timeout, documentation, packaging, or cross-language contract
  defect. Accepted, rejected, duplicate, and deferred findings are none.

## Final non-live disposition recheck — `HL-001` and `HL-003`

Final source-state recheck on 2026-08-02 passed without PLC communication.

- `HL-001`: the net8.0 filtered test command selecting
  `TcpRejectsExtraNonEmptyResponseBufferedAfterTerminator` and
  `TcpRejectsDelayedUnownedResponseBeforeSendingNextCommand` passed 2/2. The
  deterministic peers prove extra-response rejection, transport retirement,
  and zero next-command send, so a response cannot be reassigned.
- `HL-003`: the net8.0 filtered test command selecting
  `Float32TypedNamedAndPollingEntriesRejectIneligibleFamiliesBeforeTransport`
  and `Float32RejectionOccursBeforeWaitingForTheClientFifo` passed 6/6. The
  parameterized direct, typed, named, polling, and FIFO-barrier cases reject
  `Z:F` and the other ineligible families before transport.

- [x] `HL-001` deterministic non-live disposition reverified on the final source state.
- [x] `HL-003` deterministic non-live disposition reverified on the final source state.

## HL-DOTNET-001 — Failed factory open disposes its owned client exactly once

Decision status: complete on 2026-08-02. This closes the deterministic
ownership-evidence gap; it does not change the public API or supported runtime
behavior.

Implementation scope: `KvHostLinkClientFactory` internal construction/open/
dispose ownership boundary and direct deterministic factory tests. The public
factory signature, connection options, transport behavior, and error types are
unchanged.

Target contract: successful factory open transfers the exact created client to
the caller without factory disposal. Any failed open disposes that owned client
exactly once and rethrows the exact original failure instance. A disposal-only
failure cannot replace the primary open failure, and separate repeated calls
own and dispose separate clients.

Compatibility impact: none. The injection boundary and test-assembly access
are internal; the generated public API surface is unchanged.

Machine-verifiable acceptance criteria:

1. Success returns the same created instance and the factory disposal count is zero.
2. Injected connection refusal, DNS failure, internal timeout, and caller cancellation each rethrow the same exception object and dispose exactly once.
3. Injected disposal failure is suppressed only while the original open failure is rethrown unchanged.
4. Three repeated failures create three distinct clients and dispose each exactly once.
5. Existing public factory success and cancellation behavior still pass, all target frameworks build warning-free, and the documented API-difference gate reports no new public surface.

Verification evidence:

- `dotnet test tests\PlcComm.KvHostLink.Tests\PlcComm.KvHostLink.Tests.csproj -c Release -f net8.0 --filter "FullyQualifiedName~KvHostLinkClientFactoryOwnershipTests"` passed 7/7.
- The same net8.0 targeted run combined with `FactoryPreservesOpenFailure` and
  `OpenAndConnectAsync_ReturnsNormalClientWithIntegratedFifo` passed 9/9.
- `dotnet build src\PlcComm.KvHostLink\PlcComm.KvHostLink.csproj -c Debug`
  passed net8.0, net9.0, and net10.0 with zero warnings and errors.
- `scripts/check_documented_api_diff.py` passed all three target frameworks;
  it found no unclassified or undocumented public difference.
- `dotnet format PlcComm.KvHostLink.sln --verify-no-changes --no-restore` and
  targeted `git diff --check` passed.

Codex self-review inspected the actual diff, public surface, validation order,
success ownership transfer, each error identity, disposal masking, repetition,
cancellation token propagation, and existing public factory regressions.
Accepted findings: the internal cancellation token parameter was moved last to
satisfy CA1068. Rejected findings: no public factory hook or mutable global test
override is needed. Duplicate and deferred findings: none. Live PLC verification
is not required because the acceptance facts are injected object identity,
exception identity, and exact disposal counts before any real network work.

- [x] Implementation completed in this repository.
- [x] Tests added for every acceptance criterion.
- [x] Targeted tests, all-target library build, formatting, and public API checks passed.
- [x] Codex self-review completed against ownership, error, lifecycle, and public-surface requirements.
- [x] Live PLC is not required for this deterministic ownership contract.
- [x] Maintainer evidence agrees with the implementation; no user migration or public changelog entry is required.
- [x] Final acceptance criteria verified and `HL-DOTNET-001` marked complete.

## RELEASE-HOSTLINK-4.1.0-20260827 — Canonical single-request APIs

Stable identifier: `RELEASE-HOSTLINK-4.1.0-20260827`.

Implementation scope: public high-level contiguous Bit and Word helpers, compatibility aliases,
samples, user and generated API documentation, package metadata, changelog, tests, and the final
release gate in this repository.

Target contract: release `ReadWordsSingleRequestAsync`, `ReadBitsSingleRequestAsync`, and
`WriteBitsSingleRequestAsync` as additive APIs in `PlcComm.KvHostLink` `4.1.0`. Each accepted call
emits exactly one native command; invalid count, family, suffix, or value input fails before send.

Compatibility impact: `ReadWordsAsync` remains a deprecated delegate for this release. Existing
callers continue to compile, while new code uses the canonical name. No wire behavior changes for
accepted requests.

Machine-verifiable acceptance criteria:

1. MSBuild reports package version `4.1.0`, and the changelog has a dated `4.1.0` section.
2. The exact repository `release_check.bat` passes on the final source state.
3. The NuGet package exposes the canonical helpers on every supported target framework.
4. PLC Scope compiles and passes its non-live tests using the candidate package API.
5. No public registry publication is performed by the agent.

Live disposition: command count, validation ordering, delegation, and response decoding are fully
covered by deterministic transport tests. No supported-PLC or physical-compatibility claim changes,
so a live PLC check is not required for this release item.

Final self-review accepted two release-preparation findings: the two new Bit helpers were initially
missing from the exact API-difference classification, and the high-level sample still displayed old
helper names. Both are corrected and reverified; no runtime-contract finding remains.
The working-tree release gate passed, but the gate and final-acceptance boxes stay open until the
same command is rerun against the eventual release commit before tagging.

- [x] Implementation and package metadata completed in this repository.
- [x] Tests cover every acceptance criterion.
- [ ] Relevant static, unit, integration, sample, source-archive, API, and package gates passed.
- [x] Codex final self-review completed against the approved contract and actual diff.
- [x] Live verification is not required under the disposition above.
- [x] Documentation, migration notes, changelog, and generated API reference agree.
- [ ] Final acceptance criteria verified and this item marked complete.
