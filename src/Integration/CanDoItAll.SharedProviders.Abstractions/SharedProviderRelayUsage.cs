namespace CanDoItAll.SharedProviders.Abstractions;

public enum SharedProviderRelayUsageCompleteness {
    Unavailable,
    Partial,
    Complete
}

public sealed record SharedProviderRelayUsage {
    public SharedProviderRelayUsage(
        long? inputTokens,
        long? outputTokens,
        int? imageCount,
        SharedProviderRelayUsageCompleteness completeness,
        long? cachedInputTokens = null, long? cacheWriteTokens = null,
        long? reasoningTokens = null, SharedProviderReportedPrice? reportedPrice = null) {
        if (inputTokens is < 0) {
            throw new ArgumentOutOfRangeException(nameof(inputTokens));
        }

        if (outputTokens is < 0) {
            throw new ArgumentOutOfRangeException(nameof(outputTokens));
        }

        if (imageCount is <= 0 or > SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount) {
            throw new ArgumentOutOfRangeException(nameof(imageCount));
        }

        var hasInputTokens = inputTokens.HasValue;
        var hasOutputTokens = outputTokens.HasValue;
        var hasImageCount = imageCount.HasValue;
        var isConsistent = completeness switch {
            SharedProviderRelayUsageCompleteness.Unavailable =>
                !hasInputTokens && !hasOutputTokens && !hasImageCount,
            SharedProviderRelayUsageCompleteness.Partial =>
                !hasImageCount && hasInputTokens != hasOutputTokens,
            SharedProviderRelayUsageCompleteness.Complete =>
                !hasImageCount && hasInputTokens && hasOutputTokens ||
                hasImageCount && !hasInputTokens && !hasOutputTokens,
            _ => false
        };
        if (!isConsistent) {
            throw new ArgumentException(
                "Relay usage values do not match their completeness state.",
                nameof(completeness));
        }

        if (cachedInputTokens is < 0 || cacheWriteTokens is < 0 || reasoningTokens is < 0
            || cachedInputTokens.HasValue && !inputTokens.HasValue
            || cacheWriteTokens.HasValue && !inputTokens.HasValue
            || reasoningTokens.HasValue && !outputTokens.HasValue
            || cachedInputTokens > inputTokens || cacheWriteTokens > inputTokens - (cachedInputTokens ?? 0)
            || reasoningTokens > outputTokens) {
            throw new ArgumentException("Relay token categories are inconsistent with the observed totals.");
        }
        CachedInputTokens = cachedInputTokens;
        CacheWriteTokens = cacheWriteTokens;
        ReasoningTokens = reasoningTokens;
        ReportedPrice = reportedPrice;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        ImageCount = imageCount;
        Completeness = completeness;
    }

    public long? InputTokens { get; }

    public long? OutputTokens { get; }
    public long? CachedInputTokens { get; }
    public long? CacheWriteTokens { get; }
    public long? ReasoningTokens { get; }
    public SharedProviderReportedPrice? ReportedPrice { get; }

    public int? ImageCount { get; }

    public SharedProviderRelayUsageCompleteness Completeness { get; }

    public static SharedProviderRelayUsage Unavailable { get; } = new(
        inputTokens: null,
        outputTokens: null,
        imageCount: null,
        SharedProviderRelayUsageCompleteness.Unavailable);
}

public sealed record SharedProviderReportedPrice(decimal Amount, string Currency);

