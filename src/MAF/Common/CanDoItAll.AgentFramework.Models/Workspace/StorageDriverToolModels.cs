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
