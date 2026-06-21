namespace CanDoItAll.Processes.Runtime;

public enum ProcessManagerWorkItemKind
{
    Incident,
    RecoveryRequest,
    BranchDecision,
    SubprocessMessage,
    ApprovalResponse,
    UserResponse
}

public enum ProcessManagerWorkPriority
{
    Normal,
    High,
    Critical
}

public enum ProcessIncidentClassification
{
    RuntimeFault,
    StrategyFault,
    DomainDiagnostic,
    PolicyDenial,
    MissingArtifact,
    StaleArtifact,
    SubprocessIncident,
    ManagerFault
}

public enum ProcessIncidentSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

public enum ProcessIncidentStatus
{
    Raised,
    Classified,
    AwaitingPolicy,
    WaitingForUser,
    Recovering,
    Resolved,
    Escalated,
    Failed
}

public enum ProcessRecoveryActionKind
{
    ResupplyArtifact,
    RetryStep,
    WaiveRequirement,
    Escalate,
    WaitForUser
}

public enum ProcessRecoveryPolicyDecision
{
    Allowed,
    Denied,
    RequiresApproval,
    Escalate
}

public enum ProcessRecoveryPolicyDenial
{
    None,
    AccessDenied,
    ApprovalMissing,
    BudgetUnavailable,
    RepeatUnsafe,
    ArtifactUnavailable,
    DriverPolicyDenied
}

public enum ProcessRecoveryRequestStatus
{
    Requested,
    Approved,
    Scheduled,
    Running,
    Completed,
    Applied,
    Denied,
    Retriable,
    Escalated
}

public enum ProcessManagerDecisionKind
{
    IncidentRecorded,
    RecoveryApproved,
    RecoveryDenied,
    BranchOutcomeSelected,
    BranchOutcomeRejected,
    LoopBudgetEscalated,
    SubprocessMessageQueued
}

public enum ProcessManagerDecisionStatus
{
    Recorded,
    Duplicate,
    Denied,
    Escalated,
    Rejected
}

public enum ProcessLoopBudgetOutcome
{
    Consumed,
    Duplicate,
    Exhausted
}

public enum ProcessBranchDecisionStatus
{
    Recorded,
    Duplicate,
    Rejected,
    Escalated
}

public enum ProcessSubprocessMessageKind
{
    ParentToChildControl,
    ChildToParentControl,
    ArtifactProjectionRequest,
    ArtifactProjectionAccepted,
    ArtifactProjectionRejected,
    SubprocessIncidentRaised,
    SubprocessEscalationRaised,
    CancellationRequested,
    CompletionSummary,
    RecoveryCoordinationRequest
}

public enum ProcessSubprocessMessageDirection
{
    ParentToChild,
    ChildToParent
}
