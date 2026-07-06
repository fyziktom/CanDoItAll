using System.Text;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class StorageRuntimePlugin(
    IStorageCatalogService catalogService,
    IStorageDriverRegistry driverRegistry,
    AgentWorkspaceToolAccessSettings accessSettings)
{
    private readonly IStorageCatalogService catalogService = catalogService;
    private readonly IStorageDriverRegistry driverRegistry = driverRegistry;
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
        var normalizedLocator = NormalizeStoragePath(locator);
        return new StorageObjectReference(
            storage.Id,
            storage.ProviderKind,
            ResolveLocatorKind(storage.ProviderKind),
            normalizedLocator,
            Path.GetFileName(normalizedLocator),
            ResolveContentType(normalizedLocator));
    }

    private static StorageLocatorKind ResolveLocatorKind(StorageProviderKind providerKind)
    {
        return providerKind switch
        {
            StorageProviderKind.Ipfs => StorageLocatorKind.ContentAddress,
            StorageProviderKind.Ftp => StorageLocatorKind.RemotePath,
            _ => StorageLocatorKind.RelativePath
        };
    }

    private static string NormalizeStoragePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("A storage object path is required.");
        }

        var normalized = path.Trim().Replace('\\', '/').TrimStart('/');
        if (normalized.Contains("..", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage object paths cannot contain '..'.");
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
}
