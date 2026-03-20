using System.Net.Sockets;
using System.Text;

namespace PlcComm.KvHostLink;

/// <summary>
/// A low-level Host Link (Upper Link) client for KEYENCE KV series PLCs.
/// </summary>
public sealed class KvHostLinkClient : IDisposable
{
    private readonly string _host;
    private readonly int _port;
    private readonly HostLinkTransportMode _transportMode;
    private TcpClient? _tcp;
    private NetworkStream? _tcpStream;
    private UdpClient? _udp;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly List<byte> _rxBuffer = new();

    public KvHostLinkClient(string host, int port = 8501, HostLinkTransportMode transportMode = HostLinkTransportMode.Tcp)
    {
        _host = host;
        _port = port;
        _transportMode = transportMode;
    }

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(3);
    public bool AppendLfOnSend { get; set; } = false;

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
            _rxBuffer.Clear();
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
            _rxBuffer.Clear();
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose() => Close();

    public async Task<string> SendRawAsync(string body, CancellationToken cancellationToken = default)
    {
        await OpenAsync(cancellationToken).ConfigureAwait(false);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var frame = KvHostLinkProtocol.BuildFrame(body, AppendLfOnSend);
            if (_transportMode == HostLinkTransportMode.Tcp)
            {
                await _tcpStream!.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
                var responseRaw = await RecvTcpLineAsync(cancellationToken).ConfigureAwait(false);
                return KvHostLinkProtocol.EnsureSuccess(KvHostLinkProtocol.DecodeResponse(responseRaw));
            }
            else
            {
                await _udp!.SendAsync(frame, cancellationToken).ConfigureAwait(false);
                var result = await _udp.ReceiveAsync(cancellationToken).ConfigureAwait(false);
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
        var buffer = new byte[8192];
        while (true)
        {
            int crIdx = _rxBuffer.IndexOf((byte)'\r');
            int lfIdx = _rxBuffer.IndexOf((byte)'\n');

            int foundIdx = -1;
            if (crIdx >= 0 && lfIdx >= 0) foundIdx = Math.Min(crIdx, lfIdx);
            else if (crIdx >= 0) foundIdx = crIdx;
            else if (lfIdx >= 0) foundIdx = lfIdx;

            if (foundIdx >= 0)
            {
                var line = _rxBuffer.Take(foundIdx).ToArray();
                int skip = foundIdx;
                while (skip < _rxBuffer.Count && (_rxBuffer[skip] == (byte)'\r' || _rxBuffer[skip] == (byte)'\n'))
                {
                    skip++;
                }
                _rxBuffer.RemoveRange(0, skip);
                return line;
            }

            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(Timeout);
            int read = await _tcpStream!.ReadAsync(buffer, linked.Token).ConfigureAwait(false);
            if (read == 0)
            {
                if (_rxBuffer.Count > 0)
                {
                    var line = _rxBuffer.ToArray();
                    _rxBuffer.Clear();
                    return line;
                }
                throw new HostLinkConnectionException("Connection closed by PLC");
            }
            _rxBuffer.AddRange(buffer.Take(read));
        }
    }

    private async Task ExpectOkAsync(string body, CancellationToken cancellationToken = default)
    {
        var response = await SendRawAsync(body, cancellationToken).ConfigureAwait(false);
        if (response != "OK")
        {
            throw new HostLinkProtocolException($"Expected 'OK' but received '{response}' for command '{body}'");
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
        return (KvPlcMode)int.Parse(response);
    }

    public async Task SetTimeAsync(DateTime? value = null, CancellationToken cancellationToken = default)
    {
        var dt = value ?? DateTime.Now;
        int year = dt.Year % 100;
        int week = ((int)dt.DayOfWeek + 1) % 7; // Matching Python logic: (now.weekday() + 1) % 7
        
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

        var target = addr with { Suffix = suffix };
        string response = await SendRawAsync($"RD {target.ToText()}", cancellationToken).ConfigureAwait(false);
        return KvHostLinkProtocol.SplitDataTokens(response);
    }

    public async Task<string[]> ReadConsecutiveAsync(string device, int count, string? dataFormat = null, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        string suffix = dataFormat != null ? KvHostLinkDevice.NormalizeSuffix(dataFormat) : addr.Suffix;
        string effectiveFormat = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, suffix);
        
        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, effectiveFormat, count);
        
        var target = addr with { Suffix = suffix };
        string response = await SendRawAsync($"RDS {target.ToText()} {count}", cancellationToken).ConfigureAwait(false);
        return KvHostLinkProtocol.SplitDataTokens(response);
    }

    public async Task WriteAsync(string device, object value, string? dataFormat = null, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        string suffix = dataFormat != null ? KvHostLinkDevice.NormalizeSuffix(dataFormat) : addr.Suffix;
        if (string.IsNullOrEmpty(suffix))
            suffix = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, "");

        var target = addr with { Suffix = suffix };
        string valStr = FormatValue(value, suffix);
        await ExpectOkAsync($"WR {target.ToText()} {valStr}", cancellationToken).ConfigureAwait(false);
    }

    public async Task WriteConsecutiveAsync(string device, IEnumerable<object> values, string? dataFormat = null, CancellationToken cancellationToken = default)
    {
        var valList = values.ToList();
        if (valList.Count == 0) throw new HostLinkProtocolException("values must not be empty");

        var addr = KvHostLinkDevice.ParseDevice(device);
        string suffix = dataFormat != null ? KvHostLinkDevice.NormalizeSuffix(dataFormat) : addr.Suffix;
        string effectiveFormat = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, suffix);

        KvHostLinkDevice.ValidateDeviceCount(addr.DeviceType, effectiveFormat, valList.Count);

        var target = addr with { Suffix = suffix };
        string payload = string.Join(" ", valList.Select(v => FormatValue(v, suffix)));
        await ExpectOkAsync($"WRS {target.ToText()} {valList.Count} {payload}", cancellationToken).ConfigureAwait(false);
    }

    public async Task RegisterMonitorBitsAsync(IEnumerable<string> devices, CancellationToken cancellationToken = default)
    {
        var targets = devices.ToList();
        if (targets.Count == 0) throw new HostLinkProtocolException("At least one device is required");
        if (targets.Count > 120) throw new HostLinkProtocolException("Maximum 120 devices can be registered");

        var tokens = new List<string>();
        foreach (var device in targets)
        {
            var addr = KvHostLinkDevice.ParseDevice(device);
            KvHostLinkDevice.ValidateDeviceType("MBS", addr.DeviceType, KvHostLinkModels.MbsDeviceTypes);
            tokens.Add((addr with { Suffix = "" }).ToText());
        }
        await ExpectOkAsync($"MBS {string.Join(" ", tokens)}", cancellationToken).ConfigureAwait(false);
    }

    public async Task RegisterMonitorWordsAsync(IEnumerable<string> devices, CancellationToken cancellationToken = default)
    {
        var targets = devices.ToList();
        if (targets.Count == 0) throw new HostLinkProtocolException("At least one device is required");
        if (targets.Count > 120) throw new HostLinkProtocolException("Maximum 120 devices can be registered");

        var tokens = new List<string>();
        foreach (var device in targets)
        {
            var addr = KvHostLinkDevice.ParseDevice(device);
            KvHostLinkDevice.ValidateDeviceType("MWS", addr.DeviceType, KvHostLinkModels.MwsDeviceTypes);
            string suffix = KvHostLinkDevice.ResolveEffectiveFormat(addr.DeviceType, addr.Suffix);
            tokens.Add((addr with { Suffix = suffix }).ToText());
        }
        await ExpectOkAsync($"MWS {string.Join(" ", tokens)}", cancellationToken).ConfigureAwait(false);
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

    public async Task<string> ReadCommentsAsync(string device, bool stripPadding = true, CancellationToken cancellationToken = default)
    {
        var addr = KvHostLinkDevice.ParseDevice(device);
        KvHostLinkDevice.ValidateDeviceType("RDC", addr.DeviceType, KvHostLinkModels.RdcDeviceTypes);
        string response = await SendRawAsync($"RDC {(addr with { Suffix = "" }).ToText()}", cancellationToken).ConfigureAwait(false);
        return stripPadding ? response.TrimEnd(' ') : response;
    }

    private static string FormatValue(object value, string dataFormat)
    {
        if (value is int i && dataFormat == ".H")
        {
            return (i & 0xFFFF).ToString("X");
        }
        return value.ToString()?.Trim() ?? "";
    }
}
