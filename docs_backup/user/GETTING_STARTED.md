# Getting Started

## Start Here

Use this package when you want the shortest .NET path to KEYENCE KV Host Link communication through the public high-level API.

Recommended first path:

1. Install `PlcComm.KvHostLink`.
2. Open one client with `KvHostLinkClientFactory.OpenAndConnectAsync`.
3. Read one safe `DM` word.
4. Write only to a known-safe test word or bit after the first read is stable.

## First PLC Registers To Try

Start with these first:

- `DM0`
- `DM10`
- `DM100:S`
- `DM200:D`
- `DM50.3`

Do not start with these:

- large chunked reads
- validation sweeps
- addresses outside the current public register table

## Minimal Connection Pattern

```csharp
var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
```

## First Successful Run

Recommended order:

1. `ReadTypedAsync("DM0", "U")`
2. `WriteTypedAsync("DM10", "U", value)` only on a safe test word
3. `ReadNamedAsync(new[] { "DM0", "DM1:S", "DM2:D", "DM4:F", "DM10.0" })`

Expected result:

- connection opens successfully
- one `DM` read succeeds
- typed and mixed snapshot reads succeed after the first plain read

## Common Beginner Checks

If the first read fails, check these in order:

- correct host and port
- start with `DM` instead of a timer/counter or less common area
- use one scalar read before trying `.bit` or typed views

## Next Pages

- [Supported PLC Registers](./SUPPORTED_REGISTERS.md)
- [Latest Communication Verification](./LATEST_COMMUNICATION_VERIFICATION.md)
- [User Guide](./USER_GUIDE.md)
