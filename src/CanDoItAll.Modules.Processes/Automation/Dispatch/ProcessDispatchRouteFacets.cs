using CanDoItAll.Processes.Contracts;
using CanDoItAll.SharedKernel;

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
        ProcessRouteCandidate candidate,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchUpstreamMaterializationRouteFacet
{
    Task<bool> TryRequestMissingUpstreamArtifactMaterializationAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchRecoveryRouteFacet
{
    Task<ProcessRouteExecutionOutcome?> TryRecoverStrandedMissingCompletionArtifactsAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        ProcessRouteDispatchClaim dispatchClaim,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken);

    Task FinalizeRecoveredCompletionAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome recoveryOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchSubprocessRouteFacet
{
    Task HandleSubprocessDispatchAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        Guid? triggerStepRunId,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchStartTransitionRouteFacet
{
    Task<Result> TransitionStepWithClaimAsync(
        ProcessStepTransitionRequest request,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);

    Task<ProcessRouteCandidate?> LoadDispatchCandidateAsync(
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
        ProcessRouteCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchDirectAgentRouteFacet
{
    Task<ProcessRouteExecutionOutcome> ExecuteUntilSettledAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchGuardRouteFacet
{
    Task<ProcessAutomationExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome executionOutcome,
        CancellationToken cancellationToken);

    Task<bool> IsRunClosedToAutomationAsync(
        Guid processRunId,
        Guid stepRunId,
        CancellationToken cancellationToken);
}

internal interface IProcessDispatchFinalizerRouteFacet
{
    Task FinalizeDirectAgentCompletionAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken);
}
