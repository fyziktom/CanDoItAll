using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowAnalyticsQueryService(
    IWorkflowCatalogService catalog,
    IWorkflowRunStore runStore,
    IWorkflowUsageAnalyticsStore usageStore,
    TimeProvider timeProvider) : IWorkflowAnalyticsQueryService
{
    public async Task<WorkflowAnalyticsSnapshot> QueryAsync(
        WorkflowAnalyticsQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.RecentTake is < 1 or > 500)
        {
            throw new ArgumentOutOfRangeException(nameof(query), "Workflow analytics recent take must be between 1 and 500.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var asOfUtc = timeProvider.GetUtcNow();
        var definitions = await catalog.ListDefinitionsAsync(cancellationToken).ConfigureAwait(false);
        var filteredDefinitions = query.WorkflowId is { } workflowId
            ? definitions.Where(definition => definition.Id == workflowId).ToArray()
            : definitions;
        var runs = await runStore.ListRunsAsync(query.WorkflowId, cancellationToken).ConfigureAwait(false);
        var filteredRuns = FilterRuns(runs, query);
        var usageProjection = await usageStore.AggregateAsync(
            new WorkflowUsageAnalyticsStoreQuery(filteredRuns.Select(run => run.RunId).ToArray()),
            cancellationToken).ConfigureAwait(false);
        var runRows = filteredRuns
            .Select(run => CreateRunRow(
                run,
                usageProjection.Runs.GetValueOrDefault(run.RunId) ?? WorkflowUsageAnalyticsTotals.Empty,
                asOfUtc))
            .OrderByDescending(row => row.Run.UpdatedAtUtc)
            .ToArray();

        return new WorkflowAnalyticsSnapshot(
            asOfUtc,
            filteredDefinitions.Count,
            filteredDefinitions.Count(definition => definition.Status == WorkflowLifecycleStatus.Active),
            CountBy(filteredDefinitions, definition => definition.Status),
            filteredRuns.Count,
            filteredRuns.Count(run => run.State == WorkflowRunState.Running),
            filteredRuns.Count(run => run.State == WorkflowRunState.WaitingForInput),
            filteredRuns.Count(run => run.State == WorkflowRunState.Failed),
            CountBy(filteredRuns, run => run.State),
            CountBy(filteredRuns, run => run.Backend),
            usageProjection.Usage,
            SummarizeDuration(runRows),
            runRows,
            usageProjection.ProviderModels,
            usageProjection.Nodes,
            filteredRuns
                .OrderByDescending(run => run.UpdatedAtUtc)
                .Take(query.RecentTake)
                .ToArray());
    }

    private static IReadOnlyList<WorkflowRunSnapshot> FilterRuns(
        IReadOnlyList<WorkflowRunSnapshot> runs,
        WorkflowAnalyticsQuery query)
    {
        var filtered = runs.AsEnumerable();
        if (query.State is { } state)
        {
            filtered = filtered.Where(run => run.State == state);
        }

        if (query.Backend is { } backend)
        {
            filtered = filtered.Where(run => run.Backend == backend);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            filtered = filtered.Where(run =>
                run.Summary.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                run.BackendRunId.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                run.RunId.ToString().Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return filtered.ToArray();
    }

    private static WorkflowRunAnalyticsRow CreateRunRow(
        WorkflowRunSnapshot run,
        WorkflowUsageAnalyticsTotals usage,
        DateTimeOffset asOfUtc)
    {
        var duration = ResolveDuration(run, asOfUtc, out var isFinal);
        return new WorkflowRunAnalyticsRow(run, duration, isFinal, usage);
    }

    private static TimeSpan? ResolveDuration(
        WorkflowRunSnapshot run,
        DateTimeOffset asOfUtc,
        out bool isFinal)
    {
        isFinal = run.TerminalAtUtc.HasValue;
        DateTimeOffset? endAtUtc;
        if (IsTerminal(run.State))
        {
            endAtUtc = run.TerminalAtUtc;
        }
        else
        {
            endAtUtc = asOfUtc;
        }

        if (endAtUtc is null)
        {
            return null;
        }

        if (endAtUtc.Value < run.CreatedAtUtc)
        {
            throw new InvalidOperationException(
                $"Workflow run '{run.RunId}' has a duration end timestamp before its creation timestamp.");
        }

        return endAtUtc.Value - run.CreatedAtUtc;
    }

    private static bool IsTerminal(WorkflowRunState state)
        => state is WorkflowRunState.Completed or WorkflowRunState.Failed or WorkflowRunState.Cancelled;

    private static WorkflowDurationAnalyticsSummary SummarizeDuration(
        IReadOnlyList<WorkflowRunAnalyticsRow> rows)
    {
        var available = rows.Where(row => row.Duration.HasValue).ToArray();
        if (available.Length == 0)
        {
            return WorkflowDurationAnalyticsSummary.Empty with
            {
                UnavailableRunCount = rows.Count
            };
        }

        var durations = available.Select(row => row.Duration!.Value).ToArray();
        var totalTicks = durations.Sum(duration => duration.Ticks);
        return new WorkflowDurationAnalyticsSummary(
            available.Length,
            available.Count(row => row.IsDurationFinal),
            available.Count(row => !row.IsDurationFinal),
            rows.Count - available.Length,
            TimeSpan.FromTicks(totalTicks),
            TimeSpan.FromTicks(totalTicks / available.Length),
            durations.Min(),
            durations.Max());
    }

    private static IReadOnlyDictionary<TKey, int> CountBy<T, TKey>(
        IEnumerable<T> values,
        Func<T, TKey> keySelector)
        where TKey : notnull
        => values
            .GroupBy(keySelector)
            .OrderBy(group => group.Key)
            .ToDictionary(group => group.Key, group => group.Count());

}
