using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessHistoricalRunCostReader(
    ProcessPersistenceDbContext dbContext,
    IProcessRuntimeUsageTelemetryReader usageTelemetryReader) : IProcessHistoricalRunCostReader
{
    private const int MaximumTakeRuns = 20;
    private const int UsageTelemetryTakePerRun = 200;

    public async ValueTask<ProcessHistoricalRunCostEstimate> ReadAsync(
        ProcessHistoricalRunCostQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (string.IsNullOrWhiteSpace(query.DefinitionKey))
        {
            throw new ArgumentException("A process definition key is required for historical cost lookup.", nameof(query));
        }

        if (query.ObservedAtUtc == default)
        {
            throw new ArgumentException("An observation timestamp is required for historical cost lookup.", nameof(query));
        }

        var observedAtUtc = NormalizeUtc(query.ObservedAtUtc);
        var fromUtc = query.FromUtc.HasValue
            ? NormalizeUtc(query.FromUtc.Value)
            : DateTimeOffset.UnixEpoch;
        if (fromUtc >= observedAtUtc)
        {
            throw new ArgumentException("Historical cost lookup must have FromUtc earlier than ObservedAtUtc.", nameof(query));
        }

        var takeRuns = Math.Clamp(query.TakeRuns, 1, MaximumTakeRuns);
        var candidates = await (
                from state in dbContext.RuntimeStates.AsNoTracking()
                join plan in dbContext.InstancePlans.AsNoTracking()
                    on state.PlanId equals plan.PlanId
                where plan.DefinitionId == query.DefinitionId.Value &&
                      state.Status == ProcessRuntimeStatus.Completed &&
                      state.UpdatedAtUtc >= fromUtc &&
                      state.UpdatedAtUtc <= observedAtUtc
                orderby state.UpdatedAtUtc descending
                select new HistoricalRunCandidate(state.RunId, state.RootRunId, state.UpdatedAtUtc))
            .Take(takeRuns)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        if (candidates.Length == 0)
        {
            return ProcessHistoricalRunCostEstimate.Empty(query.DefinitionId, query.DefinitionKey);
        }

        var runIdsByCandidate = await ResolveRunIdsByCandidateAsync(candidates, cancellationToken).ConfigureAwait(false);
        var usageRunIds = runIdsByCandidate.Values
            .SelectMany(runIds => runIds)
            .Distinct()
            .ToArray();
        var usageObservations = usageRunIds.Length == 0
            ? []
            : await usageTelemetryReader.ListAsync(
                    new ProcessRuntimeUsageTelemetryQuery(
                        usageRunIds,
                        fromUtc,
                        observedAtUtc,
                        UsageTelemetryTakePerRun),
                    cancellationToken)
                .ConfigureAwait(false);
        var usageByRunId = usageObservations
            .GroupBy(observation => observation.RunId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        var samples = new List<ProcessHistoricalRunCostSample>(candidates.Length);
        foreach (var candidate in candidates)
        {
            var sampleObservations = runIdsByCandidate[candidate.RunId]
                .SelectMany(runId => usageByRunId.TryGetValue(runId, out var observations) ? observations : [])
                .ToArray();
            samples.Add(new ProcessHistoricalRunCostSample(
                new ProcessRunId(candidate.RunId),
                candidate.CompletedAtUtc,
                sampleObservations.Length,
                decimal.Round(sampleObservations.Sum(observation => observation.ActualCostUsd), 6, MidpointRounding.AwayFromZero)));
        }

        var pricedSamples = samples
            .Where(sample => sample.ActualCostUsd > 0m)
            .ToArray();
        var averageActualCostUsd = pricedSamples.Length == 0
            ? 0m
            : decimal.Round(
                pricedSamples.Sum(sample => sample.ActualCostUsd) / pricedSamples.Length,
                6,
                MidpointRounding.AwayFromZero);

        return new ProcessHistoricalRunCostEstimate(
            query.DefinitionId,
            query.DefinitionKey.Trim(),
            candidates.Length,
            pricedSamples.Length,
            averageActualCostUsd,
            samples);
    }

    private async Task<Dictionary<Guid, HashSet<ProcessRunId>>> ResolveRunIdsByCandidateAsync(
        IReadOnlyList<HistoricalRunCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var rootCandidateIds = candidates
            .Where(candidate => candidate.RunId == candidate.RootRunId)
            .Select(candidate => candidate.RunId)
            .Distinct()
            .ToArray();
        var descendantRows = rootCandidateIds.Length == 0
            ? []
            : await dbContext.RuntimeStates
                .AsNoTracking()
                .Where(state => rootCandidateIds.Contains(state.RootRunId))
                .Select(state => new { state.RootRunId, state.RunId })
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);

        var descendantsByRootId = descendantRows
            .GroupBy(row => row.RootRunId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(row => new ProcessRunId(row.RunId))
                    .ToHashSet());
        var runIdsByCandidate = new Dictionary<Guid, HashSet<ProcessRunId>>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var runIds = new HashSet<ProcessRunId> { new(candidate.RunId) };
            if (candidate.RunId == candidate.RootRunId &&
                descendantsByRootId.TryGetValue(candidate.RunId, out var descendantRunIds))
            {
                runIds.UnionWith(descendantRunIds);
            }

            runIdsByCandidate[candidate.RunId] = runIds;
        }

        return runIdsByCandidate;
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
    }

    private sealed record HistoricalRunCandidate(
        Guid RunId,
        Guid RootRunId,
        DateTimeOffset CompletedAtUtc);
}
