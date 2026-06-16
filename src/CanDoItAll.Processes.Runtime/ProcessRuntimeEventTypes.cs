using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Runtime;

public static class ProcessRuntimeEventTypes
{
    public static ProcessEventType ProcessRunCreated { get; } = new("ProcessRunCreated");

    public static ProcessEventType ProcessRunActivated { get; } = new("ProcessRunActivated");

    public static ProcessEventType ProcessRunCancelRequested { get; } = new("ProcessRunCancelRequested");

    public static ProcessEventType ProcessRunCancelled { get; } = new("ProcessRunCancelled");

    public static ProcessEventType ProcessRunCompleted { get; } = new("ProcessRunCompleted");

    public static ProcessEventType ProcessRunFailed { get; } = new("ProcessRunFailed");

    public static ProcessEventType StepReady { get; } = new("StepReady");

    public static ProcessEventType StepClaimed { get; } = new("StepClaimed");

    public static ProcessEventType StepRunning { get; } = new("StepRunning");

    public static ProcessEventType StepCompleted { get; } = new("StepCompleted");

    public static ProcessEventType StepFailed { get; } = new("StepFailed");

    public static ProcessEventType StepBlocked { get; } = new("StepBlocked");

    public static ProcessEventType StepCancelled { get; } = new("StepCancelled");

    public static ProcessEventType StepSkipped { get; } = new("StepSkipped");

    public static ProcessEventType DispatchClaimCreated { get; } = new("DispatchClaimCreated");

    public static ProcessEventType DispatchLeaseRenewed { get; } = new("DispatchLeaseRenewed");

    public static ProcessEventType DispatchClaimExpired { get; } = new("DispatchClaimExpired");

    public static ProcessEventType DispatchClaimReclaimed { get; } = new("DispatchClaimReclaimed");

    public static ProcessEventType DispatchClaimCompleted { get; } = new("DispatchClaimCompleted");

    public static ProcessEventType ManagerIncidentRaised { get; } = new("ManagerIncidentRaised");

    public static ProcessEventType ManagerRecoveryApproved { get; } = new("ManagerRecoveryApproved");

    public static ProcessEventType ManagerRecoveryDenied { get; } = new("ManagerRecoveryDenied");

    public static ProcessEventType ManagerBranchDecisionRecorded { get; } = new("ManagerBranchDecisionRecorded");

    public static ProcessEventType ManagerBranchDecisionRejected { get; } = new("ManagerBranchDecisionRejected");

    public static ProcessEventType ManagerLoopBudgetEscalated { get; } = new("ManagerLoopBudgetEscalated");

    public static ProcessEventType ManagerSubprocessMessageQueued { get; } = new("ManagerSubprocessMessageQueued");
}
