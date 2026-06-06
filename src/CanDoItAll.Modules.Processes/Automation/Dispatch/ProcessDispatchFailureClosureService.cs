using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchFailureClosureService(
    ProcessDispatchRunClosureGuardService runClosureGuardService,
    ProcessDispatchStepTransitionService stepTransitionService,
    Func<ProcessRunAutomationDispatchService.ProcessStepDispatchClaim, CancellationToken, Task<bool>> isStepDispatchClaimHeldAsync,
    ILogger<ProcessRunAutomationDispatchService> logger)
{
    public ProcessClaimedDispatchResult HandleDispatchHeartbeatClaimLost(
        ProcessRouteExecutionContext execution)
    {
        var claimLostException = execution.DispatchHeartbeat!.CreateClaimLostException();
        if (execution.Candidate is null)
        {
            logger.LogWarning(
                claimLostException,
                "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch heartbeat was lost before candidate hydration completed.",
                execution.ProcessRunId,
                execution.DispatchClaim.StepRunId);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        logger.LogWarning(
            claimLostException,
            "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch heartbeat was lost.",
            execution.Candidate.Run.Id,
            execution.Candidate.StepRun.Id);
        return ProcessClaimedDispatchResult.DispatchComplete;
    }

    public ProcessClaimedDispatchResult HandleDispatchClaimLost(
        ProcessRouteExecutionContext execution,
        ProcessDispatchClaimLostException exception)
    {
        if (execution.Candidate is null)
        {
            logger.LogWarning(
                exception,
                "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch claim was lost before candidate hydration completed.",
                execution.ProcessRunId,
                execution.DispatchClaim.StepRunId);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        logger.LogWarning(
            exception,
            "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch claim was lost.",
            execution.Candidate.Run.Id,
            execution.Candidate.StepRun.Id);
        return ProcessClaimedDispatchResult.DispatchComplete;
    }

    public async Task<ProcessClaimedDispatchResult> HandleDispatchFailureAsync(
        ProcessRouteExecutionContext execution,
        Exception exception)
    {
        if (execution.DispatchHeartbeat?.ClaimLost == true)
        {
            return HandleDispatchHeartbeatClaimLost(execution);
        }

        if (execution.Candidate is null)
        {
            logger.LogError(
                exception,
                "Process automation dispatch failed for run {RunId}, step {StepRunId} before candidate hydration completed.",
                execution.ProcessRunId,
                execution.DispatchClaim.StepRunId);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        logger.LogError(
            exception,
            "Process automation dispatch failed for run {RunId}, step {StepRunId}.",
            execution.Candidate.Run.Id,
            execution.Candidate.StepRun.Id);

        if (execution.DispatchHeartbeat?.ClaimLost == true)
        {
            logger.LogWarning(
                execution.DispatchHeartbeat.CreateClaimLostException(),
                "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch heartbeat was lost.",
                execution.Candidate.Run.Id,
                execution.Candidate.StepRun.Id);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        if (await runClosureGuardService.IsRunClosedToAutomationAsync(
                execution.Candidate.Run.Id,
                execution.Candidate.StepRun.Id,
                execution.DispatchCancellationToken))
        {
            logger.LogInformation(
                "Skipping automation failure transition for run {RunId}, step {StepRunId} because the process run became terminal while agent execution was in flight.",
                execution.Candidate.Run.Id,
                execution.Candidate.StepRun.Id);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        var dispatcherClaim = new ProcessRunAutomationDispatchService.ProcessStepDispatchClaim(
            execution.DispatchClaim.StepRunId,
            execution.DispatchClaim.ClaimToken);
        if (!await isStepDispatchClaimHeldAsync(dispatcherClaim, execution.DispatchCancellationToken))
        {
            logger.LogWarning(
                "Skipping automation failure transition for run {RunId}, step {StepRunId} because the durable dispatch claim is no longer held.",
                execution.Candidate.Run.Id,
                execution.Candidate.StepRun.Id);
            return ProcessClaimedDispatchResult.DispatchComplete;
        }

        var failResult = await stepTransitionService.TransitionStepWithClaimAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = execution.Candidate.StepRun.Id,
                TargetStatus = ProcessStepRunStatus.Failed,
                Reason = $"AgentFramework execution failed: {exception.Message}",
                DecidedBy = ProcessRunAutomationDispatchService.AutomationActor,
                SuppressAutomationDispatch = true
            },
            execution.DispatchClaim,
            execution.DispatchCancellationToken);
        if (failResult.IsFailure)
        {
            logger.LogWarning(
                "Process step {StepRunId} could not be moved to Failed after an execution exception. Errors: {Errors}",
                execution.Candidate.StepRun.Id,
                string.Join(" | ", failResult.Errors.Select(error => error.Message)));
        }

        return ProcessClaimedDispatchResult.DispatchComplete;
    }
}
