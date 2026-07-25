using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessRunRecordBackfillResult(
    int CandidateCount,
    int InsertedOrRevisedCount);

public sealed class ProcessRunRecordBackfillProcessor(
    IProcessRunRecordBackfillSource source,
    IProcessRunRecordStore store)
{
    public async Task<ProcessRunRecordBackfillResult> RunBatchAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take is <= 0 or > ProcessRunRecordPayloadLimits.MaximumClaimBatchSize)
        {
            throw new ArgumentOutOfRangeException(
                nameof(take),
                take,
                $"Process run record backfill size must be between 1 and {ProcessRunRecordPayloadLimits.MaximumClaimBatchSize}.");
        }

        var seeds = await source
            .ListMissingTerminalSeedsAsync(take, cancellationToken)
            .ConfigureAwait(false);
        var insertedOrRevisedCount = 0;
        foreach (var seed in seeds)
        {
            if (await store.UpsertSeedAsync(seed, cancellationToken).ConfigureAwait(false))
            {
                insertedOrRevisedCount++;
            }
        }

        return new ProcessRunRecordBackfillResult(seeds.Count, insertedOrRevisedCount);
    }
}
