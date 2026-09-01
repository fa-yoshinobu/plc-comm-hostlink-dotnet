# TODO: Host Link Communication .NET

Current active TODOs only.

## Current Status

- `HL-CROSS-API-001` and `HL-CROSS-API-002` are candidate inventories only. No rename or addition is approved for implementation.

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
