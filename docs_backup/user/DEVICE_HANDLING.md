# Device and Type Handling

This page describes device handling from the recommended high-level API.

## Typed Helpers

`ReadTypedAsync` and `WriteTypedAsync` are the standard entry points for single
typed values.

| dtype | Meaning | Width | .NET type |
|---|---|---|---|
| `U` | unsigned 16-bit | 1 word | `ushort` |
| `S` | signed 16-bit | 1 word | `short` |
| `D` | unsigned 32-bit | 2 words | `uint` |
| `L` | signed 32-bit | 2 words | `int` |
| `F` | IEEE 754 float32 | 2 words | `float` |

Example:

```csharp
ushort dm0 = (ushort)await client.ReadTypedAsync("DM0", "U");
int counter = (int)await client.ReadTypedAsync("DM10", "L");
float temp = (float)await client.ReadTypedAsync("DM20", "F");

await client.WriteTypedAsync("DM100", "U", dm0);
await client.WriteTypedAsync("DM110", "L", counter);
await client.WriteTypedAsync("DM120", "F", temp);
```

## Contiguous Areas

Use explicit single-request helpers when the data occupies consecutive word
addresses and one PLC request is required.

```csharp
ushort[] words = await client.ReadWordsSingleRequestAsync("DM200", 8);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM300", 4);
```

- `ReadWordsSingleRequestAsync` reads contiguous unsigned 16-bit values
- `ReadDWordsSingleRequestAsync` reads contiguous unsigned 32-bit values from adjacent words

Use explicit chunked helpers only when splitting is acceptable:

```csharp
ushort[] largeWords = await client.ReadWordsChunkedAsync("DM1000", 200, maxWordsPerRequest: 64);
uint[] largeDwords = await client.ReadDWordsChunkedAsync("DM2000", 40, maxDwordsPerRequest: 32);
```

## Bit-in-Word Addresses

Use `WriteBitInWordAsync` to update a single bit inside a word device without
disturbing the other bits.

```csharp
await client.WriteBitInWordAsync("DM500", bitIndex: 0, value: true);
await client.WriteBitInWordAsync("DM500", bitIndex: 10, value: false);
```

## Mixed Snapshots

Use `ReadNamedAsync` when one application snapshot mixes typed word values and
bit-in-word values.

Supported notation:

| Format | Meaning |
|---|---|
| `"DM100"` | unsigned 16-bit |
| `"DM100:S"` | signed 16-bit |
| `"DM100:D"` | unsigned 32-bit |
| `"DM100:L"` | signed 32-bit |
| `"DM100:F"` | float32 |
| `"DM100.3"` | bit 3 inside the word |
| `"DM100.A"` | bit 10 inside the word |

Example:

```csharp
var snapshot = await client.ReadNamedAsync(
    new[] { "DM100", "DM101:S", "DM102:D", "DM104:F", "DM200.3", "DM200.A" });
```

Bit indices use hexadecimal notation from `0` to `F`.

## Scope

This user-facing page intentionally stops at the helper layer.
Token-oriented reads, raw suffix handling, and direct protocol operations are
documented for maintainers only.
