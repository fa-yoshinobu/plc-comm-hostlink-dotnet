# Gotchas

## Symptom: Timer/counter preset write returns E1

| Root cause | Fix |
|---|---|
| Timer/counter preset write commands are supported on KV-8000/7000-series, not on KV-3000, KV-5000, or KV-NANO. | Read timer/counter values with the helper API, and do not write presets on unsupported models. |

```csharp
using System;
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

KvTimerCounterValue timer = await client.ReadTimerAsync("T0");
Console.WriteLine($"T0 status={timer.Status}, current={timer.Current}, preset={timer.Preset}");
```

## Symptom: AT device fails on KV-X500

| Root cause | Fix |
|---|---|
| `AT` digital trimmer devices are not available in KV-X500 profiles. | Check the selected profile catalog before reading `AT`. |

```csharp
using System;
using PlcComm.KvHostLink;

var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-x500");
var at = catalog.Entry("AT");
Console.WriteLine($"AT supported: {at?.Supported == true}");
```

## Symptom: X or Y address is rejected

| Root cause | Fix |
|---|---|
| `X` and `Y` use decimal-bank plus hex-bit notation. | Use an address such as `X10F`, where `10` is the bank and `F` is the bit. |

```csharp
using System;
using PlcComm.KvHostLink;

Console.WriteLine(KvHostLinkAddress.Normalize("X10F"));
```

## Symptom: R, MR, LR, or CR address is rejected

| Root cause | Fix |
|---|---|
| These bit families use two-digit bit notation, not a plain hexadecimal number. | Use forms such as `R200` or `MR100`. |

```csharp
using System;
using PlcComm.KvHostLink;

Console.WriteLine(KvHostLinkAddress.Normalize("MR100"));
```

## Symptom: Connection fails immediately

| Root cause | Fix |
|---|---|
| KV Host Link examples use port `8501`, not `1025`. | Set port `8501` unless your PLC connection node is configured differently. |

```csharp
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
```

## Symptom: `keyence:kv-3000-5000` is rejected

| Root cause | Fix |
|---|---|
| The old combined KV-3000/KV-5000 profile was removed. The ranges are managed separately. | Select `keyence:kv-3000` or `keyence:kv-5000`. Use the matching `-xym` profile only when you need XYM aliases. |

```csharp
using System;
using PlcComm.KvHostLink;

var kv3000 = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-3000");
var kv5000 = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-5000");
Console.WriteLine($"{kv3000.PlcProfile}, {kv5000.PlcProfile}");
```

## Symptom: Non-canonical profile string fails immediately

| Root cause | Fix |
|---|---|
| Device range catalog selection accepts only exact canonical profile strings from source. | Copy one exact string from [PLC profiles](PROFILES.md). |

```csharp
using System;
using PlcComm.KvHostLink;

var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-7000");
Console.WriteLine(catalog.PlcProfile);
```

## Symptom: Expansion unit buffer read fails

| Root cause | Fix |
|---|---|
| The selected unit number, buffer address, data format, or module may not match your connected PLC hardware. | Verify the expansion unit number and buffer address before calling the buffer API. |

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
