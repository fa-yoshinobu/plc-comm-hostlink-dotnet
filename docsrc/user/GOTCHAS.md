# Gotchas

## Timer/counter preset write returns E1 error

If `WriteTypedAsync` on a `T` or `C` preset returns error `E1`, `WS`/`WSS` preset write commands are only supported on KV-8000/7000-series.

Fix: do not write timer/counter presets on other models.

## AT device read fails or returns zero

If reading `AT0` or similar returns an error or zero on some models, the AT digital trimmer is not available on KV-X500.

Fix: check the device range catalog before accessing AT devices.

```csharp
using System;
using PlcComm.KvHostLink;

var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-x500");
var at = catalog.Entry("AT");
Console.WriteLine($"AT supported: {at?.Supported == true}");
```

## X or Y address is rejected

If an X or Y address raises a parse error, X and Y use decimal-bank + hex-bit notation, not plain decimal.

Fix: use `X10F` (bank 10, bit F), not the raw decimal equivalent.

```csharp
using System;
using PlcComm.KvHostLink;

Console.WriteLine(KvHostLinkAddress.Normalize("X10F"));
```

## R/MR/LR/CR address is rejected

If an R, MR, LR, or CR address raises a parse error, these families use two-digit bit notation (`R200`, `MR100`), not hex-only.

Fix: ensure the address string matches two-digit bit notation.

```csharp
using System;
using PlcComm.KvHostLink;

Console.WriteLine(KvHostLinkAddress.Normalize("MR100"));
```

## Connection fails immediately

If the connection times out immediately, the default port for KV Host Link is `8501`, not `1025`.

Fix: set `Port = 8501` in `KvHostLinkConnectionOptions`.

```csharp
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
```

## Expansion unit buffer read fails

If an expansion unit buffer read fails, the selected unit number, buffer address, or module may not match the connected PLC hardware.

Fix: verify the expansion unit number and buffer address before calling the buffer API.

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

string[] values = await client.ReadExpansionUnitBufferAsync(
    unitNo: 0,
    address: 0,
    count: 4,
    dataFormat: ".U");

Console.WriteLine($"Read {values.Length} values.");
```
