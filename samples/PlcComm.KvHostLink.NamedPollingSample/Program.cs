using PlcComm.KvHostLink;

var host = args.Length > 0 ? args[0] : "192.168.250.100";
var port = args.Length > 1 ? int.Parse(args[1]) : 8501;
const string bitWordAddress = "DM126";
string bit0Address = $"{bitWordAddress}.0";
string bit3Address = $"{bitWordAddress}.3";

Console.WriteLine($"Connecting to {host}:{port} ...");
var options = new KvHostLinkConnectionOptions(host, port);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

var originalBits = await client.ReadNamedAsync([bit0Address, bit3Address]);
bool originalBit0 = (bool)originalBits[bit0Address];
bool originalBit3 = (bool)originalBits[bit3Address];

try
{
    await client.WriteBitInWordAsync(bitWordAddress, bitIndex: 0, value: true);
    await client.WriteBitInWordAsync(bitWordAddress, bitIndex: 3, value: false);
    Console.WriteLine($"Updated {bitWordAddress} bit0=True bit3=False");

    string[] snapshotAddresses = ["DM0", "DM1:S", "DM2:D", "DM4:F", bit0Address, bit3Address];
    var snapshot = await client.ReadNamedAsync(snapshotAddresses);

    if ((bool)snapshot[bit0Address] != true)
        throw new InvalidOperationException($"{bit0Address} readback mismatch");
    if ((bool)snapshot[bit3Address] != false)
        throw new InvalidOperationException($"{bit3Address} readback mismatch");

    foreach (var (address, value) in snapshot)
        Console.WriteLine($"{address} = {value}");

    Console.WriteLine("Polling 3 snapshots ...");
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var pollCount = 0;
    string[] pollAddresses = ["DM0", "DM1:S", "DM4:F", bit0Address];
    await foreach (var snap in client.PollAsync(
        pollAddresses,
        TimeSpan.FromSeconds(1),
        cts.Token))
    {
        pollCount++;
        Console.WriteLine(
            $"[{pollCount}] DM0={snap["DM0"]} DM1:S={snap["DM1:S"]} " +
            $"DM4:F={snap["DM4:F"]} {bit0Address}={snap[bit0Address]}");

        if (pollCount >= 3)
            break;
    }
}
finally
{
    await client.WriteBitInWordAsync(bitWordAddress, bitIndex: 0, value: originalBit0);
    await client.WriteBitInWordAsync(bitWordAddress, bitIndex: 3, value: originalBit3);
    Console.WriteLine($"Restored {bit0Address}/{bit3Address}");
}

Console.WriteLine("Done.");
