namespace CanDoItAll.Infrastructure.Storage;

public interface IStoragePlacementRecovery {
    Task<StoragePlacementRecoveryContext> GetCurrentContextAsync(CancellationToken cancellationToken = default);
    Task<StoragePlacementRecoveryPage> ListPendingAsync(StoragePlacementRecoveryQuery query, CancellationToken cancellationToken = default);
    Task<StoragePlacementContinuationPage> ListPendingContinuationsAsync(StoragePlacementRecoveryQuery query,
        CancellationToken cancellationToken = default);
    Task<StoragePlacementRecoveryItem> GetAsync(StoragePlacementRecoveryCommand request, CancellationToken cancellationToken = default);
    Task<StoragePlacementRecoveryItem> ReconcileAsync(StoragePlacementRecoveryCommand request, CancellationToken cancellationToken = default);
    Task<StoragePlacementRecoveryItem> RecordOperatorVerifiedExternalDispatchTerminationAsync(
        StoragePlacementExternalTerminationVerification request, CancellationToken cancellationToken = default);
}
