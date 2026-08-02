# HostLink performance optimization acceptance record (2026-08-02)

## PERF2-001 — Incremental TCP receive framing and trace ownership

Target contract: the logical TCP session owns a growable receive accumulator and scan cursor, scans only newly available bytes, and returns an independently owned completed response. Receive tracing creates one owned trace snapshot only while a hook is installed.

Acceptance evidence:

- [x] A 65,536-byte body read one byte at a time has linear scan/copy counters.
- [x] A mutating and throwing trace callback cannot corrupt the decoded response or stop the command.
- [x] Existing surplus-response retirement behavior remains covered.

## PERF2-005 — Transport-specific lazy buffers

Target contract: construction allocates no transport receive buffers; accepted TCP open allocates TCP accumulator/read buffers only, accepted UDP open allocates the UDP buffer only, repeated operations reuse them, and explicit close releases all references. Allocation occurs before DNS, socket work, or send.

Acceptance evidence:

- [x] Tests verify null constructor buffers, transport separation, session reuse, and null fields after close.
- [x] UDP receives directly into the session-owned buffer.

No public API, wire request, request count, or supported behavior changed. Generated API documentation needs no refresh because the public surface is unchanged. Live PLC verification is not required because deterministic local sockets cover allocation, framing, and trace ownership.
