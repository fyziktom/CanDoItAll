namespace CanDoItAll.Infrastructure.Storage;

public sealed record StoragePlacementRecoveryContext(Guid DatabaseProfileId, long Generation);

public enum StoragePlacementRecoveryOperation {
    Read,
    Reconcile,
    VerifyExternalTermination
}

public enum StoragePlacementRecoveryAction {
    None,
    Reconcile,
    VerifyExternalTermination
}

public enum StoragePlacementRecoveryBlock {
    None,
    ReadOnlyAuthority,
    MissingOwnerAssociation,
    InvalidOwnerAssociation,
    OriginalProjectLifetimeMissing,
    OriginalProjectUnavailable,
    ImportedHistory,
    OriginalStorageUnavailable,
    OriginalTargetConflict,
    DispatchNotStarted,
    RetainedConflict,
    DeletionRequested
}

public enum StoragePlacementRecoveryOwner {
    Unknown,
    ProcessAsset,
    WorkflowAsset
}

public sealed record StoragePlacementRecoveryIdentity(StoragePlacementIntentId IntentId,
    Guid? OriginalProjectId, Guid OriginalStorageId, StorageProviderKind OriginalProvider);

public sealed record StoragePlacementRecoveryOwnerObservation(StoragePlacementRecoveryOwner Owner,
    bool NativeReceiptPresent, StoragePlacementRecoveryBlock Block);

public sealed record StoragePlacementRecoveryItem(StoragePlacementRecoveryContext Context,
    StoragePlacementRecoveryIdentity Identity, StorageStablePlacementState StorageState,
    DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, bool StorageReceiptPresent,
    StoragePlacementRecoveryOwner Owner, bool NativeReceiptPresent, StoragePlacementRecoveryAction AvailableAction,
    StoragePlacementRecoveryBlock Block);

public sealed record StoragePlacementRecoveryQuery(StoragePlacementRecoveryContext Context,
    Guid? ProjectId = null, Guid? StorageId = null, int Take = 32, int Offset = 0);

public sealed record StoragePlacementRecoveryPage(IReadOnlyList<StoragePlacementRecoveryItem> Items, int? NextOffset);

public sealed record StoragePlacementRecoveryCommand(StoragePlacementRecoveryContext Context, StoragePlacementIntentId IntentId);

public sealed record StoragePlacementExternalTerminationVerification(StoragePlacementRecoveryContext Context,
    StoragePlacementIntentId IntentId, bool VerifiedExternalDispatchStopped);

public sealed record StoragePlacementRecoveryAuthorization(string Stamp, bool CanReconcile, bool CanVerifyExternalTermination);

public interface IStoragePlacementRecoveryAccess {
    Task<StoragePlacementRecoveryAuthorization> AuthorizeAsync(StoragePlacementRecoveryOperation operation,
        CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, StoragePlacementRecoveryOwnerObservation>> InspectOwnersAsync(
        StoragePlacementRecoveryAuthorization authorization, IReadOnlyCollection<StoragePlacementRecoveryIdentity> identities,
        CancellationToken cancellationToken);
    Task<StoragePlacementContinuationScan> ListContinuationsAsync(StoragePlacementRecoveryAuthorization authorization,
        Guid? projectId, int take, int offset, CancellationToken cancellationToken);
    Task RequireOriginalOwnerForMutationAsync(StoragePlacementRecoveryAuthorization authorization,
        StoragePlacementRecoveryIdentity identity, CancellationToken cancellationToken);
    Task EnsureCurrentAsync(StoragePlacementRecoveryAuthorization authorization, CancellationToken cancellationToken);
}

public enum StoragePlacementRecoveryFailure {
    Denied,
    StaleContext,
    NotFound,
    Blocked,
    InvalidRequest,
    Unavailable
}

public sealed class StoragePlacementRecoveryException(StoragePlacementRecoveryFailure failure)
    : InvalidOperationException("The Storage recovery operation is unavailable or no longer authorized. Refresh its status before continuing.") {
    public StoragePlacementRecoveryFailure Failure { get; } = failure;
}
