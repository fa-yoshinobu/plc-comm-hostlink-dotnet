# User Guide: Host Link Communication .NET

This guide provides instructions for using the `PlcComm.KvHostLink` library to communicate with KEYENCE KV series PLCs using the Host Link (Upper Link) protocol.

## 1. Getting Started

### Installation
Reference the project or add the compiled `PlcComm.KvHostLink.dll` to your .NET 9.0 project.

### Basic Usage
The primary class for communication is `KvHostLinkClient`.

```csharp
using PlcComm.KvHostLink;

// Initialize the client (Default: TCP, Port 8501)
using var client = new KvHostLinkClient("192.168.1.10");

// Open the connection
await client.OpenAsync();

// Read a single word from DM100
var values = await client.ReadAsync("DM100");
Console.WriteLine($"DM100: {values[0]}");

// Write a value to DM100
await client.WriteAsync("DM100", 1234);
```

## 2. Advanced Features

### Device Addressing
The library supports various KEYENCE device types and data formats:
- **Bit devices**: R, B, MR, LR, CR, etc.
- **Word devices**: DM, EM, FM, W, TM, Z, etc.
- **Data Formats**: `.U` (Unsigned 16-bit), `.S` (Signed 16-bit), `.D` (Unsigned 32-bit), `.L` (Signed 32-bit), `.H` (Hexadecimal).

```csharp
// Read 32-bit signed integer from DM200
var dword = await client.ReadAsync("DM200.L");

// Read 10 consecutive words from DM300
var batch = await client.ReadConsecutiveAsync("DM300", 10);
```

### Monitoring
You can register devices for monitoring and read them efficiently.

```csharp
// Register devices
await client.RegisterMonitorWordsAsync(new[] { "DM0", "DM1", "DM2" });

// Read registered devices
var monitorData = await client.ReadMonitorWordsAsync();
```

## 3. Error Handling
The library uses custom exceptions for protocol and connection errors.

- `HostLinkConnectionException`: Network or connection issues.
- `HostLinkProtocolException`: Invalid device formats or protocol violations.
- `HostLinkException`: Generic errors returned by the PLC (e.g., E0, E1).

```csharp
try {
    await client.ReadAsync("INVALID_DEVICE");
} catch (HostLinkProtocolException ex) {
    Console.WriteLine($"Protocol error: {ex.Message}");
}
```
