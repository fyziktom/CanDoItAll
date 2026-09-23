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

/// <summary>
/// Model usage added up over a set of usage observations. An observation is the usage recorded for one model call
/// attempt of a workflow node; the set is all matching runs, one run, one provider and model, or one node, depending
/// on where the totals appear.
/// </summary>
/// <param name="ObservationCount">Number of usage observations added up.</param>
/// <param name="UsageKnownObservationCount">
/// Observations whose token counts the provider reported or the host estimated.
/// </param>
/// <param name="UsageUnknownObservationCount">
/// Observations without token counts (usage unavailable, or missing after provider activity). Their tokens count as
/// zero in the sums.
/// </param>
/// <param name="PricingKnownObservationCount">
/// Observations with a known cost, reported by the provider or calculated from the provider profile's model prices.
/// </param>
/// <param name="PricingUnknownObservationCount">
/// Observations without a known cost. When it is above zero, <c>knownCostUsd</c> understates the real cost.
/// </param>
/// <param name="InputTokens">Sum of input tokens.</param>
/// <param name="CachedInputTokens">Sum of the input tokens served from the provider's cache; part of the input.</param>
/// <param name="OutputTokens">Sum of output tokens.</param>
/// <param name="ReasoningTokens">Sum of reasoning tokens as reported by the provider.</param>
/// <param name="TotalTokens">Sum of total tokens as reported or estimated for each observation.</param>
/// <param name="ToolCallCount">Sum of tool calls made by the model.</param>
/// <param name="KnownCostUsd">
/// Sum of the known costs in US dollars, rounded to 6 decimal places. Observations without a known cost add nothing.
/// </param>
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

/// <summary>
/// Analytics entry for one matching run: the stored run record, its duration and its model usage.
/// </summary>
/// <param name="Run">
/// The stored run record, not the safe projection of the run read routes: it includes the backend run identifier. The
/// HTTP analytics operation withholds its launch origin (always null) because the run can belong to another caller.
/// </param>
/// <param name="Duration">
/// Time from the run's creation to its end, as a string in the form <c>[-][d.]hh:mm:ss[.fffffff]</c>, for example
/// <c>00:02:15.5000000</c>. For a run that has not ended it is measured up to <c>asOfUtc</c>. Null when an ended run
/// has no end time recorded.
/// </param>
/// <param name="IsDurationFinal">
/// True when the run has an end time, so <c>duration</c> will not change; false while it is measured up to the
/// snapshot time or unavailable.
/// </param>
/// <param name="Usage">Model usage recorded for this run; all zero when none was recorded.</param>
public sealed record WorkflowRunAnalyticsRow(
    WorkflowRunSnapshot Run,
    TimeSpan? Duration,
    bool IsDurationFinal,
    WorkflowUsageAnalyticsTotals Usage);

/// <summary>
/// Model usage of the matching runs for one provider and model. Rows are grouped by provider name and model ignoring
/// case, and by provider kind, and are ordered by provider name and then model.
/// </summary>
/// <param name="ProviderName">Provider name recorded with the usage, in one of the spellings seen.</param>
/// <param name="ProviderKind">
/// Provider kind, as a JSON integer: 0 OpenAi, 1 AzureOpenAi, 2 Ollama, 3 ComfyUi. Null for usage imported from the
/// older aggregate usage metrics.
/// </param>
/// <param name="Model">Model name recorded with the usage, in one of the spellings seen.</param>
/// <param name="Usage">Usage totals of this provider and model.</param>
public sealed record WorkflowProviderModelAnalyticsRow(
    string ProviderName,
    ProviderKind? ProviderKind,
    string Model,
    WorkflowUsageAnalyticsTotals Usage);

/// <summary>
/// Model usage of the matching runs for one workflow node and executor, ordered by node identifier and then executor
/// identifier.
/// </summary>
/// <param name="NodeId">Identifier of the node in the workflow graph that recorded the usage.</param>
/// <param name="ExecutorId">
/// Executor of that node, as listed by <c>GET /api/workflows/executor-catalog</c>. Null for usage of an LLM call
/// component, which runs without an executor.
/// </param>
/// <param name="Usage">Usage totals of this node and executor.</param>
public sealed record WorkflowNodeUsageAnalyticsRow(
    WorkflowNodeId NodeId,
    WorkflowExecutorId? ExecutorId,
    WorkflowUsageAnalyticsTotals Usage);

/// <summary>
/// Duration statistics over the matching runs. Durations are strings in the form <c>[-][d.]hh:mm:ss[.fffffff]</c>;
/// all four are <c>00:00:00</c> when no run has a duration.
/// </summary>
/// <param name="AvailableRunCount">Runs with a duration; the statistics cover these runs.</param>
/// <param name="FinalRunCount">Runs with a final duration because they have ended.</param>
/// <param name="ActiveRunCount">Runs that have not ended, measured up to the snapshot time.</param>
/// <param name="UnavailableRunCount">Ended runs without a recorded end time, left out of the statistics.</param>
/// <param name="Total">Sum of the available durations.</param>
/// <param name="Average">Mean of the available durations.</param>
/// <param name="Minimum">Shortest available duration.</param>
/// <param name="Maximum">Longest available duration.</param>
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

/// <summary>
/// Workflow analytics computed at request time by <c>GET /api/workflows/analytics</c>. Definition figures cover the
/// current version of every workflow, or of the one requested; run, duration and usage figures cover the runs that
/// match all query filters.
/// </summary>
/// <param name="AsOfUtc">
/// UTC time the snapshot was computed; durations of runs that have not ended are measured up to it.
/// </param>
/// <param name="DefinitionCount">Number of workflows counted (0 or 1 when one workflow is requested).</param>
/// <param name="ActiveDefinitionCount">How many of them have an Active current version.</param>
/// <param name="DefinitionsByStatus">
/// Workflow count per lifecycle status of the current version, keyed by status name (<c>Draft</c>, <c>Active</c>,
/// <c>Suspended</c>, <c>Archived</c>). Statuses with no workflow are left out.
/// </param>
/// <param name="RunCount">Number of matching runs.</param>
/// <param name="RunningRunCount">Matching runs in the Running state.</param>
/// <param name="WaitingForInputRunCount">Matching runs waiting for an external response.</param>
/// <param name="FailedRunCount">Matching runs in the Failed state.</param>
/// <param name="RunsByState">
/// Matching run count per run state, keyed by state name (<c>NotStarted</c>, <c>Running</c>, <c>WaitingForInput</c>,
/// <c>Idle</c>, <c>Completed</c>, <c>Failed</c>, <c>Cancelled</c>). States with no run are left out.
/// </param>
/// <param name="RunsByBackend">
/// Matching run count per runtime backend, keyed by backend name (<c>InProcess</c>, <c>DurableTask</c>,
/// <c>AzureFunctions</c>). Backends with no run are left out.
/// </param>
/// <param name="Usage">Model usage totals of the matching runs.</param>
/// <param name="Duration">Duration statistics of the matching runs.</param>
/// <param name="Runs">Every matching run with its duration and usage, most recently updated first.</param>
/// <param name="ProviderModels">Usage of the matching runs per provider and model.</param>
/// <param name="Nodes">Usage of the matching runs per node and executor.</param>
/// <param name="RecentRuns">
/// The most recently updated matching runs as stored run records, at most <c>Take</c> of them (8 by default); the HTTP
/// analytics operation withholds their launch origins.
/// </param>
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
