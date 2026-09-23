using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Modules.Workbench;

public sealed class ProjectWorkflowAssetContinuationService(StoragePlacementRecoveryService storage,
    IStoragePlacementRecoveryAccess access, WorkbenchProjectStructureRuntimeGateway gateway,
    IDatabaseRuntimeState runtime, IDatabaseRuntimeWriteFence profileFence) {
    public async Task<StoragePlacementOwnerContinuationObservation> GetAsync(StoragePlacementRecoveryCommand request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = await access.AuthorizeAsync(StoragePlacementRecoveryOperation.Read, cancellationToken);
        return await ReadAsync(request, authorization, cancellationToken);
    }

    public async Task<StoragePlacementOwnerContinuationObservation> ReconcileAsync(StoragePlacementWorkflowContinuationCommand request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Action is not (StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt or
                StoragePlacementOwnerContinuationAction.CompletePreparedWorkflowAsset) || request.ExpectedIntent is not { } intent ||
                intent.RunId == Guid.Empty || !IsHash(intent.OccurrencePath) || intent.Slot is < 0 or > 4095 || !IsHash(intent.Fingerprint)) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.InvalidRequest);
        }
        var authorization = await access.AuthorizeAsync(StoragePlacementRecoveryOperation.Reconcile, cancellationToken);
        var readRequest = new StoragePlacementRecoveryCommand(request.Context, request.IntentId);
        var expected = runtime.GetSnapshot();
        try {
            return await profileFence.ExecuteAsync(expected, async token => {
                var current = await ReadAsync(readRequest, authorization, token);
                if (current.WorkflowIntent != request.ExpectedIntent || current.AvailableAction != request.Action) {
                    throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
                }
                var guard = new WorkflowAssetContinuationGuard(
                    request.Action == StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt,
                    async ct => {
                        await access.EnsureCurrentAsync(authorization, ct);
                        var observed = await storage.GetAsync(readRequest, ct);
                        if (observed.Identity != current.Storage.Identity || !CanContinue(observed)) {
                            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
                        }
                    },
                    ct => access.RequireOriginalOwnerForMutationAsync(authorization, current.Storage.Identity, ct));
                var result = await gateway.ContinueWorkflowAssetAsync(request, guard, token);
                var refreshed = await ReadAsync(readRequest, authorization, token);
                if (refreshed.WorkflowIntent != request.ExpectedIntent || !CanContinue(refreshed.Storage) ||
                        refreshed.State == StoragePlacementOwnerContinuationState.CurrentReadDenied) {
                    return refreshed;
                }
                return result.WorkflowManifestPending
                    ? refreshed with { State = StoragePlacementOwnerContinuationState.WorkflowManifestPending,
                        AvailableAction = StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt }
                    : refreshed;
            }, cancellationToken);
        } catch (DatabaseRuntimeProfileChangedException) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.StaleContext);
        } catch (WorkflowStructureOutputConflictException) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
        } catch (StorageStablePlacementConflictException) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
        } catch (ProjectWriteAdmissionRejectedException) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Blocked);
        } catch (ProjectStructureAgentException) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.Denied);
        }
    }

    private async Task<StoragePlacementOwnerContinuationObservation> ReadAsync(StoragePlacementRecoveryCommand request,
        StoragePlacementRecoveryAuthorization authorization, CancellationToken cancellationToken) {
        var item = await storage.GetAsync(request, cancellationToken);
        WorkflowAssetContinuationSnapshot snapshot;
        if (item.Block != StoragePlacementRecoveryBlock.None || item.Owner != StoragePlacementRecoveryOwner.WorkflowAsset) {
            snapshot = new(null, StoragePlacementOwnerContinuationState.OriginalOwnerBlocked, StoragePlacementOwnerContinuationAction.None);
        } else if (item.StorageState != StorageStablePlacementState.Completed || !item.StorageReceiptPresent) {
            snapshot = new(null, StoragePlacementOwnerContinuationState.StorageNotCompleted, StoragePlacementOwnerContinuationAction.None);
        } else {
            try {
                snapshot = await gateway.ObserveWorkflowAssetContinuationAsync(item, cancellationToken);
            } catch (ProjectStructureAgentException) {
                snapshot = new(null, StoragePlacementOwnerContinuationState.CurrentReadDenied, StoragePlacementOwnerContinuationAction.None);
            } catch (WorkflowStructureLegacyLineageException) {
                snapshot = new(null, StoragePlacementOwnerContinuationState.WorkflowSourceMissing, StoragePlacementOwnerContinuationAction.None);
            } catch (WorkflowStructureOutputConflictException) {
                snapshot = new(null, StoragePlacementOwnerContinuationState.OriginalProposalUnavailable, StoragePlacementOwnerContinuationAction.None);
            } catch (JsonException) {
                snapshot = new(null, StoragePlacementOwnerContinuationState.OriginalProposalUnavailable, StoragePlacementOwnerContinuationAction.None);
            }
        }
        if (snapshot.Action != StoragePlacementOwnerContinuationAction.None && !authorization.CanReconcile) {
            snapshot = snapshot with { State = StoragePlacementOwnerContinuationState.ReadOnlyAuthority, Action = StoragePlacementOwnerContinuationAction.None };
        }
        await access.EnsureCurrentAsync(authorization, cancellationToken);
        var refreshed = await storage.GetAsync(request, cancellationToken);
        if (refreshed.Identity != item.Identity || refreshed.Block != item.Block || refreshed.StorageState != item.StorageState ||
                refreshed.StorageReceiptPresent != item.StorageReceiptPresent) {
            snapshot = new(null, StoragePlacementOwnerContinuationState.OriginalOwnerBlocked, StoragePlacementOwnerContinuationAction.None);
        }
        return new(refreshed, snapshot.Action, snapshot.State, WorkflowIntent: snapshot.Intent);
    }

    private static bool CanContinue(StoragePlacementRecoveryItem item)
        => item.Owner == StoragePlacementRecoveryOwner.WorkflowAsset && item.Block == StoragePlacementRecoveryBlock.None &&
            item.StorageState == StorageStablePlacementState.Completed && item.StorageReceiptPresent;

    private static bool IsHash(string value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);
}
