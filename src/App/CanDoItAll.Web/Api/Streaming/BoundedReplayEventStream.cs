using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api.Streaming;

public interface IBoundedReplayEventReader<T>
{
    TimeSpan HeartbeatInterval { get; }

    ValueTask<BoundedReplayReadResult<T>> ReadAsync(
        long afterExclusive,
        CancellationToken cancellationToken);
}

public sealed class BoundedReplayEventStream<T> : IBoundedReplayEventReader<T>
{
    private readonly object sync = new();
    private readonly SequencedServerEvent<T>[] entries;
    private readonly int maxBatchSize;
    private TaskCompletionSource<long>? changed;
    private long latestSequence;
    private int retainedCount;

    [ActivatorUtilitiesConstructor]
    public BoundedReplayEventStream(IOptions<ApiAccessOptions> options)
        : this(
            options.Value.ServerSentEvents.ReplayCapacity,
            options.Value.ServerSentEvents.MaxBatchSize,
            options.Value.ServerSentEvents.HeartbeatInterval)
    {
    }

    public BoundedReplayEventStream(
        int replayCapacity,
        int maxBatchSize,
        TimeSpan heartbeatInterval)
        : this(
            replayCapacity,
            maxBatchSize,
            heartbeatInterval,
            initialSequence: 0)
    {
    }

    internal BoundedReplayEventStream(
        int replayCapacity,
        int maxBatchSize,
        TimeSpan heartbeatInterval,
        long initialSequence)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(replayCapacity);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatchSize);
        ArgumentOutOfRangeException.ThrowIfNegative(initialSequence);
        if (maxBatchSize > replayCapacity)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxBatchSize),
                maxBatchSize,
                "The maximum batch size cannot exceed the replay capacity.");
        }

        if (heartbeatInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(heartbeatInterval),
                heartbeatInterval,
                "The heartbeat interval must be greater than zero.");
        }

        entries = new SequencedServerEvent<T>[replayCapacity];
        this.maxBatchSize = maxBatchSize;
        latestSequence = initialSequence;
        HeartbeatInterval = heartbeatInterval;
    }

    public TimeSpan HeartbeatInterval { get; }

    public long Publish(T value)
    {
        ArgumentNullException.ThrowIfNull(value);

        TaskCompletionSource<long>? completedSignal;
        long sequence;
        lock (sync)
        {
            sequence = checked(latestSequence + 1);
            latestSequence = sequence;
            entries[GetIndex(sequence)] = new SequencedServerEvent<T>(sequence, value);
            retainedCount = Math.Min(entries.Length, retainedCount + 1);
            completedSignal = changed;
            changed = null;
        }

        if (completedSignal is not null &&
            !ThreadPool.UnsafeQueueUserWorkItem(
                static signal => signal.TrySetResult(0),
                completedSignal,
                preferLocal: false))
        {
            completedSignal.TrySetResult(sequence);
        }

        return sequence;
    }

    public async ValueTask<BoundedReplayReadResult<T>> ReadAsync(
        long afterExclusive,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(afterExclusive);

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            TaskCompletionSource<long> changeSignal;
            lock (sync)
            {
                var result = TryRead(afterExclusive);
                if (result is not null)
                {
                    return result;
                }

                changed ??= CreateChangeSignal();
                changeSignal = changed;
            }

            await changeSignal.Task
                .WaitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private BoundedReplayReadResult<T>? TryRead(long afterExclusive)
    {
        if (retainedCount == 0)
        {
            return afterExclusive == latestSequence
                ? null
                : afterExclusive > latestSequence
                    ? CreateFutureCursorGap(afterExclusive)
                    : new BoundedReplayReadResult<T>(
                        [],
                        new ReplayGap(
                            ReplayGapReason.CursorBeforeRetention,
                            afterExclusive,
                            latestSequence + 1,
                            latestSequence,
                            latestSequence));
        }

        if (afterExclusive > latestSequence)
        {
            return CreateFutureCursorGap(afterExclusive);
        }

        if (afterExclusive == latestSequence)
        {
            return null;
        }

        var firstAvailableSequence = latestSequence - retainedCount + 1;
        ReplayGap? gap = null;
        var firstSequenceToRead = afterExclusive + 1;
        if (afterExclusive < firstAvailableSequence - 1)
        {
            gap = new ReplayGap(
                ReplayGapReason.CursorBeforeRetention,
                afterExclusive,
                firstAvailableSequence,
                latestSequence,
                firstAvailableSequence - 1);
            firstSequenceToRead = firstAvailableSequence;
        }

        if (firstSequenceToRead > latestSequence)
        {
            return gap is null
                ? null
                : new BoundedReplayReadResult<T>([], gap);
        }

        var count = (int)Math.Min(maxBatchSize, latestSequence - firstSequenceToRead + 1);
        var batch = new SequencedServerEvent<T>[count];
        for (var index = 0; index < count; index++)
        {
            var sequence = firstSequenceToRead + index;
            var entry = entries[GetIndex(sequence)];
            if (entry.Sequence != sequence)
            {
                throw new InvalidOperationException(
                    $"Replay entry '{sequence}' was not available inside the retained window.");
            }

            batch[index] = entry;
        }

        return new BoundedReplayReadResult<T>(batch, gap);
    }

    private BoundedReplayReadResult<T> CreateFutureCursorGap(long afterExclusive)
    {
        var firstAvailableSequence = retainedCount == 0
            ? latestSequence + 1
            : latestSequence - retainedCount + 1;
        return new BoundedReplayReadResult<T>(
            [],
            new ReplayGap(
                ReplayGapReason.CursorAheadOfStream,
                afterExclusive,
                firstAvailableSequence,
                latestSequence,
                latestSequence));
    }

    private int GetIndex(long sequence)
    {
        return (int)((sequence - 1) % entries.Length);
    }

    private static TaskCompletionSource<long> CreateChangeSignal()
    {
        return new TaskCompletionSource<long>(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}

public readonly record struct SequencedServerEvent<T>(
    long Sequence,
    T Value);

public sealed record BoundedReplayReadResult<T>(
    IReadOnlyList<SequencedServerEvent<T>> Events,
    ReplayGap? Gap);

public sealed record ReplayGap(
    ReplayGapReason Reason,
    long RequestedAfterSequence,
    long FirstAvailableSequence,
    long LastAvailableSequence,
    long ResumeAfterSequence);

public enum ReplayGapReason
{
    CursorBeforeRetention = 0,
    CursorAheadOfStream = 1
}
