namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private ProcessDispatchRouteHandlerPipeline CreateClaimedDispatchRouteHandlerPipeline()
    {
        return ProcessDispatchRouteHandlerFactory.Create(
            clock,
            logger,
            new ProcessDispatchRouteServices(this, workflowRunCoordinator));
    }

    internal bool HasAutomationDatabaseRequirementFailure()
    {
        return ResolveAutomationDatabaseRequirementFailure() is not null;
    }

    internal async Task BlockDispatchForCurrentDatabaseRequirementAsync(
        DispatchCandidate candidate,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var databaseRequirementFailure = ResolveAutomationDatabaseRequirementFailure() ??
            throw new InvalidOperationException("Database requirement blocking was requested, but no active database requirement failure was resolved.");

        await BlockDispatchForDatabaseRequirementAsync(
            candidate,
            databaseRequirementFailure,
            dispatchClaim,
            cancellationToken);
    }

    internal async Task FinalizeRecoveredCompletionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome recoveryOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var finalizedRecoveryCompletion = await FinalizeStepCompletionAsync(
            ProcessDispatchFinalizerContextFactory.ForManagerArtifactRecovery(
                candidate,
                recoveryOutcome,
                trigger,
                renewLeaseAsync),
            dispatchClaim,
            cancellationToken);
        if (finalizedRecoveryCompletion is null)
        {
            return;
        }

        await ApplyFinalizedStepTransitionAsync(
            candidate,
            finalizedRecoveryCompletion,
            dispatchClaim,
            cancellationToken);
    }

    internal async Task FinalizeDirectAgentCompletionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var finalizedCompletion = await FinalizeStepCompletionAsync(
            ProcessDispatchFinalizerContextFactory.ForDirectAgent(
                candidate,
                executionOutcome,
                trigger,
                renewLeaseAsync),
            dispatchClaim,
            cancellationToken);
        if (finalizedCompletion is null)
        {
            return;
        }

        await ApplyFinalizedStepTransitionAsync(
            candidate,
            finalizedCompletion,
            dispatchClaim,
            cancellationToken);
    }
}
