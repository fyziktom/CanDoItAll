using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowOverviewQueryService(
    IWorkflowCatalogService catalogService,
    IWorkflowOverviewStore overviewStore,
    TimeProvider timeProvider) : IWorkflowOverviewQueryService
{
    private const int MaximumRecentTake = 12;
    private const int MaximumTopWorkflowTake = 10;

    public async Task<WorkflowOverviewSnapshot> QueryAsync(
        WorkflowOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateTake(query.RecentTake, MaximumRecentTake, nameof(query.RecentTake));
        ValidateTake(query.TopWorkflowTake, MaximumTopWorkflowTake, nameof(query.TopWorkflowTake));

        var definitionsTask = catalogService.ListDefinitionsAsync(cancellationToken);
        var storeSnapshotTask = overviewStore.QueryOverviewAsync(
            new WorkflowOverviewStoreQuery(query.RecentTake, query.TopWorkflowTake),
            cancellationToken);
        await Task.WhenAll(definitionsTask, storeSnapshotTask).ConfigureAwait(false);

        var definitions = (await definitionsTask.ConfigureAwait(false)).ToArray();
        var storeSnapshot = await storeSnapshotTask.ConfigureAwait(false);
        var definitionLookup = definitions.ToDictionary(definition => definition.Id);
        var runsByState = SnapshotCounts(storeSnapshot.RunsByState);
        var completedRunCount = runsByState.GetValueOrDefault(WorkflowRunState.Completed);
        var failedRunCount = runsByState.GetValueOrDefault(WorkflowRunState.Failed);

        return new WorkflowOverviewSnapshot(
            timeProvider.GetUtcNow(),
            definitions.Length,
            definitions.Count(definition => definition.Status == WorkflowLifecycleStatus.Active),
            runsByState.Values.Sum(),
            runsByState.GetValueOrDefault(WorkflowRunState.Running),
            runsByState.GetValueOrDefault(WorkflowRunState.WaitingForInput),
            completedRunCount,
            failedRunCount,
            CalculateSuccessRate(completedRunCount, failedRunCount),
            CountBy(definitions, definition => definition.Status),
            runsByState,
            SnapshotCounts(storeSnapshot.RunsByBackend),
            storeSnapshot.TopWorkflows
                .Take(query.TopWorkflowTake)
                .Select(row => CreateWorkflowRow(row, definitionLookup))
                .ToArray(),
            definitions
                .OrderByDescending(definition => definition.UpdatedAtUtc)
                .ThenBy(definition => definition.Id.Value)
                .Take(query.TopWorkflowTake)
                .ToArray(),
            storeSnapshot.RecentRuns
                .Take(query.RecentTake)
                .Select(run => new WorkflowOverviewRecentRunRow(
                    run,
                    definitionLookup.TryGetValue(run.WorkflowId, out var definition)
                        ? definition.Name
                        : $"Deleted workflow {run.WorkflowId.Value:D}"))
                .ToArray());
    }

    private static WorkflowOverviewWorkflowRow CreateWorkflowRow(
        WorkflowOverviewStoreWorkflowRow row,
        IReadOnlyDictionary<WorkflowId, WorkflowCatalogItem> definitions)
    {
        if (definitions.TryGetValue(row.WorkflowId, out var definition))
        {
            return new WorkflowOverviewWorkflowRow(
                row.WorkflowId,
                definition.Name,
                definition.Status,
                row.RunCount,
                row.FailedRunCount,
                row.LastRunAtUtc);
        }

        return new WorkflowOverviewWorkflowRow(
            row.WorkflowId,
            $"Deleted workflow {row.WorkflowId.Value:D}",
            Status: null,
            row.RunCount,
            row.FailedRunCount,
            row.LastRunAtUtc);
    }

    private static decimal? CalculateSuccessRate(int completedRunCount, int failedRunCount)
    {
        var resolvedRunCount = completedRunCount + failedRunCount;
        return resolvedRunCount == 0
            ? null
            : decimal.Round(completedRunCount * 100m / resolvedRunCount, 1, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyDictionary<TKey, int> CountBy<T, TKey>(
        IEnumerable<T> values,
        Func<T, TKey> keySelector)
        where TKey : notnull
        => values
            .GroupBy(keySelector)
            .ToDictionary(group => group.Key, group => group.Count());

    private static IReadOnlyDictionary<TKey, int> SnapshotCounts<TKey>(
        IReadOnlyDictionary<TKey, int> counts)
        where TKey : notnull
        => counts.ToDictionary(pair => pair.Key, pair => pair.Value);

    private static void ValidateTake(int value, int maximum, string parameterName)
    {
        if (value is < 1 || value > maximum)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                $"Workflow overview take must be between 1 and {maximum}.");
        }
    }
}
