using System.Net;
using System.Net.Sockets;

namespace PlcComm.KvHostLink;

internal static class KvHostLinkNetwork
{
    internal static string ValidateIpv4Host(string host, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host must not be empty.", parameterName);

        string normalized = host.Trim();
        if (normalized.Length >= 2 && normalized[0] == '[' && normalized[^1] == ']' &&
            IPAddress.TryParse(normalized[1..^1], out IPAddress? bracketedLiteral) &&
            bracketedLiteral.AddressFamily == AddressFamily.InterNetwork)
        {
            throw new ArgumentException(
                "IPv4 addresses must not be enclosed in brackets.",
                parameterName);
        }

        if (IPAddress.TryParse(normalized, out IPAddress? literal) &&
            literal.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new ArgumentException(
                "Host must be an IPv4 address or a hostname that resolves to IPv4. IPv6 is not supported.",
                parameterName);
        }

        if (normalized.Contains(':', StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Host must be an IPv4 address or a hostname that resolves to IPv4. IPv6 is not supported.",
                parameterName);
        }

        return normalized;
    }

    internal static async Task<IPAddress> ResolveIpv4AddressAsync(
        string host,
        CancellationToken cancellationToken)
    {
        if (IPAddress.TryParse(host, out IPAddress? literal))
            return literal.AddressFamily == AddressFamily.InterNetwork
                ? literal
                : throw new HostLinkConnectionError($"Host is not an IPv4 address: {host}");

        IPAddress[] addresses;
        try
        {
            addresses = await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception error) when (error is SocketException or ArgumentException)
        {
            throw new HostLinkConnectionError($"Failed to resolve Host Link host '{host}' to IPv4.", error);
        }

        foreach (IPAddress address in addresses)
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
                return address;
        }

        throw new HostLinkConnectionError($"Host did not resolve to an IPv4 address: {host}");
    }
}
