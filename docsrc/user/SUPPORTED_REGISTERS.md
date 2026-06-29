# Supported registers

This page lists device families supported by the .NET (C#) high-level API.

## Word device families

| Family | Kind | Example | Notes |
|---|---|---|---|
| `DM` | Word | `DM0:U` | General data memory. Start here for first reads. |
| `EM` | Word | `EM0:U` | Extended data memory on models that provide EM ranges. |
| `FM` | Word | `FM0:U` | File memory on models that provide FM ranges. |
| `ZF` | Word | `ZF0:U` | File register area on models that provide ZF ranges. |
| `W` | Word | `W0:U` | Link register word area. |
| `CM` | Word | `CM0:U` | Control memory word area. |
| `VM` | Word | `VM0:U` | Variable memory word area; not available on KV-X500 profiles. |
| `TM` | Word | `TM0:U` | Timer-related word area. |

## Bit device families

| Family | Kind | Example | Notes |
|---|---|---|---|
| `R` | Bit | `R200:BIT` | Relay bits using two-digit bit notation. |
| `B` | Bit | `B0000:BIT` | Link relay bits using hexadecimal notation. |
| `MR` | Bit | `MR100:BIT` | Internal relay bits using two-digit bit notation. |
| `LR` | Bit | `LR100:BIT` | Latch relay bits using two-digit bit notation. |
| `CR` | Bit | `CR100:BIT` | Control relay bits using two-digit bit notation. |
| `VB` | Bit | `VB0:BIT` | Variable memory bits; not available on KV-X500 profiles. |
| `X` | Bit | `X10F:BIT` | Input alias in XYM profiles; decimal bank plus hex bit. |
| `Y` | Bit | `Y10F:BIT` | Output alias in XYM profiles; decimal bank plus hex bit. |
| `M` | Bit | `M0:BIT` | Internal relay alias in XYM profiles. |
| `L` | Bit | `L0:BIT` | Latch relay alias in XYM profiles. |

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
| `:U` | `DM100:U` | Unsigned 16-bit word. |
| `:S` | `DM100:S` | Signed 16-bit word. |
| `:D` | `DM100:D` | Unsigned 32-bit double word. |
| `:L` | `DM100:L` | Signed 32-bit double word. |
| `:F` | `DM100:F` | IEEE 754 32-bit floating-point value. |
| `:BIT` | `R200:BIT` | Direct bit device value. |
| `:COMMENT` | `DM100:COMMENT` | PLC device comment text through `ReadNamedAsync`. |
| `.n` | `DM100.A` | Bit `n` inside a word, where `n` is hexadecimal `0` to `F`. |

## Addressing notes

- `X` and `Y` use decimal-bank + hex-bit notation (e.g. `X10F`, meaning bank 10, bit F).
- `R`/`MR`/`LR`/`CR` use two-digit bit notation (`R200:BIT`, `MR100:BIT`).
- Helper-layer address text must include the intended type. Use `DM100:U`, not plain `DM100`, when reading an unsigned word through `ReadNamedAsync`.
- `AT` digital trimmer is not available on KV-X500.
- `CTH` and `CTC` appear in the range catalog but are not accepted by the address parser.
- Default port is `8501`.

See [PLC profiles](PROFILES.md) for per-model range limits.
