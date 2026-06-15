using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;

namespace CanDoItAll.Processes.Projections;

public sealed record ProcessDefinitionListProjection(
    ProcessDefinitionId Id,
    string Name,
    string Status,
    DateTimeOffset UpdatedAtUtc);

public readonly record struct ProcessProjectorName
{
    public ProcessProjectorName(string value)
    {
        Value = ProcessProjectionIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessProjectionKey
{
    public ProcessProjectionKey(string value)
    {
        Value = ProcessProjectionIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessProjectionShardKey
{
    public ProcessProjectionShardKey(string value)
    {
        Value = ProcessProjectionIdentifierValidation.RequireToken(value, nameof(value));
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessProjectionDeadLetterId
{
    public ProcessProjectionDeadLetterId(Guid value)
    {
        Value = ProcessProjectionIdentifierValidation.RequireGuid(value, nameof(value));
    }

    public Guid Value { get; }

    public static ProcessProjectionDeadLetterId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

public sealed record ProcessStoredRuntimeEvent(
    long GlobalSequence,
    long RootSequence,
    ProcessRuntimeEventEnvelope Envelope);

public sealed record ProcessProjectionSnapshot(
    ProcessProjectorName ProjectorName,
    ProcessProjectionKey ProjectionKey,
    string SchemaVersion,
    string PayloadJson,
    string PayloadHash,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProcessProjectorOffset(
    ProcessProjectorName ProjectorName,
    ProcessProjectionShardKey ShardKey,
    long GlobalSequence,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProcessProjectionDeadLetter(
    ProcessProjectionDeadLetterId DeadLetterId,
    ProcessProjectorName ProjectorName,
    ProcessProjectionShardKey ShardKey,
    RuntimeEventId EventId,
    long GlobalSequence,
    string ErrorClass,
    string DiagnosticReference,
    string RetryPolicy,
    DateTimeOffset DeadLetteredAtUtc);

public interface IProcessRuntimeEventReplayStore
{
    Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadAfterGlobalSequenceAsync(
        long globalSequenceExclusive,
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessStoredRuntimeEvent>> ReadByRootRunAsync(
        ProcessRunId rootRunId,
        long rootSequenceExclusive,
        int take,
        CancellationToken cancellationToken = default);
}

public interface IProcessProjectionStore
{
    Task UpsertSnapshotAsync(
        ProcessProjectionSnapshot snapshot,
        CancellationToken cancellationToken = default);

    Task<ProcessProjectionSnapshot?> LoadSnapshotAsync(
        ProcessProjectorName projectorName,
        ProcessProjectionKey projectionKey,
        CancellationToken cancellationToken = default);

    Task SaveOffsetAsync(
        ProcessProjectorOffset offset,
        CancellationToken cancellationToken = default);

    Task<ProcessProjectorOffset?> LoadOffsetAsync(
        ProcessProjectorName projectorName,
        ProcessProjectionShardKey shardKey,
        CancellationToken cancellationToken = default);

    Task WriteDeadLetterAsync(
        ProcessProjectionDeadLetter deadLetter,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProcessProjectionDeadLetter>> ReadDeadLettersAsync(
        ProcessProjectorName projectorName,
        ProcessProjectionShardKey shardKey,
        int take,
        CancellationToken cancellationToken = default);
}

internal static class ProcessProjectionIdentifierValidation
{
    public static Guid RequireGuid(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Process projection identifier cannot be empty.", parameterName);
        }

        return value;
    }

    public static string RequireToken(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Process projection token cannot be empty.", parameterName);
        }

        return value.Trim();
    }
}
