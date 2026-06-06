using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private enum ProcessClaimedDispatchResult
    {
        DispatchComplete,
        ContinueCandidates
    }

    private sealed class ProcessClaimedDispatchExecution
    {
        public ProcessClaimedDispatchExecution(
            Guid processRunId,
            Guid? triggerStepRunId,
            string trigger,
            ProcessStepDispatchClaim dispatchClaim,
            Func<CancellationToken, Task> dispatchRenewLeaseAsync,
            CancellationToken rootCancellationToken)
        {
            ProcessRunId = processRunId;
            TriggerStepRunId = triggerStepRunId;
            Trigger = trigger;
            DispatchClaim = dispatchClaim;
            DispatchRenewLeaseAsync = dispatchRenewLeaseAsync;
            RootCancellationToken = rootCancellationToken;
            DispatchCancellationToken = rootCancellationToken;
        }

        public Guid ProcessRunId { get; }

        public Guid? TriggerStepRunId { get; }

        public string Trigger { get; }

        public ProcessStepDispatchClaim DispatchClaim { get; }

        public Func<CancellationToken, Task> DispatchRenewLeaseAsync { get; }

        public CancellationToken RootCancellationToken { get; }

        public CancellationToken DispatchCancellationToken { get; set; }

        public ProcessDispatchLeaseHeartbeat? DispatchHeartbeat { get; set; }

        public DispatchCandidate? Candidate { get; set; }
    }

    private async Task<ProcessClaimedDispatchResult> RunClaimedDispatchAsync(
        ProcessDispatchClaimCoordinator claimCoordinator,
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task>? renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        var dispatchRenewLeaseAsync = CreateDispatchRenewLeaseCallback(claimCoordinator, dispatchClaim, renewLeaseAsync);
        var execution = new ProcessClaimedDispatchExecution(
            processRunId,
            triggerStepRunId,
            trigger,
            dispatchClaim,
            dispatchRenewLeaseAsync,
            cancellationToken);

        try
        {
            execution.DispatchHeartbeat = claimCoordinator.StartHeartbeat(
                dispatchClaim,
                dispatchRenewLeaseAsync,
                cancellationToken);
            execution.DispatchCancellationToken = execution.DispatchHeartbeat.DispatchCancellationToken;
            return await ExecuteClaimedDispatchRouteAsync(execution);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException) when (execution.DispatchHeartbeat?.ClaimLost == true)
        {
            return HandleDispatchHeartbeatClaimLost(execution);
        }
        catch (ProcessDispatchClaimLostException exception)
        {
            return HandleDispatchClaimLost(execution, exception);
        }
        catch (Exception exception)
        {
            return await HandleDispatchFailureAsync(execution, exception);
        }
        finally
        {
            if (execution.DispatchHeartbeat is not null)
            {
                await execution.DispatchHeartbeat.DisposeAsync();
            }

            await claimCoordinator.ReleaseAsync(dispatchClaim, cancellationToken);
        }
    }

    private async Task<ProcessClaimedDispatchResult> ExecuteClaimedDispatchRouteAsync(
        ProcessClaimedDispatchExecution execution)
    {
        var candidateHydrationStarted = Stopwatch.GetTimestamp();
        execution.Candidate = await LoadDispatchCandidateAsync(
            execution.ProcessRunId,
            execution.DispatchClaim.StepRunId,
            execution.Trigger,
            execution.DispatchCancellationToken);
        logger.LogDebug(
            "Hydrated claimed dispatch candidate for process run {ProcessRunId}, step {StepRunId}. CandidateFound={CandidateFound} ElapsedMilliseconds={ElapsedMilliseconds}.",
            execution.ProcessRunId,
            execution.DispatchClaim.StepRunId,
            execution.Candidate is not null,
            GetElapsedMilliseconds(candidateHydrationStarted));
        if (execution.Candidate is null)
        {
            return ProcessClaimedDispatchResult.ContinueCandidates;
        }

        var candidate = execution.Candidate;
        var routeSnapshot = ProcessDispatchRouteSnapshot.Create(candidate, execution.Trigger, execution.TriggerStepRunId);
        if (ShouldSkipFreshAutomationDispatch(routeSnapshot, clock.GetUtcNow()))
        {
            logger.LogInformation(
                "Skipping recovery redispatch within the fresh-step grace period for run {RunId}, step {StepRunId}, status {Status}, trigger {Trigger}. Recovery worker will retry if the execution remains stranded.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                candidate.StepRun.Status,
                NormalizeTrigger(execution.Trigger, execution.TriggerStepRunId));
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        var databaseRequirementFailure = routeSnapshot.UsesAgentAutomation
            ? ResolveAutomationDatabaseRequirementFailure()
            : null;
        if (ProcessDispatchRoutePlanner.ResolveDatabaseRequirement(
                routeSnapshot,
                databaseRequirementFailure is not null).Kind == ProcessDispatchRouteKind.DatabaseRequirement &&
            databaseRequirementFailure is not null)
        {
            await BlockDispatchForDatabaseRequirementAsync(
                candidate,
                databaseRequirementFailure,
                execution.DispatchClaim,
                execution.DispatchCancellationToken);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        var materializationRequested = await TryRequestMissingUpstreamArtifactMaterializationAsync(
            candidate,
            execution.DispatchClaim,
            execution.DispatchCancellationToken);
        if (ProcessDispatchRoutePlanner.ResolveUpstreamMaterialization(materializationRequested).Kind == ProcessDispatchRouteKind.UpstreamMaterialization)
        {
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        var strandedArtifactRecoveryOutcome = await TryRecoverStrandedMissingCompletionArtifactsAsync(
            candidate,
            execution.Trigger,
            execution.DispatchClaim,
            execution.DispatchRenewLeaseAsync,
            execution.DispatchCancellationToken);
        if (ProcessDispatchRoutePlanner.ResolveStrandedRecovery(strandedArtifactRecoveryOutcome is not null).Kind == ProcessDispatchRouteKind.StrandedRecovery &&
            strandedArtifactRecoveryOutcome is not null)
        {
            var finalizedRecoveryCompletion = await FinalizeStepCompletionAsync(
                ProcessDispatchFinalizerContextFactory.ForManagerArtifactRecovery(
                    candidate,
                    strandedArtifactRecoveryOutcome,
                    execution.Trigger,
                    execution.DispatchRenewLeaseAsync),
                execution.DispatchClaim,
                execution.DispatchCancellationToken);
            if (finalizedRecoveryCompletion is not null)
            {
                await ApplyFinalizedStepTransitionAsync(
                    candidate,
                    finalizedRecoveryCompletion,
                    execution.DispatchClaim,
                    execution.DispatchCancellationToken);
            }

            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        if (ProcessDispatchRoutePlanner.ResolveSubprocess(routeSnapshot).Kind == ProcessDispatchRouteKind.Subprocess)
        {
            await HandleSubprocessDispatchAsync(
                candidate,
                execution.Trigger,
                execution.TriggerStepRunId,
                execution.DispatchClaim,
                execution.DispatchCancellationToken);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        if (routeSnapshot.RequiresStartTransition)
        {
            var startResult = await TransitionStepWithClaimAsync(
                ProcessDispatchStartTransitionPlanner.BuildStartTransitionRequest(
                    routeSnapshot,
                    candidate.StepRun.ConcurrencyToken,
                    AutomationActor),
                execution.DispatchClaim,
                execution.DispatchCancellationToken);
            if (startResult.IsFailure)
            {
                logger.LogInformation(
                    "Process step {StepRunId} could not be claimed for automation dispatch on run {RunId}. Errors: {Errors}",
                    candidate.StepRun.Id,
                    execution.ProcessRunId,
                    string.Join(" | ", startResult.Errors.Select(error => error.Message)));
                var refreshedCandidate = await LoadDispatchCandidateAsync(
                    execution.ProcessRunId,
                    execution.DispatchClaim.StepRunId,
                    execution.Trigger,
                    execution.DispatchCancellationToken);
                if (refreshedCandidate is null ||
                    refreshedCandidate.StepRun.Id != candidate.StepRun.Id ||
                    refreshedCandidate.StepRun.Status != ProcessStepRunStatus.InProgress)
                {
                    return ProcessClaimedDispatchResult.ContinueCandidates;
                }

                logger.LogInformation(
                    "Continuing process automation dispatch for run {RunId}, step {StepRunId} after reload confirmed the step is already InProgress.",
                    refreshedCandidate.Run.Id,
                    refreshedCandidate.StepRun.Id);
                candidate = refreshedCandidate;
                execution.Candidate = refreshedCandidate;
            }
        }

        var workflowOutcome = await workflowRunCoordinator.TryRunOrObserveAsync(
            candidate.Run.Id,
            candidate.StepRun.Id,
            NormalizeTrigger(execution.Trigger, execution.TriggerStepRunId),
            execution.DispatchCancellationToken);
        var workflowRoute = ProcessDispatchRoutePlanner.ResolveWorkflow(workflowOutcome.Handled);
        if (workflowRoute.Kind == ProcessDispatchRouteKind.Workflow)
        {
            await HandleWorkflowExecutionOutcomeAsync(
                candidate,
                workflowOutcome,
                execution.DispatchClaim,
                execution.DispatchCancellationToken);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        var executionOutcome = await ExecuteUntilSettledAsync(
            candidate,
            execution.Trigger,
            execution.DispatchRenewLeaseAsync,
            execution.DispatchCancellationToken);
        execution.DispatchHeartbeat?.ThrowIfClaimLost();

        var competingExecution = executionOutcome.CompletionStatus is not ProcessStepRunStatus.Completed
            ? await ResolveCompetingActiveAutomationExecutionAsync(candidate, executionOutcome, execution.DispatchCancellationToken)
            : null;
        if (competingExecution is not null)
        {
            logger.LogInformation(
                "Skipping non-successful automation completion transition for run {RunId}, step {StepRunId}, execution run {ExecutionRunId} because execution run {CompetingExecutionRunId} is still active for the same process step.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                executionOutcome.Detail.Run.Id,
                competingExecution.Id);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        if (await IsRunClosedToAutomationAsync(candidate.Run.Id, candidate.StepRun.Id, execution.DispatchCancellationToken))
        {
            logger.LogInformation(
                "Skipping automation completion projection for run {RunId}, step {StepRunId} because the process run became terminal while agent execution was in flight.",
                candidate.Run.Id,
                candidate.StepRun.Id);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        var finalizedCompletion = await FinalizeStepCompletionAsync(
            ProcessDispatchFinalizerContextFactory.ForDirectAgent(
                candidate,
                executionOutcome,
                execution.Trigger,
                execution.DispatchRenewLeaseAsync),
            execution.DispatchClaim,
            execution.DispatchCancellationToken);
        execution.DispatchHeartbeat?.ThrowIfClaimLost();
        if (finalizedCompletion is not null)
        {
            await ApplyFinalizedStepTransitionAsync(
                candidate,
                finalizedCompletion,
                execution.DispatchClaim,
                execution.DispatchCancellationToken);
        }

        return ProcessClaimedDispatchResult.DispatchComplete;
    }
}
