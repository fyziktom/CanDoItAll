using CanDoItAll.Processes.Core.Routing;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class FreshRecoverySkipRouteHandler(
    IClock clock,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.FreshRecoverySkip;

    public Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        var routeSnapshot = context.CreateRouteSnapshot();
        if (!ProcessRunAutomationDispatchService.ShouldSkipFreshAutomationDispatch(routeSnapshot, clock.GetUtcNow()))
        {
            return Task.FromResult(ProcessDispatchRouteHandlerResult.NotHandled);
        }

        logger.LogInformation(
            "Skipping recovery redispatch within the fresh-step grace period for run {RunId}, step {StepRunId}, status {Status}, trigger {Trigger}. Recovery worker will retry if the execution remains stranded.",
            context.Candidate.Run.Id,
            context.Candidate.StepRun.Id,
            context.Candidate.StepRun.Status,
            ProcessRunAutomationDispatchService.NormalizeTrigger(context.Execution.Trigger, context.Execution.TriggerStepRunId));

        return Task.FromResult(ProcessDispatchRouteHandlerResult.DispatchComplete);
    }
}

internal sealed class DatabaseRequirementRouteHandler(
    IProcessDispatchDatabaseRequirementRouteFacet databaseRequirementFacet) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.DatabaseRequirement;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        var routeSnapshot = context.CreateRouteSnapshot();
        var hasDatabaseRequirementFailure = routeSnapshot.UsesAgentAutomation &&
            databaseRequirementFacet.HasAutomationDatabaseRequirementFailure();
        if (ProcessDispatchRoutePlanner.ResolveDatabaseRequirement(
                routeSnapshot,
                hasDatabaseRequirementFailure).Kind != ProcessDispatchRouteKind.DatabaseRequirement ||
            !hasDatabaseRequirementFailure)
        {
            return ProcessDispatchRouteHandlerResult.NotHandled;
        }

        await databaseRequirementFacet.BlockDispatchForCurrentDatabaseRequirementAsync(
            context.Candidate,
            context.Execution.DispatchClaim,
            context.Execution.DispatchCancellationToken);

        return ProcessDispatchRouteHandlerResult.DispatchComplete;
    }
}

internal sealed class UpstreamMaterializationRouteHandler(
    IProcessDispatchUpstreamMaterializationRouteFacet upstreamMaterializationFacet) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.UpstreamMaterialization;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        var materializationRequested = await upstreamMaterializationFacet.TryRequestMissingUpstreamArtifactMaterializationAsync(
            context.Candidate,
            context.Execution.DispatchClaim,
            context.Execution.DispatchCancellationToken);
        if (ProcessDispatchRoutePlanner.ResolveUpstreamMaterialization(materializationRequested).Kind != ProcessDispatchRouteKind.UpstreamMaterialization)
        {
            return ProcessDispatchRouteHandlerResult.NotHandled;
        }

        return ProcessDispatchRouteHandlerResult.DispatchComplete;
    }
}

internal sealed class StrandedArtifactRecoveryRouteHandler(
    IProcessDispatchRecoveryRouteFacet recoveryFacet) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.StrandedArtifactRecovery;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        var strandedArtifactRecoveryOutcome = await recoveryFacet.TryRecoverStrandedMissingCompletionArtifactsAsync(
            context.Candidate,
            context.Execution.Trigger,
            context.Execution.DispatchClaim,
            context.Execution.DispatchRenewLeaseAsync,
            context.Execution.DispatchCancellationToken);
        if (ProcessDispatchRoutePlanner.ResolveStrandedRecovery(strandedArtifactRecoveryOutcome is not null).Kind != ProcessDispatchRouteKind.StrandedRecovery ||
            strandedArtifactRecoveryOutcome is null)
        {
            return ProcessDispatchRouteHandlerResult.NotHandled;
        }

        await recoveryFacet.FinalizeRecoveredCompletionAsync(
            context.Candidate,
            strandedArtifactRecoveryOutcome,
            context.Execution.Trigger,
            context.Execution.DispatchRenewLeaseAsync,
            context.Execution.DispatchClaim,
            context.Execution.DispatchCancellationToken);

        return ProcessDispatchRouteHandlerResult.DispatchComplete;
    }
}

internal sealed class SubprocessRouteHandler(
    IProcessDispatchSubprocessRouteFacet subprocessFacet) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.Subprocess;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        if (ProcessDispatchRoutePlanner.ResolveSubprocess(context.CreateRouteSnapshot()).Kind != ProcessDispatchRouteKind.Subprocess)
        {
            return ProcessDispatchRouteHandlerResult.NotHandled;
        }

        await subprocessFacet.HandleSubprocessDispatchAsync(
            new ProcessDispatchSubprocessRuntimeInput(
                context.Candidate,
                context.Execution.Trigger,
                context.Execution.TriggerStepRunId,
                context.Execution.DispatchClaim),
            context.Execution.DispatchCancellationToken);

        return ProcessDispatchRouteHandlerResult.DispatchComplete;
    }
}

internal sealed class StartTransitionRouteHandler(
    IProcessDispatchStartTransitionRouteFacet startTransitionFacet,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.StartTransition;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        var routeSnapshot = context.CreateRouteSnapshot();
        if (!routeSnapshot.RequiresStartTransition)
        {
            return ProcessDispatchRouteHandlerResult.NotHandled;
        }

        var startResult = await startTransitionFacet.TransitionStepWithClaimAsync(
            ProcessDispatchStartTransitionPlanner.BuildStartTransitionRequest(
                routeSnapshot,
                context.Candidate.StepRun.ConcurrencyToken,
                ProcessRunAutomationDispatchService.AutomationActor),
            context.Execution.DispatchClaim,
            context.Execution.DispatchCancellationToken);
        if (!startResult.IsFailure)
        {
            return ProcessDispatchRouteHandlerResult.NotHandled;
        }

        logger.LogInformation(
            "Process step {StepRunId} could not be claimed for automation dispatch on run {RunId}. Errors: {Errors}",
            context.Candidate.StepRun.Id,
            context.Execution.ProcessRunId,
            string.Join(" | ", startResult.Errors.Select(error => error.Message)));
        var refreshedCandidate = await startTransitionFacet.LoadDispatchCandidateAsync(
            context.Execution.ProcessRunId,
            context.Execution.DispatchClaim.StepRunId,
            context.Execution.Trigger,
            context.Execution.DispatchCancellationToken);
        if (refreshedCandidate is null ||
            refreshedCandidate.StepRun.Id != context.Candidate.StepRun.Id ||
            refreshedCandidate.StepRun.Status != ProcessStepRunStatus.InProgress)
        {
            return ProcessDispatchRouteHandlerResult.ContinueCandidates;
        }

        logger.LogInformation(
            "Continuing process automation dispatch for run {RunId}, step {StepRunId} after reload confirmed the step is already InProgress.",
            refreshedCandidate.Run.Id,
            refreshedCandidate.StepRun.Id);
        context.UpdateCandidate(refreshedCandidate);

        return ProcessDispatchRouteHandlerResult.NotHandled;
    }
}

internal sealed class WorkflowRouteHandler(
    IProcessDispatchWorkflowRouteFacet workflowFacet) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.Workflow;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        var workflowOutcome = await workflowFacet.TryRunOrObserveWorkflowAsync(
            context.Candidate.Run.Id,
            context.Candidate.StepRun.Id,
            ProcessRunAutomationDispatchService.NormalizeTrigger(context.Execution.Trigger, context.Execution.TriggerStepRunId),
            context.Execution.DispatchCancellationToken);
        var workflowRoute = ProcessDispatchRoutePlanner.ResolveWorkflow(workflowOutcome.Handled);
        if (workflowRoute.Kind != ProcessDispatchRouteKind.Workflow)
        {
            return ProcessDispatchRouteHandlerResult.NotHandled;
        }

        await workflowFacet.HandleWorkflowExecutionOutcomeAsync(
            context.Candidate,
            workflowOutcome,
            context.Execution.DispatchClaim,
            context.Execution.DispatchCancellationToken);

        return ProcessDispatchRouteHandlerResult.DispatchComplete;
    }
}

internal sealed class DirectAgentExecutionRouteHandler(
    IProcessDispatchDirectAgentRouteFacet directAgentFacet) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.DirectAgentExecution;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        var executionOutcome = await directAgentFacet.ExecuteUntilSettledAsync(
            new ProcessDispatchDirectAgentExecutionInput(
                context.Candidate,
                context.Execution.Trigger,
                context.Execution.DispatchRenewLeaseAsync),
            context.Execution.DispatchCancellationToken);
        context.Execution.DispatchHeartbeat?.ThrowIfClaimLost();
        context.SetDirectAgentExecutionOutcome(executionOutcome);

        return ProcessDispatchRouteHandlerResult.NotHandled;
    }
}

internal sealed class CompetingExecutionGuardRouteHandler(
    IProcessDispatchGuardRouteFacet guardFacet,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.CompetingExecutionGuard;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        var executionOutcome = context.GetRequiredDirectAgentExecutionOutcome(Stage);
        var competingExecution = executionOutcome.CompletionStatus is not ProcessStepRunStatus.Completed
            ? await guardFacet.ResolveCompetingActiveAutomationExecutionAsync(
                context.Candidate,
                executionOutcome,
                context.Execution.DispatchCancellationToken)
            : null;
        if (competingExecution is null)
        {
            return ProcessDispatchRouteHandlerResult.NotHandled;
        }

        logger.LogInformation(
            "Skipping non-successful automation completion transition for run {RunId}, step {StepRunId}, execution run {ExecutionRunId} because execution run {CompetingExecutionRunId} is still active for the same process step.",
            context.Candidate.Run.Id,
            context.Candidate.StepRun.Id,
            executionOutcome.ExecutionRun.Id,
            competingExecution.Id);

        return ProcessDispatchRouteHandlerResult.DispatchComplete;
    }
}

internal sealed class RunClosedGuardRouteHandler(
    IProcessDispatchGuardRouteFacet guardFacet,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.RunClosedGuard;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        _ = context.GetRequiredDirectAgentExecutionOutcome(Stage);
        if (!await guardFacet.IsRunClosedToAutomationAsync(
                context.Candidate.Run.Id,
                context.Candidate.StepRun.Id,
                context.Execution.DispatchCancellationToken))
        {
            return ProcessDispatchRouteHandlerResult.NotHandled;
        }

        logger.LogInformation(
            "Skipping automation completion projection for run {RunId}, step {StepRunId} because the process run became terminal while agent execution was in flight.",
            context.Candidate.Run.Id,
            context.Candidate.StepRun.Id);

        return ProcessDispatchRouteHandlerResult.DispatchComplete;
    }
}

internal sealed class FinalizerTransitionRouteHandler(
    IProcessDispatchFinalizerRouteFacet finalizerFacet) : IProcessDispatchRouteHandler
{
    public ProcessDispatchRouteStage Stage => ProcessDispatchRouteStage.FinalizerTransition;

    public async Task<ProcessDispatchRouteHandlerResult> HandleAsync(ProcessDispatchRouteContext context)
    {
        var executionOutcome = context.GetRequiredDirectAgentExecutionOutcome(Stage);
        await finalizerFacet.FinalizeDirectAgentCompletionAsync(
            context.Candidate,
            executionOutcome,
            context.Execution.Trigger,
            context.Execution.DispatchRenewLeaseAsync,
            context.Execution.DispatchClaim,
            context.Execution.DispatchCancellationToken);
        context.Execution.DispatchHeartbeat?.ThrowIfClaimLost();

        return ProcessDispatchRouteHandlerResult.DispatchComplete;
    }
}
