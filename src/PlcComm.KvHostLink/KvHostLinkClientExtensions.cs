using System.Globalization;

namespace PlcComm.KvHostLink;

/// <summary>
/// Typed read/write helpers for <see cref="KvHostLinkClient"/>.
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
            _   => ushort.Parse(raw, CultureInfo.InvariantCulture),
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
}
