# API Unification Policy

This document defines the current public API policy for `plc-comm-hostlink-dotnet`.

## Purpose

- Keep Host Link behavior aligned with `plc-comm-hostlink-python` where the operation class is equivalent.
- Keep the low-level client explicit and protocol-oriented.
- Keep high-level helpers explicit as extension methods on the low-level client.
- Document intentional cross-language design differences explicitly.

## Current Public Surface

The current public low-level client is:

- `KvHostLinkClient`

The current high-level helper layer is:

- `KvHostLinkClientExtensions`
  - `OpenAndConnectAsync`
  - `ReadTypedAsync`
  - `WriteTypedAsync`
  - `WriteBitInWordAsync`
  - `ReadNamedAsync`
  - `PollAsync`
  - `ReadWordsAsync`
  - `ReadDWordsAsync`

## Async Rules

The library is async-native.

- Public operations use the `.NET` `Async` suffix.
- New async methods accept `CancellationToken`.
- High-level convenience APIs should prefer extension methods unless a separate type is clearly necessary.

## Cross-Language Semantic Parity

Semantic parity with `plc-comm-hostlink-python` is the target.
Literal public API identity is not the target.

The following must stay aligned across .NET and Python:

- Host Link frame bodies for equivalent operations
- validation behavior
- helper-layer typed behavior
- live PLC behavior

## Intentional Differences From Python

The following differences are intentional and are not treated as bugs by themselves:

- `.NET` is async-native.
  Python exposes both sync and async low-level clients.
- `.NET` low-level reads return raw `string[]` tokens.
  Python low-level reads parse decimal tokens into `int` values.
- `.NET` uses `HostLinkTransportMode` and `KvPlcMode`.
  Python uses string/int inputs for the same concepts.
- `.NET` returns `KvModelInfo.Model = "Unknown"` when the `?K` code is unmapped.
  Python returns `None`.

## Typed Access Rules

High-level typed helpers use these codes:

| Code | Type |
| --- | --- |
| `U` | unsigned 16-bit |
| `S` | signed 16-bit |
| `D` | unsigned 32-bit |
| `L` | signed 32-bit |
| `F` | IEEE 754 float32 |

`F` is helper-layer only.
Low-level Host Link suffix validation remains limited to `.U`, `.S`, `.D`, `.L`, and `.H`.

## Documentation Rules

- README and user docs must describe the current public names.
- If behavior intentionally differs from Python, document the difference explicitly.
- Do not describe an intentional design difference as missing parity.
