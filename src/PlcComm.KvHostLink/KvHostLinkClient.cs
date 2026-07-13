using System.ComponentModel;
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
    private const int MaxResponseBodyLength = 65_536;
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
    private bool _skipLeadingLf;
    private readonly byte[] _tcpReadBuf = new byte[8192];
    private TimeSpan _timeout = TimeSpan.FromSeconds(3);
    private int _monitorBitCount;
    private string[] _monitorWordFormats = [];
    private long _requestCount;
    private long _txBytes;
    private long _rxBytes;

    public KvHostLinkClient(
        string host,
        int port,
        HostLinkTransportMode transportMode,
        string plcProfile)
    {
        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Host must not be empty.", nameof(host));
        if (port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "Port must be in the range 1-65535.");
        if (!Enum.IsDefined(transportMode))
            throw new ArgumentOutOfRangeException(nameof(transportMode), "Transport must be TCP or UDP.");
        _host = host;
        _port = port;
        _transportMode = transportMode;
        PlcProfile = KvHostLinkPlcProfiles.NormalizeName(plcProfile);
    }

    public string PlcProfile { get; }
    /// <summary>Gets an immutable snapshot of cumulative traffic for this client lifetime.</summary>
    public HostLinkTrafficStats TrafficStats => new(
        unchecked((ulong)Interlocked.Read(ref _requestCount)),
        unchecked((ulong)Interlocked.Read(ref _txBytes)),
        unchecked((ulong)Interlocked.Read(ref _rxBytes)));
    /// <summary>Gets or sets the operation timeout from 1 through <see cref="int.MaxValue"/> milliseconds.</summary>
    public TimeSpan Timeout
    {
        get => _timeout;
        set => _timeout = KvHostLinkTimeout.Validate(value, nameof(value));
    }

    /// <summary>
    /// Optional maintainer hook called once for every exact raw frame sent and received.
    /// Hook failures are isolated from communication behavior.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Action<HostLinkTraceFrame>? TraceHook { get; set; }

    public bool IsOpen => _transportMode == HostLinkTransportMode.Tcp ? _tcpStream is not null : _udp is not null;

    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        if (IsOpen) return;

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsOpen) return;

            if (_transportMode == HostLinkTransportMode.Tcp)
            {
                var tcp = new TcpClient();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked.CancelAfter(Timeout);
                try
                {
                    await tcp.ConnectAsync(_host, _port, linked.Token).ConfigureAwait(false);
                    tcp.NoDelay = true;
                    _tcp = tcp;
                    _tcpStream = tcp.GetStream();
                }
                catch
                {
                    tcp.Dispose();
                    throw;
                }
            }
            else
            {
                var udp = new UdpClient();
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                linked.CancelAfter(Timeout);
                try
                {
                    await udp.Client.ConnectAsync(_host, _port, linked.Token).ConfigureAwait(false);
                    _udp = udp;
                }
                catch
                {
                    udp.Dispose();
                    throw;
                }
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
            CloseTransport();
        }
        finally
        {
            _lock.Release();
        }
    }

    private void CloseTransport()
    {
        _tcpStream?.Dispose();
        _tcpStream = null;
        _tcp?.Close();
        _tcp = null;
        _udp?.Dispose();
        _udp = null;
        _rxStart = 0; _rxCount = 0;
        _skipLeadingLf = false;
        _monitorBitCount = 0;
        _monitorWordFormats = [];
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
    {
        try
        {
            TraceHook?.Invoke(new HostLinkTraceFrame(direction, data.ToArray(), DateTime.UtcNow));
        }
        catch
        {
            // Diagnostic hooks must not change frame bytes, retries, timeout, or command results.
        }
    }

    /// <summary>Sends one maintainer raw command and returns response body bytes without terminators.</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<byte[]> SendRawAsync(string body, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => SendRawCoreAsync(body, cancellationToken), cancellationToken);

    internal async Task<T> ExecuteExclusiveAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    internal async Task ExecuteExclusiveAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await operation().ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<byte[]> SendRawCoreAsync(string body, CancellationToken cancellationToken)
    {
        if (!IsOpen)
            throw new HostLinkNotConnectedError();

        var frame = KvHostLinkProtocol.BuildFrame(body);
        FireTrace(HostLinkTraceDirection.Send, frame);
        if (_transportMode == HostLinkTransportMode.Tcp)
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(Timeout);
            try
            {
                await _tcpStream!.WriteAsync(frame, linked.Token).ConfigureAwait(false);
                RecordSend(frame.Length);
                var response = await RecvTcpFrameAsync(linked.Token).ConfigureAwait(false);
                RecordReceive(response.Frame.Length);
                FireTrace(HostLinkTraceDirection.Receive, response.Frame);
                return response.Body;
            }
            catch
            {
                CloseTransport();
                throw;
            }
        }

        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
        {
            linked.CancelAfter(Timeout);
            try
            {
                await _udp!.SendAsync(frame, linked.Token).ConfigureAwait(false);
                RecordSend(frame.Length);
                var result = await _udp.ReceiveAsync(linked.Token).ConfigureAwait(false);
                FireTrace(HostLinkTraceDirection.Receive, result.Buffer);
                byte[] responseBody = KvHostLinkProtocol.ExtractBody(result.Buffer);
                RecordReceive(result.Buffer.Length);
                if (responseBody.Length > MaxResponseBodyLength)
                    throw new HostLinkProtocolError($"Response body exceeds {MaxResponseBodyLength} bytes");
                return responseBody;
            }
            catch
            {
                // Host Link has no transaction ID. A failed datagram exchange
                // must never leave a delayed response for the next request.
                CloseTransport();
                throw;
            }
        }
    }

    private void RecordSend(int length)
    {
        Interlocked.Increment(ref _requestCount);
        Interlocked.Add(ref _txBytes, length);
    }

    private void RecordReceive(int length) => Interlocked.Add(ref _rxBytes, length);

    private async Task<(byte[] Body, byte[] Frame)> RecvTcpFrameAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_skipLeadingLf && _rxCount > 0)
            {
                if (_rxBuf[_rxStart] == '\n')
                {
                    _rxStart++;
                    _rxCount--;
                }
                _skipLeadingLf = false;
            }

            int foundIdx = -1;
            for (int i = 0; i < _rxCount; i++)
            {
                byte b = _rxBuf[_rxStart + i];
                if (b == '\r' || b == '\n') { foundIdx = i; break; }
            }

            if (foundIdx >= 0)
            {
                if (foundIdx > MaxResponseBodyLength)
                    throw new HostLinkProtocolError($"Response body exceeds {MaxResponseBodyLength} bytes");

                int frameLength = foundIdx + 1;
                while (frameLength < _rxCount && (_rxBuf[_rxStart + frameLength] == '\r' || _rxBuf[_rxStart + frameLength] == '\n'))
                    frameLength++;
                bool includedLf = foundIdx + 1 < frameLength && _rxBuf[_rxStart + foundIdx + 1] == '\n';
                _skipLeadingLf = _rxBuf[_rxStart + foundIdx] == '\r' && !includedLf;
                byte[] body = _rxBuf.AsSpan(_rxStart, foundIdx).ToArray();
                byte[] receivedFrame = _rxBuf.AsSpan(_rxStart, frameLength).ToArray();
                _rxStart += frameLength;
                _rxCount -= frameLength;
                if (_rxStart > _rxBuf.Length / 2)
                {
                    _rxBuf.AsSpan(_rxStart, _rxCount).CopyTo(_rxBuf);
                    _rxStart = 0;
                }
                return (body, receivedFrame);
            }

            if (_rxCount > MaxResponseBodyLength)
                throw new HostLinkProtocolError($"Response body exceeds {MaxResponseBodyLength} bytes");

            int read = await _tcpStream!.ReadAsync(_tcpReadBuf, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                bool hadPartialResponse = _rxCount > 0;
                CloseTransport();
                string message = hadPartialResponse
                    ? "Connection closed by PLC before the response terminator"
                    : "Connection closed by PLC";
                throw new HostLinkConnectionError(message);
            }

            if (_rxStart + _rxCount + read > _rxBuf.Length)
            {
                if (_rxCount > 0)
                    _rxBuf.AsSpan(_rxStart, _rxCount).CopyTo(_rxBuf);
                _rxStart = 0;
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

    private Task<string> SendSemanticAsync(string body, CancellationToken cancellationToken)
        => ExecuteExclusiveAsync(() => SendSemanticCoreAsync(body, cancellationToken), cancellationToken);

    internal async Task<string> SendSemanticCoreAsync(string body, CancellationToken cancellationToken)
    {
        byte[] response = await SendRawCoreAsync(body, cancellationToken).ConfigureAwait(false);
        try
        {
            return KvHostLinkProtocol.DecodeSemanticResponse(response);
        }
        catch (HostLinkProtocolError)
        {
            CloseTransport();
            throw;
        }
    }

    private Task ExpectOkAsync(string body, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ExpectOkCoreAsync(body, cancellationToken), cancellationToken);

    internal async Task ExpectOkCoreAsync(string body, CancellationToken cancellationToken)
    {
        var response = await SendSemanticCoreAsync(body, cancellationToken).ConfigureAwait(false);
        if (response != "OK")
        {
            CloseTransport();
            throw new HostLinkProtocolError($"Expected 'OK' but received '{response}' for command '{body}'");
        }
    }

    private void InvalidateProtocolState()
        => CloseTransport();


    // --- Commands ---

    public async Task ChangeModeAsync(KvPlcMode mode, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode), "Mode must be Program or Run.");
        await ExpectOkAsync($"M{(int)mode}", cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearErrorAsync(CancellationToken cancellationToken = default)
    {
        await ExpectOkAsync("ER", cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> CheckErrorNoAsync(CancellationToken cancellationToken = default)
    {
        return await SendSemanticAsync("?E", cancellationToken).ConfigureAwait(false);
    }

    public async Task<KvModelInfo> QueryModelAsync(CancellationToken cancellationToken = default)
    {
        string code = await SendSemanticAsync("?K", cancellationToken).ConfigureAwait(false);
        return new KvModelInfo(code, KvHostLinkModels.ModelCodes.GetValueOrDefault(code, "Unknown"));
    }

    public Task<KvPlcMode> ConfirmOperatingModeAsync(CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            string response = await SendSemanticCoreAsync("?M", cancellationToken).ConfigureAwait(false);
            if (response == "0") return KvPlcMode.Program;
            if (response == "1") return KvPlcMode.Run;

            CloseTransport();
            throw new HostLinkProtocolError($"Unsupported PLC mode response: {response}");
        }, cancellationToken);

    /// <summary>Sets the PLC clock from an explicit local calendar value in years 2000 through 2099.</summary>
    public async Task SetTimeAsync(DateTime value, CancellationToken cancellationToken = default)
    {
        if (value.Year is < 2000 or > 2099)
            throw new ArgumentOutOfRangeException(nameof(value), "Host Link clock year must be in the range 2000..2099.");

        int year = value.Year - 2000;
        int week = (int)value.DayOfWeek; // Sun=0, Mon=1..Sat=6 matches HostLink encoding directly

        string cmd = $"WRT {year:D2} {value.Month:D2} {value.Day:D2} {value.Hour:D2} {value.Minute:D2} {value.Second:D2} {week}";
        await ExpectOkAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    public async Task ForcedSetAsync(string device, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("ST", addr.DeviceType, KvHostLinkModels.ForceDeviceTypes);
        await ExpectOkAsync($"ST {addr.ToText()}", cancellationToken).ConfigureAwait(false);
    }

    public async Task ForcedResetAsync(string device, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("RS", addr.DeviceType, KvHostLinkModels.ForceDeviceTypes);
        await ExpectOkAsync($"RS {addr.ToText()}", cancellationToken).ConfigureAwait(false);
    }

    public Task<string[]> ReadAsync(string device, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ReadCoreAsync(device, null, 1, false, cancellationToken), cancellationToken);

    public Task<string[]> ReadAsync(string device, string dataFormat, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ReadCoreAsync(device, dataFormat, 1, false, cancellationToken), cancellationToken);

    public Task<string[]> ReadConsecutiveAsync(string device, int count, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ReadCoreAsync(device, null, count, true, cancellationToken), cancellationToken);

    public Task<string[]> ReadConsecutiveAsync(
        string device,
        int count,
        string dataFormat,
        CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(() => ReadCoreAsync(device, dataFormat, count, true, cancellationToken), cancellationToken);

    internal async Task<string[]> ReadCoreAsync(
        string device,
        string? dataFormat,
        int count,
        bool consecutive,
        CancellationToken cancellationToken)
    {
        var address = KvHostLinkDevice.RequireBaseDevice(device);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(address, dataFormat);
        if (consecutive)
            KvHostLinkDevice.ValidateDeviceCount(address.DeviceType, suffix, count);
        else if (count != 1)
            throw new HostLinkProtocolError("A single-device read must request exactly one value.");
        KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix, count);

        var target = address with { Suffix = suffix };
        string command = consecutive ? $"RDS {target.ToText()} {count}" : $"RD {target.ToText()}";
        string response = await SendSemanticCoreAsync(command, cancellationToken).ConfigureAwait(false);
        string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
        int expectedCount = consecutive
            ? count
            : KvHostLinkDevice.ReadResponseTokenCount(address.DeviceType, suffix);
        try
        {
            KvHostLinkProtocol.ValidateResponseTokens(tokens, suffix, expectedCount);
        }
        catch (HostLinkProtocolError)
        {
            InvalidateProtocolState();
            throw;
        }
        return tokens;
    }

    public Task WriteAsync<T>(string device, T value, CancellationToken cancellationToken = default)
        where T : IFormattable
        => ExecuteExclusiveAsync(() => WriteCoreAsync(device, value, null, cancellationToken), cancellationToken);

    public Task WriteAsync<T>(
        string device,
        T value,
        string dataFormat,
        CancellationToken cancellationToken = default)
        where T : IFormattable
        => ExecuteExclusiveAsync(() => WriteCoreAsync(device, value, dataFormat, cancellationToken), cancellationToken);

    internal async Task WriteCoreAsync<T>(
        string device,
        T value,
        string? dataFormat,
        CancellationToken cancellationToken) where T : IFormattable
    {
        var address = KvHostLinkDevice.RequireBaseDevice(device);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(address, dataFormat);
        KvHostLinkDevice.ValidateDeviceType("WR", address.DeviceType, KvHostLinkModels.WrDeviceTypes);
        KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix);
        string valueText = FormatValue(value, suffix);
        await ExpectOkCoreAsync(
            $"WR {(address with { Suffix = suffix }).ToText()} {valueText}",
            cancellationToken).ConfigureAwait(false);
    }

    public Task WriteConsecutiveAsync<T>(
        string device,
        IEnumerable<T> values,
        CancellationToken cancellationToken = default) where T : IFormattable
        => ExecuteExclusiveAsync(() => WriteConsecutiveCoreAsync(device, values, null, cancellationToken), cancellationToken);

    public Task WriteConsecutiveAsync<T>(
        string device,
        IEnumerable<T> values,
        string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
        => ExecuteExclusiveAsync(() => WriteConsecutiveCoreAsync(device, values, dataFormat, cancellationToken), cancellationToken);

    internal async Task WriteConsecutiveCoreAsync<T>(
        string device,
        IEnumerable<T> values,
        string? dataFormat,
        CancellationToken cancellationToken) where T : IFormattable
    {
        var valueList = values.ToList();
        if (valueList.Count == 0)
            throw new HostLinkProtocolError("values must not be empty");

        var address = KvHostLinkDevice.RequireBaseDevice(device);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(address, dataFormat);
        KvHostLinkDevice.ValidateDeviceType("WRS", address.DeviceType, KvHostLinkModels.WrDeviceTypes);
        KvHostLinkDevice.ValidateDeviceCount(address.DeviceType, suffix, valueList.Count);
        KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix, valueList.Count);
        string payload = BuildValuePayload(valueList, suffix);
        await ExpectOkCoreAsync(
            $"WRS {(address with { Suffix = suffix }).ToText()} {valueList.Count} {payload}",
            cancellationToken).ConfigureAwait(false);
    }

    public Task RegisterMonitorBitsAsync(
        IEnumerable<string> devices,
        CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            var targets = devices.ToList();
            if (targets.Count == 0) throw new HostLinkProtocolError("At least one device is required");
            if (targets.Count > 120) throw new HostLinkProtocolError("Maximum 120 devices can be registered");

            var command = new StringBuilder("MBS");
            foreach (var device in targets)
            {
                var address = KvHostLinkDevice.RequireBaseDevice(device);
                KvHostLinkDevice.ValidateDeviceType("MBS", address.DeviceType, KvHostLinkModels.MbsDeviceTypes);
                command.Append(' ');
                command.Append(address.ToText());
            }
            await ExpectOkCoreAsync(command.ToString(), cancellationToken).ConfigureAwait(false);
            _monitorBitCount = targets.Count;
        }, cancellationToken);

    public Task RegisterMonitorWordsAsync(
        IEnumerable<KvMonitorWordTarget> devices,
        CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            var targets = devices.ToList();
            if (targets.Count == 0) throw new HostLinkProtocolError("At least one device is required");
            if (targets.Count > 120) throw new HostLinkProtocolError("Maximum 120 devices can be registered");

            var command = new StringBuilder("MWS");
            var formats = new List<string>(targets.Count);
            foreach (var target in targets)
            {
                ArgumentNullException.ThrowIfNull(target);
                var address = KvHostLinkDevice.RequireBaseDevice(target.Device);
                KvHostLinkDevice.ValidateDeviceType("MWS", address.DeviceType, KvHostLinkModels.MwsDeviceTypes);
                string suffix = KvHostLinkDevice.RequireExplicitFormat(address, target.DataFormat);
                KvHostLinkDevice.ValidateDeviceSpan(address.DeviceType, address.Number, suffix);
                command.Append(' ');
                command.Append((address with { Suffix = suffix }).ToText());
                formats.Add(suffix);
            }
            await ExpectOkCoreAsync(command.ToString(), cancellationToken).ConfigureAwait(false);
            _monitorWordFormats = formats.ToArray();
        }, cancellationToken);

    public Task<string[]> ReadMonitorBitsAsync(CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            if (_monitorBitCount == 0)
                throw new HostLinkProtocolError("Monitor bits must be registered before reading them.");
            string response = await SendSemanticCoreAsync("MBR", cancellationToken).ConfigureAwait(false);
            string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
            try
            {
                KvHostLinkProtocol.ValidateResponseTokens(tokens, "", _monitorBitCount);
            }
            catch (HostLinkProtocolError)
            {
                InvalidateProtocolState();
                throw;
            }
            return tokens;
        }, cancellationToken);

    public Task<string[]> ReadMonitorWordsAsync(CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            if (_monitorWordFormats.Length == 0)
                throw new HostLinkProtocolError("Monitor words must be registered before reading them.");
            string response = await SendSemanticCoreAsync("MWR", cancellationToken).ConfigureAwait(false);
            string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
            try
            {
                if (tokens.Length != _monitorWordFormats.Length)
                    throw new HostLinkProtocolError(
                        $"Response contained {tokens.Length} values; expected {_monitorWordFormats.Length}.");
                for (int index = 0; index < tokens.Length; index++)
                    KvHostLinkProtocol.ValidateResponseTokens([tokens[index]], _monitorWordFormats[index], 1);
            }
            catch (HostLinkProtocolError)
            {
                InvalidateProtocolState();
                throw;
            }
            return tokens;
        }, cancellationToken);

    /// <summary>Consecutively force-sets up to 16 bit devices starting at <paramref name="device"/> (STS command).</summary>
    public async Task ForcedSetConsecutiveAsync(
        string device, int count, CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(count), "count must be 1-16.");
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("STS", addr.DeviceType, KvHostLinkModels.ForceConsecutiveDeviceTypes);
        await ExpectOkAsync($"STS {addr.ToText()} {count}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Consecutively force-resets up to 16 bit devices starting at <paramref name="device"/> (RSS command).</summary>
    public async Task ForcedResetConsecutiveAsync(
        string device, int count, CancellationToken cancellationToken = default)
    {
        if (count is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(count), "count must be 1-16.");
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("RSS", addr.DeviceType, KvHostLinkModels.ForceConsecutiveDeviceTypes);
        await ExpectOkAsync($"RSS {addr.ToText()} {count}", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Reads consecutive devices using the legacy RDE command.
    /// Prefer <see cref="ReadConsecutiveAsync(string, int, string, CancellationToken)"/> on current models.
    /// </summary>
    public Task<string[]> ReadConsecutiveLegacyAsync(
        string device, int count, string dataFormat, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            var addr = KvHostLinkDevice.RequireBaseDevice(device);
            string effectiveFormat = KvHostLinkDevice.RequireExplicitFormat(addr, dataFormat);
            KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, effectiveFormat, count);
            KvHostLinkDevice.ValidateDeviceSpan(addr.DeviceType, addr.Number, effectiveFormat, count);
            var target = addr with { Suffix = effectiveFormat };
            string response = await SendSemanticCoreAsync($"RDE {target.ToText()} {count}", cancellationToken)
                .ConfigureAwait(false);
            string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
            try
            {
                KvHostLinkProtocol.ValidateResponseTokens(tokens, effectiveFormat, count);
            }
            catch (HostLinkProtocolError)
            {
                InvalidateProtocolState();
                throw;
            }
            return tokens;
        }, cancellationToken);

    /// <summary>
    /// Writes consecutive devices using the legacy WRE command.
    /// Prefer <see cref="WriteConsecutiveAsync{T}(string, IEnumerable{T}, string, CancellationToken)"/> on current models.
    /// </summary>
    public async Task WriteConsecutiveLegacyAsync<T>(
        string device, IEnumerable<T> values, string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        string effectiveFormat = KvHostLinkDevice.RequireExplicitFormat(addr, dataFormat);
        KvHostLinkDevice.ValidateDeviceType("WRE", addr.DeviceType, KvHostLinkModels.WrDeviceTypes);
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
        string device, T value, string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("WS", addr.DeviceType, KvHostLinkModels.WsDeviceTypes);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(addr, dataFormat);
        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, suffix, 1);
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
        string device, IEnumerable<T> values, string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");
        var addr = KvHostLinkDevice.RequireBaseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("WSS", addr.DeviceType, KvHostLinkModels.WsDeviceTypes);
        string suffix = KvHostLinkDevice.RequireExplicitFormat(addr, dataFormat);
        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, suffix, valList.Count);
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
    /// <param name="dataFormat">Required data format suffix, e.g. ".U" or ".S".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public Task<string[]> ReadExpansionUnitBufferAsync(
        int unitNo, int address, int count,
        string dataFormat,
        CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            if (unitNo is < 0 or > 48)
                throw new ArgumentOutOfRangeException(nameof(unitNo), "unitNo must be 0-48.");
            if (address is < 0 or > 59999)
                throw new ArgumentOutOfRangeException(nameof(address), "address must be 0-59999.");

            if (string.IsNullOrWhiteSpace(dataFormat))
                throw new HostLinkProtocolError("dataFormat is required.");
            string suffix = KvHostLinkDevice.NormalizeSuffix(dataFormat);
            KvHostLinkDevice.ValidateExpansionBufferCount(suffix, count);
            KvHostLinkDevice.ValidateExpansionBufferSpan(address, suffix, count);

            string cmd = $"URD {unitNo:D2} {address}{suffix} {count}";
            string response = await SendSemanticCoreAsync(cmd, cancellationToken).ConfigureAwait(false);
            string[] tokens = KvHostLinkProtocol.SplitDataTokens(response);
            try
            {
                KvHostLinkProtocol.ValidateResponseTokens(tokens, suffix, count);
            }
            catch (HostLinkProtocolError)
            {
                InvalidateProtocolState();
                throw;
            }
            return tokens;
        }, cancellationToken);

    /// <summary>
    /// Writes buffer memory to an expansion unit (UWR command).
    /// </summary>
    /// <param name="unitNo">Unit number (0–48).</param>
    /// <param name="address">Buffer address (0–59999).</param>
    /// <param name="values">Values to write.</param>
    /// <param name="dataFormat">Required data format suffix, e.g. ".U" or ".S".</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task WriteExpansionUnitBufferAsync<T>(
        int unitNo, int address, IEnumerable<T> values,
        string dataFormat,
        CancellationToken cancellationToken = default) where T : IFormattable
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolError("values must not be empty");
        if (unitNo is < 0 or > 48)
            throw new ArgumentOutOfRangeException(nameof(unitNo), "unitNo must be 0-48.");
        if (address is < 0 or > 59999)
            throw new ArgumentOutOfRangeException(nameof(address), "address must be 0-59999.");

        if (string.IsNullOrWhiteSpace(dataFormat))
            throw new HostLinkProtocolError("dataFormat is required.");
        string suffix = KvHostLinkDevice.NormalizeSuffix(dataFormat);
        KvHostLinkDevice.ValidateExpansionBufferCount(suffix, valList.Count);
        KvHostLinkDevice.ValidateExpansionBufferSpan(address, suffix, valList.Count);

        string payload = BuildValuePayload(valList, suffix);
        string cmd = $"UWR {unitNo:D2} {address}{suffix} {valList.Count} {payload}";
        await ExpectOkAsync(cmd, cancellationToken).ConfigureAwait(false);
    }

    public Task<string> ReadCommentsAsync(string device, CancellationToken cancellationToken = default)
        => ExecuteExclusiveAsync(async () =>
        {
            var address = KvHostLinkDevice.RequireBaseDevice(device);
            KvHostLinkDevice.ValidateDeviceType("RDC", address.DeviceType, KvHostLinkModels.RdcDeviceTypes);
            byte[] response = await SendRawCoreAsync($"RDC {address.ToText()}", cancellationToken).ConfigureAwait(false);
            try
            {
                return KvHostLinkProtocol.DecodeCommentResponse(response);
            }
            catch (HostLinkProtocolError)
            {
                CloseTransport();
                throw;
            }
        }, cancellationToken);

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

    private static string FormatValue<T>(T value, string dataFormat) where T : IFormattable
    {
        object boxed = value;
        if (boxed is not byte and not sbyte and not short and not ushort and not int and not uint and not long and not ulong)
            throw new HostLinkProtocolError("Host Link numeric writes require an integral CLR value.");

        if (boxed is ulong unsignedValue)
        {
            if (dataFormat is ".S" or ".L" || unsignedValue > uint.MaxValue)
                throw new HostLinkProtocolError($"Value {unsignedValue} is out of range for data format '{dataFormat}'.");
            return FormatUnsigned(unsignedValue, dataFormat);
        }

        if (boxed is uint uintValue)
            return FormatUnsigned(uintValue, dataFormat);
        if (boxed is ushort ushortValue)
            return FormatUnsigned(ushortValue, dataFormat);
        if (boxed is byte byteValue)
            return FormatUnsigned(byteValue, dataFormat);

        long signedValue = Convert.ToInt64(boxed, CultureInfo.InvariantCulture);
        return FormatSigned(signedValue, dataFormat);
    }

    private static string FormatUnsigned(ulong value, string dataFormat)
        => dataFormat switch
        {
            "" when value <= 1 => value.ToString(CultureInfo.InvariantCulture),
            ".U" when value <= ushort.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".D" when value <= uint.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".H" when value <= ushort.MaxValue => value.ToString("X", CultureInfo.InvariantCulture),
            ".S" when value <= (ulong)short.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".L" when value <= int.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            _ => throw new HostLinkProtocolError($"Value {value} is out of range for data format '{dataFormat}'."),
        };

    private static string FormatSigned(long value, string dataFormat)
        => dataFormat switch
        {
            "" when value is 0 or 1 => value.ToString(CultureInfo.InvariantCulture),
            ".U" when value is >= ushort.MinValue and <= ushort.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".S" when value is >= short.MinValue and <= short.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".D" when value is >= uint.MinValue and <= uint.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".L" when value is >= int.MinValue and <= int.MaxValue => value.ToString(CultureInfo.InvariantCulture),
            ".H" when value is >= ushort.MinValue and <= ushort.MaxValue => value.ToString("X", CultureInfo.InvariantCulture),
            _ => throw new HostLinkProtocolError($"Value {value} is out of range for data format '{dataFormat}'."),
        };
}
