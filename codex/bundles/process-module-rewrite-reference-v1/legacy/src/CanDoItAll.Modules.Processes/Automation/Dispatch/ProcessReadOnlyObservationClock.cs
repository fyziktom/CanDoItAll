namespace CanDoItAll.Modules.Processes;

internal static class ProcessReadOnlyObservationClock
{
    public static DateTimeOffset ObservedAt(DateTimeOffset requestedAt)
    {
        return requestedAt;
    }
}
