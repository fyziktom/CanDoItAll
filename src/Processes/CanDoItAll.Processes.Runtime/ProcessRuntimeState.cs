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
    DateTimeOffset UpdatedAtUtc)
{
    public IReadOnlyList<ProcessRuntimeInputArtifactReceipt> ConnectedInputArtifacts { get; init; } = [];
}

public sealed record ProcessRuntimeStepState(
    ProcessStepInstanceId StepInstanceId,
    ProcessStepDefinitionId StepDefinitionId,
    ProcessRuntimeStepStatus Status,
    bool IsExecutable,
    int AttemptNumber,
    IReadOnlySet<ProcessStepInstanceId> DependencyStepIds,
    IReadOnlySet<ArtifactSlotId> RequiredArtifactSlots,
    DispatchClaimToken? ActiveClaimToken,
    StrategyResultIdempotencyKey? CompletedResultKey)
{
    public IReadOnlySet<ArtifactSlotId> ProducedArtifactSlots { get; init; } = new HashSet<ArtifactSlotId>();

    public IReadOnlyList<string> RequiredRuntimeToolNames { get; init; } = [];
}

public sealed record ProcessRuntimeInputArtifactReceipt(
    ProcessStepInstanceId ConsumerStepInstanceId,
    ArtifactSlotId RequiredSlotId,
    ProcessArtifactInputAvailability Availability,
    ProcessStepInstanceId? ProducerStepInstanceId,
    ArtifactInstanceId? ArtifactId,
    string ContentHash,
    string ConnectionHash);

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

public sealed record StrategyResultReceipt
{
    public StrategyResultReceipt(
        ProcessStepInstanceId stepInstanceId,
        StrategyId strategyId,
        StrategyResultIdempotencyKey idempotencyKey,
        StrategyOutcome outcome,
        ProcessRuntimeStepStatus appliedStepStatus,
        string resultHash,
        IReadOnlyList<StrategyResultDiagnosticReceipt>? diagnostics = null,
        IReadOnlyList<StrategyResultArtifactReceipt>? producedArtifacts = null,
        ProcessRecoveryDecisionReceipt? recoveryDecision = null)
    {
        StepInstanceId = stepInstanceId;
        StrategyId = strategyId;
        IdempotencyKey = idempotencyKey;
        Outcome = outcome;
        AppliedStepStatus = appliedStepStatus;
        ResultHash = resultHash;
        Diagnostics = diagnostics ?? [];
        ProducedArtifacts = producedArtifacts ?? [];
        RecoveryDecision = recoveryDecision;
    }

    public ProcessStepInstanceId StepInstanceId { get; init; }

    public StrategyId StrategyId { get; init; }

    public StrategyResultIdempotencyKey IdempotencyKey { get; init; }

    public StrategyOutcome Outcome { get; init; }

    public ProcessRuntimeStepStatus AppliedStepStatus { get; init; }

    public string ResultHash { get; init; }

    public IReadOnlyList<StrategyResultDiagnosticReceipt> Diagnostics { get; init; }

    public IReadOnlyList<StrategyResultArtifactReceipt> ProducedArtifacts { get; init; }

    public ProcessRecoveryDecisionReceipt? RecoveryDecision { get; init; }
}

public sealed record StrategyResultDiagnosticReceipt(
    string Code,
    StrategyDiagnosticSensitivity Sensitivity,
    string EvidenceHash,
    string SafeSummary,
    string? RestrictedEvidenceReference,
    ProcessDiagnosticRetrySafety RetrySafety,
    ProcessDiagnosticIdempotencyClassification Idempotency);

public sealed record StrategyResultArtifactReceipt(
    ArtifactSlotId SlotId,
    ArtifactInstanceId ArtifactId,
    string ContentHash);

public sealed record ProcessRecoveryDecisionReceipt(
    ProcessFailureCategory FailureCategory,
    ProcessRecoveryDecisionKind DecisionKind,
    string SourceDiagnosticCode,
    string Policy,
    string SafeReason)
{
    public ProcessRecoveryRouteKind RouteKind { get; init; } = ProcessRecoveryRouteKind.ManagerAction;

    public ProcessStepInstanceId? ResponsibleStepInstanceId { get; init; }
}

public enum ProcessFailureCategory
{
    Unknown,
    MissingDiagnostics,
    MissingArtifact,
    MissingCapability,
    DeniedCapability,
    PolicyViolation,
    Timeout,
    ProviderFailure,
    ChildRunBlocked,
    InstructionNonCompliance,
    AdapterRetryable
}

public enum ProcessRecoveryDecisionKind
{
    None,
    SafeRetry,
    ManagerRequired,
    TerminalBlocked
}

public enum ProcessRecoveryRouteKind
{
    None,
    CurrentStepRetry,
    UpstreamStepRework,
    ManagerAction,
    TerminalBlock,
    ChildRunPropagation,
    TemplateRepair
}

public sealed record DispatchWorkItem
{
    public DispatchWorkItem(
        ProcessRunId runId,
        ProcessStepInstanceId stepInstanceId,
        ProcessStepDefinitionId stepDefinitionId,
        ProcessStrategyBindingSnapshot strategyBinding,
        int attemptNumber,
        ProcessStepExecutionContract? stepContract = null)
    {
        RunId = runId;
        StepInstanceId = stepInstanceId;
        StepDefinitionId = stepDefinitionId;
        StrategyBinding = strategyBinding;
        AttemptNumber = attemptNumber;
        StepContract = stepContract ?? ProcessStepExecutionContract.Empty;
    }

    public ProcessRunId RunId { get; init; }

    public ProcessStepInstanceId StepInstanceId { get; init; }

    public ProcessStepDefinitionId StepDefinitionId { get; init; }

    public ProcessStrategyBindingSnapshot StrategyBinding { get; init; }

    public int AttemptNumber { get; init; }

    public ProcessStepExecutionContract StepContract { get; init; }
}

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
