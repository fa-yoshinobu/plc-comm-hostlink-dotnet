using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using PlcComm.KvHostLink;

namespace PlcComm.KvHostLink.Tests;

/// <summary>
/// Cross-language spec compliance: verifies that .NET sends the same ASCII frame bodies
/// as defined in hostlink_frame_vectors.json (shared with Python tests).
/// A loopback echo server captures sent frames without requiring a real PLC.
/// </summary>
public sealed class HostLinkFrameVectorTests
{
    private static readonly string VectorsPath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "vectors", "hostlink_frame_vectors.json");

    public static IEnumerable<object[]> Vectors()
    {
        var json = File.ReadAllText(VectorsPath);
        var doc = JsonDocument.Parse(json);
        foreach (var v in doc.RootElement.GetProperty("vectors").EnumerateArray())
        {
            yield return [v.Clone()];
        }
    }

    private static (TcpListener server, Queue<string> received) StartEchoServer()
    {
        var received = new Queue<string>();
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        _ = Task.Run(async () =>
        {
            try
            {
                using var conn = await server.AcceptTcpClientAsync().ConfigureAwait(false);
                using var stream = conn.GetStream();
                var buf = new byte[4096];
                var partial = new List<byte>();
                while (true)
                {
                    int n = await stream.ReadAsync(buf).ConfigureAwait(false);
                    if (n == 0) break;
                    for (int i = 0; i < n; i++)
                    {
                        byte b = buf[i];
                        if (b == (byte)'\r' || b == (byte)'\n')
                        {
                            if (partial.Count > 0)
                            {
                                lock (received) received.Enqueue(Encoding.ASCII.GetString([.. partial]));
                                partial.Clear();
                            }
                        }
                        else
                        {
                            partial.Add(b);
                        }
                    }
                    var ok = "OK\r\n"u8.ToArray();
                    await stream.WriteAsync(ok).ConfigureAwait(false);
                }
            }
            catch { /* server stopped */ }
        });
        return (server, received);
    }

    private static async Task RunCommandAsync(KvHostLinkClient client, JsonElement vec)
    {
        var cmd = vec.GetProperty("command").GetString()!;
        switch (cmd)
        {
            case "read":
                await client.ReadAsync(vec.GetProperty("device").GetString()!).ConfigureAwait(false);
                break;
            case "read_consecutive":
                await client.ReadConsecutiveAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("count").GetInt32()).ConfigureAwait(false);
                break;
            case "write":
                await client.WriteAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("value").GetInt32()).ConfigureAwait(false);
                break;
            case "write_consecutive":
                var values = vec.GetProperty("values").EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToArray();
                await client.WriteConsecutiveAsync(
                    vec.GetProperty("device").GetString()!, values).ConfigureAwait(false);
                break;
            case "change_mode":
                var mode = vec.GetProperty("mode").GetString()! == "RUN"
                    ? KvPlcMode.Run : KvPlcMode.Program;
                await client.ChangeModeAsync(mode).ConfigureAwait(false);
                break;
            case "clear_error":
                await client.ClearErrorAsync().ConfigureAwait(false);
                break;
            case "set_time":
                var dt = DateTime.Parse(
                    vec.GetProperty("dotnet_datetime").GetString()!,
                    CultureInfo.InvariantCulture);
                await client.SetTimeAsync(dt).ConfigureAwait(false);
                break;
            case "read_format":
                await client.ReadAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("data_format").GetString()).ConfigureAwait(false);
                break;
            case "read_consecutive_legacy":
                await client.ReadConsecutiveLegacyAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("count").GetInt32()).ConfigureAwait(false);
                break;
            case "register_monitor_bits":
                await client.RegisterMonitorBitsAsync(
                    vec.GetProperty("devices").EnumerateArray().Select(item => item.GetString()!)).ConfigureAwait(false);
                break;
            case "register_monitor_words":
                await client.RegisterMonitorWordsAsync(
                    vec.GetProperty("devices").EnumerateArray().Select(item => item.GetString()!)).ConfigureAwait(false);
                break;
            case "write_set_value":
                await client.WriteSetValueAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("value").GetInt32()).ConfigureAwait(false);
                break;
        }
    }

    [Theory]
    [MemberData(nameof(Vectors))]
    public async Task FrameVector_SendsCorrectBody(JsonElement vec)
    {
        var id = vec.GetProperty("id").GetString()!;
        var expectedBody = vec.GetProperty("expected_body").GetString()!;

        var (server, received) = StartEchoServer();
        var port = ((IPEndPoint)server.LocalEndpoint).Port;

        using var client = new KvHostLinkClient("127.0.0.1", port);
        try
        {
            await RunCommandAsync(client, vec);
        }
        catch (Exception ex) when (ex is not HostLinkProtocolError)
        {
            // Ignore TCP/parsing errors; we only care what was sent
        }
        finally
        {
            server.Stop();
            client.Close();
        }

        Assert.True(received.TryDequeue(out var actual),
            $"[{id}] No frame was received by the echo server");
        Assert.Equal(expectedBody, actual);
    }
}
