using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Web.Api;

internal sealed class StoragePlacementOwnerContinuationService(StoragePlacementRecoveryService storage,
    IStoragePlacementRecoveryAccess access, ProjectProcessAssetReceiptRecoveryQuery processAssets,
    AgentAssetCheckpointQuery checkpoints, IAgentFrameworkWorkspaceService workspace,
    IDatabaseRuntimeState runtime, IDatabaseRuntimeWriteFence profileFence,
    ProjectWorkflowAssetContinuationService workflows) : IStoragePlacementOwnerContinuation {
    public async Task<StoragePlacementOwnerContinuationObservation> GetAsync(StoragePlacementRecoveryCommand request,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = await access.AuthorizeAsync(StoragePlacementRecoveryOperation.Read, cancellationToken);
        return (await ReadAsync(request, authorization, cancellationToken)).Observation;
    }

    public async Task<StoragePlacementOwnerContinuationObservation> ReconcileCancelledRunReceiptsAsync(
        StoragePlacementRecoveryCommand request, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(request);
        var authorization = await access.AuthorizeAsync(StoragePlacementRecoveryOperation.Reconcile, cancellationToken);
        var expected = runtime.GetSnapshot();
        try {
            return await profileFence.ExecuteAsync(expected, async token => {
                var current = await ReadAsync(request, authorization, token);
                if (current.Binding is not { } original || current.Observation.State is not
                    (StoragePlacementOwnerContinuationState.Ready or StoragePlacementOwnerContinuationState.ReceiptRecorded)) {
                    return current.Observation;
                }
                await access.EnsureCurrentAsync(authorization, token);
                StoragePlacementOwnerContinuationState state;
                try {
                    var result = await workspace.ReconcileCancelledExecutionRunAsync(original.OriginalSession.ExecutionRunId,
                        AgentExecutionOperationId.New(), token);
                    state = ResultState(result, original);
                } catch (AgentToolAdmissionException) {
                    state = StoragePlacementOwnerContinuationState.ContinuationUnavailable;
                } catch (ProcessLaunchAuthorityRejectedException) {
                    state = StoragePlacementOwnerContinuationState.CurrentReadDenied;
                }
                var refreshed = (await ReadAsync(request, authorization, token)).Observation;
                if (refreshed.State is not (StoragePlacementOwnerContinuationState.Ready or StoragePlacementOwnerContinuationState.ReceiptRecorded)) {
                    return refreshed;
                }
                return refreshed with { State = state, AvailableAction = state == StoragePlacementOwnerContinuationState.NativeReceiptNotObserved
                    ? StoragePlacementOwnerContinuationAction.ReconcileCancelledRunReceipts : StoragePlacementOwnerContinuationAction.None };
            }, cancellationToken);
        } catch (DatabaseRuntimeProfileChangedException) {
            throw new StoragePlacementRecoveryException(StoragePlacementRecoveryFailure.StaleContext);
        }
    }

    public Task<StoragePlacementOwnerContinuationObservation> ReconcileWorkflowAssetAsync(StoragePlacementWorkflowContinuationCommand request,
        CancellationToken cancellationToken = default)
        => workflows.ReconcileAsync(request, cancellationToken);

    private async Task<(StoragePlacementOwnerContinuationObservation Observation, ProjectProcessAssetReceiptRecoveryBinding? Binding)> ReadAsync(
        StoragePlacementRecoveryCommand request, StoragePlacementRecoveryAuthorization authorization, CancellationToken cancellationToken) {
        var item = await storage.GetAsync(request, cancellationToken);
        ProjectProcessAssetReceiptRecoveryBinding? binding = null;
        StoragePlacementOwnerContinuationState state;
        if (item.Owner == StoragePlacementRecoveryOwner.WorkflowAsset) {
            return (await workflows.GetAsync(request, cancellationToken), null);
        }
        if (item.Block != StoragePlacementRecoveryBlock.None || item.Owner != StoragePlacementRecoveryOwner.ProcessAsset) {
            state = StoragePlacementOwnerContinuationState.OriginalOwnerBlocked;
        } else if (item.StorageState != StorageStablePlacementState.Completed || !item.StorageReceiptPresent) {
            state = StoragePlacementOwnerContinuationState.StorageNotCompleted;
        } else {
            try {
                binding = await processAssets.ReadAsync(request.IntentId, cancellationToken);
                if (binding is null || binding.StorageIntentId != item.Identity.IntentId ||
                    binding.OriginalProject.DatabaseProfileId != item.Context.DatabaseProfileId ||
                    binding.OriginalProject.ProjectId != item.Identity.OriginalProjectId) {
                    binding = null;
                    state = StoragePlacementOwnerContinuationState.OriginalProposalUnavailable;
                } else {
                    state = (await checkpoints.ReadCancelledReceiptStateAsync(binding.OriginalSession, binding.OriginalProposal,
                        binding.ToolName, cancellationToken)) switch {
                        AgentAssetCancelledReceiptState.Ready => authorization.CanReconcile
                            ? StoragePlacementOwnerContinuationState.Ready : StoragePlacementOwnerContinuationState.ReadOnlyAuthority,
                        AgentAssetCancelledReceiptState.ReceiptRecorded => StoragePlacementOwnerContinuationState.ReceiptRecorded,
                        AgentAssetCancelledReceiptState.OriginalRunNotCancelled => StoragePlacementOwnerContinuationState.OriginalRunNotCancelled,
                        _ => StoragePlacementOwnerContinuationState.OriginalProposalUnavailable
                    };
                }
            } catch (ProcessLaunchAuthorityRejectedException) {
                binding = null;
                state = StoragePlacementOwnerContinuationState.CurrentReadDenied;
            }
        }
        await access.EnsureCurrentAsync(authorization, cancellationToken);
        var refreshed = await storage.GetAsync(request, cancellationToken);
        if (refreshed.Identity != item.Identity || refreshed.Block != item.Block || refreshed.StorageState != item.StorageState) {
            binding = null;
            state = StoragePlacementOwnerContinuationState.OriginalOwnerBlocked;
        }
        var observation = new StoragePlacementOwnerContinuationObservation(refreshed, state == StoragePlacementOwnerContinuationState.Ready
            ? StoragePlacementOwnerContinuationAction.ReconcileCancelledRunReceipts : StoragePlacementOwnerContinuationAction.None,
            state, binding?.OriginalSession.ExecutionRunId);
        return (observation, binding);
    }

    private static StoragePlacementOwnerContinuationState ResultState(AgentToolRunCancellationReconciliation result,
        ProjectProcessAssetReceiptRecoveryBinding original) {
        var target = result.Outcomes.SingleOrDefault(item => item.IntentId == original.OriginalProposal.IntentId);
        if (result.ExecutionRunId != original.OriginalSession.ExecutionRunId || result.ChatSessionId != original.OriginalSession.ChatSessionId ||
            target is null || target.ToolName != original.ToolName) {
            return StoragePlacementOwnerContinuationState.ContinuationUnavailable;
        }
        return target.Cancellation switch {
            { Disposition: AgentToolCancellationDisposition.ReceiptCommitted, CommittedEffect: not null, Receipt: not null }
                when target.EffectState == AgentToolEffectState.Committed => StoragePlacementOwnerContinuationState.ReceiptRecorded,
            { Disposition: AgentToolCancellationDisposition.CancelledUnreconciled, Reason: AgentToolCancellationReason.ReceiptNotObserved }
                when target.EffectState == AgentToolEffectState.Unknown => StoragePlacementOwnerContinuationState.NativeReceiptNotObserved,
            { Disposition: AgentToolCancellationDisposition.CancelledUnreconciled, Reason: AgentToolCancellationReason.CurrentAccessDenied }
                => StoragePlacementOwnerContinuationState.CurrentReadDenied,
            { Disposition: AgentToolCancellationDisposition.CancelledUnreconciled, Reason: AgentToolCancellationReason.NoReceiptProtocol }
                => StoragePlacementOwnerContinuationState.OwnerReceiptUnconfirmed,
            _ => StoragePlacementOwnerContinuationState.ContinuationUnavailable
        };
    }
}
