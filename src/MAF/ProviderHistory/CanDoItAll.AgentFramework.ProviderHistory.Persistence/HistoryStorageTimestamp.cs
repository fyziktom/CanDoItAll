namespace CanDoItAll.AgentFramework.ProviderHistory.Persistence;

public static class HistoryStorageTimestamp {
    private static readonly long EpochTicks = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero).Ticks;

    public static DateTimeOffset Normalize(DateTimeOffset value) {
        if (value == DateTimeOffset.MinValue || value == DateTimeOffset.MaxValue) {
            return value.ToUniversalTime();
        }
        var ticks = (value.UtcTicks - EpochTicks) / TimeSpan.TicksPerMicrosecond * TimeSpan.TicksPerMicrosecond;
        return new(EpochTicks + ticks, TimeSpan.Zero);
    }

    public static DateTimeOffset? Normalize(DateTimeOffset? value) => value is { } timestamp ? Normalize(timestamp) : null;
}
