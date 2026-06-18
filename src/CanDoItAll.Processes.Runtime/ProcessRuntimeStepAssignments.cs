using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessRuntimeStepAssignment(
    ProcessRunId RunId,
    ProcessInstancePlanId PlanId,
    ProcessStepInstanceId StepInstanceId,
    string StepKey,
    string RoleKey,
    string RoleResourceKey,
    string RoleDisplayName,
    string ExecutorKind,
    string ExecutorId,
    string ExecutorDisplayName,
    string Prompt,
    string ReadinessHash,
    string AssignmentReason,
    IReadOnlyList<ArtifactSlotId> ProducedArtifactSlotIds,
    IReadOnlyList<ArtifactSlotId> RequiredArtifactSlotIds,
    IReadOnlyList<string> AllowedOperations,
    string OperationTargetScope,
    IReadOnlyDictionary<string, string> LaunchVariables,
    ProcessRuntimeBranchGate? BranchGate,
    DateTimeOffset CreatedAtUtc);

public sealed record ProcessRuntimeBranchGate(
    string SourceStepKey,
    string RequiredOutcomeKey);

public interface IProcessRuntimeStepAssignmentStore
{
    ValueTask SaveAsync(
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
        ProcessRunId runId,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
        IReadOnlyDictionary<string, string> requiredVariables,
        CancellationToken cancellationToken = default);

    ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        CancellationToken cancellationToken = default);
}
