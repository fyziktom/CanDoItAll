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

        var sourceResults = new List<ProviderUsageSourceResult>(selectedSources.Count);
        foreach (var source in selectedSources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await source.ReadAsync(cancellationToken);
            ValidateSourceResult(source, result);
            sourceResults.Add(result);
        }

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

    private static IEnumerable<ProviderUsageContribution> Deduplicate(
        IEnumerable<ProviderUsageContribution> contributions)
    {
        foreach (var group in contributions.GroupBy(
                     contribution => (contribution.WorkloadKind, contribution.ContributionId),
                     EqualityComparer<(ProviderUsageWorkloadKind, string)>.Default))
        {
            var first = group.First();
            if (group.Skip(1).Any(duplicate => duplicate != first))
            {
                throw new InvalidOperationException(
                    $"Usage contribution '{first.WorkloadKind}:{first.ContributionId}' has conflicting values.");
            }

            yield return first with { Tokens = first.Tokens.Normalize() };
        }
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
        var items = source.ToList();
        var knownUsage = items.Where(IsKnownUsage).ToList();
        var executionOutcomes = items
            .Where(item => !string.IsNullOrWhiteSpace(item.ExecutionId))
            .GroupBy(item => (item.WorkloadKind, item.ExecutionId))
            .Select(ResolveExecutionOutcome)
            .ToList();
        var priced = knownUsage.Where(item =>
            item.PricingCompleteness != ProviderUsagePricingCompleteness.Unpriced &&
            item.CostUsd is >= 0m).ToList();

        return new ProviderUsageTotals(
            executionOutcomes.Count,
            executionOutcomes.Count(outcome => outcome == ProviderUsageExecutionOutcome.Failed),
            executionOutcomes.Count(outcome => outcome == ProviderUsageExecutionOutcome.Cancelled),
            items.Count,
            knownUsage.Count,
            items.Count - knownUsage.Count,
            priced.Count,
            knownUsage.Count - priced.Count,
            new ProviderUsageTokenCounts(
                knownUsage.Sum(item => item.Tokens.InputTokens),
                knownUsage.Sum(item => item.Tokens.CachedInputTokens),
                knownUsage.Sum(item => item.Tokens.CacheWriteTokens),
                knownUsage.Sum(item => item.Tokens.OutputTokens),
                knownUsage.Sum(item => item.Tokens.ReasoningTokens),
                knownUsage.Sum(item => item.Tokens.TotalTokens)),
            decimal.Round(priced.Sum(item => item.CostUsd!.Value), 6, MidpointRounding.AwayFromZero));
    }

    private static bool IsKnownUsage(ProviderUsageContribution contribution)
    {
        return contribution.UsageCompleteness is ProviderUsageCompleteness.Observed
            or ProviderUsageCompleteness.LegacyKnownTokens;
    }

    private static ProviderUsageExecutionOutcome ResolveExecutionOutcome(
        IGrouping<(ProviderUsageWorkloadKind WorkloadKind, string ExecutionId), ProviderUsageContribution> group)
    {
        var knownOutcomes = group
            .Select(item => item.ExecutionOutcome)
            .Where(outcome => outcome != ProviderUsageExecutionOutcome.Unknown)
            .Distinct()
            .ToList();
        if (knownOutcomes.Count > 1)
        {
            throw new InvalidOperationException(
                $"Usage execution '{group.Key.WorkloadKind}:{group.Key.ExecutionId}' has conflicting terminal outcomes.");
        }

        return knownOutcomes.FirstOrDefault();
    }
}
