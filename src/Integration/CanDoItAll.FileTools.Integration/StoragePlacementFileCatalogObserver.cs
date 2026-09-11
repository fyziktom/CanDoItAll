using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.FileTools.Integration;

internal sealed class StoragePlacementFileCatalogObserver(IFileCatalogChangeSink changeSink) : IStoragePlacementReceiptObserver {
    public Task ObserveAsync(StorageStablePlacementReceipt receipt, CancellationToken cancellationToken) {
        cancellationToken.ThrowIfCancellationRequested();
        changeSink.PublishStorageChanged(receipt.Storage.Id);
        return Task.CompletedTask;
    }
}
