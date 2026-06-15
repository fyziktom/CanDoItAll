using CanDoItAll.AgentFramework.Models;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessArtifactProjectionContext(
    ProcessProjectionCandidateSnapshot Candidate,
    ProcessProjectionRunSnapshot Run,
    ProcessProjectionObservationSnapshot Observations,
    string ResponseText,
    string WorkspaceRoot,
    WorkspaceScopeDescriptor WorkspaceScope,
    ProcessArtifactProjectionWriteCoordinator WriteCoordinator,
    ProcessArtifactProjectionRecordOnlyCoordinator RecordOnlyCoordinator,
    ILogger<ProcessRunAutomationDispatchService> Logger,
    ProcessStepRunStatus CompletionStatus,
    ProcessProjectionStepDispatchClaim DispatchClaim,
    CancellationToken CancellationToken,
    ProcessProjectionLineageInput Lineage)
{
    public ProcessArtifactRecoveryProjectionContext RecoveryContext { get; } = Lineage.ToRecoveryContext();
}
