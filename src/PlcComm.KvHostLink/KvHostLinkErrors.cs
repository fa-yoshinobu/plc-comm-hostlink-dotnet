namespace PlcComm.KvHostLink;

#pragma warning disable CA1710 // Intentionally named *Error (not *Exception) for cross-language API consistency
/// <summary>
/// Base exception for Host Link communication.
/// </summary>
public class HostLinkError : Exception
{
    public string? Code { get; }
    public string? Response { get; }

    public HostLinkError(string message) : base(message) { }
    public HostLinkError(string message, Exception inner) : base(message, inner) { }
    public HostLinkError(string message, string code, string response) : base(message)
    {
        Code = code;
        Response = response;
    }
}

/// <summary>
/// Thrown when there is an error in the protocol or unexpected response.
/// </summary>
public class HostLinkProtocolError : HostLinkError
{
    public HostLinkProtocolError(string message) : base(message) { }
    public HostLinkProtocolError(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a connection error occurs.
/// </summary>
public class HostLinkConnectionError : HostLinkError
{
    public HostLinkConnectionError(string message) : base(message) { }
    public HostLinkConnectionError(string message, Exception inner) : base(message, inner) { }
}
#pragma warning restore CA1710
