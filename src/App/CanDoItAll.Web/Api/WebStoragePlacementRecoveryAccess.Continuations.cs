using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Web.Api;

internal sealed partial class WebStoragePlacementRecoveryAccess {
    public async Task<StoragePlacementContinuationScan> ListContinuationsAsync(StoragePlacementRecoveryAuthorization authorization,
        Guid? projectId, int take, int offset, CancellationToken cancellationToken) {
        await EnsureCurrentAsync(authorization, cancellationToken);
        var page = await owners.ListContinuationCandidatesAsync(projectId, take, offset, cancellationToken);
        var readable = page.Items.Where(item => item.SourceAssociationValid && !item.ImportedHistory && item.NativeReceiptPresent).ToArray();
        var agents = await agentCheckpoints.ReadAsync(readable.Where(item => item.Owner == StoragePlacementRecoveryOwner.ProcessAsset &&
            item.OriginalAdmission?.DatabaseProfileId == canonical.Profile.Profile.Id).Select(item => new AgentAssetCheckpointIdentity(item.SourceRunId,
                item.AgentIntentId!.Value)).ToArray(), cancellationToken);
        var workflows = await workflowCheckpoints.ReadAsync(readable.Where(item => item.Owner == StoragePlacementRecoveryOwner.WorkflowAsset &&
            (item.OriginalAdmission is null || item.OriginalAdmission.DatabaseProfileId == canonical.Profile.Profile.Id))
            .Select(WorkflowIdentity).ToArray(), cancellationToken);
        var pending = new List<StoragePlacementContinuationCandidate>();
        foreach (var item in page.Items) {
            var state = AssetRecoveryCheckpointState.Unavailable;
            if (item.SourceAssociationValid && !item.ImportedHistory) {
                if (!item.NativeReceiptPresent) {
                    pending.Add(new(item.StorageIntentId, StoragePlacementContinuationPhase.NativeCommit));
                    continue;
                }
                state = item.Owner switch {
                    StoragePlacementRecoveryOwner.ProcessAsset => agents.GetValueOrDefault(new(item.SourceRunId, item.AgentIntentId!.Value),
                        AssetRecoveryCheckpointState.Unavailable),
                    StoragePlacementRecoveryOwner.WorkflowAsset => workflows.GetValueOrDefault(WorkflowIdentity(item), AssetRecoveryCheckpointState.Unavailable),
                    _ => AssetRecoveryCheckpointState.Unavailable
                };
            }
            if (state != AssetRecoveryCheckpointState.Completed) {
                var phase = state == AssetRecoveryCheckpointState.Unavailable ? StoragePlacementContinuationPhase.OwnerEvidenceUnavailable
                    : item.Owner == StoragePlacementRecoveryOwner.ProcessAsset ? StoragePlacementContinuationPhase.CoreCheckpoint
                    : StoragePlacementContinuationPhase.WorkflowAcknowledgement;
                pending.Add(new(item.StorageIntentId, phase));
            }
        }
        await EnsureCurrentAsync(authorization, cancellationToken);
        return new(pending, page.NextOffset);
    }

    private static WorkflowAssetCheckpointIdentity WorkflowIdentity(ProjectStorageContinuationFact item)
        => new(item.SourceRunId, item.OccurrencePath!, item.Slot!.Value, item.StorageIntentId.Value,
            item.OriginalAdmission is { } original ? new WorkflowProjectLifetime(original.DatabaseProfileId, original.ProjectId, original.LifetimeId) : null);
}
