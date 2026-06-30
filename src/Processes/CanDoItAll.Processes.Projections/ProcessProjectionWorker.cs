using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Projections;

public interface IProcessProjectionClock
{
    DateTimeOffset GetUtcNow();
}

public sealed class SystemProcessProjectionClock : IProcessProjectionClock
{
    public DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow;
}

public sealed record ProcessProjectionExecutionContext(
    ProcessProjectionShardKey ShardKey,
    DateTimeOffset ObservedAtUtc,
    long LatestKnownGlobalSequence);

public interface IProcessRuntimeProjector
{
    ProcessProjectorName ProjectorName { get; }

    Task ProjectAsync(
        ProcessStoredRuntimeEvent runtimeEvent,
        ProcessProjectionExecutionContext context,
        CancellationToken cancellationToken = default);
}

public sealed record ProcessProjectionReplayRequest(
    ProcessProjectorName ProjectorName,
    ProcessProjectionShardKey ShardKey,
    int Take,
    long LatestKnownGlobalSequence);

public enum ProcessProjectionReplayStatus
{
    Completed,
    DeadLettered
}

public sealed record ProcessProjectionReplayResult(
    ProcessProjectionReplayStatus Status,
    int ProcessedCount,
    long LastProcessedGlobalSequence,
    int BacklogEventCount);

public sealed class ProcessProjectionReplayWorker(
    IProcessRuntimeEventReplayStore replayStore,
    IProcessProjectionStore projectionStore,
    IProcessRuntimeProjector projector,
    IProcessProjectionClock clock)
{
    public async Task<ProcessProjectionReplayResult> ReplayAsync(
        ProcessProjectionReplayRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.Take <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Take), request.Take, "Replay batch size must be positive.");
        }

        if (projector.ProjectorName != request.ProjectorName)
        {
            throw new InvalidOperationException(
                $"Replay request targets projector '{request.ProjectorName}', but worker was created for '{projector.ProjectorName}'.");
        }

        var offset = await projectionStore
            .LoadOffsetAsync(request.ProjectorName, request.ShardKey, cancellationToken)
            .ConfigureAwait(false);
        var lastProcessed = offset?.GlobalSequence ?? 0;
        var runtimeEvents = await replayStore
            .ReadAfterGlobalSequenceAsync(lastProcessed, request.Take, cancellationToken)
            .ConfigureAwait(false);
        var processed = 0;

        foreach (var runtimeEvent in runtimeEvents)
        {
            var observedAtUtc = clock.GetUtcNow();
            var context = new ProcessProjectionExecutionContext(
                request.ShardKey,
                observedAtUtc,
                request.LatestKnownGlobalSequence);

            try
            {
                await projector.ProjectAsync(runtimeEvent, context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                await projectionStore
                    .WriteDeadLetterAsync(
                        new ProcessProjectionDeadLetter(
                            ProcessProjectionDeadLetterId.New(),
                            request.ProjectorName,
                            request.ShardKey,
                            runtimeEvent.Envelope.EventId,
                            runtimeEvent.GlobalSequence,
                            exception.GetType().Name,
                            $"runtime-event:{runtimeEvent.Envelope.EventId}",
                            "manual-review",
                            observedAtUtc),
                        cancellationToken)
                    .ConfigureAwait(false);

                return new ProcessProjectionReplayResult(
                    ProcessProjectionReplayStatus.DeadLettered,
                    processed,
                    lastProcessed,
                    CalculateBacklog(request.LatestKnownGlobalSequence, lastProcessed));
            }

            processed++;
            lastProcessed = runtimeEvent.GlobalSequence;
            await projectionStore
                .SaveOffsetAsync(
                    new ProcessProjectorOffset(request.ProjectorName, request.ShardKey, lastProcessed, observedAtUtc),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        return new ProcessProjectionReplayResult(
            ProcessProjectionReplayStatus.Completed,
            processed,
            lastProcessed,
            CalculateBacklog(request.LatestKnownGlobalSequence, lastProcessed));
    }

    private static int CalculateBacklog(long latestKnownGlobalSequence, long lastProcessedGlobalSequence)
    {
        var backlog = latestKnownGlobalSequence - lastProcessedGlobalSequence;
        return backlog <= 0 ? 0 : checked((int)backlog);
    }
}
