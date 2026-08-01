using Microsoft.Extensions.Logging;

namespace CanDoItAll.Infrastructure.Storage;

public sealed class StoragePlacementService(
    IStorageCatalogService catalogService,
    IStorageRoutingService routingService,
    IStorageDriverRegistry driverRegistry,
    ILogger<StoragePlacementService> logger) : IStoragePlacementService
{
    public async Task<StoragePlacementResult> PlaceAsync(
        StoragePlacementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var recommendation = await ResolveRecommendationAsync(request, cancellationToken);
        var primaryCandidate = recommendation.PrimaryCandidate
            ?? throw new InvalidOperationException(recommendation.Reason);
        var storage = await catalogService.GetAsync(primaryCandidate.StorageId, cancellationToken)
            ?? throw new InvalidOperationException($"Storage '{primaryCandidate.StorageName}' no longer exists.");

        ValidateStorage(storage, request);

        if (!driverRegistry.TryResolve(storage.ProviderKind, out var driver))
        {
            throw new InvalidOperationException($"No storage driver is registered for provider '{storage.ProviderKind}'.");
        }

        if (recommendation.Warnings.Count > 0)
        {
            logger.LogWarning(
                "Storage placement used warnings for {UsagePurpose} ({FileName}): {Warnings}",
                request.UsagePurpose,
                request.FileName,
                string.Join("; ", recommendation.Warnings));
        }

        var writeResult = await driver.SaveAsync(
            storage,
            new StorageWriteRequest(
                request.FileName,
                request.ContentType,
                request.Content,
                request.UsagePurpose,
                request.ContentKind,
                request.ProjectId,
                request.NodeKey,
                request.RelativePathHint,
                request.PreviewRequired,
                request.PublishIntent),
            cancellationToken);

        var route = ResolveRoute(writeResult);
        var location = ResolveLocation(storage, writeResult.Reference, route);
        var relativePath = ResolveRelativePath(writeResult.Reference);

        return new StoragePlacementResult(
            storage,
            recommendation,
            writeResult,
            route,
            location,
            relativePath);
    }

    private async Task<StorageRecommendation> ResolveRecommendationAsync(
        StoragePlacementRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.PreferredStorageId.HasValue)
        {
            return await routingService.RecommendAsync(
                new StorageSelectionContext(
                    request.FileName,
                    request.ContentType,
                    request.UsagePurpose,
                    request.ContentKind,
                    request.ProjectId,
                    request.NodeKey,
                    request.Content.LongLength,
                    PreviewRequired: request.PreviewRequired,
                    PublishIntent: request.PublishIntent),
                cancellationToken);
        }

        var storage = await catalogService.GetAsync(request.PreferredStorageId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Storage '{request.PreferredStorageId.Value}' does not exist.");

        return new StorageRecommendation(
            new StorageRecommendationCandidate(
                storage.Id,
                storage.Name,
                storage.ProviderKind,
                storage.CapabilityMask,
                storage.HealthStatus,
                storage.IsReadOnly,
                "Explicit storage override."),
            [],
            "Explicit storage override.",
            []);
    }

    private static void ValidateStorage(StorageCatalogRecord storage, StoragePlacementRequest request)
    {
        if (!storage.IsEnabled)
        {
            throw new InvalidOperationException($"Storage '{storage.Name}' is disabled.");
        }

        if (storage.IsReadOnly)
        {
            throw new InvalidOperationException($"Storage '{storage.Name}' is read-only.");
        }

        if (storage.HealthStatus == StorageHealthStatus.Unavailable)
        {
            throw new InvalidOperationException($"Storage '{storage.Name}' is unavailable.");
        }

        var requiredCapabilities = StorageCapability.Write |
            (request.PreviewRequired ? StorageCapability.InlinePreview : StorageCapability.None);
        if ((storage.CapabilityMask & requiredCapabilities) != requiredCapabilities)
        {
            throw new InvalidOperationException(
                $"Storage '{storage.Name}' does not satisfy the required capabilities '{requiredCapabilities}'.");
        }
    }

    private static string ResolveRoute(StorageWriteResult writeResult)
    {
        if (writeResult.AccessDescriptor.SupportsInlinePreview &&
            !string.IsNullOrWhiteSpace(writeResult.AccessDescriptor.PreviewUrl))
        {
            return writeResult.AccessDescriptor.PreviewUrl;
        }

        if (writeResult.AccessDescriptor.SupportsDownload &&
            !string.IsNullOrWhiteSpace(writeResult.AccessDescriptor.DownloadUrl))
        {
            return writeResult.AccessDescriptor.DownloadUrl;
        }

        if (!string.IsNullOrWhiteSpace(writeResult.AccessDescriptor.DirectUrl))
        {
            return writeResult.AccessDescriptor.DirectUrl;
        }

        return writeResult.Reference.Route ?? string.Empty;
    }

    private static string ResolveLocation(
        StorageCatalogRecord storage,
        StorageObjectReference reference,
        string route)
    {
        if (storage.ProviderKind == StorageProviderKind.FileSystem)
        {
            var pathPolicy = new FileSystemStoragePathPolicy(new StaticWorkspacePathResolver(storage.EndpointOrRoot));
            return pathPolicy.ResolveFullPath(storage, reference.Locator);
        }

        if (storage.ProviderKind == StorageProviderKind.Ipfs)
        {
            return !string.IsNullOrWhiteSpace(reference.Route)
                ? reference.Route
                : reference.Locator;
        }

        if (!string.IsNullOrWhiteSpace(storage.EndpointOrRoot))
        {
            return $"{storage.EndpointOrRoot.TrimEnd('/')}/{reference.Locator.TrimStart('/')}";
        }

        return string.IsNullOrWhiteSpace(route)
            ? reference.Locator
            : route;
    }

    private static string ResolveRelativePath(StorageObjectReference reference)
    {
        return reference.ProviderKind == StorageProviderKind.FileSystem &&
            reference.LocatorKind == StorageLocatorKind.RelativePath
            ? reference.Locator
            : string.Empty;
    }

    private sealed class StaticWorkspacePathResolver(string workspaceRoot) : IWorkspacePathResolver
    {
        public string ResolveWorkspaceRoot() => workspaceRoot;

        public string ResolveManagedFilesRoot() => Path.Combine(workspaceRoot, "managed-files");

        public string ResolveExportsRoot() => Path.Combine(workspaceRoot, "exports");

        public string ResolveEvidenceRoot() => Path.Combine(workspaceRoot, "evidence");

        public string ResolveManagerArtifactsRoot() => Path.Combine(workspaceRoot, "manager-artifacts");
    }
}
