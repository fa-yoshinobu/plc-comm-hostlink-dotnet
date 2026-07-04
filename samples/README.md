# Samples

This directory contains buildable console projects that demonstrate the recommended high-level KEYENCE KV Host Link API. Each sample requires a host, port, and canonical PLC profile; the examples below use `192.168.250.100`, `8501`, and `keyence:kv-8000`.

Use only test addresses that are safe for your PLC program before you run any write example.

## How to run

```powershell
dotnet run --project samples/PlcComm.KvHostLink.HighLevelSample -- 192.168.250.100 8501 keyence:kv-8000
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.BasicReadWriteSample -- 192.168.250.100 8501 keyence:kv-8000
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.PollingReconnectSample -- 192.168.250.100 8501 keyence:kv-8000 DM100 U 1
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.MultiPlcMonitorSample -- --plc line-a=192.168.250.100,keyence:kv-8000,8501,tcp --plc line-b=192.168.250.101,keyence:kv-8000,8501,tcp --tag dm100=DM100:U
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.ConfigPollingSample -- --config samples/PlcComm.KvHostLink.ConfigPollingSample/config_polling.example.json --dry-run
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.NamedPollingSample -- 192.168.250.100 8501 keyence:kv-8000
```

## Sample index

| Project | What it demonstrates |
|---|---|
| `PlcComm.KvHostLink.HighLevelSample` | A guided tour of connection setup, typed reads/writes, block reads, bit-in-word updates, named snapshots, and polling. |
| `PlcComm.KvHostLink.BasicReadWriteSample` | Focused typed reads/writes, readback checks, restore logic, and contiguous block reads. |
| `PlcComm.KvHostLink.PollingReconnectSample` | Read-only polling loop with automatic reconnect and backoff after transport loss. |
| `PlcComm.KvHostLink.MultiPlcMonitorSample` | Read-only multi-PLC monitoring with one task and reconnect loop per PLC. |
| `PlcComm.KvHostLink.ConfigPollingSample` | Read-only JSON-configured polling with `--dry-run` and long-form `timestamp,plc,tag,value` CSV output; YAML config is Python-only. |
| `PlcComm.KvHostLink.NamedPollingSample` | Mixed snapshots, bit-in-word writes, polling, and restoring changed bits. |
