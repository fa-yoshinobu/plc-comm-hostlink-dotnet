# TODO: Host Link Communication .NET

This file tracks the remaining tasks and issues for the Host Link Communication (KEYENCE KV) .NET library.

## 1. Active Follow-Up

- [x] **No blocking protocol issues**: No blocking protocol issues are open right now.

## 2. Validation Notes

- [x] **KV-5000 live restore samples**: On 2026-05-03, the live KEYENCE KV-5000
  at `192.168.250.100:8501` passed the restore-safe
  `BasicReadWriteSample` and `NamedPollingSample`. Typed writes use
  `DM120`/`DM121`/`DM122`/`DM124`; bit-in-word writes use `DM126.0` and
  `DM126.3`. Both samples verify readback and restore the original values.

## 3. Cross-Stack API Alignment

- [ ] **Unify PLC profile naming across libraries**: Review public Host Link model/profile selectors and align them with the cross-library `PlcProfile` naming policy where practical. Standard saved/displayed names should converge on one canonical lowercase form such as `keyence:kv-x500` or `keyence:kv-7000`, with legacy names accepted only as input aliases. Do not collapse models into a broad profile when device ranges can differ.
