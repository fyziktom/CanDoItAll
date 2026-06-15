namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchWorkflowFinalizerIntent(
    ProcessWorkflowExecutionOutcome WorkflowOutcome);

internal sealed record ProcessDispatchRecoveredFinalizerIntent(
    ProcessRouteExecutionOutcome RecoveryOutcome,
    string Trigger,
    Func<CancellationToken, Task> RenewLeaseAsync);

internal sealed record ProcessDispatchDirectAgentFinalizerIntent(
    ProcessRouteExecutionOutcome ExecutionOutcome,
    string Trigger,
    Func<CancellationToken, Task> RenewLeaseAsync);

internal sealed record ProcessDispatchSubprocessFinalizerIntent(
    Guid SubprocessRunId,
    ProcessStepRunStatus TerminalStatus,
    string TransitionReason);

internal sealed record ProcessDispatchWorkflowFinalizerInput(
    ProcessRouteCandidate Candidate,
    ProcessDispatchWorkflowFinalizerIntent Intent,
    ProcessRouteDispatchClaim DispatchClaim)
{
    public ProcessDispatchWorkflowFinalizerInput(
        ProcessRouteCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessRouteDispatchClaim dispatchClaim)
        : this(candidate, new ProcessDispatchWorkflowFinalizerIntent(workflowOutcome), dispatchClaim)
    {
    }

    public ProcessWorkflowExecutionOutcome WorkflowOutcome => Intent.WorkflowOutcome;
}

internal sealed record ProcessDispatchRecoveredFinalizerInput(
    ProcessRouteCandidate Candidate,
    ProcessDispatchRecoveredFinalizerIntent Intent,
    ProcessRouteDispatchClaim DispatchClaim)
{
    public ProcessDispatchRecoveredFinalizerInput(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome recoveryOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessRouteDispatchClaim dispatchClaim)
        : this(
            candidate,
            new ProcessDispatchRecoveredFinalizerIntent(recoveryOutcome, trigger, renewLeaseAsync),
            dispatchClaim)
    {
    }

    public ProcessRouteExecutionOutcome RecoveryOutcome => Intent.RecoveryOutcome;

    public string Trigger => Intent.Trigger;

    public Func<CancellationToken, Task> RenewLeaseAsync => Intent.RenewLeaseAsync;
}

internal sealed record ProcessDispatchDirectAgentFinalizerInput(
    ProcessRouteCandidate Candidate,
    ProcessDispatchDirectAgentFinalizerIntent Intent,
    ProcessRouteDispatchClaim DispatchClaim)
{
    public ProcessDispatchDirectAgentFinalizerInput(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessRouteDispatchClaim dispatchClaim)
        : this(
            candidate,
            new ProcessDispatchDirectAgentFinalizerIntent(executionOutcome, trigger, renewLeaseAsync),
            dispatchClaim)
    {
    }

    public ProcessRouteExecutionOutcome ExecutionOutcome => Intent.ExecutionOutcome;

    public string Trigger => Intent.Trigger;

    public Func<CancellationToken, Task> RenewLeaseAsync => Intent.RenewLeaseAsync;
}

internal sealed record ProcessDispatchSubprocessFinalizerInput(
    ProcessRouteCandidate Candidate,
    ProcessDispatchSubprocessFinalizerIntent Intent,
    ProcessRouteDispatchClaim DispatchClaim)
{
    public ProcessDispatchSubprocessFinalizerInput(
        ProcessRouteCandidate candidate,
        Guid subprocessRunId,
        ProcessStepRunStatus terminalStatus,
        string transitionReason,
        ProcessRouteDispatchClaim dispatchClaim)
        : this(
            candidate,
            new ProcessDispatchSubprocessFinalizerIntent(subprocessRunId, terminalStatus, transitionReason),
            dispatchClaim)
    {
    }

    public Guid SubprocessRunId => Intent.SubprocessRunId;

    public ProcessStepRunStatus TerminalStatus => Intent.TerminalStatus;

    public string TransitionReason => Intent.TransitionReason;
}
