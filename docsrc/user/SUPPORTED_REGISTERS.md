# Supported registers

This page lists device families supported by the .NET (C#) high-level API.

## Word device families

| Family | Kind | Example | Notes |
|---|---|---|---|
| `DM` | Word | `DM0` | General data memory. Start here for first reads. |
| `EM` | Word | `EM0` | Extended data memory on models that provide EM ranges. |
| `FM` | Word | `FM0` | File memory on models that provide FM ranges. |
| `ZF` | Word | `ZF0` | File register area on models that provide ZF ranges. |
| `W` | Word | `W0` | Link register word area. |
| `CM` | Word | `CM0` | Control memory word area. |
| `VM` | Word | `VM0` | Variable memory word area; not available on KV-X500 profiles. |
| `TM` | Word | `TM0` | Timer-related word area. |

## Bit device families

| Family | Kind | Example | Notes |
|---|---|---|---|
| `R` | Bit | `R200` | Relay bits using two-digit bit notation. |
| `B` | Bit | `B0000` | Link relay bits using hexadecimal notation. |
| `MR` | Bit | `MR100` | Internal relay bits using two-digit bit notation. |
| `LR` | Bit | `LR100` | Latch relay bits using two-digit bit notation. |
| `CR` | Bit | `CR100` | Control relay bits using two-digit bit notation. |
| `VB` | Bit | `VB0` | Variable memory bits; not available on KV-X500 profiles. |
| `X` | Bit | `X10F` | Input alias in XYM profiles; decimal bank plus hex bit. |
| `Y` | Bit | `Y10F` | Output alias in XYM profiles; decimal bank plus hex bit. |
| `M` | Bit | `M0` | Internal relay alias in XYM profiles. |
| `L` | Bit | `L0` | Latch relay alias in XYM profiles. |

## Timer, counter, and index families

| Family | Kind | Example | Notes |
|---|---|---|---|
| `T` | Timer/counter | `T0` | Timer status, current value, and preset helpers. |
| `C` | Timer/counter | `C0` | Counter status, current value, and preset helpers. |
| `AT` | Timer/counter catalog category | `AT0` | Digital trimmer; not available on KV-X500. |
| `CTH` | Timer/counter catalog category | `CTH0` | High-speed counter on KV-NANO, KV-3000, and KV-5000 profiles only. Catalog entry only — not accepted by the address parser. |
| `CTC` | Timer/counter catalog category | `CTC0` | High-speed counter on KV-NANO, KV-3000, and KV-5000 profiles only. Catalog entry only — not accepted by the address parser. |
| `Z` | Index | `Z1` | Index registers. KV-X500 profiles expose `Z1` through `Z10`; other profiles expose `Z1` through `Z12`. |

## Type suffixes

| Form | Example | Meaning |
|---|---|---|
| Plain | `DM100` | Default view for the device family. |
| `:U` | `DM100:U` | Unsigned 16-bit word. |
| `:S` | `DM100:S` | Signed 16-bit word. |
| `:D` | `DM100:D` | Unsigned 32-bit double word. |
| `:L` | `DM100:L` | Signed 32-bit double word. |
| `:F` | `DM100:F` | IEEE 754 32-bit floating-point value. |
| `.n` | `DM100.A` | Bit `n` inside a word, where `n` is hexadecimal `0` to `F`. |

## Addressing notes

- `X` and `Y` use decimal-bank + hex-bit notation (e.g. `X10F`, meaning bank 10, bit F).
- `R`/`MR`/`LR`/`CR` use two-digit bit notation (`R200`, `MR100`).
- `AT` digital trimmer is not available on KV-X500.
- `CTH` and `CTC` appear in the range catalog but are not accepted by the address parser.
- Default port is `8501`.

See [PLC profiles](PROFILES.md) for per-model range limits.
