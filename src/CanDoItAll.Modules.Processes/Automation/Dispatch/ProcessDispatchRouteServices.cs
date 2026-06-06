using CanDoItAll.Processes.Contracts;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessDispatchRouteFacetSet(
    IProcessDispatchDatabaseRequirementRouteFacet DatabaseRequirement,
    IProcessDispatchUpstreamMaterializationRouteFacet UpstreamMaterialization,
    IProcessDispatchRecoveryRouteFacet Recovery,
    IProcessDispatchSubprocessRouteFacet Subprocess,
    IProcessDispatchStartTransitionRouteFacet StartTransition,
    IProcessDispatchWorkflowRouteFacet Workflow,
    IProcessDispatchDirectAgentRouteFacet DirectAgent,
    IProcessDispatchGuardRouteFacet Guard,
    IProcessDispatchFinalizerRouteFacet Finalizer);

internal sealed class ProcessDispatchDatabaseRequirementRouteService(
    ProcessRunAutomationDispatchService dispatcher) : IProcessDispatchDatabaseRequirementRouteFacet
{
    public bool HasAutomationDatabaseRequirementFailure()
    {
        return dispatcher.HasAutomationDatabaseRequirementFailure();
    }

    public async Task BlockDispatchForCurrentDatabaseRequirementAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await dispatcher.BlockDispatchForCurrentDatabaseRequirementAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            cancellationToken);
    }
}

internal sealed class ProcessDispatchUpstreamMaterializationRouteService(
    ProcessRunAutomationDispatchService dispatcher) : IProcessDispatchUpstreamMaterializationRouteFacet
{
    public async Task<bool> TryRequestMissingUpstreamArtifactMaterializationAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        return await dispatcher.TryRequestMissingUpstreamArtifactMaterializationAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            cancellationToken);
    }
}

internal sealed class ProcessDispatchRecoveryRouteService(
    ProcessRunAutomationDispatchService dispatcher) : IProcessDispatchRecoveryRouteFacet
{
    public async Task<ProcessRouteExecutionOutcome?> TryRecoverStrandedMissingCompletionArtifactsAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        ProcessRouteDispatchClaim dispatchClaim,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        var recoveryOutcome = await dispatcher.TryRecoverStrandedMissingCompletionArtifactsAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            trigger,
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            renewLeaseAsync,
            cancellationToken);

        return recoveryOutcome is null
            ? null
            : ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(recoveryOutcome);
    }

    public async Task FinalizeRecoveredCompletionAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome recoveryOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await dispatcher.FinalizeRecoveredCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(recoveryOutcome),
            trigger,
            renewLeaseAsync,
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            cancellationToken);
    }
}

internal sealed class ProcessDispatchSubprocessRouteService(
    ProcessRunAutomationDispatchService dispatcher) : IProcessDispatchSubprocessRouteFacet
{
    public async Task HandleSubprocessDispatchAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        Guid? triggerStepRunId,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await dispatcher.HandleSubprocessDispatchAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            trigger,
            triggerStepRunId,
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            cancellationToken);
    }
}

internal sealed class ProcessDispatchStartTransitionRouteService(
    ProcessRunAutomationDispatchService dispatcher) : IProcessDispatchStartTransitionRouteFacet
{
    public async Task<Result> TransitionStepWithClaimAsync(
        ProcessStepTransitionRequest request,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        return await dispatcher.TransitionStepWithClaimAsync(
            request,
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            cancellationToken);
    }

    public async Task<ProcessRouteCandidate?> LoadDispatchCandidateAsync(
        Guid processRunId,
        Guid claimedStepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        var candidate = await dispatcher.LoadDispatchCandidateAsync(
            processRunId,
            claimedStepRunId,
            trigger,
            cancellationToken);

        return candidate is null
            ? null
            : ProcessDispatchRouteModelAdapters.FromDispatcherCandidate(candidate);
    }
}

internal sealed class ProcessDispatchWorkflowRouteService(
    ProcessRunAutomationDispatchService dispatcher,
    ProcessWorkflowRunCoordinator workflowRunCoordinator) : IProcessDispatchWorkflowRouteFacet
{
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
        ProcessRouteCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await dispatcher.HandleWorkflowExecutionOutcomeAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            workflowOutcome,
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            cancellationToken);
    }
}

internal sealed class ProcessDispatchDirectAgentRouteService(
    ProcessRunAutomationDispatchService dispatcher) : IProcessDispatchDirectAgentRouteFacet
{
    public async Task<ProcessRouteExecutionOutcome> ExecuteUntilSettledAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        var executionOutcome = await dispatcher.ExecuteUntilSettledAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            trigger,
            renewLeaseAsync,
            cancellationToken);

        return ProcessDispatchRouteModelAdapters.FromDispatcherExecutionOutcome(executionOutcome);
    }
}

internal sealed class ProcessDispatchGuardRouteService(
    ProcessRunAutomationDispatchService dispatcher) : IProcessDispatchGuardRouteFacet
{
    public async Task<ProcessAutomationExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome executionOutcome,
        CancellationToken cancellationToken)
    {
        return await dispatcher.ResolveCompetingActiveAutomationExecutionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(executionOutcome),
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
}

internal sealed class ProcessDispatchFinalizerRouteService(
    ProcessRunAutomationDispatchService dispatcher) : IProcessDispatchFinalizerRouteFacet
{
    public async Task FinalizeDirectAgentCompletionAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await dispatcher.FinalizeDirectAgentCompletionAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            ProcessDispatchRouteModelAdapters.ToDispatcherExecutionOutcome(executionOutcome),
            trigger,
            renewLeaseAsync,
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            cancellationToken);
    }
}
