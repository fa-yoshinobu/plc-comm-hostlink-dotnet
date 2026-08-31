# TODO: Host Link Communication .NET

Current active TODOs only.

## Current Status

- `HL-DOTNET-001` is open; implementation is deferred pending the cross-library API decision.
- `HL-CROSS-API-001` and `HL-CROSS-API-002` are candidate inventories only. No rename or addition is approved for implementation.

## HL-DOTNET-001: Generic direct writes without a data format have no successful call path

Target state: every public write overload has at least one supported, machine-verifiable successful call path, or the unusable overload is removed from the public API. The resolution is deferred to the cross-library API review; do not implement a contract change during the current verification phase.

Observed contract:

- `WriteAsync<T>(string, T, CancellationToken)` and `WriteConsecutiveAsync<T>(string, IEnumerable<T>, CancellationToken)` constrain `T` to `IFormattable`.
- A direct-bit device requires a Boolean value, but `System.Boolean` does not implement `IFormattable`.
- A word device requires an explicit data format, which these overloads do not accept.
- Numeric values sent to a direct-bit device are intentionally rejected with `Direct bit writes require a Boolean value`.

Acceptance criteria:

1. [ ] Decide whether to remove the two unusable overloads or revise their public contract; record the approved cross-library consistency impact.
2. [ ] Implement the approved decision without disguising one overload as another in verification adapters.
3. [ ] Add compile-time and runtime tests proving every retained overload has a successful call path and preserves its exact public API identity.
4. [ ] Update generated API reference, migration notes, and changelog for the approved public-contract change.
5. [ ] Re-run the high-level API live check for every retained overload and record command, device/address, PLC response, and API result.

## HL-CROSS-API-001: Public API naming candidates (`decision_pending`)

Target state: the four Host Link implementations use the same concept names while retaining each language's normal casing and async conventions. Each candidate must be approved separately before implementation.

| Current .NET API | Candidate canonical API | Reason |
|---|---|---|
| `ReadDWordsAsync` | `ReadDWordsSingleRequestAsync` | The operation is one Host Link request; the explicit canonical API already exists. |
| `ReadCommentsAsync` | `ReadCommentAsync` | One device produces one comment string, and `ReadCommentBytesAsync` is already singular. |
| `CheckErrorNoAsync` | `ReadErrorNumberAsync` | The API returns the PLC error number rather than a Boolean check result. |
| `WriteSetValueAsync` | `WriteTimerCounterPresetAsync` | The operation is the T/C-only `WS` preset write. |
| `WriteSetValueConsecutiveAsync` | `WriteTimerCounterPresetConsecutiveAsync` | The operation is the T/C-only `WSS` consecutive preset write. |

Migration candidate: add an approved canonical name in the next version and keep the old name as a direct forwarding alias for an independently decided transition period. Input, result, exception, and wire command must not diverge. Deprecated `ReadWordsAsync` is outside this review.

## HL-CROSS-API-002: High-level API parity candidates (`decision_pending`)

- [ ] Decide whether to add `WriteNamedAsync` with the same one-request-only contract already implemented and live-verified by Node-RED `writeNamed`.
- [ ] If approved, reject the complete update set before transport when it cannot fit one compatible `WR`, `WRS`, or `WSS` request; do not synthesize multiple state-changing requests.
- [ ] Add implementation, exact public-identity tests, command/device/response/result live verification, API reference, migration note, and changelog only after the cross-library contract is approved.
