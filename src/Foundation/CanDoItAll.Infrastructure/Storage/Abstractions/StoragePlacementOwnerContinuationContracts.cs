namespace CanDoItAll.Infrastructure.Storage;

/// <summary>
/// Owner continuation the caller may run for a completed storage placement, as a JSON integer: 0 None, 1
/// ReconcileCancelledRunReceipts (reconcile the receipts of the cancelled agent run with
/// <c>POST /api/storage-placement-recovery/reconcile-cancelled-run-receipts</c>), 2 RecordWorkflowAssetReceipt (the
/// asset node exists; record the workflow receipt with
/// <c>POST /api/storage-placement-recovery/continue-workflow-asset</c>) or 3 CompletePreparedWorkflowAsset (create the
/// workflow's prepared asset node with the same operation).
/// </summary>
public enum StoragePlacementOwnerContinuationAction {
    None,
    ReconcileCancelledRunReceipts,
    RecordWorkflowAssetReceipt,
    CompletePreparedWorkflowAsset
}

/// <summary>
/// Owner state of a completed storage placement, as a JSON integer: 0 Ready (a continuation can run now; see the
/// available action), 1 ReceiptRecorded (the owner recorded the asset), 2 NativeReceiptNotObserved (reconciling the
/// cancelled run found no receipt; the effect stays uncertain and the action stays available), 3 CurrentReadDenied (the
/// caller may not read the owner's current state), 4 OriginalProposalUnavailable (the original tool proposal or
/// prepared workflow output is missing or does not match), 5 OriginalRunNotCancelled (the original agent run was not
/// cancelled, so this continuation does not apply), 6 StorageNotCompleted (the storage write is not completed with a
/// receipt), 7 OriginalOwnerBlocked (the placement is blocked, has no supported owner, or changed during the read), 8
/// WorkflowContinuationUnavailable (the workflow continuation is unavailable), 9 ReadOnlyAuthority (a continuation is
/// possible, but the caller lacks the scopes to run it), 10 OwnerReceiptUnconfirmed (the owner has no receipt protocol
/// to confirm the effect), 11 ContinuationUnavailable (the continuation could not run or its result did not match the
/// original), 12 WorkflowOutputMissing (the workflow's output record was not found), 13 WorkflowRunMissing (the
/// workflow run was not found), 14 WorkflowSourceMissing (the workflow's source authority is missing), 15
/// WorkflowSourceCannotContinue (the workflow's source no longer authorizes the output), 16 WorkflowRunCancelled (the
/// workflow run was cancelled), 17 WorkflowTargetChanged (the target node changed or no longer exists) or 18
/// WorkflowManifestPending (the asset is in the project structure but the workflow has not acknowledged it).
/// </summary>
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

/// <summary>
/// Identity of the prepared workflow output that a completed storage placement belongs to, returned as
/// <c>workflowIntent</c> by <c>GET /api/storage-placement-recovery/{intentId}/owner-continuation</c>. Send it back
/// unchanged as <c>expectedIntent</c>; the host rejects a continuation whose output differs.
/// </summary>
/// <param name="RunId">Identifier (GUID) of the workflow run; not the empty GUID.</param>
/// <param name="OccurrencePath">
/// Opaque identifier of the workflow step occurrence within the run: 64 hexadecimal characters.
/// </param>
/// <param name="Slot">Output slot of the step occurrence, from 0 through 4095.</param>
/// <param name="Fingerprint">Fingerprint of the prepared output: 64 hexadecimal characters.</param>
public sealed record StoragePlacementWorkflowContinuationIntent(Guid RunId, string OccurrencePath, int Slot, string Fingerprint);

/// <summary>
/// Request body of <c>POST /api/storage-placement-recovery/continue-workflow-asset</c>: which workflow continuation to
/// run for a completed placement, bound to the state the caller read. Send every member.
/// </summary>
/// <param name="Context">
/// The current database runtime context from <c>GET /api/storage-placement-recovery/context</c>.
/// </param>
/// <param name="IntentId">
/// The placement intent, as an object whose <c>value</c> is the intent GUID. Must not be the empty GUID.
/// </param>
/// <param name="Action">
/// Continuation to run, as a JSON integer: 2 RecordWorkflowAssetReceipt or 3 CompletePreparedWorkflowAsset. It must
/// equal <c>availableAction</c> of the latest owner-continuation read; other values are rejected with HTTP 400.
/// </param>
/// <param name="ExpectedIntent">
/// <c>workflowIntent</c> of the latest owner-continuation read, unchanged. Required.
/// </param>
public sealed record StoragePlacementWorkflowContinuationCommand(StoragePlacementRecoveryContext Context,
    StoragePlacementIntentId IntentId, StoragePlacementOwnerContinuationAction Action,
    StoragePlacementWorkflowContinuationIntent ExpectedIntent);

/// <summary>
/// Owner continuation state of a completed storage placement: the placement, the owner's state and the continuation
/// the caller may run. Returned by the owner-continuation read and by the continuation commands, which return it as
/// read after their attempt.
/// </summary>
/// <param name="Storage">Recovery state of the placement.</param>
/// <param name="AvailableAction">
/// Continuation the caller may run now, as a JSON integer: 0 None, 1 ReconcileCancelledRunReceipts, 2
/// RecordWorkflowAssetReceipt or 3 CompletePreparedWorkflowAsset.
/// </param>
/// <param name="State">
/// Owner state, as a JSON integer from 0 Ready through 18 WorkflowManifestPending; the
/// <c>StoragePlacementOwnerContinuationState</c> schema lists and explains every value. The most common are 0 Ready, 1
/// ReceiptRecorded, 2 NativeReceiptNotObserved, 6 StorageNotCompleted, 7 OriginalOwnerBlocked and 9 ReadOnlyAuthority.
/// </param>
/// <param name="OriginalExecutionRunId">
/// Identifier (GUID) of the original agent execution run of a process asset, when it could be resolved; otherwise null.
/// </param>
/// <param name="WorkflowIntent">
/// Identity of the prepared workflow output of a workflow asset, to send back as <c>expectedIntent</c>; null when the
/// owner is not a workflow or the output could not be resolved.
/// </param>
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
