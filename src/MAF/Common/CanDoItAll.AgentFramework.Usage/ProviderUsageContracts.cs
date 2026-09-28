using System.ComponentModel;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Usage;

[Description("Origin of usage evidence, as a JSON integer.")]
public enum ProviderUsageWorkloadKind
{
    Unknown = 0,
    Agent = 1,
    SimpleChat = 2,
    SharedProviderRelay = 3
}

[Flags]
[Description("Selected workload flags, as a JSON integer; the bounded API accepts only 1 Agents, 2 SimpleChats and 3 Both.")]
public enum ProviderUsageWorkloadSelection
{
    None = 0,
    Agents = 1,
    SimpleChats = 2,
    SharedProviderRelays = 4,
    Both = Agents | SimpleChats,
    All = Agents | SimpleChats | SharedProviderRelays
}

[Description("Whether provider token evidence is known, as a JSON integer.")]
public enum ProviderUsageCompleteness
{
    Observed = 0,
    MissingAfterProviderActivity = 1,
    UsageUnavailable = 2,
    LegacyKnownTokens = 3
}

[Description("Origin or absence of recorded price, as a JSON integer.")]
public enum ProviderUsagePricingCompleteness
{
    ProviderReported = 0,
    CalculatedAtExecution = 1,
    Unpriced = 2
}

[Description("Consumer identity kind, as a JSON integer.")]
public enum ProviderUsageConsumerKind
{
    Unattributed = 0,
    Agent = 1,
    SimpleChatDefinition = 2,
    SharedProviderRelay = 3
}

[Description("Current recorded execution outcome, as a JSON integer.")]
public enum ProviderUsageExecutionOutcome
{
    Unknown = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3
}

[Description("Source coverage state, as a JSON integer; indexing and failed sources do not certify zero usage.")]
public enum ProviderUsageSourceState
{
    Complete = 0,
    Partial = 1,
    Failed = 2,
    Indexing = 3
}

[Description("Token dimensions as signed 64-bit JSON integers; aggregate totals can exceed Int32 capacity.")]
public sealed record ProviderUsageTokenCounts(
    [property: Description("Known input tokens, including cached input where reported.")] long InputTokens,
    [property: Description("Known cached input tokens; a subset of input tokens.")] long CachedInputTokens,
    [property: Description("Known cache-write input tokens.")] long CacheWriteTokens,
    [property: Description("Known generated output tokens.")] long OutputTokens,
    [property: Description("Known reasoning tokens reported by the provider.")] long ReasoningTokens,
    [property: Description("Known total tokens; 64-bit sum without wrapping or clamping aggregate overflow.")] long TotalTokens)
{
    public static ProviderUsageTokenCounts Empty { get; } = new(0, 0, 0, 0, 0, 0);

    public ProviderUsageTokenCounts Normalize()
    {
        var input = Math.Max(0, InputTokens);
        var cached = Math.Clamp(CachedInputTokens, 0, input);
        var cacheWrite = Math.Clamp(CacheWriteTokens, 0, input - cached);
        var output = Math.Max(0, OutputTokens);
        var reasoning = Math.Max(0, ReasoningTokens);
        var total = TotalTokens > 0 ? TotalTokens : checked(input + output);
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

[Description("Safe machine-readable reason and human-readable explanation for incomplete coverage.")]
public sealed record ProviderUsageSourceError(
    [property: Description("Stable source error code suitable for programmatic handling.")] string Code,
    [property: Description("Safe human-readable explanation of incomplete source coverage.")] string Message);

public sealed record ProviderUsageSourceResult(
    string SourceName,
    ProviderUsageWorkloadKind WorkloadKind,
    ProviderUsageSourceState State,
    IReadOnlyList<ProviderUsageContribution> Contributions,
    DateTimeOffset UpdatedAtUtc,
    ProviderUsageSourceError? Error = null)
{
    public ProviderUsageWindow? Window { get; init; }
    [Description("UTC time when canonical coverage was verified, or null when unverified; ongoing indexed writes preserve coverage.")]
    public DateTimeOffset? CoverageVerifiedAtUtc { get; init; }
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

[Description("Aggregates of included usage observations; distinct executions are identified by workload and execution ID.")]
public sealed record ProviderUsageTotals(
    [property: Description("Distinct included workload/execution identities; does not count providerless runs.")] int ExecutionCount,
    [property: Description("Included distinct executions whose current outcome is failed.")] int FailedExecutionCount,
    [property: Description("Included distinct executions whose current outcome is cancelled.")] int CancelledExecutionCount,
    [property: Description("Included deduplicated usage observations, including unknown usage.")] int UsageObservationCount,
    [property: Description("Observations with known usage evidence.")] int KnownUsageObservationCount,
    [property: Description("Observations with missing or unavailable usage evidence.")] int UnknownUsageObservationCount,
    [property: Description("Observations with an execution-time recorded price.")] int PricedObservationCount,
    [property: Description("Observations with no known recorded price; never assumed free.")] int UnpricedObservationCount,
    [property: Description("Known token dimensions; unknown usage is counted separately.")] ProviderUsageTokenCounts Tokens,
    [property: Description("Sum of recorded decimal USD costs; excludes unpriced observations and never reprices history.")] decimal KnownCostUsd)
{
    [Description("Known image count from image-only observations; separate from token dimensions.")]
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

[Description("Usage attributed to one consumer in the accepted interval.")]
public sealed record ProviderUsageConsumerRow(
    [property: Description("Consumer identity kind, as a JSON integer.")] ProviderUsageConsumerKind ConsumerKind,
    [property: Description("Consumer identifier within its kind; empty for unattributed usage.")] string ConsumerId,
    [property: Description("Current display name or the original selected chat definition revision name.")] string ConsumerName,
    [property: Description("Usage aggregates for this grouping and the accepted interval.")] ProviderUsageTotals Totals,
    [property: Description("Latest included usage occurrence in UTC, or null when absent.")] DateTimeOffset? LastUsedAtUtc);

[Description("Usage attributed to one provider in the accepted interval.")]
public sealed record ProviderUsageProviderRow(
    [property: Description("Provider profile GUID captured by the evidence, or null for legacy evidence.")] Guid? ProviderProfileId,
    [property: Description("Provider display name captured by the evidence.")] string ProviderName,
    [property: Description("Provider kind as a JSON integer: 0 OpenAi, 1 AzureOpenAi, 2 Ollama, 3 ComfyUi.")] ProviderKind ProviderKind,
    [property: Description("Usage aggregates for this grouping and the accepted interval.")] ProviderUsageTotals Totals,
    [property: Description("Latest included usage occurrence in UTC, or null when absent.")] DateTimeOffset? LastUsedAtUtc);

[Description("Usage attributed to one provider model in the accepted interval.")]
public sealed record ProviderUsageModelRow(
    [property: Description("Provider profile GUID captured by the evidence, or null for legacy evidence.")] Guid? ProviderProfileId,
    [property: Description("Provider display name captured by the evidence.")] string ProviderName,
    [property: Description("Provider kind as a JSON integer: 0 OpenAi, 1 AzureOpenAi, 2 Ollama, 3 ComfyUi.")] ProviderKind ProviderKind,
    [property: Description("Provider model identifier captured by the evidence.")] string Model,
    [property: Description("Usage aggregates for this grouping and the accepted interval.")] ProviderUsageTotals Totals,
    [property: Description("Latest included usage occurrence in UTC, or null when absent.")] DateTimeOffset? LastUsedAtUtc);

[Description("Coverage and error metadata for one selected usage source.")]
public sealed record ProviderUsageSourceStatus(
    [property: Description("Stable source identifier; does not expose a canonical storage path.")] string SourceName,
    [property: Description("Source workload kind, as a JSON integer.")] ProviderUsageWorkloadKind WorkloadKind,
    [property: Description("Source coverage state, as a JSON integer.")] ProviderUsageSourceState State,
    [property: Description("Latest included evidence timestamp; Unix epoch when no evidence exists. This is not generation time or coverage.")] DateTimeOffset UpdatedAtUtc,
    [property: Description("Safe incomplete-source reason, or null when no error is present.")] ProviderUsageSourceError? Error) {
    [Description("UTC time when canonical coverage was verified, or null when unverified; ongoing indexed writes preserve coverage.")]
    public DateTimeOffset? CoverageVerifiedAtUtc { get; init; }
}

[Description("Unified usage with the accepted query, exact UTC interval, generation time and per-source coverage.")]
public sealed record ProviderUsageSnapshot(
    [property: Description("Accepted workload flags, as a JSON integer.")] ProviderUsageWorkloadSelection Selection,
    [property: Description("Usage aggregates for this grouping and the accepted interval.")] ProviderUsageTotals Totals,
    [property: Description("Consumer aggregates for included observations.")] IReadOnlyList<ProviderUsageConsumerRow> Consumers,
    [property: Description("Provider aggregates for included observations.")] IReadOnlyList<ProviderUsageProviderRow> Providers,
    [property: Description("Provider/model aggregates for included observations.")] IReadOnlyList<ProviderUsageModelRow> Models,
    [property: Description("Status of each selected source; inspect isComplete before interpreting zero totals.")] IReadOnlyList<ProviderUsageSourceStatus> Sources,
    [property: Description("Latest included evidence timestamp; Unix epoch when no evidence exists. This is not generation time or coverage.")] DateTimeOffset UpdatedAtUtc)
{
    [Description("Exact resolved query for bounded reads; null only for explicitly unbounded legacy callers.")]
    public ProviderUsageQuery? Query { get; init; }
    [Description("UTC time this aggregate was generated; independent of latest usage occurrence.")]
    public DateTimeOffset GeneratedAtUtc { get; init; }
    [Description("True only when all required selected sources are present and complete; false for partial, indexing, failed or absent sources.")]
    public bool IsComplete => Sources.Count > 0 && Sources.All(source => source.State == ProviderUsageSourceState.Complete)
        && (!Selection.HasFlag(ProviderUsageWorkloadSelection.Agents) || Sources.Any(source => source.WorkloadKind == ProviderUsageWorkloadKind.Agent))
        && (!Selection.HasFlag(ProviderUsageWorkloadSelection.SimpleChats) || Sources.Any(source => source.WorkloadKind == ProviderUsageWorkloadKind.SimpleChat))
        && (!Selection.HasFlag(ProviderUsageWorkloadSelection.SharedProviderRelays) || Sources.Any(source => source.WorkloadKind == ProviderUsageWorkloadKind.SharedProviderRelay));

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
