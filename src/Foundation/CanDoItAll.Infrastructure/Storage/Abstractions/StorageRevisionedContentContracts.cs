namespace CanDoItAll.Infrastructure.Storage;

public readonly record struct StorageContentRevision
{
    public StorageContentRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public sealed record StorageRevisionedWriteRequest
{
    public StorageRevisionedWriteRequest(
        StorageObjectReference reference,
        byte[] content,
        StorageContentRevision? expectedRevision,
        bool allowOverwrite)
    {
        Reference = reference ?? throw new ArgumentNullException(nameof(reference));
        Content = content ?? throw new ArgumentNullException(nameof(content));
        ExpectedRevision = expectedRevision;
        AllowOverwrite = allowOverwrite;
    }

    public StorageObjectReference Reference { get; }

    public byte[] Content { get; }

    public StorageContentRevision? ExpectedRevision { get; }

    public bool AllowOverwrite { get; }
}

public sealed record StorageRevisionedWriteResult(
    StorageWriteResult WriteResult,
    StorageContentRevision PersistedRevision);

public sealed class StorageContentConflictException(
    StorageContentRevision? expectedRevision,
    StorageContentRevision? actualRevision)
    : InvalidOperationException("The storage object changed after it was opened.")
{
    public StorageContentRevision? ExpectedRevision { get; } = expectedRevision;

    public StorageContentRevision? ActualRevision { get; } = actualRevision;
}

public interface IStorageRevisionedContentDriver
{
    Task<StorageContentRevision?> GetRevisionAsync(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        CancellationToken cancellationToken = default);

    Task<StorageRevisionedWriteResult> ReplaceAsync(
        StorageCatalogRecord storage,
        StorageRevisionedWriteRequest request,
        CancellationToken cancellationToken = default);
}
