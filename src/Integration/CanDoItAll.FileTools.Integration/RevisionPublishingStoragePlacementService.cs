using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

internal sealed class RevisionPublishingStoragePlacementService(
    StoragePlacementService inner,
    IFileCatalogChangeSink changeSink) : IStoragePlacementService
{
    public async Task<StoragePlacementResult> PlaceAsync(
        StoragePlacementRequest request,
        CancellationToken cancellationToken = default)
    {
        StoragePlacementResult result = await inner.PlaceAsync(request, cancellationToken);
        changeSink.PublishStorageChanged(result.Storage.Id);
        return result;
    }
}
