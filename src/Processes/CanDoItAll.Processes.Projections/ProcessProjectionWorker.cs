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

public sealed record ProcessProjectionReplayBatch(
    long StartingGlobalSequence,
    IReadOnlyList<ProcessStoredRuntimeEvent> RuntimeEvents);

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
        ValidateRequest(request);

        var offset = await projectionStore
            .LoadOffsetAsync(request.ProjectorName, request.ShardKey, cancellationToken)
            .ConfigureAwait(false);
        var lastProcessed = offset?.GlobalSequence ?? 0;
        var runtimeEvents = await replayStore
            .ReadAfterGlobalSequenceAsync(lastProcessed, request.Take, cancellationToken)
            .ConfigureAwait(false);

        return await ReplayBatchAsync(
            request,
            new ProcessProjectionReplayBatch(lastProcessed, runtimeEvents),
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<ProcessProjectionReplayResult> ReplayBatchAsync(
        ProcessProjectionReplayRequest request,
        ProcessProjectionReplayBatch batch,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        ArgumentNullException.ThrowIfNull(batch);
        ArgumentNullException.ThrowIfNull(batch.RuntimeEvents);

        if (batch.RuntimeEvents.Count > request.Take)
        {
            throw new ArgumentException(
                $"Replay batch contains {batch.RuntimeEvents.Count} events, exceeding the requested take of {request.Take}.",
                nameof(batch));
        }

        ValidateBatchOrdering(batch);

        var lastProcessed = batch.StartingGlobalSequence;
        var processed = 0;

        foreach (var runtimeEvent in batch.RuntimeEvents)
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

    private void ValidateRequest(ProcessProjectionReplayRequest request)
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
    }

    private static void ValidateBatchOrdering(ProcessProjectionReplayBatch batch)
    {
        var previousGlobalSequence = batch.StartingGlobalSequence;
        foreach (var runtimeEvent in batch.RuntimeEvents)
        {
            if (runtimeEvent.GlobalSequence <= previousGlobalSequence)
            {
                throw new ArgumentException(
                    "Replay batch events must be strictly ordered after the starting global sequence.",
                    nameof(batch));
            }

            previousGlobalSequence = runtimeEvent.GlobalSequence;
        }
    }

    private static int CalculateBacklog(long latestKnownGlobalSequence, long lastProcessedGlobalSequence)
    {
        var backlog = latestKnownGlobalSequence - lastProcessedGlobalSequence;
        return backlog <= 0 ? 0 : checked((int)backlog);
    }
}
