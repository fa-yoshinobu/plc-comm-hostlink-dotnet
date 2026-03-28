# Internal Architecture: KvHostLink .NET

## 1. Thread Safety and Concurrency
The `KvHostLinkClient` uses a `SemaphoreSlim(1, 1)` to ensure that only one communication request is active at a time per client instance. This is critical for the Host Link protocol as it is a request-response (half-duplex) protocol.

## 2. TCP Stream Parsing
Host Link responses are terminated by `CR` (`\r`) or `CRLF` (`\r\n`). 
The `RecvTcpLineAsync` method implements a buffering strategy:
1. It maintains an internal `_rxBuffer`.
2. It searches for terminator characters in the buffer.
3. If not found, it performs an asynchronous read from the `NetworkStream` and appends to the buffer.
4. Once a terminator is found, it extracts the line and shifts the buffer.

## 3. Validation Logic
Device validation is separated into `KvHostLinkDevice.cs`. It uses compiled Regex for performance and a centralized range dictionary (`KvHostLinkModels.cs`) to ensure consistency between the .NET and Python implementations.
