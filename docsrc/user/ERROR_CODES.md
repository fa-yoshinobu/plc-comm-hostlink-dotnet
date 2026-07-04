# Error Codes

This page summarizes Host Link PLC errors as this library reports them. It is
not a complete manufacturer code table; use the KEYENCE manuals for formal
definitions.

`HostLinkError.Code` contains the PLC code when the PLC returns an error
response. Connection and malformed-frame failures use separate exception types.

## PLC Error Codes

| Code | Typical cause | First check |
| --- | --- | --- |
| `E0` | Device number is invalid, outside range, or not available on the selected PLC model. | Check the address and selected canonical profile. |
| `E1` | Command is not supported by the selected PLC/model. Timer/counter preset writes are a common case on unsupported models. | Check the model profile and avoid unsupported write helpers. |
| `E2` | Program is not registered. | Check the PLC project/program state. |
| `E4` | Write is disabled by CPU protection, lock state, or project settings. | Check KV Studio and CPU write-protection settings. |
| `E5` | Unit error. | Check the PLC/unit error state. |
| `E6` | Comment data is not registered. | Check comment registration before using comment reads. |

## Library Exceptions

| Exception | Meaning |
| --- | --- |
| `HostLinkError` | The PLC returned `E0`, `E1`, `E2`, `E4`, `E5`, or `E6`. |
| `HostLinkProtocolError` | The response frame was malformed or did not match the expected command. |
| `HostLinkConnectionError` | Socket connection or transport I/O failed. |

See [Gotchas](GOTCHAS.md) for common symptoms such as wrong port, unsupported
timer/counter preset writes, and invalid device ranges.
