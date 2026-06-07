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

        var candidate = ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate);
        await FinalizeAndApplyAsync(
            candidate,
            ProcessDispatchFinalizerContextFactory.ForWorkflow(candidate, input.WorkflowOutcome),
            ToDispatcherClaim(input.DispatchClaim),
            cancellationToken);
    }

    public async Task FinalizeRecoveredCompletionAsync(
        ProcessDispatchRecoveredFinalizerInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var candidate = ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate);
        await FinalizeAndApplyAsync(
            candidate,
            ProcessDispatchFinalizerContextFactory.ForManagerArtifactRecovery(
                candidate,
                ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(input.RecoveryOutcome),
                input.Trigger,
                input.RenewLeaseAsync),
            ToDispatcherClaim(input.DispatchClaim),
            cancellationToken);
    }

    public async Task FinalizeDirectAgentCompletionAsync(
        ProcessDispatchDirectAgentFinalizerInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var candidate = ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate);
        await FinalizeAndApplyAsync(
            candidate,
            ProcessDispatchFinalizerContextFactory.ForDirectAgent(
                candidate,
                ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(input.ExecutionOutcome),
                input.Trigger,
                input.RenewLeaseAsync),
            ToDispatcherClaim(input.DispatchClaim),
            cancellationToken);
    }

    public async Task FinalizeSubprocessCompletionAsync(
        ProcessDispatchSubprocessFinalizerInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var candidate = ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(input.Candidate);
        await FinalizeAndApplyAsync(
            candidate,
            ProcessDispatchFinalizerContextFactory.ForSubprocess(
                candidate,
                input.SubprocessRunId,
                input.TerminalStatus,
                input.TransitionReason),
            ToDispatcherClaim(input.DispatchClaim),
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
