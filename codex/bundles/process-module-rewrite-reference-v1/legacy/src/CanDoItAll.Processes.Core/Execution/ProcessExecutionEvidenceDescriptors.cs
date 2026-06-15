using CanDoItAll.Processes.Contracts;

namespace CanDoItAll.Processes.Core.Execution;

public enum ProcessCoreExecutionRunObservationKind
{
    Active = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
    TerminalWithoutOutcome = 4
}

public sealed record ProcessExecutionRunEvidenceDescriptor(
    Guid ExecutionRunId,
    ProcessAutomationExecutionState State,
    ProcessAutomationRunOutcome? Outcome,
    bool IsTerminal,
    bool IsActive,
    bool HasPendingToolApprovals,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    ProcessCoreExecutionRunObservationKind ObservationKind);

public sealed record ProcessExecutionAttemptEvidenceDescriptor(
    Guid ExecutionRunId,
    int AttemptNumber,
    ProcessStepRunStatus CompletionStatus,
    string CompletionReason,
    IReadOnlyList<string> MissingRequiredTools,
    bool HasMissingRequiredTools,
    int MissingRequiredToolCount,
    bool HasUnresolvedCriticalToolFailures,
    int UnresolvedCriticalToolFailureCount,
    Guid? SelectedBranchOutcomeId);

public sealed record ProcessExecutionCarriedProofDescriptor(
    bool HasConcreteImplementationProof,
    bool HasRunnableApplicationProof,
    bool HasConcreteProductMutation);

public sealed record ProcessExecutionEvidenceDescriptor(
    ProcessExecutionRunEvidenceDescriptor Run,
    ProcessExecutionAttemptEvidenceDescriptor Attempt,
    ProcessExecutionCarriedProofDescriptor CarriedProof);

public static class ProcessExecutionEvidenceDescriptorRules
{
    public static ProcessExecutionRunEvidenceDescriptor DescribeRun(
        ProcessAutomationExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return new ProcessExecutionRunEvidenceDescriptor(
            run.Id,
            run.State,
            run.Outcome,
            IsTerminalState(run.State),
            !IsTerminalState(run.State),
            run.PendingApprovals.Count > 0,
            run.CreatedAtUtc,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            ResolveObservationKind(run.State, run.Outcome));
    }

    public static ProcessExecutionAttemptEvidenceDescriptor DescribeAttempt(
        Guid executionRunId,
        int attemptNumber,
        ProcessStepRunStatus completionStatus,
        string completionReason,
        IEnumerable<string> missingRequiredTools,
        int unresolvedCriticalToolFailureCount,
        Guid? selectedBranchOutcomeId)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(attemptNumber);
        ArgumentNullException.ThrowIfNull(missingRequiredTools);

        var missingToolNames = missingRequiredTools
            .Where(static toolName => !string.IsNullOrWhiteSpace(toolName))
            .Select(static toolName => toolName.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var criticalFailureCount = Math.Max(0, unresolvedCriticalToolFailureCount);

        return new ProcessExecutionAttemptEvidenceDescriptor(
            executionRunId,
            attemptNumber,
            completionStatus,
            completionReason.Trim(),
            missingToolNames,
            missingToolNames.Length > 0,
            missingToolNames.Length,
            criticalFailureCount > 0,
            criticalFailureCount,
            selectedBranchOutcomeId);
    }

    public static ProcessExecutionCarriedProofDescriptor DescribeCarriedProof(
        bool hasConcreteImplementationProof,
        bool hasRunnableApplicationProof,
        bool hasConcreteProductMutation)
    {
        return new ProcessExecutionCarriedProofDescriptor(
            hasConcreteImplementationProof,
            hasRunnableApplicationProof,
            hasConcreteProductMutation);
    }

    public static bool IsTerminalState(ProcessAutomationExecutionState state)
    {
        return state is ProcessAutomationExecutionState.Completed or ProcessAutomationExecutionState.Failed;
    }

    public static ProcessCoreExecutionRunObservationKind ResolveObservationKind(
        ProcessAutomationExecutionState state,
        ProcessAutomationRunOutcome? outcome)
    {
        return outcome switch
        {
            ProcessAutomationRunOutcome.Succeeded => ProcessCoreExecutionRunObservationKind.Succeeded,
            ProcessAutomationRunOutcome.Failed => ProcessCoreExecutionRunObservationKind.Failed,
            ProcessAutomationRunOutcome.Cancelled => ProcessCoreExecutionRunObservationKind.Cancelled,
            null when IsTerminalState(state) => ProcessCoreExecutionRunObservationKind.TerminalWithoutOutcome,
            _ => ProcessCoreExecutionRunObservationKind.Active
        };
    }
}
