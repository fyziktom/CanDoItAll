using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Projections;

public interface IProcessExecutionObservationReader
{
    ValueTask<IReadOnlyList<ProcessExecutionObservation>> ListAsync(
        ProcessExecutionObservationQuery query,
        CancellationToken cancellationToken = default);
}

public interface IProcessRuntimeUsageTelemetryReader
{
    ValueTask<IReadOnlyList<ProcessRuntimeUsageObservation>> ListAsync(
        ProcessRuntimeUsageTelemetryQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessExecutionObservationQuery(
    IReadOnlyList<ProcessRunId> RunIds,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TakePerRun)
{
    public IReadOnlyList<ProcessStepInstanceId> StepInstanceIds { get; init; } = [];
}

public sealed record ProcessRuntimeUsageTelemetryQuery(
    IReadOnlyList<ProcessRunId> RunIds,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int TakePerRun);

public sealed record ProcessRuntimeUsageObservation(
    Guid UsageObservationId,
    Guid ExecutionRunId,
    ProcessRunId RunId,
    ProcessStepInstanceId? StepInstanceId,
    DateTimeOffset CreatedAtUtc,
    string ProviderName,
    string Model,
    string SourcePhase,
    string UsageStatus,
    bool IsKnownUsage,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    int ReasoningTokens,
    int TotalTokens,
    decimal EstimatedCostUsd,
    decimal ActualCostUsd)
{
    public int ToolCallCount { get; init; }

    public int ContextEstimatedInputTokens { get; init; }

    public int ContextInputMessageCount { get; init; }

    public int ContextToolCount { get; init; }

    public int ContextToolSchemaEstimatedTokens { get; init; }

    public int ContextSourceCount { get; init; }

    public bool ContextBudgetExceeded { get; init; }

    public string ContextBudgetWarning { get; init; } = string.Empty;

    public string ContextDiagnosticsJson { get; init; } = string.Empty;
}

public sealed record ProcessExecutionObservation(
    Guid ExecutionRunId,
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    Guid AgentId,
    string AgentName,
    string ProviderName,
    string Model,
    string State,
    string Outcome,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string InputSummary,
    string ResultSummary,
    IReadOnlyList<ProcessExecutionActivityObservation> RecentActivities,
    IReadOnlyList<ProcessExecutionToolObservation> RecentTools,
    IReadOnlyList<ProcessExecutionArtifactObservation> Artifacts,
    string LastError)
{
    public string AgentAvatarImageUrl { get; init; } = string.Empty;
}

public sealed record ProcessExecutionActivityObservation(
    DateTimeOffset CreatedAtUtc,
    string State,
    string Phase,
    string Message);

public sealed record ProcessExecutionToolObservation(
    string ToolName,
    string RuntimeToolProviderKey,
    string RequestSummary,
    string ExitSummary,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc);

public sealed record ProcessExecutionArtifactObservation(
    string ArtifactKind,
    string DisplayName,
    string RelativePath,
    string Summary,
    DateTimeOffset CreatedAtUtc)
{
    public string ProducedBy { get; init; } = string.Empty;
}
