# Samples

This directory contains buildable console projects that demonstrate the recommended high-level KEYENCE KV Host Link API. Each endpoint requires a host, port, TCP/UDP transport, and canonical PLC profile; the examples below use `192.168.250.100`, `8501`, `tcp`, and `keyence:kv-8000`.

All samples are read-only by default. The three write demonstrations require an explicit
`--allow-writes` argument, use changing test values, and restore the values captured before writing.
Use that option only with addresses reserved for a controlled test PLC program; it is not a
production-safety mode.

## How to run

```powershell
dotnet run --project samples/PlcComm.KvHostLink.HighLevelSample -- 192.168.250.100 8501 tcp keyence:kv-8000
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.BasicReadWriteSample -- 192.168.250.100 8501 tcp keyence:kv-8000
```

To opt in to its write/readback/restore section on controlled test addresses:

```powershell
dotnet run --project samples/PlcComm.KvHostLink.BasicReadWriteSample -- 192.168.250.100 8501 tcp keyence:kv-8000 --allow-writes
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.PollingReconnectSample -- 192.168.250.100 8501 tcp keyence:kv-8000 DM100 U 1
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.MultiPlcMonitorSample -- --plc line-a=192.168.250.100,keyence:kv-8000,8501,tcp --plc line-b=192.168.250.101,keyence:kv-8000,8501,tcp --tag dm100=DM100:U
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.ConfigPollingSample -- --config samples/PlcComm.KvHostLink.ConfigPollingSample/config_polling.example.json --dry-run
```

```powershell
dotnet run --project samples/PlcComm.KvHostLink.NamedPollingSample -- 192.168.250.100 8501 tcp keyence:kv-8000
```

## Sample index

| Project | What it demonstrates |
|---|---|
| `PlcComm.KvHostLink.HighLevelSample` | A guided tour of connection setup, typed reads, block reads, named snapshots, polling, and opt-in write/restore examples. |
| `PlcComm.KvHostLink.BasicReadWriteSample` | Focused typed reads, contiguous block reads, and an opt-in random write/readback/restore section. |
| `PlcComm.KvHostLink.PollingReconnectSample` | Read-only polling loop with automatic reconnect and backoff after transport loss. |
| `PlcComm.KvHostLink.MultiPlcMonitorSample` | Read-only multi-PLC monitoring with one task and reconnect loop per PLC. |
| `PlcComm.KvHostLink.ConfigPollingSample` | Read-only JSON-configured polling with `--dry-run` and long-form `timestamp,plc,tag,value` CSV output; YAML config is Python-only. |
| `PlcComm.KvHostLink.NamedPollingSample` | Mixed snapshots and polling, plus opt-in bit-in-word writes that restore changed bits. |
