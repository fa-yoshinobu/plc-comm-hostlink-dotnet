using System.Text;
using System.Text.RegularExpressions;

namespace PlcComm.KvHostLink;

internal static class KvHostLinkProtocol
{
    private static readonly Regex ErrorRegex = new(@"^E[0-9]$", RegexOptions.Compiled);
    private static readonly byte[] Cr = { (byte)'\r' };
    private static readonly Encoding Utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static readonly Encoding Cp932;
    private static readonly Encoding Cp932Strict;

    static KvHostLinkProtocol()
    {
        // CP932/Windows-31J decoding needs code pages on .NET (Core).
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        Cp932 = Encoding.GetEncoding(932);
        Cp932Strict = Encoding.GetEncoding(932, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
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

    public static void ValidateCommentEncoding(HostLinkCommentEncoding encoding)
    {
        if (encoding is not HostLinkCommentEncoding.Utf8 and not HostLinkCommentEncoding.Cp932)
        {
            throw new ArgumentOutOfRangeException(
                nameof(encoding),
                encoding,
                "Comment encoding must be Utf8 or Cp932.");
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

    public static void EnsureCommentSuccess(byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (body.Length == 2 && body[0] == 'E' && body[1] is >= (byte)'0' and <= (byte)'9')
        {
            string errorCode = Encoding.ASCII.GetString(body);
            throw new HostLinkError($"PLC returned error: {errorCode}", errorCode, errorCode);
        }
    }

    public static string DecodeCommentResponse(byte[] body, HostLinkCommentEncoding encoding)
    {
        ArgumentNullException.ThrowIfNull(body);
        ValidateCommentEncoding(encoding);
        EnsureCommentSuccess(body);

        int length = body.Length;
        while (length > 0 && body[length - 1] == 0x20)
            length--;
        if (length == 0)
            return string.Empty;

        try
        {
            return encoding switch
            {
                HostLinkCommentEncoding.Utf8 => Utf8Strict.GetString(body, 0, length),
                HostLinkCommentEncoding.Cp932 => DecodeCp932(body, length),
                _ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, null),
            };
        }
        catch (DecoderFallbackException ex)
        {
            throw new HostLinkProtocolError(
                $"RDC comment payload is not valid {encoding} text.",
                ex);
        }
    }

    private static string DecodeCp932(byte[] body, int length)
    {
        for (int index = 0; index < length; index++)
        {
            byte first = body[index];
            if (first <= 0x7F || first is >= 0xA1 and <= 0xDF)
                continue;

            if (first is not (>= 0x81 and <= 0x9F) and not (>= 0xE0 and <= 0xFC) ||
                index + 1 >= length)
            {
                throw new DecoderFallbackException(
                    $"Invalid CP932 byte 0x{first:X2} at offset {index}.");
            }

            byte second = body[++index];
            if (!IsStrictCp932Pair(first, second) && !IsCp932WindowsExtensionPair(first, second))
            {
                throw new DecoderFallbackException(
                    $"Invalid CP932 byte pair 0x{first:X2}{second:X2} at offset {index - 1}.");
            }
        }

        // The default .NET CP932 table maps the Windows extension pairs that
        // ExceptionFallback rejects even though Python cp932 and WHATWG
        // Shift_JIS accept them. Prevalidation above prevents replacement text.
        return Cp932.GetString(body, 0, length);
    }

    private static bool IsStrictCp932Pair(byte first, byte second)
    {
        Span<byte> pair = stackalloc byte[2] { first, second };
        try
        {
            _ = Cp932Strict.GetCharCount(pair);
            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }

    private static bool IsCp932WindowsExtensionPair(byte first, byte second)
        => first switch
        {
            0x87 => second is >= 0x90 and <= 0x92 or
                >= 0x95 and <= 0x97 or
                >= 0x9A and <= 0x9C,
            0xED => second is >= 0x40 and <= 0x7E or >= 0x80 and <= 0xFC,
            0xEE => second is >= 0x40 and <= 0x7E or
                >= 0x80 and <= 0xEC or
                >= 0xEF and <= 0xFC,
            0xFA => second is >= 0x4A and <= 0x54 or >= 0x58 and <= 0x5B,
            _ => false,
        };

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

    public static void ValidateResponseTokens(
        IReadOnlyList<string> tokens,
        string dataFormat,
        int expectedCount,
        bool timerCounterComposite = false)
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

        if (timerCounterComposite && tokens[0] is not ("0" or "1"))
            throw new HostLinkProtocolError(
                $"Timer/counter response status '{tokens[0]}' is invalid; expected 0 or 1.");
    }
}
