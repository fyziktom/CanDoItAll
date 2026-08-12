using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Contracts;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessRuntimeOperatorActionCommand(
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    ProcessRuntimeOperatorActionKind Kind,
    string RequestedBy,
    string Reason)
{
    public ProcessRuntimeBlockedRecoveryAuthorization? BlockedRecoveryAuthorization { get; init; }
}

public sealed record ProcessRuntimeOperatorActionResult
{
    public ProcessRuntimeOperatorActionResult(
        ProcessRunId RunId,
        ProcessStepInstanceId StepInstanceId,
        ProcessRuntimeOperatorActionKind Kind,
        ProcessRuntimeTransitionOutcome Outcome,
        ProcessRuntimeStatus Status,
        IReadOnlyList<string> Diagnostics)
    {
        this.RunId = RunId;
        this.StepInstanceId = StepInstanceId;
        this.Kind = Kind;
        this.Outcome = Outcome;
        this.Status = Status;
        this.Diagnostics = ProcessPublicReceiptTextPolicy.NormalizePublicMessages(Diagnostics);
    }

    public ProcessRunId RunId { get; }

    public ProcessStepInstanceId StepInstanceId { get; }

    public ProcessRuntimeOperatorActionKind Kind { get; }

    public ProcessRuntimeTransitionOutcome Outcome { get; }

    public ProcessRuntimeStatus Status { get; }

    public IReadOnlyList<string> Diagnostics { get; }

    public bool Succeeded => Outcome is ProcessRuntimeTransitionOutcome.Applied or ProcessRuntimeTransitionOutcome.Duplicate;
}

public sealed record ProcessRuntimeRunCancellationCommand(
    ProcessRunId RunId,
    string RequestedBy,
    string Reason);

public sealed record ProcessRuntimeRunCancellationResult
{
    public ProcessRuntimeRunCancellationResult(
        ProcessRunId RunId,
        ProcessRuntimeOperatorActionKind Kind,
        ProcessRuntimeTransitionOutcome Outcome,
        ProcessRuntimeStatus Status,
        IReadOnlyList<string> Diagnostics)
    {
        this.RunId = RunId;
        this.Kind = Kind;
        this.Outcome = Outcome;
        this.Status = Status;
        this.Diagnostics = ProcessPublicReceiptTextPolicy.NormalizePublicMessages(Diagnostics);
    }

    public ProcessRunId RunId { get; }

    public ProcessRuntimeOperatorActionKind Kind { get; }

    public ProcessRuntimeTransitionOutcome Outcome { get; }

    public ProcessRuntimeStatus Status { get; }

    public IReadOnlyList<string> Diagnostics { get; }

    public bool Succeeded => Outcome is ProcessRuntimeTransitionOutcome.Applied or ProcessRuntimeTransitionOutcome.Duplicate;
}

public sealed record ProcessRuntimeRunCancellationObservation(
    ProcessRunId RequestedRunId,
    IReadOnlyList<ProcessRunId> CancelledRunIds,
    string RequestedBy,
    string Reason,
    DateTimeOffset CancelledAtUtc);

public sealed record ProcessRuntimeRunCancellationObservationResult
{
    public ProcessRuntimeRunCancellationObservationResult(IReadOnlyList<string> Diagnostics)
    {
        this.Diagnostics = ProcessPublicReceiptTextPolicy.NormalizePublicMessages(Diagnostics);
    }

    public IReadOnlyList<string> Diagnostics { get; }

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
