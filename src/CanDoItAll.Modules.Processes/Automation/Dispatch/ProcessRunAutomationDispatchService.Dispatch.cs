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

                var databaseRequirementFailure = ResolveAutomationDatabaseRequirementFailure();
                if (databaseRequirementFailure is not null)
                {
                    await BlockDispatchForDatabaseRequirementAsync(candidate, databaseRequirementFailure, cancellationToken);
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

                    if (await IsRunTerminalAsync(candidate.Run.Id, cancellationToken))
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

                    if (await IsRunTerminalAsync(candidate.Run.Id, cancellationToken))
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

    private async Task<bool> IsRunTerminalAsync(
        Guid processRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var status = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.Id == processRunId)
            .Select(item => (ProcessRunStatus?)item.Status)
            .SingleOrDefaultAsync(cancellationToken);

        return status is null or ProcessRunStatus.Completed or ProcessRunStatus.Cancelled or ProcessRunStatus.Failed;
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
            logger.LogInformation(
                "Subprocess step {StepRunId} on run {RunId} is observing child run {SubprocessRunId} with status {SubprocessStatus}.",
                stepRunSnapshot.Id,
                candidate.Run.Id,
                subprocessRun.RunId,
                subprocessRun.Status);
            return;
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

    private async Task<DispatchCandidate?> LoadDispatchCandidateAsync(
        Guid processRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == processRunId, cancellationToken);
        if (run is null || run.Status is ProcessRunStatus.Completed or ProcessRunStatus.Cancelled or ProcessRunStatus.Failed)
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
