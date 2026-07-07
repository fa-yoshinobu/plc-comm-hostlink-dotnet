# TODO: Host Link Communication .NET

## API cleanup

- [x] Add `KvHostLinkPlcProfiles.GetDisplayName(string plcProfile)` as the
  PLC-profile display-name API. Keep device-range APIs under
  `KvHostLinkDeviceRanges`; `GetDisplayName` should not be discoverable only
  through a device-range class.

## Live verification

- Verify high-level `:H` / `H` hexadecimal word read and write on a live KEYENCE KV PLC.

## Deferred verification

- [ ] Later: after the `plc-comm-soak-test` 12h run completes, confirm the
  completion summary and validate that `report.md` shows memory slope and Time
  Windows without a regression relevant to this library. See
  `D:\APP\soak_bench_integration_goal_20260705.md`.
