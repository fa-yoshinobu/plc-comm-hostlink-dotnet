using System.Globalization;
using System.IO;
using System.Net.Sockets;
using PlcComm.KvHostLink;

namespace PlcComm.KvHostLink.Samples;

internal sealed record TagSpec(string Name, string Address);

internal sealed record PlcEndpoint(
    string Name,
    string Host,
    string PlcProfile,
    int Port,
    HostLinkTransportMode Transport,
    TimeSpan Timeout,
    TimeSpan Interval);

internal static class OperationalCommon
{
    public static double ParsePositiveDouble(string value, string name)
    {
        var parsed = double.Parse(value, CultureInfo.InvariantCulture);
        if (parsed <= 0)
            throw new ArgumentException($"{name} must be greater than zero.", name);
        return parsed;
    }

    public static int ParsePositiveInt(string value, string name)
    {
        var parsed = int.Parse(value, CultureInfo.InvariantCulture);
        if (parsed <= 0)
            throw new ArgumentException($"{name} must be greater than zero.", name);
        return parsed;
    }

    public static HostLinkTransportMode ParseTransport(string value)
    {
        if (value.Equals("tcp", StringComparison.OrdinalIgnoreCase))
            return HostLinkTransportMode.Tcp;
        if (value.Equals("udp", StringComparison.OrdinalIgnoreCase))
            return HostLinkTransportMode.Udp;
        throw new ArgumentException("transport must be tcp or udp.", nameof(value));
    }

    public static string TransportLabel(HostLinkTransportMode transport)
        => transport == HostLinkTransportMode.Udp ? "udp" : "tcp";

    public static TagSpec ParseTagSpec(string value)
    {
        var parts = value.Split('=', 2);
        if (parts.Length == 2)
        {
            if (string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
                throw new ArgumentException("tag must be NAME=ADDRESS.", nameof(value));
            return new TagSpec(parts[0].Trim(), parts[1].Trim());
        }

        return new TagSpec(NormalizeTagName(value), value.Trim());
    }

    public static TagSpec DefaultTag() => ParseTagSpec("DM100:U");

    public static PlcEndpoint ParsePlcSpec(
        string value,
        TimeSpan defaultTimeout,
        TimeSpan defaultInterval)
    {
        var named = value.Split('=', 2);
        if (named.Length != 2 || string.IsNullOrWhiteSpace(named[0]) || string.IsNullOrWhiteSpace(named[1]))
            throw new ArgumentException("PLC must be NAME=HOST,PROFILE,PORT,TRANSPORT.", nameof(value));

        var parts = named[1].Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4 || parts.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("PLC must be NAME=HOST,PROFILE,PORT,TRANSPORT.", nameof(value));

        var port = ParsePositiveInt(parts[2], "port");
        var transport = ParseTransport(parts[3]);

        return new PlcEndpoint(
            named[0].Trim(),
            parts[0],
            parts[1],
            port,
            transport,
            defaultTimeout,
            defaultInterval);
    }

    public static (string Device, string DType) SplitAddress(string address)
    {
        var trimmed = address.Trim();
        var index = trimmed.LastIndexOf(':');
        if (index < 0)
            return (trimmed, "U");
        if (index == 0 || index == trimmed.Length - 1)
            throw new ArgumentException($"Address '{address}' must use DEVICE:DTYPE.");
        return (trimmed[..index], trimmed[(index + 1)..].ToUpperInvariant());
    }

    public static async Task MonitorEndpointAsync(
        PlcEndpoint endpoint,
        IReadOnlyList<TagSpec> tags,
        int? cycles,
        TimeSpan initialBackoff,
        TimeSpan maxBackoff,
        Func<PlcEndpoint, IReadOnlyDictionary<string, object>, CancellationToken, Task> handleSnapshot,
        CancellationToken cancellationToken)
    {
        if (tags.Count == 0)
            throw new ArgumentException("at least one tag is required.", nameof(tags));

        QueuedKvHostLinkClient? client = null;
        var backoff = initialBackoff;
        var connectedOnce = false;
        var completed = 0;

        try
        {
            while (!cancellationToken.IsCancellationRequested && (cycles is null || completed < cycles.Value))
            {
                if (client is null)
                {
                    LogState(endpoint.Name, "reconnecting", $"{TransportLabel(endpoint.Transport)} {endpoint.Host}:{endpoint.Port} profile={endpoint.PlcProfile}");
                    try
                    {
                        client = await KvHostLinkClientFactory.OpenAndConnectAsync(BuildOptions(endpoint), cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (IsRetryable(ex) && !cancellationToken.IsCancellationRequested)
                    {
                        LogState(endpoint.Name, "reconnecting", $"connect failed: {ex.Message}; retry in {backoff.TotalSeconds:0.0}s");
                        await Delay(backoff, cancellationToken).ConfigureAwait(false);
                        backoff = NextBackoff(backoff, maxBackoff);
                        continue;
                    }

                    LogState(endpoint.Name, connectedOnce ? "recovered" : "connected", $"{tags.Count} tags");
                    connectedOnce = true;
                    backoff = initialBackoff;
                }

                try
                {
                    var snapshot = new Dictionary<string, object>(StringComparer.Ordinal);
                    foreach (var tag in tags)
                    {
                        var (device, dtype) = SplitAddress(tag.Address);
                        snapshot[tag.Name] = await client.ReadTypedAsync(device, dtype, cancellationToken).ConfigureAwait(false);
                    }

                    LogState(endpoint.Name, "read", FormatSnapshot(snapshot));
                    await handleSnapshot(endpoint, snapshot, cancellationToken).ConfigureAwait(false);
                    completed++;
                    if (cycles is null || completed < cycles.Value)
                        await Task.Delay(endpoint.Interval, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex) when (IsRetryable(ex) && !cancellationToken.IsCancellationRequested)
                {
                    LogState(endpoint.Name, "lost", ex.Message);
                    await DisposeClientAsync(client).ConfigureAwait(false);
                    client = null;
                    LogState(endpoint.Name, "reconnecting", $"retry in {backoff.TotalSeconds:0.0}s");
                    await Delay(backoff, cancellationToken).ConfigureAwait(false);
                    backoff = NextBackoff(backoff, maxBackoff);
                }
            }
        }
        finally
        {
            await DisposeClientAsync(client).ConfigureAwait(false);
        }
    }

    public static Task IgnoreSnapshotAsync(
        PlcEndpoint _endpoint,
        IReadOnlyDictionary<string, object> _snapshot,
        CancellationToken _cancellationToken)
        => Task.CompletedTask;

    public static void LogState(string plcName, string state, string message)
        => Console.WriteLine($"{DateTimeOffset.Now:yyyy-MM-ddTHH:mm:ss} [{plcName}] [{state}] {message}");

    public static string FormatValue(object value)
        => value switch
        {
            float f => f.ToString("G9", CultureInfo.InvariantCulture),
            double d => d.ToString("G17", CultureInfo.InvariantCulture),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty,
        };

    private static KvHostLinkConnectionOptions BuildOptions(PlcEndpoint endpoint)
        => new(endpoint.Host, endpoint.Port, endpoint.Transport, endpoint.PlcProfile, endpoint.Timeout);

    private static string NormalizeTagName(string address)
    {
        var chars = address.Trim().Select(ch => char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '_').ToArray();
        return new string(chars).Trim('_');
    }

    private static bool IsRetryable(Exception ex)
        => ex is IOException or SocketException or TimeoutException or OperationCanceledException or HostLinkConnectionError;

    private static string FormatSnapshot(IReadOnlyDictionary<string, object> snapshot)
        => string.Join(", ", snapshot.Select(item => $"{item.Key}={FormatValue(item.Value)}"));

    private static TimeSpan NextBackoff(TimeSpan current, TimeSpan max)
        => TimeSpan.FromSeconds(Math.Min(current.TotalSeconds * 2.0, max.TotalSeconds));

    private static async Task Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private static async Task DisposeClientAsync(QueuedKvHostLinkClient? client)
    {
        if (client is not null)
            await client.DisposeAsync().ConfigureAwait(false);
    }
}
