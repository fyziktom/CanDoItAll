using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.Processes;

using DispatchCandidate = ProcessRunAutomationDispatchService.DispatchCandidate;
using ProcessStepDispatchClaim = ProcessRunAutomationDispatchService.ProcessStepDispatchClaim;

internal sealed class ProcessDispatchSubprocessRuntimeService(
    ProcessDispatchStepTransitionService stepTransitionService,
    ProcessDispatchFinalizerApplicationService finalizerApplicationService,
    IDbContextFactory<AppDbContext> dbContextFactory,
    IServiceScopeFactory serviceScopeFactory,
    IWorkspacePathResolver workspacePathResolver,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IClock clock,
    Func<ProcessStepDispatchClaim, CancellationToken, Task> ensureStepDispatchClaimHeldAsync,
    ILogger<ProcessRunAutomationDispatchService> logger)
{
    public async Task HandleSubprocessDispatchAsync(
        DispatchCandidate candidate,
        string trigger,
        Guid? triggerStepRunId,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var stepRunSnapshot = candidate.StepRun;
        if (stepRunSnapshot.Status != ProcessStepRunStatus.InProgress)
        {
            var startResult = await stepTransitionService.TransitionStepWithClaimAsync(
                ProcessSubprocessLifecycleRules.BuildStartTransitionRequest(
                    stepRunSnapshot,
                    ProcessRunAutomationDispatchService.NormalizeTrigger(trigger, triggerStepRunId),
                    ProcessRunAutomationDispatchService.AutomationActor),
                dispatchClaim,
                cancellationToken);
            if (startResult.IsFailure)
            {
                logger.LogInformation(
                    "Process subprocess step {StepRunId} could not be claimed on run {RunId}. Errors: {Errors}",
                    stepRunSnapshot.Id,
                    candidate.Run.Id,
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
                dispatchClaim,
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
                    dispatchClaim,
                    cancellationToken);
                if (blockResult.IsFailure)
                {
                    logger.LogWarning(
                        "Subprocess step {StepRunId} on run {RunId} could not be blocked after child run {SubprocessRunId} exposed capability gaps. Errors: {Errors}",
                        stepRunSnapshot.Id,
                        candidate.Run.Id,
                        subprocessRun.RunId,
                        string.Join(" | ", blockResult.Errors.Select(error => error.Message)));
                }

                return;
            }

            logger.LogInformation(
                "Subprocess step {StepRunId} on run {RunId} is observing child run {SubprocessRunId} with status {SubprocessStatus}.",
                stepRunSnapshot.Id,
                candidate.Run.Id,
                subprocessRun.RunId,
                subprocessRun.Status);
            return;
        }

        if (terminalStatus.Value == ProcessStepRunStatus.Completed)
        {
            await ProjectCompletedSubprocessArtifactsAsync(candidate, subprocessRun, dispatchClaim, cancellationToken);
            var transitionReason = ProcessSubprocessLifecycleRules.BuildParentTransitionReason(subprocessRun);
            await finalizerApplicationService.FinalizeSubprocessCompletionAsync(
                candidate,
                subprocessRun.RunId,
                terminalStatus.Value,
                transitionReason,
                dispatchClaim,
                cancellationToken);

            return;
        }

        var transitionResult = await stepTransitionService.TransitionStepWithClaimAsync(
            ProcessSubprocessLifecycleRules.BuildTerminalMirrorTransitionRequest(
                stepRunSnapshot,
                subprocessRun,
                terminalStatus.Value,
                ProcessRunAutomationDispatchService.AutomationActor),
            dispatchClaim,
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            logger.LogWarning(
                "Subprocess step {StepRunId} on run {RunId} could not mirror child run {SubprocessRunId}. Errors: {Errors}",
                stepRunSnapshot.Id,
                candidate.Run.Id,
                subprocessRun.RunId,
                string.Join(" | ", transitionResult.Errors.Select(error => error.Message)));
        }
    }

    private async Task ProjectCompletedSubprocessArtifactsAsync(
        DispatchCandidate candidate,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await ensureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var expectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item =>
                item.StepDefinitionId == candidate.StepRun.StepDefinitionId &&
                item.IsRequired)
            .OrderBy(item => item.Title)
            .ToListAsync(cancellationToken);
        if (expectations.Count == 0)
        {
            return;
        }

        var parentArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item =>
                item.ProcessRunId == candidate.Run.Id &&
                item.StepRunId == candidate.StepRun.Id)
            .ToListAsync(cancellationToken);
        var missingProjectableExpectations = expectations
            .Where(ProcessSubprocessArtifactSourceResolver.IsCompletionProjectionAllowed)
            .Where(expectation => !parentArtifacts.Any(artifact =>
                ProcessSubprocessProjectionPlanBuilder.SatisfiesCurrentArtifactExpectation(
                    artifact,
                    expectation,
                    subprocessRun.RunId)))
            .ToList();
        if (missingProjectableExpectations.Count == 0)
        {
            return;
        }

        var childArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == subprocessRun.RunId)
            .ToListAsync(cancellationToken);
        childArtifacts = childArtifacts
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        var now = clock.GetUtcNow();
        var scopedProfileId = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N");
        var gapJournalCoordinator = new ProcessSubprocessProjectionGapJournalCoordinator();
        var projectionWriterCoordinator = new ProcessSubprocessProjectionWriterCoordinator(workspacePathResolver);

        foreach (var expectation in missingProjectableExpectations)
        {
            await ensureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
            var sourceArtifact = ProcessSubprocessArtifactSourceResolver.ResolveSourceArtifact(
                childArtifacts,
                missingProjectableExpectations,
                expectation,
                out var projectionDiagnostic);
            if (sourceArtifact is null)
            {
                await gapJournalCoordinator.RecordAsync(
                    dbContext,
                    candidate,
                    subprocessRun,
                    expectation,
                    projectionDiagnostic,
                    now,
                    cancellationToken);
                continue;
            }

            var projectionPlan = ProcessSubprocessProjectionPlanBuilder.Build(
                candidate,
                subprocessRun,
                expectation,
                sourceArtifact,
                projectionDiagnostic,
                scopedProfileId);
            await projectionWriterCoordinator.WriteAsync(
                dbContext,
                candidate,
                subprocessRun,
                projectionPlan,
                now,
                cancellationToken);
        }

        await ensureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
