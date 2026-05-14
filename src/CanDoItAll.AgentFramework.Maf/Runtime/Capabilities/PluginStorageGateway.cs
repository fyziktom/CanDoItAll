using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Maf;

public interface IPluginStorageGateway
{
    Task<PluginStorageAccessDescriptor> DescribeAsync(
        StorageObjectReference reference,
        CancellationToken cancellationToken = default);

    Task<PluginStoragePlacementResult> PlaceAsync(
        PluginStoragePlacementRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class PluginStorageGateway(
    IStorageAccessService accessService,
    IStoragePlacementService placementService) : IPluginStorageGateway
{
    public async Task<PluginStorageAccessDescriptor> DescribeAsync(
        StorageObjectReference reference,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        var descriptor = await accessService.DescribeAsync(reference, cancellationToken).ConfigureAwait(false);
        return MapAccessDescriptor(descriptor);
    }

    public async Task<PluginStoragePlacementResult> PlaceAsync(
        PluginStoragePlacementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await placementService.PlaceAsync(
                new StoragePlacementRequest(
                    request.FileName,
                    request.ContentType,
                    request.Content,
                    request.UsagePurpose,
                    request.ContentKind,
                    request.ProjectId,
                    request.NodeKey,
                    request.RelativePathHint,
                    request.PreviewRequired,
                    request.PublishIntent,
                    request.PreferredStorageId),
                cancellationToken)
            .ConfigureAwait(false);

        return new PluginStoragePlacementResult(
            result.Storage.Id,
            result.Storage.Name,
            result.Storage.ProviderKind,
            result.WriteResult.Reference,
            MapAccessDescriptor(result.WriteResult.AccessDescriptor),
            result.Route,
            result.RelativePath,
            result.Recommendation.Warnings);
    }

    private static PluginStorageAccessDescriptor MapAccessDescriptor(StorageAccessDescriptor descriptor)
        => new(
            descriptor.PreviewUrl,
            descriptor.DownloadUrl,
            descriptor.DirectUrl,
            descriptor.SupportsInlinePreview,
            descriptor.SupportsDownload,
            descriptor.SupportsOpenLocally,
            descriptor.DisplayFileName,
            descriptor.ContentType,
            descriptor.ContentLength,
            descriptor.ReasonWhenUnavailable);
}

public sealed record PluginStoragePlacementRequest(
    string FileName,
    string ContentType,
    byte[] Content,
    StorageUsagePurpose UsagePurpose,
    StorageContentKind ContentKind = StorageContentKind.Unknown,
    Guid? ProjectId = null,
    string? NodeKey = null,
    string? RelativePathHint = null,
    bool PreviewRequired = false,
    bool PublishIntent = false,
    Guid? PreferredStorageId = null);

public sealed record PluginStoragePlacementResult(
    Guid StorageId,
    string StorageName,
    StorageProviderKind ProviderKind,
    StorageObjectReference Reference,
    PluginStorageAccessDescriptor Access,
    string Route,
    string RelativePath,
    IReadOnlyList<string> Warnings);

public sealed record PluginStorageAccessDescriptor(
    string PreviewUrl,
    string DownloadUrl,
    string? DirectUrl,
    bool SupportsInlinePreview,
    bool SupportsDownload,
    bool SupportsOpenLocally,
    string DisplayFileName,
    string ContentType,
    long? ContentLength,
    string ReasonWhenUnavailable);
