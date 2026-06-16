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

    public async Task<ProcessProjectionReplayResult> CatchUpAsync(CancellationToken cancellationToken = default)
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
            .ReplayAsync(
                new ProcessProjectionReplayRequest(
                    projector.ProjectorName,
                    DefaultShard,
                    ReplayBatchSize,
                    latestKnownGlobalSequence),
                cancellationToken)
            .ConfigureAwait(false);
    }
}
