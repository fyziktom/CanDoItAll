namespace CanDoItAll.Infrastructure.Storage;

/// <summary>
/// Owner step still missing for a completed storage placement, as a JSON integer: 0 NativeCommit (the owner has not
/// recorded its own receipt for the placement), 1 CoreCheckpoint (process asset: the agent run's asset checkpoint is
/// not complete), 2 WorkflowAcknowledgement (workflow asset: the workflow has not acknowledged the asset) or 3
/// OwnerEvidenceUnavailable (the owner's records cannot be read or used, for example an invalid owner association or
/// imported history).
/// </summary>
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

/// <summary>
/// A completed storage placement whose owner has not finished recording it, with the missing owner step.
/// </summary>
/// <param name="Storage">Recovery state of the placement; its storage state is Completed or Deleted.</param>
/// <param name="Phase">
/// The missing owner step, as a JSON integer: 0 NativeCommit, 1 CoreCheckpoint, 2 WorkflowAcknowledgement or 3
/// OwnerEvidenceUnavailable.
/// </param>
public sealed record StoragePlacementContinuationItem(StoragePlacementRecoveryItem Storage,
    StoragePlacementContinuationPhase Phase);

/// <summary>
/// One page of completed storage placements waiting for their owner, returned by
/// <c>GET /api/storage-placement-recovery/pending-continuations</c>.
/// </summary>
/// <param name="Items">
/// The placements of this page. It can hold fewer items than <c>take</c>, or none, while <c>nextOffset</c> is not null.
/// </param>
/// <param name="NextOffset">
/// Value to send as <c>offset</c> for the next page; null when all owner candidates were examined.
/// </param>
public sealed record StoragePlacementContinuationPage(IReadOnlyList<StoragePlacementContinuationItem> Items,
    int? NextOffset);
