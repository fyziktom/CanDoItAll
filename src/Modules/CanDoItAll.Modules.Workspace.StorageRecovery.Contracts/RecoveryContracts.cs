namespace CanDoItAll.Modules.Workspace.StorageRecovery.Contracts;

public sealed record RecoveryContext(Guid DatabaseProfileId, long Generation);
public sealed record RecoveryTarget(RecoveryContext Context, Guid IntentId);
public enum RecoveryFeed { Placements, OwnerFollowUps }
public enum RecoveryAction { None, Reconcile, VerifyExternalTermination, ReconcileCancelledRunReceipts, RecordWorkflowAssetReceipt, CompletePreparedWorkflowAsset }
public enum RecoveryFailure { Denied, StaleContext, NotFound, Blocked, InvalidRequest, Unavailable }

public sealed record RecoveryItem(RecoveryTarget Target, Guid? OriginalProjectId, Guid OriginalStorageId,
    string StorageState, DateTimeOffset UpdatedAtUtc, bool StorageReceiptPresent, bool NativeReceiptPresent,
    RecoveryAction AvailableAction, string BlockReason, string Phase);
public sealed record RecoveryPage(IReadOnlyList<RecoveryItem> Items, int? NextOffset);
public sealed record RecoveryWorkflowIntent(Guid RunId, string OccurrencePath, int Slot, string Fingerprint);
public sealed record RecoveryDetail(RecoveryItem Item, RecoveryAction OwnerAction, string OwnerState,
    Guid? OriginalExecutionRunId, RecoveryWorkflowIntent? WorkflowIntent);
public sealed record RecoveryCommand(RecoveryTarget Target, RecoveryAction Action,
    bool VerifiedExternalDispatchStopped = false, RecoveryWorkflowIntent? ExpectedWorkflow = null);
public sealed record RecoveryResult(RecoveryItem Item, RecoveryDetail? Detail = null);

public sealed class RecoveryException(RecoveryFailure failure) : InvalidOperationException("The original recovery target is unavailable or no longer authorized.") {
    public RecoveryFailure Failure { get; } = failure;
}

public interface IStorageRecoveryOwner {
    Task<RecoveryContext> GetContextAsync(CancellationToken cancellationToken);
    Task<RecoveryPage> ReadPageAsync(RecoveryContext context, Guid? storageId, RecoveryFeed feed, int offset, int take, CancellationToken cancellationToken);
    Task<RecoveryDetail> ReadDetailAsync(RecoveryTarget target, CancellationToken cancellationToken);
    Task<RecoveryResult> ExecuteAsync(RecoveryCommand command, CancellationToken cancellationToken);
}
