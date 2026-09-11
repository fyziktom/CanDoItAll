using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Workbench;

internal sealed record WorkflowAssetContinuationSnapshot(StoragePlacementWorkflowContinuationIntent? Intent,
    StoragePlacementOwnerContinuationState State, StoragePlacementOwnerContinuationAction Action);

internal sealed record WorkflowAssetContinuationGuard(bool ReceiptOnly, Func<CancellationToken, Task> RequireCurrentReadAsync,
    Func<CancellationToken, Task> RequireCurrentMutationAsync);

public sealed partial class WorkbenchProjectStructureRuntimeGateway {
    internal async Task<WorkflowAssetContinuationSnapshot> ObserveWorkflowAssetContinuationAsync(StoragePlacementRecoveryItem item,
        CancellationToken cancellationToken) {
        var prepared = await projectWorkbenchService.FindPreparedWorkflowAssetAsync(item.Identity.IntentId, cancellationToken);
        if (prepared is null) {
            return new(null, StoragePlacementOwnerContinuationState.OriginalProposalUnavailable, StoragePlacementOwnerContinuationAction.None);
        }
        var plan = prepared.Plan;
        if (plan.ProjectLifetime is not { } target || plan.SourceAuthorityFingerprint is null) {
            return new(null, StoragePlacementOwnerContinuationState.WorkflowSourceMissing, StoragePlacementOwnerContinuationAction.None);
        }
        if (target.DatabaseProfileId != item.Context.DatabaseProfileId || target.ProjectId != item.Identity.OriginalProjectId) {
            throw new WorkflowStructureOutputConflictException();
        }
        var output = await workflowOutputs.FindAsync(plan.Identity, cancellationToken);
        if (output is null) {
            return new(null, StoragePlacementOwnerContinuationState.WorkflowOutputMissing, StoragePlacementOwnerContinuationAction.None);
        }
        if (output.Plan != plan || output.StoragePlacementIntentId != item.Identity.IntentId.Value) {
            throw new WorkflowStructureOutputConflictException();
        }
        var run = await workflowRuns.GetRunAsync(plan.Identity.Occurrence.RunId, cancellationToken);
        if (run is null) {
            return new(null, StoragePlacementOwnerContinuationState.WorkflowRunMissing, StoragePlacementOwnerContinuationAction.None);
        }
        if (run.Origin?.StructureAuthority is not { ProjectScope: not null } authority) {
            return new(null, StoragePlacementOwnerContinuationState.WorkflowSourceMissing, StoragePlacementOwnerContinuationAction.None);
        }
        if (run.VersionId != plan.WorkflowVersionId || WorkflowStructureAuthorityFingerprint.Create(authority) != plan.SourceAuthorityFingerprint) {
            throw new WorkflowStructureOutputConflictException();
        }
        await using var disclosure = await workflowAuthority.AcquireAsync(authority, WorkflowStructureAuthorityUse.Disclosure, target, cancellationToken);
        var intent = new StoragePlacementWorkflowContinuationIntent(plan.Identity.Occurrence.RunId.Value,
            plan.Identity.Occurrence.Path, plan.Identity.Slot, plan.Fingerprint);
        var native = await projectWorkbenchService.FindWorkflowContributionAsync(plan.Identity, cancellationToken);
        if (native is not null) {
            RetainedEvidenceImport.RequireNative(native.ImportedHistory);
            if (native.Receipt?.Fingerprint != plan.Fingerprint || native.Receipt.ProjectLifetime != target ||
                    native.Receipt.AssetId != item.Identity.IntentId.Value || output.Receipt is not null && output.Receipt != native.Receipt) {
                throw new WorkflowStructureOutputConflictException();
            }
            return output.Receipt is not null
                ? new(intent, StoragePlacementOwnerContinuationState.ReceiptRecorded, StoragePlacementOwnerContinuationAction.None)
                : new(intent, StoragePlacementOwnerContinuationState.Ready, StoragePlacementOwnerContinuationAction.RecordWorkflowAssetReceipt);
        }
        if (output.Receipt is not null) {
            throw new WorkflowStructureOutputConflictException();
        }
        if (run.State == WorkflowRunState.Cancelled) {
            return new(intent, StoragePlacementOwnerContinuationState.WorkflowRunCancelled, StoragePlacementOwnerContinuationAction.None);
        }
        try {
            if (await projectWorkbenchService.ReadWorkflowTargetBindingAsync(plan.ProjectId, plan.ParentNodeId.Value, cancellationToken) != plan.TargetBindingFingerprint) {
                return new(intent, StoragePlacementOwnerContinuationState.WorkflowTargetChanged, StoragePlacementOwnerContinuationAction.None);
            }
        } catch (ProjectStructureAgentException exception) when (exception.StatusCode == 404) {
            return new(intent, StoragePlacementOwnerContinuationState.WorkflowTargetChanged, StoragePlacementOwnerContinuationAction.None);
        }
        await disclosure.DisposeAsync();
        try {
            await using var mutation = await workflowAuthority.AcquireAsync(authority, WorkflowStructureAuthorityUse.AssetOutput, target, cancellationToken);
            return new(intent, StoragePlacementOwnerContinuationState.Ready, StoragePlacementOwnerContinuationAction.CompletePreparedWorkflowAsset);
        } catch (ProjectStructureAgentException) {
            return new(intent, StoragePlacementOwnerContinuationState.WorkflowSourceCannotContinue, StoragePlacementOwnerContinuationAction.None);
        }
    }

    internal async Task<ProjectStructureRuntimeNodeSummary> ContinueWorkflowAssetAsync(StoragePlacementWorkflowContinuationCommand request,
        WorkflowAssetContinuationGuard guard, CancellationToken cancellationToken) {
        var prepared = await projectWorkbenchService.FindPreparedWorkflowAssetAsync(request.IntentId, cancellationToken)
            ?? throw new WorkflowStructureOutputConflictException();
        var plan = prepared.Plan;
        if (request.ExpectedIntent != new StoragePlacementWorkflowContinuationIntent(plan.Identity.Occurrence.RunId.Value,
                plan.Identity.Occurrence.Path, plan.Identity.Slot, plan.Fingerprint)) {
            throw new WorkflowStructureOutputConflictException();
        }
        return await ReconcileWorkflowOutputCoreAsync(plan.Identity, cancellationToken, guard)
            ?? throw new WorkflowStructureOutputConflictException();
    }
}
