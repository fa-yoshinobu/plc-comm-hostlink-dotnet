# High-Level API Contract

This document defines the target public API shape for the Host Link .NET library.
Backward compatibility is not a design constraint for this contract.

This contract is intentionally aligned with:

- `plc-comm-slmp-dotnet`
- `plc-comm-computerlink-dotnet`

## 1. Design Goals

- keep one obvious high-level entry point for application code
- keep typed read/write helpers consistent across the three .NET PLC libraries
- make connection options explicit instead of implicit
- preserve semantic atomicity by default
- forbid hidden fallback splitting that changes the meaning of one logical request

## 2. Primary Client Shape

Application-facing code should use a connected, async-safe client wrapper as the primary entry point.

Target shape:

```csharp
public sealed record KvHostLinkConnectionOptions(
    string Host,
    int Port = 8501,
    TimeSpan Timeout = default,
    HostLinkTransport Transport = HostLinkTransport.Tcp,
    bool AppendLfOnSend = false
);

public static class KvHostLinkClientFactory
{
    public static Task<QueuedKvHostLinkClient> OpenAndConnectAsync(
        KvHostLinkConnectionOptions options,
        CancellationToken cancellationToken = default);
}
```

Notes:

- the returned client must be safe to share across multiple async callers
- transport and line-ending behavior must stay explicit
- no hidden compatibility probing should be introduced

## 3. Required High-Level Methods

The primary client should expose or clearly own these operations:

```csharp
Task<object> ReadTypedAsync(
    string address,
    string dtype,
    CancellationToken cancellationToken = default);

Task WriteTypedAsync(
    string address,
    string dtype,
    object value,
    CancellationToken cancellationToken = default);

Task WriteBitInWordAsync(
    string address,
    int bitIndex,
    bool value,
    CancellationToken cancellationToken = default);

Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
    IEnumerable<string> addresses,
    CancellationToken cancellationToken = default);

IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
    IEnumerable<string> addresses,
    TimeSpan interval,
    CancellationToken cancellationToken = default);
```

## 4. Contiguous Read/Write Contract

Contiguous block access must distinguish between three behaviors:

### 4.1 Single-request behavior

Use this when the caller requires one protocol request or an error.

```csharp
Task<ushort[]> ReadWordsSingleRequestAsync(
    string start,
    int count,
    CancellationToken cancellationToken = default);

Task<uint[]> ReadDWordsSingleRequestAsync(
    string start,
    int count,
    CancellationToken cancellationToken = default);

Task WriteWordsSingleRequestAsync(
    string start,
    IReadOnlyList<ushort> values,
    CancellationToken cancellationToken = default);

Task WriteDWordsSingleRequestAsync(
    string start,
    IReadOnlyList<uint> values,
    CancellationToken cancellationToken = default);
```

### 4.2 Semantic-atomic behavior

Use this when the caller cares about logical value integrity but accepts documented protocol boundaries.

- do not split one logical `DWord` / `Float32`
- do not split one caller-visible logical block through hidden fallback logic
- if one request cannot preserve semantics, return an error

### 4.3 Explicit chunked behavior

Use this only when the caller explicitly opts into segmentation.

```csharp
Task<ushort[]> ReadWordsChunkedAsync(
    string start,
    int count,
    int maxWordsPerRequest,
    CancellationToken cancellationToken = default);

Task<uint[]> ReadDWordsChunkedAsync(
    string start,
    int count,
    int maxDwordsPerRequest,
    CancellationToken cancellationToken = default);

Task WriteWordsChunkedAsync(
    string start,
    IReadOnlyList<ushort> values,
    int maxWordsPerRequest,
    CancellationToken cancellationToken = default);

Task WriteDWordsChunkedAsync(
    string start,
    IReadOnlyList<uint> values,
    int maxDwordsPerRequest,
    CancellationToken cancellationToken = default);
```

## 5. Atomicity Rules

These rules are normative.

- `ReadTypedAsync`, `WriteTypedAsync`, `WriteBitInWordAsync`, and `ReadNamedAsync` must preserve logical value integrity
- default APIs must not silently split one logical request into different semantics after an error
- fallback retry with a different write shape must be opt-in and explicitly named
- if the library cannot preserve the requested semantics, it should return an error

For Host Link specifically:

- typed `DWord` and `Float32` access must keep low/high-word pairing intact
- contiguous write helpers must not silently degrade into smaller writes unless the caller explicitly selected a chunked API

## 6. Address Helper Contract

String address handling should be public and reusable instead of duplicated in UI or adapter code.

Target shape:

```csharp
public static class KvHostLinkAddress
{
    public static bool TryParse(string text, out KvDeviceAddress address);
    public static KvDeviceAddress Parse(string text);
    public static string Format(KvDeviceAddress address);
    public static string Normalize(string text);
}
```

High-level logical address helpers should remain available for:

- `DM100`
- `DM100:S`
- `DM100:D`
- `DM100:L`
- `DM100:F`
- `DM100.3`

## 7. Error Contract

- invalid address text should fail deterministically during parsing
- unsupported dtype should fail before any transport call
- operations that require preserved semantics should fail instead of silently degrading into chunked behavior
- protocol and device errors should stay visible to callers

## 8. Non-Goals

- no hidden fallback splitting
- no requirement to preserve old extension-method naming if a cleaner public surface is chosen
