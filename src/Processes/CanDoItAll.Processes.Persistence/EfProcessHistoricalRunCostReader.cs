using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Persistence;

public sealed class EfProcessHistoricalRunCostReader(
    IProcessRunRecordStore runRecordStore) : IProcessHistoricalRunCostReader
{
    private const int MaximumTakeRuns = 20;

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
        var page = await runRecordStore
            .ListAsync(
                new ProcessRunRecordListQuery(takeRuns)
                {
                    Payload = ProcessRunRecordListPayload.Compact,
                    DefinitionId = query.DefinitionId,
                    Disposition = ProcessRunDisposition.Succeeded,
                    RootRunsOnly = true,
                    EndedFromUtc = fromUtc,
                    EndedBeforeUtc = observedAtUtc == DateTimeOffset.MaxValue
                        ? observedAtUtc
                        : observedAtUtc.AddTicks(1)
                },
                cancellationToken)
            .ConfigureAwait(false);

        if (page.Records.Count == 0)
        {
            return ProcessHistoricalRunCostEstimate.Empty(query.DefinitionId, query.DefinitionKey);
        }

        var samplesWithPricing = page.Records
            .Select(record => new
            {
                Sample = new ProcessHistoricalRunCostSample(
                    record.Identity.RunId,
                    record.Metrics.EndedAtUtc,
                    record.Metrics.ExecutionCount,
                    decimal.Round(record.Metrics.ActualCost, 6, MidpointRounding.AwayFromZero)),
                IsPriced = record.AvailableEvidenceSources.HasFlag(ProcessRunEvidenceSource.Pricing)
            })
            .ToArray();
        var samples = samplesWithPricing
            .Select(item => item.Sample)
            .ToArray();
        var pricedSamples = samplesWithPricing
            .Where(item => item.IsPriced)
            .Select(item => item.Sample)
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
            samples.Length,
            pricedSamples.Length,
            averageActualCostUsd,
            samples);
    }

    private static DateTimeOffset NormalizeUtc(DateTimeOffset value)
    {
        return value.Offset == TimeSpan.Zero ? value : value.ToUniversalTime();
    }
}
