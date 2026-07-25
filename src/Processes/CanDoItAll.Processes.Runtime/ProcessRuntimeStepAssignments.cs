using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;

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
    DateTimeOffset CreatedAtUtc)
{
    public ProcessWorkflowExecutorBinding? WorkflowBinding { get; init; }

    public ProcessCapabilityScope CapabilityScope { get; init; } = ProcessCapabilityScope.Empty;
}

public sealed record ProcessRuntimeBranchGate(
    string SourceStepKey,
    string RequiredOutcomeKey);

public interface IProcessRuntimeStepAssignmentStore
{
    public const int MaximumBatchRunCount = 2_049;

    ValueTask SaveAsync(
        IReadOnlyList<ProcessRuntimeStepAssignment> assignments,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunAsync(
        ProcessRunId runId,
        CancellationToken cancellationToken = default);

    async ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> LoadByRunsAsync(
        IReadOnlyList<ProcessRunId> runIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runIds);
        if (runIds.Count > MaximumBatchRunCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runIds),
                runIds.Count,
                $"Step-assignment batch cannot exceed {MaximumBatchRunCount} runs.");
        }

        var result = new List<ProcessRuntimeStepAssignment>();
        foreach (var runId in runIds.Distinct().OrderBy(runId => runId.Value))
        {
            result.AddRange(
                await LoadByRunAsync(runId, cancellationToken).ConfigureAwait(false));
        }

        return result;
    }

    ValueTask<IReadOnlyList<ProcessRuntimeStepAssignment>> FindByLaunchVariablesAsync(
        IReadOnlyDictionary<string, string> requiredVariables,
        CancellationToken cancellationToken = default);

    ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        CancellationToken cancellationToken = default);
}
