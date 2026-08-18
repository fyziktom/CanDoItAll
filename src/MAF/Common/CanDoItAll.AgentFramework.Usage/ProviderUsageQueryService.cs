namespace CanDoItAll.AgentFramework.Usage;

public sealed class ProviderUsageQueryService(IEnumerable<IProviderUsageProjectionSource> sources)
{
    private readonly IReadOnlyList<IProviderUsageProjectionSource> _sources = sources?.ToList()
        ?? throw new ArgumentNullException(nameof(sources));

    public async ValueTask<ProviderUsageSnapshot> QueryAsync(
        ProviderUsageWorkloadSelection selection,
        CancellationToken cancellationToken = default)
    {
        selection.Validate();
        var selectedSources = _sources
            .Where(source => selection.Includes(source.WorkloadKind))
            .OrderBy(source => source.SourceName, StringComparer.Ordinal)
            .ToList();

        var readTasks = new Task<ProviderUsageSourceResult>[selectedSources.Count];
        for (var index = 0; index < selectedSources.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            readTasks[index] = ReadSourceAsync(selectedSources[index], cancellationToken);
        }

        var sourceResults = await Task.WhenAll(readTasks).ConfigureAwait(false);

        var contributions = Deduplicate(sourceResults
                .Where(result => result.State != ProviderUsageSourceState.Failed)
                .SelectMany(result => result.Contributions))
            .Where(contribution => selection.Includes(contribution.WorkloadKind))
            .ToList();

        var statuses = sourceResults
            .Select(result => new ProviderUsageSourceStatus(
                result.SourceName,
                result.WorkloadKind,
                result.State,
                result.UpdatedAtUtc,
                result.Error))
            .ToList();
        var updatedAtUtc = statuses.Count == 0
            ? DateTimeOffset.UnixEpoch
            : statuses.Max(status => status.UpdatedAtUtc);

        return new ProviderUsageSnapshot(
            selection,
            Summarize(contributions),
            BuildConsumers(contributions),
            BuildProviders(contributions),
            BuildModels(contributions),
            statuses,
            updatedAtUtc);
    }

    private static async Task<ProviderUsageSourceResult> ReadSourceAsync(
        IProviderUsageProjectionSource source,
        CancellationToken cancellationToken)
    {
        var result = await source.ReadAsync(cancellationToken).ConfigureAwait(false);
        ValidateSourceResult(source, result);
        return result;
    }

    private static void ValidateSourceResult(
        IProviderUsageProjectionSource source,
        ProviderUsageSourceResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (!string.Equals(source.SourceName, result.SourceName, StringComparison.Ordinal) ||
            source.WorkloadKind != result.WorkloadKind)
        {
            throw new InvalidOperationException($"Usage source '{source.SourceName}' returned mismatched source metadata.");
        }

        if (result.State == ProviderUsageSourceState.Failed && result.Error is null)
        {
            throw new InvalidOperationException($"Failed usage source '{source.SourceName}' must provide an error.");
        }
    }

    private static IReadOnlyList<ProviderUsageContribution> Deduplicate(
        IEnumerable<ProviderUsageContribution> contributions)
    {
        var seen = new Dictionary<
            (ProviderUsageWorkloadKind WorkloadKind, string ContributionId),
            ProviderUsageContribution>();
        var deduplicated = new List<ProviderUsageContribution>();
        foreach (var contribution in contributions)
        {
            var key = (contribution.WorkloadKind, contribution.ContributionId);
            if (seen.TryGetValue(key, out var existing))
            {
                if (existing != contribution)
                {
                    throw new InvalidOperationException(
                        $"Usage contribution '{key.WorkloadKind}:{key.ContributionId}' has conflicting values.");
                }

                continue;
            }

            seen.Add(key, contribution);
            deduplicated.Add(contribution with { Tokens = contribution.Tokens.Normalize() });
        }

        return deduplicated;
    }

    private static IReadOnlyList<ProviderUsageConsumerRow> BuildConsumers(
        IReadOnlyList<ProviderUsageContribution> contributions)
    {
        return contributions
            .GroupBy(item => (item.ConsumerKind, item.ConsumerId, item.ConsumerName))
            .Select(group => new ProviderUsageConsumerRow(
                group.Key.ConsumerKind,
                group.Key.ConsumerId,
                group.Key.ConsumerName,
                Summarize(group),
                group.Max(item => (DateTimeOffset?)item.OccurredAtUtc)))
            .OrderByDescending(row => row.Totals.KnownCostUsd)
            .ThenByDescending(row => row.Totals.Tokens.TotalTokens)
            .ThenBy(row => row.ConsumerName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ProviderUsageProviderRow> BuildProviders(
        IReadOnlyList<ProviderUsageContribution> contributions)
    {
        return contributions
            .GroupBy(item => (item.ProviderProfileId, item.ProviderName, item.ProviderKind))
            .Select(group => new ProviderUsageProviderRow(
                group.Key.ProviderProfileId,
                group.Key.ProviderName,
                group.Key.ProviderKind,
                Summarize(group),
                group.Max(item => (DateTimeOffset?)item.OccurredAtUtc)))
            .OrderByDescending(row => row.Totals.KnownCostUsd)
            .ThenBy(row => row.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<ProviderUsageModelRow> BuildModels(
        IReadOnlyList<ProviderUsageContribution> contributions)
    {
        return contributions
            .GroupBy(item => (item.ProviderProfileId, item.ProviderName, item.ProviderKind, item.Model))
            .Select(group => new ProviderUsageModelRow(
                group.Key.ProviderProfileId,
                group.Key.ProviderName,
                group.Key.ProviderKind,
                group.Key.Model,
                Summarize(group),
                group.Max(item => (DateTimeOffset?)item.OccurredAtUtc)))
            .OrderByDescending(row => row.Totals.KnownCostUsd)
            .ThenBy(row => row.ProviderName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Model, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ProviderUsageTotals Summarize(IEnumerable<ProviderUsageContribution> source)
    {
        var executionOutcomes = new Dictionary<
            (ProviderUsageWorkloadKind WorkloadKind, string ExecutionId),
            ProviderUsageExecutionOutcome>();
        var usageObservationCount = 0;
        var knownUsageObservationCount = 0;
        var pricedObservationCount = 0;
        var inputTokens = 0;
        var cachedInputTokens = 0;
        var cacheWriteTokens = 0;
        var outputTokens = 0;
        var reasoningTokens = 0;
        var totalTokens = 0;
        var knownCostUsd = 0m;
        foreach (var item in source)
        {
            usageObservationCount++;
            AccumulateExecutionOutcome(executionOutcomes, item);
            if (!IsKnownUsage(item))
            {
                continue;
            }

            knownUsageObservationCount++;
            checked
            {
                inputTokens += item.Tokens.InputTokens;
                cachedInputTokens += item.Tokens.CachedInputTokens;
                cacheWriteTokens += item.Tokens.CacheWriteTokens;
                outputTokens += item.Tokens.OutputTokens;
                reasoningTokens += item.Tokens.ReasoningTokens;
                totalTokens += item.Tokens.TotalTokens;
            }

            if (item.PricingCompleteness == ProviderUsagePricingCompleteness.Unpriced ||
                item.CostUsd is not >= 0m)
            {
                continue;
            }

            pricedObservationCount++;
            knownCostUsd += item.CostUsd.Value;
        }

        var failedExecutionCount = 0;
        var cancelledExecutionCount = 0;
        foreach (var outcome in executionOutcomes.Values)
        {
            failedExecutionCount += outcome == ProviderUsageExecutionOutcome.Failed ? 1 : 0;
            cancelledExecutionCount += outcome == ProviderUsageExecutionOutcome.Cancelled ? 1 : 0;
        }

        return new ProviderUsageTotals(
            executionOutcomes.Count,
            failedExecutionCount,
            cancelledExecutionCount,
            usageObservationCount,
            knownUsageObservationCount,
            usageObservationCount - knownUsageObservationCount,
            pricedObservationCount,
            knownUsageObservationCount - pricedObservationCount,
            new ProviderUsageTokenCounts(
                inputTokens,
                cachedInputTokens,
                cacheWriteTokens,
                outputTokens,
                reasoningTokens,
                totalTokens),
            decimal.Round(knownCostUsd, 6, MidpointRounding.AwayFromZero));
    }

    private static bool IsKnownUsage(ProviderUsageContribution contribution)
    {
        return contribution.UsageCompleteness is ProviderUsageCompleteness.Observed
            or ProviderUsageCompleteness.LegacyKnownTokens;
    }

    private static void AccumulateExecutionOutcome(
        IDictionary<
            (ProviderUsageWorkloadKind WorkloadKind, string ExecutionId),
            ProviderUsageExecutionOutcome> executionOutcomes,
        ProviderUsageContribution contribution)
    {
        if (string.IsNullOrWhiteSpace(contribution.ExecutionId))
        {
            return;
        }

        var key = (contribution.WorkloadKind, contribution.ExecutionId);
        if (!executionOutcomes.TryGetValue(key, out var currentOutcome))
        {
            executionOutcomes.Add(key, contribution.ExecutionOutcome);
            return;
        }

        if (currentOutcome == ProviderUsageExecutionOutcome.Unknown)
        {
            executionOutcomes[key] = contribution.ExecutionOutcome;
            return;
        }

        if (contribution.ExecutionOutcome != ProviderUsageExecutionOutcome.Unknown &&
            contribution.ExecutionOutcome != currentOutcome)
        {
            throw new InvalidOperationException(
                $"Usage execution '{key.WorkloadKind}:{key.ExecutionId}' has conflicting terminal outcomes.");
        }
    }
}
