namespace CanDoItAll.Modules.LlmChats.Application;

public sealed record LlmChatStreamingOptions
{
    public int MinimumChunkBytes { get; init; } = 256;

    public int MaximumChunkBytes { get; init; } = 8 * 1024;

    public TimeSpan MaximumCoalescingDelay { get; init; } = TimeSpan.FromMilliseconds(150);

    public int MaximumResponseCharacters { get; init; } = 1_000_000;

    public int MaximumResponseBytes { get; init; } = 4_000_000;

    public int MaximumDeltaEvents { get; init; } = 4_000;

    public TimeSpan EventRetention { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);

    public int CleanupBatchSize { get; init; } = 500;

    public int MaximumReplayPageSize { get; init; } = 500;

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MinimumChunkBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumChunkBytes, MinimumChunkBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumCoalescingDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumResponseCharacters, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumResponseBytes, MaximumChunkBytes);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumDeltaEvents, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(EventRetention, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(CleanupInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(CleanupInterval, EventRetention);
        ArgumentOutOfRangeException.ThrowIfLessThan(CleanupBatchSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(CleanupBatchSize, 10_000);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumReplayPageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaximumReplayPageSize, 5_000);
    }
}
