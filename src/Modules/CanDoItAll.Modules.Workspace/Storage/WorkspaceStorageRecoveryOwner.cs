using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workspace.StorageRecovery.Contracts;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Workspace;

public sealed class WorkspaceStorageRecoveryOwner(IStoragePlacementRecovery recovery,
    IStoragePlacementOwnerContinuation continuation, ILogger<WorkspaceStorageRecoveryOwner> logger) : IStorageRecoveryOwner {
    public Task<RecoveryContext> GetContextAsync(CancellationToken cancellationToken) => ObserveAsync(async () => {
        var context = await recovery.GetCurrentContextAsync(cancellationToken);
        return new RecoveryContext(context.DatabaseProfileId, context.Generation);
    });

    public Task<RecoveryPage> ReadPageAsync(RecoveryContext context, Guid? storageId, RecoveryFeed feed, int offset, int take,
        CancellationToken cancellationToken) => ObserveAsync(async () => {
        var query = new StoragePlacementRecoveryQuery(Context(context), StorageId: storageId, Take: take, Offset: offset);
        if (feed == RecoveryFeed.Placements) {
            var page = await recovery.ListPendingAsync(query, cancellationToken);
            return new RecoveryPage([.. page.Items.Select(item => Item(item))], page.NextOffset);
        }
        if (feed != RecoveryFeed.OwnerFollowUps) {
            throw new RecoveryException(RecoveryFailure.InvalidRequest);
        }
        var followUps = await recovery.ListPendingContinuationsAsync(query, cancellationToken);
        return new RecoveryPage([.. followUps.Items.Select(item => Item(item.Storage, Phase(item.Phase)))], followUps.NextOffset);
    });

    public Task<RecoveryDetail> ReadDetailAsync(RecoveryTarget target, CancellationToken cancellationToken) =>
        ObserveAsync(async () => Detail(await continuation.GetAsync(Command(target), cancellationToken)), target);

    public Task<RecoveryResult> ExecuteAsync(RecoveryCommand command, CancellationToken cancellationToken) => ObserveAsync(async () => {
        var original = Command(command.Target);
        switch (command.Action) {
            case RecoveryAction.Reconcile:
                return new RecoveryResult(Item(await recovery.ReconcileAsync(original, cancellationToken)));
            case RecoveryAction.VerifyExternalTermination when command.VerifiedExternalDispatchStopped:
                return new RecoveryResult(Item(await recovery.RecordOperatorVerifiedExternalDispatchTerminationAsync(
                    new(original.Context, original.IntentId, true), cancellationToken)));
            case RecoveryAction.ReconcileCancelledRunReceipts:
                var cancelled = Detail(await continuation.ReconcileCancelledRunReceiptsAsync(original, cancellationToken));
                return new RecoveryResult(cancelled.Item, cancelled);
            case RecoveryAction.RecordWorkflowAssetReceipt or RecoveryAction.CompletePreparedWorkflowAsset when command.ExpectedWorkflow is { } expected:
                var action = command.Action == RecoveryAction.RecordWorkflowAssetReceipt
                    ? StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt
                    : StoragePlacementOwnerContinuationAction.CompletePreparedWorkflowAsset;
                var workflow = Detail(await continuation.ReconcileWorkflowAssetAsync(new(original.Context, original.IntentId,
                    action, new(expected.RunId, expected.OccurrencePath, expected.Slot, expected.Fingerprint)), cancellationToken));
                return new RecoveryResult(workflow.Item, workflow);
            default:
                throw new RecoveryException(RecoveryFailure.InvalidRequest);
        }
    }, command.Target);

    private async Task<T> ObserveAsync<T>(Func<Task<T>> operation, RecoveryTarget? target = null) {
        try {
            return await operation();
        } catch (StoragePlacementRecoveryException exception) {
            throw new RecoveryException(exception.Failure switch {
                StoragePlacementRecoveryFailure.Denied => RecoveryFailure.Denied,
                StoragePlacementRecoveryFailure.StaleContext => RecoveryFailure.StaleContext,
                StoragePlacementRecoveryFailure.NotFound => RecoveryFailure.NotFound,
                StoragePlacementRecoveryFailure.Blocked => RecoveryFailure.Blocked,
                StoragePlacementRecoveryFailure.InvalidRequest => RecoveryFailure.InvalidRequest,
                _ => RecoveryFailure.Unavailable
            });
        } catch (OperationCanceledException) {
            throw;
        } catch (RecoveryException) {
            throw;
        } catch (Exception exception) {
            logger.LogWarning("Storage recovery failed with {FailureType}; original profile {ProfileId}, generation {Generation}, intent {IntentId}.",
                exception.GetType().Name, target?.Context.DatabaseProfileId, target?.Context.Generation, target?.IntentId);
            throw new RecoveryException(RecoveryFailure.Unavailable);
        }
    }

    private static StoragePlacementRecoveryContext Context(RecoveryContext context) => new(context.DatabaseProfileId, context.Generation);
    private static StoragePlacementRecoveryCommand Command(RecoveryTarget target) => new(Context(target.Context), new(target.IntentId));
    private static RecoveryDetail Detail(StoragePlacementOwnerContinuationObservation value) => new(Item(value.Storage), value.AvailableAction switch {
        StoragePlacementOwnerContinuationAction.None => RecoveryAction.None,
        StoragePlacementOwnerContinuationAction.ReconcileCancelledRunReceipts => RecoveryAction.ReconcileCancelledRunReceipts,
        StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt => RecoveryAction.RecordWorkflowAssetReceipt,
        StoragePlacementOwnerContinuationAction.CompletePreparedWorkflowAsset => RecoveryAction.CompletePreparedWorkflowAsset,
        _ => throw new RecoveryException(RecoveryFailure.Unavailable)
    }, value.State.ToString(), value.OriginalExecutionRunId,
        value.WorkflowIntent is { } workflow ? new(workflow.RunId, workflow.OccurrencePath, workflow.Slot, workflow.Fingerprint) : null);

    private static RecoveryItem Item(StoragePlacementRecoveryItem item, string phase = "File placement") => new(
        new(new(item.Context.DatabaseProfileId, item.Context.Generation), item.Identity.IntentId.Value),
        item.Identity.OriginalProjectId, item.Identity.OriginalStorageId, item.StorageState.ToString(), item.UpdatedAtUtc,
        item.StorageReceiptPresent, item.NativeReceiptPresent, item.AvailableAction switch {
            StoragePlacementRecoveryAction.None => RecoveryAction.None,
            StoragePlacementRecoveryAction.Reconcile => RecoveryAction.Reconcile,
            StoragePlacementRecoveryAction.VerifyExternalTermination => RecoveryAction.VerifyExternalTermination,
            _ => throw new RecoveryException(RecoveryFailure.Unavailable)
        }, item.Block switch {
            StoragePlacementRecoveryBlock.None => "See the available action below.",
            StoragePlacementRecoveryBlock.ReadOnlyAuthority => "Read-only access",
            StoragePlacementRecoveryBlock.OriginalProjectLifetimeMissing => "Original project lifetime was not recorded",
            StoragePlacementRecoveryBlock.OriginalProjectUnavailable => "Original project is unavailable",
            StoragePlacementRecoveryBlock.ImportedHistory => "Imported history cannot authorize a new effect",
            StoragePlacementRecoveryBlock.OriginalStorageUnavailable => "Original storage is unavailable",
            StoragePlacementRecoveryBlock.OriginalTargetConflict => "Original target conflicts with current data",
            StoragePlacementRecoveryBlock.DispatchNotStarted => "Transfer was not started",
            StoragePlacementRecoveryBlock.RetainedConflict => "A retained conflict requires owner review",
            StoragePlacementRecoveryBlock.DeletionRequested => "Deletion was requested",
            _ => "Original owner evidence is unavailable or inconsistent"
        }, phase);

    private static string Phase(StoragePlacementContinuationPhase phase) => phase switch {
        StoragePlacementContinuationPhase.NativeCommit => "Native asset creation",
        StoragePlacementContinuationPhase.CoreCheckpoint => "Run receipt",
        StoragePlacementContinuationPhase.WorkflowAcknowledgement => "Workflow acknowledgement",
        _ => "Owner evidence unavailable"
    };
}
