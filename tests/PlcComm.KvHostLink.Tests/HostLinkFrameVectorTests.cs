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
    private const string TestPlcProfile = "keyence:kv-8000";

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
                if (vec.TryGetProperty("data_format", out var readFormat))
                    await client.ReadAsync(
                        vec.GetProperty("device").GetString()!, readFormat.GetString()!).ConfigureAwait(false);
                else
                    await client.ReadAsync(vec.GetProperty("device").GetString()!).ConfigureAwait(false);
                break;
            case "read_consecutive":
                if (vec.TryGetProperty("data_format", out var readConsecutiveFormat))
                    await client.ReadConsecutiveAsync(
                        vec.GetProperty("device").GetString()!,
                        vec.GetProperty("count").GetInt32(),
                        readConsecutiveFormat.GetString()!).ConfigureAwait(false);
                else
                    await client.ReadConsecutiveAsync(
                        vec.GetProperty("device").GetString()!,
                        vec.GetProperty("count").GetInt32()).ConfigureAwait(false);
                break;
            case "write":
                if (vec.TryGetProperty("data_format", out var writeFormat))
                    await client.WriteAsync(
                        vec.GetProperty("device").GetString()!,
                        vec.GetProperty("value").GetInt32(),
                        writeFormat.GetString()!).ConfigureAwait(false);
                else
                    await client.WriteAsync(
                        vec.GetProperty("device").GetString()!,
                        vec.GetProperty("value").GetInt32()).ConfigureAwait(false);
                break;
            case "write_consecutive":
                var values = vec.GetProperty("values").EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToArray();
                await client.WriteConsecutiveAsync(
                    vec.GetProperty("device").GetString()!,
                    values,
                    vec.GetProperty("data_format").GetString()!).ConfigureAwait(false);
                break;
            case "change_mode":
                var mode = vec.GetProperty("mode").GetString()! == "RUN"
                    ? KvPlcMode.Run : KvPlcMode.Program;
                await client.ChangeModeAsync(mode).ConfigureAwait(false);
                break;
            case "clear_error":
                await client.ClearErrorAsync().ConfigureAwait(false);
                break;
            case "check_error_no":
                await client.CheckErrorNoAsync().ConfigureAwait(false);
                break;
            case "query_model":
                await client.QueryModelAsync().ConfigureAwait(false);
                break;
            case "confirm_operating_mode":
                await client.ConfirmOperatingModeAsync().ConfigureAwait(false);
                break;
            case "set_time":
                var dt = DateTime.Parse(
                    vec.GetProperty("dotnet_datetime").GetString()!,
                    CultureInfo.InvariantCulture);
                await client.SetTimeAsync(dt).ConfigureAwait(false);
                break;
            case "forced_set":
                await client.ForcedSetAsync(vec.GetProperty("device").GetString()!).ConfigureAwait(false);
                break;
            case "forced_reset":
                await client.ForcedResetAsync(vec.GetProperty("device").GetString()!).ConfigureAwait(false);
                break;
            case "read_format":
                await client.ReadAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("data_format").GetString()!).ConfigureAwait(false);
                break;
            case "read_monitor_bits":
                await client.ReadMonitorBitsAsync().ConfigureAwait(false);
                break;
            case "read_monitor_words":
                await client.ReadMonitorWordsAsync().ConfigureAwait(false);
                break;
            case "forced_set_consecutive":
                await client.ForcedSetConsecutiveAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("count").GetInt32()).ConfigureAwait(false);
                break;
            case "forced_reset_consecutive":
                await client.ForcedResetConsecutiveAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("count").GetInt32()).ConfigureAwait(false);
                break;
            case "read_consecutive_legacy":
                await client.ReadConsecutiveLegacyAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("count").GetInt32(),
                    vec.GetProperty("data_format").GetString()!).ConfigureAwait(false);
                break;
            case "write_consecutive_legacy":
                var legacyValues = vec.GetProperty("values").EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToArray();
                await client.WriteConsecutiveLegacyAsync(
                    vec.GetProperty("device").GetString()!,
                    legacyValues,
                    vec.GetProperty("data_format").GetString()!).ConfigureAwait(false);
                break;
            case "register_monitor_bits":
                await client.RegisterMonitorBitsAsync(
                    vec.GetProperty("devices").EnumerateArray().Select(item => item.GetString()!)).ConfigureAwait(false);
                break;
            case "register_monitor_words":
                await client.RegisterMonitorWordsAsync(
                    vec.GetProperty("devices").EnumerateArray().Select(item => new KvMonitorWordTarget(
                        item.GetProperty("device").GetString()!,
                        item.GetProperty("data_format").GetString()!))).ConfigureAwait(false);
                break;
            case "write_set_value":
                await client.WriteSetValueAsync(
                    vec.GetProperty("device").GetString()!,
                    vec.GetProperty("value").GetInt32(),
                    vec.GetProperty("data_format").GetString()!).ConfigureAwait(false);
                break;
            case "write_set_value_consecutive":
                var setValues = vec.GetProperty("values").EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToArray();
                await client.WriteSetValueConsecutiveAsync(
                    vec.GetProperty("device").GetString()!,
                    setValues,
                    vec.GetProperty("data_format").GetString()!).ConfigureAwait(false);
                break;
            case "switch_bank":
                await client.SwitchBankAsync(vec.GetProperty("bank").GetInt32()).ConfigureAwait(false);
                break;
            case "read_expansion_unit_buffer":
                await client.ReadExpansionUnitBufferAsync(
                    vec.GetProperty("unit").GetInt32(),
                    vec.GetProperty("address").GetInt32(),
                    vec.GetProperty("count").GetInt32(),
                    vec.GetProperty("data_format").GetString()!).ConfigureAwait(false);
                break;
            case "write_expansion_unit_buffer":
                var expansionValues = vec.GetProperty("values").EnumerateArray()
                    .Select(x => x.GetInt32())
                    .ToArray();
                await client.WriteExpansionUnitBufferAsync(
                    vec.GetProperty("unit").GetInt32(),
                    vec.GetProperty("address").GetInt32(),
                    expansionValues,
                    vec.GetProperty("data_format").GetString()!).ConfigureAwait(false);
                break;
            case "read_comments":
                await client.ReadCommentsAsync(vec.GetProperty("device").GetString()!).ConfigureAwait(false);
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

        using var client = new KvHostLinkClient("127.0.0.1", port, HostLinkTransportMode.Tcp, TestPlcProfile);
        try
        {
            await client.OpenAsync();
            await RunCommandAsync(client, vec);
        }
        catch (Exception)
        {
            // Response parsing may fail because the fixture always replies OK;
            // the assertion below still requires the expected frame to have been sent.
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
