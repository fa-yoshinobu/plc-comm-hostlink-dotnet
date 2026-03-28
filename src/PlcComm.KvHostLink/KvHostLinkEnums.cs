namespace PlcComm.KvHostLink;

/// <summary>
/// Transport protocol for Host Link communication.
/// </summary>
public enum HostLinkTransportMode
{
    Tcp,
    Udp
}

/// <summary>
/// PLC operating mode.
/// </summary>
public enum KvPlcMode
{
    Program = 0,
    Run = 1
}

/// <summary>
/// Direction of a traced frame.
/// </summary>
public enum HostLinkTraceDirection
{
    Send,
    Receive
}

/// <summary>
/// A raw frame captured by <see cref="KvHostLinkClient.TraceHook"/>.
/// </summary>
public record HostLinkTraceFrame(
    HostLinkTraceDirection Direction,
    byte[] Data,
    DateTime Timestamp);
