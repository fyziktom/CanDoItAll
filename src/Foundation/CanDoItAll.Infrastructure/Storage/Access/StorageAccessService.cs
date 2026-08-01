namespace CanDoItAll.Infrastructure.Storage;

public sealed class StorageAccessService(
    IStorageCatalogService catalogService,
    IStorageDriverRegistry driverRegistry,
    IWorkspacePathResolver workspacePathResolver) : IStorageAccessService
{
    public async Task<StorageAccessDescriptor> DescribeAsync(
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);

        var storage = reference.StorageId.HasValue
            ? await catalogService.GetAsync(reference.StorageId.Value, cancellationToken)
            : null;
        var capabilityMask = ResolveCapabilityMask(reference, storage);
        var supportsInlinePreview = capabilityMask.HasFlag(StorageCapability.InlinePreview);
        var supportsDownload = capabilityMask.HasFlag(StorageCapability.Download) || capabilityMask.HasFlag(StorageCapability.Read);
        var supportsOpenLocally = SupportsOpenLocally(reference, storage, capabilityMask);

        return new StorageAccessDescriptor(
            supportsInlinePreview ? StorageJson.BuildPreviewUrl(reference) : string.Empty,
            supportsDownload ? StorageJson.BuildDownloadUrl(reference) : string.Empty,
            ResolveDirectUrl(reference, storage),
            supportsInlinePreview,
            supportsDownload,
            supportsOpenLocally,
            string.IsNullOrWhiteSpace(reference.DisplayName) ? reference.Locator : reference.DisplayName,
            string.IsNullOrWhiteSpace(reference.ContentType) ? "application/octet-stream" : reference.ContentType,
            reference.ContentLength,
            BuildReasonWhenUnavailable(supportsInlinePreview, supportsDownload, supportsOpenLocally));
    }

    private StorageCapability ResolveCapabilityMask(StorageObjectReference reference, StorageCatalogRecord? storage)
    {
        if (storage is not null)
        {
            var capabilityMask = storage.CapabilityMask;
            if (driverRegistry.TryResolve(storage.ProviderKind, out var driver))
            {
                capabilityMask &= driver.SupportedCapabilities;
            }

            return capabilityMask;
        }

        return reference.ProviderKind switch
        {
            StorageProviderKind.FileSystem => StorageCapability.Read |
                                              StorageCapability.Download |
                                              StorageCapability.InlinePreview |
                                              StorageCapability.OpenLocally,
            StorageProviderKind.Ipfs => StorageCapability.Read |
                                        StorageCapability.Download |
                                        StorageCapability.InlinePreview |
                                        StorageCapability.DirectUrl,
            StorageProviderKind.Ftp => StorageCapability.Read | StorageCapability.Download,
            _ => StorageCapability.None
        };
    }

    private bool SupportsOpenLocally(
        StorageObjectReference reference,
        StorageCatalogRecord? storage,
        StorageCapability capabilityMask)
    {
        if (reference.ProviderKind != StorageProviderKind.FileSystem ||
            !capabilityMask.HasFlag(StorageCapability.OpenLocally))
        {
            return false;
        }

        if (storage is null)
        {
            return reference.LocatorKind == StorageLocatorKind.RelativePath;
        }

        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var storageRoot = Path.GetFullPath(storage.EndpointOrRoot);
        return storageRoot.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveDirectUrl(StorageObjectReference reference, StorageCatalogRecord? storage)
    {
        if (!string.IsNullOrWhiteSpace(reference.Route) &&
            Uri.TryCreate(reference.Route, UriKind.Absolute, out var directUri))
        {
            return directUri.ToString();
        }

        if (reference.ProviderKind != StorageProviderKind.Ipfs || storage is null)
        {
            return null;
        }

        var configuration = StorageJson.ParseProviderConfiguration(storage.ConfigJson);
        if (string.IsNullOrWhiteSpace(configuration.GatewayBaseUrl))
        {
            return null;
        }

        var gatewayBaseUrl = configuration.GatewayBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? configuration.GatewayBaseUrl
            : configuration.GatewayBaseUrl + "/";
        return new Uri(new Uri(gatewayBaseUrl), reference.Locator).ToString();
    }

    private static string BuildReasonWhenUnavailable(
        bool supportsInlinePreview,
        bool supportsDownload,
        bool supportsOpenLocally)
    {
        if (supportsInlinePreview && supportsDownload && supportsOpenLocally)
        {
            return string.Empty;
        }

        var blockedActions = new List<string>();
        if (!supportsInlinePreview)
        {
            blockedActions.Add("preview");
        }

        if (!supportsDownload)
        {
            blockedActions.Add("download");
        }

        if (!supportsOpenLocally)
        {
            blockedActions.Add("local open");
        }

        return blockedActions.Count == 0
            ? string.Empty
            : $"This storage object does not support {string.Join(", ", blockedActions)}.";
    }
}
