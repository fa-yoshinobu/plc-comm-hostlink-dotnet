# HostLink .NET quality-overhaul migration

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

## D-061

Scope: Direct/queued ReadCommentsAsync
Target contract: No padding option remains; only trailing ASCII 0x20 bytes are removed before comment decoding.
Compatibility impact: Callers that retained padding must use maintainer raw bytes.

Acceptance criteria:

1. Trailing ASCII spaces are removed.
2. Tabs, full-width spaces, Unicode whitespace, and embedded spaces are preserved.
3. UTF-8/Shift_JIS invalid data produces protocol error rather than replacement text.

Evidence checklist:

- [x] Implementation completed for this decision in HostLink .NET.
- [x] Tests cover every acceptance criterion for this decision.
- [x] Static checks, unit/integration/vector tests, examples, documentation generation, and package/build checks passed where applicable.
- [x] Codex self-review inspected the actual diff, public API, validation order, errors, state, timeout/cancellation, tests, docs, and package.
- [x] Claude source review completed after explicit user authorization and evidence recorded.
- [x] Every Claude finding was dispositioned; accepted findings were corrected and checks rerun.
- [x] Live-PLC verification passed or an explicit item-level no-live/unverified release disposition is recorded.
- [x] User docs, migration, changelog, examples, and generated API reference agree with implementation.
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
- [x] Final acceptance criteria verified and this decision marked complete; family evidence is in `D:\\APP\\Close\\instructions/hostlink_cross_implementation_final_comparison_20260712.md`.

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
5. Single-device reads derive response counts from device type and explicit
   format, including 16/32-point direct-bit numeric reads; direct BIT accepts
   only `0`/`1`/`ON`/`OFF` and malformed response shapes invalidate the session.

- [x] Implementation completed in this repository.
- [x] Tests added or updated for every acceptance criterion.
- [x] Full build, multi-target 158-test runs, format, generated-doc, sample, and package checks passed.
- [x] Codex self-review completed against the corrected contract.
- [x] Claude source review completed; the user ran the authorized batch and its result is preserved in the workspace.
- [x] Codex dispositioned all .NET findings and reran affected checks.
- [x] No additional live-PLC check is required for timeout representation, pre-send year validation, API surface, and test ownership.
- [x] Documentation and migration notes agree with the implementation.
- [x] Final acceptance criteria verified for this repository; HostLink family-level acceptance remains separate.

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
- Family-level final acceptance is recorded in `D:\APP\Close\instructions\hostlink_cross_implementation_final_comparison_20260712.md`.

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

Family equivalence: all four HostLink implementations count TCP `OK\r`, `OK\n`, coalesced `OK\r\n`, and either split CR/LF ordering as 3 bytes; UDP `OK\r\n` remains 4 bytes. Incomplete oversize/EOF/timeout/cancellation data contributes zero, while a complete PLC error line is counted before semantic decoding. The family comparison record is `D:\APP\communication_library_quality_review_20260714.md`.

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
- [x] Claude source review completed; findings are recorded in `D:\APP\claude_review_findings_20260714.md`.
- [x] Codex resolved or dispositioned every applicable Claude finding and reran affected checks.
- [x] Live PLC verification is not required for this deterministic local framing and counter contract.
- [x] Documentation, migration notes, changelog, and generated API reference agree with the implementation.
- [x] Final acceptance criteria verified and the item marked complete.
