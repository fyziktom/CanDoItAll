using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Workflows.Abstractions;

public sealed record WorkflowOverviewStoreQuery(
    int RecentTake = 6,
    int TopWorkflowTake = 5);

public sealed record WorkflowOverviewStoreWorkflowRow(
    WorkflowId WorkflowId,
    int RunCount,
    int FailedRunCount,
    DateTimeOffset LastRunAtUtc);

public sealed record WorkflowOverviewStoreSnapshot(
    IReadOnlyDictionary<WorkflowRunState, int> RunsByState,
    IReadOnlyDictionary<WorkflowRuntimeBackendKind, int> RunsByBackend,
    IReadOnlyList<WorkflowOverviewStoreWorkflowRow> TopWorkflows,
    IReadOnlyList<WorkflowRunSnapshot> RecentRuns);

public interface IWorkflowOverviewStore
{
    Task<WorkflowOverviewStoreSnapshot> QueryOverviewAsync(
        WorkflowOverviewStoreQuery query,
        CancellationToken cancellationToken = default);
}

public sealed record WorkflowOverviewQuery(
    int RecentTake = 6,
    int TopWorkflowTake = 5);

public sealed record WorkflowOverviewWorkflowRow(
    WorkflowId WorkflowId,
    string Name,
    WorkflowLifecycleStatus? Status,
    int RunCount,
    int FailedRunCount,
    DateTimeOffset LastRunAtUtc);

public sealed record WorkflowOverviewRecentRunRow(
    WorkflowRunSnapshot Run,
    string WorkflowName);

public sealed record WorkflowOverviewSnapshot(
    DateTimeOffset AsOfUtc,
    int DefinitionCount,
    int ActiveDefinitionCount,
    int RunCount,
    int RunningRunCount,
    int WaitingForInputRunCount,
    int CompletedRunCount,
    int FailedRunCount,
    decimal? SuccessRatePercent,
    IReadOnlyDictionary<WorkflowLifecycleStatus, int> DefinitionsByStatus,
    IReadOnlyDictionary<WorkflowRunState, int> RunsByState,
    IReadOnlyDictionary<WorkflowRuntimeBackendKind, int> RunsByBackend,
    IReadOnlyList<WorkflowOverviewWorkflowRow> TopWorkflows,
    IReadOnlyList<WorkflowCatalogItem> RecentlyUpdatedDefinitions,
    IReadOnlyList<WorkflowOverviewRecentRunRow> RecentRuns);

public interface IWorkflowOverviewQueryService
{
    Task<WorkflowOverviewSnapshot> QueryAsync(
        WorkflowOverviewQuery query,
        CancellationToken cancellationToken = default);
}
