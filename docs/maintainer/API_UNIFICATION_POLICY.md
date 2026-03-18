# API Unification Policy

This document defines the planned public API rules for the Host Link .NET library.
It is a design policy document for later implementation.

## Purpose

- Keep the Host Link API aligned with the TOYOPUC .NET library where the usage model is comparable.
- Reserve low-level and high-level layers from the beginning instead of mixing them later.
- Define the async naming contract before implementation starts.
- Avoid a canonical class name that is ambiguous with non-KEYENCE Host Link protocols.

## Planned Public API Layers

The library should expose two explicit layers.

1. `KvHostLinkClient`
   Low-level API for transport control, raw frames, numeric device operations, and protocol-oriented commands.
2. `KvHostLinkDeviceClient`
   High-level API for string device addresses and application-facing read and write operations.

Planned async class names:

1. `AsyncKvHostLinkClient`
2. `AsyncKvHostLinkDeviceClient`

Quick-start documentation should prefer `KvHostLinkDeviceClient`.

## Naming Rules

High-level generic device access must use these names.

- `Read`
- `Write`
- `ReadMany`
- `WriteMany`
- `ReadDWord`
- `WriteDWord`
- `ReadDWords`
- `WriteDWords`
- `ReadFloat32`
- `WriteFloat32`
- `ReadFloat32s`
- `WriteFloat32s`
- `ResolveDevice`
- `ReadClock`
- `WriteClock`
- `ReadCpuStatus`

If the protocol later exposes special operation groups, keep them explicit and domain-named.

Examples:

- `ReadTimer`
- `WriteTimer`
- `ReadCounter`
- `WriteCounter`

Low-level typed access must keep explicit names rather than overloading the generic layer.

- `ReadWords`
- `WriteWords`
- `ReadBits`
- `WriteBits`
- `ReadDWords`
- `WriteDWords`
- `ReadFloat32s`
- `WriteFloat32s`
- `SendRaw`

Do not use `HostLinkClient` or `HostLinkHighLevelClient` as canonical public names.
Those names are too ambiguous once multiple PLC vendor protocols are considered.

Do not use `KvHostLinkClient` as the permanent high-level class name if a low-level client also exists.
That would break naming consistency with the other PLC libraries in this workspace.

## 32-Bit Value Rules

The library should distinguish raw 32-bit integers from IEEE 754 floating-point values.

- `DWord` means a raw 32-bit unsigned value stored across two PLC words.
- Signed 32-bit helpers, if added later, should be named `ReadInt32` and `WriteInt32`.
- Floating-point helpers should use `Float32` in the public name, not plain `Float`.

Default 32-bit word-pair interpretation:

- The default contract is low-word-first ordering unless the protocol requires a different native order.
- If alternate word order must be supported, expose it as an explicit option such as `wordOrder`.

## Async Rules

Async methods must use the same base names as sync methods with the .NET `Async` suffix.

Examples:

- `ConnectAsync`
- `SendRawAsync`
- `ReadWordsAsync`
- `WriteWordsAsync`
- `ReadAsync`
- `WriteAsync`
- `ReadManyAsync`
- `WriteManyAsync`
- `ReadClockAsync`
- `ReadCpuStatusAsync`

Async methods must follow these rules.

- Keep parameter order aligned with the sync method.
- Return the same logical result shape as the sync method.
- Accept `CancellationToken` on new async methods.
- Avoid creating a different user vocabulary only for async.

## Internal Naming Rules

Private helpers must describe their real subject.
Do not rely on vague names that only make sense while the file is fresh in memory.

Preferred patterns:

- `ReadResolvedDevice`
- `WriteResolvedDevice`
- `ReadResolvedBatch`
- `WriteResolvedBatch`
- `NormalizeResolvedDeviceInput`
- `NormalizeResolvedWriteItems`
- `PackUInt32LowWordFirst`
- `UnpackUInt32LowWordFirst`
- `PackFloat32LowWordFirst`
- `UnpackFloat32LowWordFirst`

Avoid names such as:

- `ReadOne`
- `WriteOne`
- `DoRead`
- `HandleWrite`
- `Offset`

## Documentation Rules

README and future samples must use the planned canonical names above.
Provisional names may exist during development, but they must not become the long-term public contract unless this document is updated.

Compatibility aliases may exist during migration, but primary samples and README quick starts must use:

- `KvHostLinkClient`
- `KvHostLinkDeviceClient`
- `AsyncKvHostLinkClient`
- `AsyncKvHostLinkDeviceClient`

## Implementation Constraint

This document is intentionally written before source implementation so that later code, samples, and tests target one stable naming plan instead of converging after the API spreads.
