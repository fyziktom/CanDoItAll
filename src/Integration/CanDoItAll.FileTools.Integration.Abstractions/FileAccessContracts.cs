using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.FileTools.Integration;

[Flags]
public enum FileAccessOperation
{
    None = 0,
    View = 1 << 0,
    Download = 1 << 1,
    Edit = 1 << 2,
    Overwrite = 1 << 3
}

public readonly record struct FileAccessActorId
{
    public FileAccessActorId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct FileAccessSessionId
{
    public FileAccessSessionId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public readonly record struct FileAccessCorrelationId
{
    public FileAccessCorrelationId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public sealed record FileAccessContext
{
    public FileAccessContext(
        FileAccessActorId actorId,
        FileAccessSessionId sessionId,
        Guid runtimeProfileId,
        long runtimeGeneration,
        long authorizationRevision,
        FileAccessCorrelationId correlationId)
    {
        if (string.IsNullOrWhiteSpace(actorId.Value))
        {
            throw new ArgumentException("A file-access actor is required.", nameof(actorId));
        }

        if (string.IsNullOrWhiteSpace(sessionId.Value))
        {
            throw new ArgumentException("A file-access session is required.", nameof(sessionId));
        }

        if (runtimeProfileId == Guid.Empty)
        {
            throw new ArgumentException("A runtime profile is required.", nameof(runtimeProfileId));
        }

        if (runtimeGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runtimeGeneration));
        }

        if (authorizationRevision < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(authorizationRevision));
        }

        if (string.IsNullOrWhiteSpace(correlationId.Value))
        {
            throw new ArgumentException("A file-access correlation identifier is required.", nameof(correlationId));
        }

        ActorId = actorId;
        SessionId = sessionId;
        RuntimeProfileId = runtimeProfileId;
        RuntimeGeneration = runtimeGeneration;
        AuthorizationRevision = authorizationRevision;
        CorrelationId = correlationId;
    }

    public FileAccessActorId ActorId { get; }

    public FileAccessSessionId SessionId { get; }

    public Guid RuntimeProfileId { get; }

    public long RuntimeGeneration { get; }

    public long AuthorizationRevision { get; }

    public FileAccessCorrelationId CorrelationId { get; }
}

public interface IFileAccessContextProvider
{
    ValueTask<FileAccessContext> GetCurrentAsync(CancellationToken cancellationToken = default);
}

public sealed record FileAccessGrantRequest
{
    public FileAccessGrantRequest(
        FileAccessContext context,
        FileToolsSemanticScope scope,
        Guid storageId,
        string occurrenceId,
        FileAccessOperation operations,
        string? expectedRevision = null)
    {
        const FileAccessOperation supported =
            FileAccessOperation.View |
            FileAccessOperation.Download |
            FileAccessOperation.Edit |
            FileAccessOperation.Overwrite;
        if (storageId == Guid.Empty)
        {
            throw new ArgumentException("A storage binding is required.", nameof(storageId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceId);
        if (operations == FileAccessOperation.None || (operations & ~supported) != FileAccessOperation.None)
        {
            throw new ArgumentOutOfRangeException(nameof(operations));
        }

        if (operations.HasFlag(FileAccessOperation.Overwrite) &&
            !operations.HasFlag(FileAccessOperation.Edit))
        {
            throw new ArgumentException("Overwrite authority requires edit authority.", nameof(operations));
        }

        Context = context ?? throw new ArgumentNullException(nameof(context));
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        StorageId = storageId;
        OccurrenceId = occurrenceId.Trim();
        Operations = operations;
        ExpectedRevision = string.IsNullOrWhiteSpace(expectedRevision) ? null : expectedRevision.Trim();
    }

    public FileAccessContext Context { get; }

    public FileToolsSemanticScope Scope { get; }

    public Guid StorageId { get; }

    public string OccurrenceId { get; }

    public FileAccessOperation Operations { get; }

    public string? ExpectedRevision { get; }
}
