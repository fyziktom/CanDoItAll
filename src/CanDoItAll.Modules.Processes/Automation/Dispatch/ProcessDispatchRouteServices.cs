using CanDoItAll.Processes.Contracts;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchDatabaseRequirementRouteService(
    ProcessAutomationDatabaseRequirementResolver databaseRequirementResolver,
    ProcessDispatchPreExecutionGuardHandler preExecutionGuardHandler,
    ProcessDispatchStepTransitionService stepTransitionService,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessDispatchDatabaseRequirementRouteFacet
{
    public bool HasAutomationDatabaseRequirementFailure()
    {
        return databaseRequirementResolver.Resolve() is not null;
    }

    public async Task BlockDispatchForCurrentDatabaseRequirementAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var databaseRequirementFailure = databaseRequirementResolver.Resolve() ??
            throw new InvalidOperationException("Database requirement blocking was requested, but no active database requirement failure was resolved.");
        var dispatcherCandidate = ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate);
        var dispatcherClaim = ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim);
        var decision = preExecutionGuardHandler.BuildDatabaseRequirementDecision(
            dispatcherCandidate,
            databaseRequirementFailure.Message,
            ProcessRunAutomationDispatchService.AutomationActor);
        if (decision.IsUnsupportedNoOpTarget)
        {
            logger.LogWarning(
                "Process automation dispatch for run {RunId}, step {StepRunId} requires PostgreSQL but current status {Status} has no supported blocking transition. Reason: {Reason}",
                dispatcherCandidate.Run.Id,
                dispatcherCandidate.StepRun.Id,
                dispatcherCandidate.StepRun.Status,
                databaseRequirementFailure.Message);
            return;
        }

        if (!decision.IsTransitionAllowed)
        {
            logger.LogWarning(
                "Process automation dispatch for run {RunId}, step {StepRunId} requires PostgreSQL but current status {Status} cannot transition to {TargetStatus}. Reason: {Reason}",
                dispatcherCandidate.Run.Id,
                dispatcherCandidate.StepRun.Id,
                dispatcherCandidate.StepRun.Status,
                decision.TargetStatus,
                databaseRequirementFailure.Message);
            return;
        }

        var transitionRequest = decision.TransitionRequest
            ?? throw new InvalidOperationException("Database requirement transition request was not built for a supported target.");
        var transitionResult = await stepTransitionService.TransitionStepWithClaimAsync(
            transitionRequest,
            dispatcherClaim,
            cancellationToken);

        if (transitionResult.IsFailure)
        {
            logger.LogWarning(
                "Process step {StepRunId} could not be moved to {TargetStatus} after PostgreSQL runtime requirement failed. Errors: {Errors}",
                dispatcherCandidate.StepRun.Id,
                decision.TargetStatus,
                string.Join(" | ", transitionResult.Errors.Select(error => error.Message)));
            return;
        }

        logger.LogWarning(
            "Blocked process automation dispatch for run {RunId}, step {StepRunId} because the active database profile is not PostgreSQL.",
            dispatcherCandidate.Run.Id,
            dispatcherCandidate.StepRun.Id);
    }
}

internal sealed class ProcessDispatchUpstreamMaterializationRouteService(
    ProcessDispatchPreExecutionGuardHandler preExecutionGuardHandler,
    ProcessDispatchStepTransitionService stepTransitionService,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessDispatchUpstreamMaterializationRouteFacet
{
    public async Task<bool> TryRequestMissingUpstreamArtifactMaterializationAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var dispatcherCandidate = ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate);
        var dispatcherClaim = ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim);
        var plan = preExecutionGuardHandler.PlanMissingUpstreamArtifactMaterialization(dispatcherCandidate);
        if (!plan.HasMissingInputs)
        {
            return false;
        }

        if (dispatcherCandidate.StepRun.Status != ProcessStepRunStatus.Blocked)
        {
            var snapshot = await stepTransitionService.LoadStepRunTransitionSnapshotAsync(
                dispatcherCandidate.StepRun.Id,
                cancellationToken);
            if (snapshot is not null &&
                snapshot.Status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.InProgress)
            {
                var blockResult = await stepTransitionService.TransitionStepWithClaimAsync(
                    preExecutionGuardHandler.BuildMissingUpstreamArtifactBlockTransitionRequest(
                        plan,
                        dispatcherCandidate.StepRun.Id,
                        snapshot.ConcurrencyToken,
                        ProcessRunAutomationDispatchService.AutomationActor),
                    dispatcherClaim,
                    cancellationToken);
                if (blockResult.IsFailure)
                {
                    logger.LogWarning(
                        "Could not block downstream step {StepRunId} before upstream artifact materialization for run {RunId}. Errors: {Errors}",
                        dispatcherCandidate.StepRun.Id,
                        dispatcherCandidate.Run.Id,
                        string.Join(" | ", blockResult.Errors.Select(error => error.Message)));
                    return true;
                }
            }
        }

        return await preExecutionGuardHandler.RecordAndRequestMissingUpstreamArtifactMaterializationAsync(
            dispatcherCandidate,
            plan,
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
    ProcessDispatchSubprocessRuntimeService subprocessRuntimeService) : IProcessDispatchSubprocessRouteFacet
{
    public async Task HandleSubprocessDispatchAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        Guid? triggerStepRunId,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await subprocessRuntimeService.HandleSubprocessDispatchAsync(
            ProcessDispatchRouteModelAdapters.ToDispatcherCandidate(candidate),
            trigger,
            triggerStepRunId,
            ProcessDispatchRouteModelAdapters.ToDispatcherClaim(dispatchClaim),
            cancellationToken);
    }
}

internal sealed class ProcessDispatchStartTransitionRouteService(
    ProcessDispatchStepTransitionService stepTransitionService,
    ProcessDispatchCandidateHydrationService candidateHydrationService) : IProcessDispatchStartTransitionRouteFacet
{
    public async Task<Result> TransitionStepWithClaimAsync(
        ProcessStepTransitionRequest request,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        return await stepTransitionService.TransitionStepWithClaimAsync(
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
        var candidate = await candidateHydrationService.LoadAsync(
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
    ProcessRunAutomationDispatchService dispatcher,
    ProcessDispatchRunClosureGuardService runClosureGuardService) : IProcessDispatchGuardRouteFacet
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
        return await runClosureGuardService.IsRunClosedToAutomationAsync(
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
