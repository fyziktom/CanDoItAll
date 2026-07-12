using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.AgentFramework.Core;

public sealed class WorkflowUsageAnalyticsStore(
    IWorkflowUsageObservationStore observationStore) : IWorkflowUsageAnalyticsStore
{
    public async Task<WorkflowUsageAnalyticsStoreSnapshot> AggregateAsync(
        WorkflowUsageAnalyticsStoreQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (query.RunIds.Count == 0)
        {
            return WorkflowUsageAnalyticsAggregation.Empty;
        }

        var observations = await observationStore.ListAsync(new WorkflowUsageObservationQuery
        {
            RunIds = query.RunIds
        }, cancellationToken).ConfigureAwait(false);
        return WorkflowUsageAnalyticsAggregation.Create(observations);
    }
}

public static class WorkflowUsageAnalyticsAggregation
{
    public static WorkflowUsageAnalyticsStoreSnapshot Empty { get; } = new(
        WorkflowUsageAnalyticsTotals.Empty,
        new Dictionary<WorkflowRunId, WorkflowUsageAnalyticsTotals>(),
        [],
        []);

    public static WorkflowUsageAnalyticsStoreSnapshot Create(
        IReadOnlyList<WorkflowUsageObservation> observations)
    {
        ArgumentNullException.ThrowIfNull(observations);
        var canonical = EnsureCanonicalFacts(observations);
        var runs = canonical
            .Where(observation => observation.RunId.HasValue)
            .GroupBy(observation => observation.RunId!.Value)
            .ToDictionary(group => group.Key, SumUsage);

        return new WorkflowUsageAnalyticsStoreSnapshot(
            SumUsage(canonical),
            runs,
            CreateProviderModelRows(canonical),
            CreateNodeRows(canonical));
    }

    public static WorkflowUsageAnalyticsTotals SumUsage(
        IEnumerable<WorkflowUsageObservation> observations)
    {
        var items = observations.ToArray();
        var usageKnownCount = items.Count(WorkflowUsageCompatibilityProjection.IsUsageKnown);
        var pricingKnownCount = items.Count(observation => observation.PricingStatus == WorkflowPricingStatus.Known);

        return new WorkflowUsageAnalyticsTotals(
            items.Length,
            usageKnownCount,
            items.Length - usageKnownCount,
            pricingKnownCount,
            items.Length - pricingKnownCount,
            items.Sum(observation => (long)observation.InputTokens),
            items.Sum(observation => (long)observation.CachedInputTokens),
            items.Sum(observation => (long)observation.OutputTokens),
            items.Sum(observation => (long)observation.ReasoningTokens),
            items.Sum(observation => (long)observation.TotalTokens),
            items.Sum(observation => (long)observation.ToolCallCount),
            decimal.Round(
                items
                    .Where(observation => observation.PricingStatus == WorkflowPricingStatus.Known)
                    .Sum(observation => observation.CostUsd ?? 0m),
                6,
                MidpointRounding.AwayFromZero));
    }

    private static IReadOnlyList<WorkflowUsageObservation> EnsureCanonicalFacts(
        IReadOnlyList<WorkflowUsageObservation> observations)
    {
        var canonical = new List<WorkflowUsageObservation>(observations.Count);
        foreach (var group in observations.GroupBy(observation => observation.Id))
        {
            var fact = group.First();
            if (group.Any(candidate => candidate != fact))
            {
                throw new WorkflowUsageObservationConflictException(group.Key);
            }

            canonical.Add(fact);
        }

        return canonical;
    }

    private static IReadOnlyList<WorkflowProviderModelAnalyticsRow> CreateProviderModelRows(
        IReadOnlyList<WorkflowUsageObservation> observations)
        => observations
            .GroupBy(observation => (
                ProviderName: observation.ProviderName.ToUpperInvariant(),
                observation.ProviderKind,
                Model: observation.Model.ToUpperInvariant()))
            .Select(group =>
            {
                var display = group.First();
                return new WorkflowProviderModelAnalyticsRow(
                    display.ProviderName,
                    display.ProviderKind,
                    display.Model,
                    SumUsage(group));
            })
            .OrderBy(row => row.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Model, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IReadOnlyList<WorkflowNodeUsageAnalyticsRow> CreateNodeRows(
        IReadOnlyList<WorkflowUsageObservation> observations)
        => observations
            .GroupBy(observation => (observation.NodeId, observation.ExecutorId))
            .Select(group => new WorkflowNodeUsageAnalyticsRow(
                group.Key.NodeId,
                group.Key.ExecutorId,
                SumUsage(group)))
            .OrderBy(row => row.NodeId.Value, StringComparer.Ordinal)
            .ThenBy(row => row.ExecutorId?.Value, StringComparer.Ordinal)
            .ToArray();
}
