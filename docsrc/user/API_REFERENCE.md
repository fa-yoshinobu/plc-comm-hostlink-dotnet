# KV Host Link .NET API Reference

This page is generated from the `PlcComm.KvHostLink` assembly public API and XML documentation comments.

Every public PLC communication method is a single-request operation unless it is explicitly named `ReadNamedAsync` or `PollAsync`. Those two read-only methods are non-atomic aggregate operations and may issue multiple sequential requests while retaining one client FIFO turn. Lifecycle, parsing, profile, and offline catalog members issue no PLC protocol request.

Run `python scripts/generate_api_reference.py --help` from the repository root to regenerate it.

## PlcComm.KvHostLink

### HostLinkClosedError

```csharp
public sealed class HostLinkClosedError
```

Thrown when `Close` retires the connection generation that owns an active or queued operation.

#### Members

##### HostLinkClosedError

```csharp
public HostLinkClosedError()
```

##### HostLinkClosedError

```csharp
public HostLinkClosedError(string message, Exception inner)
```

### HostLinkCommentEncoding

```csharp
public enum HostLinkCommentEncoding
```

Explicit text encoding used to decode an RDC device-comment payload.

#### Members

##### Cp932

```csharp
public const HostLinkCommentEncoding Cp932
```

Strict Windows code page 932 (CP932/Windows-31J), used as the compatibility selection for KEYENCE material that describes text as Shift_JIS. Strict decoding accepts mapped Windows extension pairs but rejects forbidden singleton bytes, incomplete sequences, and unassigned pairs.

##### Utf8

```csharp
public const HostLinkCommentEncoding Utf8
```

Strict UTF-8 without malformed-byte replacement.

### HostLinkConnectionError

```csharp
public class HostLinkConnectionError
```

Thrown when a connection error occurs.

#### Members

##### HostLinkConnectionError

```csharp
public HostLinkConnectionError(string message)
```

##### HostLinkConnectionError

```csharp
public HostLinkConnectionError(string message, Exception inner)
```

### HostLinkError

```csharp
public class HostLinkError
```

Base exception for Host Link communication.

#### Members

##### HostLinkError

```csharp
public HostLinkError(string message)
```

##### HostLinkError

```csharp
public HostLinkError(string message, Exception inner)
```

##### HostLinkError

```csharp
public HostLinkError(string message, string code, string response)
```

##### Code

```csharp
public string Code { get; }
```

##### Response

```csharp
public string Response { get; }
```

### HostLinkNotConnectedError

```csharp
public sealed class HostLinkNotConnectedError
```

Thrown when a command is attempted before an explicit open or after the transport was closed.

#### Members

##### HostLinkNotConnectedError

```csharp
public HostLinkNotConnectedError()
```

### HostLinkOutcomeUnknownError

```csharp
public sealed class HostLinkOutcomeUnknownError
```

Thrown when a state-changing request may have reached the PLC but no definitive result was received.

#### Members

##### HostLinkOutcomeUnknownError

```csharp
public HostLinkOutcomeUnknownError(string message, HostLinkOutcomeUnknownReason reason, Exception inner)
```

##### Reason

```csharp
public HostLinkOutcomeUnknownReason Reason { get; }
```

Gets the structured cause category for the ambiguous result.

### HostLinkOutcomeUnknownReason

```csharp
public enum HostLinkOutcomeUnknownReason
```

Machine-readable reason retained by `HostLinkOutcomeUnknownError`.

#### Members

##### CallerCancellation

```csharp
public const HostLinkOutcomeUnknownReason CallerCancellation
```

The caller cancelled after transmission may have begun.

##### ConnectionClosed

```csharp
public const HostLinkOutcomeUnknownReason ConnectionClosed
```

The connection was closed after transmission may have begun.

##### InvalidResponse

```csharp
public const HostLinkOutcomeUnknownReason InvalidResponse
```

A response could not prove whether the state-changing command completed.

##### Timeout

```csharp
public const HostLinkOutcomeUnknownReason Timeout
```

The transaction deadline expired after transmission may have begun.

##### TransportFailure

```csharp
public const HostLinkOutcomeUnknownReason TransportFailure
```

A transport failure occurred after transmission may have begun.

### HostLinkProtocolError

```csharp
public class HostLinkProtocolError
```

Thrown when there is an error in the protocol or unexpected response.

#### Members

##### HostLinkProtocolError

```csharp
public HostLinkProtocolError(string message)
```

##### HostLinkProtocolError

```csharp
public HostLinkProtocolError(string message, Exception inner)
```

### HostLinkReentrancyError

```csharp
public sealed class HostLinkReentrancyError
```

Thrown when public client code attempts to enter the same client recursively.

#### Members

##### HostLinkReentrancyError

```csharp
public HostLinkReentrancyError()
```

### HostLinkTimeoutError

```csharp
public sealed class HostLinkTimeoutError
```

Thrown when the library's configured absolute transaction deadline expires while the operation still has a known read-only or pre-send outcome.

#### Members

##### HostLinkTimeoutError

```csharp
public HostLinkTimeoutError(string message)
```

##### HostLinkTimeoutError

```csharp
public HostLinkTimeoutError(string message, Exception inner)
```

### HostLinkTrafficStats

```csharp
public struct HostLinkTrafficStats
```

Immutable lifetime traffic counters for one Host Link client. TCP receive bytes count the body and first CR/LF terminator; UDP receive bytes count the complete datagram.

#### Members

##### HostLinkTrafficStats

```csharp
public HostLinkTrafficStats(ulong RequestCount, ulong TxBytes, ulong RxBytes)
```

Immutable lifetime traffic counters for one Host Link client. TCP receive bytes count the body and first CR/LF terminator; UDP receive bytes count the complete datagram.

##### RequestCount

```csharp
public ulong RequestCount { get; init; }
```

##### RxBytes

```csharp
public ulong RxBytes { get; init; }
```

##### TxBytes

```csharp
public ulong TxBytes { get; init; }
```

### HostLinkTransportMode

```csharp
public enum HostLinkTransportMode
```

Transport protocol for Host Link communication.

#### Members

##### Tcp

```csharp
public const HostLinkTransportMode Tcp
```

##### Udp

```csharp
public const HostLinkTransportMode Udp
```

### KvDeviceAddress

```csharp
public class KvDeviceAddress
```

#### Members

##### KvDeviceAddress

```csharp
public KvDeviceAddress(string DeviceType, int Number, string Suffix = "")
```

##### ToText

```csharp
public string ToText()
```

##### DeviceType

```csharp
public string DeviceType { get; init; }
```

##### Number

```csharp
public int Number { get; init; }
```

##### Suffix

```csharp
public string Suffix { get; init; }
```

### KvDeviceRangeCatalog

```csharp
public sealed class KvDeviceRangeCatalog
```

#### Members

##### KvDeviceRangeCatalog

```csharp
public KvDeviceRangeCatalog(string PlcProfile, string ModelCode, bool HasModelCode, string RequestedPlcProfile, string ResolvedPlcProfile, IReadOnlyList<KvDeviceRangeEntry> Entries)
```

##### Entry

```csharp
public KvDeviceRangeEntry Entry(string deviceType)
```

##### Entries

```csharp
public IReadOnlyList<KvDeviceRangeEntry> Entries { get; init; }
```

##### HasModelCode

```csharp
public bool HasModelCode { get; init; }
```

##### ModelCode

```csharp
public string ModelCode { get; init; }
```

##### PlcProfile

```csharp
public string PlcProfile { get; init; }
```

##### RequestedPlcProfile

```csharp
public string RequestedPlcProfile { get; init; }
```

##### ResolvedPlcProfile

```csharp
public string ResolvedPlcProfile { get; init; }
```

### KvDeviceRangeCategory

```csharp
public enum KvDeviceRangeCategory
```

#### Members

##### Bit

```csharp
public const KvDeviceRangeCategory Bit
```

##### FileRegister

```csharp
public const KvDeviceRangeCategory FileRegister
```

##### Index

```csharp
public const KvDeviceRangeCategory Index
```

##### TimerCounter

```csharp
public const KvDeviceRangeCategory TimerCounter
```

##### Word

```csharp
public const KvDeviceRangeCategory Word
```

### KvDeviceRangeEntry

```csharp
public sealed class KvDeviceRangeEntry
```

#### Members

##### KvDeviceRangeEntry

```csharp
public KvDeviceRangeEntry(string Device, string DeviceType, KvDeviceRangeCategory Category, bool IsBitDevice, KvDeviceRangeNotation Notation, bool Supported, uint LowerBound, uint? UpperBound, uint? PointCount, string AddressRange, string Source, string Notes, IReadOnlyList<KvDeviceRangeSegment> Segments)
```

##### AddressRange

```csharp
public string AddressRange { get; init; }
```

##### Category

```csharp
public KvDeviceRangeCategory Category { get; init; }
```

##### Device

```csharp
public string Device { get; init; }
```

##### DeviceType

```csharp
public string DeviceType { get; init; }
```

##### IsBitDevice

```csharp
public bool IsBitDevice { get; init; }
```

##### LowerBound

```csharp
public uint LowerBound { get; init; }
```

##### Notation

```csharp
public KvDeviceRangeNotation Notation { get; init; }
```

##### Notes

```csharp
public string Notes { get; init; }
```

##### PointCount

```csharp
public uint? PointCount { get; init; }
```

##### Segments

```csharp
public IReadOnlyList<KvDeviceRangeSegment> Segments { get; init; }
```

##### Source

```csharp
public string Source { get; init; }
```

##### Supported

```csharp
public bool Supported { get; init; }
```

##### UpperBound

```csharp
public uint? UpperBound { get; init; }
```

### KvDeviceRangeNotation

```csharp
public enum KvDeviceRangeNotation
```

#### Members

##### Decimal

```csharp
public const KvDeviceRangeNotation Decimal
```

##### Hexadecimal

```csharp
public const KvDeviceRangeNotation Hexadecimal
```

### KvDeviceRangeSegment

```csharp
public sealed class KvDeviceRangeSegment
```

#### Members

##### KvDeviceRangeSegment

```csharp
public KvDeviceRangeSegment(string Device, KvDeviceRangeCategory Category, bool IsBitDevice, KvDeviceRangeNotation Notation, uint LowerBound, uint? UpperBound, uint? PointCount, string AddressRange)
```

##### AddressRange

```csharp
public string AddressRange { get; init; }
```

##### Category

```csharp
public KvDeviceRangeCategory Category { get; init; }
```

##### Device

```csharp
public string Device { get; init; }
```

##### IsBitDevice

```csharp
public bool IsBitDevice { get; init; }
```

##### LowerBound

```csharp
public uint LowerBound { get; init; }
```

##### Notation

```csharp
public KvDeviceRangeNotation Notation { get; init; }
```

##### PointCount

```csharp
public uint? PointCount { get; init; }
```

##### UpperBound

```csharp
public uint? UpperBound { get; init; }
```

### KvHostLinkAddress

```csharp
public static class KvHostLinkAddress
```

Public address helpers for Host Link device strings and logical helper addresses.

Remarks: These helpers separate base device parsing from logical high-level helper parsing so generated docs can explain exactly when a string refers to a raw PLC device versus a typed logical view.

#### Members

##### Format

```csharp
public static string Format(KvDeviceAddress address)
```

Formats a base device address to canonical text.

Returns: Canonical uppercase Host Link device text.

Parameters:
- `address`: The parsed base address.

##### Normalize

```csharp
public static string Normalize(string text)
```

Normalizes either a base device address or a logical helper address.

Returns: The canonical uppercase helper text.

Parameters:
- `text`: Input text in either base-device or logical-helper form.

##### NormalizeLogical

```csharp
public static string NormalizeLogical(string text)
```

Normalizes a logical helper address to canonical text.

Returns: Canonical helper text returned by `ToText`.

Parameters:
- `text`: Logical helper text in any supported spelling.

##### Parse

```csharp
public static KvDeviceAddress Parse(string text)
```

Parses a base device address.

Returns: The parsed base device address.

Parameters:
- `text`: Base device text such as `DM100` or `MR0A`.

##### ParseLogical

```csharp
public static KvLogicalAddress ParseLogical(string text)
```

Parses a logical helper address such as `DM100:U`, `DM100:F`, `R100:BIT`, `DM100:COMMENT`, or `DM100.A`.

Returns: The normalized logical address.

Parameters:
- `text`: Logical helper text to parse.

##### TryParse

```csharp
public static bool TryParse(string text, out KvDeviceAddress address)
```

Attempts to parse a base device address.

Returns: `true` when parsing succeeds; otherwise `false`.

Parameters:
- `text`: Base device text to parse.
- `address`: When this method returns `true`, receives the parsed base address.

##### TryParseLogical

```csharp
public static bool TryParseLogical(string text, out KvLogicalAddress address)
```

Attempts to parse a logical helper address.

Returns: `true` when parsing succeeds; otherwise `false`.

Parameters:
- `text`: Logical helper text to parse.
- `address`: When this method returns `true`, receives the normalized logical address.

### KvHostLinkClient

```csharp
public sealed class KvHostLinkClient
```

A low-level Host Link (Upper Link) client for KEYENCE KV series PLCs.

Remarks: Public operations enter one arrival-order FIFO queue. One client therefore owns at most one active wire transaction, and an explicitly aggregate read retains the same turn for its complete plan. Queue waiting does not consume the transaction timeout. Waiting cancellation sends nothing. Recursive entry on the same client is rejected. For application code, prefer `OpenAndConnectAsync`.

#### Members

##### KvHostLinkClient

```csharp
public KvHostLinkClient(string host, int port, HostLinkTransportMode transportMode, string plcProfile)
```

##### ChangeModeAsync

```csharp
public Task ChangeModeAsync(KvPlcMode mode, CancellationToken cancellationToken = default)
```

##### CheckErrorNoAsync

```csharp
public Task<string> CheckErrorNoAsync(CancellationToken cancellationToken = default)
```

##### ClearErrorAsync

```csharp
public Task ClearErrorAsync(CancellationToken cancellationToken = default)
```

##### Close

```csharp
public void Close()
```

Closes the transport and interrupts active I/O.

##### CloseAsync

```csharp
public Task CloseAsync()
```

Closes the transport, promptly interrupts active I/O, and asynchronously awaits cleanup.

##### ConfirmOperatingModeAsync

```csharp
public Task<KvPlcMode> ConfirmOperatingModeAsync(CancellationToken cancellationToken = default)
```

##### Dispose

```csharp
public void Dispose()
```

Closes the transport, interrupts active I/O, and disposes the client.

##### DisposeAsync

```csharp
public ValueTask DisposeAsync()
```

Closes the transport, interrupts active I/O, and asynchronously disposes the client.

##### ForcedResetAsync

```csharp
public Task ForcedResetAsync(string device, CancellationToken cancellationToken = default)
```

##### ForcedResetConsecutiveAsync

```csharp
public Task ForcedResetConsecutiveAsync(string device, int count, CancellationToken cancellationToken = default)
```

Consecutively force-resets up to 16 bit devices starting at `device` (RSS command).

##### ForcedSetAsync

```csharp
public Task ForcedSetAsync(string device, CancellationToken cancellationToken = default)
```

##### ForcedSetConsecutiveAsync

```csharp
public Task ForcedSetConsecutiveAsync(string device, int count, CancellationToken cancellationToken = default)
```

Consecutively force-sets up to 16 bit devices starting at `device` (STS command).

##### Open

```csharp
public void Open()
```

Opens the configured transport synchronously without retrying.

##### OpenAsync

```csharp
public Task OpenAsync(CancellationToken cancellationToken = default)
```

Opens the configured transport without retrying.

Remarks: An internal connect timeout throws `HostLinkTimeoutError`; caller cancellation throws `OperationCanceledException`. UDP open resolves the IPv4 endpoint once and creates a connected socket. Successful requests reuse that socket. An anomalous exchange discards only the socket; the next request creates a replacement from the retained endpoint without DNS resolution or an automatic request retry.

##### QueryModelAsync

```csharp
public Task<KvModelInfo> QueryModelAsync(CancellationToken cancellationToken = default)
```

##### ReadAsync

```csharp
public Task<string[]> ReadAsync(string device, string dataFormat, CancellationToken cancellationToken = default)
```

Reads one device with an explicit Host Link numeric format.

Remarks: Timer/counter reads return three tokens. The first token is the PLC's structural status and remains the exact `0` or `1`; the selected numeric format applies only to the current and preset tokens. In particular, hexadecimal reads normalize only those two values to four uppercase digits and never synthesize `0000` or `0001` for the status.

##### ReadAsync

```csharp
public Task<string[]> ReadAsync(string device, CancellationToken cancellationToken = default)
```

Reads one device using its canonical Host Link format.

Remarks: Timer/counter reads return three tokens. The first token is the PLC's structural status and remains the exact `0` or `1`; numeric parsing applies only to the current and preset tokens.

##### ReadCommentBytesAsync

```csharp
public Task<byte[]> ReadCommentBytesAsync(string device, CancellationToken cancellationToken = default)
```

Reads one RDC device comment as exact response-body bytes.

Returns: The exact RDC payload after the Host Link CR/LF frame terminator is removed. Trailing ASCII padding spaces are retained.

Parameters:
- `device`: Base device whose comment is read.
- `cancellationToken`: Cancellation token.

##### ReadCommentsAsync

```csharp
public Task<string> ReadCommentsAsync(string device, HostLinkCommentEncoding encoding, CancellationToken cancellationToken = default)
```

Reads and strictly decodes one RDC device comment.

Remarks: Decoding is strict. Malformed input raises `HostLinkProtocolError` and retires the connection; the library never guesses or falls back to another encoding.

Returns: Decoded comment text with trailing ASCII padding spaces removed.

Parameters:
- `device`: Base device whose comment is read.
- `encoding`: Explicit text encoding. `Cp932` is CP932/Windows-31J and is the compatibility selection for KEYENCE material that describes text as Shift_JIS.
- `cancellationToken`: Cancellation token.

##### ReadConsecutiveAsync

```csharp
public Task<string[]> ReadConsecutiveAsync(string device, int count, string dataFormat, CancellationToken cancellationToken = default)
```

##### ReadConsecutiveAsync

```csharp
public Task<string[]> ReadConsecutiveAsync(string device, int count, CancellationToken cancellationToken = default)
```

##### ReadConsecutiveLegacyAsync

```csharp
public Task<string[]> ReadConsecutiveLegacyAsync(string device, int count, string dataFormat, CancellationToken cancellationToken = default)
```

Reads consecutive devices using the legacy RDE command. Prefer `ReadConsecutiveAsync` on current models.

##### ReadExpansionUnitBufferAsync

```csharp
public Task<string[]> ReadExpansionUnitBufferAsync(int unitNo, int address, int count, string dataFormat, CancellationToken cancellationToken = default)
```

Reads buffer memory from an expansion unit (URD command).

Parameters:
- `unitNo`: Unit number (0–48).
- `address`: Buffer address (0–59999).
- `count`: Number of values to read.
- `dataFormat`: Required data format suffix, e.g. ".U" or ".S".
- `cancellationToken`: Cancellation token.

##### ReadMonitorBitsAsync

```csharp
public Task<string[]> ReadMonitorBitsAsync(CancellationToken cancellationToken = default)
```

##### ReadMonitorWordsAsync

```csharp
public Task<string[]> ReadMonitorWordsAsync(CancellationToken cancellationToken = default)
```

##### RegisterMonitorBitsAsync

```csharp
public Task RegisterMonitorBitsAsync(IEnumerable<string> devices, CancellationToken cancellationToken = default)
```

##### RegisterMonitorWordsAsync

```csharp
public Task RegisterMonitorWordsAsync(IEnumerable<KvMonitorWordTarget> devices, CancellationToken cancellationToken = default)
```

##### SetTimeAsync

```csharp
public Task SetTimeAsync(DateTime value, CancellationToken cancellationToken = default)
```

Sets the PLC clock from an explicit local calendar value in years 2000 through 2099.

##### SwitchBankAsync

```csharp
public Task SwitchBankAsync(int bankNo, CancellationToken cancellationToken = default)
```

Switches the active data bank (BE command). Valid range: 0–15.

##### WriteAsync

```csharp
public Task WriteAsync(string device, bool value, CancellationToken cancellationToken = default)
```

Writes one direct bit in one request using the exact Boolean-only bit-value contract.

##### WriteAsync

```csharp
public Task WriteAsync<T>(string device, T value, string dataFormat, CancellationToken cancellationToken = default)
```

##### WriteAsync

```csharp
public Task WriteAsync<T>(string device, T value, CancellationToken cancellationToken = default)
```

##### WriteConsecutiveAsync

```csharp
public Task WriteConsecutiveAsync(string device, IEnumerable<bool> values, CancellationToken cancellationToken = default)
```

Writes consecutive direct bits in one request from an immutable Boolean-value snapshot.

##### WriteConsecutiveAsync

```csharp
public Task WriteConsecutiveAsync<T>(string device, IEnumerable<T> values, string dataFormat, CancellationToken cancellationToken = default)
```

##### WriteConsecutiveAsync

```csharp
public Task WriteConsecutiveAsync<T>(string device, IEnumerable<T> values, CancellationToken cancellationToken = default)
```

##### WriteConsecutiveLegacyAsync

```csharp
public Task WriteConsecutiveLegacyAsync<T>(string device, IEnumerable<T> values, string dataFormat, CancellationToken cancellationToken = default)
```

Writes consecutive devices using the legacy WRE command. Prefer `WriteConsecutiveAsync` on current models.

##### WriteExpansionUnitBufferAsync

```csharp
public Task WriteExpansionUnitBufferAsync<T>(int unitNo, int address, IEnumerable<T> values, string dataFormat, CancellationToken cancellationToken = default)
```

Writes buffer memory to an expansion unit (UWR command).

Parameters:
- `unitNo`: Unit number (0–48).
- `address`: Buffer address (0–59999).
- `values`: Values to write.
- `dataFormat`: Required data format suffix, e.g. ".U" or ".S".
- `cancellationToken`: Cancellation token.

##### WriteSetValueAsync

```csharp
public Task WriteSetValueAsync<T>(string device, T value, string dataFormat, CancellationToken cancellationToken = default)
```

Writes a set-value (preset) for a timer or counter device (WS command). Supported device types: T, C.

##### WriteSetValueConsecutiveAsync

```csharp
public Task WriteSetValueConsecutiveAsync<T>(string device, IEnumerable<T> values, string dataFormat, CancellationToken cancellationToken = default)
```

Writes set-values (presets) for consecutive timer or counter devices (WSS command). Supported device types: T, C.

##### IsOpen

```csharp
public bool IsOpen { get; }
```

Gets whether TCP is connected or a resolved UDP logical endpoint remains open.

##### PlcProfile

```csharp
public string PlcProfile { get; }
```

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

Gets or sets the operation timeout from 1 through `MaxValue` milliseconds.

##### TrafficStats

```csharp
public HostLinkTrafficStats TrafficStats { get; }
```

Gets an immutable snapshot of cumulative traffic for this client lifetime.

### KvHostLinkClientExtensions

```csharp
public static class KvHostLinkClientExtensions
```

High-level helper API for `KvHostLinkClient`.

Remarks: These extension methods are the recommended user-facing surface for normal application code. They wrap the token-oriented low-level client API with typed reads and writes, named aggregate reads, polling, and one-step connection setup. The ordinary client keeps every compound read plan exclusive through its built-in FIFO queue.

#### Members

##### OpenAndConnectAsync

```csharp
public static Task<KvHostLinkClient> OpenAndConnectAsync(string host, int port, HostLinkTransportMode transport, string plcProfile, CancellationToken ct = default)
```

Creates a Host Link client with built-in FIFO admission and opens the connection.

Remarks: This is the recommended convenience entry point for high-level application code that does not need to construct `KvHostLinkConnectionOptions` manually.

Returns: A connected ordinary client that is safe to share across async callers.

Parameters:
- `host`: PLC IPv4 address or hostname that resolves to IPv4.
- `port`: Required KV Host Link TCP/UDP port.
- `transport`: Required TCP or UDP transport.
- `plcProfile`: Canonical KEYENCE KV PLC profile for the session.
- `ct`: Cancellation token.

##### PollAsync

```csharp
public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(KvHostLinkClient client, IEnumerable<string> addresses, TimeSpan interval, HostLinkCommentEncoding commentEncoding, CancellationToken ct = default)
```

Polls named values with an explicit strict codec for every RDC comment entry.

Remarks: The complete plan is validated and copied before its first send. Every cycle retains one FIFO turn and returns either the full non-atomic result or an error. At least one address must use `:COMMENT`; an otherwise unused comment encoding is rejected during complete preflight before transport.

Returns: One non-atomic, all-or-error aggregate result per cycle.

Parameters:
- `client`: The client to use.
- `addresses`: Named addresses containing at least one `:COMMENT` entry.
- `interval`: Strictly positive time between polls.
- `commentEncoding`: Explicit strict codec for every RDC comment.
- `ct`: Cancellation token to stop polling.

##### PollAsync

```csharp
public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(KvHostLinkClient client, IEnumerable<string> addresses, TimeSpan interval, CancellationToken ct = default)
```

Continuously polls the specified addresses and yields one non-atomic aggregate result each cycle.

Remarks: The validated compiled read plan is reused on every iteration for lower per-cycle overhead. Every cycle has the same input-order result, indivisible-value, no-interleaving, and no-partial-result contract as `ReadNamedAsync`. The interval is a completion delay outside the client FIFO turn; cycles never overlap or catch up. This overload accepts no implicit comment codec. A plan containing `:COMMENT` is rejected during complete preflight before its first send; use the overload that requires `HostLinkCommentEncoding`.

Parameters:
- `client`: The client to use.
- `addresses`: Address strings in the same format as `ReadNamedAsync`.
- `interval`: Strictly positive time between polls.
- `ct`: Cancellation token to stop polling.

##### ReadCounterAsync

```csharp
public static Task<KvTimerCounterValue> ReadCounterAsync(KvHostLinkClient client, string device, CancellationToken ct = default)
```

Reads a counter composite value.

##### ReadDWordsAsync

```csharp
public static Task<uint[]> ReadDWordsAsync(KvHostLinkClient client, string device, int count, CancellationToken ct = default)
```

Reads contiguous unsigned 32-bit values starting at `device`.

Remarks: This helper preserves single-request semantics by delegating to `ReadDWordsSingleRequestAsync`.

Returns: Unsigned 32-bit values in logical device order.

Parameters:
- `client`: The client to use.
- `device`: Starting device address (for example `"DM0"`).
- `count`: Number of 32-bit values to read.
- `ct`: Cancellation token.

##### ReadDWordsSingleRequestAsync

```csharp
public static Task<uint[]> ReadDWordsSingleRequestAsync(KvHostLinkClient client, string device, int count, CancellationToken ct = default)
```

Reads contiguous unsigned 32-bit values using one protocol request or returns an error.

Remarks: Use this helper when the logical range must stay atomic.

Returns: The contiguous 32-bit values read by one request.

Parameters:
- `client`: Connected Host Link client.
- `device`: Start device address.
- `count`: Number of 32-bit values to read.
- `ct`: Cancellation token.

##### ReadNamedAsync

```csharp
public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(KvHostLinkClient client, IEnumerable<string> addresses, HostLinkCommentEncoding commentEncoding, CancellationToken ct = default)
```

Reads multiple independent named values with an explicit RDC comment encoding.

Remarks: This overload has the same complete-plan, input-order, one-FIFO-turn, non-atomic, and no-partial-result contract as the non-comment overload. At least one address must use `:COMMENT`; an otherwise unused comment encoding is rejected during complete preflight before transport.

Returns: A dictionary keyed by original input address; no partial result is returned.

Parameters:
- `client`: The client to use.
- `addresses`: Named addresses containing at least one `:COMMENT` entry.
- `commentEncoding`: Explicit strict codec for every RDC comment in the aggregate.
- `ct`: Cancellation token.

##### ReadNamedAsync

```csharp
public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(KvHostLinkClient client, IEnumerable<string> addresses, CancellationToken ct = default)
```

Reads multiple independent named values as one read-only aggregate operation.

Remarks: Address format examples: "DM100:U" -- unsigned 16-bit (ushort) "DM100:F" -- float "DM100:S" -- signed 16-bit (short) "DM100:D" -- unsigned 32-bit "DM100:L" -- signed 32-bit "DM100.3" -- bit 3 within word (bool) "DM100.A" -- bit 10 within word (bool); bits 10-15 use hex digits A-F "DM100:COMMENT" -- PLC device comment text (string) Bit-in-word indices use hexadecimal notation (0-F), matching the KEYENCE address format. Bits 0-9 can be written as decimal digits; bits 10-15 must be written as A-F. For example, bit 12 is addressed as `"DM100.C"`, not `"DM100.12"`. A multi-request result is non-atomic: separate requests can observe different PLC scan times. Each declared scalar, float32 value, or bit-in-word value remains wholly inside one request, but callers requiring one coherent point in time must use a single-request read or a PLC-side snapshot/handshake. The complete plan is validated and copied before the first send, and the client turn is retained until every internal read succeeds or the aggregate fails. The planner groups wire-compatible device families in first-appearance order, sorts addresses within each group, and merges contiguous spans up to the request limit. A non-batchable entry uses its native single read without disabling batching for other groups. Named keys must be semantically unique after device, number, data type, bit index, and scalar count normalization. Spelling-only variants are rejected before transport, while distinct data-type views, bit indices, and overlapping multiword spans remain valid. Returned dictionary keys preserve the original input strings. This overload accepts no implicit comment codec. If an address uses `:COMMENT`, the complete aggregate is rejected before transport; use the overload that requires `HostLinkCommentEncoding`.

Returns: A dictionary keyed by the original input address strings. No partial result is returned on failure.

Parameters:
- `client`: The client to use.
- `addresses`: Address strings that specify both the base device and the desired interpretation.
- `ct`: Cancellation token.

##### ReadTimerAsync

```csharp
public static Task<KvTimerCounterValue> ReadTimerAsync(KvHostLinkClient client, string device, CancellationToken ct = default)
```

Reads a timer composite value.

##### ReadTimerCounterAsync

```csharp
public static Task<KvTimerCounterValue> ReadTimerCounterAsync(KvHostLinkClient client, string device, CancellationToken ct = default)
```

Reads a timer/counter composite value as status, current, and preset. Status must be exactly zero or one.

##### ReadTypedAsync

```csharp
public static Task<object> ReadTypedAsync(KvHostLinkClient client, string device, string dtype, CancellationToken ct = default)
```

Reads a single device value and converts it to a high-level CLR type.

Remarks: The float helper is implemented at the extension layer by reading two consecutive `.U` words and combining them as low-word, high-word. Float32 is valid only for ordinary device families whose canonical default format is one `.U` word.

Returns: A boxed CLR value. Integer formats return boxed integral types and `"F"` returns a boxed `Single`, `"H"` returns a `String`, and `"BIT"` returns a `Boolean`.

Parameters:
- `client`: The client to use.
- `device`: Base device address string, for example `"DM100"`.
- `dtype`: High-level data type code: `"U"` = `UInt16`, `"S"` = `Int16`, `"D"` = `UInt32`, `"L"` = signed 32-bit `Int32`, `"F"` = IEEE 754 float32, `"H"` = hexadecimal 16-bit word text.
- `ct`: Cancellation token.

##### ReadWordsAsync

```csharp
public static Task<ushort[]> ReadWordsAsync(KvHostLinkClient client, string device, int count, CancellationToken ct = default)
```

Reads contiguous unsigned 16-bit words starting at `device`.

Remarks: This helper is the preferred user-facing block-read API for contiguous word devices. It preserves single-request semantics by delegating to `ReadWordsSingleRequestAsync`.

Returns: Unsigned word values in PLC order.

Parameters:
- `client`: The client to use.
- `device`: Starting device address (e.g. `"DM0"`).
- `count`: Number of words to read.
- `ct`: Cancellation token.

##### ReadWordsSingleRequestAsync

```csharp
public static Task<ushort[]> ReadWordsSingleRequestAsync(KvHostLinkClient client, string device, int count, CancellationToken ct = default)
```

Reads contiguous unsigned 16-bit words using one protocol request or returns an error.

Remarks: Use this helper when the logical range must stay atomic.

Returns: The contiguous word values read by one request.

Parameters:
- `client`: Connected Host Link client.
- `device`: Start device address.
- `count`: Number of words to read.
- `ct`: Cancellation token.

##### WriteBitInExpansionUnitBufferAsync

```csharp
public static Task WriteBitInExpansionUnitBufferAsync(KvHostLinkClient client, int unitNo, int address, int bitIndex, bool value, CancellationToken ct = default)
```

Sets or clears one bit in one expansion-unit buffer word through explicit URD/UWR.

Remarks: The route is fixed to the supplied unit and address and the data format is fixed to one 16-bit `.U` value. The complete plan is validated before FIFO admission. After activation, the helper always sends one URD followed by one UWR under one local FIFO turn and one absolute deadline, even when the requested bit is unchanged. It performs no route fallback, retry, or success readback and is not PLC-atomic against expansion-unit logic or another connection. Cancellation before UWR begins sends no write; a failure after UWR transmission may have begun reports outcome unknown and retires the connection.

Parameters:
- `client`: The client to use.
- `unitNo`: Expansion unit number from 0 through 48.
- `address`: Expansion-unit buffer address from 0 through 59999.
- `bitIndex`: The bit index from 0 through 15.
- `value`: The Boolean value to write.
- `ct`: Cancellation token.

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(KvHostLinkClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
```

Sets or clears one bit in a 16-bit word through an explicit read-modify-write operation.

Remarks: The complete target, index, and Boolean value are validated before FIFO admission. After activation, the helper always sends one word read followed by one word write under one local FIFO turn and one absolute deadline, even when the requested bit already has the desired value. Queue wait is outside that deadline. It performs no fallback, retry, or success readback. The operation is not PLC-atomic: PLC logic or another connection can update the word between the two requests, and that update can be lost. Use PLC-side logic, a handshake, or exclusive ownership of the complete word when that risk is unacceptable. Cancellation before the write begins sends no write; cancellation after write transmission may have begun reports an outcome-unknown failure and retires the connection.

Parameters:
- `client`: The client to use.
- `device`: A base 16-bit word-device address such as `DM100`.
- `bitIndex`: The bit index from 0 through 15.
- `value`: The Boolean value to write.
- `ct`: Cancellation token.

##### WriteDWordsSingleRequestAsync

```csharp
public static Task WriteDWordsSingleRequestAsync(KvHostLinkClient client, string device, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes contiguous unsigned 32-bit values using one protocol request or returns an error.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(KvHostLinkClient client, string device, string dtype, bool value, CancellationToken ct = default)
```

Writes a direct bit device in one request using an explicit BIT dtype and Boolean value.

Remarks: Numeric, string, and truthy compatibility values are not accepted.

Parameters:
- `client`: The client to use.
- `device`: Direct-bit device address.
- `dtype`: The exact logical type `BIT`.
- `value`: Boolean bit value.
- `ct`: Cancellation token.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync<T>(KvHostLinkClient client, string device, string dtype, T value, CancellationToken ct = default)
```

Writes a single device value using a high-level data type code.

Remarks: The float helper is implemented at the extension layer by converting a finite input value within the IEEE 754 float32 range and writing two consecutive `.U` words. Float32 is valid only for ordinary device families whose canonical default format is one `.U` word. Direct bit device families cannot represent that two-word value; direct-bit and special-response families are rejected before FIFO admission and transport I/O.

Parameters:
- `client`: The client to use.
- `device`: Base device address string, for example `"DM100"`.
- `dtype`: High-level data type code: `"U"`, `"S"`, `"D"`, `"L"`, `"F"`, or `"H"`.
- `value`: Value to write.
- `ct`: Cancellation token.

##### WriteWordsSingleRequestAsync

```csharp
public static Task WriteWordsSingleRequestAsync(KvHostLinkClient client, string device, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes contiguous unsigned 16-bit values using one protocol request or returns an error.

### KvHostLinkClientFactory

```csharp
public static class KvHostLinkClientFactory
```

Factory helpers for opening ready-to-use Host Link clients.

Remarks: The factory centralizes validation of host, port, transport, and timeout behavior so samples and generated docs can point to one explicit connection entry point.

#### Members

##### OpenAndConnectAsync

```csharp
public static Task<KvHostLinkClient> OpenAndConnectAsync(KvHostLinkConnectionOptions options, CancellationToken cancellationToken = default)
```

Creates, configures, and opens a Host Link client with built-in FIFO admission.

Remarks: The ordinary client owns the one FIFO queue used by all low-level and high-level operations. Hostname resolution selects IPv4 only and never falls back to IPv6.

Returns: A connected Host Link client.

Parameters:
- `options`: Explicit connection options.
- `cancellationToken`: Cancellation token.

### KvHostLinkConnectionOptions

```csharp
public sealed class KvHostLinkConnectionOptions
```

Explicit connection options for a Host Link session.

Remarks: This type is intended for the unified high-level connection flow so generated documentation can describe transport, timeout, profile, and framing behavior in one place.

#### Members

##### KvHostLinkConnectionOptions

```csharp
public KvHostLinkConnectionOptions(string Host, int Port, HostLinkTransportMode Transport, string PlcProfile, TimeSpan? Timeout = null)
```

Explicit connection options for a Host Link session.

Remarks: This type is intended for the unified high-level connection flow so generated documentation can describe transport, timeout, profile, and framing behavior in one place.

Parameters:
- `Host`: Unbracketed PLC IPv4 address or hostname that resolves to IPv4. IPv6 and bracketed IPv4 literals are not supported.
- `Transport`: Transport protocol.
- `PlcProfile`: Canonical KEYENCE KV PLC profile for the session.
- `Port`: Host Link port number.
- `Timeout`: Operation timeout from 1 millisecond through `MaxValue` milliseconds. Omit it to use three seconds.

##### EffectiveTimeout

```csharp
public TimeSpan EffectiveTimeout { get; }
```

Gets the effective timeout used for a new client instance.

Remarks: Host Link callers may leave `Timeout` at its default value and use this property when they need the resolved timeout that will be applied to the client.

##### Host

```csharp
public string Host { get; init; }
```

Gets the validated unbracketed PLC IPv4 address or hostname.

##### PlcProfile

```csharp
public string PlcProfile { get; init; }
```

Gets or initializes the canonical KEYENCE KV PLC profile for the session.

##### Port

```csharp
public int Port { get; init; }
```

Gets the validated Host Link port.

##### Timeout

```csharp
public TimeSpan? Timeout { get; init; }
```

Gets the optional communication timeout in the supported 1 through `MaxValue` millisecond range.

##### Transport

```csharp
public HostLinkTransportMode Transport { get; init; }
```

Gets the explicitly selected transport.

### KvHostLinkDevice

```csharp
public static class KvHostLinkDevice
```

#### Members

##### NormalizeSuffix

```csharp
public static string NormalizeSuffix(string suffix)
```

##### ParseDevice

```csharp
public static KvDeviceAddress ParseDevice(string text)
```

Parses a Host Link device token with an explicit device type.

##### RequireExplicitFormat

```csharp
public static string RequireExplicitFormat(KvDeviceAddress address, string dataFormat)
```

##### ValidateDeviceCount

```csharp
public static void ValidateDeviceCount(string deviceType, string effectiveFormat, int count)
```

##### ValidateDeviceSpan

```csharp
public static void ValidateDeviceSpan(string deviceType, int startNumber, string effectiveFormat, int count = 1)
```

##### ValidateDeviceType

```csharp
public static void ValidateDeviceType(string command, string deviceType, HashSet<string> allowedTypes)
```

##### ValidateExpansionBufferCount

```csharp
public static void ValidateExpansionBufferCount(string effectiveFormat, int count)
```

##### ValidateExpansionBufferSpan

```csharp
public static void ValidateExpansionBufferSpan(int address, string effectiveFormat, int count)
```

### KvHostLinkDeviceRanges

```csharp
public static class KvHostLinkDeviceRanges
```

#### Members

##### DeviceRangeCatalogForPlcProfile

```csharp
public static KvDeviceRangeCatalog DeviceRangeCatalogForPlcProfile(string plcProfile)
```

### KvHostLinkPlcProfile

```csharp
public sealed class KvHostLinkPlcProfile
```

#### Members

##### KvHostLinkPlcProfile

```csharp
public KvHostLinkPlcProfile(string Name, string DisplayName)
```

##### DisplayName

```csharp
public string DisplayName { get; init; }
```

##### Name

```csharp
public string Name { get; init; }
```

### KvHostLinkPlcProfileDescriptor

```csharp
public sealed class KvHostLinkPlcProfileDescriptor
```

Canonical metadata used to select and describe one KV Host Link PLC profile.

#### Members

##### KvHostLinkPlcProfileDescriptor

```csharp
public KvHostLinkPlcProfileDescriptor(string CanonicalName, string DisplayName, bool Connectable, string BaseProfile)
```

Canonical metadata used to select and describe one KV Host Link PLC profile.

##### BaseProfile

```csharp
public string BaseProfile { get; init; }
```

##### CanonicalName

```csharp
public string CanonicalName { get; init; }
```

##### Connectable

```csharp
public bool Connectable { get; init; }
```

##### DisplayName

```csharp
public string DisplayName { get; init; }
```

### KvHostLinkPlcProfiles

```csharp
public static class KvHostLinkPlcProfiles
```

#### Members

##### FromName

```csharp
public static KvHostLinkPlcProfile FromName(string plcProfile)
```

##### GetDisplayName

```csharp
public static string GetDisplayName(string plcProfile)
```

##### GetNames

```csharp
public static IReadOnlyList<string> GetNames()
```

##### GetProfileDescriptors

```csharp
public static IReadOnlyList<KvHostLinkPlcProfileDescriptor> GetProfileDescriptors()
```

Return all canonical profiles with display, connection, and base-profile metadata.

##### NormalizeName

```csharp
public static string NormalizeName(string plcProfile)
```

##### Kv3000

```csharp
public static KvHostLinkPlcProfile Kv3000 { get; }
```

##### Kv3000Xym

```csharp
public static KvHostLinkPlcProfile Kv3000Xym { get; }
```

##### Kv5000

```csharp
public static KvHostLinkPlcProfile Kv5000 { get; }
```

##### Kv5000Xym

```csharp
public static KvHostLinkPlcProfile Kv5000Xym { get; }
```

##### Kv7000

```csharp
public static KvHostLinkPlcProfile Kv7000 { get; }
```

##### Kv7000Xym

```csharp
public static KvHostLinkPlcProfile Kv7000Xym { get; }
```

##### Kv8000

```csharp
public static KvHostLinkPlcProfile Kv8000 { get; }
```

##### Kv8000Xym

```csharp
public static KvHostLinkPlcProfile Kv8000Xym { get; }
```

##### KvNano

```csharp
public static KvHostLinkPlcProfile KvNano { get; }
```

##### KvNanoXym

```csharp
public static KvHostLinkPlcProfile KvNanoXym { get; }
```

##### KvX500

```csharp
public static KvHostLinkPlcProfile KvX500 { get; }
```

##### KvX500Xym

```csharp
public static KvHostLinkPlcProfile KvX500Xym { get; }
```

### KvLogicalAddress

```csharp
public struct KvLogicalAddress
```

A normalized logical Host Link address used by the high-level helper layer.

#### Members

##### KvLogicalAddress

```csharp
public KvLogicalAddress(KvDeviceAddress BaseAddress, string DataType, int? BitIndex)
```

A normalized logical Host Link address used by the high-level helper layer.

Parameters:
- `BaseAddress`: Base word device address without a logical suffix.
- `DataType`: Logical data type code such as `U`, `S`, `D`, `L`, `F`, `BIT`, or `COMMENT`.
- `BitIndex`: Bit index inside the base word when the logical address targets a bit-in-word.

##### ToText

```csharp
public string ToText()
```

Formats the logical address using the public helper contract.

Returns: Canonical helper text such as `DM100:U`, `DM100:F`, or `DM100.A`.

##### BaseAddress

```csharp
public KvDeviceAddress BaseAddress { get; init; }
```

Base word device address without a logical suffix.

##### BitIndex

```csharp
public int? BitIndex { get; init; }
```

Bit index inside the base word when the logical address targets a bit-in-word.

##### DataType

```csharp
public string DataType { get; init; }
```

Logical data type code such as `U`, `S`, `D`, `L`, `F`, `BIT`, or `COMMENT`.

##### IsBitInWord

```csharp
public bool IsBitInWord { get; }
```

Gets a value indicating whether this logical address targets a bit inside a word.

### KvModelInfo

```csharp
public class KvModelInfo
```

Information about a PLC model.

#### Members

##### KvModelInfo

```csharp
public KvModelInfo(string Code, string Model)
```

Information about a PLC model.

##### Code

```csharp
public string Code { get; init; }
```

##### Model

```csharp
public string Model { get; init; }
```

### KvMonitorWordTarget

```csharp
public sealed class KvMonitorWordTarget
```

One base device and optional data format used by word monitoring.

#### Members

##### KvMonitorWordTarget

```csharp
public KvMonitorWordTarget(string Device, string DataFormat = null)
```

One base device and optional data format used by word monitoring.

Parameters:
- `Device`: The base device without a data-format suffix.
- `DataFormat`: The explicit numeric data format, or `null` only for a direct-bit device whose bare MWS/MWR representation is an unsigned packed 16-bit word.

##### DataFormat

```csharp
public string DataFormat { get; init; }
```

The explicit numeric data format, or `null` only for a direct-bit device whose bare MWS/MWR representation is an unsigned packed 16-bit word.

##### Device

```csharp
public string Device { get; init; }
```

The base device without a data-format suffix.

### KvPlcMode

```csharp
public enum KvPlcMode
```

PLC operating mode.

#### Members

##### Program

```csharp
public const KvPlcMode Program
```

##### Run

```csharp
public const KvPlcMode Run
```

### KvTimerCounterValue

```csharp
public struct KvTimerCounterValue
```

Composite timer/counter value returned by Host Link T/C reads.

#### Members

##### KvTimerCounterValue

```csharp
public KvTimerCounterValue(uint Status, uint Current, uint Preset)
```

Composite timer/counter value returned by Host Link T/C reads.

##### Current

```csharp
public uint Current { get; init; }
```

##### Preset

```csharp
public uint Preset { get; init; }
```

##### Status

```csharp
public uint Status { get; init; }
```
