using PlcComm.KvHostLink;
using PlcComm.KvHostLink.Samples;

var options = MultiPlcOptions.Parse(args);
if (options is null)
    return 2;

if (options.MaxBackoff < options.InitialBackoff)
    throw new ArgumentException("--max-backoff must be greater than or equal to --initial-backoff.");

if (options.DryRun)
{
    foreach (var endpoint in options.Endpoints)
    {
        Console.WriteLine($"{endpoint.Name}: {OperationalCommon.TransportLabel(endpoint.Transport)} {endpoint.Host}:{endpoint.Port} profile={endpoint.PlcProfile} interval={endpoint.Interval.TotalSeconds:0.###}s");
    }
    Console.WriteLine("tags: " + string.Join(", ", options.Tags.Select(tag => $"{tag.Name}={tag.Address}")));
    Console.WriteLine($"cycles: {(options.Cycles is null ? "forever" : options.Cycles.Value.ToString())}");
    return 0;
}

using var shutdown = new CancellationTokenSource();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    shutdown.Cancel();
};

await Task.WhenAll(options.Endpoints.Select(endpoint =>
    OperationalCommon.MonitorEndpointAsync(
        endpoint,
        options.Tags,
        options.Cycles,
        options.InitialBackoff,
        options.MaxBackoff,
        OperationalCommon.IgnoreSnapshotAsync,
        shutdown.Token)));

return 0;

internal sealed record MultiPlcOptions(
    IReadOnlyList<PlcEndpoint> Endpoints,
    IReadOnlyList<TagSpec> Tags,
    int? Cycles,
    TimeSpan InitialBackoff,
    TimeSpan MaxBackoff,
    bool DryRun)
{
    public static MultiPlcOptions? Parse(string[] args)
    {
        var plcSpecs = new List<string>();
        var tagSpecs = new List<string>();
        var defaultPort = 8501;
        var defaultTransport = HostLinkTransportMode.Tcp;
        var timeout = TimeSpan.FromSeconds(3);
        var interval = TimeSpan.FromSeconds(1);
        int? cycles = null;
        var initialBackoff = TimeSpan.FromSeconds(1);
        var maxBackoff = TimeSpan.FromSeconds(30);
        var dryRun = false;

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            switch (arg)
            {
                case "--help":
                case "-h":
                    PrintUsage();
                    return null;
                case "--plc":
                    plcSpecs.Add(RequireValue(args, ref i, arg));
                    break;
                case "--tag":
                    tagSpecs.Add(RequireValue(args, ref i, arg));
                    break;
                case "--port":
                    defaultPort = OperationalCommon.ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;
                case "--transport":
                    defaultTransport = OperationalCommon.ParseTransport(RequireValue(args, ref i, arg));
                    break;
                case "--timeout":
                    timeout = TimeSpan.FromSeconds(OperationalCommon.ParsePositiveDouble(RequireValue(args, ref i, arg), arg));
                    break;
                case "--interval":
                    interval = TimeSpan.FromSeconds(OperationalCommon.ParsePositiveDouble(RequireValue(args, ref i, arg), arg));
                    break;
                case "--cycles":
                    cycles = OperationalCommon.ParsePositiveInt(RequireValue(args, ref i, arg), arg);
                    break;
                case "--initial-backoff":
                    initialBackoff = TimeSpan.FromSeconds(OperationalCommon.ParsePositiveDouble(RequireValue(args, ref i, arg), arg));
                    break;
                case "--max-backoff":
                    maxBackoff = TimeSpan.FromSeconds(OperationalCommon.ParsePositiveDouble(RequireValue(args, ref i, arg), arg));
                    break;
                case "--dry-run":
                    dryRun = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {arg}");
            }
        }

        if (plcSpecs.Count == 0)
            throw new ArgumentException("--plc is required and can be repeated.");

        var endpoints = plcSpecs
            .Select(spec => OperationalCommon.ParsePlcSpec(spec, defaultPort, defaultTransport, timeout, interval))
            .ToArray();
        var tags = tagSpecs.Count == 0 ? [OperationalCommon.DefaultTag()] : tagSpecs.Select(OperationalCommon.ParseTagSpec).ToArray();
        return new MultiPlcOptions(endpoints, tags, cycles, initialBackoff, maxBackoff, dryRun);
    }

    private static string RequireValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{option} requires a value.");
        index++;
        return args[index];
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Read the same tag set from multiple KEYENCE KV Host Link PLCs concurrently.");
        Console.WriteLine("Usage:");
        Console.WriteLine("  dotnet run --project samples/PlcComm.KvHostLink.MultiPlcMonitorSample -- --plc NAME=HOST,PROFILE[,PORT[,TRANSPORT]] [--plc ...] [--tag NAME=ADDRESS]");
    }
}
