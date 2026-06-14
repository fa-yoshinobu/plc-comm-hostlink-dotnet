# PLC profiles

This library supports all KV series models. Device ranges differ by model. You select the device-range catalog by passing a canonical PLC profile name such as `keyence:kv-7000`; the library does not query the PLC to choose a profile for you.

## Supported models

| Model | Key available devices | Notes |
|---|---|---|
| `keyence:kv-nano` | `R`, `B`, `MR`, `LR`, `CR`, `CM`, `T`, `C`, `DM`, `W`, `TM`, `VM`, `VB`, `Z`, `CTH`, `CTC` | Standard KV-NANO profile. `EM`, `FM`, `ZF`, and `AT` are not in this profile. |
| `keyence:kv-nano-xym` | `X`, `Y`, `M`, `L`, `D`, plus standard KV-NANO devices | KV-NANO profile with XYM aliases. `E`, `F`, `ZF`, and `AT` are not in this profile. |
| `keyence:kv-3000` | `R`, `B`, `MR`, `LR`, `CR`, `CM`, `T`, `C`, `DM`, `EM`, `FM`, `ZF`, `W`, `TM`, `VM`, `VB`, `Z`, `CTH`, `CTC`, `AT` | Standard KV-3000 profile. |
| `keyence:kv-3000-xym` | `X`, `Y`, `M`, `L`, `D`, `E`, `F`, plus standard KV-3000 devices | KV-3000 profile with XYM aliases. |
| `keyence:kv-5000` | `R`, `B`, `MR`, `LR`, `CR`, `CM`, `T`, `C`, `DM`, `EM`, `FM`, `ZF`, `W`, `TM`, `VM`, `VB`, `Z`, `CTH`, `CTC`, `AT` | Standard KV-5000 profile. |
| `keyence:kv-5000-xym` | `X`, `Y`, `M`, `L`, `D`, `E`, `F`, plus standard KV-5000 devices | KV-5000 profile with XYM aliases. |
| `keyence:kv-7000` | `R`, `B`, `MR`, `LR`, `CR`, `CM`, `T`, `C`, `DM`, `EM`, `FM`, `ZF`, `W`, `TM`, `VM`, `VB`, `Z`, `AT` | Standard profile for KV-7000, KV-7300, and KV-7500 family models. `CTH` and `CTC` are not in this profile. |
| `keyence:kv-7000-xym` | `X`, `Y`, `M`, `L`, `D`, `E`, `F`, plus standard KV-7000 devices | KV-7000 profile with XYM aliases. `CTH` and `CTC` are not in this profile. |
| `keyence:kv-8000` | `R`, `B`, `MR`, `LR`, `CR`, `CM`, `T`, `C`, `DM`, `EM`, `FM`, `ZF`, `W`, `TM`, `VM`, `VB`, `Z`, `AT` | Standard KV-8000 profile. `CTH` and `CTC` are not in this profile. |
| `keyence:kv-8000-xym` | `X`, `Y`, `M`, `L`, `D`, `E`, `F`, plus standard KV-8000 devices | KV-8000 profile with XYM aliases. `CTH` and `CTC` are not in this profile. |
| `keyence:kv-x500` | `R`, `B`, `MR`, `LR`, `CR`, `CM`, `T`, `C`, `DM`, `EM`, `FM`, `ZF`, `W`, `TM`, `Z` | Standard profile for KV-X500, KV-X520, KV-X530, KV-X550, and KV-X310 family models. `AT`, `VM`, `VB`, `CTH`, and `CTC` are not in this profile. |
| `keyence:kv-x500-xym` | `X`, `Y`, `M`, `L`, `D`, `E`, `F`, plus standard KV-X500 devices | KV-X500 profile with XYM aliases. `AT`, `VM`, `VB`, `CTH`, and `CTC` are not in this profile. |

## How to select a catalog

```csharp
using System;
using PlcComm.KvHostLink;

var catalog = KvHostLinkDeviceRanges.DeviceRangeCatalogForPlcProfile("keyence:kv-7000");
Console.WriteLine(catalog.PlcProfile);
```

Select the canonical profile in your application settings, project file, or UI. Connect separately when you need to read or write PLC data:

```csharp
using PlcComm.KvHostLink;

var options = new KvHostLinkConnectionOptions("192.168.250.100", 8501);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);
ushort dm0 = (ushort)await client.ReadTypedAsync("DM0", "U");
```

## Model-specific cautions

KV-NANO profiles do not include `EM`, `FM`, `ZF`, or `AT`. Use `DM` for first reads and check the device range catalog before using model-specific areas.

KV-3000 and KV-5000 profiles include `AT`, `CTH`, and `CTC`, but timer/counter preset writes (`WS`/`WSS`) are documented for KV-8000/7000-series only.

KV-7000 and KV-8000 profiles are the documented profiles for timer/counter preset writes (`WS`/`WSS`). They do not include `CTH` or `CTC`.

KV-X500 profiles do not include `AT`, `VM`, `VB`, `CTH`, or `CTC`. Check [Gotchas](GOTCHAS.md) before using `AT` or XYM-style addresses.
