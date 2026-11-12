namespace CanDoItAll.Modules.CrmHr;

internal static class CrmHrEditVersions {
    public static DateTimeOffset Next(DateTimeOffset current, DateTimeOffset now) {
        var normalizedCurrent = Normalize(current);
        var normalizedNow = Normalize(now);
        return normalizedNow > normalizedCurrent
            ? normalizedNow
            : normalizedCurrent.AddTicks(TimeSpan.TicksPerMicrosecond);
    }

    public static DateTimeOffset Normalize(DateTimeOffset value) {
        var ticks = value.UtcTicks;
        return new(ticks - ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
    }
}
