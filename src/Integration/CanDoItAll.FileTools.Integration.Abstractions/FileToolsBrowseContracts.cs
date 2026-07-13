using CanDoItAll.FileTools.FileBrowser;

namespace CanDoItAll.FileTools.Integration;

public enum FileToolsHostBrowseCacheMode
{
    UseStoragePolicy,
    Disabled
}

public sealed record FileToolsBrowseWorkLimits
{
    public FileToolsBrowseWorkLimits(
        int maximumReturnedItems = 100,
        int maximumInspectedItems = 512,
        int maximumMetadataProbes = 100,
        int maximumConcurrentMetadataProbes = 1,
        TimeSpan? maximumDuration = null)
    {
        if (maximumReturnedItems < 1 ||
            maximumInspectedItems < maximumReturnedItems ||
            maximumMetadataProbes < 0 ||
            maximumMetadataProbes > maximumInspectedItems ||
            maximumConcurrentMetadataProbes < 0 ||
            maximumConcurrentMetadataProbes > maximumMetadataProbes)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumReturnedItems));
        }

        MaximumDuration = maximumDuration ?? TimeSpan.FromSeconds(5);
        if (MaximumDuration < TimeSpan.FromMilliseconds(50) || MaximumDuration > TimeSpan.FromMinutes(2))
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
        }

        MaximumReturnedItems = maximumReturnedItems;
        MaximumInspectedItems = maximumInspectedItems;
        MaximumMetadataProbes = maximumMetadataProbes;
        MaximumConcurrentMetadataProbes = maximumConcurrentMetadataProbes;
    }

    public int MaximumReturnedItems { get; }

    public int MaximumInspectedItems { get; }

    public int MaximumMetadataProbes { get; }

    public int MaximumConcurrentMetadataProbes { get; }

    public TimeSpan MaximumDuration { get; }
}

public sealed record FileToolsStorageBinding
{
    public const int MaximumDisplayNameLength = 512;

    public FileToolsStorageBinding(
        Guid storageId,
        string displayName,
        FileToolsBrowseWorkLimits workLimits,
        FileToolsStorageRoot? root = null,
        FileToolsHostBrowseCacheMode hostCacheMode = FileToolsHostBrowseCacheMode.UseStoragePolicy)
    {
        if (storageId == Guid.Empty)
        {
            throw new ArgumentException("A storage binding identifier is required.", nameof(storageId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (displayName.Trim().Length > MaximumDisplayNameLength)
        {
            throw new ArgumentOutOfRangeException(nameof(displayName));
        }

        if (!Enum.IsDefined(hostCacheMode))
        {
            throw new ArgumentOutOfRangeException(nameof(hostCacheMode));
        }

        StorageId = storageId;
        DisplayName = displayName.Trim();
        WorkLimits = workLimits ?? throw new ArgumentNullException(nameof(workLimits));
        Root = root ?? FileToolsStorageRoot.StorageRoot;
        HostCacheMode = hostCacheMode;
    }

    public Guid StorageId { get; }

    public string DisplayName { get; }

    public FileToolsBrowseWorkLimits WorkLimits { get; }

    public FileToolsStorageRoot Root { get; }

    public FileToolsHostBrowseCacheMode HostCacheMode { get; }
}

public readonly record struct FileToolsStorageRoot
{
    public static FileToolsStorageRoot StorageRoot { get; } = new(string.Empty);

    public FileToolsStorageRoot(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string normalized = value.Trim().Replace('\\', '/').TrimEnd('/');
        if (normalized.Length > 4096 ||
            normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("The storage binding root is invalid.", nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public bool IsStorageRoot => string.IsNullOrEmpty(Value);

    public override string ToString() => Value ?? string.Empty;
}

public interface IFileToolsStorageBindingProvider
{
    ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveAsync(
        FileToolsSemanticScope scope,
        CancellationToken cancellationToken = default);
}

public interface IFileToolsStorageBindingSource
{
    FileToolsSemanticScopeKind ScopeKind { get; }

    ValueTask<IReadOnlyList<FileToolsStorageBinding>> ResolveAsync(
        FileToolsSemanticScope scope,
        CancellationToken cancellationToken = default);
}

public readonly record struct FileToolsBrowseSessionRevision
{
    public FileToolsBrowseSessionRevision(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string normalized = value.Trim();
        if (normalized.Length > 128)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }

    public override string ToString() => Value ?? string.Empty;
}

public sealed record FileToolsBrowseSession(
    FileToolsSemanticScope Scope,
    IReadOnlyList<IFileBrowserProvider> Providers,
    FileBrowserSortDescriptor DefaultSort,
    FileToolsBrowseSessionRevision Revision);

public interface IFileToolsBrowseSessionFactory
{
    ValueTask<FileToolsBrowseSession> CreateAsync(
        FileToolsSemanticScope scope,
        CancellationToken cancellationToken = default);
}
