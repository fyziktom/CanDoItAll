namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchWorkflowFinalizerInput(
    ProcessRouteCandidate Candidate,
    ProcessWorkflowExecutionOutcome WorkflowOutcome,
    ProcessRouteDispatchClaim DispatchClaim);

internal sealed record ProcessDispatchRecoveredFinalizerInput(
    ProcessRouteCandidate Candidate,
    ProcessRouteExecutionOutcome RecoveryOutcome,
    string Trigger,
    Func<CancellationToken, Task> RenewLeaseAsync,
    ProcessRouteDispatchClaim DispatchClaim);

internal sealed record ProcessDispatchDirectAgentFinalizerInput(
    ProcessRouteCandidate Candidate,
    ProcessRouteExecutionOutcome ExecutionOutcome,
    string Trigger,
    Func<CancellationToken, Task> RenewLeaseAsync,
    ProcessRouteDispatchClaim DispatchClaim);

internal sealed record ProcessDispatchSubprocessFinalizerInput(
    ProcessRouteCandidate Candidate,
    Guid SubprocessRunId,
    ProcessStepRunStatus TerminalStatus,
    string TransitionReason,
    ProcessRouteDispatchClaim DispatchClaim);
