using PlcComm.KvHostLink;

if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/PlcComm.KvHostLink.NamedPollingSample -- <host> <port> <transport> <plc-profile>");
    Console.Error.WriteLine("Example: dotnet run --project samples/PlcComm.KvHostLink.NamedPollingSample -- 192.168.250.100 8501 tcp keyence:kv-8000");
    return;
}

var host = args[0];
var port = int.Parse(args[1]);
var transport = args[2].ToLowerInvariant() switch
{
    "tcp" => HostLinkTransportMode.Tcp,
    "udp" => HostLinkTransportMode.Udp,
    _ => throw new ArgumentException("transport must be tcp or udp."),
};
var plcProfile = args[3];
const string bitWordAddress = "DM126";
string bit0Address = $"{bitWordAddress}.0";
string bit3Address = $"{bitWordAddress}.3";

Console.WriteLine($"Connecting to {host}:{port} ({plcProfile}) ...");
var options = new KvHostLinkConnectionOptions(host, port, transport, plcProfile);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

string[] aggregateAddresses = ["DM0:U", "DM1:S", "DM2:D", "DM4:F", bit0Address, bit3Address];
// This mixed result is explicitly non-atomic if the planner needs multiple requests.
var aggregate = await client.ReadNamedAsync(aggregateAddresses);

foreach (var (address, value) in aggregate)
    Console.WriteLine($"{address} = {value}");

Console.WriteLine("Polling 3 non-atomic aggregate results ...");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var pollCount = 0;
string[] pollAddresses = ["DM0:U", "DM1:S", "DM4:F", bit0Address];
await foreach (var result in client.PollAsync(
    pollAddresses,
    TimeSpan.FromSeconds(1),
    cts.Token))
{
    pollCount++;
    Console.WriteLine(
        $"[{pollCount}] DM0:U={result["DM0:U"]} DM1:S={result["DM1:S"]} " +
        $"DM4:F={result["DM4:F"]} {bit0Address}={result[bit0Address]}");

    if (pollCount >= 3)
        break;
}

Console.WriteLine("Done.");
