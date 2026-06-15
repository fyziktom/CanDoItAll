using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessDispatchSubprocessRuntimeService(
    ProcessDispatchStepTransitionService stepTransitionService,
    ProcessDispatchFinalizerApplicationService finalizerApplicationService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IServiceScopeFactory serviceScopeFactory,
    ProcessSubprocessProjectionPersistenceService projectionPersistenceService,
    ILogger<ProcessRunAutomationDispatchService> logger)
{
    public async Task HandleSubprocessDispatchAsync(
        ProcessDispatchSubprocessRuntimeInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);

        var stepRunSnapshot = input.StepRun;
        if (stepRunSnapshot.Status != ProcessStepRunStatus.InProgress)
        {
            var startResult = await stepTransitionService.TransitionStepWithClaimAsync(
                ProcessSubprocessLifecycleRules.BuildStartTransitionRequest(
                    stepRunSnapshot,
                    ProcessRunAutomationDispatchService.NormalizeTrigger(input.Trigger, input.TriggerStepRunId),
                    ProcessRunAutomationDispatchService.AutomationActor),
                input.DispatchClaim,
                cancellationToken);
            if (startResult.IsFailure)
            {
                logger.LogInformation(
                    "Process subprocess step {StepRunId} could not be claimed on run {RunId}. Errors: {Errors}",
                    stepRunSnapshot.Id,
                    input.Run.Id,
                    string.Join(" | ", startResult.Errors.Select(error => error.Message)));
                return;
            }
        }

        var subprocessResult = await new ProcessSubprocessRunObservationCoordinator(serviceScopeFactory)
            .EnsureRunForStepAsync(stepRunSnapshot.Id, cancellationToken);
        if (subprocessResult.IsFailure)
        {
            await stepTransitionService.TransitionStepWithClaimAsync(
                ProcessSubprocessLifecycleRules.BuildEnsureFailureBlockTransitionRequest(
                    stepRunSnapshot,
                    string.Join(" | ", subprocessResult.Errors.Select(error => error.Message)),
                    ProcessRunAutomationDispatchService.AutomationActor),
                input.DispatchClaim,
                cancellationToken);
            return;
        }

        var subprocessRun = subprocessResult.Value!;
        var terminalStatus = ProcessSubprocessLifecycleRules.ResolveParentStepStatus(subprocessRun.Status);
        if (!terminalStatus.HasValue)
        {
            var capabilityGapBlockReason = await new ProcessSubprocessCapabilityGapInspector(dbContextFactory)
                .TryBuildBlockReasonAsync(subprocessRun, cancellationToken);
            if (capabilityGapBlockReason is not null)
            {
                var blockResult = await stepTransitionService.TransitionStepWithClaimAsync(
                    ProcessSubprocessLifecycleRules.BuildCapabilityGapBlockTransitionRequest(
                        stepRunSnapshot,
                        capabilityGapBlockReason,
                        ProcessRunAutomationDispatchService.AutomationActor),
                    input.DispatchClaim,
                    cancellationToken);
                if (blockResult.IsFailure)
                {
                    logger.LogWarning(
                        "Subprocess step {StepRunId} on run {RunId} could not be blocked after child run {SubprocessRunId} exposed capability gaps. Errors: {Errors}",
                        stepRunSnapshot.Id,
                        input.Run.Id,
                        subprocessRun.RunId,
                        string.Join(" | ", blockResult.Errors.Select(error => error.Message)));
                }

                return;
            }

            logger.LogInformation(
                "Subprocess step {StepRunId} on run {RunId} is observing child run {SubprocessRunId} with status {SubprocessStatus}.",
                stepRunSnapshot.Id,
                input.Run.Id,
                subprocessRun.RunId,
                subprocessRun.Status);
            return;
        }

        if (terminalStatus.Value == ProcessStepRunStatus.Completed)
        {
            await projectionPersistenceService.ProjectCompletedArtifactsAsync(
                input,
                subprocessRun,
                cancellationToken);
            var transitionReason = ProcessSubprocessLifecycleRules.BuildParentTransitionReason(subprocessRun);

            await finalizerApplicationService.FinalizeSubprocessCompletionAsync(
                new ProcessDispatchSubprocessFinalizerInput(
                    input.Candidate,
                    subprocessRun.RunId,
                    terminalStatus.Value,
                    transitionReason,
                    input.DispatchClaim),
                cancellationToken);

            return;
        }

        var transitionResult = await stepTransitionService.TransitionStepWithClaimAsync(
            ProcessSubprocessLifecycleRules.BuildTerminalMirrorTransitionRequest(
                stepRunSnapshot,
                subprocessRun,
                terminalStatus.Value,
                ProcessRunAutomationDispatchService.AutomationActor),
            input.DispatchClaim,
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            logger.LogWarning(
                "Subprocess step {StepRunId} on run {RunId} could not mirror child run {SubprocessRunId}. Errors: {Errors}",
                stepRunSnapshot.Id,
                input.Run.Id,
                subprocessRun.RunId,
                string.Join(" | ", transitionResult.Errors.Select(error => error.Message)));
        }
    }
}
