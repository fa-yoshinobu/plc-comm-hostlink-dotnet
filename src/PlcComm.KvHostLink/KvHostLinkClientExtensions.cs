using System.Globalization;
using System.Runtime.CompilerServices;

namespace PlcComm.KvHostLink;

/// <summary>
/// Extension methods for <see cref="KvHostLinkClient"/> providing typed read/write helpers,
/// bit-in-word access, named-device reads, and polling.
/// </summary>
public static class KvHostLinkClientExtensions
{
    /// <summary>
    /// Reads a single device value and converts it to the specified type.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Device address string, e.g. "DM100".</param>
    /// <param name="dtype">
    /// Data type: "U" = ushort, "S" = short, "D" = uint, "L" = int (signed 32-bit), "F" = float.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<object> ReadTypedAsync(
        this KvHostLinkClient client,
        string device,
        string dtype,
        CancellationToken ct = default)
    {
        string fmt = dtype.StartsWith('.') ? dtype : "." + dtype;
        var tokens = await client.ReadAsync(device, fmt, ct).ConfigureAwait(false);
        var raw = tokens.FirstOrDefault() ?? "0";
        return dtype.TrimStart('.').ToUpperInvariant() switch
        {
            "S" => short.Parse(raw, CultureInfo.InvariantCulture),
            "D" => uint.Parse(raw, CultureInfo.InvariantCulture),
            "L" => int.Parse(raw, CultureInfo.InvariantCulture),
            "F" => float.Parse(raw, CultureInfo.InvariantCulture),
            _ => ushort.Parse(raw, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// Writes a single device value using the specified data type format.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Device address string, e.g. "DM100".</param>
    /// <param name="dtype">
    /// Data type: "U" = ushort, "S" = short, "D" = uint, "L" = int, "F" = float.
    /// </param>
    /// <param name="value">Value to write.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteTypedAsync(
        this KvHostLinkClient client,
        string device,
        string dtype,
        object value,
        CancellationToken ct = default)
    {
        string fmt = dtype.StartsWith('.') ? dtype : "." + dtype;
        await client.WriteAsync(device, value, fmt, ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Bit-in-word
    // -----------------------------------------------------------------------

    /// <summary>
    /// Performs a read-modify-write to set a single bit within a word device.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Word device address string, e.g. "DM100".</param>
    /// <param name="bitIndex">Bit position within the word (0–15).</param>
    /// <param name="value">New bit value.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task WriteBitInWordAsync(
        this KvHostLinkClient client,
        string device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
    {
        if (bitIndex is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bitIndex), "bitIndex must be 0-15.");
        var tokens = await client.ReadAsync(device, ".U", ct).ConfigureAwait(false);
        int cur = ushort.Parse(tokens.FirstOrDefault() ?? "0", CultureInfo.InvariantCulture);
        if (value) cur |= 1 << bitIndex;
        else cur &= ~(1 << bitIndex);
        await client.WriteAsync(device, (ushort)(cur & 0xFFFF), ".U", ct).ConfigureAwait(false);
    }

    // -----------------------------------------------------------------------
    // Named-device read
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads multiple devices by address string and returns results in a dictionary.
    /// </summary>
    /// <remarks>
    /// Address format examples:
    /// <list type="bullet">
    ///   <item><description>"DM100" — unsigned 16-bit (ushort)</description></item>
    ///   <item><description>"DM100:F" — float</description></item>
    ///   <item><description>"DM100:S" — signed 16-bit (short)</description></item>
    ///   <item><description>"DM100:D" — unsigned 32-bit</description></item>
    ///   <item><description>"DM100:L" — signed 32-bit</description></item>
    ///   <item><description>"DM100.3" — bit 3 within word (bool)</description></item>
    /// </list>
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this KvHostLinkClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var result = new Dictionary<string, object>();
        foreach (var address in addresses)
        {
            var (baseAddr, dtype, bitIdx) = ParseAddress(address);
            if (dtype == "BIT_IN_WORD")
            {
                var tokens = await client.ReadAsync(baseAddr, ".U", ct).ConfigureAwait(false);
                int w = ushort.Parse(tokens.FirstOrDefault() ?? "0", CultureInfo.InvariantCulture);
                result[address] = ((w >> (bitIdx ?? 0)) & 1) != 0;
            }
            else
            {
                result[address] = await client.ReadTypedAsync(baseAddr, dtype, ct).ConfigureAwait(false);
            }
        }
        return result;
    }

    // -----------------------------------------------------------------------
    // Polling
    // -----------------------------------------------------------------------

    /// <summary>
    /// Continuously polls the specified devices at the given interval, yielding a snapshot each cycle.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="addresses">Device addresses to poll (same format as <see cref="ReadNamedAsync"/>).</param>
    /// <param name="interval">Time between polls.</param>
    /// <param name="ct">Cancellation token to stop polling.</param>
    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this KvHostLinkClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var addrList = addresses.ToList();
        while (!ct.IsCancellationRequested)
        {
            yield return await client.ReadNamedAsync(addrList, ct).ConfigureAwait(false);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Connection helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads <paramref name="count"/> contiguous word values starting at <paramref name="device"/>.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Starting device address (e.g. <c>"DM0"</c>).</param>
    /// <param name="count">Number of words to read.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<ushort[]> ReadWordsAsync(
        this KvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
    {
        var tokens = await client.ReadConsecutiveAsync(device, count, "U", ct).ConfigureAwait(false);
        var result = new ushort[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
            result[i] = ushort.Parse(tokens[i], CultureInfo.InvariantCulture);
        return result;
    }

    /// <summary>
    /// Reads <paramref name="count"/> contiguous DWord (32-bit unsigned) values starting at <paramref name="device"/>.
    /// Combines adjacent word pairs (lo, hi).
    /// </summary>
    public static async Task<uint[]> ReadDWordsAsync(
        this KvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
    {
        var words = await client.ReadWordsAsync(device, count * 2, ct).ConfigureAwait(false);
        var result = new uint[count];
        for (int i = 0; i < count; i++)
            result[i] = (uint)(words[i * 2] | (words[i * 2 + 1] << 16));
        return result;
    }

    /// <summary>
    /// Creates a <see cref="KvHostLinkClient"/> and opens the connection.
    /// </summary>
    /// <param name="host">PLC IP address or hostname.</param>
    /// <param name="port">KV HostLink TCP port. Defaults to 8501.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<KvHostLinkClient> OpenAndConnectAsync(
        string host,
        int port = 8501,
        CancellationToken ct = default)
    {
        var client = new KvHostLinkClient(host, port);
        await client.OpenAsync(ct).ConfigureAwait(false);
        return client;
    }

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    // "DM100:F" → ("DM100", "F", null),  "DM100.3" → ("DM100", "BIT_IN_WORD", 3)
    private static (string Base, string DType, int? BitIdx) ParseAddress(string address)
    {
        if (address.Contains(':'))
        {
            int i = address.IndexOf(':');
            return (address[..i], address[(i + 1)..].ToUpperInvariant(), null);
        }
        if (address.Contains('.'))
        {
            int i = address.IndexOf('.');
            if (int.TryParse(address[(i + 1)..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var bit))
                return (address[..i], "BIT_IN_WORD", bit);
        }
        return (address, "U", null);
    }
}
