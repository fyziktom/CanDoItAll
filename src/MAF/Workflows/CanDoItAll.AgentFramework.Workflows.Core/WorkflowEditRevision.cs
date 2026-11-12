namespace CanDoItAll.AgentFramework.Core;

public static class WorkflowEditRevision {
    public static DateTimeOffset Next(DateTimeOffset? original) => Next(original, DateTimeOffset.UtcNow);

    public static DateTimeOffset Next(DateTimeOffset? original, DateTimeOffset now) {
        now = now.ToUniversalTime();
        var next = new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMicrosecond, TimeSpan.Zero);
        return original is { } previous && next <= previous ? previous.AddTicks(TimeSpan.TicksPerMicrosecond) : next;
    }
}
