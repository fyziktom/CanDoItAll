using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed record WorkflowUsageObservationQuery
{
    public IReadOnlyList<WorkflowRunId> RunIds { get; init; } = [];

    public IReadOnlyList<WorkflowProcessRunId> OriginProcessRunIds { get; init; } = [];

    public WorkflowId? WorkflowId { get; init; }

    public WorkflowVersionId? VersionId { get; init; }

    public WorkflowNodeId? NodeId { get; init; }

    public WorkflowExecutorId? ExecutorId { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public string Model { get; init; } = string.Empty;

    public DateTimeOffset? RecordedFromUtc { get; init; }

    public DateTimeOffset? RecordedToUtc { get; init; }
}

public sealed record WorkflowUsageObservationPageRequest(
    WorkflowUsageObservationQuery Query,
    int PageIndex = 0,
    int PageSize = 50);

public interface IWorkflowUsageObservationStore
{
    Task AppendAsync(
        WorkflowUsageObservation observation,
        CancellationToken cancellationToken = default);

    Task AppendRangeAsync(
        IReadOnlyList<WorkflowUsageObservation> observations,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowUsageObservation>> ListAsync(
        WorkflowUsageObservationQuery query,
        CancellationToken cancellationToken = default);

    Task<WorkflowListPage<WorkflowUsageObservation>> ListPageAsync(
        WorkflowUsageObservationPageRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowUsageAnalyticsStoreQuery(
    IReadOnlyList<WorkflowRunId> RunIds);

public sealed record WorkflowUsageAnalyticsStoreSnapshot(
    WorkflowUsageAnalyticsTotals Usage,
    IReadOnlyDictionary<WorkflowRunId, WorkflowUsageAnalyticsTotals> Runs,
    IReadOnlyList<WorkflowProviderModelAnalyticsRow> ProviderModels,
    IReadOnlyList<WorkflowNodeUsageAnalyticsRow> Nodes);

public interface IWorkflowUsageAnalyticsStore
{
    Task<WorkflowUsageAnalyticsStoreSnapshot> AggregateAsync(
        WorkflowUsageAnalyticsStoreQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowAnalyticsQuery(
    WorkflowId? WorkflowId = null,
    WorkflowRunState? State = null,
    WorkflowRuntimeBackendKind? Backend = null,
    string Search = "",
    int RecentTake = 8);

public sealed record WorkflowUsageAnalyticsTotals(
    int ObservationCount,
    int UsageKnownObservationCount,
    int UsageUnknownObservationCount,
    int PricingKnownObservationCount,
    int PricingUnknownObservationCount,
    long InputTokens,
    long CachedInputTokens,
    long OutputTokens,
    long ReasoningTokens,
    long TotalTokens,
    long ToolCallCount,
    decimal KnownCostUsd)
{
    public static WorkflowUsageAnalyticsTotals Empty { get; } = new(
        ObservationCount: 0,
        UsageKnownObservationCount: 0,
        UsageUnknownObservationCount: 0,
        PricingKnownObservationCount: 0,
        PricingUnknownObservationCount: 0,
        InputTokens: 0,
        CachedInputTokens: 0,
        OutputTokens: 0,
        ReasoningTokens: 0,
        TotalTokens: 0,
        ToolCallCount: 0,
        KnownCostUsd: 0m);
}

public sealed record WorkflowRunAnalyticsRow(
    WorkflowRunSnapshot Run,
    TimeSpan? Duration,
    bool IsDurationFinal,
    WorkflowUsageAnalyticsTotals Usage);

public sealed record WorkflowProviderModelAnalyticsRow(
    string ProviderName,
    ProviderKind? ProviderKind,
    string Model,
    WorkflowUsageAnalyticsTotals Usage);

public sealed record WorkflowNodeUsageAnalyticsRow(
    WorkflowNodeId NodeId,
    WorkflowExecutorId? ExecutorId,
    WorkflowUsageAnalyticsTotals Usage);

public sealed record WorkflowDurationAnalyticsSummary(
    int AvailableRunCount,
    int FinalRunCount,
    int ActiveRunCount,
    int UnavailableRunCount,
    TimeSpan Total,
    TimeSpan Average,
    TimeSpan Minimum,
    TimeSpan Maximum)
{
    public static WorkflowDurationAnalyticsSummary Empty { get; } = new(
        AvailableRunCount: 0,
        FinalRunCount: 0,
        ActiveRunCount: 0,
        UnavailableRunCount: 0,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero,
        TimeSpan.Zero);
}

public sealed record WorkflowAnalyticsSnapshot(
    DateTimeOffset AsOfUtc,
    int DefinitionCount,
    int ActiveDefinitionCount,
    IReadOnlyDictionary<WorkflowLifecycleStatus, int> DefinitionsByStatus,
    int RunCount,
    int RunningRunCount,
    int WaitingForInputRunCount,
    int FailedRunCount,
    IReadOnlyDictionary<WorkflowRunState, int> RunsByState,
    IReadOnlyDictionary<WorkflowRuntimeBackendKind, int> RunsByBackend,
    WorkflowUsageAnalyticsTotals Usage,
    WorkflowDurationAnalyticsSummary Duration,
    IReadOnlyList<WorkflowRunAnalyticsRow> Runs,
    IReadOnlyList<WorkflowProviderModelAnalyticsRow> ProviderModels,
    IReadOnlyList<WorkflowNodeUsageAnalyticsRow> Nodes,
    IReadOnlyList<WorkflowRunSnapshot> RecentRuns);

public interface IWorkflowAnalyticsQueryService
{
    Task<WorkflowAnalyticsSnapshot> QueryAsync(
        WorkflowAnalyticsQuery query,
        CancellationToken cancellationToken = default);
}
