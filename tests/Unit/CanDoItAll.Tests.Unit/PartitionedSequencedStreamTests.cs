using CanDoItAll.SharedKernel.Streaming;

namespace CanDoItAll.Tests.Unit;

public sealed class PartitionedSequencedStreamTests
{
    private static readonly DateTimeOffset InitialUtcNow =
        new(2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task CompletedPartition_ReplaysOrderedEventsAndTerminalState()
    {
        var stream = CreateStream();

        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation"));
        stream.Append("operation", "accepted");
        stream.Append("operation", "loading");
        var terminal = stream.Complete("operation", "succeeded");

        Assert.Equal(3, terminal.Sequence.Value);

        await using var firstReader = stream.OpenReader(
            "operation",
            StreamSequence.Beginning);
        var firstReplay = Assert.IsType<SequencedStreamEvents<string>>(
            await firstReader.ReadAsync());

        Assert.Equal(
            [1L, 2L, 3L],
            firstReplay.Items.Select(item => item.Sequence.Value));
        Assert.Equal(
            ["accepted", "loading", "succeeded"],
            firstReplay.Items.Select(item => item.Event));
        Assert.Equal(4, firstReader.NextSequence.Value);

        var firstCompletion = Assert.IsType<SequencedStreamCompleted<string>>(
            await firstReader.ReadAsync());
        Assert.Equal(3, firstCompletion.LastSequence.Value);

        await using var lateReader = stream.OpenReader(
            "operation",
            StreamSequence.Beginning);
        var lateReplay = Assert.IsType<SequencedStreamEvents<string>>(
            await lateReader.ReadAsync());
        var lateCompletion = Assert.IsType<SequencedStreamCompleted<string>>(
            await lateReader.ReadAsync());

        Assert.Equal(
            firstReplay.Items,
            lateReplay.Items);
        Assert.Equal(3, lateCompletion.LastSequence.Value);
    }

    [Fact]
    public async Task ConcurrentAppends_AssignUniqueContiguousSequences()
    {
        const int eventCount = 256;
        var stream = CreateStream<int>(maxEventsPerPartition: eventCount);
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation"));

        var appendTasks = Enumerable.Range(0, eventCount)
            .Select(value => Task.Run(() => stream.Append("operation", value)))
            .ToArray();

        var envelopes = await Task.WhenAll(appendTasks);

        Assert.Equal(
            Enumerable.Range(1, eventCount).Select(value => (long)value),
            envelopes
                .Select(envelope => envelope.Sequence.Value)
                .Order());
        Assert.Equal(
            eventCount,
            envelopes
                .Select(envelope => envelope.Sequence.Value)
                .Distinct()
                .Count());

        await using var reader = stream.OpenReader(
            "operation",
            StreamSequence.Beginning);
        var replay = Assert.IsType<SequencedStreamEvents<int>>(
            await reader.ReadAsync());

        Assert.Equal(eventCount, replay.Items.Count);
        Assert.Equal(
            Enumerable.Range(1, eventCount).Select(value => (long)value),
            replay.Items.Select(item => item.Sequence.Value));
        Assert.Equal(
            Enumerable.Range(0, eventCount).Order(),
            replay.Items.Select(item => item.Event).Order());
    }

    [Fact]
    public async Task Readers_FanOutWithinPartitionWithoutCrossPartitionSignals()
    {
        var stream = CreateStream();
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation-a"));
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation-b"));

        await using var firstReaderA = stream.OpenReader(
            "operation-a",
            StreamSequence.Beginning);
        await using var secondReaderA = stream.OpenReader(
            "operation-a",
            StreamSequence.Beginning);
        await using var readerB = stream.OpenReader(
            "operation-b",
            StreamSequence.Beginning);

        var pendingReadA = firstReaderA.ReadAsync().AsTask();
        Assert.False(pendingReadA.IsCompleted);

        stream.Append("operation-b", "b-1");
        var eventsB = Assert.IsType<SequencedStreamEvents<string>>(
            await readerB.ReadAsync());

        Assert.Equal("b-1", Assert.Single(eventsB.Items).Event);
        Assert.False(pendingReadA.IsCompleted);

        stream.Append("operation-a", "a-1");
        var firstEventsA = Assert.IsType<SequencedStreamEvents<string>>(
            await pendingReadA);
        var secondEventsA = Assert.IsType<SequencedStreamEvents<string>>(
            await secondReaderA.ReadAsync());

        Assert.Equal("a-1", Assert.Single(firstEventsA.Items).Event);
        Assert.Equal(firstEventsA.Items, secondEventsA.Items);
    }

    [Fact]
    public async Task BoundedHistory_ReportsGapThenRecoversReaderCursor()
    {
        var stream = CreateStream<int>(maxEventsPerPartition: 3);
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation"));
        foreach (var value in Enumerable.Range(1, 5))
        {
            stream.Append("operation", value);
        }

        await using var reader = stream.OpenReader(
            "operation",
            StreamSequence.Beginning);
        var gap = Assert.IsType<SequencedStreamGap<int>>(
            await reader.ReadAsync());

        Assert.Equal(0, gap.RequestedFromInclusive.Value);
        Assert.Equal(3, gap.AvailableFromInclusive.Value);
        Assert.Equal(3, reader.NextSequence.Value);

        var recovered = Assert.IsType<SequencedStreamEvents<int>>(
            await reader.ReadAsync());

        Assert.Equal(
            [3, 4, 5],
            recovered.Items.Select(item => item.Event));
        Assert.Equal(
            [3L, 4L, 5L],
            recovered.Items.Select(item => item.Sequence.Value));
        Assert.Equal(6, reader.NextSequence.Value);
    }

    [Fact]
    public void Admit_WhenEveryPartitionIsActive_RejectsWithoutEviction()
    {
        var stream = CreateStream(
            maxPartitions: 2,
            maxTerminalPartitions: 1);
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation-a"));
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation-b"));

        var outcome = stream.Admit("operation-c");

        Assert.Equal(
            StreamPartitionAdmissionOutcome.CapacityExhausted,
            outcome);
        Assert.Equal(
            StreamPartitionAdmissionOutcome.AlreadyActive,
            stream.Admit("operation-a"));
        Assert.Equal(1, stream.Append("operation-a", "still-active").Sequence.Value);
        Assert.Equal(1, stream.Append("operation-b", "still-active").Sequence.Value);
    }

    [Fact]
    public async Task TerminalCapacity_EvictsOldestTerminalAndRetainsTombstone()
    {
        var stream = CreateStream(
            maxPartitions: 3,
            maxTerminalPartitions: 1);
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation-a"));
        stream.Complete("operation-a", "a-terminal");
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation-b"));

        stream.Complete("operation-b", "b-terminal");

        Assert.Equal(
            StreamPartitionAdmissionOutcome.PreviouslyEvicted,
            stream.Admit("operation-a"));
        await using var evictedReader = stream.OpenReader(
            "operation-a",
            StreamSequence.Beginning);
        var eviction = Assert.IsType<SequencedStreamEvicted<string>>(
            await evictedReader.ReadAsync());

        Assert.Equal(
            StreamEvictionReason.TerminalCapacityExceeded,
            eviction.Reason);
        Assert.Throws<InvalidOperationException>(
            () => stream.Append("operation-a", "late-event"));

        await using var retainedReader = stream.OpenReader(
            "operation-b",
            StreamSequence.Beginning);
        var retainedEvents = Assert.IsType<SequencedStreamEvents<string>>(
            await retainedReader.ReadAsync());
        var retainedCompletion = Assert.IsType<SequencedStreamCompleted<string>>(
            await retainedReader.ReadAsync());

        Assert.Equal(
            "b-terminal",
            Assert.Single(retainedEvents.Items).Event);
        Assert.Equal(1, retainedCompletion.LastSequence.Value);
    }

    [Fact]
    public async Task RetentionExpiry_TransitionsFromTerminalToTombstoneToUnknown()
    {
        var clock = new SequencedStreamManualTimeProvider(InitialUtcNow);
        var stream = CreateStream(
            terminalRetention: TimeSpan.FromMinutes(1),
            tombstoneRetention: TimeSpan.FromMinutes(2),
            timeProvider: clock);
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation"));
        stream.Complete("operation", "terminal");
        await using var reader = stream.OpenReader(
            "operation",
            new StreamSequence(2));

        clock.Advance(TimeSpan.FromSeconds(59));
        var completed = Assert.IsType<SequencedStreamCompleted<string>>(
            await reader.ReadAsync());
        Assert.Equal(1, completed.LastSequence.Value);

        clock.Advance(TimeSpan.FromSeconds(1));
        var evicted = Assert.IsType<SequencedStreamEvicted<string>>(
            await reader.ReadAsync());

        Assert.Equal(
            StreamEvictionReason.TerminalRetentionExpired,
            evicted.Reason);
        Assert.Equal(clock.GetUtcNow(), evicted.EvictedAtUtc);
        Assert.Equal(
            StreamPartitionAdmissionOutcome.PreviouslyEvicted,
            stream.Admit("operation"));

        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.IsType<SequencedStreamUnknown<string>>(
            await reader.ReadAsync());
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation"));
    }

    [Fact]
    public async Task ReaderCancellationAndDisposal_DoNotAffectPublisherOrOtherReaders()
    {
        var stream = CreateStream();
        Assert.Equal(
            StreamPartitionAdmissionOutcome.Admitted,
            stream.Admit("operation"));

        await using var cancelledReader = stream.OpenReader(
            "operation",
            StreamSequence.Beginning);
        using var cancellation = new CancellationTokenSource();
        var cancelledRead = cancelledReader
            .ReadAsync(cancellation.Token)
            .AsTask();
        Assert.False(cancelledRead.IsCompleted);

        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => cancelledRead);

        var firstEnvelope = stream.Append("operation", "first");
        Assert.Equal(1, firstEnvelope.Sequence.Value);

        await using var independentReader = stream.OpenReader(
            "operation",
            StreamSequence.Beginning);
        var firstEvents = Assert.IsType<SequencedStreamEvents<string>>(
            await independentReader.ReadAsync());
        Assert.Equal("first", Assert.Single(firstEvents.Items).Event);

        var disposedReader = stream.OpenReader(
            "operation",
            new StreamSequence(2));
        var disposedRead = disposedReader.ReadAsync().AsTask();
        Assert.False(disposedRead.IsCompleted);

        await disposedReader.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => disposedRead);

        var secondEnvelope = stream.Append("operation", "second");
        Assert.Equal(2, secondEnvelope.Sequence.Value);

        await using var finalReader = stream.OpenReader(
            "operation",
            new StreamSequence(2));
        var finalEvents = Assert.IsType<SequencedStreamEvents<string>>(
            await finalReader.ReadAsync());
        Assert.Equal("second", Assert.Single(finalEvents.Items).Event);
    }

    private static PartitionedSequencedStream<string, TEvent> CreateStream<TEvent>(
        int maxPartitions = 8,
        int maxEventsPerPartition = 16,
        int maxTerminalPartitions = 4,
        TimeSpan? terminalRetention = null,
        int maxTombstones = 8,
        TimeSpan? tombstoneRetention = null,
        TimeProvider? timeProvider = null)
    {
        var policy = new PartitionedSequencedStreamPolicy(
            maxPartitions,
            maxEventsPerPartition,
            maxTerminalPartitions,
            terminalRetention ?? TimeSpan.FromMinutes(10),
            maxTombstones,
            tombstoneRetention ?? TimeSpan.FromMinutes(15));
        return new PartitionedSequencedStream<string, TEvent>(
            policy,
            timeProvider ?? TimeProvider.System);
    }

    private static PartitionedSequencedStream<string, string> CreateStream(
        int maxPartitions = 8,
        int maxEventsPerPartition = 16,
        int maxTerminalPartitions = 4,
        TimeSpan? terminalRetention = null,
        int maxTombstones = 8,
        TimeSpan? tombstoneRetention = null,
        TimeProvider? timeProvider = null)
    {
        return CreateStream<string>(
            maxPartitions,
            maxEventsPerPartition,
            maxTerminalPartitions,
            terminalRetention,
            maxTombstones,
            tombstoneRetention,
            timeProvider);
    }

    private sealed class SequencedStreamManualTimeProvider(
        DateTimeOffset initialUtcNow)
        : TimeProvider
    {
        private readonly Lock gate = new();
        private DateTimeOffset utcNow = initialUtcNow;

        public override DateTimeOffset GetUtcNow()
        {
            lock (gate)
            {
                return utcNow;
            }
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    duration,
                    "Time cannot move backwards.");
            }

            lock (gate)
            {
                utcNow += duration;
            }
        }
    }
}
