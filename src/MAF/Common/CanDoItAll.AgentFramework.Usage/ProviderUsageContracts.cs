using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Usage;

public enum ProviderUsageWorkloadKind
{
    Unknown = 0,
    Agent = 1,
    SimpleChat = 2,
    SharedProviderRelay = 3
}

[Flags]
public enum ProviderUsageWorkloadSelection
{
    None = 0,
    Agents = 1,
    SimpleChats = 2,
    SharedProviderRelays = 4,
    Both = Agents | SimpleChats,
    All = Agents | SimpleChats | SharedProviderRelays
}

public enum ProviderUsageCompleteness
{
    Observed = 0,
    MissingAfterProviderActivity = 1,
    UsageUnavailable = 2,
    LegacyKnownTokens = 3
}

public enum ProviderUsagePricingCompleteness
{
    ProviderReported = 0,
    CalculatedAtExecution = 1,
    Unpriced = 2
}

public enum ProviderUsageConsumerKind
{
    Unattributed = 0,
    Agent = 1,
    SimpleChatDefinition = 2,
    SharedProviderRelay = 3
}

public enum ProviderUsageExecutionOutcome
{
    Unknown = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3
}

public enum ProviderUsageSourceState
{
    Complete = 0,
    Partial = 1,
    Failed = 2
}

public sealed record ProviderUsageTokenCounts(
    int InputTokens,
    int CachedInputTokens,
    int CacheWriteTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens)
{
    public static ProviderUsageTokenCounts Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public ProviderUsageTokenCounts Normalize()
    {
        var input = Math.Max(0, InputTokens);
        var cached = Math.Clamp(CachedInputTokens, 0, input);
        var cacheWrite = Math.Clamp(CacheWriteTokens, 0, input - cached);
        var output = Math.Max(0, OutputTokens);
        var reasoning = Math.Max(0, ReasoningTokens);
        var total = TotalTokens > 0 ? TotalTokens : input + output;
        return new(input, cached, cacheWrite, output, reasoning, Math.Max(0, total));
    }
}

public sealed record ProviderUsageContribution(
    string ContributionId,
    ProviderUsageWorkloadKind WorkloadKind,
    ProviderUsageConsumerKind ConsumerKind,
    string ConsumerId,
    string ConsumerName,
    Guid? ProviderProfileId,
    string ProviderName,
    ProviderKind ProviderKind,
    string Model,
    string ExecutionId,
    ProviderUsageExecutionOutcome ExecutionOutcome,
    ProviderUsageCompleteness UsageCompleteness,
    ProviderUsagePricingCompleteness PricingCompleteness,
    ProviderUsageTokenCounts Tokens,
    decimal? CostUsd,
    DateTimeOffset OccurredAtUtc)
{
    public int? ImageCount { get; init; }
}

public sealed record ProviderUsageSourceError(string Code, string Message);

public sealed record ProviderUsageSourceResult(
    string SourceName,
    ProviderUsageWorkloadKind WorkloadKind,
    ProviderUsageSourceState State,
    IReadOnlyList<ProviderUsageContribution> Contributions,
    DateTimeOffset UpdatedAtUtc,
    ProviderUsageSourceError? Error = null)
{
    public static ProviderUsageSourceResult Failed(
        string sourceName,
        ProviderUsageWorkloadKind workloadKind,
        string code,
        string message,
        DateTimeOffset updatedAtUtc)
    {
        return new(
            sourceName,
            workloadKind,
            ProviderUsageSourceState.Failed,
            [],
            updatedAtUtc,
            new ProviderUsageSourceError(code, message));
    }
}

public interface IProviderUsageProjectionSource
{
    string SourceName { get; }

    ProviderUsageWorkloadKind WorkloadKind { get; }

    ValueTask<ProviderUsageSourceResult> ReadAsync(CancellationToken cancellationToken = default);
}

public sealed record ProviderUsageTotals(
    int ExecutionCount,
    int FailedExecutionCount,
    int CancelledExecutionCount,
    int UsageObservationCount,
    int KnownUsageObservationCount,
    int UnknownUsageObservationCount,
    int PricedObservationCount,
    int UnpricedObservationCount,
    ProviderUsageTokenCounts Tokens,
    decimal KnownCostUsd)
{
    public int ImageCount { get; init; }

    public static ProviderUsageTotals Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        ProviderUsageTokenCounts.Empty,
        0m);
}

public sealed record ProviderUsageConsumerRow(
    ProviderUsageConsumerKind ConsumerKind,
    string ConsumerId,
    string ConsumerName,
    ProviderUsageTotals Totals,
    DateTimeOffset? LastUsedAtUtc);

public sealed record ProviderUsageProviderRow(
    Guid? ProviderProfileId,
    string ProviderName,
    ProviderKind ProviderKind,
    ProviderUsageTotals Totals,
    DateTimeOffset? LastUsedAtUtc);

public sealed record ProviderUsageModelRow(
    Guid? ProviderProfileId,
    string ProviderName,
    ProviderKind ProviderKind,
    string Model,
    ProviderUsageTotals Totals,
    DateTimeOffset? LastUsedAtUtc);

public sealed record ProviderUsageSourceStatus(
    string SourceName,
    ProviderUsageWorkloadKind WorkloadKind,
    ProviderUsageSourceState State,
    DateTimeOffset UpdatedAtUtc,
    ProviderUsageSourceError? Error);

public sealed record ProviderUsageSnapshot(
    ProviderUsageWorkloadSelection Selection,
    ProviderUsageTotals Totals,
    IReadOnlyList<ProviderUsageConsumerRow> Consumers,
    IReadOnlyList<ProviderUsageProviderRow> Providers,
    IReadOnlyList<ProviderUsageModelRow> Models,
    IReadOnlyList<ProviderUsageSourceStatus> Sources,
    DateTimeOffset UpdatedAtUtc)
{
    public bool IsComplete => Sources.All(source => source.State == ProviderUsageSourceState.Complete);

    public static ProviderUsageSnapshot Empty(ProviderUsageWorkloadSelection selection)
    {
        selection.Validate();
        return new(
            selection,
            ProviderUsageTotals.Empty,
            [],
            [],
            [],
            [],
            DateTimeOffset.UnixEpoch);
    }
}

public static class ProviderUsageWorkloadSelectionExtensions
{
    private const ProviderUsageWorkloadSelection KnownValues = ProviderUsageWorkloadSelection.All;

    public static ProviderUsageWorkloadSelection Validate(this ProviderUsageWorkloadSelection selection)
    {
        if (selection == ProviderUsageWorkloadSelection.None || (selection & ~KnownValues) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                selection,
                "Select at least one known provider usage workload.");
        }

        return selection;
    }

    public static bool Includes(this ProviderUsageWorkloadSelection selection, ProviderUsageWorkloadKind workloadKind)
    {
        selection.Validate();
        return workloadKind switch
        {
            ProviderUsageWorkloadKind.Agent => selection.HasFlag(ProviderUsageWorkloadSelection.Agents),
            ProviderUsageWorkloadKind.SimpleChat => selection.HasFlag(ProviderUsageWorkloadSelection.SimpleChats),
            ProviderUsageWorkloadKind.SharedProviderRelay =>
                selection.HasFlag(ProviderUsageWorkloadSelection.SharedProviderRelays),
            ProviderUsageWorkloadKind.Unknown =>
                selection is ProviderUsageWorkloadSelection.Both or ProviderUsageWorkloadSelection.All,
            _ => false
        };
    }
}

public static class ProviderUsageWorkloadClassifier
{
    public static ProviderUsageWorkloadKind ClassifyAgentObservation(
        Guid? agentId,
        Guid? executionRunId,
        Guid? chatSessionId)
    {
        _ = chatSessionId;
        return agentId.HasValue || executionRunId.HasValue
            ? ProviderUsageWorkloadKind.Agent
            : ProviderUsageWorkloadKind.Unknown;
    }
}
