using PlcComm.KvHostLink;

if (args.Length < 4)
{
    Console.Error.WriteLine("Usage: dotnet run --project samples/PlcComm.KvHostLink.BasicReadWriteSample -- <host> <port> <transport> <plc-profile> [--allow-writes]");
    Console.Error.WriteLine("Example: dotnet run --project samples/PlcComm.KvHostLink.BasicReadWriteSample -- 192.168.250.100 8501 tcp keyence:kv-8000");
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
const string targetU16 = "DM120";
const string targetI16 = "DM121";
const string targetU32 = "DM122";
const string targetF32 = "DM124";

Console.WriteLine($"Connecting to {host}:{port} ({plcProfile}) ...");
var options = new KvHostLinkConnectionOptions(host, port, transport, plcProfile);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

// Read source values from DM devices with explicit high-level type suffixes.
ushort dm0 = (ushort)await client.ReadTypedAsync("DM0", "U");
short dm1 = (short)await client.ReadTypedAsync("DM1", "S");
uint dm2 = (uint)await client.ReadTypedAsync("DM2", "D");
float dm4 = (float)await client.ReadTypedAsync("DM4", "F");

Console.WriteLine($"DM0(U)={dm0}");
Console.WriteLine($"DM1(S)={dm1}");
Console.WriteLine($"DM2(D)={dm2}");
Console.WriteLine($"DM4(F)={dm4}");

if (allowWrites)
{
    // Capture original test-register values so the sample can restore them.
    ushort originalU16 = (ushort)await client.ReadTypedAsync(targetU16, "U");
    short originalI16 = (short)await client.ReadTypedAsync(targetI16, "S");
    uint originalU32 = (uint)await client.ReadTypedAsync(targetU32, "D");
    float originalF32 = (float)await client.ReadTypedAsync(targetF32, "F");
    ushort testU16 = (ushort)Random.Shared.Next(0, ushort.MaxValue + 1);
    short testI16 = (short)Random.Shared.Next(short.MinValue, short.MaxValue + 1);
    uint testU32 = (uint)Random.Shared.NextInt64(0, (long)uint.MaxValue + 1);
    float testF32 = (Random.Shared.NextSingle() * 20_000f) - 10_000f;

    try
    {
        // Use only DM addresses that are safe in a controlled PLC test program.
        await client.WriteTypedAsync(targetU16, "U", testU16);
        await client.WriteTypedAsync(targetI16, "S", testI16);
        await client.WriteTypedAsync(targetU32, "D", testU32);
        await client.WriteTypedAsync(targetF32, "F", testF32);

        // Read back each test address with the matching type suffix.
        ushort readbackU16 = (ushort)await client.ReadTypedAsync(targetU16, "U");
        short readbackI16 = (short)await client.ReadTypedAsync(targetI16, "S");
        uint readbackU32 = (uint)await client.ReadTypedAsync(targetU32, "D");
        float readbackF32 = (float)await client.ReadTypedAsync(targetF32, "F");

        if (readbackU16 != testU16)
            throw new InvalidOperationException($"{targetU16} readback mismatch: expected {testU16}, got {readbackU16}");
        if (readbackI16 != testI16)
            throw new InvalidOperationException($"{targetI16} readback mismatch: expected {testI16}, got {readbackI16}");
        if (readbackU32 != testU32)
            throw new InvalidOperationException($"{targetU32} readback mismatch: expected {testU32}, got {readbackU32}");
        if (Math.Abs(readbackF32 - testF32) > 0.0001f)
            throw new InvalidOperationException($"{targetF32} readback mismatch: expected {testF32}, got {readbackF32}");

        Console.WriteLine($"Wrote random test values into {targetU16}/{targetI16}/{targetU32}/{targetF32}");
        Console.WriteLine("Readback verified");
    }
    finally
    {
        // Restore the original values even if a readback check fails.
        await client.WriteTypedAsync(targetU16, "U", originalU16);
        await client.WriteTypedAsync(targetI16, "S", originalI16);
        await client.WriteTypedAsync(targetU32, "D", originalU32);
        await client.WriteTypedAsync(targetF32, "F", originalF32);
        Console.WriteLine($"Restored {targetU16}/{targetI16}/{targetU32}/{targetF32}");
    }
}
else
{
    Console.WriteLine("Write/readback example skipped. Add --allow-writes only for controlled test addresses.");
}

// Read contiguous blocks when values occupy adjacent DM words.
ushort[] words = await client.ReadWordsSingleRequestAsync("DM200", 6);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM300", 3);
Console.WriteLine($"DM200-DM205 = [{string.Join(", ", words)}]");
Console.WriteLine($"DM300-DM305 = [{string.Join(", ", dwords)}]");

Console.WriteLine("Done.");
