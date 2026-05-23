using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections;
using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    public async Task DispatchAsync(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        Func<CancellationToken, Task>? renewLeaseAsync = null,
        CancellationToken cancellationToken = default)
    {
        if (processRunId == Guid.Empty)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            var initialCandidate = await LoadDispatchCandidateAsync(processRunId, cancellationToken);
            if (initialCandidate is null)
            {
                return;
            }

            var dispatchGuard = StepDispatchGuards.GetOrAdd(initialCandidate.StepRun.Id, static _ => new SemaphoreSlim(1, 1));
            await dispatchGuard.WaitAsync(cancellationToken);
            try
            {
                var candidate = await LoadDispatchCandidateAsync(processRunId, cancellationToken);
                if (candidate is null)
                {
                    return;
                }

                if (candidate.StepRun.Id != initialCandidate.StepRun.Id)
                {
                    continue;
                }

                if (ShouldSkipFreshAutomationDispatch(
                    candidate.StepRun.Status,
                    candidate.RecoveryExecutionRunId,
                    candidate.StepRun.StartedAtUtc,
                    clock.GetUtcNow(),
                    trigger))
                {
                    logger.LogInformation(
                        "Skipping recovery redispatch within the fresh-step grace period for run {RunId}, step {StepRunId}, status {Status}, trigger {Trigger}. Recovery worker will retry if the execution remains stranded.",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        candidate.StepRun.Status,
                        NormalizeTrigger(trigger, triggerStepRunId));
                    return;
                }

                if (candidate.StepRun.StepKind == ProcessStepKind.Subprocess)
                {
                    await HandleSubprocessDispatchAsync(candidate, trigger, triggerStepRunId, cancellationToken);
                    return;
                }

                var isWorkflowCandidate = candidate.TechnicalAgentId == Guid.Empty &&
                    candidate.StepRun.StepKind != ProcessStepKind.Subprocess;
                var databaseRequirementFailure = isWorkflowCandidate
                    ? null
                    : ResolveAutomationDatabaseRequirementFailure();
                if (databaseRequirementFailure is not null)
                {
                    await BlockDispatchForDatabaseRequirementAsync(candidate, databaseRequirementFailure, cancellationToken);
                    return;
                }

                if (await TryRequestMissingUpstreamArtifactMaterializationAsync(candidate, cancellationToken))
                {
                    return;
                }

                var strandedArtifactRecoveryOutcome = await TryRecoverStrandedMissingCompletionArtifactsAsync(
                    candidate,
                    trigger,
                    renewLeaseAsync,
                    cancellationToken);
                if (strandedArtifactRecoveryOutcome is not null)
                {
                    var recoveryStepRunSnapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken)
                        ?? throw new InvalidOperationException($"Process step run {candidate.StepRun.Id} could not be reloaded after manager artifact recovery.");
                    if (ShouldSkipAutomationCompletionTransition(recoveryStepRunSnapshot.Status, strandedArtifactRecoveryOutcome.CompletionStatus))
                    {
                        logger.LogInformation(
                            "Skipping stale process manager artifact recovery transition for run {RunId}, step {StepRunId}. Current status is {CurrentStatus}, requested status is {RequestedStatus}.",
                            candidate.Run.Id,
                            candidate.StepRun.Id,
                            recoveryStepRunSnapshot.Status,
                            strandedArtifactRecoveryOutcome.CompletionStatus);
                        return;
                    }

                    var recoveryTransitionResult = await TransitionStepAsync(
                        new ProcessStepTransitionRequest
                        {
                            StepRunId = candidate.StepRun.Id,
                            StepRunConcurrencyToken = recoveryStepRunSnapshot.ConcurrencyToken,
                            TargetStatus = strandedArtifactRecoveryOutcome.CompletionStatus,
                            Reason = strandedArtifactRecoveryOutcome.CompletionReason,
                            SelectedBranchOutcomeId = strandedArtifactRecoveryOutcome.SelectedBranchOutcomeId,
                            DecidedBy = AutomationActor,
                            SuppressAutomationDispatch = strandedArtifactRecoveryOutcome.CompletionStatus != ProcessStepRunStatus.Completed
                        },
                        cancellationToken);
                    if (recoveryTransitionResult.IsFailure)
                    {
                        throw new InvalidOperationException(string.Join(" | ", recoveryTransitionResult.Errors.Select(error => error.Message)));
                    }

                    return;
                }

                if (candidate.StepRun.Status != ProcessStepRunStatus.InProgress)
                {
                    var startResult = await TransitionStepAsync(
                        new ProcessStepTransitionRequest
                        {
                            StepRunId = candidate.StepRun.Id,
                            StepRunConcurrencyToken = candidate.StepRun.ConcurrencyToken,
                            TargetStatus = ProcessStepRunStatus.InProgress,
                            Reason = $"Started by the durable process automation dispatcher ({NormalizeTrigger(trigger, triggerStepRunId)}).",
                            DecidedBy = AutomationActor,
                            SuppressAutomationDispatch = true
                        },
                        cancellationToken);
                    if (startResult.IsFailure)
                    {
                        logger.LogInformation(
                            "Process step {StepRunId} could not be claimed for automation dispatch on run {RunId}. Errors: {Errors}",
                            candidate.StepRun.Id,
                            processRunId,
                            string.Join(" | ", startResult.Errors.Select(error => error.Message)));
                        var refreshedCandidate = await LoadDispatchCandidateAsync(processRunId, cancellationToken);
                        if (refreshedCandidate is null ||
                            refreshedCandidate.StepRun.Id != candidate.StepRun.Id ||
                            refreshedCandidate.StepRun.Status != ProcessStepRunStatus.InProgress)
                        {
                            continue;
                        }

                        logger.LogInformation(
                            "Continuing process automation dispatch for run {RunId}, step {StepRunId} after reload confirmed the step is already InProgress.",
                            refreshedCandidate.Run.Id,
                            refreshedCandidate.StepRun.Id);
                        candidate = refreshedCandidate;
                    }
                }

                try
                {
                    var workflowOutcome = await workflowRunCoordinator.TryRunOrObserveAsync(
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        NormalizeTrigger(trigger, triggerStepRunId),
                        cancellationToken);
                    if (workflowOutcome.Handled)
                    {
                        await HandleWorkflowExecutionOutcomeAsync(candidate, workflowOutcome, cancellationToken);
                        return;
                    }

                    var executionOutcome = await ExecuteUntilSettledAsync(candidate, trigger, renewLeaseAsync, cancellationToken);
                    var competingExecution = executionOutcome.CompletionStatus is not ProcessStepRunStatus.Completed
                        ? await ResolveCompetingActiveAutomationExecutionAsync(candidate, executionOutcome, cancellationToken)
                        : null;
                    if (competingExecution is not null)
                    {
                        logger.LogInformation(
                            "Skipping non-successful automation completion transition for run {RunId}, step {StepRunId}, execution run {ExecutionRunId} because execution run {CompetingExecutionRunId} is still active for the same process step.",
                            candidate.Run.Id,
                            candidate.StepRun.Id,
                            executionOutcome.Detail.Run.Id,
                            competingExecution.Id);
                        return;
                    }

                    if (await IsRunClosedToAutomationAsync(candidate.Run.Id, candidate.StepRun.Id, cancellationToken))
                    {
                        logger.LogInformation(
                            "Skipping automation completion projection for run {RunId}, step {StepRunId} because the process run became terminal while agent execution was in flight.",
                            candidate.Run.Id,
                            candidate.StepRun.Id);
                        return;
                    }

                    var stepRunSnapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken)
                        ?? throw new InvalidOperationException($"Process step run {candidate.StepRun.Id} could not be reloaded before completion.");
                    if (ShouldSkipAutomationCompletionTransition(stepRunSnapshot.Status, executionOutcome.CompletionStatus))
                    {
                        logger.LogInformation(
                            "Skipping stale process automation completion transition for run {RunId}, step {StepRunId}. Current status is {CurrentStatus}, requested status is {RequestedStatus}.",
                            candidate.Run.Id,
                            candidate.StepRun.Id,
                            stepRunSnapshot.Status,
                            executionOutcome.CompletionStatus);
                    }
                    else
                    {
                        await ProjectExecutionArtifactsAsync(
                            candidate,
                            executionOutcome.Detail,
                            executionOutcome.ResponseText,
                            executionOutcome.CompletionStatus,
                            cancellationToken);
                        executionOutcome = await TryRecoverMissingCompletionArtifactsAsync(
                            candidate,
                            executionOutcome,
                            trigger,
                            renewLeaseAsync,
                            cancellationToken);

                        var completionResult = await TransitionStepAsync(
                            new ProcessStepTransitionRequest
                            {
                                StepRunId = candidate.StepRun.Id,
                                StepRunConcurrencyToken = stepRunSnapshot.ConcurrencyToken,
                                TargetStatus = executionOutcome.CompletionStatus,
                                Reason = executionOutcome.CompletionReason,
                                SelectedBranchOutcomeId = executionOutcome.SelectedBranchOutcomeId,
                                DecidedBy = AutomationActor,
                                SuppressAutomationDispatch = executionOutcome.CompletionStatus != ProcessStepRunStatus.Completed
                            },
                            cancellationToken);
                        if (completionResult.IsFailure)
                        {
                            var refreshedSnapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken);
                            if (refreshedSnapshot is not null &&
                                ShouldSkipAutomationCompletionTransition(refreshedSnapshot.Status, executionOutcome.CompletionStatus))
                            {
                                logger.LogInformation(
                                    "Skipping stale process automation completion transition after a failed attempt for run {RunId}, step {StepRunId}. Current status is {CurrentStatus}, requested status is {RequestedStatus}.",
                                    candidate.Run.Id,
                                    candidate.StepRun.Id,
                                    refreshedSnapshot.Status,
                                    executionOutcome.CompletionStatus);
                            }
                            else
                            {
                                throw new InvalidOperationException(string.Join(" | ", completionResult.Errors.Select(error => error.Message)));
                            }
                        }
                    }

                    return;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Process automation dispatch failed for run {RunId}, step {StepRunId}.",
                        candidate.Run.Id,
                        candidate.StepRun.Id);

                    if (await IsRunClosedToAutomationAsync(candidate.Run.Id, candidate.StepRun.Id, cancellationToken))
                    {
                        logger.LogInformation(
                            "Skipping automation failure transition for run {RunId}, step {StepRunId} because the process run became terminal while agent execution was in flight.",
                            candidate.Run.Id,
                            candidate.StepRun.Id);
                        return;
                    }

                    var failResult = await TransitionStepAsync(
                        new ProcessStepTransitionRequest
                        {
                            StepRunId = candidate.StepRun.Id,
                            TargetStatus = ProcessStepRunStatus.Failed,
                            Reason = $"AgentFramework execution failed: {exception.Message}",
                            DecidedBy = AutomationActor,
                            SuppressAutomationDispatch = true
                        },
                        cancellationToken);
                    if (failResult.IsFailure)
                    {
                        logger.LogWarning(
                            "Process step {StepRunId} could not be moved to Failed after an execution exception. Errors: {Errors}",
                            candidate.StepRun.Id,
                            string.Join(" | ", failResult.Errors.Select(error => error.Message)));
                    }

                    return;
                }
            }
            finally
            {
                dispatchGuard.Release();
            }
        }
    }

    private async Task HandleWorkflowExecutionOutcomeAsync(
        DispatchCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        CancellationToken cancellationToken)
    {
        var stepRunSnapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken)
            ?? throw new InvalidOperationException($"Process step run {candidate.StepRun.Id} could not be reloaded after workflow execution.");
        if (workflowOutcome.CompletionStatus == ProcessStepRunStatus.InProgress ||
            stepRunSnapshot.Status == workflowOutcome.CompletionStatus)
        {
            logger.LogInformation(
                "Workflow execution for process run {RunId}, step {StepRunId} is {Status}.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                workflowOutcome.CompletionStatus);
            return;
        }

        if (ShouldSkipAutomationCompletionTransition(stepRunSnapshot.Status, workflowOutcome.CompletionStatus))
        {
            logger.LogInformation(
                "Skipping stale workflow completion transition for run {RunId}, step {StepRunId}. Current status is {CurrentStatus}, requested status is {RequestedStatus}.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                stepRunSnapshot.Status,
                workflowOutcome.CompletionStatus);
            return;
        }

        var transitionResult = await TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = candidate.StepRun.Id,
                StepRunConcurrencyToken = stepRunSnapshot.ConcurrencyToken,
                TargetStatus = workflowOutcome.CompletionStatus,
                Reason = workflowOutcome.CompletionReason,
                DecidedBy = AutomationActor,
                SuppressAutomationDispatch = workflowOutcome.CompletionStatus != ProcessStepRunStatus.Completed
            },
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            throw new InvalidOperationException(string.Join(" | ", transitionResult.Errors.Select(error => error.Message)));
        }
    }

    private async Task<bool> IsRunClosedToAutomationAsync(
        Guid processRunId,
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var state = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(run => run.Id == processRunId)
            .Join(
                dbContext.Set<ProcessStepRun>().AsNoTracking().Where(stepRun => stepRun.Id == stepRunId),
                run => run.Id,
                stepRun => stepRun.ProcessRunId,
                (run, stepRun) => new
                {
                    RunStatus = (ProcessRunStatus?)run.Status,
                    StepStatus = (ProcessStepRunStatus?)stepRun.Status
                })
            .SingleOrDefaultAsync(cancellationToken);

        return state is null || IsRunClosedToAutomation(state.RunStatus, state.StepStatus);
    }

    internal static bool IsRunClosedToAutomation(
        ProcessRunStatus? runStatus,
        ProcessStepRunStatus? stepStatus)
    {
        return runStatus is null or ProcessRunStatus.Completed or ProcessRunStatus.Cancelled ||
            runStatus == ProcessRunStatus.Failed && stepStatus != ProcessStepRunStatus.InProgress;
    }

    internal static bool IsRunEligibleForDispatchCandidate(ProcessRunStatus? runStatus)
    {
        return runStatus is not null and not ProcessRunStatus.Completed and not ProcessRunStatus.Cancelled;
    }

    internal static bool IsStepStatusDispatchableForRun(
        ProcessRunStatus runStatus,
        ProcessStepRunStatus stepStatus)
    {
        return runStatus == ProcessRunStatus.Failed
            ? stepStatus == ProcessStepRunStatus.InProgress
            : stepStatus is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.InProgress;
    }

    private ProcessAutomationDatabaseRequirementFailure? ResolveAutomationDatabaseRequirementFailure()
    {
        if (!processRuntimeOptions.Value.RequirePostgreSqlForAgentAutomation)
        {
            return null;
        }

        var profile = databaseProfileRuntimeAccessor.ResolveCurrentProfile();
        if (profile.Profile.ProviderKind == DatabaseProviderKind.PostgreSql)
        {
            return null;
        }

        return new ProcessAutomationDatabaseRequirementFailure(
            $"Governed process automation requires PostgreSQL, but the active database profile is '{profile.Profile.DisplayName}' ({profile.Profile.Id:D}, provider {profile.Profile.ProviderKind}, source {profile.Profile.SourceKind}, resolved by {profile.ResolutionSource}). Switch the active database profile to PostgreSQL before rerunning automation.");
    }

    private async Task HandleSubprocessDispatchAsync(
        DispatchCandidate candidate,
        string trigger,
        Guid? triggerStepRunId,
        CancellationToken cancellationToken)
    {
        var stepRunSnapshot = candidate.StepRun;
        if (stepRunSnapshot.Status != ProcessStepRunStatus.InProgress)
        {
            var startResult = await TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRunSnapshot.Id,
                    StepRunConcurrencyToken = stepRunSnapshot.ConcurrencyToken,
                    TargetStatus = ProcessStepRunStatus.InProgress,
                    Reason = $"Started subprocess by the durable process automation dispatcher ({NormalizeTrigger(trigger, triggerStepRunId)}).",
                    DecidedBy = AutomationActor,
                    SuppressAutomationDispatch = true
                },
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

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var subprocessResult = await processesService.EnsureSubprocessRunForStepAsync(stepRunSnapshot.Id, cancellationToken);
        if (subprocessResult.IsFailure)
        {
            await TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRunSnapshot.Id,
                    TargetStatus = ProcessStepRunStatus.Blocked,
                    Reason = string.Join(" | ", subprocessResult.Errors.Select(error => error.Message)),
                    DecidedBy = AutomationActor,
                    SuppressAutomationDispatch = true
                },
                cancellationToken);
            return;
        }

        var subprocessRun = subprocessResult.Value!;
        var terminalStatus = ResolveSubprocessParentStepStatus(subprocessRun.Status);
        if (!terminalStatus.HasValue)
        {
            var capabilityGapBlockReason = await TryBuildSubprocessCapabilityGapBlockReasonAsync(
                subprocessRun,
                cancellationToken);
            if (capabilityGapBlockReason is not null)
            {
                var blockResult = await TransitionStepAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = stepRunSnapshot.Id,
                        TargetStatus = ProcessStepRunStatus.Blocked,
                        Reason = capabilityGapBlockReason,
                        DecidedBy = AutomationActor,
                        SuppressAutomationDispatch = true
                    },
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

        if (terminalStatus.Value == ProcessStepRunStatus.Completed) {
            await ProjectCompletedSubprocessArtifactsAsync(candidate, subprocessRun, cancellationToken);
        }

        var transitionResult = await TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRunSnapshot.Id,
                TargetStatus = terminalStatus.Value,
                Reason = BuildSubprocessParentTransitionReason(subprocessRun),
                DecidedBy = AutomationActor,
                SuppressAutomationDispatch = terminalStatus.Value != ProcessStepRunStatus.Completed
            },
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

    private async Task<string?> TryBuildSubprocessCapabilityGapBlockReasonAsync(
        ProcessSubprocessRunStartResult subprocessRun,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var activeChildSteps = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessRunId == subprocessRun.RunId &&
                (item.Status == ProcessStepRunStatus.Ready ||
                 item.Status == ProcessStepRunStatus.WaitingApproval ||
                 item.Status == ProcessStepRunStatus.InProgress))
            .OrderBy(item => item.Sequence)
            .Select(item => new SubprocessCapabilityGapStep(
                item.Title,
                item.Status,
                item.CapabilityGapSeverity,
                item.CurrentExecutorPartyId,
                item.CurrentExecutorName))
            .ToListAsync(cancellationToken);
        if (activeChildSteps.Count == 0)
        {
            return null;
        }

        var executableChildStepExists = activeChildSteps.Any(item =>
            item.CapabilityGapSeverity == ProcessCapabilityGapSeverity.None &&
            item.CurrentExecutorPartyId.HasValue);
        if (executableChildStepExists)
        {
            return null;
        }

        var blockingSteps = activeChildSteps
            .Where(item =>
                item.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None ||
                !item.CurrentExecutorPartyId.HasValue)
            .Take(3)
            .Select(BuildSubprocessCapabilityGapStepSummary)
            .ToList();
        if (blockingSteps.Count == 0)
        {
            return null;
        }

        var additionalCount = activeChildSteps.Count - blockingSteps.Count;
        var additionalSummary = additionalCount <= 0
            ? string.Empty
            : $" and {additionalCount} more active child step(s)";

        return $"Subprocess run '{subprocessRun.RunName}' cannot proceed because active child step(s) have unresolved required role assignments or capability gaps: {string.Join("; ", blockingSteps)}{additionalSummary}. Resolve the subprocess role assignments or rerun with a launch plan that binds the required roles.";
    }

    private static string BuildSubprocessCapabilityGapStepSummary(SubprocessCapabilityGapStep step)
    {
        var executorName = string.IsNullOrWhiteSpace(step.CurrentExecutorName)
            ? "unassigned"
            : step.CurrentExecutorName.Trim();

        return $"'{step.Title}' is {step.Status} for executor '{executorName}' ({step.CapabilityGapSeverity})";
    }

    private async Task ProjectCompletedSubprocessArtifactsAsync(
        DispatchCandidate candidate,
        ProcessSubprocessRunStartResult subprocessRun,
        CancellationToken cancellationToken) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var expectations = await dbContext.Set<ProcessArtifactExpectation>()
            .Where(item =>
                item.StepDefinitionId == candidate.StepRun.StepDefinitionId &&
                item.IsRequired)
            .OrderBy(item => item.Title)
            .ToListAsync(cancellationToken);
        if (expectations.Count == 0) {
            return;
        }

        var parentArtifacts = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item =>
                item.ProcessRunId == candidate.Run.Id &&
                item.StepRunId == candidate.StepRun.Id)
            .ToListAsync(cancellationToken);
        var missingProjectableExpectations = expectations
            .Where(IsSubprocessCompletionProjectionAllowed)
            .Where(expectation => !parentArtifacts.Any(artifact => SatisfiesArtifactExpectation(artifact, expectation)))
            .ToList();
        if (missingProjectableExpectations.Count == 0) {
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

        foreach (var expectation in missingProjectableExpectations) {
            var sourceArtifact = ResolveSubprocessSourceArtifact(childArtifacts, expectation);
            var artifact = new ProcessArtifactRecord {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = expectation.ArtifactKind,
                Title = expectation.Title,
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ResolveProjectedSubprocessSensitivity(expectation, sourceArtifact),
                ProvenanceSummary = BuildSubprocessArtifactProjectionProvenance(candidate, subprocessRun, sourceArtifact),
                AllowedFutureUsageSummary = expectation.AllowedFutureUsageSummary,
                ReviewSummary = BuildSubprocessArtifactProjectionReviewSummary(subprocessRun, sourceArtifact),
                ManagedStoragePath = BoundProjectedSubprocessStoragePath(sourceArtifact?.ManagedStoragePath ?? string.Empty),
                ExternalReferenceKey = BuildSubprocessArtifactProjectionReferenceKey(subprocessRun.RunId, expectation.Id),
                CreatedAtUtc = now
            };
            await dbContext.Set<ProcessArtifactRecord>().AddAsync(artifact, cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    EventType = "artifact-recorded",
                    Title = "Recorded process artifact",
                    Description = artifact.Title,
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    OperatingMode = candidate.Run.OperatingMode,
                    PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                    EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                    ReplayContextJson = JsonSerializer.Serialize(new {
                        RunId = candidate.Run.Id,
                        StepRunId = candidate.StepRun.Id,
                        SubprocessRunId = subprocessRun.RunId,
                        SourceArtifactId = sourceArtifact?.Id,
                        Summary = artifact.ProvenanceSummary
                    }),
                    OccurredAtUtc = now
                },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsSubprocessCompletionProjectionAllowed(ProcessArtifactExpectation expectation) {
        return expectation.TrustRequirement is
            ProcessArtifactTrustRequirement.None or
            ProcessArtifactTrustRequirement.ReviewRequired;
    }

    private static ProcessArtifactRecord? ResolveSubprocessSourceArtifact(
        IReadOnlyList<ProcessArtifactRecord> childArtifacts,
        ProcessArtifactExpectation expectation) {
        return childArtifacts
            .Where(artifact =>
                artifact.ArtifactKind == expectation.ArtifactKind &&
                artifact.SensitivityLevel >= expectation.SensitivityLevel &&
                SatisfiesTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement))
            .OrderByDescending(artifact => artifact.ArtifactExpectationId.HasValue)
            .ThenByDescending(artifact => string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(artifact => artifact.CreatedAtUtc)
            .FirstOrDefault();
    }

    private static bool SatisfiesArtifactExpectation(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation) {
        if (artifact.ArtifactKind != expectation.ArtifactKind) {
            return false;
        }

        if (artifact.SensitivityLevel < expectation.SensitivityLevel) {
            return false;
        }

        if (!SatisfiesTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement)) {
            return false;
        }

        return artifact.ArtifactExpectationId.HasValue
            ? artifact.ArtifactExpectationId.Value == expectation.Id
            : string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SatisfiesTrustRequirement(
        ProcessArtifactTrustStatus trustStatus,
        ProcessArtifactTrustRequirement trustRequirement) {
        return trustRequirement switch {
            ProcessArtifactTrustRequirement.None => true,
            ProcessArtifactTrustRequirement.ReviewRequired => trustStatus is
                ProcessArtifactTrustStatus.ReviewRequired or
                ProcessArtifactTrustStatus.Approved or
                ProcessArtifactTrustStatus.TrustedSource,
            ProcessArtifactTrustRequirement.HumanApproved => trustStatus == ProcessArtifactTrustStatus.Approved,
            ProcessArtifactTrustRequirement.TrustedSource => trustStatus == ProcessArtifactTrustStatus.TrustedSource,
            _ => false
        };
    }

    private static ProcessSensitivityLevel ResolveProjectedSubprocessSensitivity(
        ProcessArtifactExpectation expectation,
        ProcessArtifactRecord? sourceArtifact) {
        if (sourceArtifact is null || sourceArtifact.SensitivityLevel < expectation.SensitivityLevel) {
            return expectation.SensitivityLevel;
        }

        return sourceArtifact.SensitivityLevel;
    }

    private static string BuildSubprocessArtifactProjectionProvenance(
        DispatchCandidate candidate,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactRecord? sourceArtifact) {
        var sourceSummary = sourceArtifact is null
            ? "No child artifact with the same kind was available; inspect the child run ledger for detailed evidence."
            : $"Source subprocess artifact '{sourceArtifact.Title}' ({sourceArtifact.Id:D}).";
        return $"Auto-projected from completed subprocess run '{subprocessRun.RunName}' ({subprocessRun.RunId:D}) for parent subprocess step '{candidate.StepRun.Title}'. {sourceSummary}";
    }

    private static string BuildSubprocessArtifactProjectionReviewSummary(
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactRecord? sourceArtifact) {
        if (sourceArtifact is null) {
            return $"Subprocess run '{subprocessRun.RunName}' completed. Review the child run artifact ledger before reusing this parent evidence outside the process.";
        }

        return string.IsNullOrWhiteSpace(sourceArtifact.ReviewSummary)
            ? $"Subprocess run '{subprocessRun.RunName}' completed. Source artifact: {sourceArtifact.Title}."
            : $"Subprocess run '{subprocessRun.RunName}' completed. Source artifact: {sourceArtifact.Title}. {sourceArtifact.ReviewSummary}";
    }

    private static string BoundProjectedSubprocessStoragePath(string value) {
        const int maxManagedStoragePathLength = 500;
        var normalized = value.Trim();
        return normalized.Length <= maxManagedStoragePathLength
            ? normalized
            : normalized[..maxManagedStoragePathLength];
    }

    private static string BuildSubprocessArtifactProjectionReferenceKey(Guid subprocessRunId, Guid expectationId) {
        return $"subprocess-run:{subprocessRunId:D}:artifact:{expectationId:D}";
    }

    private static ProcessStepRunStatus? ResolveSubprocessParentStepStatus(ProcessRunStatus subprocessStatus)
    {
        return subprocessStatus switch
        {
            ProcessRunStatus.Completed => ProcessStepRunStatus.Completed,
            ProcessRunStatus.Blocked => ProcessStepRunStatus.Blocked,
            ProcessRunStatus.Cancelled or ProcessRunStatus.Failed => ProcessStepRunStatus.Failed,
            _ => null
        };
    }

    private static string BuildSubprocessParentTransitionReason(ProcessSubprocessRunStartResult subprocessRun)
    {
        return subprocessRun.Status switch
        {
            ProcessRunStatus.Completed => $"Subprocess run '{subprocessRun.RunName}' completed.",
            ProcessRunStatus.Blocked => $"Subprocess run '{subprocessRun.RunName}' is blocked.",
            ProcessRunStatus.Cancelled => $"Subprocess run '{subprocessRun.RunName}' was cancelled.",
            ProcessRunStatus.Failed => $"Subprocess run '{subprocessRun.RunName}' failed.",
            _ => $"Subprocess run '{subprocessRun.RunName}' is {subprocessRun.Status}."
        };
    }

    private async Task BlockDispatchForDatabaseRequirementAsync(
        DispatchCandidate candidate,
        ProcessAutomationDatabaseRequirementFailure failure,
        CancellationToken cancellationToken)
    {
        var targetStatus = candidate.StepRun.Status switch
        {
            ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.Blocked => ProcessStepRunStatus.Blocked,
            ProcessStepRunStatus.InProgress or ProcessStepRunStatus.Failed => ProcessStepRunStatus.Failed,
            _ => candidate.StepRun.Status
        };

        if (targetStatus == candidate.StepRun.Status &&
            targetStatus is not ProcessStepRunStatus.Blocked and not ProcessStepRunStatus.Failed)
        {
            logger.LogWarning(
                "Process automation dispatch for run {RunId}, step {StepRunId} requires PostgreSQL but current status {Status} has no supported blocking transition. Reason: {Reason}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                candidate.StepRun.Status,
                failure.Message);
            return;
        }

        if (!ProcessStepRunTransitions.IsAllowed(candidate.StepRun.Status, targetStatus))
        {
            logger.LogWarning(
                "Process automation dispatch for run {RunId}, step {StepRunId} requires PostgreSQL but current status {Status} cannot transition to {TargetStatus}. Reason: {Reason}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                candidate.StepRun.Status,
                targetStatus,
                failure.Message);
            return;
        }

        var transitionResult = await TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = candidate.StepRun.Id,
                StepRunConcurrencyToken = candidate.StepRun.ConcurrencyToken,
                TargetStatus = targetStatus,
                Reason = failure.Message,
                DecidedBy = AutomationActor,
                SuppressAutomationDispatch = true
            },
            cancellationToken);

        if (transitionResult.IsFailure)
        {
            logger.LogWarning(
                "Process step {StepRunId} could not be moved to {TargetStatus} after PostgreSQL runtime requirement failed. Errors: {Errors}",
                candidate.StepRun.Id,
                targetStatus,
                string.Join(" | ", transitionResult.Errors.Select(error => error.Message)));
            return;
        }

        logger.LogWarning(
            "Blocked process automation dispatch for run {RunId}, step {StepRunId} because the active database profile is not PostgreSQL.",
            candidate.Run.Id,
            candidate.StepRun.Id);
    }

    private async Task<bool> TryRequestMissingUpstreamArtifactMaterializationAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        var missingInputs = ResolveMissingUpstreamArtifactInputs(candidate);
        if (missingInputs.Count == 0)
        {
            return false;
        }

        var materializationTarget = missingInputs.FirstOrDefault(IsRunnableUpstreamArtifactMaterializationTarget);
        var blockReason = BuildMissingUpstreamArtifactMaterializationBlockReason(candidate, missingInputs, materializationTarget);
        if (candidate.StepRun.Status != ProcessStepRunStatus.Blocked)
        {
            var snapshot = await LoadStepRunTransitionSnapshotAsync(candidate.StepRun.Id, cancellationToken);
            if (snapshot is not null &&
                snapshot.Status is ProcessStepRunStatus.Ready or ProcessStepRunStatus.WaitingApproval or ProcessStepRunStatus.InProgress)
            {
                var blockResult = await TransitionStepAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = candidate.StepRun.Id,
                        StepRunConcurrencyToken = snapshot.ConcurrencyToken,
                        TargetStatus = ProcessStepRunStatus.Blocked,
                        Reason = blockReason,
                        DecidedBy = AutomationActor,
                        SuppressAutomationDispatch = true
                    },
                    cancellationToken);
                if (blockResult.IsFailure)
                {
                    logger.LogWarning(
                        "Could not block downstream step {StepRunId} before upstream artifact materialization for run {RunId}. Errors: {Errors}",
                        candidate.StepRun.Id,
                        candidate.Run.Id,
                        string.Join(" | ", blockResult.Errors.Select(error => error.Message)));
                    return true;
                }
            }
        }

        if (materializationTarget is null)
        {
            logger.LogWarning(
                "Process run {RunId}, step {StepRunId} is missing required upstream artifacts, but no completed, blocked, or failed agent-owned source step is available for automatic materialization. Missing inputs: {MissingInputs}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                string.Join(" | ", missingInputs.Select(input => $"{input.SourceStepTitle}: {input.ExpectedArtifactTitle}")));
            return true;
        }

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var rerunResult = await processesService.RerunAgentStepAsync(
            new ProcessAgentStepRerunRequest
            {
                StepRunId = materializationTarget.SourceStepRunId!.Value,
                StepRunConcurrencyToken = materializationTarget.SourceStepRunConcurrencyToken,
                OperatorReason = BuildUpstreamArtifactMaterializationDirective(candidate, missingInputs, materializationTarget)
            },
            cancellationToken);
        if (rerunResult.IsFailure)
        {
            logger.LogWarning(
                "Could not request upstream artifact materialization from step {SourceStepRunId} for run {RunId}, blocked downstream step {StepRunId}. Errors: {Errors}",
                materializationTarget.SourceStepRunId,
                candidate.Run.Id,
                candidate.StepRun.Id,
                string.Join(" | ", rerunResult.Errors.Select(error => error.Message)));
            return true;
        }

        logger.LogInformation(
            "Requested upstream artifact materialization from step {SourceStepRunId} for blocked downstream step {StepRunId} on process run {RunId}. Missing artifact: {ExpectedArtifactTitle}",
            materializationTarget.SourceStepRunId,
            candidate.StepRun.Id,
            candidate.Run.Id,
            materializationTarget.ExpectedArtifactTitle);
        return true;
    }

    private static IReadOnlyList<DispatchArtifactInput> ResolveMissingUpstreamArtifactInputs(DispatchCandidate candidate)
    {
        return candidate.ArtifactInputs
            .Where(input => input.Artifacts.Count == 0)
            .ToList();
    }

    private static bool IsRunnableUpstreamArtifactMaterializationTarget(DispatchArtifactInput input)
    {
        return input.SourceStepRunId.HasValue &&
               input.SourceStepRunConcurrencyToken.HasValue &&
               input.SourceStepHasAgentExecutor &&
               input.SourceStepRunStatus is ProcessStepRunStatus.Completed or ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed;
    }

    private static string BuildMissingUpstreamArtifactMaterializationBlockReason(
        DispatchCandidate candidate,
        IReadOnlyList<DispatchArtifactInput> missingInputs,
        DispatchArtifactInput? materializationTarget)
    {
        var missingSummary = string.Join(
            "; ",
            missingInputs
                .Take(3)
                .Select(input => $"upstream step '{input.SourceStepTitle}' must provide required artifact '{input.ExpectedArtifactTitle}'"));
        var targetSummary = materializationTarget is null
            ? "No eligible agent-owned upstream step is available for automatic materialization."
            : $"Automation requested upstream artifact materialization from '{materializationTarget.SourceStepTitle}' before retrying this step.";
        return $"Cannot dispatch '{candidate.StepRun.Title}' because required upstream artifacts are missing: {missingSummary}. {targetSummary}";
    }

    private static string BuildUpstreamArtifactMaterializationDirective(
        DispatchCandidate candidate,
        IReadOnlyList<DispatchArtifactInput> missingInputs,
        DispatchArtifactInput materializationTarget)
    {
        var targetMissingInputs = missingInputs
            .Where(input => input.SourceStepRunId == materializationTarget.SourceStepRunId)
            .ToList();
        var artifactTitles = targetMissingInputs.Count == 0
            ? materializationTarget.ExpectedArtifactTitle
            : string.Join(", ", targetMissingInputs.Select(input => input.ExpectedArtifactTitle).Distinct(StringComparer.OrdinalIgnoreCase));
        return $"Automatic upstream artifact materialization requested. Downstream step '{candidate.StepRun.Title}' cannot proceed because required upstream artifact(s) are missing: {artifactTitles}. Use this step's existing records, artifacts, decisions, and prior execution context to create or repair only the missing required artifact(s). Do not redo unrelated work. When the artifact(s) are recorded, the downstream step will retry from its configured artifact inputs.";
    }

    private async Task<DispatchCandidate?> LoadDispatchCandidateAsync(
        Guid processRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == processRunId, cancellationToken);
        if (run is null || !IsRunEligibleForDispatchCandidate(run.Status))
        {
            return null;
        }

        var definition = await dbContext.Set<ProcessDefinition>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == run.ProcessDefinitionId, cancellationToken);
        var dispatchableSteps = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId &&
                (item.Status == ProcessStepRunStatus.Ready ||
                 item.Status == ProcessStepRunStatus.WaitingApproval ||
                 item.Status == ProcessStepRunStatus.InProgress))
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        dispatchableSteps = dispatchableSteps
            .Where(item => IsStepStatusDispatchableForRun(run.Status, item.Status))
            .ToList();
        if (dispatchableSteps.Count == 0)
        {
            return null;
        }

        var stepRunIds = dispatchableSteps.Select(item => item.Id).ToList();
        var workBriefsByStepRunId = (await dbContext.Set<ProcessWorkBrief>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == processRunId && item.StepRunId.HasValue && stepRunIds.Contains(item.StepRunId.Value))
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .GroupBy(item => item.StepRunId!.Value)
            .ToDictionary(group => group.Key, group => group.First());
        var allStepRuns = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId)
            .ToListAsync(cancellationToken);
        var stepRunsByDefinitionId = allStepRuns
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepRun>)group
                    .OrderByDescending(item => item.Sequence)
                    .ToList());
        var existingArtifacts = (await dbContext.Set<ProcessArtifactRecord>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == processRunId)
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
        var externalReferenceKeys = existingArtifacts
            .Select(item => item.ExternalReferenceKey)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var readyStepDefinitionIds = dispatchableSteps
            .Select(item => item.StepDefinitionId)
            .Distinct()
            .ToList();
        var readyStepDefinitionsById = readyStepDefinitionIds.Count == 0
            ? new Dictionary<Guid, ProcessStepDefinition>()
            : await dbContext.Set<ProcessStepDefinition>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var stepRoleRequirements = readyStepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.FallbackOrder)
                .ToListAsync(cancellationToken);
        var stepRoleRequirementsByStepDefinitionId = stepRoleRequirements
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepRoleAssignmentRequirement>)group.ToList());
        var roleRequirementIds = stepRoleRequirements
            .Select(item => item.RoleRequirementId)
            .Distinct()
            .ToList();
        var roleRequirementsById = roleRequirementIds.Count == 0
            ? new Dictionary<Guid, ProcessRoleRequirement>()
            : await dbContext.Set<ProcessRoleRequirement>()
                .AsNoTracking()
                .Where(item => roleRequirementIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var runAssignments = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId)
            .ToListAsync(cancellationToken);
        var artifactInputs = readyStepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepArtifactInputDefinition>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        var artifactInputsByStepDefinitionId = artifactInputs
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ProcessStepArtifactInputDefinition>)group.ToList());
        var artifactExpectationIds = artifactInputs
            .Select(item => item.ArtifactExpectationId)
            .Distinct()
            .ToList();
        var branchOutcomes = readyStepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken);
        var branchOutcomesByStepDefinitionId = branchOutcomes
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepBranchOutcomeDefinition>)group.ToList());
        var conditionalDependencies = readyStepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepDependencyDefinition>()
                .AsNoTracking()
                .Where(item => readyStepDefinitionIds.Contains(item.DependsOnStepId) && item.DependsOnBranchOutcomeId.HasValue)
                .ToListAsync(cancellationToken);
        var conditionalDependencyOutcomeIdsByStepDefinitionId = conditionalDependencies
            .GroupBy(item => item.DependsOnStepId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Where(item => item.DependsOnBranchOutcomeId.HasValue)
                    .Select(item => item.DependsOnBranchOutcomeId!.Value)
                    .ToHashSet());
        var artifactExpectationsById = artifactExpectationIds.Count == 0
            ? new Dictionary<Guid, ProcessArtifactExpectation>()
            : await dbContext.Set<ProcessArtifactExpectation>()
                .AsNoTracking()
                .Where(item => artifactExpectationIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var sourceStepDefinitionIds = artifactExpectationsById.Values
            .Select(item => item.StepDefinitionId)
            .Distinct()
            .ToList();
        var sourceStepsById = sourceStepDefinitionIds.Count == 0
            ? new Dictionary<Guid, ProcessStepDefinition>()
            : await dbContext.Set<ProcessStepDefinition>()
                .AsNoTracking()
                .Where(item => sourceStepDefinitionIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));

        foreach (var stepRun in dispatchableSteps)
        {
            if (!readyStepDefinitionsById.TryGetValue(stepRun.StepDefinitionId, out var currentStepDefinition))
            {
                continue;
            }

            if (stepRun.StepKind == ProcessStepKind.Subprocess)
            {
                return new DispatchCandidate(
                    run,
                    definition,
                    stepRun,
                    currentStepDefinition,
                    workBriefsByStepRunId.GetValueOrDefault(stepRun.Id),
                    Guid.Empty,
                    [],
                    new HashSet<Guid>(),
                    [],
                    externalReferenceKeys,
                    null,
                    null,
                    string.Empty,
                    [],
                    false,
                    new AgentProcessCooperationMetadata(
                        AgentProcessCooperationMode.ProcessArtifactHandoff,
                        AgentWorkspaceToolProfileKind.ReadOnly,
                        "Subprocess step is orchestrated by the process runtime."));
            }

            stepRoleRequirementsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var workflowStepRoleRequirements);
            var workflowAssignment = ResolveDispatchCurrentAssignment(stepRun, workflowStepRoleRequirements ?? [], runAssignments);
            var workflowRole = workflowAssignment is null
                ? null
                : roleRequirementsById.GetValueOrDefault(workflowAssignment.RoleRequirementId);
            if (IsWorkflowDispatchAssignment(workflowAssignment, workflowRole))
            {
                return new DispatchCandidate(
                    run,
                    definition,
                    stepRun,
                    currentStepDefinition,
                    workBriefsByStepRunId.GetValueOrDefault(stepRun.Id),
                    Guid.Empty,
                    [],
                    new HashSet<Guid>(),
                    [],
                    externalReferenceKeys,
                    null,
                    null,
                    string.Empty,
                    [],
                    false,
                    new AgentProcessCooperationMetadata(
                        AgentProcessCooperationMode.ProcessArtifactHandoff,
                        AgentWorkspaceToolProfileKind.ReadOnly,
                        "Workflow step is orchestrated through the Microsoft Agent Framework workflow runtime."));
            }

            if (!stepRun.CurrentExecutorPartyId.HasValue)
            {
                continue;
            }

            var executorPartyId = stepRun.CurrentExecutorPartyId.Value;
            var executionRuns = await workspaceService.ListExecutionRunsAsync(
                new ExecutionRunQuery(
                    ProcessRunId: processRunId.ToString("D"),
                    ProcessStepId: stepRun.Id.ToString("D"),
                    Take: 20),
                cancellationToken);
            if (HasBlockingAutomationExecutionRun(executionRuns, clock.GetUtcNow()))
            {
                continue;
            }

            var recoveryExecutionRunId = ResolveRecoverableAutomationExecutionRunId(stepRun, executionRuns);
            Guid? reusableChatSessionId = null;
            var manualRecoveryDirective = await LoadLatestManualRecoveryDirectiveAsync(
                dbContext,
                run.Id,
                stepRun.Id,
                stepRun.StartedAtUtc,
                cancellationToken);
            var summaries = await technicalAgentBridge.GetDirectorySummariesAsync([executorPartyId], cancellationToken);
            var hasTechnicalAgentSummary = summaries.TryGetValue(executorPartyId, out var technicalAgentSummary);
            if (!hasTechnicalAgentSummary ||
                technicalAgentSummary is null ||
                !technicalAgentSummary.TechnicalAgentId.HasValue ||
                technicalAgentSummary.BindingStatus != AiResourceBindingStatus.Bound)
            {
                logger.LogWarning(
                    "{Diagnostic}",
                    BuildMissingTechnicalAgentBindingDiagnostic(
                        run.Id,
                        stepRun.Id,
                        stepRun.Title,
                        executorPartyId,
                        technicalAgentSummary?.BindingStatus,
                        technicalAgentSummary?.TechnicalAgentId));
                continue;
            }

            var agentEditor = await workspaceService.GetAgentEditorAsync(technicalAgentSummary.TechnicalAgentId.Value, cancellationToken);
            if (TryResolveProjectStructureAccessProjectId(run, out var projectStructureAccessProjectId) &&
                ApplyProjectStructureReadAccess(agentEditor, projectStructureAccessProjectId))
            {
                await workspaceService.SaveAgentAsync(agentEditor, cancellationToken);
                logger.LogInformation(
                    "Granted project-structure read access for project {ProjectId} to technical agent {TechnicalAgentId} before dispatching process run {RunId}, step {StepRunId}.",
                    projectStructureAccessProjectId,
                    technicalAgentSummary.TechnicalAgentId.Value,
                    run.Id,
                    stepRun.Id);
            }

            artifactInputsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var configuredArtifactInputs);
            var availableBranchOutcomes = branchOutcomesByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var configuredBranchOutcomes)
                ? configuredBranchOutcomes
                    .Select(item => new DispatchBranchOutcome(item.Id, item.Key, item.Title, item.Description))
                    .ToList()
                : [];
            var requiresExplicitBranchOutcomeSelection =
                conditionalDependencyOutcomeIdsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var requiredBranchOutcomeIds) &&
                availableBranchOutcomes.Any(item => requiredBranchOutcomeIds.Contains(item.Id));
            stepRoleRequirementsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var currentStepRoleRequirements);
            var currentAssignment = ResolveDispatchCurrentAssignment(stepRun, currentStepRoleRequirements ?? [], runAssignments);
            var currentRole = currentAssignment is null
                ? null
                : roleRequirementsById.GetValueOrDefault(currentAssignment.RoleRequirementId);
            var expectedArtifacts = await LoadExpectedArtifactsAsync(dbContext, stepRun.StepDefinitionId, cancellationToken);
            var recordedArtifactExpectationIds = existingArtifacts
                .Where(item => item.StepRunId == stepRun.Id && item.ArtifactExpectationId.HasValue)
                .Select(item => item.ArtifactExpectationId!.Value)
                .ToHashSet();
            recoveryExecutionRunId ??= ResolveArtifactRecoveryExecutionRunId(
                stepRun,
                executionRuns,
                expectedArtifacts,
                recordedArtifactExpectationIds);
            var preparedArtifactInputs = PrepareArtifactInputsForPrompt(
                BuildResolvedArtifactInputs(
                    configuredArtifactInputs ?? [],
                    artifactExpectationsById,
                    sourceStepsById,
                    stepRunsByDefinitionId,
                    existingArtifacts),
                workspaceRoot,
                workspaceScope);
            return new DispatchCandidate(
                run,
                definition,
                stepRun,
                currentStepDefinition,
                workBriefsByStepRunId.GetValueOrDefault(stepRun.Id),
                technicalAgentSummary.TechnicalAgentId.Value,
                expectedArtifacts,
                recordedArtifactExpectationIds,
                preparedArtifactInputs,
                externalReferenceKeys,
                reusableChatSessionId,
                recoveryExecutionRunId,
                manualRecoveryDirective,
                availableBranchOutcomes,
                requiresExplicitBranchOutcomeSelection,
                ResolveProcessCooperationMetadata(
                    stepRun,
                    workBriefsByStepRunId.GetValueOrDefault(stepRun.Id),
                    currentRole,
                    currentAssignment,
                    expectedArtifacts,
                    preparedArtifactInputs,
                    availableBranchOutcomes,
                    agentEditor));
        }

        return null;
    }

    internal static bool ApplyProjectStructureReadAccess(AgentEditorModel agentEditor, Guid projectId)
    {
        ArgumentNullException.ThrowIfNull(agentEditor);

        if (projectId == Guid.Empty)
        {
            return false;
        }

        var access = AgentProjectStructureAccessMetadata.Normalize(agentEditor.ProjectStructureAccess);
        if (access.CanRead &&
            (access.AllowAllProjects || access.AllowedProjectIds.Contains(projectId)))
        {
            agentEditor.ProjectStructureAccess = access;
            return false;
        }

        access.CanRead = true;
        if (!access.AllowAllProjects &&
            !access.AllowedProjectIds.Contains(projectId))
        {
            access.AllowedProjectIds.Add(projectId);
        }

        agentEditor.ProjectStructureAccess = AgentProjectStructureAccessMetadata.Normalize(access);
        return true;
    }

    private static bool TryResolveProjectStructureAccessProjectId(ProcessRun run, out Guid projectId)
    {
        if (ProcessProjectStructureContextFormatter.TryParse(run.TriggerReason, out var projectStructureContext) &&
            projectStructureContext is not null &&
            projectStructureContext.ProjectId != Guid.Empty)
        {
            projectId = projectStructureContext.ProjectId;
            return true;
        }

        if (run.ProjectId.HasValue && run.ProjectId.Value != Guid.Empty)
        {
            projectId = run.ProjectId.Value;
            return true;
        }

        projectId = Guid.Empty;
        return false;
    }

    private static bool IsWorkflowDispatchAssignment(
        ProcessRunAssignment? assignment,
        ProcessRoleRequirement? role)
    {
        return assignment is not null &&
            (ProcessExecutorKindNames.IsWorkflow(assignment.ExecutorKind) ||
             assignment.WorkflowDefinitionId.HasValue ||
             ProcessExecutorKindNames.IsWorkflow(role?.PreferredExecutorKind) ||
             role?.PreferredWorkflowDefinitionId.HasValue == true);
    }

    private static async Task<string> LoadLatestManualRecoveryDirectiveAsync(
        AppDbContext dbContext,
        Guid runId,
        Guid stepRunId,
        DateTimeOffset? stepStartedAtUtc,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessRunId == runId &&
                item.StepRunId == stepRunId &&
                item.EventType == ProcessRuntimeEventTypes.ManualAgentStepRerun);
        var journalEntries = await query.ToListAsync(cancellationToken);
        var candidateEntries = stepStartedAtUtc.HasValue
            ? journalEntries.Where(item => item.OccurredAtUtc >= stepStartedAtUtc.Value)
            : journalEntries;

        return candidateEntries
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => item.Description)
            .FirstOrDefault() ?? string.Empty;
    }

}
