using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

using ArtifactProjectionLineage = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ArtifactProjectionLineage;
using DispatchCandidate = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchCandidate;
using ProcessStepDispatchClaim = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactProjectionContext(
    DispatchCandidate Candidate,
    ProcessAutomationExecutionRunDetail Detail,
    string ResponseText,
    string WorkspaceRoot,
    WorkspaceScopeDescriptor WorkspaceScope,
    ProcessArtifactProjectionWriteCoordinator WriteCoordinator,
    ProcessArtifactProjectionRecordOnlyCoordinator RecordOnlyCoordinator,
    ILogger<ProcessRunAutomationDispatchService> Logger,
    ProcessStepRunStatus CompletionStatus,
    ProcessStepDispatchClaim DispatchClaim,
    CancellationToken CancellationToken,
    ArtifactProjectionLineage? Lineage)
{
    public ProcessArtifactRecoveryProjectionContext RecoveryContext { get; } = Lineage is null
        ? ProcessArtifactRecoveryProjectionContext.None
        : new ProcessArtifactRecoveryProjectionContext(
            Lineage.RecoveryExecutionRunId,
            Lineage.RecoveredForExecutionRunId,
            Lineage.ReworkPacketId);
}
