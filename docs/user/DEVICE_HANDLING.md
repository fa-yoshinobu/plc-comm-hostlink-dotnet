# Device Data Handling: KEYENCE Host Link

This page explains how the library handles different data types and device suffixes when communicating with KEYENCE KV PLCs.

## 1. Data Format Suffixes
The library supports the standard KEYENCE suffixes to interpret data correctly:

| Suffix | Meaning | Size | C# Type Equivalent |
| :--- | :--- | :--- | :--- |
| `.U` | Unsigned 16-bit | 1 Word | `ushort` |
| `.S` | Signed 16-bit | 1 Word | `short` |
| `.D` | Unsigned 32-bit | 2 Words | `uint` |
| `.L` | Signed 32-bit | 2 Words | `int` |
| `.H` | Hexadecimal | 1 Word | `string` (Hex) |

## 2. 32-bit Data Handling
When using `.D` or `.L`, the library and the PLC treat two consecutive 16-bit registers as a single 32-bit value.

### Important Note on Multi-Word Occupancy:
If you read 10 items from `DM100.L`, you are effectively reading 20 words (`DM100` to `DM119`). The library handles the count validation automatically.

## 3. Bit vs. Word Devices
- **Bit Devices (R, B, MR, LR)**: Returned as `"0"` or `"1"`.
- **Word Devices (DM, EM, W)**: Returned as decimal strings (or Hex if `.H` is used).

Example:
```csharp
// Reading a bit
var relay = await client.ReadAsync("MR0"); // returns ["0"] or ["1"]

// Reading a signed double-word
var value = await client.ReadAsync("DM100.L"); // returns ["-1234567"]
```
