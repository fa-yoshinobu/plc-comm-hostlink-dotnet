using System.Globalization;
using System.Runtime.CompilerServices;

namespace PlcComm.KvHostLink;

/// <summary>
/// High-level helper API for <see cref="KvHostLinkClient"/> and <see cref="QueuedKvHostLinkClient"/>.
/// </summary>
/// <remarks>
/// These extension methods are the recommended user-facing surface for normal
/// application code. They wrap the token-oriented low-level client API with
/// typed reads and writes, bit-in-word helpers, named snapshots, polling, and
/// one-step connection setup. Overloads for <see cref="QueuedKvHostLinkClient"/>
/// keep compound helper operations exclusive when a shared connection is used.
/// </remarks>
public static class KvHostLinkClientExtensions
{
    private enum ReadPlanValueKind
    {
        Unsigned16,
        Signed16,
        Unsigned32,
        Signed32,
        Float32,
        BitInWord,
    }

    private static readonly HashSet<string> OptimizableReadNamedDeviceTypes =
        KvHostLinkModels.DefaultFormatByDeviceType
            .Where(static pair => pair.Value == ".U")
            .Select(static pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);

    private readonly record struct ReadPlanRequest(
        int Index,
        string Address,
        KvDeviceAddress BaseAddress,
        ReadPlanValueKind Kind,
        int BitIndex);

    private sealed class ReadPlanSegment
    {
        public required KvDeviceAddress StartAddress { get; init; }
        public required int StartNumber { get; init; }
        public required int Count { get; init; }
        public required ReadPlanRequest[] Requests { get; init; }
    }

    private sealed class CompiledReadNamedPlan
    {
        public required ReadPlanRequest[] RequestsInInputOrder { get; init; }
        public required ReadPlanSegment[] Segments { get; init; }
    }

    /// <summary>
    /// Reads a single device value and converts it to a high-level CLR type.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Base device address string, for example <c>"DM100"</c>.</param>
    /// <param name="dtype">
    /// High-level data type code: <c>"U"</c> = <see cref="ushort"/>,
    /// <c>"S"</c> = <see cref="short"/>, <c>"D"</c> = <see cref="uint"/>,
    /// <c>"L"</c> = signed 32-bit <see cref="int"/>, <c>"F"</c> = IEEE 754
    /// float32.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A boxed CLR value. Integer formats return boxed integral types and
    /// <c>"F"</c> returns a boxed <see cref="float"/>.
    /// </returns>
    /// <remarks>
    /// The float helper is implemented at the extension layer by reading two
    /// consecutive <c>.U</c> words and combining them as low-word, high-word.
    /// </remarks>
    public static async Task<object> ReadTypedAsync(
        this KvHostLinkClient client,
        string device,
        string dtype,
        CancellationToken ct = default)
        => await ReadTypedCoreAsync(client, device, dtype, ct).ConfigureAwait(false);

    private static async Task<object> ReadTypedCoreAsync(
        KvHostLinkClient client,
        string device,
        string dtype,
        CancellationToken ct)
    {
        if (dtype.TrimStart('.').Equals("F", StringComparison.OrdinalIgnoreCase))
        {
            ushort[] words = await client.ReadWordsAsync(device, 2, ct).ConfigureAwait(false);
            return BitConverter.Int32BitsToSingle(unchecked((int)(words[0] | (words[1] << 16))));
        }

        string fmt = dtype.StartsWith('.') ? dtype : "." + dtype;
        var tokens = await client.ReadAsync(device, fmt, ct).ConfigureAwait(false);
        var raw = tokens.FirstOrDefault() ?? "0";
        return dtype.TrimStart('.').ToUpperInvariant() switch
        {
            "S" => (object)short.Parse(raw, CultureInfo.InvariantCulture),
            "D" => (object)uint.Parse(raw, CultureInfo.InvariantCulture),
            "L" => (object)int.Parse(raw, CultureInfo.InvariantCulture),
            "F" => (object)float.Parse(raw, CultureInfo.InvariantCulture),
            _ => (object)ushort.Parse(raw, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// Reads a single device value and converts it to a high-level CLR type.
    /// </summary>
    public static Task<object> ReadTypedAsync(
        this QueuedKvHostLinkClient client,
        string device,
        string dtype,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => ReadTypedCoreAsync(inner, device, dtype, ct), ct);

    /// <summary>
    /// Writes a single device value using a high-level data type code.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Base device address string, for example <c>"DM100"</c>.</param>
    /// <param name="dtype">
    /// High-level data type code: <c>"U"</c>, <c>"S"</c>, <c>"D"</c>,
    /// <c>"L"</c>, or <c>"F"</c>.
    /// </param>
    /// <param name="value">Value to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// The float helper is implemented at the extension layer by converting
    /// the input value to IEEE 754 float32 and writing two consecutive
    /// <c>.U</c> words.
    /// </remarks>
    public static async Task WriteTypedAsync<T>(
        this KvHostLinkClient client,
        string device,
        string dtype,
        T value,
        CancellationToken ct = default) where T : IFormattable
    {
        if (dtype.TrimStart('.').Equals("F", StringComparison.OrdinalIgnoreCase))
        {
            float single = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            int bits = BitConverter.SingleToInt32Bits(single);
            ushort loWord = unchecked((ushort)(bits & 0xFFFF));
            ushort hiWord = unchecked((ushort)((bits >> 16) & 0xFFFF));
            await client.WriteConsecutiveAsync(device, [loWord, hiWord], "U", ct).ConfigureAwait(false);
            return;
        }

        string fmt = dtype.StartsWith('.') ? dtype : "." + dtype;
        await client.WriteAsync(device, value, fmt, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a single device value using a high-level data type code.
    /// </summary>
    public static Task WriteTypedAsync<T>(
        this QueuedKvHostLinkClient client,
        string device,
        string dtype,
        T value,
        CancellationToken ct = default) where T : IFormattable
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.WriteTypedAsync(inner, device, dtype, value, ct), ct);

    // -----------------------------------------------------------------------
    // Bit-in-word
    // -----------------------------------------------------------------------

    /// <summary>
    /// Performs a read-modify-write to set or clear a single bit inside a word
    /// device.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Base word device address string, for example <c>"DM100"</c>.</param>
    /// <param name="bitIndex">Bit position within the word (0–15).</param>
    /// <param name="value">New bit value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="bitIndex"/> is outside the range 0 to 15.
    /// </exception>
    /// <remarks>
    /// This helper operates on word-oriented devices such as <c>DM</c>. It is
    /// distinct from PLC force-set / force-reset commands for bit device
    /// families.
    /// </remarks>
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

    /// <summary>
    /// Performs a read-modify-write to set or clear a single bit inside a word device.
    /// </summary>
    public static Task WriteBitInWordAsync(
        this QueuedKvHostLinkClient client,
        string device,
        int bitIndex,
        bool value,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.WriteBitInWordAsync(inner, device, bitIndex, value, ct), ct);

    // -----------------------------------------------------------------------
    // Named-device read
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads multiple named values and returns a snapshot dictionary.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="addresses">
    /// Address strings that specify both the base device and the desired
    /// interpretation.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary keyed by the original input address strings.</returns>
    /// <remarks>
    /// Address format examples:
    /// <list type="bullet">
    ///   <item><description>"DM100" -- unsigned 16-bit (ushort)</description></item>
    ///   <item><description>"DM100:F" -- float</description></item>
    ///   <item><description>"DM100:S" -- signed 16-bit (short)</description></item>
    ///   <item><description>"DM100:D" -- unsigned 32-bit</description></item>
    ///   <item><description>"DM100:L" -- signed 32-bit</description></item>
    ///   <item><description>"DM100.3" -- bit 3 within word (bool)</description></item>
    ///   <item><description>"DM100.A" -- bit 10 within word (bool); bits 10-15 use hex digits A-F</description></item>
    /// </list>
    /// <para>
    /// Bit-in-word indices use hexadecimal notation (0-F), matching the KEYENCE address format.
    /// Bits 0-9 can be written as decimal digits; bits 10-15 must be written as A-F.
    /// For example, bit 12 is addressed as <c>"DM100.C"</c>, not <c>"DM100.12"</c>.
    /// </para>
    /// <para>
    /// When all requested addresses are compatible with helper-layer batching,
    /// this method merges contiguous reads into one or more <c>RDS</c>
    /// operations. Mixed or non-optimizable address sets fall back to
    /// sequential helper reads with the same return shape.
    /// </para>
    /// </remarks>
    public static async Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this KvHostLinkClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var addrList = addresses as IList<string> ?? addresses.ToList();
        if (addrList.Count == 0)
            return new Dictionary<string, object>();

        if (TryCompileReadNamedPlan(addrList, out var plan))
            return await ExecuteReadNamedPlanAsync(client, plan, ct).ConfigureAwait(false);

        return await ReadNamedSequentialAsync(client, addrList, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads multiple named values and returns a snapshot dictionary.
    /// </summary>
    public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this QueuedKvHostLinkClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
    {
        var addrList = addresses as IList<string> ?? addresses.ToList();
        return client.ExecuteAsync(inner => KvHostLinkClientExtensions.ReadNamedAsync(inner, addrList, ct), ct);
    }

    // -----------------------------------------------------------------------
    // Polling
    // -----------------------------------------------------------------------

    /// <summary>
    /// Continuously polls the specified addresses and yields a snapshot each
    /// cycle.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="addresses">Address strings in the same format as <see cref="ReadNamedAsync(KvHostLinkClient, IEnumerable{string}, CancellationToken)"/>.</param>
    /// <param name="interval">Time between polls.</param>
    /// <param name="ct">Cancellation token to stop polling.</param>
    /// <remarks>
    /// If the address set is batchable, the compiled read plan is reused on
    /// every iteration for lower per-cycle overhead.
    /// </remarks>
    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this KvHostLinkClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var addrList = addresses as IList<string> ?? addresses.ToList();
        bool hasPlan = TryCompileReadNamedPlan(addrList, out var plan);
        while (!ct.IsCancellationRequested)
        {
            yield return hasPlan
                ? await ExecuteReadNamedPlanAsync(client, plan, ct).ConfigureAwait(false)
                : await ReadNamedSequentialAsync(client, addrList, ct).ConfigureAwait(false);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Continuously polls the specified addresses and yields a snapshot each cycle.
    /// </summary>
    public static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this QueuedKvHostLinkClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var addrList = addresses as IList<string> ?? addresses.ToList();
        bool hasPlan = TryCompileReadNamedPlan(addrList, out var plan);
        while (!ct.IsCancellationRequested)
        {
            var snapshot = await client.ExecuteAsync(
                inner => hasPlan
                    ? ExecuteReadNamedPlanAsync(inner, plan, ct)
                    : ReadNamedSequentialAsync(inner, addrList, ct),
                ct).ConfigureAwait(false);
            yield return snapshot;
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Connection helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads contiguous unsigned 16-bit words using one protocol request or returns an error.
    /// </summary>
    public static async Task<ushort[]> ReadWordsSingleRequestAsync(
        this KvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");

        var tokens = await client.ReadConsecutiveAsync(device, count, "U", ct).ConfigureAwait(false);
        var result = new ushort[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
            result[i] = ushort.Parse(tokens[i], CultureInfo.InvariantCulture);
        return result;
    }

    /// <summary>
    /// Reads contiguous unsigned 16-bit words using one protocol request or returns an error.
    /// </summary>
    public static Task<ushort[]> ReadWordsSingleRequestAsync(
        this QueuedKvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.ReadWordsSingleRequestAsync(inner, device, count, ct), ct);

    /// <summary>
    /// Reads contiguous unsigned 32-bit values using one protocol request or returns an error.
    /// </summary>
    public static async Task<uint[]> ReadDWordsSingleRequestAsync(
        this KvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");

        var tokens = await client.ReadConsecutiveAsync(device, count, "D", ct).ConfigureAwait(false);
        var result = new uint[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
            result[i] = uint.Parse(tokens[i], CultureInfo.InvariantCulture);
        return result;
    }

    /// <summary>
    /// Reads contiguous unsigned 32-bit values using one protocol request or returns an error.
    /// </summary>
    public static Task<uint[]> ReadDWordsSingleRequestAsync(
        this QueuedKvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.ReadDWordsSingleRequestAsync(inner, device, count, ct), ct);

    /// <summary>
    /// Writes contiguous unsigned 16-bit values using one protocol request or returns an error.
    /// </summary>
    public static Task WriteWordsSingleRequestAsync(
        this KvHostLinkClient client,
        string device,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new HostLinkProtocolError("values must not be empty");
        return client.WriteConsecutiveAsync(device, values, "U", ct);
    }

    /// <summary>
    /// Writes contiguous unsigned 16-bit values using one protocol request or returns an error.
    /// </summary>
    public static Task WriteWordsSingleRequestAsync(
        this QueuedKvHostLinkClient client,
        string device,
        IReadOnlyList<ushort> values,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.WriteWordsSingleRequestAsync(inner, device, values, ct), ct);

    /// <summary>
    /// Writes contiguous unsigned 32-bit values using one protocol request or returns an error.
    /// </summary>
    public static Task WriteDWordsSingleRequestAsync(
        this KvHostLinkClient client,
        string device,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new HostLinkProtocolError("values must not be empty");
        return client.WriteConsecutiveAsync(device, values, "D", ct);
    }

    /// <summary>
    /// Writes contiguous unsigned 32-bit values using one protocol request or returns an error.
    /// </summary>
    public static Task WriteDWordsSingleRequestAsync(
        this QueuedKvHostLinkClient client,
        string device,
        IReadOnlyList<uint> values,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.WriteDWordsSingleRequestAsync(inner, device, values, ct), ct);

    /// <summary>
    /// Reads contiguous unsigned 16-bit words using explicit chunking.
    /// </summary>
    public static async Task<ushort[]> ReadWordsChunkedAsync(
        this KvHostLinkClient client,
        string device,
        int count,
        int maxWordsPerRequest,
        CancellationToken ct = default)
    {
        ValidateChunkArguments(count, maxWordsPerRequest, nameof(count), nameof(maxWordsPerRequest));
        var start = KvHostLinkAddress.Parse(device) with { Suffix = string.Empty };
        var result = new ushort[count];

        int offset = 0;
        while (offset < count)
        {
            int chunkCount = Math.Min(maxWordsPerRequest, count - offset);
            string chunkStart = OffsetDevice(start, offset);
            var chunk = await client.ReadWordsSingleRequestAsync(chunkStart, chunkCount, ct).ConfigureAwait(false);
            Array.Copy(chunk, 0, result, offset, chunkCount);
            offset += chunkCount;
        }

        return result;
    }

    /// <summary>
    /// Reads contiguous unsigned 16-bit words using explicit chunking.
    /// </summary>
    public static Task<ushort[]> ReadWordsChunkedAsync(
        this QueuedKvHostLinkClient client,
        string device,
        int count,
        int maxWordsPerRequest,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.ReadWordsChunkedAsync(inner, device, count, maxWordsPerRequest, ct), ct);

    /// <summary>
    /// Reads contiguous unsigned 32-bit values using explicit chunking.
    /// </summary>
    public static async Task<uint[]> ReadDWordsChunkedAsync(
        this KvHostLinkClient client,
        string device,
        int count,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
    {
        ValidateChunkArguments(count, maxDwordsPerRequest, nameof(count), nameof(maxDwordsPerRequest));
        var start = KvHostLinkAddress.Parse(device) with { Suffix = string.Empty };
        var result = new uint[count];

        int offset = 0;
        while (offset < count)
        {
            int chunkCount = Math.Min(maxDwordsPerRequest, count - offset);
            string chunkStart = OffsetDevice(start, offset * 2);
            var chunk = await client.ReadDWordsSingleRequestAsync(chunkStart, chunkCount, ct).ConfigureAwait(false);
            Array.Copy(chunk, 0, result, offset, chunkCount);
            offset += chunkCount;
        }

        return result;
    }

    /// <summary>
    /// Reads contiguous unsigned 32-bit values using explicit chunking.
    /// </summary>
    public static Task<uint[]> ReadDWordsChunkedAsync(
        this QueuedKvHostLinkClient client,
        string device,
        int count,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.ReadDWordsChunkedAsync(inner, device, count, maxDwordsPerRequest, ct), ct);

    /// <summary>
    /// Writes contiguous unsigned 16-bit values using explicit chunking.
    /// </summary>
    public static async Task WriteWordsChunkedAsync(
        this KvHostLinkClient client,
        string device,
        IReadOnlyList<ushort> values,
        int maxWordsPerRequest,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new HostLinkProtocolError("values must not be empty");
        ValidateChunkSize(maxWordsPerRequest, nameof(maxWordsPerRequest));

        var start = KvHostLinkAddress.Parse(device) with { Suffix = string.Empty };
        int offset = 0;
        while (offset < values.Count)
        {
            int chunkCount = Math.Min(maxWordsPerRequest, values.Count - offset);
            string chunkStart = OffsetDevice(start, offset);
            await client.WriteWordsSingleRequestAsync(chunkStart, values.Skip(offset).Take(chunkCount).ToArray(), ct)
                .ConfigureAwait(false);
            offset += chunkCount;
        }
    }

    /// <summary>
    /// Writes contiguous unsigned 16-bit values using explicit chunking.
    /// </summary>
    public static Task WriteWordsChunkedAsync(
        this QueuedKvHostLinkClient client,
        string device,
        IReadOnlyList<ushort> values,
        int maxWordsPerRequest,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.WriteWordsChunkedAsync(inner, device, values, maxWordsPerRequest, ct), ct);

    /// <summary>
    /// Writes contiguous unsigned 32-bit values using explicit chunking.
    /// </summary>
    public static async Task WriteDWordsChunkedAsync(
        this KvHostLinkClient client,
        string device,
        IReadOnlyList<uint> values,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.Count == 0)
            throw new HostLinkProtocolError("values must not be empty");
        ValidateChunkSize(maxDwordsPerRequest, nameof(maxDwordsPerRequest));

        var start = KvHostLinkAddress.Parse(device) with { Suffix = string.Empty };
        int offset = 0;
        while (offset < values.Count)
        {
            int chunkCount = Math.Min(maxDwordsPerRequest, values.Count - offset);
            string chunkStart = OffsetDevice(start, offset * 2);
            await client.WriteDWordsSingleRequestAsync(chunkStart, values.Skip(offset).Take(chunkCount).ToArray(), ct)
                .ConfigureAwait(false);
            offset += chunkCount;
        }
    }

    /// <summary>
    /// Writes contiguous unsigned 32-bit values using explicit chunking.
    /// </summary>
    public static Task WriteDWordsChunkedAsync(
        this QueuedKvHostLinkClient client,
        string device,
        IReadOnlyList<uint> values,
        int maxDwordsPerRequest,
        CancellationToken ct = default)
        => client.ExecuteAsync(inner => KvHostLinkClientExtensions.WriteDWordsChunkedAsync(inner, device, values, maxDwordsPerRequest, ct), ct);

    /// <summary>
    /// Reads contiguous unsigned 16-bit words starting at <paramref name="device"/>.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Starting device address (e.g. <c>"DM0"</c>).</param>
    /// <param name="count">Number of words to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unsigned word values in PLC order.</returns>
    /// <remarks>
    /// This helper is the preferred user-facing block-read API for contiguous
    /// word devices. It preserves single-request semantics by delegating to
    /// <see cref="ReadWordsSingleRequestAsync(KvHostLinkClient, string, int, CancellationToken)"/>.
    /// </remarks>
    public static Task<ushort[]> ReadWordsAsync(
        this KvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
        => client.ReadWordsSingleRequestAsync(device, count, ct);

    /// <summary>
    /// Reads contiguous unsigned 16-bit words starting at <paramref name="device"/>.
    /// </summary>
    public static Task<ushort[]> ReadWordsAsync(
        this QueuedKvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
        => KvHostLinkClientExtensions.ReadWordsSingleRequestAsync(client, device, count, ct);

    /// <summary>
    /// Reads contiguous unsigned 32-bit values starting at <paramref name="device"/>.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Starting device address (for example <c>"DM0"</c>).</param>
    /// <param name="count">Number of 32-bit values to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Unsigned 32-bit values in logical device order.</returns>
    /// <remarks>
    /// This helper preserves single-request semantics by delegating to
    /// <see cref="ReadDWordsSingleRequestAsync(KvHostLinkClient, string, int, CancellationToken)"/>.
    /// </remarks>
    public static Task<uint[]> ReadDWordsAsync(
        this KvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
        => client.ReadDWordsSingleRequestAsync(device, count, ct);

    /// <summary>
    /// Reads contiguous unsigned 32-bit values starting at <paramref name="device"/>.
    /// </summary>
    public static Task<uint[]> ReadDWordsAsync(
        this QueuedKvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
        => KvHostLinkClientExtensions.ReadDWordsSingleRequestAsync(client, device, count, ct);

    /// <summary>
    /// Creates a queued client and opens the connection.
    /// </summary>
    /// <param name="host">PLC IP address or hostname.</param>
    /// <param name="port">KV Host Link TCP/UDP port. Defaults to 8501.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A connected queued client that is safe to share across async callers.</returns>
    /// <remarks>
    /// This is the recommended convenience entry point for high-level
    /// application code that does not need to construct
    /// <see cref="KvHostLinkConnectionOptions"/> manually.
    /// </remarks>
    public static Task<QueuedKvHostLinkClient> OpenAndConnectAsync(
        string host,
        int port = 8501,
        CancellationToken ct = default)
        => KvHostLinkClientFactory.OpenAndConnectAsync(new KvHostLinkConnectionOptions(host, port), ct);

    private static async Task<IReadOnlyDictionary<string, object>> ReadNamedSequentialAsync(
        KvHostLinkClient client,
        IEnumerable<string> addresses,
        CancellationToken ct)
    {
        var result = new Dictionary<string, object>();
        foreach (var address in addresses)
        {
            var (baseAddr, dtype, bitIdx) = ParseAddress(address);
            if (dtype == "BIT_IN_WORD")
            {
                var tokens = await client.ReadAsync(baseAddr, ".U", ct).ConfigureAwait(false);
                int w = ushort.Parse(tokens.Length > 0 ? tokens[0] : "0", CultureInfo.InvariantCulture);
                result[address] = ((w >> (bitIdx ?? 0)) & 1) != 0;
            }
            else
            {
                result[address] = await client.ReadTypedAsync(baseAddr, dtype, ct).ConfigureAwait(false);
            }
        }
        return result;
    }

    private static bool TryCompileReadNamedPlan(
        IEnumerable<string> addresses,
        out CompiledReadNamedPlan plan)
    {
        var requestsInInputOrder = new List<ReadPlanRequest>();
        var requestsByDeviceType = new Dictionary<string, List<ReadPlanRequest>>(StringComparer.Ordinal);

        int index = 0;
        foreach (var address in addresses)
        {
            if (!TryParseOptimizableReadNamedRequest(address, index, out var request))
            {
                plan = null!;
                return false;
            }

            requestsInInputOrder.Add(request);
            if (!requestsByDeviceType.TryGetValue(request.BaseAddress.DeviceType, out var bucket))
            {
                bucket = [];
                requestsByDeviceType.Add(request.BaseAddress.DeviceType, bucket);
            }
            bucket.Add(request);
            index++;
        }

        var segments = new List<ReadPlanSegment>();
        foreach (var bucket in requestsByDeviceType.Values)
        {
            var sorted = bucket
                .OrderBy(static req => req.BaseAddress.Number)
                .ThenByDescending(static req => GetWordWidth(req.Kind))
                .ToArray();

            var pending = new List<ReadPlanRequest>();
            KvDeviceAddress? currentStart = null;
            int currentStartNumber = 0;
            int currentEndExclusive = 0;

            foreach (var request in sorted)
            {
                int requestStart = request.BaseAddress.Number;
                int requestEndExclusive = requestStart + GetWordWidth(request.Kind);

                if (currentStart is null || requestStart > currentEndExclusive)
                {
                    if (currentStart is not null)
                    {
                        segments.Add(new ReadPlanSegment
                        {
                            StartAddress = currentStart,
                            StartNumber = currentStartNumber,
                            Count = currentEndExclusive - currentStartNumber,
                            Requests = [.. pending],
                        });
                        pending.Clear();
                    }

                    currentStart = request.BaseAddress with { Suffix = "" };
                    currentStartNumber = requestStart;
                    currentEndExclusive = requestEndExclusive;
                }
                else if (requestEndExclusive > currentEndExclusive)
                {
                    currentEndExclusive = requestEndExclusive;
                }

                pending.Add(request);
            }

            if (currentStart is not null)
            {
                segments.Add(new ReadPlanSegment
                {
                    StartAddress = currentStart,
                    StartNumber = currentStartNumber,
                    Count = currentEndExclusive - currentStartNumber,
                    Requests = [.. pending],
                });
            }
        }

        plan = new CompiledReadNamedPlan
        {
            RequestsInInputOrder = [.. requestsInInputOrder],
            Segments = [.. segments],
        };
        return true;
    }

    private static async Task<IReadOnlyDictionary<string, object>> ExecuteReadNamedPlanAsync(
        KvHostLinkClient client,
        CompiledReadNamedPlan plan,
        CancellationToken ct)
    {
        var resolved = new object[plan.RequestsInInputOrder.Length];

        foreach (var segment in plan.Segments)
        {
            ushort[] words = await client.ReadWordsAsync(
                segment.StartAddress.ToText(),
                segment.Count,
                ct).ConfigureAwait(false);

            foreach (var request in segment.Requests)
            {
                int offset = request.BaseAddress.Number - segment.StartNumber;
                resolved[request.Index] = ResolvePlannedValue(words, offset, request.Kind, request.BitIndex);
            }
        }

        var result = new Dictionary<string, object>(plan.RequestsInInputOrder.Length);
        foreach (var request in plan.RequestsInInputOrder)
            result[request.Address] = resolved[request.Index];
        return result;
    }

    private static object ResolvePlannedValue(
        ushort[] words,
        int offset,
        ReadPlanValueKind kind,
        int bitIndex)
    {
        return kind switch
        {
            ReadPlanValueKind.Unsigned16 => words[offset],
            ReadPlanValueKind.Signed16 => unchecked((short)words[offset]),
            ReadPlanValueKind.Unsigned32 => (uint)(words[offset] | (words[offset + 1] << 16)),
            ReadPlanValueKind.Signed32 => unchecked((int)(words[offset] | (words[offset + 1] << 16))),
            ReadPlanValueKind.Float32 => BitConverter.Int32BitsToSingle(
                unchecked((int)(words[offset] | (words[offset + 1] << 16)))),
            ReadPlanValueKind.BitInWord => ((words[offset] >> bitIndex) & 1) != 0,
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static bool TryParseOptimizableReadNamedRequest(
        string address,
        int index,
        out ReadPlanRequest request)
    {
        request = default;
        try
        {
            var (baseAddr, dtype, bitIdx) = ParseAddress(address);
            var parsed = KvHostLinkDevice.ParseDevice(baseAddr);
            if (!OptimizableReadNamedDeviceTypes.Contains(parsed.DeviceType))
                return false;

            if (dtype == "BIT_IN_WORD")
            {
                request = new ReadPlanRequest(index, address, parsed with { Suffix = "" }, ReadPlanValueKind.BitInWord, bitIdx ?? 0);
                return true;
            }

            if (!TryMapReadPlanValueKind(dtype, out var kind))
                return false;

            request = new ReadPlanRequest(index, address, parsed with { Suffix = "" }, kind, 0);
            return true;
        }
        catch (HostLinkProtocolError)
        {
            return false;
        }
    }

    private static bool TryMapReadPlanValueKind(string dtype, out ReadPlanValueKind kind)
    {
        switch (dtype.TrimStart('.').ToUpperInvariant())
        {
            case "U":
                kind = ReadPlanValueKind.Unsigned16;
                return true;
            case "S":
                kind = ReadPlanValueKind.Signed16;
                return true;
            case "D":
                kind = ReadPlanValueKind.Unsigned32;
                return true;
            case "L":
                kind = ReadPlanValueKind.Signed32;
                return true;
            case "F":
                kind = ReadPlanValueKind.Float32;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static int GetWordWidth(ReadPlanValueKind kind)
        => kind is ReadPlanValueKind.Unsigned32 or ReadPlanValueKind.Signed32 or ReadPlanValueKind.Float32 ? 2 : 1;

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private static void ValidateChunkArguments(int count, int maxPerRequest, string countName, string chunkName)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(countName, "count must be 1 or greater.");
        ValidateChunkSize(maxPerRequest, chunkName);
    }

    private static void ValidateChunkSize(int maxPerRequest, string paramName)
    {
        if (maxPerRequest < 1)
            throw new ArgumentOutOfRangeException(paramName, "Chunk size must be 1 or greater.");
    }

    private static string OffsetDevice(KvDeviceAddress start, int wordOffset)
        => KvHostLinkAddress.Format(start with { Number = checked(start.Number + wordOffset), Suffix = string.Empty });

    // "DM100:F" -> ("DM100", "F", null),  "DM100.3" -> ("DM100", "BIT_IN_WORD", 3),  "DM100.A" -> ("DM100", "BIT_IN_WORD", 10)
    // Bit indices are parsed as hexadecimal (0-F). Bits 10-15 must be specified as A-F.
    private static (string Base, string DType, int? BitIdx) ParseAddress(string address)
    {
        var logical = KvHostLinkAddress.ParseLogical(address);
        return (
            KvHostLinkAddress.Format(logical.BaseAddress with { Suffix = string.Empty }),
            logical.DataType,
            logical.BitIndex);
    }
}
