using System.Globalization;
using System.Runtime.CompilerServices;

namespace PlcComm.KvHostLink;

/// <summary>
/// Composite timer/counter value returned by Host Link T/C reads.
/// </summary>
public readonly record struct KvTimerCounterValue(uint Status, uint Current, uint Preset);

/// <summary>
/// High-level helper API for <see cref="KvHostLinkClient"/>.
/// </summary>
/// <remarks>
/// These extension methods are the recommended user-facing surface for normal
/// application code. They wrap the token-oriented low-level client API with
/// typed reads and writes, named aggregate reads, polling, and one-step connection setup.
/// The ordinary client keeps every compound read plan exclusive through its built-in FIFO queue.
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
        Hex16,
        BitInWord,
        DirectBit,
        Individual,
    }

    private enum ReadPlanSegmentMode
    {
        Words,
        DirectBits,
    }

    private static readonly HashSet<string> OptimizableReadNamedDeviceTypes =
        KvHostLinkModels.Float32DeviceTypes.ToHashSet(StringComparer.Ordinal);

    private static readonly HashSet<string> DirectBitDeviceTypes =
        KvHostLinkModels.DefaultFormatByDeviceType
            .Where(static pair => pair.Value == "")
            .Select(static pair => pair.Key)
            .ToHashSet(StringComparer.Ordinal);

    private readonly record struct ReadPlanRequest(
        int Index,
        string Address,
        KvDeviceAddress BaseAddress,
        ReadPlanValueKind Kind,
        int BitIndex,
        string DataType,
        bool Batchable);

    private sealed class ReadPlanSegment
    {
        public required KvDeviceAddress StartAddress { get; init; }
        public required int StartNumber { get; init; }
        public required int Count { get; init; }
        public required ReadPlanSegmentMode Mode { get; init; }
        public required ReadPlanRequest[] Requests { get; init; }
        public bool IsIndividual { get; init; }
    }

    private sealed class CompiledReadNamedPlan
    {
        public required ReadPlanRequest[] RequestsInInputOrder { get; init; }
        public required ReadPlanSegment[] Segments { get; init; }
    }

    private sealed class ReadPlanGroup
    {
        public required ReadPlanSegmentMode Mode { get; init; }
        public required bool IsIndividual { get; init; }
        public required List<ReadPlanRequest> Requests { get; init; }
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
    /// float32, <c>"H"</c> = hexadecimal 16-bit word text.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A boxed CLR value. Integer formats return boxed integral types and
    /// <c>"F"</c> returns a boxed <see cref="float"/>, <c>"H"</c>
    /// returns a <see cref="string"/>, and <c>"BIT"</c> returns a <see cref="bool"/>.
    /// </returns>
    /// <remarks>
    /// The float helper is implemented at the extension layer by reading two
    /// consecutive <c>.U</c> words and combining them as low-word, high-word.
    /// Float32 is valid only for ordinary device families whose canonical
    /// default format is one <c>.U</c> word.
    /// </remarks>
    public static async Task<object> ReadTypedAsync(
        this KvHostLinkClient client,
        string device,
        string dtype,
        CancellationToken ct = default)
        => await ReadTypedCoreAsync(client, device, dtype, operationAlreadyAdmitted: false, ct)
            .ConfigureAwait(false);

    private static async Task<object> ReadTypedCoreAsync(
        KvHostLinkClient client,
        string device,
        string dtype,
        bool operationAlreadyAdmitted,
        CancellationToken ct)
    {
        string normalized = dtype.Trim().TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
            throw new HostLinkProtocolError("dtype is required.");

        var parsedDevice = KvHostLinkDevice.RequireBaseDevice(device);
        if (normalized == "F")
        {
            KvHostLinkDevice.ValidateFloat32DeviceType(parsedDevice.DeviceType, device);
            string[] wordTokens = operationAlreadyAdmitted
                ? await client.ReadCoreAsync(device, ".U", 2, true, ct).ConfigureAwait(false)
                : await client.ReadConsecutiveAsync(device, 2, ".U", ct).ConfigureAwait(false);
            ushort[] words = wordTokens
                .Select(static token => ushort.Parse(token, CultureInfo.InvariantCulture))
                .ToArray();
            return BitConverter.Int32BitsToSingle(unchecked((int)(words[0] | (words[1] << 16))));
        }

        if (normalized == "BIT")
        {
            var address = KvHostLinkDevice.RequireBaseDevice(device);
            if (!KvHostLinkModels.DirectBitDeviceTypes.Contains(address.DeviceType))
                throw new HostLinkProtocolError("BIT reads require a direct bit device.");
            var bitTokens = operationAlreadyAdmitted
                ? await client.ReadCoreAsync(device, null, 1, false, ct).ConfigureAwait(false)
                : await client.ReadAsync(device, ct).ConfigureAwait(false);
            var bitRaw = RequireDataToken(bitTokens, device);
            return ParseBoolToken(bitRaw);
        }

        string fmt = "." + normalized;
        var tokens = operationAlreadyAdmitted
            ? await client.ReadCoreAsync(device, fmt, 1, false, ct).ConfigureAwait(false)
            : await client.ReadAsync(device, fmt, ct).ConfigureAwait(false);
        bool timerCounterComposite = (parsedDevice.DeviceType is "T" or "C") && (normalized is "U" or "S" or "D" or "L" or "H");
        var raw = timerCounterComposite
            ? RequireLastDataToken(tokens, device)
            : RequireDataToken(tokens, device);
        if (normalized == "H")
            return ushort.Parse(raw, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture)
                .ToString("X4", CultureInfo.InvariantCulture);
        return normalized switch
        {
            "S" => (object)short.Parse(raw, CultureInfo.InvariantCulture),
            "D" => (object)uint.Parse(raw, CultureInfo.InvariantCulture),
            "L" => (object)int.Parse(raw, CultureInfo.InvariantCulture),
            "F" => (object)float.Parse(raw, CultureInfo.InvariantCulture),
            _ => (object)ushort.Parse(raw, CultureInfo.InvariantCulture),
        };
    }

    /// <summary>
    /// Reads a timer/counter composite value as status, current, and preset. Status must be exactly zero or one.
    /// </summary>
    public static async Task<KvTimerCounterValue> ReadTimerCounterAsync(
        this KvHostLinkClient client,
        string device,
        CancellationToken ct = default)
    {
        var parsed = KvHostLinkDevice.ParseDevice(device);
        if (parsed.DeviceType is not ("T" or "C"))
            throw new HostLinkProtocolError("ReadTimerCounterAsync requires a T or C device.");

        string target = (parsed with { Suffix = "" }).ToText();
        string[] tokens = await client.ReadAsync(target, ".D", ct).ConfigureAwait(false);
        if (tokens.Length < 3)
            throw new HostLinkProtocolError(
                $"Timer/counter response for '{target}' did not contain status/current/preset.");

        return new KvTimerCounterValue(
            uint.Parse(tokens[0], CultureInfo.InvariantCulture),
            uint.Parse(tokens[1], CultureInfo.InvariantCulture),
            uint.Parse(tokens[2], CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Reads a timer composite value.
    /// </summary>
    public static Task<KvTimerCounterValue> ReadTimerAsync(
        this KvHostLinkClient client,
        string device,
        CancellationToken ct = default)
    {
        if (KvHostLinkDevice.ParseDevice(device).DeviceType != "T")
            throw new HostLinkProtocolError("ReadTimerAsync requires a T device.");
        return client.ReadTimerCounterAsync(device, ct);
    }

    /// <summary>
    /// Reads a counter composite value.
    /// </summary>
    public static Task<KvTimerCounterValue> ReadCounterAsync(
        this KvHostLinkClient client,
        string device,
        CancellationToken ct = default)
    {
        if (KvHostLinkDevice.ParseDevice(device).DeviceType != "C")
            throw new HostLinkProtocolError("ReadCounterAsync requires a C device.");
        return client.ReadTimerCounterAsync(device, ct);
    }

    /// <summary>
    /// Writes a single device value using a high-level data type code.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Base device address string, for example <c>"DM100"</c>.</param>
    /// <param name="dtype">
    /// High-level data type code: <c>"U"</c>, <c>"S"</c>, <c>"D"</c>,
    /// <c>"L"</c>, <c>"F"</c>, or <c>"H"</c>.
    /// </param>
    /// <param name="value">Value to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>
    /// The float helper is implemented at the extension layer by converting
    /// a finite input value within the IEEE 754 float32 range and writing two consecutive
    /// <c>.U</c> words. Float32 is valid only for ordinary device families whose
    /// canonical default format is one <c>.U</c> word. Direct bit device families cannot
    /// represent that two-word value; direct-bit and special-response families are rejected
    /// before FIFO admission and transport I/O.
    /// </remarks>
    public static async Task WriteTypedAsync<T>(
        this KvHostLinkClient client,
        string device,
        string dtype,
        T value,
        CancellationToken ct = default) where T : IFormattable
    {
        string normalized = dtype.Trim().TrimStart('.').ToUpperInvariant();
        if (string.IsNullOrEmpty(normalized))
            throw new HostLinkProtocolError("dtype is required.");

        if (normalized == "F")
        {
            var address = KvHostLinkDevice.RequireBaseDevice(device);
            KvHostLinkDevice.ValidateFloat32DeviceType(address.DeviceType, device);

            float single = Convert.ToSingle(value, CultureInfo.InvariantCulture);
            if (!float.IsFinite(single))
                throw new HostLinkProtocolError("Float32 input must be finite and within the binary32 range.");
            int bits = BitConverter.SingleToInt32Bits(single);
            ushort loWord = unchecked((ushort)(bits & 0xFFFF));
            ushort hiWord = unchecked((ushort)((bits >> 16) & 0xFFFF));
            await client.WriteConsecutiveAsync(device, [loWord, hiWord], "U", ct).ConfigureAwait(false);
            return;
        }

        if (normalized == "BIT")
        {
            throw new HostLinkProtocolError(
                "BIT writes require the Boolean WriteTypedAsync overload; numeric 0/1 compatibility inputs are not accepted.");
        }

        string fmt = "." + normalized;
        await client.WriteAsync(device, value, fmt, ct).ConfigureAwait(false);
    }

    /// <summary>Writes a direct bit device in one request using an explicit BIT dtype and Boolean value.</summary>
    /// <param name="client">The client to use.</param>
    /// <param name="device">Direct-bit device address.</param>
    /// <param name="dtype">The exact logical type <c>BIT</c>.</param>
    /// <param name="value">Boolean bit value.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <remarks>Numeric, string, and truthy compatibility values are not accepted.</remarks>
    public static Task WriteTypedAsync(
        this KvHostLinkClient client,
        string device,
        string dtype,
        bool value,
        CancellationToken ct = default)
    {
        if (!string.Equals(dtype.Trim().TrimStart('.'), "BIT", StringComparison.OrdinalIgnoreCase))
            throw new HostLinkProtocolError("Boolean WriteTypedAsync requires dtype 'BIT'.");
        return client.WriteAsync(device, value, ct);
    }

    // -----------------------------------------------------------------------
    // Named-device read
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads multiple independent named values as one read-only aggregate operation.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="addresses">
    /// Address strings that specify both the base device and the desired
    /// interpretation.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary keyed by the original input address strings. No partial result is returned on failure.</returns>
    /// <remarks>
    /// Address format examples:
    /// <list type="bullet">
    ///   <item><description>"DM100:U" -- unsigned 16-bit (ushort)</description></item>
    ///   <item><description>"DM100:F" -- float</description></item>
    ///   <item><description>"DM100:S" -- signed 16-bit (short)</description></item>
    ///   <item><description>"DM100:D" -- unsigned 32-bit</description></item>
    ///   <item><description>"DM100:L" -- signed 32-bit</description></item>
    ///   <item><description>"DM100.3" -- bit 3 within word (bool)</description></item>
    ///   <item><description>"DM100.A" -- bit 10 within word (bool); bits 10-15 use hex digits A-F</description></item>
    ///   <item><description>"DM100:COMMENT" -- PLC device comment text (string)</description></item>
    /// </list>
    /// <para>
    /// Bit-in-word indices use hexadecimal notation (0-F), matching the KEYENCE address format.
    /// Bits 0-9 can be written as decimal digits; bits 10-15 must be written as A-F.
    /// For example, bit 12 is addressed as <c>"DM100.C"</c>, not <c>"DM100.12"</c>.
    /// </para>
    /// <para>
    /// A multi-request result is non-atomic: separate requests can observe different PLC scan times.
    /// Each declared scalar, float32 value, or bit-in-word value remains wholly inside one request,
    /// but callers requiring one coherent point in time must use a single-request read or a PLC-side
    /// snapshot/handshake. The complete plan is validated and copied before the first send, and the
    /// client turn is retained until every internal read succeeds or the aggregate fails. The planner
    /// groups wire-compatible device families in first-appearance order, sorts addresses within each
    /// group, and merges contiguous spans up to the request limit. A non-batchable entry uses its
    /// native single read without disabling batching for other groups.
    /// </para>
    /// <para>
    /// Named keys must be semantically unique after device, number, data type, bit index, and
    /// scalar count normalization. Spelling-only variants are rejected before transport, while
    /// distinct data-type views, bit indices, and overlapping multiword spans remain valid.
    /// Returned dictionary keys preserve the original input strings.
    /// </para>
    /// <para>
    /// This overload accepts no implicit comment codec. If an address uses
    /// <c>:COMMENT</c>, the complete aggregate is rejected before transport; use
    /// the overload that requires <see cref="HostLinkCommentEncoding"/>.
    /// </para>
    /// </remarks>
    public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this KvHostLinkClient client,
        IEnumerable<string> addresses,
        CancellationToken ct = default)
        => ReadNamedCoreAsync(client, addresses, commentEncoding: null, ct);

    /// <summary>
    /// Reads multiple independent named values with an explicit RDC comment encoding.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="addresses">Named addresses containing at least one <c>:COMMENT</c> entry.</param>
    /// <param name="commentEncoding">Explicit strict codec for every RDC comment in the aggregate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A dictionary keyed by original input address; no partial result is returned.</returns>
    /// <remarks>
    /// This overload has the same complete-plan, input-order, one-FIFO-turn,
    /// non-atomic, and no-partial-result contract as the non-comment overload.
    /// At least one address must use <c>:COMMENT</c>; an otherwise unused comment
    /// encoding is rejected during complete preflight before transport.
    /// </remarks>
    public static Task<IReadOnlyDictionary<string, object>> ReadNamedAsync(
        this KvHostLinkClient client,
        IEnumerable<string> addresses,
        HostLinkCommentEncoding commentEncoding,
        CancellationToken ct = default)
    {
        KvHostLinkProtocol.ValidateCommentEncoding(commentEncoding);
        return ReadNamedCoreAsync(client, addresses, commentEncoding, ct);
    }

    private static async Task<IReadOnlyDictionary<string, object>> ReadNamedCoreAsync(
        KvHostLinkClient client,
        IEnumerable<string> addresses,
        HostLinkCommentEncoding? commentEncoding,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        string[] addressSnapshot = addresses.ToArray();
        ValidateReadNamedAggregate(addressSnapshot, commentEncoding);
        if (addressSnapshot.Length == 0)
            return new Dictionary<string, object>();

        CompiledReadNamedPlan plan = CompileReadNamedPlan(addressSnapshot);
        object[] resolved = await client.ExecuteExclusiveAsync(
            () => ExecuteReadNamedPlanAsync(client, plan, commentEncoding, ct),
            ct).ConfigureAwait(false);
        return MaterializeReadNamedResult(plan, resolved);
    }

    // -----------------------------------------------------------------------
    // Polling
    // -----------------------------------------------------------------------

    /// <summary>
    /// Continuously polls the specified addresses and yields one non-atomic aggregate result each
    /// cycle.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="addresses">Address strings in the same format as <see cref="ReadNamedAsync(KvHostLinkClient, IEnumerable{string}, CancellationToken)"/>.</param>
    /// <param name="interval">Strictly positive time between polls.</param>
    /// <param name="ct">Cancellation token to stop polling.</param>
    /// <remarks>
    /// The validated compiled read plan is reused on every iteration for lower per-cycle overhead.
    /// Every cycle has the same input-order result, indivisible-value,
    /// no-interleaving, and no-partial-result contract as <see cref="ReadNamedAsync(KvHostLinkClient, IEnumerable{string}, CancellationToken)"/>.
    /// The interval is a completion delay outside the client FIFO turn; cycles never overlap or catch up.
    /// This overload accepts no implicit comment codec. A plan containing <c>:COMMENT</c>
    /// is rejected during complete preflight before its first send; use the overload that
    /// requires <see cref="HostLinkCommentEncoding"/>.
    /// </remarks>
    public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this KvHostLinkClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        CancellationToken ct = default)
        => PollCoreAsync(client, addresses, interval, commentEncoding: null, ct);

    /// <summary>
    /// Polls named values with an explicit strict codec for every RDC comment entry.
    /// </summary>
    /// <param name="client">The client to use.</param>
    /// <param name="addresses">Named addresses containing at least one <c>:COMMENT</c> entry.</param>
    /// <param name="interval">Strictly positive time between polls.</param>
    /// <param name="commentEncoding">Explicit strict codec for every RDC comment.</param>
    /// <param name="ct">Cancellation token to stop polling.</param>
    /// <returns>One non-atomic, all-or-error aggregate result per cycle.</returns>
    /// <remarks>
    /// The complete plan is validated and copied before its first send. Every cycle
    /// retains one FIFO turn and returns either the full non-atomic result or an error.
    /// At least one address must use <c>:COMMENT</c>; an otherwise unused comment
    /// encoding is rejected during complete preflight before transport.
    /// </remarks>
    public static IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollAsync(
        this KvHostLinkClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        HostLinkCommentEncoding commentEncoding,
        CancellationToken ct = default)
    {
        KvHostLinkProtocol.ValidateCommentEncoding(commentEncoding);
        return PollCoreAsync(client, addresses, interval, commentEncoding, ct);
    }

    private static async IAsyncEnumerable<IReadOnlyDictionary<string, object>> PollCoreAsync(
        KvHostLinkClient client,
        IEnumerable<string> addresses,
        TimeSpan interval,
        HostLinkCommentEncoding? commentEncoding,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        if (interval <= TimeSpan.Zero || interval > TimeSpan.FromMilliseconds(int.MaxValue))
            throw new ArgumentOutOfRangeException(nameof(interval));
        string[] addressSnapshot = addresses.ToArray();
        ValidateReadNamedAggregate(addressSnapshot, commentEncoding);
        CompiledReadNamedPlan plan = CompileReadNamedPlan(addressSnapshot);
        while (!ct.IsCancellationRequested)
        {
            object[] resolved = await client.ExecuteExclusiveAsync(
                () => ExecuteReadNamedPlanAsync(client, plan, commentEncoding, ct),
                ct).ConfigureAwait(false);
            yield return MaterializeReadNamedResult(plan, resolved);
            await Task.Delay(interval, ct).ConfigureAwait(false);
        }
    }

    // -----------------------------------------------------------------------
    // Connection helper
    // -----------------------------------------------------------------------

    /// <summary>
    /// Reads contiguous unsigned 16-bit words using one protocol request or returns an error.
    /// </summary>
    /// <param name="client">Connected Host Link client.</param>
    /// <param name="device">Start device address.</param>
    /// <param name="count">Number of words to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The contiguous word values read by one request.</returns>
    /// <remarks>Use this helper when the logical range must stay atomic.</remarks>
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
    /// Reads contiguous unsigned 32-bit values using one protocol request or returns an error.
    /// </summary>
    /// <param name="client">Connected Host Link client.</param>
    /// <param name="device">Start device address.</param>
    /// <param name="count">Number of 32-bit values to read.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The contiguous 32-bit values read by one request.</returns>
    /// <remarks>Use this helper when the logical range must stay atomic.</remarks>
    public static async Task<uint[]> ReadDWordsSingleRequestAsync(
        this KvHostLinkClient client,
        string device,
        int count,
        CancellationToken ct = default)
    {
        if (count < 1)
            throw new ArgumentOutOfRangeException(nameof(count), "count must be 1 or greater.");

        if (count > 500)
            throw new HostLinkProtocolError("Dword count must be in the range 1-500 for one request.");
        string[] tokens = await client.ReadConsecutiveAsync(device, count, ".D", ct).ConfigureAwait(false);
        var result = new uint[count];
        for (int i = 0; i < count; i++)
            result[i] = uint.Parse(tokens[i], CultureInfo.InvariantCulture);
        return result;
    }

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

        if (values.Count > 500)
            throw new HostLinkProtocolError("Dword count must be in the range 1-500 for one request.");
        return client.WriteConsecutiveAsync(device, values, ".D", ct);
    }

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
    /// Creates a Host Link client with built-in FIFO admission and opens the connection.
    /// </summary>
    /// <param name="host">PLC IPv4 address or hostname that resolves to IPv4.</param>
    /// <param name="port">Required KV Host Link TCP/UDP port.</param>
    /// <param name="transport">Required TCP or UDP transport.</param>
    /// <param name="plcProfile">Canonical KEYENCE KV PLC profile for the session.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A connected ordinary client that is safe to share across async callers.</returns>
    /// <remarks>
    /// This is the recommended convenience entry point for high-level
    /// application code that does not need to construct
    /// <see cref="KvHostLinkConnectionOptions"/> manually.
    /// </remarks>
    public static Task<KvHostLinkClient> OpenAndConnectAsync(
        string host,
        int port,
        HostLinkTransportMode transport,
        string plcProfile,
        CancellationToken ct = default)
        => KvHostLinkClientFactory.OpenAndConnectAsync(
            new KvHostLinkConnectionOptions(host, port, transport, plcProfile), ct);

    private static void ValidateReadNamedAggregate(
        IReadOnlyList<string> addresses,
        HostLinkCommentEncoding? commentEncoding)
    {
        var uniqueAddresses = new HashSet<(string DeviceType, int Number, string DataType, int? BitIndex, int Count)>();
        bool hasComment = false;
        foreach (string address in addresses)
        {
            if (address is null)
                throw new HostLinkProtocolError("Named read addresses must not contain null.");

            var (baseAddress, dtype, bitIndex) = ParseAddress(address);
            KvDeviceAddress parsed = KvHostLinkDevice.RequireBaseDevice(baseAddress);
            string normalized = dtype.Trim().TrimStart('.').ToUpperInvariant();
            string semanticDataType = normalized.Length == 0 && DirectBitDeviceTypes.Contains(parsed.DeviceType)
                ? "BIT"
                : normalized;
            if (!uniqueAddresses.Add((parsed.DeviceType, parsed.Number, semanticDataType, bitIndex, 1)))
                throw new HostLinkProtocolError($"Named read address '{address}' is semantically duplicated.");
            if (normalized == "COMMENT")
            {
                hasComment = true;
                if (!commentEncoding.HasValue)
                {
                    throw new HostLinkProtocolError(
                        "Named RDC comment reads require the ReadNamedAsync/PollAsync overload with an explicit HostLinkCommentEncoding.");
                }
                KvHostLinkDevice.ValidateDeviceType(
                    "RDC",
                    parsed.DeviceType,
                    KvHostLinkModels.RdcDeviceTypes);
                continue;
            }

            if (normalized == "BIT_IN_WORD")
            {
                if (!bitIndex.HasValue)
                    throw new HostLinkProtocolError($"Bit index is required for bit-in-word address '{address}'.");
                ValidateReadShape(parsed, ".U", 1, consecutive: false);
                continue;
            }

            if (normalized == "BIT" ||
                (normalized.Length == 0 && DirectBitDeviceTypes.Contains(parsed.DeviceType)))
            {
                if (!DirectBitDeviceTypes.Contains(parsed.DeviceType))
                    throw new HostLinkProtocolError(
                        $"Logical data type BIT is only valid for direct bit devices: '{address}'.");
                ValidateReadShape(parsed, null, 1, consecutive: false);
                continue;
            }

            if (normalized == "F")
            {
                KvHostLinkDevice.ValidateFloat32DeviceType(parsed.DeviceType, address);
                ValidateReadShape(parsed, ".U", 2, consecutive: true);
                continue;
            }

            if (normalized is not ("U" or "S" or "D" or "L" or "H"))
                throw new HostLinkProtocolError($"Unsupported named read data type '{dtype}'.");
            ValidateReadShape(parsed, "." + normalized, 1, consecutive: false);
        }

        if (commentEncoding.HasValue && !hasComment)
        {
            throw new ArgumentException(
                "An explicit comment encoding requires at least one :COMMENT address.",
                nameof(commentEncoding));
        }
    }

    private static void ValidateReadShape(
        KvDeviceAddress address,
        string? dataFormat,
        int count,
        bool consecutive)
    {
        string suffix = KvHostLinkDevice.RequireExplicitFormat(address, dataFormat);
        if (consecutive)
            KvHostLinkDevice.ValidateDeviceCount(address.DeviceType, suffix, count);
        KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix, count);
    }

    private static CompiledReadNamedPlan CompileReadNamedPlan(IEnumerable<string> addresses)
    {
        var requestsInInputOrder = new List<ReadPlanRequest>();
        var groups = new List<ReadPlanGroup>();
        var batchGroups = new Dictionary<(string DeviceType, ReadPlanSegmentMode Mode), ReadPlanGroup>();
        int index = 0;
        foreach (var address in addresses)
        {
            ReadPlanRequest request = CompileReadNamedRequest(address, index);
            requestsInInputOrder.Add(request);
            index++;

            ReadPlanSegmentMode mode = request.Kind == ReadPlanValueKind.DirectBit
                ? ReadPlanSegmentMode.DirectBits
                : ReadPlanSegmentMode.Words;
            if (!request.Batchable)
            {
                groups.Add(new ReadPlanGroup
                {
                    Mode = mode,
                    IsIndividual = true,
                    Requests = [request],
                });
                continue;
            }

            var key = (request.BaseAddress.DeviceType, mode);
            if (!batchGroups.TryGetValue(key, out ReadPlanGroup? group))
            {
                group = new ReadPlanGroup
                {
                    Mode = mode,
                    IsIndividual = false,
                    Requests = [],
                };
                batchGroups.Add(key, group);
                groups.Add(group);
            }
            group.Requests.Add(request);
        }

        var segments = new List<ReadPlanSegment>();
        foreach (ReadPlanGroup group in groups)
        {
            if (group.IsIndividual)
            {
                ReadPlanRequest request = group.Requests[0];
                segments.Add(new ReadPlanSegment
                {
                    StartAddress = request.BaseAddress,
                    StartNumber = ReadPlanNumber(request),
                    Count = 1,
                    Mode = group.Mode,
                    Requests = [request],
                    IsIndividual = true,
                });
                continue;
            }

            ReadPlanRequest[] sorted = group.Requests
                .OrderBy(ReadPlanNumber)
                .ThenBy(static request => request.Index)
                .ToArray();
            var pending = new List<ReadPlanRequest>();
            KvDeviceAddress? currentStart = null;
            int currentStartNumber = 0;
            int currentEndExclusive = 0;

            void FinishCurrentSegment()
            {
                if (currentStart is null)
                    return;
                segments.Add(new ReadPlanSegment
                {
                    StartAddress = currentStart,
                    StartNumber = currentStartNumber,
                    Count = currentEndExclusive - currentStartNumber,
                    Mode = group.Mode,
                    Requests = [.. pending],
                    IsIndividual = false,
                });
                pending.Clear();
            }

            foreach (ReadPlanRequest request in sorted)
            {
                int requestStart = ReadPlanNumber(request);
                int requestEndExclusive = requestStart + GetWordWidth(request.Kind);
                int segmentLimit = ReadPlanSegmentLimit(request.BaseAddress.DeviceType, group.Mode);
                bool startsNewSegment = currentStart is null ||
                    requestStart > currentEndExclusive ||
                    requestEndExclusive - currentStartNumber > segmentLimit;

                if (startsNewSegment)
                {
                    FinishCurrentSegment();
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
            FinishCurrentSegment();
        }

        return new CompiledReadNamedPlan
        {
            RequestsInInputOrder = [.. requestsInInputOrder],
            Segments = [.. segments],
        };
    }

    private static async Task<object[]> ExecuteReadNamedPlanAsync(
        KvHostLinkClient client,
        CompiledReadNamedPlan plan,
        HostLinkCommentEncoding? commentEncoding,
        CancellationToken ct)
    {
        var resolved = new object[plan.RequestsInInputOrder.Length];

        foreach (var segment in plan.Segments)
        {
            if (segment.IsIndividual)
            {
                ReadPlanRequest request = segment.Requests[0];
                resolved[request.Index] = await ExecuteIndividualReadNamedRequestAsync(
                    client,
                    request,
                    commentEncoding,
                    ct).ConfigureAwait(false);
                continue;
            }

            if (segment.Mode == ReadPlanSegmentMode.DirectBits)
            {
                string[] tokens = await client.ReadCoreAsync(
                    segment.StartAddress.ToText(),
                    null,
                    segment.Count,
                    consecutive: true,
                    ct).ConfigureAwait(false);

                foreach (var request in segment.Requests)
                {
                    int offset = ReadPlanNumber(request) - segment.StartNumber;
                    resolved[request.Index] = ResolveDirectBitValue(tokens, offset);
                }
            }
            else
            {
                string[] tokens = await client.ReadCoreAsync(
                    segment.StartAddress.ToText(),
                    ".U",
                    segment.Count,
                    consecutive: true,
                    ct).ConfigureAwait(false);
                ushort[] words = tokens
                    .Select(static token => ushort.Parse(token, CultureInfo.InvariantCulture))
                    .ToArray();

                foreach (var request in segment.Requests)
                {
                    int offset = ReadPlanNumber(request) - segment.StartNumber;
                    resolved[request.Index] = ResolvePlannedValue(words, offset, request.Kind, request.BitIndex);
                }
            }
        }
        return resolved;
    }

    private static async Task<object> ExecuteIndividualReadNamedRequestAsync(
        KvHostLinkClient client,
        ReadPlanRequest request,
        HostLinkCommentEncoding? commentEncoding,
        CancellationToken ct)
    {
        string baseAddress = request.BaseAddress.ToText();
        if (request.DataType == "BIT_IN_WORD")
        {
            string[] tokens = await client.ReadCoreAsync(baseAddress, ".U", 1, false, ct).ConfigureAwait(false);
            int word = ushort.Parse(RequireDataToken(tokens, baseAddress), CultureInfo.InvariantCulture);
            return ((word >> request.BitIndex) & 1) != 0;
        }

        if (request.DataType == "COMMENT")
        {
            HostLinkCommentEncoding selectedEncoding = commentEncoding
                ?? throw new InvalidOperationException("COMMENT was not rejected during aggregate preflight.");
            return await client.ReadCommentsCoreAsync(baseAddress, selectedEncoding, ct).ConfigureAwait(false);
        }

        return await ReadTypedCoreAsync(
            client,
            baseAddress,
            request.DataType,
            operationAlreadyAdmitted: true,
            ct).ConfigureAwait(false);
    }

    private static Dictionary<string, object> MaterializeReadNamedResult(
        CompiledReadNamedPlan plan,
        object[] resolved)
    {
        var result = new Dictionary<string, object>(plan.RequestsInInputOrder.Length);
        foreach (ReadPlanRequest request in plan.RequestsInInputOrder)
            result.Add(request.Address, resolved[request.Index]);
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
            ReadPlanValueKind.Hex16 => words[offset].ToString("X4", CultureInfo.InvariantCulture),
            ReadPlanValueKind.BitInWord => ((words[offset] >> bitIndex) & 1) != 0,
            ReadPlanValueKind.DirectBit => throw new HostLinkProtocolError("Direct bit values must be resolved from bit tokens."),
            ReadPlanValueKind.Individual => throw new HostLinkProtocolError("Individual values must be resolved by their native command."),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private static bool ResolveDirectBitValue(string[] tokens, int offset)
    {
        if (offset < 0 || offset >= tokens.Length)
            throw new HostLinkProtocolError("Batched direct bit response was too short");
        return ParseBoolToken(tokens[offset]);
    }

    private static ReadPlanRequest CompileReadNamedRequest(string address, int index)
    {
        var (baseAddress, dataType, bitIndex) = ParseAddress(address);
        KvDeviceAddress parsed = KvHostLinkDevice.ParseDevice(baseAddress) with { Suffix = "" };
        string normalized = dataType.Trim().TrimStart('.').ToUpperInvariant();

        if ((normalized.Length == 0 || normalized == "BIT") &&
            DirectBitDeviceTypes.Contains(parsed.DeviceType))
        {
            return new ReadPlanRequest(
                index, address, parsed, ReadPlanValueKind.DirectBit, 0, normalized, Batchable: true);
        }

        if (normalized == "BIT_IN_WORD" &&
            bitIndex.HasValue &&
            OptimizableReadNamedDeviceTypes.Contains(parsed.DeviceType))
        {
            return new ReadPlanRequest(
                index, address, parsed, ReadPlanValueKind.BitInWord, bitIndex.Value, normalized, Batchable: true);
        }

        if (OptimizableReadNamedDeviceTypes.Contains(parsed.DeviceType) &&
            TryMapReadPlanValueKind(normalized, out ReadPlanValueKind kind))
        {
            return new ReadPlanRequest(index, address, parsed, kind, 0, normalized, Batchable: true);
        }

        return new ReadPlanRequest(
            index,
            address,
            parsed,
            ReadPlanValueKind.Individual,
            bitIndex.GetValueOrDefault(),
            normalized,
            Batchable: false);
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
            case "H":
                kind = ReadPlanValueKind.Hex16;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static int GetWordWidth(ReadPlanValueKind kind)
        => kind is ReadPlanValueKind.Unsigned32 or ReadPlanValueKind.Signed32 or ReadPlanValueKind.Float32 ? 2 : 1;

    private static int ReadPlanSegmentLimit(string deviceType, ReadPlanSegmentMode mode)
    {
        if (mode == ReadPlanSegmentMode.DirectBits)
            return 1000;
        return deviceType switch
        {
            "TM" => 512,
            "Z" => 12,
            _ => 1000,
        };
    }

    private static int ReadPlanNumber(ReadPlanRequest request) =>
        request.Kind == ReadPlanValueKind.DirectBit &&
        KvHostLinkDevice.UsesBitBankAddress(request.BaseAddress.DeviceType)
            ? KvHostLinkDevice.BitBankLogicalNumber(request.BaseAddress.Number)
            : request.BaseAddress.Number;

    // -----------------------------------------------------------------------
    // Internal helpers
    // -----------------------------------------------------------------------

    private static bool ParseBoolToken(string token)
    {
        switch (token.Trim().ToUpperInvariant())
        {
            case "1":
            case "ON":
                return true;
            case "0":
            case "OFF":
                return false;
            default:
                throw new HostLinkProtocolError($"Invalid direct bit response token: {token}");
        }
    }

    private static string RequireDataToken(string[] tokens, string context)
    {
        if (tokens.Length == 0)
            throw new HostLinkProtocolError($"Response for '{context}' did not contain a data token.");
        return tokens[0];
    }

    private static string RequireLastDataToken(string[] tokens, string context)
    {
        if (tokens.Length == 0)
            throw new HostLinkProtocolError($"Response for '{context}' did not contain a data token.");
        return tokens[^1];
    }

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
