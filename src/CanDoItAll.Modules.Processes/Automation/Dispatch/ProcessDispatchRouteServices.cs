using CanDoItAll.Processes.Contracts;
using CanDoItAll.SharedKernel;
using DispatchCandidate = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchCandidate;
using DispatchExecutionOutcome = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchExecutionOutcome;
using ProcessStepDispatchClaim = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchRouteServices(
    ProcessRunAutomationDispatchService dispatcher,
    ProcessWorkflowRunCoordinator workflowRunCoordinator)
    : IProcessDispatchDatabaseRequirementRouteFacet,
        IProcessDispatchUpstreamMaterializationRouteFacet,
        IProcessDispatchRecoveryRouteFacet,
        IProcessDispatchSubprocessRouteFacet,
        IProcessDispatchStartTransitionRouteFacet,
        IProcessDispatchWorkflowRouteFacet,
        IProcessDispatchDirectAgentRouteFacet,
        IProcessDispatchGuardRouteFacet,
        IProcessDispatchFinalizerRouteFacet
{
    public bool HasAutomationDatabaseRequirementFailure()
    {
        return dispatcher.HasAutomationDatabaseRequirementFailure();
    }

    public async Task BlockDispatchForCurrentDatabaseRequirementAsync(
        DispatchCandidate candidate,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await dispatcher.BlockDispatchForCurrentDatabaseRequirementAsync(
            candidate,
            dispatchClaim,
            cancellationToken);
    }

    public async Task<bool> TryRequestMissingUpstreamArtifactMaterializationAsync(
        DispatchCandidate candidate,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        return await dispatcher.TryRequestMissingUpstreamArtifactMaterializationAsync(
            candidate,
            dispatchClaim,
            cancellationToken);
    }

    public async Task<DispatchExecutionOutcome?> TryRecoverStrandedMissingCompletionArtifactsAsync(
        DispatchCandidate candidate,
        string trigger,
        ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        return await dispatcher.TryRecoverStrandedMissingCompletionArtifactsAsync(
            candidate,
            trigger,
            dispatchClaim,
            renewLeaseAsync,
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
        await dispatcher.FinalizeRecoveredCompletionAsync(
            candidate,
            recoveryOutcome,
            trigger,
            renewLeaseAsync,
            dispatchClaim,
            cancellationToken);
    }

    public async Task HandleSubprocessDispatchAsync(
        DispatchCandidate candidate,
        string trigger,
        Guid? triggerStepRunId,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await dispatcher.HandleSubprocessDispatchAsync(
            candidate,
            trigger,
            triggerStepRunId,
            dispatchClaim,
            cancellationToken);
    }

    public async Task<Result> TransitionStepWithClaimAsync(
        ProcessStepTransitionRequest request,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        return await dispatcher.TransitionStepWithClaimAsync(
            request,
            dispatchClaim,
            cancellationToken);
    }

    public async Task<DispatchCandidate?> LoadDispatchCandidateAsync(
        Guid processRunId,
        Guid claimedStepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        return await dispatcher.LoadDispatchCandidateAsync(
            processRunId,
            claimedStepRunId,
            trigger,
            cancellationToken);
    }

    public async Task<ProcessWorkflowExecutionOutcome> TryRunOrObserveWorkflowAsync(
        Guid processRunId,
        Guid stepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        return await workflowRunCoordinator.TryRunOrObserveAsync(
            processRunId,
            stepRunId,
            trigger,
            cancellationToken);
    }

    public async Task HandleWorkflowExecutionOutcomeAsync(
        DispatchCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await dispatcher.HandleWorkflowExecutionOutcomeAsync(
            candidate,
            workflowOutcome,
            dispatchClaim,
            cancellationToken);
    }

    public async Task<DispatchExecutionOutcome> ExecuteUntilSettledAsync(
        DispatchCandidate candidate,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        return await dispatcher.ExecuteUntilSettledAsync(
            candidate,
            trigger,
            renewLeaseAsync,
            cancellationToken);
    }

    public async Task<ProcessAutomationExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        CancellationToken cancellationToken)
    {
        return await dispatcher.ResolveCompetingActiveAutomationExecutionAsync(
            candidate,
            executionOutcome,
            cancellationToken);
    }

    public async Task<bool> IsRunClosedToAutomationAsync(
        Guid processRunId,
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        return await dispatcher.IsRunClosedToAutomationAsync(
            processRunId,
            stepRunId,
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
        await dispatcher.FinalizeDirectAgentCompletionAsync(
            candidate,
            executionOutcome,
            trigger,
            renewLeaseAsync,
            dispatchClaim,
            cancellationToken);
    }
}
