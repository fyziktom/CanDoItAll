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
}
