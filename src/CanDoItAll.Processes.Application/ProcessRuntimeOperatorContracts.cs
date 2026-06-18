using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessRuntimeOperatorActionCommand(
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    ProcessRuntimeOperatorActionKind Kind,
    string RequestedBy,
    string Reason);

public sealed record ProcessRuntimeOperatorActionResult(
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    ProcessRuntimeOperatorActionKind Kind,
    ProcessRuntimeTransitionOutcome Outcome,
    ProcessRuntimeStatus Status,
    IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Outcome is ProcessRuntimeTransitionOutcome.Applied or ProcessRuntimeTransitionOutcome.Duplicate;
}

public sealed record ProcessRuntimeStepAssignmentRepairResult(
    ProcessRuntimeStepAssignment Assignment,
    bool Repaired,
    string Summary);

public interface IProcessRuntimeStepAssignmentRepairService
{
    ValueTask<ProcessRuntimeStepAssignmentRepairResult> RepairAsync(
        ProcessRuntimeStepAssignment assignment,
        string operatorReason,
        CancellationToken cancellationToken = default);
}
