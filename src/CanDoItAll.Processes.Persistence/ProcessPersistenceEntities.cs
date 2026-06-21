using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

public sealed class ProcessInstancePlanEntity
{
    public Guid PlanId { get; set; }

    public Guid RootPlanId { get; set; }

    public Guid? ParentPlanId { get; set; }

    public Guid? ParentStepId { get; set; }

    public Guid DefinitionId { get; set; }

    public Guid DefinitionVersionId { get; set; }

    public string PlanHash { get; set; } = string.Empty;

    public string PlanSchemaVersion { get; set; } = string.Empty;

    public string DefinitionContentHash { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ProcessRuntimeStateEntity
{
    public Guid RunId { get; set; }

    public Guid RootRunId { get; set; }

    public Guid PlanId { get; set; }

    public string PlanHash { get; set; } = string.Empty;

    public ProcessRuntimeStatus Status { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }

    public List<ProcessRuntimeStepEntity> Steps { get; } = [];

    public List<ProcessDispatchClaimEntity> Claims { get; } = [];

    public List<ProcessStrategyResultReceiptEntity> ResultReceipts { get; } = [];

    public List<ProcessRuntimeAvailableArtifactSlotEntity> AvailableArtifactSlots { get; } = [];
}

public sealed class ProcessRuntimeStepAssignmentEntity
{
    public Guid RunId { get; set; }

    public Guid StepInstanceId { get; set; }

    public Guid PlanId { get; set; }

    public string StepKey { get; set; } = string.Empty;

    public string RoleKey { get; set; } = string.Empty;

    public string RoleResourceKey { get; set; } = string.Empty;

    public string RoleDisplayName { get; set; } = string.Empty;

    public string ExecutorKind { get; set; } = string.Empty;

    public string ExecutorId { get; set; } = string.Empty;

    public string ExecutorDisplayName { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string ReadinessHash { get; set; } = string.Empty;

    public string AssignmentReason { get; set; } = string.Empty;

    public string ProducedArtifactSlotIds { get; set; } = string.Empty;

    public string RequiredArtifactSlotIds { get; set; } = string.Empty;

    public string AllowedOperations { get; set; } = string.Empty;

    public string OperationTargetScope { get; set; } = string.Empty;

    public string LaunchVariablesJson { get; set; } = string.Empty;

    public string? BranchGateSourceStepKey { get; set; }

    public string? BranchGateRequiredOutcomeKey { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}

public sealed class ProcessRuntimeStepEntity
{
    public Guid RunId { get; set; }

    public Guid StepInstanceId { get; set; }

    public Guid StepDefinitionId { get; set; }

    public ProcessRuntimeStepStatus Status { get; set; }

    public bool IsExecutable { get; set; }

    public int AttemptNumber { get; set; }

    public string DependencyStepIds { get; set; } = string.Empty;

    public string RequiredArtifactSlotIds { get; set; } = string.Empty;

    public Guid? ActiveClaimToken { get; set; }

    public Guid? CompletedResultKey { get; set; }

    public ProcessRuntimeStateEntity? RuntimeState { get; set; }
}

public sealed class ProcessDispatchClaimEntity
{
    public Guid RunId { get; set; }

    public Guid ClaimToken { get; set; }

    public Guid StepInstanceId { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public DispatchClaimStatus Status { get; set; }

    public int AttemptNumber { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RenewedAtUtc { get; set; }

    public Guid? ResultIdempotencyKey { get; set; }

    public ProcessRuntimeStateEntity? RuntimeState { get; set; }
}

public sealed class ProcessStrategyResultReceiptEntity
{
    public Guid RunId { get; set; }

    public Guid StepInstanceId { get; set; }

    public string StrategyId { get; set; } = string.Empty;

    public Guid IdempotencyKey { get; set; }

    public string Outcome { get; set; } = string.Empty;

    public ProcessRuntimeStepStatus AppliedStepStatus { get; set; }

    public string ResultHash { get; set; } = string.Empty;

    public ProcessRuntimeStateEntity? RuntimeState { get; set; }
}

public sealed class ProcessRuntimeAvailableArtifactSlotEntity
{
    public Guid RunId { get; set; }

    public Guid SlotId { get; set; }

    public ProcessRuntimeStateEntity? RuntimeState { get; set; }
}

public sealed class ProcessRuntimeEventEntity
{
    public long GlobalSequence { get; set; }

    public long RootSequence { get; set; }

    public Guid EventId { get; set; }

    public Guid RootRunId { get; set; }

    public Guid RunId { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public Guid? CausationId { get; set; }

    public string ActorKind { get; set; } = string.Empty;

    public string ActorId { get; set; } = string.Empty;

    public string SchemaVersion { get; set; } = string.Empty;

    public string Sensitivity { get; set; } = string.Empty;

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;
}

public sealed class ProcessOutboxMessageEntity
{
    public Guid MessageId { get; set; }

    public Guid EventId { get; set; }

    public ProcessOutboxSubscriberKind SubscriberKind { get; set; }

    public string PayloadHash { get; set; } = string.Empty;

    public ProcessOutboxDeliveryStatus Status { get; set; }

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? AvailableAtUtc { get; set; }

    public DateTimeOffset? LockedAtUtc { get; set; }

    public string? LockId { get; set; }

    public DateTimeOffset? DeliveredAtUtc { get; set; }

    public string? LastErrorClass { get; set; }
}

public sealed class ProcessArtifactLedgerEventEntity
{
    public Guid LedgerEventId { get; set; }

    public Guid EventId { get; set; }

    public Guid SlotId { get; set; }

    public Guid ArtifactId { get; set; }

    public string ContentHash { get; set; } = string.Empty;
}

public sealed class ProcessRuntimeIdempotencyEntity
{
    public Guid RunId { get; set; }

    public Guid CommandId { get; set; }

    public ProcessRuntimeTransitionOutcome Outcome { get; set; }

    public DateTimeOffset CompletedAtUtc { get; set; }
}

public sealed class ProcessProjectionSnapshotEntity
{
    public string ProjectorName { get; set; } = string.Empty;

    public string ProjectionKey { get; set; } = string.Empty;

    public string SchemaVersion { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ProcessProjectionHistoryEntity
{
    public string ProjectorName { get; set; } = string.Empty;

    public string ProjectionKey { get; set; } = string.Empty;

    public long GlobalSequence { get; set; }

    public Guid RootRunId { get; set; }

    public Guid RunId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string SchemaVersion { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public string Sensitivity { get; set; } = string.Empty;
}

public sealed class ProcessProjectorOffsetEntity
{
    public string ProjectorName { get; set; } = string.Empty;

    public string ShardKey { get; set; } = string.Empty;

    public long GlobalSequence { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}

public sealed class ProcessProjectionDeadLetterEntity
{
    public Guid DeadLetterId { get; set; }

    public string ProjectorName { get; set; } = string.Empty;

    public string ShardKey { get; set; } = string.Empty;

    public Guid EventId { get; set; }

    public long GlobalSequence { get; set; }

    public string ErrorClass { get; set; } = string.Empty;

    public string DiagnosticReference { get; set; } = string.Empty;

    public string RetryPolicy { get; set; } = string.Empty;

    public DateTimeOffset DeadLetteredAtUtc { get; set; }
}

public enum ProcessOutboxDeliveryStatus
{
    Pending,
    Locked,
    Delivered,
    Failed
}
