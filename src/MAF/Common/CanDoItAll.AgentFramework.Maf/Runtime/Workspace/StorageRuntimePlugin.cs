using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class StorageRuntimePlugin(
    IStorageCatalogService catalogService,
    IStorageDriverRegistry driverRegistry,
    IStorageBrowseDriverRegistry? browseDriverRegistry,
    AgentWorkspaceToolAccessSettings accessSettings)
{
    private readonly IStorageCatalogService catalogService = catalogService;
    private readonly IStorageDriverRegistry driverRegistry = driverRegistry;
    private readonly IStorageBrowseDriverRegistry? browseDriverRegistry = browseDriverRegistry;
    private readonly AgentWorkspaceToolAccessSettings accessSettings = AgentWorkspaceToolAccessMetadata.Normalize(accessSettings);

    public async Task<AgentStorageCatalogListResult> ListStorageCatalogs(
        bool includeDisabled = false,
        CancellationToken cancellationToken = default)
    {
        EnsureStorageReadAllowed();
        var storages = await catalogService.ListAsync(cancellationToken).ConfigureAwait(false);
        var accessibleStorages = storages
            .Where(storage => includeDisabled || storage.IsEnabled)
            .Where(IsStorageCatalogAllowed)
            .OrderBy(storage => storage.DisplayOrder)
            .ThenBy(storage => storage.Name, StringComparer.OrdinalIgnoreCase)
            .Select(storage => new AgentStorageCatalogToolEntry(
                storage.Id,
                storage.Name,
                storage.ProviderKind.ToString(),
                storage.IsEnabled,
                storage.IsReadOnly,
                storage.CapabilityMask.ToString(),
                storage.HealthStatus.ToString(),
                storage.EndpointOrRoot))
            .ToList();

        var warnings = accessSettings.AllowAllStorageCatalogs
            ? Array.Empty<string>()
            : ["Only storage catalogs explicitly allowed in this agent's settings are returned."];

        return new AgentStorageCatalogListResult(accessibleStorages, warnings);
    }

    public async Task<AgentStorageBrowseResult> BrowseStorage(
        Guid storageId,
        string? containerKey = null,
        int pageSize = 50,
        string? cursor = null,
        bool includeMetadata = false,
        CancellationToken cancellationToken = default)
    {
        EnsureStorageReadAllowed();
        var storage = await ResolveStorageAsync(storageId, requireWrite: false, cancellationToken).ConfigureAwait(false);
        var contentDriver = ResolveDriver(storage, StorageCapability.Read);
        var registry = browseDriverRegistry
            ?? throw new InvalidOperationException("Storage browsing is not available because no browse-driver registry is configured.");
        var driver = registry.Resolve(storage.ProviderKind);
        var metadata = includeMetadata
            ? ResolveBrowseMetadata(driver)
            : StorageBrowseMetadataField.None;
        var request = new StorageBrowseRequest(
            new StorageBrowseContainer(containerKey ?? string.Empty),
            pageSize,
            cursor is null ? null : new StorageBrowseCursor(cursor),
            metadata: metadata);
        var page = await driver.BrowseAsync(storage, request, cancellationToken).ConfigureAwait(false);

        return new AgentStorageBrowseResult(
            storage.Id,
            storage.Name,
            page.Container.Key,
            page.Path
                .Select(segment => new AgentStorageBrowsePathSegment(
                    segment.DisplayName,
                    segment.Container.Key))
                .ToArray(),
            page.Entries
                .Select(entry => new AgentStorageBrowseEntry(
                    entry.Id.Value,
                    entry.Parent.Key,
                    entry.Name,
                    entry.DisplayPath,
                    MapBrowseEntryKind(entry.Kind),
                    MapBrowseEntryCapabilities(entry.Capabilities, storage, contentDriver),
                    entry.Size,
                    entry.CreatedAtUtc,
                    entry.ModifiedAtUtc,
                    entry.MediaType))
                .ToArray(),
            MapBrowseCompleteness(page.Completeness),
            page.NextCursor?.Token,
            page.Metrics.InspectedItems,
            page.Metrics.MetadataProbes);
    }

    public async Task<AgentStorageTextReadResult> ReadStorageTextFile(
        Guid storageId,
        string locator,
        int maxCharacters = 12000,
        CancellationToken cancellationToken = default)
    {
        EnsureStorageReadAllowed();
        var storage = await ResolveStorageAsync(storageId, requireWrite: false, cancellationToken).ConfigureAwait(false);
        var driver = ResolveDriver(storage, StorageCapability.Read);
        var reference = BuildReference(storage, locator);

        await using var stream = await driver.OpenReadAsync(storage, reference, cancellationToken).ConfigureAwait(false);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var buffer = new char[Math.Clamp(maxCharacters, 1, 100_000) + 1];
        var read = await reader.ReadBlockAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false);
        var truncated = read == buffer.Length;
        var content = new string(buffer, 0, truncated ? buffer.Length - 1 : read);

        return new AgentStorageTextReadResult(
            storage.Id,
            storage.Name,
            reference.Locator,
            reference.DisplayName,
            reference.ContentType,
            reference.ContentLength,
            content,
            truncated);
    }

    public async Task<AgentStorageWriteToolResult> WriteStorageTextFile(
        Guid storageId,
        string path,
        string content,
        string contentType = "text/plain",
        CancellationToken cancellationToken = default)
    {
        EnsureStorageWriteAllowed();
        var storage = await ResolveStorageAsync(storageId, requireWrite: true, cancellationToken).ConfigureAwait(false);
        var driver = ResolveDriver(storage, StorageCapability.Write);
        var normalizedPath = NormalizeStoragePath(path);
        var bytes = Encoding.UTF8.GetBytes(content ?? string.Empty);
        var result = await driver.SaveAsync(
                storage,
                new StorageWriteRequest(
                    Path.GetFileName(normalizedPath),
                    string.IsNullOrWhiteSpace(contentType) ? "text/plain" : contentType.Trim(),
                    bytes,
                    StorageUsagePurpose.PromptExport,
                    ResolveContentKind(normalizedPath, contentType),
                    RelativePathHint: normalizedPath),
                cancellationToken)
            .ConfigureAwait(false);

        return new AgentStorageWriteToolResult(
            storage.Id,
            storage.Name,
            result.Reference.Locator,
            result.Reference.DisplayName,
            result.Reference.ContentType,
            result.Reference.ContentLength,
            result.AccessDescriptor.PreviewUrl,
            result.AccessDescriptor.DownloadUrl);
    }

    public async Task<AgentStorageDeleteToolResult> DeleteStorageObject(
        Guid storageId,
        string locator,
        CancellationToken cancellationToken = default)
    {
        EnsureStorageWriteAllowed();
        var storage = await ResolveStorageAsync(storageId, requireWrite: true, cancellationToken).ConfigureAwait(false);
        var driver = ResolveDriver(storage, StorageCapability.Delete);
        var reference = BuildReference(storage, locator);
        await driver.DeleteAsync(storage, reference, cancellationToken).ConfigureAwait(false);
        return new AgentStorageDeleteToolResult(storage.Id, storage.Name, reference.Locator, true);
    }

    private async Task<StorageCatalogRecord> ResolveStorageAsync(
        Guid storageId,
        bool requireWrite,
        CancellationToken cancellationToken)
    {
        if (storageId == Guid.Empty)
        {
            throw new InvalidOperationException("A storage catalog id is required.");
        }

        var storage = await catalogService.GetAsync(storageId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Storage catalog '{storageId:D}' was not found.");

        if (!storage.IsEnabled)
        {
            throw new InvalidOperationException($"Storage catalog '{storage.Name}' is disabled.");
        }

        if (!IsStorageCatalogAllowed(storage))
        {
            throw new InvalidOperationException($"Storage catalog '{storage.Name}' is not allowed for this agent.");
        }

        if (requireWrite && storage.IsReadOnly)
        {
            throw new InvalidOperationException($"Storage catalog '{storage.Name}' is read-only.");
        }

        return storage;
    }

    private IStorageDriver ResolveDriver(StorageCatalogRecord storage, StorageCapability requiredCapability)
    {
        var driver = driverRegistry.Resolve(storage.ProviderKind);
        var effectiveCapabilities = storage.CapabilityMask & driver.SupportedCapabilities;
        if ((effectiveCapabilities & requiredCapability) != requiredCapability)
        {
            throw new InvalidOperationException(
                $"Storage catalog '{storage.Name}' does not support required capability '{requiredCapability}'.");
        }

        return driver;
    }

    private bool IsStorageCatalogAllowed(StorageCatalogRecord storage)
    {
        return accessSettings.AllowAllStorageCatalogs ||
               accessSettings.AllowedStorageCatalogIds.Contains(storage.Id);
    }

    private void EnsureStorageReadAllowed()
    {
        if (!accessSettings.CanReadStorage && !accessSettings.CanWriteStorage)
        {
            throw new InvalidOperationException("This agent is not allowed to read storage catalogs.");
        }
    }

    private void EnsureStorageWriteAllowed()
    {
        if (!accessSettings.CanWriteStorage)
        {
            throw new InvalidOperationException("This agent is not allowed to write storage catalogs.");
        }
    }

    private static StorageObjectReference BuildReference(StorageCatalogRecord storage, string locator)
    {
        var entryId = new StorageBrowseEntryId(locator).Value;
        var (locatorKind, normalizedLocator) = ResolveStorageLocator(storage.ProviderKind, entryId);
        return new StorageObjectReference(
            storage.Id,
            storage.ProviderKind,
            locatorKind,
            normalizedLocator,
            Path.GetFileName(normalizedLocator),
            ResolveContentType(normalizedLocator));
    }

    private static (StorageLocatorKind Kind, string Locator) ResolveStorageLocator(
        StorageProviderKind providerKind,
        string entryId)
    {
        return providerKind switch
        {
            StorageProviderKind.FileSystem => (StorageLocatorKind.RelativePath, NormalizeStoragePath(entryId)),
            StorageProviderKind.Ftp => (StorageLocatorKind.RemotePath, NormalizeStoragePath(entryId)),
            StorageProviderKind.Ipfs => ResolveIpfsLocator(entryId),
            _ => throw new ArgumentOutOfRangeException(
                nameof(providerKind),
                providerKind,
                "Unsupported storage provider kind.")
        };
    }

    private static (StorageLocatorKind Kind, string Locator) ResolveIpfsLocator(string entryId)
    {
        if (entryId.StartsWith("cid:", StringComparison.Ordinal))
        {
            var contentAddress = entryId["cid:".Length..];
            if (string.IsNullOrWhiteSpace(contentAddress))
            {
                throw new InvalidOperationException("An IPFS content address is required after 'cid:'.");
            }

            return (StorageLocatorKind.ContentAddress, contentAddress);
        }

        if (entryId.StartsWith("mfs:", StringComparison.Ordinal))
        {
            var mutablePath = NormalizeStoragePath(entryId);
            if (mutablePath.Length == "mfs:".Length)
            {
                throw new InvalidOperationException("An IPFS mutable-file path is required after 'mfs:'.");
            }

            return (StorageLocatorKind.RemotePath, mutablePath);
        }

        return (StorageLocatorKind.ContentAddress, entryId);
    }

    private static string NormalizeStoragePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("A storage object path is required.");
        }

        var normalized = path.Trim().Replace('\\', '/').TrimStart('/');
        if (normalized.Length == 0)
        {
            throw new InvalidOperationException("A storage object path is required.");
        }

        if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
        {
            throw new InvalidOperationException("Storage object paths cannot contain '.' or '..' segments.");
        }

        return normalized;
    }

    private static string ResolveContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => "application/json",
            ".md" or ".markdown" => "text/markdown",
            ".mmd" or ".mermaid" => "text/vnd.mermaid",
            ".log" or ".txt" or ".cs" or ".razor" or ".css" or ".js" or ".ts" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static StorageContentKind ResolveContentKind(string path, string? contentType)
    {
        var normalizedContentType = contentType?.Trim() ?? string.Empty;
        if (normalizedContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Json;
        }

        if (normalizedContentType.Contains("markdown", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Markdown;
        }

        if (normalizedContentType.Contains("mermaid", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Mermaid;
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => StorageContentKind.Json,
            ".md" or ".markdown" => StorageContentKind.Markdown,
            ".mmd" or ".mermaid" => StorageContentKind.Mermaid,
            ".log" => StorageContentKind.Log,
            _ => StorageContentKind.Text
        };
    }

    private static StorageBrowseMetadataField ResolveBrowseMetadata(IStorageBrowseDriver driver)
    {
        if (!driver.Capabilities.HasFlag(StorageBrowseCapability.Metadata))
        {
            throw new InvalidOperationException(
                $"Storage provider '{driver.ProviderKind}' does not support browse metadata. Retry with includeMetadata=false.");
        }

        return StorageBrowseMetadataField.Size |
               StorageBrowseMetadataField.CreatedAtUtc |
               StorageBrowseMetadataField.ModifiedAtUtc |
               StorageBrowseMetadataField.MediaType;
    }

    private static AgentStorageBrowseEntryKind MapBrowseEntryKind(StorageBrowseEntryKind kind)
    {
        return kind switch
        {
            StorageBrowseEntryKind.File => AgentStorageBrowseEntryKind.File,
            StorageBrowseEntryKind.Container => AgentStorageBrowseEntryKind.Container,
            StorageBrowseEntryKind.Link => AgentStorageBrowseEntryKind.Link,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unsupported storage browse entry kind.")
        };
    }

    private AgentStorageBrowseEntryCapability MapBrowseEntryCapabilities(
        StorageBrowseEntryCapability capabilities,
        StorageCatalogRecord storage,
        IStorageDriver contentDriver)
    {
        var result = AgentStorageBrowseEntryCapability.None;
        if (capabilities.HasFlag(StorageBrowseEntryCapability.Browse))
        {
            result |= AgentStorageBrowseEntryCapability.Browse;
        }

        var effectiveCapabilities = storage.CapabilityMask & contentDriver.SupportedCapabilities;
        if (capabilities.HasFlag(StorageBrowseEntryCapability.Read) &&
            effectiveCapabilities.HasFlag(StorageCapability.Read))
        {
            result |= AgentStorageBrowseEntryCapability.Read;
        }

        if (accessSettings.CanWriteStorage &&
            !storage.IsReadOnly &&
            capabilities.HasFlag(StorageBrowseEntryCapability.Write) &&
            effectiveCapabilities.HasFlag(StorageCapability.Write))
        {
            result |= AgentStorageBrowseEntryCapability.Write;
        }

        if (accessSettings.CanWriteStorage &&
            !storage.IsReadOnly &&
            capabilities.HasFlag(StorageBrowseEntryCapability.Delete) &&
            effectiveCapabilities.HasFlag(StorageCapability.Delete))
        {
            result |= AgentStorageBrowseEntryCapability.Delete;
        }

        const StorageBrowseEntryCapability supported =
            StorageBrowseEntryCapability.Browse |
            StorageBrowseEntryCapability.Read |
            StorageBrowseEntryCapability.Write |
            StorageBrowseEntryCapability.Delete;
        if ((capabilities & ~supported) != StorageBrowseEntryCapability.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capabilities),
                capabilities,
                "Unsupported storage browse entry capabilities.");
        }

        return result;
    }

    private static AgentStorageBrowseCompleteness MapBrowseCompleteness(StorageBrowseCompleteness completeness)
    {
        return completeness switch
        {
            StorageBrowseCompleteness.Complete => AgentStorageBrowseCompleteness.Complete,
            StorageBrowseCompleteness.PartialInspectionLimit => AgentStorageBrowseCompleteness.PartialInspectionLimit,
            StorageBrowseCompleteness.PartialMetadataLimit => AgentStorageBrowseCompleteness.PartialMetadataLimit,
            StorageBrowseCompleteness.PartialTimeLimit => AgentStorageBrowseCompleteness.PartialTimeLimit,
            _ => throw new ArgumentOutOfRangeException(
                nameof(completeness),
                completeness,
                "Unsupported storage browse completeness value.")
        };
    }
}
