using System.Text;
using System.Text.RegularExpressions;

namespace PlcComm.KvHostLink;

internal static class KvHostLinkProtocol
{
    private static readonly Regex ErrorRegex = new(@"^E[0-9]$", RegexOptions.Compiled);
    private static readonly byte[] Cr = { (byte)'\r' };
    private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding ShiftJisStrict;

    static KvHostLinkProtocol()
    {
        // Shift_JIS decoding needs code pages on .NET (Core).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ShiftJisStrict = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }

    public static byte[] BuildFrame(string body)
    {
        if (string.IsNullOrEmpty(body))
            throw new HostLinkProtocolError("Command body must not be empty");
        if (body.IndexOfAny(['\r', '\n']) >= 0)
            throw new HostLinkProtocolError("Command body must not contain CR or LF");

        byte[] payload;
        try
        {
            payload = Encoding.GetEncoding(
                Encoding.ASCII.CodePage,
                EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback).GetBytes(body);
        }
        catch (EncoderFallbackException ex)
        {
            throw new HostLinkProtocolError("Command body must contain ASCII characters only", ex);
        }

        var result = new byte[payload.Length + Cr.Length];
        Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
        Buffer.BlockCopy(Cr, 0, result, payload.Length, Cr.Length);
        return result;
    }

    public static string DecodeResponse(byte[] raw)
    {
        if (raw == null || raw.Length == 0)
            throw new HostLinkProtocolError("Empty response");

        // Find the trimmed length first to avoid a second string allocation from TrimEnd.
        int len = raw.Length;
        while (len > 0 && (raw[len - 1] == '\r' || raw[len - 1] == '\n'))
            len--;

        if (len == 0)
            throw new HostLinkProtocolError("Malformed response frame");

        bool isAscii = true;
        for (int i = 0; i < len; i++)
        {
            if (raw[i] > 0x7F)
            {
                isAscii = false;
                break;
            }
        }
        if (isAscii)
            return Encoding.ASCII.GetString(raw, 0, len);

        try
        {
            return Utf8Strict.GetString(raw, 0, len);
        }
        catch (DecoderFallbackException)
        {
        }
        try
        {
            return ShiftJisStrict.GetString(raw, 0, len);
        }
        catch (DecoderFallbackException ex)
        {
            throw new HostLinkProtocolError("Response could not be decoded as UTF-8 or Shift_JIS", ex);
        }
    }

    public static string EnsureSuccess(string responseText)
    {
        if (ErrorRegex.IsMatch(responseText))
        {
            throw new HostLinkError($"PLC returned error: {responseText}", responseText, responseText);
        }
        return responseText;
    }

    public static string DecodeSemanticResponse(byte[] body)
    {
        if (body.Length == 0)
            throw new HostLinkProtocolError("Empty response");
        if (body.Any(static value => value > 0x7F))
            throw new HostLinkProtocolError("Semantic response must contain ASCII characters only");
        return EnsureSuccess(Encoding.ASCII.GetString(body));
    }

    public static string DecodeCommentResponse(byte[] body)
    {
        string responseText;
        if (body.Length > 0 && body.All(static value => value <= 0x7F))
            responseText = Encoding.ASCII.GetString(body);
        else
            responseText = DecodeResponse(body);

        EnsureSuccess(responseText);
        int length = body.Length;
        while (length > 0 && body[length - 1] == 0x20)
            length--;
        if (length == 0)
            return string.Empty;
        return DecodeResponse(body.AsSpan(0, length).ToArray());
    }

    public static byte[] ExtractBody(byte[] frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        int length = frame.Length;
        if (length == 0 || (frame[length - 1] != '\r' && frame[length - 1] != '\n'))
            throw new HostLinkProtocolError("Response frame is missing a CR or LF terminator");
        while (length > 0 && (frame[length - 1] == '\r' || frame[length - 1] == '\n'))
            length--;
        return frame.AsSpan(0, length).ToArray();
    }

    public static string[] SplitDataTokens(string responseText)
    {
        return responseText.Split([' ', ','], StringSplitOptions.RemoveEmptyEntries);
    }

    public static void ValidateResponseTokens(IReadOnlyList<string> tokens, string dataFormat, int expectedCount)
    {
        if (tokens.Count != expectedCount)
            throw new HostLinkProtocolError($"Response contained {tokens.Count} values; expected {expectedCount}.");

        foreach (string token in tokens)
        {
            bool valid = dataFormat switch
            {
                "" => token is "0" or "1" or "ON" or "OFF",
                ".U" => ushort.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
                ".S" => short.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
                ".D" => uint.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
                ".L" => int.TryParse(token, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out _),
                ".H" => ushort.TryParse(token, System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out _),
                _ => false,
            };
            if (!valid)
                throw new HostLinkProtocolError($"Response value '{token}' is invalid for data format '{dataFormat}'.");
        }
    }
}
