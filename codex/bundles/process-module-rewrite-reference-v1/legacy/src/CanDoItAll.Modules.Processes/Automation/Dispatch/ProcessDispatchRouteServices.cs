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
        var routeFacts = ProcessDispatchPreExecutionRouteFacts.FromCandidate(candidate);
        var decision = preExecutionGuardHandler.BuildDatabaseRequirementDecision(
            routeFacts,
            databaseRequirementFailure.Message,
            ProcessRunAutomationDispatchService.AutomationActor);
        if (decision.IsUnsupportedNoOpTarget)
        {
            logger.LogWarning(
                "Process automation dispatch for run {RunId}, step {StepRunId} requires PostgreSQL but current status {Status} has no supported blocking transition. Reason: {Reason}",
                routeFacts.Run.Id,
                routeFacts.StepRun.Id,
                routeFacts.StepRun.Status,
                databaseRequirementFailure.Message);
            return;
        }

        if (!decision.IsTransitionAllowed)
        {
            logger.LogWarning(
                "Process automation dispatch for run {RunId}, step {StepRunId} requires PostgreSQL but current status {Status} cannot transition to {TargetStatus}. Reason: {Reason}",
                routeFacts.Run.Id,
                routeFacts.StepRun.Id,
                routeFacts.StepRun.Status,
                decision.TargetStatus,
                databaseRequirementFailure.Message);
            return;
        }

        var transitionRequest = decision.TransitionRequest
            ?? throw new InvalidOperationException("Database requirement transition request was not built for a supported target.");
        var transitionResult = await stepTransitionService.TransitionStepWithClaimAsync(
            transitionRequest,
            dispatchClaim,
            cancellationToken);

        if (transitionResult.IsFailure)
        {
            logger.LogWarning(
                "Process step {StepRunId} could not be moved to {TargetStatus} after PostgreSQL runtime requirement failed. Errors: {Errors}",
                routeFacts.StepRun.Id,
                decision.TargetStatus,
                string.Join(" | ", transitionResult.Errors.Select(error => error.Message)));
            return;
        }

        logger.LogWarning(
            "Blocked process automation dispatch for run {RunId}, step {StepRunId} because the active database profile is not PostgreSQL.",
            routeFacts.Run.Id,
            routeFacts.StepRun.Id);
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
        var routeFacts = ProcessDispatchPreExecutionRouteFacts.FromCandidate(candidate);
        var plan = preExecutionGuardHandler.PlanMissingUpstreamArtifactMaterialization(routeFacts);
        if (!plan.HasMissingInputs)
        {
            return false;
        }

        if (routeFacts.StepRun.Status != ProcessStepRunStatus.Blocked)
        {
            var snapshot = await stepTransitionService.LoadStepRunTransitionSnapshotAsync(
                routeFacts.StepRun.Id,
                cancellationToken);
            if (snapshot is not null &&
                snapshot.Status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.InProgress)
            {
                var blockResult = await stepTransitionService.TransitionStepWithClaimAsync(
                    preExecutionGuardHandler.BuildMissingUpstreamArtifactBlockTransitionRequest(
                        plan,
                        routeFacts.StepRun.Id,
                        snapshot.ConcurrencyToken,
                        ProcessRunAutomationDispatchService.AutomationActor),
                    dispatchClaim,
                    cancellationToken);
                if (blockResult.IsFailure)
                {
                    logger.LogWarning(
                        "Could not block downstream step {StepRunId} before upstream artifact materialization for run {RunId}. Errors: {Errors}",
                        routeFacts.StepRun.Id,
                        routeFacts.Run.Id,
                        string.Join(" | ", blockResult.Errors.Select(error => error.Message)));
                    return true;
                }
            }
        }

        return await preExecutionGuardHandler.RecordAndRequestMissingUpstreamArtifactMaterializationAsync(
            routeFacts,
            plan,
            cancellationToken);
    }
}

internal sealed class ProcessDispatchRecoveryRouteService(
    ProcessDispatchRecoveryRuntimeService recoveryRuntimeService,
    ProcessDispatchFinalizerApplicationService finalizerApplicationService) : IProcessDispatchRecoveryRouteFacet
{
    public async Task<ProcessRouteExecutionOutcome?> TryRecoverStrandedMissingCompletionArtifactsAsync(
        ProcessRouteCandidate candidate,
        string trigger,
        ProcessRouteDispatchClaim dispatchClaim,
        Func<CancellationToken, Task> renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        return await recoveryRuntimeService.TryRecoverStrandedMissingCompletionArtifactsAsync(
            candidate,
            trigger,
            dispatchClaim,
            renewLeaseAsync,
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
        await finalizerApplicationService.FinalizeRecoveredCompletionAsync(
            new ProcessDispatchRecoveredFinalizerInput(
                candidate,
                recoveryOutcome,
                trigger,
                renewLeaseAsync,
                dispatchClaim),
            cancellationToken);
    }
}

internal sealed class ProcessDispatchSubprocessRouteService(
    ProcessDispatchSubprocessRuntimeService subprocessRuntimeService) : IProcessDispatchSubprocessRouteFacet
{
    public async Task HandleSubprocessDispatchAsync(
        ProcessDispatchSubprocessRuntimeInput input,
        CancellationToken cancellationToken)
    {
        await subprocessRuntimeService.HandleSubprocessDispatchAsync(
            input,
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
            dispatchClaim,
            cancellationToken);
    }

    public async Task<ProcessRouteCandidate?> LoadDispatchCandidateAsync(
        Guid processRunId,
        Guid claimedStepRunId,
        string trigger,
        CancellationToken cancellationToken)
    {
        return await candidateHydrationService.LoadRouteCandidateAsync(
            processRunId,
            claimedStepRunId,
            trigger,
            cancellationToken);
    }
}

internal sealed class ProcessDispatchWorkflowRouteService(
    ProcessDispatchFinalizerApplicationService finalizerApplicationService,
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
        await finalizerApplicationService.FinalizeWorkflowCompletionAsync(
            new ProcessDispatchWorkflowFinalizerInput(
                candidate,
                workflowOutcome,
                dispatchClaim),
            cancellationToken);
    }
}

internal sealed class ProcessDispatchDirectAgentRouteService(
    ProcessDispatchDirectAgentRuntimeService directAgentRuntimeService) : IProcessDispatchDirectAgentRouteFacet
{
    public async Task<ProcessRouteExecutionOutcome> ExecuteUntilSettledAsync(
        ProcessDispatchDirectAgentExecutionInput input,
        CancellationToken cancellationToken)
    {
        return await directAgentRuntimeService.ExecuteUntilSettledAsync(
            input,
            cancellationToken);
    }
}

internal sealed class ProcessDispatchGuardRouteService(
    ProcessDispatchCompetingExecutionGuardService competingExecutionGuardService,
    ProcessDispatchRunClosureGuardService runClosureGuardService) : IProcessDispatchGuardRouteFacet
{
    public async Task<ProcessAutomationExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome executionOutcome,
        CancellationToken cancellationToken)
    {
        return await competingExecutionGuardService.ResolveCompetingActiveAutomationExecutionAsync(
            candidate,
            executionOutcome,
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
    ProcessDispatchFinalizerApplicationService finalizerApplicationService) : IProcessDispatchFinalizerRouteFacet
{
    public async Task FinalizeDirectAgentCompletionAsync(
        ProcessRouteCandidate candidate,
        ProcessRouteExecutionOutcome executionOutcome,
        string trigger,
        Func<CancellationToken, Task> renewLeaseAsync,
        ProcessRouteDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await finalizerApplicationService.FinalizeDirectAgentCompletionAsync(
            new ProcessDispatchDirectAgentFinalizerInput(
                candidate,
                executionOutcome,
                trigger,
                renewLeaseAsync,
                dispatchClaim),
            cancellationToken);
    }
}
