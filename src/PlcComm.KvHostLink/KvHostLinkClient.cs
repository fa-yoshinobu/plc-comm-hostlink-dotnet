using System.Globalization;
using System.Net.Sockets;
using System.Text;

namespace PlcComm.KvHostLink;

/// <summary>
/// A low-level Host Link (Upper Link) client for KEYENCE KV series PLCs.
/// </summary>
/// <remarks>
/// This class serializes individual raw requests on one connection, but
/// compound helper workflows such as typed polling and read-modify-write are
/// better served by <see cref="QueuedKvHostLinkClient"/>. For application code,
/// prefer <see cref="KvHostLinkClientFactory.OpenAndConnectAsync(KvHostLinkConnectionOptions, CancellationToken)"/>.
/// </remarks>
public sealed class KvHostLinkClient : IDisposable, IAsyncDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly HostLinkTransportMode _transportMode;
    private TcpClient? _tcp;
    private NetworkStream? _tcpStream;
    private UdpClient? _udp;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private byte[] _rxBuf = new byte[4096];
    private int _rxStart;
    private int _rxCount;
    private readonly byte[] _tcpReadBuf = new byte[8192];

    public KvHostLinkClient(string host, int port = 8501, HostLinkTransportMode transportMode = HostLinkTransportMode.Tcp)
    {
        _host = host;
        _port = port;
        _transportMode = transportMode;
    }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
    public bool AppendLfOnSend { get; set; }

    /// <summary>
    /// Optional hook called for every raw frame sent and received.
    /// Useful for protocol tracing and debugging.
    /// </summary>
    public Action<HostLinkTraceFrame>? TraceHook { get; set; }

    public bool IsOpen => _transportMode == HostLinkTransportMode.Tcp ? _tcp?.Connected == true : _udp is not null;

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (IsOpen) return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsOpen) return;

            if (_transportMode == HostLinkTransportMode.Tcp)
            {
                _tcp = new TcpClient();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked.CancelAfter(Timeout);
                await _tcp.ConnectAsync(_host, _port, linked.Token).ConfigureAwait(false);
                _tcp.NoDelay = true;
                _tcpStream = _tcp.GetStream();
            }
            else
            {
                _udp = new UdpClient();
                _udp.Connect(_host, _port);
            }
            _rxStart = 0; _rxCount = 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Open() => OpenAsync().GetAwaiter().GetResult();

    public void Close()
    {
        _lock.Wait();
        try
        {
            _tcpStream?.Dispose();
            _tcpStream = null;
            _tcp?.Close();
            _tcp = null;
            _udp?.Dispose();
            _udp = null;
            _rxStart = 0; _rxCount = 0;
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task CloseAsync()
    {
        Close();
        return Task.CompletedTask;
    }

    public void Dispose() => Close();

    public ValueTask DisposeAsync()
    {
        Close();
        return ValueTask.CompletedTask;
    }

    private void FireTrace(HostLinkTraceDirection direction, byte[] data)
        => TraceHook?.Invoke(new HostLinkTraceFrame(direction, data, DateTime.UtcNow));

    public async Task<string> SendRawAsync(string body, CancellationToken cancellationToken = default)
    {
        await OpenAsync(cancellationToken).ConfigureAwait(false);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var frame = KvHostLinkProtocol.BuildFrame(body, AppendLfOnSend);
            FireTrace(HostLinkTraceDirection.Send, frame);
            if (_transportMode == HostLinkTransportMode.Tcp)
            {
                await _tcpStream!.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                var responseRaw = await RecvTcpLineAsync(cancellationToken).ConfigureAwait(false);
                FireTrace(HostLinkTraceDirection.Receive, responseRaw);
                return KvHostLinkProtocol.EnsureSuccess(KvHostLinkProtocol.DecodeResponse(responseRaw));
            }
            else
            {
                await _udp!.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                var result = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                FireTrace(HostLinkTraceDirection.Receive, result.Buffer);
                return KvHostLinkProtocol.EnsureSuccess(KvHostLinkProtocol.DecodeResponse(result.Buffer));
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<byte[]> RecvTcpLineAsync(CancellationToken cancellationToken)
    {
        // Create CTS once per receive call instead of inside the read loop.
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(Timeout);
        var token = linked.Token;

        while (true)
        {
            // Scan for first CR or LF -- O(n) on valid data only, no allocation
            int foundIdx = -1;
            for (int i = 0; i < _rxCount; i++)
            {
                byte b = _rxBuf[_rxStart + i];
                if (b == '\r' || b == '\n') { foundIdx = i; break; }
            }

            if (foundIdx >= 0)
            {
                var line = _rxBuf.AsSpan(_rxStart, foundIdx).ToArray();
                int skip = foundIdx;
                while (skip < _rxCount && (_rxBuf[_rxStart + skip] == '\r' || _rxBuf[_rxStart + skip] == '\n'))
                    skip++;
                _rxStart += skip;   // O(1) — no copy
                _rxCount -= skip;
                // Compact when the dead zone at the front exceeds half the buffer
                if (_rxStart > _rxBuf.Length / 2)
                {
                    _rxBuf.AsSpan(_rxStart, _rxCount).CopyTo(_rxBuf);
                    _rxStart = 0;
                }
                return line;
            }

            int read = await _tcpStream!.ReadAsync(_tcpReadBuf, token).ConfigureAwait(false);
            if (read == 0)
            {
                if (_rxCount > 0)
                {
                    var line = _rxBuf.AsSpan(_rxStart, _rxCount).ToArray();
                    _rxStart = 0; _rxCount = 0;
                    return line;
                }
                throw new HostLinkConnectionError("Connection closed by PLC");
            }

            // Ensure there is room for the newly read bytes
            if (_rxStart + _rxCount + read > _rxBuf.Length)
            {
                // Compact first (move valid data to index 0)
                if (_rxCount > 0)
                    _rxBuf.AsSpan(_rxStart, _rxCount).CopyTo(_rxBuf);
                _rxStart = 0;
                // Grow the backing array if still not enough room
                if (_rxCount + read > _rxBuf.Length)
                {
                    var grown = new byte[Math.Max(_rxBuf.Length * 2, _rxCount + read)];
                    _rxBuf.AsSpan(0, _rxCount).CopyTo(grown);
                    _rxBuf = grown;
                }
            }
            _tcpReadBuf.AsSpan(0, read).CopyTo(_rxBuf.AsSpan(_rxStart + _rxCount));
            _rxCount += read;
        }
    }

    private async Task ExpectOkAsync(string body, CancellationToken cancellationToken = default)
    {
        var response = await SendRawAsync(body, cancellationToken).ConfigureAwait(false);
        if (response != "OK")
        {
            throw new HostLinkProtocolError($"Expected 'OK' but received '{response}' for command '{body}'");
        }
    }

    // --- Commands ---

    public async Task ChangeModeAsync(KvPlcMode mode, CancellationToken cancellationToken = default)
    {
        await ExpectOkAsync($"M{(int)mode}", cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearErrorAsync(CancellationToken cancellationToken = default)
    {
        await ExpectOkAsync("ER", cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> CheckErrorNoAsync(CancellationToken cancellationToken = default)
    {
        return await SendRawAsync("?E", cancellationToken).ConfigureAwait(false);
    }

    public async Task<KvModelInfo> QueryModelAsync(CancellationToken cancellationToken = default)
    {
        string code = await SendRawAsync("?K", cancellationToken).ConfigureAwait(false);
        return new KvModelInfo(code, KvHostLinkModels.ModelCodes.GetValueOrDefault(code, "Unknown"));
    }

    public async Task<KvPlcMode> ConfirmOperatingModeAsync(CancellationToken cancellationToken = default)
    {
        string response = await SendRawAsync("?M", cancellationToken).ConfigureAwait(false);
        return (KvPlcMode)int.Parse(response, CultureInfo.InvariantCulture);
    }

    public async Task SetTimeAsync(DateTime? value = null, CancellationToken cancellationToken = default)
    {
        var dt = value ?? DateTime.Now;
        int year = dt.Year % 100;
        int week = (int)dt.DayOfWeek; // Sun=0, Mon=1..Sat=6 matches HostLink encoding directly

        string cmd = $"WRT {year:D2} {dt.Month:D2} {dt.Day:D2} {dt.Hour:D2} {dt.Minute:D2} {dt.Second:D2} {week}";
        await ExpectOkAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    public async Task ForcedSetAsync(string device, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("ST", addr.DeviceType, KvHostLinkModels.ForceDeviceTypes);
        await ExpectOkAsync($"ST {(addr with { Suffix = "" }).ToText()}", cancellationToken).ConfigureAwait(false);
    }

    public async Task ForcedResetAsync(string device, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("RS", addr.DeviceType, KvHostLinkModels.ForceDeviceTypes);
        await ExpectOkAsync($"RS {(addr with { Suffix = "" }).ToText()}", cancellationToken).ConfigureAwait(false);
    }

    public async Task<string[]> ReadAsync(string device, string? dataFormat = null, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        string suffix = dataFormat != null ? KvHostLinkDevice.NormalizeSuffix(dataFormat) : addr.Suffix;
        if (string.IsNullOrEmpty(suffix))
            suffix = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, "");
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, suffix);

        var target = addr with { Suffix = suffix };
        string response = await SendRawAsync($"RD {target.ToText()}", cancellationToken).ConfigureAwait(false);
        return KvHostLinkProtocol.SplitDataTokens(response);
    }

    public async Task<string[]> ReadConsecutiveAsync(string device, int count, string? dataFormat = null, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        string suffix = dataFormat != null ? KvHostLinkDevice.NormalizeSuffix(dataFormat) : addr.Suffix;
        if (string.IsNullOrEmpty(suffix))
            suffix = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, "");

        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, suffix, count);
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, suffix, count);

        var target = addr with { Suffix = suffix };
        string response = await SendRawAsync($"RDS {target.ToText()} {count}", cancellationToken).ConfigureAwait(false);
        return KvHostLinkProtocol.SplitDataTokens(response);
    }

    public async Task WriteAsync<T>(string device, T value, string? dataFormat = null, CancellationToken cancellationToken = default)
        where T : IFormattable
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        string suffix = dataFormat != null ? KvHostLinkDevice.NormalizeSuffix(dataFormat) : addr.Suffix;
        if (string.IsNullOrEmpty(suffix))
            suffix = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, "");
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, suffix);

        var target = addr with { Suffix = suffix };
        string valStr = FormatValue(value, suffix);
        await ExpectOkAsync($"WR {target.ToText()} {valStr}", cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteConsecutiveAsync<T>(string device, IEnumerable<T> values, string? dataFormat = null, CancellationToken cancellationToken = default)
        where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");

        var addr = KvHostLinkDevice.ParseDevice(device);
        string suffix = dataFormat != null ? KvHostLinkDevice.NormalizeSuffix(dataFormat) : addr.Suffix;
        if (string.IsNullOrEmpty(suffix))
            suffix = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, "");

        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, suffix, valList.Count);
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, suffix, valList.Count);

        var target = addr with { Suffix = suffix };
        string payload = BuildValuePayload(valList, suffix);
        await ExpectOkAsync($"WRS {target.ToText()} {valList.Count} {payload}", cancellationToken).ConfigureAwait(false);
    }

    public async Task RegisterMonitorBitsAsync(IEnumerable<string> devices, CancellationToken cancellationToken = default)
    {
        var targets = devices.ToList();
        if (targets.Count == 0) throw new HostLinkProtocolError("At least one device is required");
        if (targets.Count > 120) throw new HostLinkProtocolError("Maximum 120 devices can be registered");

        var sb = new StringBuilder("MBS");
        foreach (var device in targets)
        {
            var addr = KvHostLinkDevice.ParseDevice(device);
            KvHostLinkDevice.ValidateDeviceType("MBS", addr.DeviceType, KvHostLinkModels.MbsDeviceTypes);
            sb.Append(' ');
            sb.Append((addr with { Suffix = "" }).ToText());
        }
        await ExpectOkAsync(sb.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task RegisterMonitorWordsAsync(IEnumerable<string> devices, CancellationToken cancellationToken = default)
    {
        var targets = devices.ToList();
        if (targets.Count == 0) throw new HostLinkProtocolError("At least one device is required");
        if (targets.Count > 120) throw new HostLinkProtocolError("Maximum 120 devices can be registered");

        var sb = new StringBuilder("MWS");
        foreach (var device in targets)
        {
            var addr = KvHostLinkDevice.ParseDevice(device);
            KvHostLinkDevice.ValidateDeviceType("MWS", addr.DeviceType, KvHostLinkModels.MwsDeviceTypes);
            string suffix = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, addr.Suffix);
            KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, suffix);
            sb.Append(' ');
            sb.Append((addr with { Suffix = suffix }).ToText());
        }
        await ExpectOkAsync(sb.ToString(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<string[]> ReadMonitorBitsAsync(CancellationToken cancellationToken = default)
    {
        string response = await SendRawAsync("MBR", cancellationToken).ConfigureAwait(false);
        return KvHostLinkProtocol.SplitDataTokens(response);
    }

    public async Task<string[]> ReadMonitorWordsAsync(CancellationToken cancellationToken = default)
    {
        string response = await SendRawAsync("MWR", cancellationToken).ConfigureAwait(false);
        return KvHostLinkProtocol.SplitDataTokens(response);
    }

    /// <summary>Consecutively force-sets up to 16 bit devices starting at <paramref name="device"/> (STS command).</summary>
    public async Task ForcedSetConsecutiveAsync(
        string device, int count, CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(count), "count must be 1-16.");
        var addr = KvHostLinkDevice.ParseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("STS", addr.DeviceType, KvHostLinkModels.ForceDeviceTypes);
        await ExpectOkAsync($"STS {(addr with { Suffix = "" }).ToText()} {count}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Consecutively force-resets up to 16 bit devices starting at <paramref name="device"/> (RSS command).</summary>
    public async Task ForcedResetConsecutiveAsync(
        string device, int count, CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(count), "count must be 1-16.");
        var addr = KvHostLinkDevice.ParseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("RSS", addr.DeviceType, KvHostLinkModels.ForceDeviceTypes);
        await ExpectOkAsync($"RSS {(addr with { Suffix = "" }).ToText()} {count}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads consecutive devices using the legacy RDE command.
    /// Prefer <see cref="ReadConsecutiveAsync"/> on current models.
    /// </summary>
    public async Task<string[]> ReadConsecutiveLegacyAsync(
        string device, int count, string? dataFormat = null, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        string suffix = dataFormat != null ? KvHostLinkDevice.NormalizeSuffix(dataFormat) : addr.Suffix;
        string effectiveFormat = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, suffix);
        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, effectiveFormat, count);
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, effectiveFormat, count);
        var target = addr with { Suffix = effectiveFormat };
        string response = await SendRawAsync($"RDE {target.ToText()} {count}", cancellationToken)
            .ConfigureAwait(false);
        return KvHostLinkProtocol.SplitDataTokens(response);
    }

    /// <summary>
    /// Writes consecutive devices using the legacy WRE command.
    /// Prefer <see cref="WriteConsecutiveAsync"/> on current models.
    /// </summary>
    public async Task WriteConsecutiveLegacyAsync<T>(
        string device, IEnumerable<T> values, string? dataFormat = null,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");
        var addr = KvHostLinkDevice.ParseDevice(device);
        string suffix = dataFormat != null ? KvHostLinkDevice.NormalizeSuffix(dataFormat) : addr.Suffix;
        string effectiveFormat = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, suffix);
        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, effectiveFormat, valList.Count);
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, effectiveFormat, valList.Count);
        var target = addr with { Suffix = effectiveFormat };
        string payload = BuildValuePayload(valList, effectiveFormat);
        await ExpectOkAsync($"WRE {target.ToText()} {valList.Count} {payload}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Writes a set-value (preset) for a timer or counter device (WS command).
    /// Supported device types: T, C.
    /// </summary>
    public async Task WriteSetValueAsync<T>(
        string device, T value, string? dataFormat = null,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("WS", addr.DeviceType, KvHostLinkModels.WsDeviceTypes);
        string suffix = !string.IsNullOrEmpty(addr.Suffix) ? addr.Suffix
            : KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, "");
        if (dataFormat != null) suffix = KvHostLinkDevice.NormalizeSuffix(dataFormat);
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, suffix);
        var target = addr with { Suffix = suffix };
        string valStr = FormatValue(value, suffix);
        await ExpectOkAsync($"WS {target.ToText()} {valStr}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes set-values (presets) for consecutive timer or counter devices (WSS command).
    /// Supported device types: T, C.
    /// </summary>
    public async Task WriteSetValueConsecutiveAsync<T>(
        string device, IEnumerable<T> values, string? dataFormat = null,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");
        var addr = KvHostLinkDevice.ParseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("WSS", addr.DeviceType, KvHostLinkModels.WsDeviceTypes);
        string suffix = !string.IsNullOrEmpty(addr.Suffix) ? addr.Suffix
            : KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, "");
        if (dataFormat != null) suffix = KvHostLinkDevice.NormalizeSuffix(dataFormat);
        KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, suffix, valList.Count);
        var target = addr with { Suffix = suffix };
        string payload = BuildValuePayload(valList, suffix);
        await ExpectOkAsync($"WSS {target.ToText()} {valList.Count} {payload}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Switches the active data bank (BE command). Valid range: 0–15.</summary>
    public async Task SwitchBankAsync(int bankNo, CancellationToken cancellationToken = default)
    {
        if (bankNo is < 0 or > 15)
            throw new ArgumentOutOfRangeException(nameof(bankNo), "bankNo must be 0-15.");
        await ExpectOkAsync($"BE {bankNo}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Reads buffer memory from an expansion unit (URD command).
    /// </summary>
    /// <param name="unitNo">Unit number (0–48).</param>
    /// <param name="address">Buffer address (0–59999).</param>
    /// <param name="count">Number of values to read.</param>
    /// <param name="dataFormat">Data format suffix, e.g. ".U" or ".S". Defaults to ".U".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<string[]> ReadExpansionUnitBufferAsync(
        int unitNo, int address, int count,
        string dataFormat = "",
        CancellationToken cancellationToken = default)
    {
        if (unitNo is < 0 or > 48)
            throw new ArgumentOutOfRangeException(nameof(unitNo), "unitNo must be 0-48.");
        if (address is < 0 or > 59999)
            throw new ArgumentOutOfRangeException(nameof(address), "address must be 0-59999.");

        string suffix = string.IsNullOrEmpty(dataFormat) ? ".U"
            : KvHostLinkDevice.NormalizeSuffix(dataFormat);
        KvHostLinkDevice.ValidateExpansionBufferCount(suffix, count);
        KvHostLinkDevice.ValidateExpansionBufferSpan(address, suffix, count);

        string cmd = $"URD {unitNo:D2} {address} {suffix} {count}";
        string response = await SendRawAsync(cmd, cancellationToken).ConfigureAwait(false);
        return KvHostLinkProtocol.SplitDataTokens(response);
    }

    /// <summary>
    /// Writes buffer memory to an expansion unit (UWR command).
    /// </summary>
    /// <param name="unitNo">Unit number (0–48).</param>
    /// <param name="address">Buffer address (0–59999).</param>
    /// <param name="values">Values to write.</param>
    /// <param name="dataFormat">Data format suffix, e.g. ".U" or ".S". Defaults to ".U".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WriteExpansionUnitBufferAsync<T>(
        int unitNo, int address, IEnumerable<T> values,
        string dataFormat = "",
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");
        if (unitNo is < 0 or > 48)
            throw new ArgumentOutOfRangeException(nameof(unitNo), "unitNo must be 0-48.");
        if (address is < 0 or > 59999)
            throw new ArgumentOutOfRangeException(nameof(address), "address must be 0-59999.");

        string suffix = string.IsNullOrEmpty(dataFormat) ? ".U"
            : KvHostLinkDevice.NormalizeSuffix(dataFormat);
        KvHostLinkDevice.ValidateExpansionBufferCount(suffix, valList.Count);
        KvHostLinkDevice.ValidateExpansionBufferSpan(address, suffix, valList.Count);

        string payload = BuildValuePayload(valList, suffix);
        string cmd = $"UWR {unitNo:D2} {address} {suffix} {valList.Count} {payload}";
        await ExpectOkAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> ReadCommentsAsync(string device, bool stripPadding = true, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("RDC", addr.DeviceType, KvHostLinkModels.RdcDeviceTypes);
        string response = await SendRawAsync($"RDC {(addr with { Suffix = "" }).ToText()}", cancellationToken).ConfigureAwait(false);
        return stripPadding ? response.TrimEnd(' ') : response;
    }

    private static string BuildValuePayload<T>(List<T> values, string dataFormat) where T : IFormattable
    {
        var sb = new StringBuilder();
        foreach (var v in values)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(FormatValue(v, dataFormat));
        }
        return sb.ToString();
    }

    // Constrained call on T avoids boxing for value types (int, ushort, float, etc.)
    private static string FormatValue<T>(T value, string dataFormat) where T : IFormattable
    {
        if (dataFormat == ".H" && value is int i)
        {
            return (i & 0xFFFF).ToString("X", CultureInfo.InvariantCulture);
        }
        return value.ToString(null, CultureInfo.InvariantCulture);
    }
}
