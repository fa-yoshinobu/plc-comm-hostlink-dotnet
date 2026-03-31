# TODO: Host Link Communication .NET

This file tracks the remaining tasks and issues for the Host Link Communication (Keyence KV) .NET library.

## 1. Project Scaffold


## 2. Protocol and Feature Work

## 3. Testing and Validation


## 4. Documentation and Packaging

## 5. Cross-Stack API Alignment

- [ ] **Align the high-level helper surface**: Keep the public entry points intentionally parallel to the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [ ] **Promote reusable address helpers**: Review whether device parse/normalize/format helpers should be made public so app integrations do not need to duplicate Host Link address handling.
- [ ] **Define a stable connection-options model**: Keep Host Link specific settings such as transport choice and `Append LF on send` explicit while still matching the common connection-shape used by the other .NET stacks.
- [ ] **Preserve semantic atomicity by default**: Do not silently split reads or writes that users would reasonably treat as one logical value or one logical block. Protocol-defined boundaries are acceptable, but fallback retries that change semantics should be opt-in and explicitly named.
- [ ] **Preserve semantic atomicity by default**: Do not silently split reads or writes that users would reasonably treat as one logical value or one logical block. Protocol-defined boundaries are acceptable, but fallback retries that change semantics should be opt-in and explicitly named.

## 5. Cross-Stack API Alignment

- [ ] **Align the high-level helper surface**: Keep the public entry points intentionally parallel to the sibling .NET libraries around `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, and `PollAsync`.
- [ ] **Promote reusable address helpers**: Review whether device parse/normalize/format helpers should be made public so app integrations do not need to duplicate Host Link address handling.
- [ ] **Define a stable connection-options model**: Keep Host Link specific settings such as transport choice and `Append LF on send` explicit while still matching the common connection-shape used by the other .NET stacks.


