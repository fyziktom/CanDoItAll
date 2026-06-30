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

public sealed record ProcessRuntimeRunCancellationCommand(
    ProcessRunId RunId,
    string RequestedBy,
    string Reason);

public sealed record ProcessRuntimeRunCancellationResult(
    ProcessRunId RunId,
    ProcessRuntimeOperatorActionKind Kind,
    ProcessRuntimeTransitionOutcome Outcome,
    ProcessRuntimeStatus Status,
    IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Outcome is ProcessRuntimeTransitionOutcome.Applied or ProcessRuntimeTransitionOutcome.Duplicate;
}

public sealed record ProcessRuntimeRunCancellationObservation(
    ProcessRunId RequestedRunId,
    IReadOnlyList<ProcessRunId> CancelledRunIds,
    string RequestedBy,
    string Reason,
    DateTimeOffset CancelledAtUtc);

public sealed record ProcessRuntimeRunCancellationObservationResult(
    IReadOnlyList<string> Diagnostics)
{
    public static ProcessRuntimeRunCancellationObservationResult Empty { get; } = new([]);
}

public interface IProcessRuntimeRunCancellationObserver
{
    ValueTask<ProcessRuntimeRunCancellationObservationResult> OnRunsCancelledAsync(
        ProcessRuntimeRunCancellationObservation observation,
        CancellationToken cancellationToken = default);
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
