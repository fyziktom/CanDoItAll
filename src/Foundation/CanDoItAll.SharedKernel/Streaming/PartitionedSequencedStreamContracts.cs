namespace CanDoItAll.SharedKernel.Streaming;

public readonly record struct StreamSequence
{
    public StreamSequence(long value)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "A stream sequence cannot be negative.");
        }

        Value = value;
    }

    public static StreamSequence Beginning { get; } = new(0);

    public static StreamSequence First { get; } = new(1);

    public long Value { get; }

    internal StreamSequence Next()
    {
        if (Value == long.MaxValue)
        {
            throw new InvalidOperationException("The stream sequence is exhausted.");
        }

        return new StreamSequence(Value + 1);
    }
}

public sealed record SequencedStreamEnvelope<TEvent>(
    StreamSequence Sequence,
    TEvent Event);

public sealed record PartitionedSequencedStreamPolicy
{
    public PartitionedSequencedStreamPolicy(
        int maxPartitions,
        int maxEventsPerPartition,
        int maxTerminalPartitions,
        TimeSpan terminalRetention,
        int maxTombstones,
        TimeSpan tombstoneRetention)
    {
        ValidatePositive(maxPartitions, nameof(maxPartitions));
        ValidatePositive(maxEventsPerPartition, nameof(maxEventsPerPartition));
        ValidatePositive(maxTerminalPartitions, nameof(maxTerminalPartitions));
        ValidatePositive(terminalRetention, nameof(terminalRetention));
        ValidatePositive(maxTombstones, nameof(maxTombstones));
        ValidatePositive(tombstoneRetention, nameof(tombstoneRetention));

        if (maxTerminalPartitions > maxPartitions)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxTerminalPartitions),
                maxTerminalPartitions,
                "Terminal partition capacity cannot exceed total partition capacity.");
        }

        MaxPartitions = maxPartitions;
        MaxEventsPerPartition = maxEventsPerPartition;
        MaxTerminalPartitions = maxTerminalPartitions;
        TerminalRetention = terminalRetention;
        MaxTombstones = maxTombstones;
        TombstoneRetention = tombstoneRetention;
    }

    public static PartitionedSequencedStreamPolicy Default { get; } = new(
        maxPartitions: 1024,
        maxEventsPerPartition: 256,
        maxTerminalPartitions: 256,
        terminalRetention: TimeSpan.FromMinutes(10),
        maxTombstones: 1024,
        tombstoneRetention: TimeSpan.FromMinutes(15));

    public int MaxPartitions { get; }

    public int MaxEventsPerPartition { get; }

    public int MaxTerminalPartitions { get; }

    public TimeSpan TerminalRetention { get; }

    public int MaxTombstones { get; }

    public TimeSpan TombstoneRetention { get; }

    private static void ValidatePositive(int value, string parameterName)
    {
        if (value < 1)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The value must be greater than zero.");
        }
    }

    private static void ValidatePositive(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "The duration must be greater than zero.");
        }
    }
}

public enum StreamPartitionAdmissionOutcome
{
    Admitted,
    AlreadyActive,
    AlreadyTerminal,
    PreviouslyEvicted,
    CapacityExhausted
}

public enum StreamEvictionReason
{
    TerminalRetentionExpired,
    TerminalCapacityExceeded,
    PartitionCapacityPressure
}

public abstract record SequencedStreamReadResult<TEvent>;

public sealed record SequencedStreamEvents<TEvent>(
    IReadOnlyList<SequencedStreamEnvelope<TEvent>> Items)
    : SequencedStreamReadResult<TEvent>;

public sealed record SequencedStreamGap<TEvent>(
    StreamSequence RequestedFromInclusive,
    StreamSequence AvailableFromInclusive)
    : SequencedStreamReadResult<TEvent>;

public sealed record SequencedStreamCompleted<TEvent>(
    StreamSequence LastSequence)
    : SequencedStreamReadResult<TEvent>;

public sealed record SequencedStreamEvicted<TEvent>(
    StreamEvictionReason Reason,
    DateTimeOffset EvictedAtUtc)
    : SequencedStreamReadResult<TEvent>;

public sealed record SequencedStreamUnknown<TEvent>
    : SequencedStreamReadResult<TEvent>;

public interface ISequencedStreamReader<TEvent> : IAsyncDisposable
{
    StreamSequence NextSequence { get; }

    ValueTask<SequencedStreamReadResult<TEvent>> ReadAsync(
        CancellationToken cancellationToken = default);
}
