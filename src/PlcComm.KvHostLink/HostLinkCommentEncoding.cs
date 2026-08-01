namespace PlcComm.KvHostLink;

/// <summary>Explicit text encoding used to decode an RDC device-comment payload.</summary>
public enum HostLinkCommentEncoding
{
    /// <summary>Strict UTF-8 without malformed-byte replacement.</summary>
    Utf8 = 0,

    /// <summary>
    /// Strict Windows code page 932 (CP932/Windows-31J), used as the compatibility
    /// selection for KEYENCE material that describes text as Shift_JIS. Strict
    /// decoding accepts mapped Windows extension pairs but rejects forbidden
    /// singleton bytes, incomplete sequences, and unassigned pairs.
    /// </summary>
    Cp932 = 1,
}
