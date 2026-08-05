namespace CanDoItAll.AgentFramework.Models;

public enum ProviderUsageObservationStatus
{
    Observed = 0,
    MissingAfterProviderActivity = 1,
    UsageUnavailable = 2,
    EstimatedFromMetric = 3,
    ObservedFromMetric = 4
}

public static class ProviderUsageSourcePhases
{
    public const string AgentRuntime = "agent-runtime";
    public const string AgentRuntimeContinuation = "agent-runtime-continuation";
    public const string InputAttachmentAnalysis = "input-attachment-analysis";
    public const string FinalizerShortCircuit = "finalizer-short-circuit";
    public const string FinalizerRecovery = "finalizer-recovery";
    public const string StructuredOutputRepair = "structured-output-repair";
    public const string LegacyAgentRunMetric = "legacy-agent-run-metric";
}

public sealed record ProviderUsageObservation(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string ProviderName,
    ProviderKind ProviderKind,
    string Model,
    ProviderTransportKind TransportKind,
    string SourcePhase,
    ProviderUsageObservationStatus UsageStatus,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    int ToolCallCount)
{
    public int CacheWriteTokens { get; init; }

    public Guid? ProviderProfileId { get; init; }

    public Guid? ExecutionRunId { get; init; }

    public Guid? AgentId { get; init; }

    public Guid? ChatSessionId { get; init; }

    public string ProviderResponseId { get; init; } = string.Empty;

    public string ProviderRequestId { get; init; } = string.Empty;

    public string RuntimeSessionKey { get; init; } = string.Empty;

    public string ProcessRunId { get; init; } = string.Empty;

    public string ProcessStepId { get; init; } = string.Empty;

    public string WorkflowRunId { get; init; } = string.Empty;

    public string WorkflowNodeId { get; init; } = string.Empty;

    public string CorrelationId { get; init; } = string.Empty;

    public decimal? ProviderCostUsd { get; init; }

    public decimal? CalculatedCostUsd { get; init; }

    public string PricingProfileHash { get; init; } = string.Empty;

    public string PricingVersion { get; init; } = string.Empty;

    public string RawUsageJson { get; init; } = string.Empty;

    public string DiagnosticsJson { get; init; } = string.Empty;
}

public sealed record ProviderUsageSummary(
    int ObservationCount,
    int KnownObservationCount,
    int UnknownObservationCount,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal KnownCostUsd)
{
    public int CacheWriteTokens { get; init; }

    public bool HasUnknownUsage => UnknownObservationCount > 0;
}
