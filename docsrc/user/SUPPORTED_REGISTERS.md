# Supported PLC Registers

This page is the canonical public register table for the .NET high-level API.

## Supported Bit Devices

| Family | Kind | Example | Notes |
| --- | --- | --- | --- |
| `R` | bit | `R200` | direct bit access |
| `B` | bit | `B10` | direct bit access |
| `MR` | bit | `MR100` | direct bit access |
| `LR` | bit | `LR10` | direct bit access |
| `CR` | bit | `CR0` | direct bit access |
| `VB` | bit | `VB0` | direct bit access |
| `X` | bit | `X10` | direct bit access |
| `Y` | bit | `Y10` | direct bit access |
| `M` | bit | `M100` | direct bit access |
| `L` | bit | `L100` | direct bit access |

## Supported Word Devices

| Family | Kind | Example | Notes |
| --- | --- | --- | --- |
| `DM` | word | `DM100` | recommended first test area |
| `EM` | word | `EM100` | high-level word access |
| `FM` | word | `FM100` | high-level word access |
| `ZF` | word | `ZF100` | high-level word access |
| `W` | word | `W100` | high-level word access |
| `TM` | word | `TM100` | high-level word access |
| `Z` | word | `Z10` | high-level word access |
| `TC` | word | `TC10` | word access |
| `TS` | word | `TS10` | word access |
| `CC` | word | `CC10` | word access |
| `CS` | word | `CS10` | word access |
| `CM` | word | `CM10` | word access |
| `VM` | word | `VM10` | word access |
| `D` | word | `D10` | word access |
| `E` | word | `E10` | word access |
| `F` | word | `F10` | word access |

## High-Level Views

| Form | Example | Meaning |
| --- | --- | --- |
| plain word | `DM100` | unsigned 16-bit word |
| signed view | `DM100:S` | signed 16-bit value |
| dword view | `DM100:D` | unsigned 32-bit value |
| long view | `DM100:L` | signed 32-bit value |
| float view | `DM100:F` | float32 value |
| bit in word | `DM100.3` or `DM100.A` | one bit inside a word |
| timer scalar | `T10:D` | high-level typed timer value |
| counter scalar | `C10:D` | high-level typed counter value |

## Addressing Notes

- Start with `DM`.
- Bit-in-word updates belong on word devices, not direct bit families.
- `KvHostLinkAddress.Normalize("dm100.a")` returns the canonical uppercase form.
- If a family is not listed above, do not treat it as publicly supported by the current high-level API.
