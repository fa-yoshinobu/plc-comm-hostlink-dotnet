# Troubleshooting

Use this page for first-pass checks when a KV Host Link request does not behave as expected. For address-shape details, see [GOTCHAS.md](GOTCHAS.md).

## Connection checks

| Symptom | Check |
| --- | --- |
| Connection timeout | Confirm the PLC host address and that the KV Ethernet connection is enabled. |
| TCP connection refused | Confirm the Host Link port. Examples use `8501`. |
| UDP requests do not return | Confirm the PLC UDP port and that the connection node accepts UDP Host Link requests. |
| Intermittent timeouts | Increase connection timeout settings and avoid creating a new connection for every small request. |

## Profile and address checks

| Symptom | Check |
| --- | --- |
| Profile rejected before communication | Use one exact canonical profile from [PROFILES.md](PROFILES.md). |
| Device address returns `E0` | Check the selected profile and the PLC model range before using that address. |
| `X` or `Y` is rejected | Use decimal-bank plus hex-bit notation such as `X10F:BIT`. |
| `R`, `MR`, `LR`, or `CR` is rejected | Use KEYENCE two-digit bit notation such as `R200:BIT`. |
| `CTH` or `CTC` is rejected | Treat these as catalog rows only; they are not accepted as address input. |

## Write checks

| Symptom | Check |
| --- | --- |
| Write returns `E4` | Check PLC write protection and project settings. |
| Timer/counter preset write returns `E1` | Preset writes are for KV-8000/7000-series. Do not use them on unsupported models. |
| `AT` write is rejected | `AT` is not in the Host Link write-device table; use it only where reads are supported. |

