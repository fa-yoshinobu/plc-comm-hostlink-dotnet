# KV Host Link .NET API Reference

This page is generated from the `PlcComm.KvHostLink` assembly public API and XML documentation comments.

Run `python scripts/generate_api_reference.py --help` from the repository root to regenerate it.

## PlcComm.KvHostLink

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

### HostLinkTraceDirection

```csharp
public enum HostLinkTraceDirection
```

Direction of a traced frame.

#### Members

##### Send

```csharp
public const HostLinkTraceDirection Send
```

##### Receive

```csharp
public const HostLinkTraceDirection Receive
```

### HostLinkTraceFrame

```csharp
public class HostLinkTraceFrame
```

A raw frame captured by `TraceHook`.

#### Members

##### HostLinkTraceFrame

```csharp
public HostLinkTraceFrame(HostLinkTraceDirection Direction, byte[] Data, DateTime Timestamp)
```

A raw frame captured by `TraceHook`.

##### Direction

```csharp
public HostLinkTraceDirection Direction { get; set; }
```

##### Data

```csharp
public byte[] Data { get; set; }
```

##### Timestamp

```csharp
public DateTime Timestamp { get; set; }
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
public string DeviceType { get; set; }
```

##### Number

```csharp
public int Number { get; set; }
```

##### Suffix

```csharp
public string Suffix { get; set; }
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

##### PlcProfile

```csharp
public string PlcProfile { get; set; }
```

##### ModelCode

```csharp
public string ModelCode { get; set; }
```

##### HasModelCode

```csharp
public bool HasModelCode { get; set; }
```

##### RequestedPlcProfile

```csharp
public string RequestedPlcProfile { get; set; }
```

##### ResolvedPlcProfile

```csharp
public string ResolvedPlcProfile { get; set; }
```

##### Entries

```csharp
public IReadOnlyList<KvDeviceRangeEntry> Entries { get; set; }
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

##### Word

```csharp
public const KvDeviceRangeCategory Word
```

##### TimerCounter

```csharp
public const KvDeviceRangeCategory TimerCounter
```

##### Index

```csharp
public const KvDeviceRangeCategory Index
```

##### FileRegister

```csharp
public const KvDeviceRangeCategory FileRegister
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

##### Device

```csharp
public string Device { get; set; }
```

##### DeviceType

```csharp
public string DeviceType { get; set; }
```

##### Category

```csharp
public KvDeviceRangeCategory Category { get; set; }
```

##### IsBitDevice

```csharp
public bool IsBitDevice { get; set; }
```

##### Notation

```csharp
public KvDeviceRangeNotation Notation { get; set; }
```

##### Supported

```csharp
public bool Supported { get; set; }
```

##### LowerBound

```csharp
public uint LowerBound { get; set; }
```

##### UpperBound

```csharp
public uint? UpperBound { get; set; }
```

##### PointCount

```csharp
public uint? PointCount { get; set; }
```

##### AddressRange

```csharp
public string AddressRange { get; set; }
```

##### Source

```csharp
public string Source { get; set; }
```

##### Notes

```csharp
public string Notes { get; set; }
```

##### Segments

```csharp
public IReadOnlyList<KvDeviceRangeSegment> Segments { get; set; }
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

##### Device

```csharp
public string Device { get; set; }
```

##### Category

```csharp
public KvDeviceRangeCategory Category { get; set; }
```

##### IsBitDevice

```csharp
public bool IsBitDevice { get; set; }
```

##### Notation

```csharp
public KvDeviceRangeNotation Notation { get; set; }
```

##### LowerBound

```csharp
public uint LowerBound { get; set; }
```

##### UpperBound

```csharp
public uint? UpperBound { get; set; }
```

##### PointCount

```csharp
public uint? PointCount { get; set; }
```

##### AddressRange

```csharp
public string AddressRange { get; set; }
```

### KvHostLinkAddress

```csharp
public static class KvHostLinkAddress
```

Public address helpers for Host Link device strings and logical helper addresses.

Remarks: These helpers separate base device parsing from logical high-level helper parsing so generated docs can explain exactly when a string refers to a raw PLC device versus a typed logical view.

#### Members

##### Parse

```csharp
public static KvDeviceAddress Parse(string text)
```

Parses a base device address.

Returns: The parsed base device address.

Parameters:
- `text`: Base device text such as `DM100` or `MR0A`.

##### TryParse

```csharp
public static bool TryParse(string text, out KvDeviceAddress address)
```

Attempts to parse a base device address.

Returns: `true` when parsing succeeds; otherwise `false`.

Parameters:
- `text`: Base device text to parse.
- `address`: When this method returns `true`, receives the parsed base address.

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

##### ParseLogical

```csharp
public static KvLogicalAddress ParseLogical(string text)
```

Parses a logical helper address such as `DM100:U`, `DM100:F`, `R100:BIT`, `DM100:COMMENT`, or `DM100.A`.

Returns: The normalized logical address.

Parameters:
- `text`: Logical helper text to parse.

##### TryParseLogical

```csharp
public static bool TryParseLogical(string text, out KvLogicalAddress address)
```

Attempts to parse a logical helper address.

Returns: `true` when parsing succeeds; otherwise `false`.

Parameters:
- `text`: Logical helper text to parse.
- `address`: When this method returns `true`, receives the normalized logical address.

##### NormalizeLogical

```csharp
public static string NormalizeLogical(string text)
```

Normalizes a logical helper address to canonical text.

Returns: Canonical helper text returned by `ToText`.

Parameters:
- `text`: Logical helper text in any supported spelling.

### KvHostLinkClient

```csharp
public sealed class KvHostLinkClient
```

A low-level Host Link (Upper Link) client for KEYENCE KV series PLCs.

Remarks: This class serializes individual raw requests on one connection, but compound helper workflows such as typed polling and read-modify-write are better served by `QueuedKvHostLinkClient`. For application code, prefer `OpenAndConnectAsync`.

#### Members

##### KvHostLinkClient

```csharp
public KvHostLinkClient(string host, string plcProfile, int port = 8501, HostLinkTransportMode transportMode = Tcp)
```

##### OpenAsync

```csharp
public Task OpenAsync(CancellationToken cancellationToken = default)
```

##### Open

```csharp
public void Open()
```

##### Close

```csharp
public void Close()
```

##### CloseAsync

```csharp
public Task CloseAsync()
```

##### Dispose

```csharp
public void Dispose()
```

##### DisposeAsync

```csharp
public ValueTask DisposeAsync()
```

##### SendRawAsync

```csharp
public Task<string> SendRawAsync(string body, CancellationToken cancellationToken = default)
```

##### ChangeModeAsync

```csharp
public Task ChangeModeAsync(KvPlcMode mode, CancellationToken cancellationToken = default)
```

##### ClearErrorAsync

```csharp
public Task ClearErrorAsync(CancellationToken cancellationToken = default)
```

##### CheckErrorNoAsync

```csharp
public Task<string> CheckErrorNoAsync(CancellationToken cancellationToken = default)
```

##### QueryModelAsync

```csharp
public Task<KvModelInfo> QueryModelAsync(CancellationToken cancellationToken = default)
```

##### ConfirmOperatingModeAsync

```csharp
public Task<KvPlcMode> ConfirmOperatingModeAsync(CancellationToken cancellationToken = default)
```

##### SetTimeAsync

```csharp
public Task SetTimeAsync(DateTime? value = null, CancellationToken cancellationToken = default)
```

##### ForcedSetAsync

```csharp
public Task ForcedSetAsync(string device, CancellationToken cancellationToken = default)
```

##### ForcedResetAsync

```csharp
public Task ForcedResetAsync(string device, CancellationToken cancellationToken = default)
```

##### ReadAsync

```csharp
public Task<string[]> ReadAsync(string device, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### ReadConsecutiveAsync

```csharp
public Task<string[]> ReadConsecutiveAsync(string device, int count, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### WriteAsync

```csharp
public Task WriteAsync<T>(string device, T value, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### WriteConsecutiveAsync

```csharp
public Task WriteConsecutiveAsync<T>(string device, IEnumerable<T> values, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### RegisterMonitorBitsAsync

```csharp
public Task RegisterMonitorBitsAsync(IEnumerable<string> devices, CancellationToken cancellationToken = default)
```

##### RegisterMonitorWordsAsync

```csharp
public Task RegisterMonitorWordsAsync(IEnumerable<string> devices, CancellationToken cancellationToken = default)
```

##### ReadMonitorBitsAsync

```csharp
public Task<string[]> ReadMonitorBitsAsync(CancellationToken cancellationToken = default)
```

##### ReadMonitorWordsAsync

```csharp
public Task<string[]> ReadMonitorWordsAsync(CancellationToken cancellationToken = default)
```

##### ForcedSetConsecutiveAsync

```csharp
public Task ForcedSetConsecutiveAsync(string device, int count, CancellationToken cancellationToken = default)
```

Consecutively force-sets up to 16 bit devices starting at `device` (STS command).

##### ForcedResetConsecutiveAsync

```csharp
public Task ForcedResetConsecutiveAsync(string device, int count, CancellationToken cancellationToken = default)
```

Consecutively force-resets up to 16 bit devices starting at `device` (RSS command).

##### ReadConsecutiveLegacyAsync

```csharp
public Task<string[]> ReadConsecutiveLegacyAsync(string device, int count, string dataFormat = null, CancellationToken cancellationToken = default)
```

Reads consecutive devices using the legacy RDE command. Prefer `ReadConsecutiveAsync` on current models.

##### WriteConsecutiveLegacyAsync

```csharp
public Task WriteConsecutiveLegacyAsync<T>(string device, IEnumerable<T> values, string dataFormat = null, CancellationToken cancellationToken = default)
```

Writes consecutive devices using the legacy WRE command. Prefer `WriteConsecutiveAsync` on current models.

##### WriteSetValueAsync

```csharp
public Task WriteSetValueAsync<T>(string device, T value, string dataFormat = null, CancellationToken cancellationToken = default)
```

Writes a set-value (preset) for a timer or counter device (WS command). Supported device types: T, C.

##### WriteSetValueConsecutiveAsync

```csharp
public Task WriteSetValueConsecutiveAsync<T>(string device, IEnumerable<T> values, string dataFormat = null, CancellationToken cancellationToken = default)
```

Writes set-values (presets) for consecutive timer or counter devices (WSS command). Supported device types: T, C.

##### SwitchBankAsync

```csharp
public Task SwitchBankAsync(int bankNo, CancellationToken cancellationToken = default)
```

Switches the active data bank (BE command). Valid range: 0–15.

##### ReadExpansionUnitBufferAsync

```csharp
public Task<string[]> ReadExpansionUnitBufferAsync(int unitNo, int address, int count, string dataFormat = "", CancellationToken cancellationToken = default)
```

Reads buffer memory from an expansion unit (URD command).

Parameters:
- `unitNo`: Unit number (0–48).
- `address`: Buffer address (0–59999).
- `count`: Number of values to read.
- `dataFormat`: Data format suffix, e.g. ".U" or ".S". Defaults to ".U".
- `cancellationToken`: Cancellation token.

##### WriteExpansionUnitBufferAsync

```csharp
public Task WriteExpansionUnitBufferAsync<T>(int unitNo, int address, IEnumerable<T> values, string dataFormat = "", CancellationToken cancellationToken = default)
```

Writes buffer memory to an expansion unit (UWR command).

Parameters:
- `unitNo`: Unit number (0–48).
- `address`: Buffer address (0–59999).
- `values`: Values to write.
- `dataFormat`: Data format suffix, e.g. ".U" or ".S". Defaults to ".U".
- `cancellationToken`: Cancellation token.

##### ReadCommentsAsync

```csharp
public Task<string> ReadCommentsAsync(string device, bool stripPadding = true, CancellationToken cancellationToken = default)
```

##### PlcProfile

```csharp
public string PlcProfile { get; }
```

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

##### AppendLfOnSend

```csharp
public bool AppendLfOnSend { get; set; }
```

##### TraceHook

```csharp
public Action<HostLinkTraceFrame> TraceHook { get; set; }
```

Optional hook called for every raw frame sent and received. Useful for protocol tracing and debugging.

##### IsOpen

```csharp
public bool IsOpen { get; }
```

### KvHostLinkClientExtensions

```csharp
public static class KvHostLinkClientExtensions
```

High-level helper API for `KvHostLinkClient` and `QueuedKvHostLinkClient`.

Remarks: These extension methods are the recommended user-facing surface for normal application code. They wrap the token-oriented low-level client API with typed reads and writes, bit-in-word helpers, named snapshots, polling, and one-step connection setup. Overloads for `QueuedKvHostLinkClient` keep compound helper operations exclusive when a shared connection is used.

#### Members

##### ReadTypedAsync

```csharp
public static Task<object> ReadTypedAsync(KvHostLinkClient client, string device, string dtype, CancellationToken ct = default)
```

Reads a single device value and converts it to a high-level CLR type.

Remarks: The float helper is implemented at the extension layer by reading two consecutive `.U` words and combining them as low-word, high-word.

Returns: A boxed CLR value. Integer formats return boxed integral types and `"F"` returns a boxed `Single`, and `"H"` returns a `String`.

Parameters:
- `client`: The client to use.
- `device`: Base device address string, for example `"DM100"`.
- `dtype`: High-level data type code: `"U"` = `UInt16`, `"S"` = `Int16`, `"D"` = `UInt32`, `"L"` = signed 32-bit `Int32`, `"F"` = IEEE 754 float32, `"H"` = hexadecimal 16-bit word text.
- `ct`: Cancellation token.

##### ReadTypedAsync

```csharp
public static Task<object> ReadTypedAsync(QueuedKvHostLinkClient client, string device, string dtype, CancellationToken ct = default)
```

Reads a single device value and converts it to a high-level CLR type.

##### ReadTimerCounterAsync

```csharp
public static Task<KvTimerCounterValue> ReadTimerCounterAsync(KvHostLinkClient client, string device, CancellationToken ct = default)
```

Reads a timer/counter composite value as status, current, and preset.

##### ReadTimerCounterAsync

```csharp
public static Task<KvTimerCounterValue> ReadTimerCounterAsync(QueuedKvHostLinkClient client, string device, CancellationToken ct = default)
```

Reads a timer/counter composite value as status, current, and preset.

##### ReadTimerAsync

```csharp
public static Task<KvTimerCounterValue> ReadTimerAsync(KvHostLinkClient client, string device, CancellationToken ct = default)
```

Reads a timer composite value.

##### ReadTimerAsync

```csharp
public static Task<KvTimerCounterValue> ReadTimerAsync(QueuedKvHostLinkClient client, string device, CancellationToken ct = default)
```

Reads a timer composite value.

##### ReadCounterAsync

```csharp
public static Task<KvTimerCounterValue> ReadCounterAsync(KvHostLinkClient client, string device, CancellationToken ct = default)
```

Reads a counter composite value.

##### ReadCounterAsync

```csharp
public static Task<KvTimerCounterValue> ReadCounterAsync(QueuedKvHostLinkClient client, string device, CancellationToken ct = default)
```

Reads a counter composite value.

##### ReadCommentsAsync

```csharp
public static Task<string> ReadCommentsAsync(QueuedKvHostLinkClient client, string device, bool stripPadding = true, CancellationToken ct = default)
```

Reads the configured PLC comment text for one device through the queued helper surface.

Returns: The PLC comment text for `device`.

Parameters:
- `client`: The queued client to use.
- `device`: Base device address such as `"DM100"`.
- `stripPadding`: Whether to trim the Host Link fixed-width trailing spaces.
- `ct`: Cancellation token.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync<T>(KvHostLinkClient client, string device, string dtype, T value, CancellationToken ct = default)
```

Writes a single device value using a high-level data type code.

Remarks: The float helper is implemented at the extension layer by converting the input value to IEEE 754 float32 and writing two consecutive `.U` words.

Parameters:
- `client`: The client to use.
- `device`: Base device address string, for example `"DM100"`.
- `dtype`: High-level data type code: `"U"`, `"S"`, `"D"`, `"L"`, `"F"`, or `"H"`.
- `value`: Value to write.
- `ct`: Cancellation token.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(KvHostLinkClient client, string device, string dtype, string value, CancellationToken ct = default)
```

Writes a hexadecimal word text value using the high-level `"H"` data type code.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync<T>(QueuedKvHostLinkClient client, string device, string dtype, T value, CancellationToken ct = default)
```

Writes a single device value using a high-level data type code.

Remarks: The float helper is implemented at the extension layer by converting the input value to IEEE 754 float32 and writing two consecutive `.U` words.

Parameters:
- `client`: The client to use.
- `device`: Base device address string, for example `"DM100"`.
- `dtype`: High-level data type code: `"U"`, `"S"`, `"D"`, `"L"`, `"F"`, or `"H"`.
- `value`: Value to write.
- `ct`: Cancellation token.

##### WriteTypedAsync

```csharp
public static Task WriteTypedAsync(QueuedKvHostLinkClient client, string device, string dtype, string value, CancellationToken ct = default)
```

Writes a hexadecimal word text value using the high-level `"H"` data type code.

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(KvHostLinkClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
```

Performs a read-modify-write to set or clear a single bit inside a word device.

Remarks: This helper operates on word-oriented devices such as `DM`. It is distinct from PLC force-set / force-reset commands for bit device families.

Parameters:
- `client`: The client to use.
- `device`: Base word device address string, for example `"DM100"`.
- `bitIndex`: Bit position within the word (0–15).
- `value`: New bit value.
- `ct`: Cancellation token.

##### WriteBitInWordAsync

```csharp
public static Task WriteBitInWordAsync(QueuedKvHostLinkClient client, string device, int bitIndex, bool value, CancellationToken ct = default)
```

Performs a read-modify-write to set or clear a single bit inside a word device.

##### ReadNamedAsync

```csharp
public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(KvHostLinkClient client, IEnumerable<string> addresses, CancellationToken ct = default)
```

Reads multiple named values and returns a snapshot dictionary.

Remarks: Address format examples: "DM100:U" -- unsigned 16-bit (ushort) "DM100:F" -- float "DM100:S" -- signed 16-bit (short) "DM100:D" -- unsigned 32-bit "DM100:L" -- signed 32-bit "DM100.3" -- bit 3 within word (bool) "DM100.A" -- bit 10 within word (bool); bits 10-15 use hex digits A-F "DM100:COMMENT" -- PLC device comment text (string) Bit-in-word indices use hexadecimal notation (0-F), matching the KEYENCE address format. Bits 0-9 can be written as decimal digits; bits 10-15 must be written as A-F. For example, bit 12 is addressed as `"DM100.C"`, not `"DM100.12"`. When all requested addresses are compatible with helper-layer batching, this method merges contiguous reads into one or more `RDS` operations. Mixed or non-optimizable address sets fall back to sequential helper reads with the same return shape.

Returns: A dictionary keyed by the original input address strings.

Parameters:
- `client`: The client to use.
- `addresses`: Address strings that specify both the base device and the desired interpretation.
- `ct`: Cancellation token.

##### ReadNamedAsync

```csharp
public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(QueuedKvHostLinkClient client, IEnumerable<string> addresses, CancellationToken ct = default)
```

Reads multiple named values and returns a snapshot dictionary.

##### PollAsync

```csharp
public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(KvHostLinkClient client, IEnumerable<string> addresses, TimeSpan interval, CancellationToken ct = default)
```

Continuously polls the specified addresses and yields a snapshot each cycle.

Remarks: If the address set is batchable, the compiled read plan is reused on every iteration for lower per-cycle overhead.

Parameters:
- `client`: The client to use.
- `addresses`: Address strings in the same format as `ReadNamedAsync`.
- `interval`: Time between polls.
- `ct`: Cancellation token to stop polling.

##### PollAsync

```csharp
public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(QueuedKvHostLinkClient client, IEnumerable<string> addresses, TimeSpan interval, CancellationToken ct = default)
```

Continuously polls the specified addresses and yields a snapshot each cycle.

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

##### ReadWordsSingleRequestAsync

```csharp
public static Task<ushort[]> ReadWordsSingleRequestAsync(QueuedKvHostLinkClient client, string device, int count, CancellationToken ct = default)
```

Reads contiguous unsigned 16-bit words using one protocol request or returns an error.

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

##### ReadDWordsSingleRequestAsync

```csharp
public static Task<uint[]> ReadDWordsSingleRequestAsync(QueuedKvHostLinkClient client, string device, int count, CancellationToken ct = default)
```

Reads contiguous unsigned 32-bit values using one protocol request or returns an error.

##### WriteWordsSingleRequestAsync

```csharp
public static Task WriteWordsSingleRequestAsync(KvHostLinkClient client, string device, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes contiguous unsigned 16-bit values using one protocol request or returns an error.

##### WriteWordsSingleRequestAsync

```csharp
public static Task WriteWordsSingleRequestAsync(QueuedKvHostLinkClient client, string device, IReadOnlyList<ushort> values, CancellationToken ct = default)
```

Writes contiguous unsigned 16-bit values using one protocol request or returns an error.

##### WriteDWordsSingleRequestAsync

```csharp
public static Task WriteDWordsSingleRequestAsync(KvHostLinkClient client, string device, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes contiguous unsigned 32-bit values using one protocol request or returns an error.

##### WriteDWordsSingleRequestAsync

```csharp
public static Task WriteDWordsSingleRequestAsync(QueuedKvHostLinkClient client, string device, IReadOnlyList<uint> values, CancellationToken ct = default)
```

Writes contiguous unsigned 32-bit values using one protocol request or returns an error.

##### ReadWordsChunkedAsync

```csharp
public static Task<ushort[]> ReadWordsChunkedAsync(KvHostLinkClient client, string device, int count, int maxWordsPerRequest, CancellationToken ct = default)
```

Reads contiguous unsigned 16-bit words using explicit chunking.

Remarks: Chunking is opt-in and advances only by contiguous word boundaries.

Returns: The concatenated word values from all explicit chunks.

Parameters:
- `client`: Connected Host Link client.
- `device`: Start device address.
- `count`: Number of words to read.
- `maxWordsPerRequest`: Maximum words per protocol request.
- `ct`: Cancellation token.

##### ReadWordsChunkedAsync

```csharp
public static Task<ushort[]> ReadWordsChunkedAsync(QueuedKvHostLinkClient client, string device, int count, int maxWordsPerRequest, CancellationToken ct = default)
```

Reads contiguous unsigned 16-bit words using explicit chunking.

##### ReadDWordsChunkedAsync

```csharp
public static Task<uint[]> ReadDWordsChunkedAsync(KvHostLinkClient client, string device, int count, int maxDwordsPerRequest, CancellationToken ct = default)
```

Reads contiguous unsigned 32-bit values using explicit chunking.

Remarks: Chunking is opt-in and advances only by whole double-word boundaries.

Returns: The concatenated 32-bit values from all explicit chunks.

Parameters:
- `client`: Connected Host Link client.
- `device`: Start device address.
- `count`: Number of 32-bit values to read.
- `maxDwordsPerRequest`: Maximum double-words per protocol request.
- `ct`: Cancellation token.

##### ReadDWordsChunkedAsync

```csharp
public static Task<uint[]> ReadDWordsChunkedAsync(QueuedKvHostLinkClient client, string device, int count, int maxDwordsPerRequest, CancellationToken ct = default)
```

Reads contiguous unsigned 32-bit values using explicit chunking.

##### WriteWordsChunkedAsync

```csharp
public static Task WriteWordsChunkedAsync(KvHostLinkClient client, string device, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
```

Writes contiguous unsigned 16-bit values using explicit chunking.

##### WriteWordsChunkedAsync

```csharp
public static Task WriteWordsChunkedAsync(QueuedKvHostLinkClient client, string device, IReadOnlyList<ushort> values, int maxWordsPerRequest, CancellationToken ct = default)
```

Writes contiguous unsigned 16-bit values using explicit chunking.

##### WriteDWordsChunkedAsync

```csharp
public static Task WriteDWordsChunkedAsync(KvHostLinkClient client, string device, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
```

Writes contiguous unsigned 32-bit values using explicit chunking.

##### WriteDWordsChunkedAsync

```csharp
public static Task WriteDWordsChunkedAsync(QueuedKvHostLinkClient client, string device, IReadOnlyList<uint> values, int maxDwordsPerRequest, CancellationToken ct = default)
```

Writes contiguous unsigned 32-bit values using explicit chunking.

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

##### ReadWordsAsync

```csharp
public static Task<ushort[]> ReadWordsAsync(QueuedKvHostLinkClient client, string device, int count, CancellationToken ct = default)
```

Reads contiguous unsigned 16-bit words starting at `device`.

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

##### ReadDWordsAsync

```csharp
public static Task<uint[]> ReadDWordsAsync(QueuedKvHostLinkClient client, string device, int count, CancellationToken ct = default)
```

Reads contiguous unsigned 32-bit values starting at `device`.

##### OpenAndConnectAsync

```csharp
public static Task<QueuedKvHostLinkClient> OpenAndConnectAsync(string host, string plcProfile, int port = 8501, CancellationToken ct = default)
```

Creates a queued client and opens the connection.

Remarks: This is the recommended convenience entry point for high-level application code that does not need to construct `KvHostLinkConnectionOptions` manually.

Returns: A connected queued client that is safe to share across async callers.

Parameters:
- `host`: PLC IP address or hostname.
- `plcProfile`: Canonical KEYENCE KV PLC profile for the session.
- `port`: KV Host Link TCP/UDP port. Defaults to 8501.
- `ct`: Cancellation token.

### KvHostLinkClientFactory

```csharp
public static class KvHostLinkClientFactory
```

Factory helpers for opening ready-to-use Host Link clients.

Remarks: The factory centralizes validation of host, port, timeout, and line-ending behavior so samples and generated docs can point to one explicit connection entry point.

#### Members

##### OpenAndConnectAsync

```csharp
public static Task<QueuedKvHostLinkClient> OpenAndConnectAsync(KvHostLinkConnectionOptions options, CancellationToken cancellationToken = default)
```

Creates, configures, and opens a queued Host Link client.

Remarks: The returned client uses queued access so higher-level read, write, and polling helpers can share one Host Link session predictably.

Returns: A connected queued client.

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
public KvHostLinkConnectionOptions(string Host, string PlcProfile, int Port = 8501, TimeSpan Timeout = default, HostLinkTransportMode Transport = Tcp, bool AppendLfOnSend = false)
```

Explicit connection options for a Host Link session.

Remarks: This type is intended for the unified high-level connection flow so generated documentation can describe transport, timeout, profile, and framing behavior in one place.

Parameters:
- `Host`: PLC IP address or hostname.
- `PlcProfile`: Canonical KEYENCE KV PLC profile for the session.
- `Port`: Host Link port number. Defaults to 8501.
- `Timeout`: Operation timeout. A zero value falls back to the library default.
- `Transport`: Transport protocol.
- `AppendLfOnSend`: Whether to append LF after CR on send.

##### Host

```csharp
public string Host { get; set; }
```

PLC IP address or hostname.

##### Port

```csharp
public int Port { get; set; }
```

Host Link port number. Defaults to 8501.

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

Operation timeout. A zero value falls back to the library default.

##### Transport

```csharp
public HostLinkTransportMode Transport { get; set; }
```

Transport protocol.

##### AppendLfOnSend

```csharp
public bool AppendLfOnSend { get; set; }
```

Whether to append LF after CR on send.

##### PlcProfile

```csharp
public string PlcProfile { get; set; }
```

Gets or sets the canonical KEYENCE KV PLC profile for the session.

##### EffectiveTimeout

```csharp
public TimeSpan EffectiveTimeout { get; }
```

Gets the effective timeout used for a new client instance.

Remarks: Host Link callers may leave `Timeout` at its default value and use this property when they need the resolved timeout that will be applied to the client.

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

##### ParseDevice

```csharp
public static KvDeviceAddress ParseDevice(string text, bool allowOmittedType)
```

Parses a Host Link device token with an explicit device type.

Remarks: Use `ParseDevice`. This overload will be removed in a future major release.

Parameters:
- `text`: Device token such as `DM100`.
- `allowOmittedType`: Retained for source and binary compatibility. The value is ignored because Host Link device types are always required.

##### ParseDeviceText

```csharp
public static string ParseDeviceText(string text, string defaultSuffix = "")
```

##### ResolveEffectiveFormat

```csharp
public static string ResolveEffectiveFormat(string deviceType, string suffix)
```

##### RequireExplicitFormat

```csharp
public static string RequireExplicitFormat(KvDeviceAddress address, string dataFormat = null)
```

##### ValidateDeviceType

```csharp
public static void ValidateDeviceType(string command, string deviceType, HashSet<string> allowedTypes)
```

##### ValidateDeviceCount

```csharp
public static void ValidateDeviceCount(string deviceType, string effectiveFormat, int count)
```

##### ValidateDeviceSpan

```csharp
public static void ValidateDeviceSpan(string deviceType, int startNumber, string effectiveFormat, int count = 1)
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

##### Name

```csharp
public string Name { get; set; }
```

##### DisplayName

```csharp
public string DisplayName { get; set; }
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

##### CanonicalName

```csharp
public string CanonicalName { get; set; }
```

##### DisplayName

```csharp
public string DisplayName { get; set; }
```

##### Connectable

```csharp
public bool Connectable { get; set; }
```

##### BaseProfile

```csharp
public string BaseProfile { get; set; }
```

### KvHostLinkPlcProfiles

```csharp
public static class KvHostLinkPlcProfiles
```

#### Members

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

##### GetDisplayName

```csharp
public static string GetDisplayName(string plcProfile)
```

##### FromName

```csharp
public static KvHostLinkPlcProfile FromName(string plcProfile)
```

##### KvNano

```csharp
public static KvHostLinkPlcProfile KvNano { get; }
```

##### KvNanoXym

```csharp
public static KvHostLinkPlcProfile KvNanoXym { get; }
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
public KvDeviceAddress BaseAddress { get; set; }
```

Base word device address without a logical suffix.

##### DataType

```csharp
public string DataType { get; set; }
```

Logical data type code such as `U`, `S`, `D`, `L`, `F`, `BIT`, or `COMMENT`.

##### BitIndex

```csharp
public int? BitIndex { get; set; }
```

Bit index inside the base word when the logical address targets a bit-in-word.

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
public string Code { get; set; }
```

##### Model

```csharp
public string Model { get; set; }
```

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

##### Status

```csharp
public uint Status { get; set; }
```

##### Current

```csharp
public uint Current { get; set; }
```

##### Preset

```csharp
public uint Preset { get; set; }
```

### QueuedKvHostLinkClient

```csharp
public sealed class QueuedKvHostLinkClient
```

A wrapper for `KvHostLinkClient` that serializes multi-step operations with a semaphore.

Remarks: Host Link requests often reuse one TCP session and one framing configuration. This wrapper provides a documentation-friendly queued surface for those shared-session scenarios.

#### Members

##### QueuedKvHostLinkClient

```csharp
public QueuedKvHostLinkClient(KvHostLinkClient client)
```

Initializes a new instance of the `QueuedKvHostLinkClient` class.

Parameters:
- `client`: The underlying client to wrap.

##### OpenAsync

```csharp
public Task OpenAsync(CancellationToken cancellationToken = default)
```

Opens the connection asynchronously with exclusive access.

Remarks: Call this once after construction or again after an intentional disconnect.

##### CloseAsync

```csharp
public Task CloseAsync(CancellationToken cancellationToken = default)
```

Closes the connection asynchronously with exclusive access.

##### ExecuteAsync

```csharp
public Task<T> ExecuteAsync<T>(Func<KvHostLinkClient, Task<T>> operation, CancellationToken cancellationToken = default)
```

Executes a custom async operation with exclusive access to the wrapped client.

Returns: The value returned by `operation`.

Parameters:
- `operation`: Delegate that receives the wrapped `KvHostLinkClient`.
- `cancellationToken`: Cancellation token used while waiting for exclusive access.

##### ExecuteAsync

```csharp
public Task ExecuteAsync(Func<KvHostLinkClient, Task> operation, CancellationToken cancellationToken = default)
```

Executes a custom async operation with exclusive access to the wrapped client.

Parameters:
- `operation`: Delegate that receives the wrapped `KvHostLinkClient`.
- `cancellationToken`: Cancellation token used while waiting for exclusive access.

##### SendRawAsync

```csharp
public Task<string> SendRawAsync(string body, CancellationToken cancellationToken = default)
```

##### ChangeModeAsync

```csharp
public Task ChangeModeAsync(KvPlcMode mode, CancellationToken cancellationToken = default)
```

##### ClearErrorAsync

```csharp
public Task ClearErrorAsync(CancellationToken cancellationToken = default)
```

##### CheckErrorNoAsync

```csharp
public Task<string> CheckErrorNoAsync(CancellationToken cancellationToken = default)
```

##### QueryModelAsync

```csharp
public Task<KvModelInfo> QueryModelAsync(CancellationToken cancellationToken = default)
```

##### ConfirmOperatingModeAsync

```csharp
public Task<KvPlcMode> ConfirmOperatingModeAsync(CancellationToken cancellationToken = default)
```

##### SetTimeAsync

```csharp
public Task SetTimeAsync(DateTime? value = null, CancellationToken cancellationToken = default)
```

##### ForcedSetAsync

```csharp
public Task ForcedSetAsync(string device, CancellationToken cancellationToken = default)
```

##### ForcedResetAsync

```csharp
public Task ForcedResetAsync(string device, CancellationToken cancellationToken = default)
```

##### ReadAsync

```csharp
public Task<string[]> ReadAsync(string device, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### ReadConsecutiveAsync

```csharp
public Task<string[]> ReadConsecutiveAsync(string device, int count, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### ReadCommentsAsync

```csharp
public Task<string> ReadCommentsAsync(string device, bool stripPadding = true, CancellationToken cancellationToken = default)
```

##### RegisterMonitorBitsAsync

```csharp
public Task RegisterMonitorBitsAsync(IEnumerable<string> devices, CancellationToken cancellationToken = default)
```

##### RegisterMonitorWordsAsync

```csharp
public Task RegisterMonitorWordsAsync(IEnumerable<string> devices, CancellationToken cancellationToken = default)
```

##### ReadMonitorBitsAsync

```csharp
public Task<string[]> ReadMonitorBitsAsync(CancellationToken cancellationToken = default)
```

##### ReadMonitorWordsAsync

```csharp
public Task<string[]> ReadMonitorWordsAsync(CancellationToken cancellationToken = default)
```

##### ForcedSetConsecutiveAsync

```csharp
public Task ForcedSetConsecutiveAsync(string device, int count, CancellationToken cancellationToken = default)
```

##### ForcedResetConsecutiveAsync

```csharp
public Task ForcedResetConsecutiveAsync(string device, int count, CancellationToken cancellationToken = default)
```

##### ReadConsecutiveLegacyAsync

```csharp
public Task<string[]> ReadConsecutiveLegacyAsync(string device, int count, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### WriteAsync

```csharp
public Task WriteAsync<T>(string device, T value, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### WriteConsecutiveAsync

```csharp
public Task WriteConsecutiveAsync<T>(string device, IEnumerable<T> values, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### WriteConsecutiveLegacyAsync

```csharp
public Task WriteConsecutiveLegacyAsync<T>(string device, IEnumerable<T> values, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### WriteSetValueAsync

```csharp
public Task WriteSetValueAsync<T>(string device, T value, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### WriteSetValueConsecutiveAsync

```csharp
public Task WriteSetValueConsecutiveAsync<T>(string device, IEnumerable<T> values, string dataFormat = null, CancellationToken cancellationToken = default)
```

##### SwitchBankAsync

```csharp
public Task SwitchBankAsync(int bankNo, CancellationToken cancellationToken = default)
```

##### ReadExpansionUnitBufferAsync

```csharp
public Task<string[]> ReadExpansionUnitBufferAsync(int unitNo, int address, int count, string dataFormat = "", CancellationToken cancellationToken = default)
```

##### WriteExpansionUnitBufferAsync

```csharp
public Task WriteExpansionUnitBufferAsync<T>(int unitNo, int address, IEnumerable<T> values, string dataFormat = "", CancellationToken cancellationToken = default)
```

##### Dispose

```csharp
public void Dispose()
```

Disposes the wrapper and the underlying client.

##### DisposeAsync

```csharp
public ValueTask DisposeAsync()
```

Disposes the wrapper and the underlying client asynchronously.

##### InnerClient

```csharp
public KvHostLinkClient InnerClient { get; }
```

Gets the underlying low-level client.

Remarks: Use `ExecuteAsync` when you need direct access while preserving serialized request ordering.

##### PlcProfile

```csharp
public string PlcProfile { get; }
```

Gets the canonical KEYENCE KV PLC profile selected for this session.

##### Timeout

```csharp
public TimeSpan Timeout { get; set; }
```

Gets or sets the communication timeout.

##### AppendLfOnSend

```csharp
public bool AppendLfOnSend { get; set; }
```

Gets or sets whether LF is appended after CR on send.

##### TraceHook

```csharp
public Action<HostLinkTraceFrame> TraceHook { get; set; }
```

Gets or sets the raw frame trace hook.

##### IsOpen

```csharp
public bool IsOpen { get; }
```

Gets a value indicating whether the client is connected.
