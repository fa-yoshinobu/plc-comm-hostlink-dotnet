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
