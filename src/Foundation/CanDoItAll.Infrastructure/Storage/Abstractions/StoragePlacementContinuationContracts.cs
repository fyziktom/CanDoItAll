namespace CanDoItAll.Infrastructure.Storage;

public enum StoragePlacementContinuationPhase {
    NativeCommit,
    CoreCheckpoint,
    WorkflowAcknowledgement,
    OwnerEvidenceUnavailable
}

public sealed record StoragePlacementContinuationCandidate(StoragePlacementIntentId IntentId,
    StoragePlacementContinuationPhase Phase);

public sealed record StoragePlacementContinuationScan(IReadOnlyList<StoragePlacementContinuationCandidate> Items,
    int? NextOffset);

public sealed record StoragePlacementContinuationItem(StoragePlacementRecoveryItem Storage,
    StoragePlacementContinuationPhase Phase);

public sealed record StoragePlacementContinuationPage(IReadOnlyList<StoragePlacementContinuationItem> Items,
    int? NextOffset);
