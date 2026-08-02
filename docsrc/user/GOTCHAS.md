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
| Bracketed IPv4 endpoint | Construction rejects an address such as `[192.168.250.100]`. | Remove the brackets and use `192.168.250.100`. |
| Ambiguous write result | `HostLinkOutcomeUnknownError` is thrown after transmission may have begun. | Do not retry automatically. Inspect `Reason`, determine PLC state safely, and explicitly reopen only when appropriate. |
| UDP duplicate response | A delayed or duplicate datagram arrives after the pre-send queue check. | Host Link UDP has no transaction ID, so the narrow check-to-send race cannot guarantee response association. Use TCP when strict association is required. UDP anomalies discard the socket but retain the resolved logical endpoint for the next request. |
| Named aggregate timing | `ReadNamedAsync` or one `PollAsync` cycle uses multiple wire reads. | Results are non-atomic. Use a single-request read or a PLC-side snapshot/handshake when values must share one coherence point. |
| Device-comment encoding | Host Link `RDC` payloads do not identify their text encoding. | Use `HostLinkCommentEncoding.Utf8` or `.Cp932` explicitly, or use `ReadCommentBytesAsync` when the encoding is not known. Malformed selected text is rejected without fallback or replacement. |
