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

public sealed record ProcessRuntimeStepAssignmentBoundedSearchResult(
    IReadOnlyList<ProcessRuntimeStepAssignment> Assignments,
    bool LimitExceeded);

public interface IProcessRuntimeStepAssignmentStore
{
    public const int MaximumBatchRunCount = 2_049;
    public const int MaximumBoundedSearchRunCount = MaximumBatchRunCount + 1;

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

    async ValueTask<ProcessRuntimeStepAssignmentBoundedSearchResult>
        FindByLaunchVariablesBoundedAsync(
            IReadOnlyDictionary<string, string> requiredVariables,
            int maximumDistinctRunCount,
            CancellationToken cancellationToken = default)
    {
        if (maximumDistinctRunCount is < 1 or > MaximumBoundedSearchRunCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDistinctRunCount),
                maximumDistinctRunCount,
                $"Bounded assignment search must allow between 1 and {MaximumBoundedSearchRunCount} distinct runs.");
        }

        var assignments = await FindByLaunchVariablesAsync(
                requiredVariables,
                cancellationToken)
            .ConfigureAwait(false);
        var limitExceeded = assignments
            .Select(assignment => assignment.RunId)
            .Distinct()
            .Take(maximumDistinctRunCount + 1)
            .Count() > maximumDistinctRunCount;
        return new ProcessRuntimeStepAssignmentBoundedSearchResult(
            limitExceeded ? [] : assignments,
            limitExceeded);
    }

    ValueTask<ProcessRuntimeStepAssignment?> LoadAsync(
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        CancellationToken cancellationToken = default);
}
