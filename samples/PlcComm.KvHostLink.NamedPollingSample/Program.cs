using PlcComm.KvHostLink;

var host = args.Length > 0 ? args[0] : "192.168.250.100";
var port = args.Length > 1 ? int.Parse(args[1]) : 8501;

Console.WriteLine($"Connecting to {host}:{port} ...");
var options = new KvHostLinkConnectionOptions(host, port);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

await client.WriteBitInWordAsync("DM50", bitIndex: 0, value: true);
await client.WriteBitInWordAsync("DM50", bitIndex: 3, value: false);
Console.WriteLine("Updated DM50 bit0=True bit3=False");

string[] snapshotAddresses = ["DM0", "DM1:S", "DM2:D", "DM4:F", "DM50.0", "DM50.3"];
var snapshot = await client.ReadNamedAsync(snapshotAddresses);

foreach (var (address, value) in snapshot)
    Console.WriteLine($"{address} = {value}");

Console.WriteLine("Polling 3 snapshots ...");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
var pollCount = 0;
string[] pollAddresses = ["DM0", "DM1:S", "DM4:F", "DM50.0"];
await foreach (var snap in client.PollAsync(
    pollAddresses,
    TimeSpan.FromSeconds(1),
    cts.Token))
{
    pollCount++;
    Console.WriteLine(
        $"[{pollCount}] DM0={snap["DM0"]} DM1:S={snap["DM1:S"]} " +
        $"DM4:F={snap["DM4:F"]} DM50.0={snap["DM50.0"]}");

    if (pollCount >= 3)
        break;
}

Console.WriteLine("Done.");
