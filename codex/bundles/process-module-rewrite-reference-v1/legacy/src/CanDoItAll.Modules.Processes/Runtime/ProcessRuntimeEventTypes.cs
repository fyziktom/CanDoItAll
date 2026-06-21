namespace CanDoItAll.Modules.Processes;

internal static class ProcessRuntimeEventTypes
{
    public const string ManualAgentStepRerun = "agent-step-rerun";
    public const string AgentReworkPacketCreated = "agent-rework-packet-created";
    public const string AgentRecoveryAttemptRecorded = "agent-recovery-attempt-recorded";
    public const string RecoveryRoutingDecisionRecorded = "recovery-routing-decision-recorded";
    public const string BlockedRunStopped = "blocked-run-stopped";
    public const string SubprocessRunCreated = "subprocess-run-created";
    public const string SubprocessRunObserved = "subprocess-run-observed";
    public const string WorkflowRunStarted = "workflow-run-started";
    public const string WorkflowRunObserved = "workflow-run-observed";
    public const string ArtifactValidationDiagnostic = "artifact-validation-diagnostic";
    public const string RuntimeInvariantViolationRecorded = "runtime-invariant-violation-recorded";
    public const string MissingUpstreamArtifactMaterializationRequested = "missing-upstream-artifact-materialization-requested";
    public const string MissingUpstreamArtifactMaterializationResolved = "missing-upstream-artifact-materialization-resolved";
    public const string NoProgressRetryObserved = "no-progress-retry-observed";
    public const string NoProgressRetryCompressed = "no-progress-retry-compressed";
    public const string ManagerDirectiveRecorded = "manager-directive-recorded";
    public const string ProcessEscalationCreated = "process-escalation-created";
    public const string ProcessEscalationAssigned = "process-escalation-assigned";
    public const string ProcessEscalationResolved = "process-escalation-resolved";
    public const string ProcessEscalationReopened = "process-escalation-reopened";
    public const string ProcessEscalationReworkRequested = "process-escalation-rework-requested";
    public const string ProcessOperatorApprovalDecided = "process-operator-approval-decided";
}
