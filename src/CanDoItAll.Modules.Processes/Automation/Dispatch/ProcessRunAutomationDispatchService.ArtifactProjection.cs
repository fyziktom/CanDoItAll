using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private async Task ProjectExecutionArtifactsAsync(
        DispatchCandidate candidate,
        ProcessAutomationExecutionRunDetail detail,
        string responseText,
        ProcessStepRunStatus completionStatus,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken,
        ArtifactProjectionLineage? lineage = null)
    {
        await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));
        var writeCoordinator = new ProcessArtifactProjectionWriteCoordinator(
            storagePlacementService,
            RecordArtifactAsync);
        var recordOnlyCoordinator = new ProcessArtifactProjectionRecordOnlyCoordinator(RecordArtifactAsync);
        var facets = ProcessArtifactProjectionFacetFactory.Create((claim, token) =>
            EnsureStepDispatchClaimHeldAsync(ProcessProjectionSnapshotBuilderAdapter.ToDispatchClaim(claim), token));
        var context = new ProcessArtifactProjectionContext(
            ProcessProjectionSnapshotBuilderAdapter.FromDispatchCandidate(candidate),
            ProcessProjectionSnapshotBuilderAdapter.FromExecutionDetail(detail),
            responseText,
            workspaceRoot,
            workspaceScope,
            writeCoordinator,
            recordOnlyCoordinator,
            logger,
            completionStatus,
            ProcessProjectionSnapshotBuilderAdapter.FromDispatchClaim(dispatchClaim),
            cancellationToken,
            ProcessProjectionSnapshotBuilderAdapter.FromDispatchLineage(lineage));

        await ProcessArtifactProjectionOrchestrator.CreateDefault(facets).ProjectAsync(context);
    }
}
