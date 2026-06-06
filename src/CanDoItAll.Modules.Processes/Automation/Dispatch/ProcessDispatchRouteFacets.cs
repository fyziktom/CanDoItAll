using CanDoItAll.Processes.Contracts;
using CanDoItAll.SharedKernel;
using DispatchCandidate = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchCandidate;
using DispatchExecutionOutcome = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.DispatchExecutionOutcome;
using ProcessStepDispatchClaim = CanDoItAll.Modules.Processes.ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;

namespace CanDoItAll.Modules.Processes;

internal interface IProcessDispatchRouteHandler
{
    ProcessDispatchRouteStage Stage { get; }

    Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context);
}

internal interface IProcessDispatchDatabaseRequirementRouteFacet
{
    bool HasAutomationDatabaseRequirementFailure();

    Task BlockDispatchForCurrentDatabaseRequirementAsync(
        DispatchCandidate candidate,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchUpstreamMaterializationRouteFacet
{
    Task<bool> TryRequestMissingUpstreamArtifactMaterializationAsync(
        DispatchCandidate candidate,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchRecoveryRouteFacet
{
    Task<DispatchExecutionOutcome?> TryRecoverStrandedMissingCompletionArtifactsAsync(
        DispatchCandidate candidate,
        string trigger,
        ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken);

    Task FinalizeRecoveredCompletionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome recoveryOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchSubprocessRouteFacet
{
    Task HandleSubprocessDispatchAsync(
        DispatchCandidate candidate,
        string trigger,
        Guid? triggerStepRunId,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchStartTransitionRouteFacet
{
    Task<Result> TransitionStepWithClaimAsync(
        ProcessStepTransitionRequest request,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);

    Task<DispatchCandidate?> LoadDispatchCandidateAsync(
        Guid processRunId,
        Guid claimedStepRunId,
        string trigger,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchWorkflowRouteFacet
{
    Task<ProcessWorkflowExecutionOutcome> TryRunOrObserveWorkflowAsync(
        Guid processRunId,
        Guid stepRunId,
        string trigger,
        CancellationToken cancellationToken);

    Task HandleWorkflowExecutionOutcomeAsync(
        DispatchCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchDirectAgentRouteFacet
{
    Task<DispatchExecutionOutcome> ExecuteUntilSettledAsync(
        DispatchCandidate candidate,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchGuardRouteFacet
{
    Task<ProcessAutomationExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        CancellationToken cancellationToken);

    Task<bool> IsRunClosedToAutomationAsync(
        Guid processRunId,
        Guid stepRunId,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchFinalizerRouteFacet
{
    Task FinalizeDirectAgentCompletionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}
