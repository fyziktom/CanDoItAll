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
        var host = new DispatcherArtifactProjectionHost(this);
        var context = new ProcessArtifactProjectionContext(
            candidate,
            detail,
            responseText,
            workspaceRoot,
            workspaceScope,
            writeCoordinator,
            recordOnlyCoordinator,
            logger,
            completionStatus,
            dispatchClaim,
            cancellationToken,
            lineage);

        await ProcessArtifactProjectionOrchestrator.CreateDefault(host).ProjectAsync(context);
    }
}
