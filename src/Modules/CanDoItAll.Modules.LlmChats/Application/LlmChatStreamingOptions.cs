using CanDoItAll.AgentFramework.Llm.Abstractions;

namespace CanDoItAll.Modules.LlmChats.Application;

public sealed record LlmChatStreamingOptions
{
    public const string SectionName = "LlmChats:Streaming";

    public int MinimumChunkBytes { get; init; } = 256;

    public int MaximumChunkBytes { get; init; } = LlmChatStreamingLimits.MaximumPersistedEventTextBytes;

    public TimeSpan MaximumCoalescingDelay { get; init; } = TimeSpan.FromMilliseconds(150);

    public int MaximumResponseCharacters { get; init; } = LlmMessage.MaximumTextLength;

    public int MaximumResponseBytes { get; init; } = LlmChatStreamingLimits.MaximumAssistantMessageUtf8Bytes;

    public int MaximumDeltaEvents { get; init; } = 4_000;

    public TimeSpan EventRetention { get; init; } = TimeSpan.FromDays(7);

    public TimeSpan CleanupInterval { get; init; } = TimeSpan.FromHours(1);

    public int CleanupBatchSize { get; init; } = 500;

    public int MaximumReplayPageSize { get; init; } = 500;

    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(MinimumChunkBytes, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumChunkBytes, MinimumChunkBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            MaximumChunkBytes,
            LlmChatStreamingLimits.MaximumPersistedEventTextBytes);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(MaximumCoalescingDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumResponseCharacters, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(MaximumResponseCharacters, LlmMessage.MaximumTextLength);
        ArgumentOutOfRangeException.ThrowIfLessThan(MaximumResponseBytes, MaximumChunkBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            MaximumResponseBytes,
            checked(MaximumResponseCharacters * LlmChatStreamingLimits.MaximumUtf8BytesPerCharacter));
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

public static class LlmChatStreamingLimits
{
    public const int MaximumUtf8BytesPerCharacter = 4;
    public const int MaximumPersistedEventTextBytes = 8 * 1024;
    public const int MaximumAssistantMessageUtf8Bytes =
        LlmMessage.MaximumTextLength * MaximumUtf8BytesPerCharacter;
}
