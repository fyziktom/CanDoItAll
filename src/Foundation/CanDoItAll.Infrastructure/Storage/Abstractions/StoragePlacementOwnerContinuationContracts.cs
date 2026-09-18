namespace CanDoItAll.Infrastructure.Storage;

public enum StoragePlacementOwnerContinuationAction {
    None,
    ReconcileCancelledRunReceipts,
    RecordWorkflowAssetReceipt,
    CompletePreparedWorkflowAsset
}

public enum StoragePlacementOwnerContinuationState {
    Ready,
    ReceiptRecorded,
    NativeReceiptNotObserved,
    CurrentReadDenied,
    OriginalProposalUnavailable,
    OriginalRunNotCancelled,
    StorageNotCompleted,
    OriginalOwnerBlocked,
    WorkflowContinuationUnavailable,
    ReadOnlyAuthority,
    OwnerReceiptUnconfirmed,
    ContinuationUnavailable,
    WorkflowOutputMissing,
    WorkflowRunMissing,
    WorkflowSourceMissing,
    WorkflowSourceCannotContinue,
    WorkflowRunCancelled,
    WorkflowTargetChanged,
    WorkflowManifestPending
}

public sealed record StoragePlacementWorkflowContinuationIntent(Guid RunId, string OccurrencePath, int Slot, string Fingerprint);

public sealed record StoragePlacementWorkflowContinuationCommand(StoragePlacementRecoveryContext Context,
    StoragePlacementIntentId IntentId, StoragePlacementOwnerContinuationAction Action,
    StoragePlacementWorkflowContinuationIntent ExpectedIntent);

public sealed record StoragePlacementOwnerContinuationObservation(StoragePlacementRecoveryItem Storage,
    StoragePlacementOwnerContinuationAction AvailableAction, StoragePlacementOwnerContinuationState State,
    Guid? OriginalExecutionRunId = null, StoragePlacementWorkflowContinuationIntent? WorkflowIntent = null);

public interface IStoragePlacementOwnerContinuation {
    Task<StoragePlacementOwnerContinuationObservation> GetAsync(StoragePlacementRecoveryCommand request,
        CancellationToken cancellationToken = default);
    Task<StoragePlacementOwnerContinuationObservation> ReconcileCancelledRunReceiptsAsync(StoragePlacementRecoveryCommand request,
        CancellationToken cancellationToken = default);
    Task<StoragePlacementOwnerContinuationObservation> ReconcileWorkflowAssetAsync(StoragePlacementWorkflowContinuationCommand request,
        CancellationToken cancellationToken = default);
}
