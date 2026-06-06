namespace CanDoItAll.Modules.Processes;

using DispatchCandidate = ProcessRunAutomationDispatchService.DispatchCandidate;
using DispatchExecutionOutcome = ProcessRunAutomationDispatchService.DispatchExecutionOutcome;
using FinalizerContext = ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerContext;
using FinalizerResult = ProcessRunAutomationDispatchService.ProcessStepCompletionFinalizerResult;
using ProcessStepDispatchClaim = ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;

internal sealed class ProcessDispatchFinalizerApplicationService(
    Func<FinalizerContext, ProcessStepDispatchClaim, CancellationToken, Task<FinalizerResult?>> finalizeStepCompletionAsync,
    Func<DispatchCandidate, FinalizerResult, ProcessStepDispatchClaim, CancellationToken, Task> applyFinalizedStepTransitionAsync)
{
    public async Task FinalizeWorkflowCompletionAsync(
        ProcessRouteCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await FinalizeWorkflowCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            workflowOutcome,
            ToDispatcherClaim(dispatchClaim),
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
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome recoveryOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await FinalizeRecoveredCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(recoveryOutcome),
            trigger,
            renewLeaseAsync,
            ToDispatcherClaim(dispatchClaim),
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
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await FinalizeDirectAgentCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(executionOutcome),
            trigger,
            renewLeaseAsync,
            ToDispatcherClaim(dispatchClaim),
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
        ProcessRouteCandidate candidate,
        Guid subprocessRunId,
        ProcessStepRunStatus terminalStatus,
        string transitionReason,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await FinalizeSubprocessCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            subprocessRunId,
            terminalStatus,
            transitionReason,
            ToDispatcherClaim(dispatchClaim),
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
