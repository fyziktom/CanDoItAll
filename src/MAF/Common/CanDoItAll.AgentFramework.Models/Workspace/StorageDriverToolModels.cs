namespace CanDoItAll.AgentFramework.Models;

public sealed record AgentStorageCatalogToolEntry(
    Guid Id,
    string Name,
    string ProviderKind,
    bool IsEnabled,
    bool IsReadOnly,
    string CapabilityMask,
    string HealthStatus,
    string EndpointOrRoot);

public sealed record AgentStorageCatalogListResult(
    IReadOnlyList<AgentStorageCatalogToolEntry> Storages,
    IReadOnlyList<string> Warnings);

public sealed record AgentStorageBrowsePathSegment(
    string DisplayName,
    string ContainerKey);

public enum AgentStorageBrowseEntryKind
{
    File,
    Container,
    Link
}

[Flags]
public enum AgentStorageBrowseEntryCapability
{
    None = 0,
    Browse = 1 << 0,
    Read = 1 << 1,
    Write = 1 << 2,
    Delete = 1 << 3
}

public enum AgentStorageBrowseCompleteness
{
    Complete,
    PartialInspectionLimit,
    PartialMetadataLimit,
    PartialTimeLimit
}

public sealed record AgentStorageBrowseEntry(
    string EntryId,
    string ParentContainerKey,
    string Name,
    string DisplayPath,
    AgentStorageBrowseEntryKind Kind,
    AgentStorageBrowseEntryCapability Capabilities,
    long? SizeBytes,
    DateTimeOffset? CreatedAtUtc,
    DateTimeOffset? ModifiedAtUtc,
    string? MediaType);

public sealed record AgentStorageBrowseResult(
    Guid StorageId,
    string StorageName,
    string ContainerKey,
    IReadOnlyList<AgentStorageBrowsePathSegment> Path,
    IReadOnlyList<AgentStorageBrowseEntry> Entries,
    AgentStorageBrowseCompleteness Completeness,
    string? NextCursor,
    int InspectedItems,
    int MetadataProbes);

public sealed record AgentStorageTextReadResult(
    Guid StorageId,
    string StorageName,
    string Locator,
    string DisplayName,
    string ContentType,
    long? ContentLength,
    string Content,
    bool Truncated);

public sealed record AgentStorageWriteToolResult(
    Guid StorageId,
    string StorageName,
    string Locator,
    string DisplayName,
    string ContentType,
    long? ContentLength,
    string PreviewUrl,
    string DownloadUrl);

public sealed record AgentStorageDeleteToolResult(
    Guid StorageId,
    string StorageName,
    string Locator,
    bool Deleted);
