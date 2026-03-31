# TODO: Host Link Communication .NET

This file tracks the remaining tasks and issues for the Host Link Communication (KEYENCE KV) .NET library.

## 1. Active Follow-Up

- No blocking protocol issues are open right now.

## 2. Completed Recently

- [x] **Align the high-level helper surface**: The public entry points are aligned with the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [x] **Promote reusable address helpers**: Device parse/normalize/format helpers are exposed through `KvHostLinkAddress`.
- [x] **Define a stable connection-options model**: Host Link specific settings such as transport choice and `Append LF on send` are carried by `KvHostLinkConnectionOptions`.
- [x] **Preserve semantic atomicity by default**: `*SingleRequestAsync` and `*ChunkedAsync` helpers explicitly separate one-request operations from opt-in multi-request transfers.
