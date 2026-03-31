using PlcComm.KvHostLink;

var host = args.Length > 0 ? args[0] : "192.168.250.100";
var port = args.Length > 1 ? int.Parse(args[1]) : 8501;

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

await client.WriteTypedAsync("DM100", "U", dm0);
await client.WriteTypedAsync("DM101", "S", dm1);
await client.WriteTypedAsync("DM102", "D", dm2);
await client.WriteTypedAsync("DM104", "F", dm4);
Console.WriteLine("Mirrored source values into DM100/DM101/DM102/DM104");

ushort[] words = await client.ReadWordsSingleRequestAsync("DM200", 6);
uint[] dwords = await client.ReadDWordsSingleRequestAsync("DM300", 3);
Console.WriteLine($"DM200-DM205 = [{string.Join(", ", words)}]");
Console.WriteLine($"DM300-DM305 = [{string.Join(", ", dwords)}]");

Console.WriteLine("Done.");
