# KEYENCE KV Host Link Protocol Spec (.NET)

This document is a working protocol summary for the KEYENCE KV series Host Link protocol.

It is not a verbatim manufacturer manual. It is a reorganized implementation note based on the current code and verified hardware behavior.

Related documents:

- [ARCHITECTURE.md](ARCHITECTURE.md)
- [API_UNIFICATION_POLICY.md](API_UNIFICATION_POLICY.md)

## 1. Communication Overview

- Roles: PLC Ethernet unit is server, PC application is client
- Transport: TCP/IP or UDP/IP
- Default port: `8501` (configurable)
- Encoding: ASCII

## 2. Frame Rules

### 2.1 Command Frame

```text
<COMMAND> [<PARAM> ...] CR
```

- `CR` (`0x0D`) is the command separator.
- Optional `LF` (`0x0A`) after `CR` is accepted by the PLC.

### 2.2 Response Frame

```text
<DATA | OK | E*> CR LF
```

Examples:

```text
OK\r\n
1 0 1 0\r\n
E1\r\n
```

### 2.3 Error Responses

Error responses begin with `E` followed by an error code number.

| Response | Meaning |
| --- | --- |
| `E1` | Undefined command |
| `E2` | Operand error |
| `E3` | Monitor registration error |

## 3. Data Format Suffixes

| Suffix | Type |
| --- | --- |
| `.U` | unsigned 16-bit decimal |
| `.S` | signed 16-bit decimal |
| `.D` | unsigned 32-bit decimal |
| `.L` | signed 32-bit decimal |
| `.H` | 16-bit hexadecimal |
| `.F` | IEEE 754 32-bit float |

Current library builds validate and document `.U`, `.S`, `.D`, `.L`, and `.H` in user-facing APIs.
Treat `.F` as protocol-level reference only until implementation support is enabled.

## 4. Device Range Table

| Device | Range |
| --- | --- |
| R | 0..199915 |
| B | 0..7FFF |
| MR | 0..399915 |
| LR | 0..99915 |
| CR | 0..7915 |
| VB | 0..F9FF |
| DM | 0..65534 |
| EM | 0..65534 |
| FM | 0..32767 |
| ZF | 0..524287 |
| W | 0..7FFF |
| TM | 0..511 |
| C | 0..511 |
| TC | 0..511 |
| CC | 0..511 |
| CTH | 0..7 |
| CTC | 0..7 |
| AT | 0..3 |
| CM | 0..2 |

## 5. Key Commands

| Command | Name | Notes |
| --- | --- | --- |
| `RD` | Read device | single or multiple words |
| `WR` | Write device | single or multiple words |
| `RDS` | Read device (STS) | named status read |
| `WRS` | Write device (STS) | |
| `RDE` | Read device (extended) | |
| `WRE` | Write device (extended) | |
| `WS` | Write set | bit set |
| `WSS` | Write set (STS) | |
| `BE` | Batch read bits | |
| `URD` | Unit read | |
| `UWR` | Unit write | |
| `ST` | Status read | CPU status |
| `RSS` | Read system status | |
| `TRD` | Timer read | |
| `TRW` | Timer read/write | |
| `CKR` | Clock read | |
| `CKW` | Clock write | |

## 6. Implementation Notes

### Address Format

Devices are identified by type prefix + decimal number (or hex for B, W, VB).

Examples: `R0`, `DM100`, `B1F`, `MR0`

### Multi-Point Read

`RD R0 R1 R2.U` reads devices R0, R1, R2 as unsigned words in a single command.

### 32-Bit Values

32-bit values span two consecutive 16-bit words: low word first.
`KvHostLinkClient.ReadDWords` / `WriteDWords` handle this packing automatically.
