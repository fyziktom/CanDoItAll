using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public enum ProcessRuntimeStatus
{
    Created,
    Active,
    Waiting,
    Blocked,
    Completed,
    Failed,
    CancelRequested,
    Cancelled,
    Escalated,
    WaitingForUser
}

public enum ProcessRuntimeStepStatus
{
    Planned,
    Pending,
    Ready,
    Waiting,
    WaitingApproval,
    Claimed,
    Running,
    Blocked,
    Completed,
    Failed,
    Cancelled,
    Skipped
}

public enum DispatchClaimStatus
{
    Claimed,
    LeaseRenewed,
    Released,
    Expired,
    Reclaimed,
    Completed,
    Cancelled
}

public enum ProcessRuntimeTransitionOutcome
{
    Applied,
    Duplicate,
    Rejected
}

public sealed record ProcessRuntimeStateSnapshot(
    ProcessRunId RootRunId,
    ProcessRunId RunId,
    ProcessInstancePlanId PlanId,
    string PlanHash,
    ProcessRuntimeStatus Status,
    IReadOnlyList<ProcessRuntimeStepState> Steps,
    IReadOnlyList<DispatchClaimState> Claims,
    IReadOnlyList<StrategyResultReceipt> AppliedResults,
    IReadOnlySet<ArtifactSlotId> AvailableArtifactSlots,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProcessRuntimeStepState(
    ProcessStepInstanceId StepInstanceId,
    ProcessStepDefinitionId StepDefinitionId,
    ProcessRuntimeStepStatus Status,
    bool IsExecutable,
    int AttemptNumber,
    IReadOnlySet<ProcessStepInstanceId> DependencyStepIds,
    IReadOnlySet<ArtifactSlotId> RequiredArtifactSlots,
    DispatchClaimToken? ActiveClaimToken,
    StrategyResultIdempotencyKey? CompletedResultKey);

public sealed record DispatchClaimState(
    DispatchClaimToken ClaimToken,
    ProcessStepInstanceId StepInstanceId,
    DispatcherOwnerId OwnerId,
    DispatchClaimStatus Status,
    int AttemptNumber,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RenewedAtUtc,
    StrategyResultIdempotencyKey? ResultIdempotencyKey);

public sealed record StrategyResultReceipt(
    ProcessStepInstanceId StepInstanceId,
    StrategyId StrategyId,
    StrategyResultIdempotencyKey IdempotencyKey,
    StrategyOutcome Outcome,
    ProcessRuntimeStepStatus AppliedStepStatus,
    string ResultHash);

public sealed record DispatchWorkItem(
    ProcessRunId RunId,
    ProcessStepInstanceId StepInstanceId,
    ProcessStepDefinitionId StepDefinitionId,
    ProcessStrategyBindingSnapshot StrategyBinding,
    int AttemptNumber);

public sealed record ProcessRuntimeMutation(
    ProcessRuntimeTransitionOutcome Outcome,
    ProcessRuntimeStateSnapshot State,
    IReadOnlyList<ProcessRuntimeEventEnvelope> Events,
    IReadOnlyList<ProcessOutboxMessage> OutboxMessages,
    IReadOnlyList<ProcessArtifactLedgerEvent> ArtifactLedgerEvents,
    IReadOnlyList<ProcessValidationFailure> Diagnostics)
{
    public bool Succeeded => Outcome is ProcessRuntimeTransitionOutcome.Applied or ProcessRuntimeTransitionOutcome.Duplicate;

    public static ProcessRuntimeMutation Rejected(
        ProcessRuntimeStateSnapshot state,
        string code,
        string message)
    {
        return new ProcessRuntimeMutation(
            ProcessRuntimeTransitionOutcome.Rejected,
            state,
            [],
            [],
            [],
            [new ProcessValidationFailure(code, message)]);
    }
}

public sealed record ProcessRuntimeCommitRequest(
    RuntimeCommandId CommandId,
    ProcessRuntimeStateSnapshot OriginalState,
    ProcessRuntimeMutation Mutation);

public sealed record ProcessRuntimeCommitResult(
    ProcessRuntimeTransitionOutcome Outcome,
    ProcessRuntimeStateSnapshot State,
    IReadOnlyList<ProcessRuntimeEventEnvelope> Events,
    IReadOnlyList<ProcessOutboxMessage> OutboxMessages,
    IReadOnlyList<ProcessArtifactLedgerEvent> ArtifactLedgerEvents,
    IReadOnlyList<ProcessValidationFailure> Diagnostics)
{
    public bool Succeeded => Outcome is ProcessRuntimeTransitionOutcome.Applied or ProcessRuntimeTransitionOutcome.Duplicate;

    public static ProcessRuntimeCommitResult FromMutation(ProcessRuntimeMutation mutation)
    {
        return new ProcessRuntimeCommitResult(
            mutation.Outcome,
            mutation.State,
            mutation.Events,
            mutation.OutboxMessages,
            mutation.ArtifactLedgerEvents,
            mutation.Diagnostics);
    }
}

public sealed class ProcessRuntimeOptimisticConcurrencyException : Exception
{
    public ProcessRuntimeOptimisticConcurrencyException(
        ProcessRunId runId,
        DateTimeOffset originalUpdatedAtUtc)
        : base($"Runtime state '{runId}' changed before command commit '{originalUpdatedAtUtc:O}'.")
    {
        RunId = runId;
        OriginalUpdatedAtUtc = originalUpdatedAtUtc;
    }

    public ProcessRunId RunId { get; }

    public DateTimeOffset OriginalUpdatedAtUtc { get; }
}

public static class ProcessRuntimeTerminalStates
{
    public static bool IsRunTerminal(ProcessRuntimeStatus status)
    {
        return status is ProcessRuntimeStatus.Completed or ProcessRuntimeStatus.Failed or ProcessRuntimeStatus.Cancelled;
    }

    public static bool IsStepTerminal(ProcessRuntimeStepStatus status)
    {
        return status is ProcessRuntimeStepStatus.Completed or ProcessRuntimeStepStatus.Failed or ProcessRuntimeStepStatus.Cancelled or ProcessRuntimeStepStatus.Skipped;
    }
}
