namespace CanDoItAll.Modules.Processes;

public sealed class ProcessObservationCacheOptions
{
    public const string SectionName = "Processes:ObservationCache";

    public long SizeLimit { get; set; } = 4096;

    public int ActiveDashboardAbsoluteExpirationSeconds { get; set; } = 3;

    public int InactiveDashboardAbsoluteExpirationSeconds { get; set; } = 20;

    public int RunDetailsAbsoluteExpirationSeconds { get; set; } = 10;

    public int TimelineAbsoluteExpirationSeconds { get; set; } = 15;

    public int SlidingExpirationSeconds { get; set; } = 2;

    public int DashboardEntrySize { get; set; } = 4;

    public int RunDetailsEntrySize { get; set; } = 8;

    public int TimelineEntrySize { get; set; } = 4;

    internal TimeSpan GetActiveDashboardAbsoluteExpiration()
    {
        return TimeSpan.FromSeconds(Math.Max(1, ActiveDashboardAbsoluteExpirationSeconds));
    }

    internal TimeSpan GetInactiveDashboardAbsoluteExpiration()
    {
        return TimeSpan.FromSeconds(Math.Max(1, InactiveDashboardAbsoluteExpirationSeconds));
    }

    internal TimeSpan GetRunDetailsAbsoluteExpiration()
    {
        return TimeSpan.FromSeconds(Math.Max(1, RunDetailsAbsoluteExpirationSeconds));
    }

    internal TimeSpan GetTimelineAbsoluteExpiration()
    {
        return TimeSpan.FromSeconds(Math.Max(1, TimelineAbsoluteExpirationSeconds));
    }

    internal TimeSpan GetSlidingExpiration()
    {
        return TimeSpan.FromSeconds(Math.Max(1, SlidingExpirationSeconds));
    }
}
