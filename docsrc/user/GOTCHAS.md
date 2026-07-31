# Gotchas

Use this page only for library-specific caveats.

Use the shared
[KV Host Link Troubleshooting & Codes](https://fa-yoshinobu.github.io/plc-comm-docs-site/plc-setup/kv/troubleshooting-codes/)
page for common connection, profile, address-shape, write-permission, and PLC
error-code symptoms.

## Current library-specific caveats

| Area | Symptom | Guidance |
| --- | --- | --- |
| IPv6 endpoint | Construction rejects an IPv6 literal, or a hostname has no usable address. | Host Link connections are IPv4-only. Use an IPv4 literal or a hostname with an IPv4 result. |
| Ambiguous write result | `HostLinkOutcomeUnknownError` is thrown after transmission may have begun. | Do not retry automatically. Inspect `Reason`, determine PLC state safely, and explicitly reopen only when appropriate. |
| Named aggregate timing | `ReadNamedAsync` or one `PollAsync` cycle uses multiple wire reads. | Results are non-atomic. Use a single-request read or a PLC-side snapshot/handshake when values must share one coherence point. |
