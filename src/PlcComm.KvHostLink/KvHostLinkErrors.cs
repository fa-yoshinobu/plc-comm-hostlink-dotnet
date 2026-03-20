namespace PlcComm.KvHostLink;

/// <summary>
/// Base exception for Host Link communication.
/// </summary>
public class HostLinkException : Exception
{
    public string? Code { get; }
    public string? Response { get; }

    public HostLinkException(string message) : base(message) { }
    public HostLinkException(string message, Exception inner) : base(message, inner) { }
    public HostLinkException(string message, string code, string response) : base(message)
    {
        Code = code;
        Response = response;
    }
}

/// <summary>
/// Thrown when there is an error in the protocol or unexpected response.
/// </summary>
public class HostLinkProtocolException : HostLinkException
{
    public HostLinkProtocolException(string message) : base(message) { }
    public HostLinkProtocolException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a connection error occurs.
/// </summary>
public class HostLinkConnectionException : HostLinkException
{
    public HostLinkConnectionException(string message) : base(message) { }
    public HostLinkConnectionException(string message, Exception inner) : base(message, inner) { }
}
