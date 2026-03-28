# plc-comm-hostlink-dotnet / samples

Buildable sample projects for the recommended high-level API.

## Projects

| Project | Primary APIs | Description |
|---|---|---|
| `PlcComm.KvHostLink.HighLevelSample` | `OpenAndConnectAsync`, `ReadTypedAsync`, `WriteTypedAsync`, `ReadWordsAsync`, `ReadDWordsAsync`, `WriteBitInWordAsync`, `ReadNamedAsync`, `PollAsync` | Guided tour of all high-level APIs |
| `PlcComm.KvHostLink.BasicReadWriteSample` | `ReadTypedAsync`, `WriteTypedAsync`, `ReadWordsAsync`, `ReadDWordsAsync` | Focused typed read/write and block read example |
| `PlcComm.KvHostLink.NamedPollingSample` | `ReadNamedAsync`, `WriteBitInWordAsync`, `PollAsync` | Mixed snapshot, bit-in-word, and polling example |

## Quick start

```powershell
dotnet build samples/PlcComm.KvHostLink.HighLevelSample/PlcComm.KvHostLink.HighLevelSample.csproj -c Debug
dotnet build samples/PlcComm.KvHostLink.BasicReadWriteSample/PlcComm.KvHostLink.BasicReadWriteSample.csproj -c Debug
dotnet build samples/PlcComm.KvHostLink.NamedPollingSample/PlcComm.KvHostLink.NamedPollingSample.csproj -c Debug

dotnet run --project samples/PlcComm.KvHostLink.HighLevelSample -- 192.168.250.100 8501
dotnet run --project samples/PlcComm.KvHostLink.BasicReadWriteSample -- 192.168.250.100 8501
dotnet run --project samples/PlcComm.KvHostLink.NamedPollingSample -- 192.168.250.100 8501
```

CI validates these projects with explicit `dotnet build` commands and checks
that the published docs reference every user-facing sample project.
