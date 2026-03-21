using System.Text;
using System.Text.RegularExpressions;

namespace PlcComm.KvHostLink;

internal static class KvHostLinkProtocol
{
    private static readonly Regex ErrorRegex = new(@"^E[0-9]$");
    private static readonly byte[] Cr = { (byte)'\r' };
    private static readonly byte[] CrLf = { (byte)'\r', (byte)'\n' };

    public static byte[] BuildFrame(string body, bool appendLf = false)
    {
        var payload = Encoding.ASCII.GetBytes(body.Trim());
        var terminator = appendLf ? CrLf : Cr;
        var result = new byte[payload.Length + terminator.Length];
        Buffer.BlockCopy(payload, 0, result, 0, payload.Length);
        Buffer.BlockCopy(terminator, 0, result, payload.Length, terminator.Length);
        return result;
    }

    public static string DecodeResponse(byte[] raw)
    {
        if (raw == null || raw.Length == 0)
            throw new HostLinkProtocolError("Empty response");

        string text;
        try
        {
            text = Encoding.ASCII.GetString(raw);
        }
        catch (DecoderFallbackException ex)
        {
            throw new HostLinkProtocolError("Response is not ASCII", ex);
        }

        text = text.TrimEnd('\r', '\n');
        if (string.IsNullOrEmpty(text))
            throw new HostLinkProtocolError("Malformed response frame");

        return text;
    }

    public static string EnsureSuccess(string responseText)
    {
        if (ErrorRegex.IsMatch(responseText))
        {
            throw new HostLinkError($"PLC returned error: {responseText}", responseText, responseText);
        }
        return responseText;
    }

    public static string[] SplitDataTokens(string responseText)
    {
        return responseText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }
}
