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
