using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessRuntimeProjectionCatchupService(
    IProcessRuntimeEventReplayStore eventReplayStore,
    IProcessProjectionStore projectionStore,
    IProcessRuntimeProjector projector,
    IProcessProjectionClock clock)
{
    private const int ReplayBatchSize = 500;
    private static readonly ProcessProjectionShardKey DefaultShard = new("runtime-global");
    private static readonly SemaphoreSlim CatchUpSemaphore = new(1, 1);

    public async Task<ProcessProjectionReplayResult> CatchUpAsync(CancellationToken cancellationToken = default)
    {
        await CatchUpSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var offset = await projectionStore
                .LoadOffsetAsync(projector.ProjectorName, DefaultShard, cancellationToken)
                .ConfigureAwait(false);
            var latestEvents = await eventReplayStore
                .ReadAfterGlobalSequenceAsync(offset?.GlobalSequence ?? 0, ReplayBatchSize, cancellationToken)
                .ConfigureAwait(false);
            var latestKnownGlobalSequence = latestEvents.Count == 0
                ? offset?.GlobalSequence ?? 0
                : latestEvents[^1].GlobalSequence;
            var worker = new ProcessProjectionReplayWorker(eventReplayStore, projectionStore, projector, clock);

            return await worker
                .ReplayBatchAsync(
                    new ProcessProjectionReplayRequest(
                        projector.ProjectorName,
                        DefaultShard,
                        ReplayBatchSize,
                        latestKnownGlobalSequence),
                    new ProcessProjectionReplayBatch(
                        offset?.GlobalSequence ?? 0,
                        latestEvents),
                    cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            CatchUpSemaphore.Release();
        }
    }
}
