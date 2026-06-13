# Samples

This directory contains buildable console projects that demonstrate the recommended high-level KEYENCE KV Host Link API. Each sample accepts an optional host and port; the examples below use `192.168.250.100` and `8501`.

## How to run

```powershell
dotnet run --project samples/PlcComm.KvHostLink.HighLevelSample -- 192.168.250.100 8501
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.BasicReadWriteSample -- 192.168.250.100 8501
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.NamedPollingSample -- 192.168.250.100 8501
```

## Sample index

| Project | What it demonstrates |
|---|---|
| `PlcComm.KvHostLink.HighLevelSample` | A guided tour of connection setup, typed reads/writes, block reads, bit-in-word updates, named snapshots, and polling. |
| `PlcComm.KvHostLink.BasicReadWriteSample` | Focused typed reads/writes, readback checks, restore logic, and contiguous block reads. |
| `PlcComm.KvHostLink.NamedPollingSample` | Mixed snapshots, bit-in-word writes, polling, and restoring changed bits. |
