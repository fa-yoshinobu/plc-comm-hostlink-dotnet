using PlcComm.KvHostLink;

if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/PlcComm.KvHostLink.NamedPollingSample -- <host> <port> <transport> <plc-profile> [--allow-writes]");
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
var allowWrites = args.Skip(4).Contains("--allow-writes", StringComparer.Ordinal);
const string bitWordAddress = "DM126";
string bit0Address = $"{bitWordAddress}.0";
string bit3Address = $"{bitWordAddress}.3";

Console.WriteLine($"Connecting to {host}:{port} ({plcProfile}) ...");
var options = new KvHostLinkConnectionOptions(host, port, transport, plcProfile);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

// Read the original bit values so the sample can restore them later.
var originalBits = await client.ReadNamedAsync([bit0Address, bit3Address]);
bool originalBit0 = (bool)originalBits[bit0Address];
bool originalBit3 = (bool)originalBits[bit3Address];

try
{
    bool expectedBit0 = originalBit0;
    bool expectedBit3 = originalBit3;
    if (allowWrites)
    {
        expectedBit0 = !originalBit0;
        expectedBit3 = !originalBit3;
        await client.WriteBitInWordAsync(bitWordAddress, bitIndex: 0, value: expectedBit0);
        await client.WriteBitInWordAsync(bitWordAddress, bitIndex: 3, value: expectedBit3);
        Console.WriteLine($"Updated {bitWordAddress} bit0={expectedBit0} bit3={expectedBit3}");
    }
    else
    {
        Console.WriteLine("[Read-only] Bit writes skipped. Add --allow-writes only for a controlled test word.");
    }

    string[] snapshotAddresses = ["DM0:U", "DM1:S", "DM2:D", "DM4:F", bit0Address, bit3Address];
    // Read a mixed snapshot containing word values and bit-in-word values.
    var snapshot = await client.ReadNamedAsync(snapshotAddresses);

    if ((bool)snapshot[bit0Address] != expectedBit0)
        throw new InvalidOperationException($"{bit0Address} readback mismatch");
    if ((bool)snapshot[bit3Address] != expectedBit3)
        throw new InvalidOperationException($"{bit3Address} readback mismatch");

    foreach (var (address, value) in snapshot)
        Console.WriteLine($"{address} = {value}");

    Console.WriteLine("Polling 3 snapshots ...");
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
    var pollCount = 0;
    string[] pollAddresses = ["DM0:U", "DM1:S", "DM4:F", bit0Address];
    // Poll a named snapshot once per second.
    await foreach (var snap in client.PollAsync(
        pollAddresses,
        TimeSpan.FromSeconds(1),
        cts.Token))
    {
        pollCount++;
        Console.WriteLine(
            $"[{pollCount}] DM0:U={snap["DM0:U"]} DM1:S={snap["DM1:S"]} " +
            $"DM4:F={snap["DM4:F"]} {bit0Address}={snap[bit0Address]}");

        if (pollCount >= 3)
            break;
    }
}
finally
{
    if (allowWrites)
    {
        // Restore the bits this sample changed.
        await client.WriteBitInWordAsync(bitWordAddress, bitIndex: 0, value: originalBit0);
        await client.WriteBitInWordAsync(bitWordAddress, bitIndex: 3, value: originalBit3);
        Console.WriteLine($"Restored {bit0Address}/{bit3Address}");
    }
}

Console.WriteLine("Done.");
