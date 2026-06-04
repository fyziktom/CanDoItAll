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
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

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
            var candidateHeaderLoadStarted = Stopwatch.GetTimestamp();
            var candidateHeaders = await LoadDispatchCandidateHeadersAsync(processRunId, cancellationToken);
            logger.LogDebug(
                "Loaded {CandidateCount} dispatch candidate headers for process run {ProcessRunId} in {ElapsedMilliseconds} ms.",
                candidateHeaders.Count,
                processRunId,
                GetElapsedMilliseconds(candidateHeaderLoadStarted));
            if (candidateHeaders.Count == 0)
            {
                return;
            }

            foreach (var candidateHeader in candidateHeaders)
            {
                var dispatchGuard = StepDispatchGuards.GetOrAdd(candidateHeader.StepRunId, static _ => new SemaphoreSlim(1, 1));
                await dispatchGuard.WaitAsync(cancellationToken);
                var dispatchGuardHeld = true;
                try
                {
                    var dispatchClaim = await TryClaimStepDispatchAsync(
                        processRunId,
                        candidateHeader.StepRunId,
                        trigger,
                        triggerStepRunId,
                        cancellationToken);
                    if (dispatchClaim is null)
                    {
                        continue;
                    }

                    dispatchGuard.Release();
                    dispatchGuardHeld = false;

                    DispatchCandidate? candidate = null;
                    ProcessDispatchLeaseHeartbeat? dispatchHeartbeat = null;
                    var dispatchCancellationToken = cancellationToken;
                    try
                    {
                        var dispatchRenewLeaseAsync = CreateDispatchRenewLeaseCallback(dispatchClaim, renewLeaseAsync);
                        dispatchHeartbeat = ProcessDispatchLeaseHeartbeat.Start(
                            dispatchClaim.StepRunId,
                            ResolveStepDispatchHeartbeatInterval(),
                            dispatchRenewLeaseAsync,
                            cancellationToken);
                        dispatchCancellationToken = dispatchHeartbeat.DispatchCancellationToken;
                        var candidateHydrationStarted = Stopwatch.GetTimestamp();
                        candidate = await LoadDispatchCandidateAsync(processRunId, dispatchClaim.StepRunId, trigger, dispatchCancellationToken);
                        logger.LogDebug(
                            "Hydrated claimed dispatch candidate for process run {ProcessRunId}, step {StepRunId}. CandidateFound={CandidateFound} ElapsedMilliseconds={ElapsedMilliseconds}.",
                            processRunId,
                            dispatchClaim.StepRunId,
                            candidate is not null,
                            GetElapsedMilliseconds(candidateHydrationStarted));
                        if (candidate is null)
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

                        var usesAgentAutomation = candidate.TechnicalAgentId != Guid.Empty &&
                            candidate.StepRun.StepKind != ProcessStepKind.Subprocess;
                        var databaseRequirementFailure = usesAgentAutomation
                            ? ResolveAutomationDatabaseRequirementFailure()
                            : null;
                        if (databaseRequirementFailure is not null)
                        {
                            await BlockDispatchForDatabaseRequirementAsync(candidate, databaseRequirementFailure, dispatchClaim, dispatchCancellationToken);
                            return;
                        }

                        if (await TryRequestMissingUpstreamArtifactMaterializationAsync(candidate, dispatchClaim, dispatchCancellationToken))
                        {
                            return;
                        }

                        var strandedArtifactRecoveryOutcome = await TryRecoverStrandedMissingCompletionArtifactsAsync(
                            candidate,
                            trigger,
                            dispatchClaim,
                            dispatchRenewLeaseAsync,
                            dispatchCancellationToken);
                        if (strandedArtifactRecoveryOutcome is not null)
                        {
                            var finalizedRecoveryCompletion = await FinalizeStepCompletionAsync(
                                new ProcessStepCompletionFinalizerContext(
                                    ExecutorKind: ProcessStepCompletionExecutorKind.ManagerArtifactRecovery,
                                    Candidate: candidate,
                                    CompletionStatus: strandedArtifactRecoveryOutcome.CompletionStatus,
                                    CompletionReason: strandedArtifactRecoveryOutcome.CompletionReason,
                                    SelectedBranchOutcomeId: strandedArtifactRecoveryOutcome.SelectedBranchOutcomeId,
                                    ExecutionDetail: strandedArtifactRecoveryOutcome.Detail,
                                    WorkflowRunId: null,
                                    SubprocessRunId: null,
                                    ResponseText: strandedArtifactRecoveryOutcome.ResponseText,
                                    ProjectExecutionArtifacts: false,
                                    AllowManagerArtifactRecovery: false,
                                    Trigger: trigger,
                                    RenewLeaseAsync: dispatchRenewLeaseAsync,
                                    RecoveryExecutionRunId: strandedArtifactRecoveryOutcome.Detail.Run.Id,
                                    RecoveredForExecutionRunId: candidate.RecoveryExecutionRunId),
                                dispatchClaim,
                                dispatchCancellationToken);
                            if (finalizedRecoveryCompletion is not null)
                            {
                                await ApplyFinalizedStepTransitionAsync(candidate, finalizedRecoveryCompletion, dispatchClaim, dispatchCancellationToken);
                            }

                            return;
                        }

                        if (candidate.StepRun.StepKind == ProcessStepKind.Subprocess)
                        {
                            await HandleSubprocessDispatchAsync(candidate, trigger, triggerStepRunId, dispatchClaim, dispatchCancellationToken);
                            return;
                        }

                        if (candidate.StepRun.Status != ProcessStepRunStatus.InProgress)
                        {
                            var startResult = await TransitionStepWithClaimAsync(
                                new ProcessStepTransitionRequest
                                {
                                    StepRunId = candidate.StepRun.Id,
                                    StepRunConcurrencyToken = candidate.StepRun.ConcurrencyToken,
                                    TargetStatus = ProcessStepRunStatus.InProgress,
                                    Reason = $"Started by the durable process automation dispatcher ({NormalizeTrigger(trigger, triggerStepRunId)}).",
                                    DecidedBy = AutomationActor,
                                    SuppressAutomationDispatch = true
                                },
                                dispatchClaim,
                                dispatchCancellationToken);
                            if (startResult.IsFailure)
                            {
                                logger.LogInformation(
                                    "Process step {StepRunId} could not be claimed for automation dispatch on run {RunId}. Errors: {Errors}",
                                    candidate.StepRun.Id,
                                    processRunId,
                                    string.Join(" | ", startResult.Errors.Select(error => error.Message)));
                                var refreshedCandidate = await LoadDispatchCandidateAsync(processRunId, dispatchClaim.StepRunId, trigger, dispatchCancellationToken);
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

                        var workflowOutcome = await workflowRunCoordinator.TryRunOrObserveAsync(
                            candidate.Run.Id,
                            candidate.StepRun.Id,
                            NormalizeTrigger(trigger, triggerStepRunId),
                            dispatchCancellationToken);
                        if (workflowOutcome.Handled)
                        {
                            await HandleWorkflowExecutionOutcomeAsync(candidate, workflowOutcome, dispatchClaim, dispatchCancellationToken);
                            return;
                        }

                        var executionOutcome = await ExecuteUntilSettledAsync(candidate, trigger, dispatchRenewLeaseAsync, dispatchCancellationToken);
                        dispatchHeartbeat.ThrowIfClaimLost();
                        var competingExecution = executionOutcome.CompletionStatus is not ProcessStepRunStatus.Completed
                            ? await ResolveCompetingActiveAutomationExecutionAsync(candidate, executionOutcome, dispatchCancellationToken)
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

                        if (await IsRunClosedToAutomationAsync(candidate.Run.Id, candidate.StepRun.Id, dispatchCancellationToken))
                        {
                            logger.LogInformation(
                                "Skipping automation completion projection for run {RunId}, step {StepRunId} because the process run became terminal while agent execution was in flight.",
                                candidate.Run.Id,
                                candidate.StepRun.Id);
                            return;
                        }

                        var finalizedCompletion = await FinalizeStepCompletionAsync(
                            new ProcessStepCompletionFinalizerContext(
                                ExecutorKind: ProcessStepCompletionExecutorKind.DirectAgent,
                                Candidate: candidate,
                                CompletionStatus: executionOutcome.CompletionStatus,
                                CompletionReason: executionOutcome.CompletionReason,
                                SelectedBranchOutcomeId: executionOutcome.SelectedBranchOutcomeId,
                                ExecutionDetail: executionOutcome.Detail,
                                WorkflowRunId: null,
                                SubprocessRunId: null,
                                ResponseText: executionOutcome.ResponseText,
                                ProjectExecutionArtifacts: true,
                                AllowManagerArtifactRecovery: true,
                                Trigger: trigger,
                                RenewLeaseAsync: dispatchRenewLeaseAsync),
                            dispatchClaim,
                            dispatchCancellationToken);
                        dispatchHeartbeat.ThrowIfClaimLost();
                        if (finalizedCompletion is not null)
                        {
                            await ApplyFinalizedStepTransitionAsync(candidate, finalizedCompletion, dispatchClaim, dispatchCancellationToken);
                        }

                        return;
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (OperationCanceledException) when (dispatchHeartbeat?.ClaimLost == true)
                    {
                        var claimLostException = dispatchHeartbeat.CreateClaimLostException();
                        if (candidate is null)
                        {
                            logger.LogWarning(
                                claimLostException,
                                "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch heartbeat was lost before candidate hydration completed.",
                                processRunId,
                                dispatchClaim.StepRunId);
                            return;
                        }

                        logger.LogWarning(
                            claimLostException,
                            "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch heartbeat was lost.",
                            candidate.Run.Id,
                            candidate.StepRun.Id);
                        return;
                    }
                    catch (ProcessDispatchClaimLostException exception)
                    {
                        if (candidate is null)
                        {
                            logger.LogWarning(
                                exception,
                                "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch claim was lost before candidate hydration completed.",
                                processRunId,
                                dispatchClaim.StepRunId);
                            return;
                        }

                        logger.LogWarning(
                            exception,
                            "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch claim was lost.",
                            candidate.Run.Id,
                            candidate.StepRun.Id);
                        return;
                    }
                    catch (Exception exception)
                    {
                        if (dispatchHeartbeat?.ClaimLost == true)
                        {
                            var claimLostException = dispatchHeartbeat.CreateClaimLostException();
                            if (candidate is null)
                            {
                                logger.LogWarning(
                                    claimLostException,
                                    "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch heartbeat was lost before candidate hydration completed.",
                                    processRunId,
                                    dispatchClaim.StepRunId);
                                return;
                            }

                            logger.LogWarning(
                                claimLostException,
                                "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch heartbeat was lost.",
                                candidate.Run.Id,
                                candidate.StepRun.Id);
                            return;
                        }

                        if (candidate is null)
                        {
                            logger.LogError(
                                exception,
                                "Process automation dispatch failed for run {RunId}, step {StepRunId} before candidate hydration completed.",
                                processRunId,
                                dispatchClaim.StepRunId);
                            return;
                        }

                        logger.LogError(
                            exception,
                            "Process automation dispatch failed for run {RunId}, step {StepRunId}.",
                            candidate.Run.Id,
                            candidate.StepRun.Id);

                        if (dispatchHeartbeat?.ClaimLost == true)
                        {
                            logger.LogWarning(
                                dispatchHeartbeat.CreateClaimLostException(),
                                "Stopping process automation dispatch for run {RunId}, step {StepRunId} because the durable dispatch heartbeat was lost.",
                                candidate.Run.Id,
                                candidate.StepRun.Id);
                            return;
                        }

                        if (await IsRunClosedToAutomationAsync(candidate.Run.Id, candidate.StepRun.Id, dispatchCancellationToken))
                        {
                            logger.LogInformation(
                                "Skipping automation failure transition for run {RunId}, step {StepRunId} because the process run became terminal while agent execution was in flight.",
                                candidate.Run.Id,
                                candidate.StepRun.Id);
                            return;
                        }

                        if (!await IsStepDispatchClaimHeldAsync(dispatchClaim, dispatchCancellationToken))
                        {
                            logger.LogWarning(
                                "Skipping automation failure transition for run {RunId}, step {StepRunId} because the durable dispatch claim is no longer held.",
                                candidate.Run.Id,
                                candidate.StepRun.Id);
                            return;
                        }

                        var failResult = await TransitionStepWithClaimAsync(
                            new ProcessStepTransitionRequest
                            {
                                StepRunId = candidate.StepRun.Id,
                                TargetStatus = ProcessStepRunStatus.Failed,
                                Reason = $"AgentFramework execution failed: {exception.Message}",
                                DecidedBy = AutomationActor,
                                SuppressAutomationDispatch = true
                            },
                            dispatchClaim,
                            dispatchCancellationToken);
                        if (failResult.IsFailure)
                        {
                            logger.LogWarning(
                                "Process step {StepRunId} could not be moved to Failed after an execution exception. Errors: {Errors}",
                                candidate.StepRun.Id,
                                string.Join(" | ", failResult.Errors.Select(error => error.Message)));
                        }

                        return;
                    }
                    finally
                    {
                        if (dispatchHeartbeat is not null)
                        {
                            await dispatchHeartbeat.DisposeAsync();
                        }

                        await ReleaseStepDispatchClaimAsync(dispatchClaim, cancellationToken);
                    }
                }
                finally
                {
                    if (dispatchGuardHeld)
                    {
                        dispatchGuard.Release();
                    }

                    TryRemoveReleasedDispatchGuard(candidateHeader.StepRunId, dispatchGuard);
                }
            }

            return;
        }
    }

    private static double GetElapsedMilliseconds(long startTimestamp)
        => Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;

    private TimeSpan ResolveStepDispatchClaimLeaseDuration()
    {
        var leaseDuration = processRuntimeOptions.Value.StepDispatchClaimLeaseDuration;
        if (leaseDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Processes:Runtime:StepDispatchClaimLeaseDuration must be positive.");
        }

        return leaseDuration;
    }

    private TimeSpan ResolveStepDispatchHeartbeatInterval()
    {
        var heartbeatInterval = processRuntimeOptions.Value.StepDispatchHeartbeatInterval;
        if (heartbeatInterval <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Processes:Runtime:StepDispatchHeartbeatInterval must be positive.");
        }

        if (heartbeatInterval >= ResolveStepDispatchClaimLeaseDuration())
        {
            throw new InvalidOperationException("Processes:Runtime:StepDispatchHeartbeatInterval must be shorter than StepDispatchClaimLeaseDuration.");
        }

        return heartbeatInterval;
    }

    private static void TryRemoveReleasedDispatchGuard(Guid stepRunId, SemaphoreSlim dispatchGuard)
    {
        if (dispatchGuard.CurrentCount != 1)
        {
            return;
        }

        ((ICollection<KeyValuePair<Guid, SemaphoreSlim>>)StepDispatchGuards)
            .Remove(new KeyValuePair<Guid, SemaphoreSlim>(stepRunId, dispatchGuard));
    }

    private async Task HandleWorkflowExecutionOutcomeAsync(
        DispatchCandidate candidate,
        ProcessWorkflowExecutionOutcome workflowOutcome,
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var finalizedCompletion = await FinalizeStepCompletionAsync(
            new ProcessStepCompletionFinalizerContext(
                ProcessStepCompletionExecutorKind.WorkflowBackedRole,
                candidate,
                workflowOutcome.CompletionStatus,
                workflowOutcome.CompletionReason,
                SelectedBranchOutcomeId: null,
                ExecutionDetail: null,
                WorkflowRunId: workflowOutcome.Link?.WorkflowRunId,
                SubprocessRunId: null,
                ResponseText: workflowOutcome.CompletionReason,
                ProjectExecutionArtifacts: false,
                AllowManagerArtifactRecovery: false,
                Trigger: "workflow-execution-outcome",
                RenewLeaseAsync: null),
            dispatchClaim,
            cancellationToken);
        if (finalizedCompletion is null)
        {
            return;
        }

        await ApplyFinalizedStepTransitionAsync(candidate, finalizedCompletion, dispatchClaim, cancellationToken);
    }

    private async Task<ProcessStepDispatchClaim?> TryClaimStepDispatchAsync(
        Guid processRunId,
        Guid stepRunId,
        string trigger,
        Guid? triggerStepRunId,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var claimToken = Guid.NewGuid().ToString("N");
        var leaseExpiresAtUtc = now.Add(ResolveStepDispatchClaimLeaseDuration());
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var updatedRows = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.Id == stepRunId)
            .Where(item => item.ProcessRunId == processRunId)
            .Where(item =>
                item.Status == ProcessStepRunStatus.Ready ||
                item.Status == ProcessStepRunStatus.WaitingApproval ||
                item.Status == ProcessStepRunStatus.InProgress)
            .Where(item =>
                item.AutomationDispatchLeaseExpiresAtUtc == null ||
                item.AutomationDispatchLeaseExpiresAtUtc <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.AutomationDispatchClaimToken, claimToken)
                    .SetProperty(item => item.AutomationDispatchClaimedBy, AutomationDispatcherInstanceId)
                    .SetProperty(item => item.AutomationDispatchClaimedAtUtc, now)
                    .SetProperty(item => item.AutomationDispatchLeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(item => item.AutomationDispatchAttemptCount, item => item.AutomationDispatchAttemptCount + 1),
                cancellationToken);
        if (updatedRows == 0)
        {
            logger.LogInformation(
                "Process automation dispatch for run {RunId}, step {StepRunId} was skipped because another worker holds the durable dispatch claim or the step is no longer dispatchable.",
                processRunId,
                stepRunId);
            return null;
        }

        logger.LogInformation(
            "Claimed process automation dispatch for run {RunId}, step {StepRunId}. Trigger={Trigger}. LeaseExpiresAtUtc={LeaseExpiresAtUtc}.",
            processRunId,
            stepRunId,
            NormalizeTrigger(trigger, triggerStepRunId),
            leaseExpiresAtUtc);

        return new ProcessStepDispatchClaim(stepRunId, claimToken);
    }

    private Func<CancellationToken, Task> CreateDispatchRenewLeaseCallback(
        ProcessStepDispatchClaim dispatchClaim,
        Func<CancellationToken, Task>? renewOuterLeaseAsync)
    {
        return async token =>
        {
            if (renewOuterLeaseAsync is not null)
            {
                await renewOuterLeaseAsync(token);
            }

            if (!await RenewStepDispatchClaimAsync(dispatchClaim, token))
            {
                throw new ProcessDispatchClaimLostException(dispatchClaim.StepRunId);
            }
        };
    }

    private async Task<bool> RenewStepDispatchClaimAsync(
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var leaseExpiresAtUtc = now.Add(ResolveStepDispatchClaimLeaseDuration());
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var updatedRows = await dbContext.Set<ProcessStepRun>()
            .Where(item => item.Id == dispatchClaim.StepRunId)
            .Where(item => item.AutomationDispatchClaimToken == dispatchClaim.ClaimToken)
            .Where(item => item.AutomationDispatchLeaseExpiresAtUtc > now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(item => item.AutomationDispatchLeaseExpiresAtUtc, leaseExpiresAtUtc),
                cancellationToken);
        if (updatedRows == 0)
        {
            logger.LogWarning(
                "Could not renew process automation dispatch claim for step {StepRunId}; another worker may have claimed or completed it.",
                dispatchClaim.StepRunId);
            return false;
        }

        return true;
    }

    private async Task EnsureStepDispatchClaimHeldAsync(
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        if (await IsStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken))
        {
            return;
        }

        throw new ProcessDispatchClaimLostException(dispatchClaim.StepRunId);
    }

    private async Task<bool> IsStepDispatchClaimHeldAsync(
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.Id == dispatchClaim.StepRunId)
            .Where(item => item.AutomationDispatchClaimToken == dispatchClaim.ClaimToken)
            .Where(item => item.AutomationDispatchLeaseExpiresAtUtc > now)
            .AnyAsync(cancellationToken);
    }

    private async Task ReleaseStepDispatchClaimAsync(
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            await dbContext.Set<ProcessStepRun>()
                .Where(item => item.Id == dispatchClaim.StepRunId)
                .Where(item => item.AutomationDispatchClaimToken == dispatchClaim.ClaimToken)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(item => item.AutomationDispatchClaimToken, string.Empty)
                        .SetProperty(item => item.AutomationDispatchClaimedBy, string.Empty)
                        .SetProperty(item => item.AutomationDispatchClaimedAtUtc, (DateTimeOffset?)null)
                        .SetProperty(item => item.AutomationDispatchLeaseExpiresAtUtc, (DateTimeOffset?)null),
                    cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private sealed record ProcessStepDispatchClaim(Guid StepRunId, string ClaimToken);

    private sealed record DispatchCandidateHeader(Guid StepRunId, ProcessStepRunStatus Status);

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
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        var stepRunSnapshot = candidate.StepRun;
        if (stepRunSnapshot.Status != ProcessStepRunStatus.InProgress)
        {
            var startResult = await TransitionStepWithClaimAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRunSnapshot.Id,
                    StepRunConcurrencyToken = stepRunSnapshot.ConcurrencyToken,
                    TargetStatus = ProcessStepRunStatus.InProgress,
                    Reason = $"Started subprocess by the durable process automation dispatcher ({NormalizeTrigger(trigger, triggerStepRunId)}).",
                    DecidedBy = AutomationActor,
                    SuppressAutomationDispatch = true
                },
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

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        var subprocessResult = await processesService.EnsureSubprocessRunForStepAsync(stepRunSnapshot.Id, cancellationToken);
        if (subprocessResult.IsFailure)
        {
            await TransitionStepWithClaimAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRunSnapshot.Id,
                    TargetStatus = ProcessStepRunStatus.Blocked,
                    Reason = string.Join(" | ", subprocessResult.Errors.Select(error => error.Message)),
                    DecidedBy = AutomationActor,
                    SuppressAutomationDispatch = true
                },
                dispatchClaim,
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
                var blockResult = await TransitionStepWithClaimAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = stepRunSnapshot.Id,
                        TargetStatus = ProcessStepRunStatus.Blocked,
                        Reason = capabilityGapBlockReason,
                        DecidedBy = AutomationActor,
                        SuppressAutomationDispatch = true
                    },
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
            var transitionReason = BuildSubprocessParentTransitionReason(subprocessRun);
            var finalizedCompletion = await FinalizeStepCompletionAsync(
                new ProcessStepCompletionFinalizerContext(
                    ProcessStepCompletionExecutorKind.SubprocessParent,
                    candidate,
                    terminalStatus.Value,
                    transitionReason,
                    SelectedBranchOutcomeId: null,
                    ExecutionDetail: null,
                    WorkflowRunId: null,
                    SubprocessRunId: subprocessRun.RunId,
                    ResponseText: transitionReason,
                    ProjectExecutionArtifacts: false,
                    AllowManagerArtifactRecovery: false,
                    Trigger: "subprocess-execution-outcome",
                    RenewLeaseAsync: null),
                dispatchClaim,
                cancellationToken);
            if (finalizedCompletion is not null)
            {
                await ApplyFinalizedStepTransitionAsync(candidate, finalizedCompletion, dispatchClaim, cancellationToken);
            }

            return;
        }

        var transitionResult = await TransitionStepWithClaimAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = stepRunSnapshot.Id,
                TargetStatus = terminalStatus.Value,
                Reason = BuildSubprocessParentTransitionReason(subprocessRun),
                DecidedBy = AutomationActor,
                SuppressAutomationDispatch = terminalStatus.Value != ProcessStepRunStatus.Completed
            },
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
        ProcessStepDispatchClaim dispatchClaim,
        CancellationToken cancellationToken)
    {
        await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
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
            .Where(WorkflowSubprocessArtifactMapper.IsSubprocessCompletionProjectionAllowed)
            .Where(expectation => !parentArtifacts.Any(artifact =>
                SatisfiesCurrentSubprocessArtifactExpectation(artifact, expectation, subprocessRun.RunId)))
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

        foreach (var expectation in missingProjectableExpectations)
        {
            await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
            var sourceArtifact = WorkflowSubprocessArtifactMapper.ResolveSubprocessSourceArtifact(
                childArtifacts,
                missingProjectableExpectations,
                expectation,
                out var projectionDiagnostic);
            if (sourceArtifact is null)
            {
                await RecordSubprocessProjectionGapAsync(
                    dbContext,
                    candidate,
                    subprocessRun,
                    expectation,
                    projectionDiagnostic,
                    now,
                    cancellationToken);
                continue;
            }

            var projectedManagedStoragePath = await WriteProjectedSubprocessArtifactAsync(
                candidate,
                subprocessRun,
                expectation,
                sourceArtifact,
                cancellationToken);
            var projectionLineage = ProcessArtifactProjectionLineageJson.Normalize(
                new ProcessArtifactProjectionLineage
                {
                    SourceKind = ProcessArtifactProjectionSourceKind.SubprocessArtifact,
                    SubprocessRunId = subprocessRun.RunId,
                    SourceArtifactId = sourceArtifact.Id,
                    SourceExternalReferenceKey = sourceArtifact.ExternalReferenceKey
                })!;
            var artifact = new ProcessArtifactRecord
            {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                ArtifactExpectationId = expectation.Id,
                ArtifactKind = expectation.ArtifactKind,
                Title = expectation.Title,
                TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                SensitivityLevel = ResolveProjectedSubprocessSensitivity(expectation, sourceArtifact),
                ProvenanceSummary = BuildSubprocessArtifactProjectionProvenance(candidate, subprocessRun, sourceArtifact),
                AllowedFutureUsageSummary = expectation.AllowedFutureUsageSummary,
                ReviewSummary = BuildSubprocessArtifactProjectionReviewSummary(subprocessRun, sourceArtifact, projectionDiagnostic),
                ManagedStoragePath = projectedManagedStoragePath,
                ExternalReferenceKey = BuildSubprocessArtifactProjectionReferenceKey(subprocessRun.RunId, sourceArtifact.Id),
                ProjectionLineageJson = ProcessArtifactProjectionLineageJson.SerializeNormalized(projectionLineage),
                ProjectionIdentityHash = projectionLineage.ProjectionIdentityHash,
                CreatedAtUtc = now
            };
            await dbContext.Set<ProcessArtifactRecord>().AddAsync(artifact, cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry
                {
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

        await EnsureStepDispatchClaimHeldAsync(dispatchClaim, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> WriteProjectedSubprocessArtifactAsync(
        DispatchCandidate candidate,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactExpectation expectation,
        ProcessArtifactRecord sourceArtifact,
        CancellationToken cancellationToken)
    {
        var fileSlug = FileSafeSlugBuilder.Build(expectation.Title);
        if (string.IsNullOrWhiteSpace(fileSlug))
        {
            fileSlug = "subprocess-artifact-projection";
        }

        var scopedProfileId = databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N");
        var relativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(
            "artifacts",
            "scopes",
            "organization",
            scopedProfileId,
            "process-runs",
            candidate.Run.Id.ToString("D"),
            candidate.StepRun.Id.ToString("D"),
            $"{fileSlug}.md"));
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        var fullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinWorkspace(workspaceRoot, fullPath))
        {
            throw new InvalidOperationException(
                $"Projected subprocess artifact path '{relativePath}' resolves outside the workspace root.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        await File.WriteAllTextAsync(
            fullPath,
            BuildProjectedSubprocessArtifactMarkdown(candidate, subprocessRun, expectation, sourceArtifact),
            Encoding.UTF8,
            cancellationToken);
        return relativePath;
    }

    private static string BuildProjectedSubprocessArtifactMarkdown(
        DispatchCandidate candidate,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactExpectation expectation,
        ProcessArtifactRecord sourceArtifact)
    {
        return $"""
            # {expectation.Title}

            Parent process run: {candidate.Run.Id:D}
            Parent subprocess step: {candidate.StepRun.Id:D}
            Subprocess run: {subprocessRun.RunId:D}
            Subprocess artifact: {sourceArtifact.Id:D}
            Subprocess artifact title: {sourceArtifact.Title}
            Subprocess managed path: {sourceArtifact.ManagedStoragePath}

            This parent-scoped artifact is a durable projection of the completed subprocess output. The child run artifact ledger remains the source of detailed runtime evidence.
            """;
    }

    private async Task RecordSubprocessProjectionGapAsync(
        AppDbContext dbContext,
        DispatchCandidate candidate,
        ProcessSubprocessRunStartResult subprocessRun,
        ProcessArtifactExpectation expectation,
        string projectionDiagnostic,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var fingerprint = CreateSubprocessProjectionGapFingerprint(candidate.Run.Id, candidate.StepRun.Id, subprocessRun.RunId, expectation.Id);
        var existingGap = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.ProcessRunId == candidate.Run.Id &&
                    item.StepRunId == candidate.StepRun.Id &&
                    item.EventType == ProcessRuntimeEventTypes.ArtifactValidationDiagnostic &&
                    item.CorrelationId == fingerprint,
                cancellationToken);
        if (existingGap)
        {
            return;
        }

        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            new ProcessJournalEntry
            {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                EventType = ProcessRuntimeEventTypes.ArtifactValidationDiagnostic,
                Title = $"Subprocess artifact projection gap: {expectation.Title}",
                Description = string.IsNullOrWhiteSpace(projectionDiagnostic)
                    ? $"Completed subprocess run '{subprocessRun.RunName}' did not produce a child artifact that can satisfy parent expectation '{expectation.Title}'."
                    : $"Completed subprocess run '{subprocessRun.RunName}' did not produce a child artifact that can satisfy parent expectation '{expectation.Title}'. {projectionDiagnostic}",
                CorrelationId = fingerprint,
                OperatingMode = candidate.Run.OperatingMode,
                PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(new
                {
                    candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    SubprocessRunId = subprocessRun.RunId,
                    ExpectationId = expectation.Id,
                    ExpectationTitle = expectation.Title,
                    ProjectionDiagnostic = projectionDiagnostic
                }),
                OccurredAtUtc = now
            },
            cancellationToken);
    }

    private static string CreateSubprocessProjectionGapFingerprint(
        Guid processRunId,
        Guid stepRunId,
        Guid subprocessRunId,
        Guid expectationId)
    {
        var normalized = string.Join(
            "|",
            "subprocess-projection-gap",
            processRunId.ToString("D"),
            stepRunId.ToString("D"),
            subprocessRunId.ToString("D"),
            expectationId.ToString("D"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
    }

    internal static ProcessArtifactRecord? ResolveSubprocessSourceArtifact(
        IReadOnlyList<ProcessArtifactRecord> childArtifacts,
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations,
        ProcessArtifactExpectation expectation,
        out string diagnostic)
    {
        return WorkflowSubprocessArtifactMapper.ResolveSubprocessSourceArtifact(
            childArtifacts,
            parentExpectations,
            expectation,
            out diagnostic);
    }

    internal static IReadOnlyList<ProcessSubprocessOutputArtifactMapping> ResolveSubprocessOutputArtifactMappings(
        IReadOnlyList<ProcessArtifactExpectation> parentExpectations)
    {
        return WorkflowSubprocessArtifactMapper.ResolveSubprocessOutputArtifactMappings(parentExpectations);
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

    private static bool SatisfiesCurrentSubprocessArtifactExpectation(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation,
        Guid subprocessRunId)
    {
        return SatisfiesArtifactExpectation(artifact, expectation) &&
               artifact.ExternalReferenceKey.StartsWith("subprocess-run:", StringComparison.OrdinalIgnoreCase) &&
               artifact.ExternalReferenceKey.Contains(subprocessRunId.ToString("D"), StringComparison.OrdinalIgnoreCase);
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
            ProcessArtifactTrustRequirement.ApprovalRequired => trustStatus == ProcessArtifactTrustStatus.Approved,
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
        ProcessArtifactRecord? sourceArtifact,
        string projectionDiagnostic) {
        var diagnosticSuffix = string.IsNullOrWhiteSpace(projectionDiagnostic)
            ? string.Empty
            : $" Mapping diagnostic: {projectionDiagnostic}";
        if (sourceArtifact is null) {
            return $"Subprocess run '{subprocessRun.RunName}' completed. Review the child run artifact ledger before reusing this parent evidence outside the process.{diagnosticSuffix}";
        }

        var summary = string.IsNullOrWhiteSpace(sourceArtifact.ReviewSummary)
            ? $"Subprocess run '{subprocessRun.RunName}' completed. Source artifact: {sourceArtifact.Title}."
            : $"Subprocess run '{subprocessRun.RunName}' completed. Source artifact: {sourceArtifact.Title}. {sourceArtifact.ReviewSummary}";
        return $"{summary}{diagnosticSuffix}";
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
        ProcessStepDispatchClaim dispatchClaim,
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

        var transitionResult = await TransitionStepWithClaimAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = candidate.StepRun.Id,
                StepRunConcurrencyToken = candidate.StepRun.ConcurrencyToken,
                TargetStatus = targetStatus,
                Reason = failure.Message,
                DecidedBy = AutomationActor,
                SuppressAutomationDispatch = true
            },
            dispatchClaim,
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
        ProcessStepDispatchClaim dispatchClaim,
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
                var blockResult = await TransitionStepWithClaimAsync(
                    new ProcessStepTransitionRequest
                    {
                        StepRunId = candidate.StepRun.Id,
                        StepRunConcurrencyToken = snapshot.ConcurrencyToken,
                        TargetStatus = ProcessStepRunStatus.Blocked,
                        Reason = blockReason,
                        BlockCause = ProcessStepBlockCause.UpstreamInput,
                        DecidedBy = AutomationActor,
                        SuppressAutomationDispatch = true
                    },
                    dispatchClaim,
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
            await RecordMissingUpstreamArtifactMaterializationAsync(
                candidate,
                missingInputs,
                materializationTarget,
                blockReason,
                cancellationToken);
            logger.LogWarning(
                "Process run {RunId}, step {StepRunId} is missing required upstream artifacts, but no completed, blocked, or failed agent-owned source step is available for automatic materialization. Missing inputs: {MissingInputs}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                string.Join(" | ", missingInputs.Select(input => $"{input.SourceStepTitle}: {input.ExpectedArtifactTitle}")));
            return true;
        }

        var shouldRequestMaterialization = await RecordMissingUpstreamArtifactMaterializationAsync(
            candidate,
            missingInputs,
            materializationTarget,
            blockReason,
            cancellationToken);
        if (!shouldRequestMaterialization)
        {
            logger.LogInformation(
                "Skipping duplicate upstream artifact materialization request for run {RunId}, blocked downstream step {StepRunId}, source step {SourceStepRunId}; the same missing-artifact fingerprint is already recorded.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                materializationTarget.SourceStepRunId);
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

    private async Task<bool> RecordMissingUpstreamArtifactMaterializationAsync(
        DispatchCandidate candidate,
        IReadOnlyList<DispatchArtifactInput> missingInputs,
        DispatchArtifactInput? materializationTarget,
        string blockReason,
        CancellationToken cancellationToken)
    {
        var fingerprint = CreateMissingUpstreamArtifactMaterializationFingerprint(candidate, missingInputs, materializationTarget);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingFingerprint = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .AnyAsync(
                item =>
                    item.ProcessRunId == candidate.Run.Id &&
                    item.StepRunId == candidate.StepRun.Id &&
                    item.EventType == ProcessRuntimeEventTypes.MissingUpstreamArtifactMaterializationRequested &&
                    item.CorrelationId == fingerprint,
                cancellationToken);
        if (existingFingerprint)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        await dbContext.Set<ProcessJournalEntry>().AddAsync(
            new ProcessJournalEntry
            {
                ProcessRunId = candidate.Run.Id,
                StepRunId = candidate.StepRun.Id,
                EventType = ProcessRuntimeEventTypes.MissingUpstreamArtifactMaterializationRequested,
                Title = "Missing upstream artifact materialization requested",
                Description = blockReason,
                CorrelationId = fingerprint,
                OperatingMode = candidate.Run.OperatingMode,
                PolicyVersion = $"definition-version:{candidate.Run.ProcessDefinitionVersionId:D}",
                EnvironmentMode = candidate.Run.OperatingMode.ToString(),
                ReplayContextJson = JsonSerializer.Serialize(new
                {
                    candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    MaterializationSourceStepRunId = materializationTarget?.SourceStepRunId,
                    MissingInputs = missingInputs.Select(input => new
                    {
                        input.SourceStepTitle,
                        input.ExpectedArtifactTitle,
                        input.ArtifactExpectationId,
                        input.SourceStepDefinitionId,
                        input.SourceStepRunId,
                        input.SourceStepRunStatus
                    }).ToArray()
                }),
                OccurredAtUtc = now
            },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string CreateMissingUpstreamArtifactMaterializationFingerprint(
        DispatchCandidate candidate,
        IReadOnlyList<DispatchArtifactInput> missingInputs,
        DispatchArtifactInput? materializationTarget)
    {
        var normalizedInputs = missingInputs
            .OrderBy(input => input.SourceStepDefinitionId)
            .ThenBy(input => input.ArtifactExpectationId)
            .Select(input => string.Join(
                ":",
                input.SourceStepDefinitionId.ToString("D"),
                input.ArtifactExpectationId.ToString("D"),
                input.SourceStepRunId?.ToString("D") ?? string.Empty,
                input.SourceStepRunStatus?.ToString() ?? string.Empty));
        var normalized = string.Join(
            "|",
            "missing-upstream-artifact-materialization",
            candidate.Run.Id.ToString("D"),
            candidate.StepRun.Id.ToString("D"),
            materializationTarget?.SourceStepRunId?.ToString("D") ?? string.Empty,
            string.Join(",", normalizedInputs));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
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

    private async Task<IReadOnlyList<DispatchCandidateHeader>> LoadDispatchCandidateHeadersAsync(
        Guid processRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runStatus = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.Id == processRunId)
            .Select(item => (ProcessRunStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken);
        if (!runStatus.HasValue || !IsRunEligibleForDispatchCandidate(runStatus.Value))
        {
            return [];
        }

        var now = clock.GetUtcNow();
        var dispatchableSteps = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId &&
                (item.Status == ProcessStepRunStatus.Ready ||
                 item.Status == ProcessStepRunStatus.WaitingApproval ||
                 item.Status == ProcessStepRunStatus.InProgress))
            .Where(item =>
                item.AutomationDispatchLeaseExpiresAtUtc == null ||
                item.AutomationDispatchLeaseExpiresAtUtc <= now)
            .OrderBy(item => item.Sequence)
            .Select(item => new DispatchCandidateHeader(item.Id, item.Status))
            .ToListAsync(cancellationToken);

        return dispatchableSteps
            .Where(item => IsStepStatusDispatchableForRun(runStatus.Value, item.Status))
            .ToList();
    }

    private async Task<DispatchCandidate?> LoadDispatchCandidateAsync(
        Guid processRunId,
        Guid claimedStepRunId,
        string trigger,
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
                item.Id == claimedStepRunId &&
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

            artifactInputsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var configuredArtifactInputs);
            var availableBranchOutcomes = branchOutcomesByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var configuredBranchOutcomes)
                ? configuredBranchOutcomes
                    .Select(item => new DispatchBranchOutcome(item.Id, item.Key, item.Title, item.Description))
                    .ToList()
                : [];
            var requiresExplicitBranchOutcomeSelection =
                conditionalDependencyOutcomeIdsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var requiredBranchOutcomeIds) &&
                availableBranchOutcomes.Any(item => requiredBranchOutcomeIds.Contains(item.Id));
            var expectedArtifacts = await LoadExpectedArtifactsAsync(dbContext, stepRun.StepDefinitionId, cancellationToken);
            var recordedArtifactExpectationIds = existingArtifacts
                .Where(item => item.StepRunId == stepRun.Id && item.ArtifactExpectationId.HasValue)
                .Select(item => item.ArtifactExpectationId!.Value)
                .ToHashSet();
            var preparedArtifactInputs = PrepareArtifactInputsForPrompt(
                BuildResolvedArtifactInputs(
                    configuredArtifactInputs ?? [],
                    artifactExpectationsById,
                    sourceStepsById,
                    stepRunsByDefinitionId,
                    existingArtifacts),
                workspaceRoot,
                workspaceScope);

            if (stepRun.StepKind == ProcessStepKind.Subprocess)
            {
                return new DispatchCandidate(
                    run,
                    definition,
                    stepRun,
                    currentStepDefinition,
                    workBriefsByStepRunId.GetValueOrDefault(stepRun.Id),
                    Guid.Empty,
                    expectedArtifacts,
                    recordedArtifactExpectationIds,
                    preparedArtifactInputs,
                    externalReferenceKeys,
                    null,
                    null,
                    string.Empty,
                    availableBranchOutcomes,
                    requiresExplicitBranchOutcomeSelection,
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
                    expectedArtifacts,
                    recordedArtifactExpectationIds,
                    preparedArtifactInputs,
                    externalReferenceKeys,
                    null,
                    null,
                    string.Empty,
                    availableBranchOutcomes,
                    requiresExplicitBranchOutcomeSelection,
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
            var executionRuns = await executionClient.ListExecutionRunsAsync(
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

            var agentEditor = await executionClient.GetAgentEditorAsync(technicalAgentSummary.TechnicalAgentId.Value, cancellationToken);
            if (TryResolveProjectStructureAccessProjectId(run, out var projectStructureAccessProjectId) &&
                ApplyProjectStructureReadAccess(agentEditor, projectStructureAccessProjectId))
            {
                await executionClient.SaveAgentAsync(agentEditor, cancellationToken);
                logger.LogInformation(
                    "Granted project-structure read access for project {ProjectId} to technical agent {TechnicalAgentId} before dispatching process run {RunId}, step {StepRunId}.",
                    projectStructureAccessProjectId,
                    technicalAgentSummary.TechnicalAgentId.Value,
                    run.Id,
                    stepRun.Id);
            }

            stepRoleRequirementsByStepDefinitionId.TryGetValue(stepRun.StepDefinitionId, out var currentStepRoleRequirements);
            var currentAssignment = ResolveDispatchCurrentAssignment(stepRun, currentStepRoleRequirements ?? [], runAssignments);
            var currentRole = currentAssignment is null
                ? null
                : roleRequirementsById.GetValueOrDefault(currentAssignment.RoleRequirementId);
            if (ShouldReusePriorArtifactRecoveryExecutionRun(trigger))
            {
                recoveryExecutionRunId ??= ResolveArtifactRecoveryExecutionRunId(
                    stepRun,
                    executionRuns,
                    expectedArtifacts,
                    recordedArtifactExpectationIds);
            }

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
