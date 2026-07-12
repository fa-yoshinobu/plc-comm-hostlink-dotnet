namespace PlcComm.KvHostLink;

internal static class KvHostLinkTimeout
{
    internal static readonly TimeSpan Minimum = TimeSpan.FromMilliseconds(1);
    internal static readonly TimeSpan Maximum = TimeSpan.FromMilliseconds(int.MaxValue);

    internal static TimeSpan Validate(TimeSpan value, string parameterName)
    {
        if (value < Minimum || value > Maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"Timeout must be in the range {Minimum.TotalMilliseconds:0}..{Maximum.TotalMilliseconds:0} milliseconds.");
        }

        return value;
    }
}
