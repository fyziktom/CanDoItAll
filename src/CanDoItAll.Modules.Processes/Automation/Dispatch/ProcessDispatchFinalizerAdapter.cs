namespace CanDoItAll.Modules.Processes;

using DispatchCandidate = ProcessRunAutomationDispatchService.DispatchCandidate;
using DispatchExecutionOutcome = ProcessRunAutomationDispatchService.DispatchExecutionOutcome;
using FinalizerContext = ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext;
using FinalizerResult = ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerResult;
using ProcessStepDispatchClaim = ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;

internal sealed class ProcessDispatchFinalizerAdapter(
    Func<FinalizerContext, ProcessStepDispatchClaim, CancellationToken, Task<FinalizerResult?>> finalizeStepCompletionAsync,
    Func<DispatchCandidate, FinalizerResult, ProcessStepDispatchClaim, CancellationToken, Task> applyFinalizedStepTransitionAsync)
{
    public async Task FinalizeWorkflowCompletionAsync(
        ProcessDispatchWorkflowFinalizerInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        await FinalizeWorkflowCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate),
            input.WorkflowOutcome,
            ToDispatcherClaim(input.DispatchClaim),
            cancellationToken);
    }

    public async Task FinalizeWorkflowCompletionAsync(
        DispatchCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await FinalizeAndApplyAsync(
            candidate,
            ProcessDispatchFinalizerContextFactory.ForWorkflow(candidate, workflowOutcome),
            dispatchClaim,
            cancellationToken);
    }

    public async Task FinalizeRecoveredCompletionAsync(
        ProcessDispatchRecoveredFinalizerInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        await FinalizeRecoveredCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate),
            ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(input.RecoveryOutcome),
            input.Trigger,
            input.RenewLeaseAsync,
            ToDispatcherClaim(input.DispatchClaim),
            cancellationToken);
    }

    public async Task FinalizeRecoveredCompletionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome recoveryOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await FinalizeAndApplyAsync(
            candidate,
            ProcessDispatchFinalizerContextFactory.ForManagerArtifactRecovery(
                candidate,
                recoveryOutcome,
                trigger,
                renewLeaseAsync),
            dispatchClaim,
            cancellationToken);
    }

    public async Task FinalizeDirectAgentCompletionAsync(
        ProcessDispatchDirectAgentFinalizerInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        await FinalizeDirectAgentCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate),
            ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(input.ExecutionOutcome),
            input.Trigger,
            input.RenewLeaseAsync,
            ToDispatcherClaim(input.DispatchClaim),
            cancellationToken);
    }

    public async Task FinalizeDirectAgentCompletionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await FinalizeAndApplyAsync(
            candidate,
            ProcessDispatchFinalizerContextFactory.ForDirectAgent(
                candidate,
                executionOutcome,
                trigger,
                renewLeaseAsync),
            dispatchClaim,
            cancellationToken);
    }

    public async Task FinalizeSubprocessCompletionAsync(
        ProcessDispatchSubprocessFinalizerInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        await FinalizeSubprocessCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate),
            input.SubprocessRunId,
            input.TerminalStatus,
            input.TransitionReason,
            ToDispatcherClaim(input.DispatchClaim),
            cancellationToken);
    }

    public async Task FinalizeSubprocessCompletionAsync(
        DispatchCandidate candidate,
        Guid subprocessRunId,
        ProcessStepRunStatus terminalStatus,
        string transitionReason,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await FinalizeAndApplyAsync(
            candidate,
            ProcessDispatchFinalizerContextFactory.ForSubprocess(
                candidate,
                subprocessRunId,
                terminalStatus,
                transitionReason),
            dispatchClaim,
            cancellationToken);
    }

    private async Task FinalizeAndApplyAsync(
        DispatchCandidate candidate,
        FinalizerContext context,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var finalizedCompletion = await finalizeStepCompletionAsync(context, dispatchClaim, cancellationToken);
        if (finalizedCompletion is null)
        {
            return;
        }

        await applyFinalizedStepTransitionAsync(candidate, finalizedCompletion, dispatchClaim, cancellationToken);
    }

    private static ProcessStepDispatchClaim ToDispatcherClaim(ProcessRouteDispatchClaim dispatchClaim)
    {
        return new ProcessStepDispatchClaim(dispatchClaim.StepRunId, dispatchClaim.ClaimToken);
    }
}
