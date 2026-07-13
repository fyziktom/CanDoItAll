using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;

namespace CanDoItAll.FileTools.Integration;

public enum FileToolsKnownFileIntent
{
    ReadOnly,
    Edit
}

public enum FileToolsKnownFileOccurrenceKind
{
    RelativePath,
    ContentAddress,
    RemotePath
}

public sealed record FileToolsKnownFileOccurrence
{
    public const int MaximumOccurrenceIdLength = 4096;
    public const int MaximumFileNameLength = 512;
    public const int MaximumMediaTypeLength = 256;

    public FileToolsKnownFileOccurrence(
        Guid storageId,
        FileToolsKnownFileOccurrenceKind kind,
        string occurrenceId,
        string fileName,
        string? mediaType,
        long? size)
    {
        if (storageId == Guid.Empty)
        {
            throw new ArgumentException("A storage identifier is required.", nameof(storageId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(occurrenceId);
        string normalizedOccurrenceId = occurrenceId.Trim().Replace('\\', '/').Trim('/');
        if (normalizedOccurrenceId.Length == 0 ||
            normalizedOccurrenceId.Length > MaximumOccurrenceIdLength ||
            normalizedOccurrenceId.Split('/', StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("The known-file occurrence is invalid.", nameof(occurrenceId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        string normalizedFileName = fileName.Trim();
        if (normalizedFileName.Length > MaximumFileNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(fileName));
        }

        string? normalizedMediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
        if (normalizedMediaType?.Length > MaximumMediaTypeLength)
        {
            throw new ArgumentOutOfRangeException(nameof(mediaType));
        }

        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        StorageId = storageId;
        Kind = kind;
        OccurrenceId = normalizedOccurrenceId;
        FileName = normalizedFileName;
        MediaType = normalizedMediaType;
        Size = size;
    }

    public Guid StorageId { get; }

    public FileToolsKnownFileOccurrenceKind Kind { get; }

    public string OccurrenceId { get; }

    public string FileName { get; }

    public string? MediaType { get; }

    public long? Size { get; }
}

public sealed record FileToolsKnownFileRequest
{
    public FileToolsKnownFileRequest(
        FileToolsSemanticScope scope,
        FileReference file,
        FileToolsKnownFileIntent intent)
    {
        if (!Enum.IsDefined(intent))
        {
            throw new ArgumentOutOfRangeException(nameof(intent));
        }

        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        if (string.IsNullOrWhiteSpace(file.SourceId) || string.IsNullOrWhiteSpace(file.Value))
        {
            throw new ArgumentException("A known-file reference is required.", nameof(file));
        }

        File = file;
        Intent = intent;
    }

    public FileToolsSemanticScope Scope { get; }

    public FileReference File { get; }

    public FileToolsKnownFileIntent Intent { get; }
}

public sealed record FileToolsKnownFileSession
{
    public FileToolsKnownFileSession(
        FileReference file,
        IFileContentSource contentSource,
        FileToolsKnownFileIntent intent,
        IFileSaveTarget? saveTarget = null)
    {
        if (!Enum.IsDefined(intent))
        {
            throw new ArgumentOutOfRangeException(nameof(intent));
        }

        if (string.IsNullOrWhiteSpace(file.SourceId) || string.IsNullOrWhiteSpace(file.Value))
        {
            throw new ArgumentException("A known-file reference is required.", nameof(file));
        }

        File = file;
        ContentSource = contentSource ?? throw new ArgumentNullException(nameof(contentSource));
        Intent = intent;
        SaveTarget = saveTarget;
    }

    public FileReference File { get; }

    public IFileContentSource ContentSource { get; }

    public FileToolsKnownFileIntent Intent { get; }

    public IFileSaveTarget? SaveTarget { get; }
}

public interface IFileToolsKnownFileSessionFactory
{
    ValueTask<FileToolsKnownFileSession> CreateAsync(
        FileToolsKnownFileRequest request,
        CancellationToken cancellationToken = default);
}

public interface IFileToolsKnownFileSessionReleaser
{
    ValueTask ReleaseAsync(
        FileReference file,
        CancellationToken cancellationToken = default);
}

public sealed record FileToolsKnownFileActivation
{
    public FileToolsKnownFileActivation(
        FileToolsKnownFileRequest request,
        string fileName,
        string? mediaType,
        long? size)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (size < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(size));
        }

        FileName = fileName.Trim();
        MediaType = string.IsNullOrWhiteSpace(mediaType) ? null : mediaType.Trim();
        Size = size;
    }

    public FileToolsKnownFileRequest Request { get; }

    public string FileName { get; }

    public string? MediaType { get; }

    public long? Size { get; }
}

public interface IFileToolsBrowseItemActivator
{
    ValueTask<FileToolsKnownFileActivation> ActivateAsync(
        FileToolsSemanticScope scope,
        FileBrowserItemKey itemKey,
        FileToolsKnownFileIntent intent,
        CancellationToken cancellationToken = default);
}

public interface IFileToolsKnownFileActivator
{
    ValueTask<FileToolsKnownFileActivation> ActivateAsync(
        FileToolsSemanticScope scope,
        FileToolsKnownFileOccurrence occurrence,
        FileToolsKnownFileIntent intent,
        CancellationToken cancellationToken = default);
}
