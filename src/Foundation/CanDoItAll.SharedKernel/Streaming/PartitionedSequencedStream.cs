namespace CanDoItAll.SharedKernel.Streaming;

public sealed class PartitionedSequencedStream<TKey, TEvent>
    where TKey : notnull
{
    private readonly Lock lifecycleLock = new();
    private readonly Dictionary<TKey, PartitionState> partitions = [];
    private readonly Dictionary<TKey, Tombstone> tombstones = [];
    private readonly PartitionedSequencedStreamPolicy policy;
    private readonly TimeProvider timeProvider;
    private long completionOrder;

    public PartitionedSequencedStream(
        PartitionedSequencedStreamPolicy policy,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(policy);
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.policy = policy;
        this.timeProvider = timeProvider;
    }

    public StreamPartitionAdmissionOutcome Admit(TKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        List<TaskCompletionSource<bool>> signals = [];
        StreamPartitionAdmissionOutcome outcome;
        lock (lifecycleLock)
        {
            var now = timeProvider.GetUtcNow();
            CleanupExpiredUnderLock(now, signals);
            if (partitions.TryGetValue(key, out var existing))
            {
                lock (existing.Gate)
                {
                    outcome = existing.IsCompleted
                        ? StreamPartitionAdmissionOutcome.AlreadyTerminal
                        : StreamPartitionAdmissionOutcome.AlreadyActive;
                }
            }
            else if (tombstones.ContainsKey(key))
            {
                outcome = StreamPartitionAdmissionOutcome.PreviouslyEvicted;
            }
            else
            {
                MakePartitionCapacityUnderLock(now, signals);
                if (partitions.Count >= policy.MaxPartitions)
                {
                    outcome = StreamPartitionAdmissionOutcome.CapacityExhausted;
                }
                else
                {
                    partitions.Add(key, new PartitionState());
                    outcome = StreamPartitionAdmissionOutcome.Admitted;
                }
            }
        }

        SignalReaders(signals);
        return outcome;
    }

    public SequencedStreamEnvelope<TEvent> Append(TKey key, TEvent @event)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(@event);
        var partition = ResolvePartitionForWrite(key);
        TaskCompletionSource<bool> signal;
        SequencedStreamEnvelope<TEvent> envelope;
        lock (partition.Gate)
        {
            EnsureWritable(partition);
            envelope = AppendUnderLock(partition, @event);
            signal = RotateSignalUnderLock(partition);
        }

        signal.TrySetResult(true);
        return envelope;
    }

    public SequencedStreamEnvelope<TEvent> Complete(TKey key, TEvent terminalEvent)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(terminalEvent);
        var partition = ResolvePartitionForWrite(key);
        TaskCompletionSource<bool> completionSignal;
        SequencedStreamEnvelope<TEvent> envelope;
        lock (partition.Gate)
        {
            EnsureWritable(partition);
            envelope = AppendUnderLock(partition, terminalEvent);
            partition.IsCompleted = true;
            partition.CompletedAtUtc = timeProvider.GetUtcNow();
            partition.CompletionOrder = Interlocked.Increment(ref completionOrder);
            completionSignal = RotateSignalUnderLock(partition);
        }

        completionSignal.TrySetResult(true);

        List<TaskCompletionSource<bool>> evictionSignals = [];
        lock (lifecycleLock)
        {
            var now = timeProvider.GetUtcNow();
            CleanupExpiredUnderLock(now, evictionSignals);
            EnforceTerminalCapacityUnderLock(now, evictionSignals);
        }

        SignalReaders(evictionSignals);
        return envelope;
    }

    public ISequencedStreamReader<TEvent> OpenReader(
        TKey key,
        StreamSequence fromInclusive)
    {
        ArgumentNullException.ThrowIfNull(key);
        return new Reader(this, key, fromInclusive);
    }

    private static void EnsureWritable(PartitionState partition)
    {
        if (partition.Eviction is not null)
        {
            throw new InvalidOperationException("The stream partition was evicted.");
        }

        if (partition.IsCompleted)
        {
            throw new InvalidOperationException("The stream partition is already complete.");
        }
    }

    private SequencedStreamEnvelope<TEvent> AppendUnderLock(
        PartitionState partition,
        TEvent @event)
    {
        var envelope = new SequencedStreamEnvelope<TEvent>(
            partition.NextSequence,
            @event);
        partition.NextSequence = partition.NextSequence.Next();
        partition.Events.Enqueue(envelope);
        while (partition.Events.Count > policy.MaxEventsPerPartition)
        {
            partition.Events.Dequeue();
        }

        return envelope;
    }

    private PartitionState ResolvePartitionForWrite(TKey key)
    {
        lock (lifecycleLock)
        {
            if (partitions.TryGetValue(key, out var partition))
            {
                return partition;
            }

            if (tombstones.ContainsKey(key))
            {
                throw new InvalidOperationException("The stream partition was evicted.");
            }

            throw new InvalidOperationException("The stream partition is unknown.");
        }
    }

    private async ValueTask<SequencedStreamReadResult<TEvent>> ReadAsync(
        TKey key,
        StreamSequence fromInclusive,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<TaskCompletionSource<bool>> cleanupSignals = [];
            PartitionState? partition;
            Tombstone? tombstone;
            lock (lifecycleLock)
            {
                CleanupExpiredUnderLock(timeProvider.GetUtcNow(), cleanupSignals);
                partitions.TryGetValue(key, out partition);
                tombstones.TryGetValue(key, out tombstone);
            }

            SignalReaders(cleanupSignals);
            if (partition is null)
            {
                return tombstone is null
                    ? new SequencedStreamUnknown<TEvent>()
                    : new SequencedStreamEvicted<TEvent>(
                        tombstone.Reason,
                        tombstone.EvictedAtUtc);
            }

            Task waitTask;
            lock (partition.Gate)
            {
                if (partition.Eviction is not null)
                {
                    return new SequencedStreamEvicted<TEvent>(
                        partition.Eviction.Reason,
                        partition.Eviction.EvictedAtUtc);
                }

                if (partition.Events.TryPeek(out var earliest) &&
                    fromInclusive.Value < earliest.Sequence.Value &&
                    (fromInclusive != StreamSequence.Beginning ||
                     earliest.Sequence != StreamSequence.First))
                {
                    return new SequencedStreamGap<TEvent>(
                        fromInclusive,
                        earliest.Sequence);
                }

                var items = partition.Events
                    .Where(item => item.Sequence.Value >= fromInclusive.Value)
                    .ToArray();
                if (items.Length > 0)
                {
                    return new SequencedStreamEvents<TEvent>(items);
                }

                if (partition.IsCompleted)
                {
                    return new SequencedStreamCompleted<TEvent>(
                        new StreamSequence(partition.NextSequence.Value - 1));
                }

                waitTask = partition.Signal.Task;
            }

            await waitTask.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private void CleanupExpiredUnderLock(
        DateTimeOffset now,
        List<TaskCompletionSource<bool>> signals)
    {
        var expiredTerminalKeys = partitions
            .Where(pair => IsTerminalExpired(pair.Value, now))
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var key in expiredTerminalKeys)
        {
            EvictPartitionUnderLock(
                key,
                StreamEvictionReason.TerminalRetentionExpired,
                now,
                signals);
        }

        var expiredTombstoneKeys = tombstones
            .Where(pair => now - pair.Value.EvictedAtUtc >= policy.TombstoneRetention)
            .Select(pair => pair.Key)
            .ToArray();
        foreach (var key in expiredTombstoneKeys)
        {
            tombstones.Remove(key);
        }
    }

    private bool IsTerminalExpired(
        PartitionState partition,
        DateTimeOffset now)
    {
        lock (partition.Gate)
        {
            return partition.IsCompleted &&
                partition.CompletedAtUtc.HasValue &&
                now - partition.CompletedAtUtc.Value >= policy.TerminalRetention;
        }
    }

    private void EnforceTerminalCapacityUnderLock(
        DateTimeOffset now,
        List<TaskCompletionSource<bool>> signals)
    {
        while (CountTerminalPartitionsUnderLock() > policy.MaxTerminalPartitions)
        {
            if (!TryFindOldestTerminalKeyUnderLock(out var key))
            {
                return;
            }

            EvictPartitionUnderLock(
                key,
                StreamEvictionReason.TerminalCapacityExceeded,
                now,
                signals);
        }
    }

    private void MakePartitionCapacityUnderLock(
        DateTimeOffset now,
        List<TaskCompletionSource<bool>> signals)
    {
        while (partitions.Count >= policy.MaxPartitions)
        {
            if (!TryFindOldestTerminalKeyUnderLock(out var key))
            {
                return;
            }

            EvictPartitionUnderLock(
                key,
                StreamEvictionReason.PartitionCapacityPressure,
                now,
                signals);
        }
    }

    private int CountTerminalPartitionsUnderLock()
    {
        var count = 0;
        foreach (var partition in partitions.Values)
        {
            lock (partition.Gate)
            {
                if (partition.IsCompleted)
                {
                    count++;
                }
            }
        }

        return count;
    }

    private bool TryFindOldestTerminalKeyUnderLock(out TKey key)
    {
        var found = false;
        var oldestOrder = long.MaxValue;
        key = default!;
        foreach (var pair in partitions)
        {
            lock (pair.Value.Gate)
            {
                if (!pair.Value.IsCompleted ||
                    pair.Value.CompletionOrder >= oldestOrder)
                {
                    continue;
                }

                found = true;
                oldestOrder = pair.Value.CompletionOrder;
                key = pair.Key;
            }
        }

        return found;
    }

    private void EvictPartitionUnderLock(
        TKey key,
        StreamEvictionReason reason,
        DateTimeOffset now,
        List<TaskCompletionSource<bool>> signals)
    {
        if (!partitions.TryGetValue(key, out var partition))
        {
            return;
        }

        lock (partition.Gate)
        {
            if (!partition.IsCompleted)
            {
                return;
            }

            partition.Eviction = new Tombstone(reason, now);
            partition.Events.Clear();
            signals.Add(RotateSignalUnderLock(partition));
        }

        partitions.Remove(key);
        tombstones[key] = new Tombstone(reason, now);
        TrimTombstonesUnderLock();
    }

    private void TrimTombstonesUnderLock()
    {
        while (tombstones.Count > policy.MaxTombstones)
        {
            var oldest = tombstones.MinBy(pair => pair.Value.EvictedAtUtc);
            tombstones.Remove(oldest.Key);
        }
    }

    private static TaskCompletionSource<bool> RotateSignalUnderLock(
        PartitionState partition)
    {
        var signal = partition.Signal;
        partition.Signal = CreateSignal();
        return signal;
    }

    private static TaskCompletionSource<bool> CreateSignal()
    {
        return new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static void SignalReaders(
        IEnumerable<TaskCompletionSource<bool>> signals)
    {
        foreach (var signal in signals)
        {
            signal.TrySetResult(true);
        }
    }

    private sealed class PartitionState
    {
        public Lock Gate { get; } = new();

        public Queue<SequencedStreamEnvelope<TEvent>> Events { get; } = [];

        public StreamSequence NextSequence { get; set; } = StreamSequence.First;

        public TaskCompletionSource<bool> Signal { get; set; } = CreateSignal();

        public bool IsCompleted { get; set; }

        public DateTimeOffset? CompletedAtUtc { get; set; }

        public long CompletionOrder { get; set; }

        public Tombstone? Eviction { get; set; }
    }

    private sealed record Tombstone(
        StreamEvictionReason Reason,
        DateTimeOffset EvictedAtUtc);

    private sealed class Reader(
        PartitionedSequencedStream<TKey, TEvent> owner,
        TKey key,
        StreamSequence fromInclusive)
        : ISequencedStreamReader<TEvent>
    {
        private readonly Lock stateLock = new();
        private readonly SemaphoreSlim readLock = new(1, 1);
        private readonly CancellationTokenSource disposal = new();
        private StreamSequence nextSequence = fromInclusive;
        private bool disposed;

        public StreamSequence NextSequence
        {
            get
            {
                lock (stateLock)
                {
                    return nextSequence;
                }
            }
        }

        public async ValueTask<SequencedStreamReadResult<TEvent>> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                disposal.Token);
            await readLock.WaitAsync(linkedCancellation.Token).ConfigureAwait(false);
            try
            {
                StreamSequence requested;
                lock (stateLock)
                {
                    ObjectDisposedException.ThrowIf(disposed, this);
                    requested = nextSequence;
                }

                var result = await owner.ReadAsync(
                    key,
                    requested,
                    linkedCancellation.Token).ConfigureAwait(false);
                lock (stateLock)
                {
                    nextSequence = result switch
                    {
                        SequencedStreamEvents<TEvent> events =>
                            events.Items[^1].Sequence.Next(),
                        SequencedStreamGap<TEvent> gap =>
                            gap.AvailableFromInclusive,
                        _ => nextSequence
                    };
                }

                return result;
            }
            finally
            {
                readLock.Release();
            }
        }

        public ValueTask DisposeAsync()
        {
            lock (stateLock)
            {
                if (disposed)
                {
                    return ValueTask.CompletedTask;
                }

                disposed = true;
            }

            disposal.Cancel();
            disposal.Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
