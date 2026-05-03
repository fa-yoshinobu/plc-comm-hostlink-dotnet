using PlcComm.KvHostLink;

var host = args.Length > 0 ? args[0] : "192.168.250.100";
var port = args.Length > 1 ? int.Parse(args[1]) : 8501;
const string targetU16 = "DM120";
const string targetI16 = "DM121";
const string targetU32 = "DM122";
const string targetF32 = "DM124";

Console.WriteLine($"Connecting to {host}:{port} ...");
var options = new KvHostLinkConnectionOptions(host, port);
await using var client = await KvHostLinkClientFactory.OpenAndConnectAsync(options);

ushort dm0 = (ushort)await client.ReadTypedAsync("DM0", "U");
short dm1 = (short)await client.ReadTypedAsync("DM1", "S");
uint dm2 = (uint)await client.ReadTypedAsync("DM2", "D");
float dm4 = (float)await client.ReadTypedAsync("DM4", "F");

Console.WriteLine($"DM0(U)={dm0}");
Console.WriteLine($"DM1(S)={dm1}");
Console.WriteLine($"DM2(D)={dm2}");
Console.WriteLine($"DM4(F)={dm4}");

ushort originalU16 = (ushort)await client.ReadTypedAsync(targetU16, "U");
short originalI16 = (short)await client.ReadTypedAsync(targetI16, "S");
uint originalU32 = (uint)await client.ReadTypedAsync(targetU32, "D");
float originalF32 = (float)await client.ReadTypedAsync(targetF32, "F");

try
{
    await client.WriteTypedAsync(targetU16, "U", dm0);
    await client.WriteTypedAsync(targetI16, "S", dm1);
    await client.WriteTypedAsync(targetU32, "D", dm2);
    await client.WriteTypedAsync(targetF32, "F", dm4);

    ushort readbackU16 = (ushort)await client.ReadTypedAsync(targetU16, "U");
    short readbackI16 = (short)await client.ReadTypedAsync(targetI16, "S");
    uint readbackU32 = (uint)await client.ReadTypedAsync(targetU32, "D");
    float readbackF32 = (float)await client.ReadTypedAsync(targetF32, "F");

    if (readbackU16 != dm0)
        throw new InvalidOperationException($"{targetU16} readback mismatch: expected {dm0}, got {readbackU16}");
    if (readbackI16 != dm1)
        throw new InvalidOperationException($"{targetI16} readback mismatch: expected {dm1}, got {readbackI16}");
    if (readbackU32 != dm2)
        throw new InvalidOperationException($"{targetU32} readback mismatch: expected {dm2}, got {readbackU32}");
    if (Math.Abs(readbackF32 - dm4) > 0.0001f)
        throw new InvalidOperationException($"{targetF32} readback mismatch: expected {dm4}, got {readbackF32}");

    Console.WriteLine($"Mirrored source values into {targetU16}/{targetI16}/{targetU32}/{targetF32}");
    Console.WriteLine("Readback verified");
}
finally
{
    await client.WriteTypedAsync(targetU16, "U", originalU16);
    await client.WriteTypedAsync(targetI16, "S", originalI16);
    await client.WriteTypedAsync(targetU32, "D", originalU32);
    await client.WriteTypedAsync(targetF32, "F", originalF32);
    Console.WriteLine($"Restored {targetU16}/{targetI16}/{targetU32}/{targetF32}");
}

ushort[] words = await client.ReadWordsSingleRequestAsync("DM200", 6);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM300", 3);
Console.WriteLine($"DM200-DM205 = [{string.Join(", ", words)}]");
Console.WriteLine($"DM300-DM305 = [{string.Join(", ", dwords)}]");

Console.WriteLine("Done.");
