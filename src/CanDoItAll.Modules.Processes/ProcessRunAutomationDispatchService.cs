using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

public interface IProcessRunAutomationDispatchService
{
    Task DispatchAsync(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        CancellationToken cancellationToken = default);
}

internal sealed partial class ProcessRunAutomationDispatchService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IServiceScopeFactory serviceScopeFactory,
    IAiTechnicalAgentBridge technicalAgentBridge,
    IAgentFrameworkWorkspaceService workspaceService,
    IStoragePlacementService storagePlacementService,
    IWorkspacePathResolver workspacePathResolver,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IClock clock,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessRunAutomationDispatchService
{
    private const string AutomationActor = "process-automation-dispatch";
    private const int MaxExecutionAttempts = 3;
    private static readonly TimeSpan FreshInProgressRecoveryGracePeriod = TimeSpan.FromMinutes(2);
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> StepDispatchGuards = [];
    private static readonly Regex RequiredToolNameRegex = new(
        @"\b(?:workspace|browser|project_structure)_[a-z0-9_]+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DeclaredStepOutcomeRegex = new(
        @"<!--\s*PROCESS_STEP_OUTCOME\s*(?<json>\{[^\r\n]*\})\s*-->",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly string[] NegatedRequiredToolPhrases =
    [
        "do not",
        "don't",
        "must not",
        "should not",
        "shall not",
        "cannot",
        "can't",
        "never",
        "without"
    ];
    private static readonly HashSet<string> NonCriticalWorkspaceProcessToolNames =
    [
        "workspace_git_diff",
        "workspace_git_status"
    ];
    private static readonly HashSet<string> RequiredBrowserEvidenceToolNames =
    [
        "browser_console_messages",
        "browser_network_requests",
        "browser_snapshot",
        "browser_take_screenshot"
    ];
    private static readonly string[] GovernedInspectionToolNames =
    [
        "workspace_stat_path",
        "workspace_read_file"
    ];
    private static readonly string[] ImplementationProofToolNames =
    [
        "workspace_stat_path",
        "workspace_read_file",
        "workspace_dotnet_build"
    ];
    private sealed record PrefetchedProjectStructureGrounding(string PromptSummary, IReadOnlyList<string> SatisfiedToolNames)
    {
        public static PrefetchedProjectStructureGrounding Empty { get; } = new(string.Empty, []);

        public bool HasPromptSummary => !string.IsNullOrWhiteSpace(PromptSummary);
    }
    private sealed record PrefetchedArtifactInspectionGrounding(string PromptSummary, IReadOnlyList<string> SatisfiedToolNames)
    {
        public static PrefetchedArtifactInspectionGrounding Empty { get; } = new(string.Empty, []);

        public bool HasPromptSummary => !string.IsNullOrWhiteSpace(PromptSummary);
    }
    private sealed record ProjectStructureGroundingNodeData(
        string Id,
        string ParentId,
        string ObjectType,
        string ObjectSubtype,
        string Title,
        string Subtitle,
        string Status,
        string Notes,
        string MetadataJson);

    public async Task DispatchAsync(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
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
                        "Skipping stale fresh automation dispatch for run {RunId}, step {StepRunId}, status {Status}, trigger {Trigger}. Recovery worker will handle stranded execution if needed.",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        candidate.StepRun.Status,
                        NormalizeTrigger(trigger, triggerStepRunId));
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
                        continue;
                    }
                }

                try
                {
                    var executionOutcome = await ExecuteUntilSettledAsync(candidate, trigger, cancellationToken);
                    await ProjectExecutionArtifactsAsync(
                        candidate,
                        executionOutcome.Detail,
                        executionOutcome.ResponseText,
                        executionOutcome.CompletionStatus,
                        cancellationToken);

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
                        var completionResult = await TransitionStepAsync(
                            new ProcessStepTransitionRequest
                            {
                                StepRunId = candidate.StepRun.Id,
                                StepRunConcurrencyToken = stepRunSnapshot.ConcurrencyToken,
                                TargetStatus = executionOutcome.CompletionStatus,
                                Reason = executionOutcome.CompletionReason,
                                SelectedBranchOutcomeId = executionOutcome.SelectedBranchOutcomeId,
                                DecidedBy = AutomationActor,
                                SuppressAutomationDispatch = true
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
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "Process automation dispatch failed for run {RunId}, step {StepRunId}.",
                        candidate.Run.Id,
                        candidate.StepRun.Id);

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
                }
            }
            finally
            {
                dispatchGuard.Release();
            }
        }
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
            if (!stepRun.CurrentExecutorPartyId.HasValue)
            {
                continue;
            }

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
            var reusableChatSessionId = ResolveReusableAutomationChatSessionId(executionRuns);
            var summaries = await technicalAgentBridge.GetDirectorySummariesAsync([stepRun.CurrentExecutorPartyId.Value], cancellationToken);
            if (!summaries.TryGetValue(stepRun.CurrentExecutorPartyId.Value, out var technicalAgentSummary) ||
                !technicalAgentSummary.TechnicalAgentId.HasValue ||
                technicalAgentSummary.BindingStatus != AiResourceBindingStatus.Bound)
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
            return new DispatchCandidate(
                run,
                definition,
                stepRun,
                workBriefsByStepRunId.GetValueOrDefault(stepRun.Id),
                technicalAgentSummary.TechnicalAgentId.Value,
                await LoadExpectedArtifactsAsync(dbContext, stepRun.StepDefinitionId, cancellationToken),
                PrepareArtifactInputsForPrompt(
                    BuildResolvedArtifactInputs(
                        configuredArtifactInputs ?? [],
                        artifactExpectationsById,
                        sourceStepsById,
                        stepRunsByDefinitionId,
                        existingArtifacts),
                    workspaceRoot,
                    workspaceScope),
                externalReferenceKeys,
                reusableChatSessionId,
                recoveryExecutionRunId,
                availableBranchOutcomes,
                requiresExplicitBranchOutcomeSelection);
        }

        return null;
    }

    private async Task<DispatchExecutionOutcome> ExecuteUntilSettledAsync(
        DispatchCandidate candidate,
        string trigger,
        CancellationToken cancellationToken)
    {
        DispatchExecutionOutcome? finalOutcome = null;
        string? recoveryDirective = null;
        var recoverableExecutionRunId = candidate.RecoveryExecutionRunId;
        var automationChatSessionId = candidate.ChatSessionId;
        var prefetchedProjectStructureGrounding = await TryBuildProjectStructureGroundingAsync(candidate, cancellationToken);
        var prefetchedArtifactInspectionGrounding = await TryBuildArtifactInspectionGroundingAsync(candidate, cancellationToken);
        var successfulToolNamesAcrossAttempts = new HashSet<string>(
            prefetchedProjectStructureGrounding.SatisfiedToolNames,
            StringComparer.Ordinal);
        successfulToolNamesAcrossAttempts.UnionWith(prefetchedArtifactInspectionGrounding.SatisfiedToolNames);

        for (var attemptNumber = 1; attemptNumber <= MaxExecutionAttempts; attemptNumber++)
        {
            ExecutionRunDetail detail;
            Guid executionRunId;
            string responseText;

            if (attemptNumber == 1 && recoverableExecutionRunId.HasValue)
            {
                executionRunId = recoverableExecutionRunId.Value;
                detail = await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
                responseText = ResolveRecoveredExecutionResponseText(detail);
                automationChatSessionId ??= detail.Run.ChatSessionId;
                recoverableExecutionRunId = null;

                logger.LogInformation(
                    "Recovering existing AgentFramework execution run {ExecutionRunId} for stranded process step {StepRunId} on run {RunId}.",
                    executionRunId,
                    candidate.StepRun.Id,
                    candidate.Run.Id);
            }
            else
            {
                var concurrentExecution = await TryAdoptConcurrentAutomationExecutionAsync(candidate, cancellationToken);
                if (concurrentExecution is not null)
                {
                    executionRunId = concurrentExecution.ExecutionRunId;
                    detail = concurrentExecution.Detail;
                    responseText = concurrentExecution.ResponseText;
                    automationChatSessionId ??= detail.Run.ChatSessionId;

                    logger.LogInformation(
                        "Adopting concurrently-started AgentFramework execution run {ExecutionRunId} for process step {StepRunId} on run {RunId}.",
                        executionRunId,
                        candidate.StepRun.Id,
                        candidate.Run.Id);
                }
                else
                {
                    automationChatSessionId = (await workspaceService.GetOrCreateChatSessionAsync(
                        candidate.TechnicalAgentId,
                        automationChatSessionId,
                        cancellationToken)).Id;
                    var executionResult = await workspaceService.ExecuteRunAsync(
                        new ExecutionRunRequest(
                            candidate.TechnicalAgentId,
                            BuildExecutionPromptCore(
                                candidate,
                                recoveryDirective,
                                prefetchedProjectStructureGrounding.HasPromptSummary
                                    ? prefetchedProjectStructureGrounding.PromptSummary
                                    : null,
                                prefetchedArtifactInspectionGrounding.HasPromptSummary
                                    ? prefetchedArtifactInspectionGrounding.PromptSummary
                                    : null),
                            ChatSessionId: automationChatSessionId,
                            Context: new ExecutionInvocationContext(
                                SourceKind: "process-step",
                                SourceId: candidate.StepRun.Id.ToString("D"),
                                CorrelationId: BuildCorrelationId(candidate.StepRun.Id),
                                CausationId: string.IsNullOrWhiteSpace(trigger)
                                    ? string.Empty
                                    : trigger.Trim(),
                                RequestedBy: AutomationActor,
                                RequestedByKind: "system",
                                MetadataJson: BuildExecutionMetadataJson(candidate, trigger),
                                ProcessRunId: candidate.Run.Id.ToString("D"),
                                ProcessStepId: candidate.StepRun.Id.ToString("D")),
                            AutoApprovePendingToolCalls: true),
                        cancellationToken);
                    executionRunId = executionResult.ExecutionRunId;
                    automationChatSessionId ??= executionResult.ChatSessionId;
                    detail = await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
                    responseText = ResolvePreferredExecutionResponseText(candidate, executionResult.ResponseText, detail);
                }
            }

            successfulToolNamesAcrossAttempts.UnionWith(ResolveSuccessfulToolNames(detail));
            var missingRequiredTools = ResolveMissingRequiredToolExecutionsWithCarryForward(
                candidate,
                detail,
                successfulToolNamesAcrossAttempts);
            var unresolvedCriticalToolFailures = ResolveUnresolvedCriticalToolFailures(detail);
            var completionStatus = ResolveCompletionStatusWithCarryForward(
                candidate,
                detail,
                successfulToolNamesAcrossAttempts,
                responseText);
            var completionReason = BuildCompletionReasonWithCarryForward(
                candidate,
                detail,
                candidate.StepRun.Title,
                successfulToolNamesAcrossAttempts,
                responseText);
            var selectedBranchOutcomeId = ResolveSelectedBranchOutcomeId(
                candidate,
                completionStatus,
                responseText);

            if (attemptNumber > 1)
            {
                completionReason = completionStatus == ProcessStepRunStatus.Completed
                    ? $"{completionReason} Recovered on attempt {attemptNumber} of {MaxExecutionAttempts}."
                    : $"{completionReason} Recovery attempt {attemptNumber} of {MaxExecutionAttempts}.";
            }

            finalOutcome = new DispatchExecutionOutcome(
                detail,
                responseText,
                completionStatus,
                completionReason,
                missingRequiredTools,
                attemptNumber,
                selectedBranchOutcomeId);

            var providerRepair = await TryRepairAssignedAgentProvidersAsync(
                candidate,
                detail,
                responseText,
                attemptNumber,
                cancellationToken);
            if (providerRepair is not null)
            {
                logger.LogWarning(
                    "Recovered provider failure for process run {RunId}, step {StepRunId} by switching {AffectedAgentCount} assigned internal agent(s) from '{FailedProviderName}' to '{FallbackProviderName}' ({FallbackModel}). Failure summary: {FailureSummary}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    providerRepair.AffectedAgentCount,
                    providerRepair.FailedProviderName,
                    providerRepair.FallbackProviderName,
                    providerRepair.FallbackModel,
                    providerRepair.FailureSummary);

                automationChatSessionId = null;
                recoveryDirective = BuildProviderRepairRecoveryDirective(
                    BuildRecoveryDirective(
                        candidate,
                        detail,
                        responseText,
                        missingRequiredTools,
                        unresolvedCriticalToolFailures,
                        attemptNumber),
                    providerRepair);
                continue;
            }

            if (!ShouldRetryIncompleteSuccessfulRun(candidate, detail, responseText, missingRequiredTools, attemptNumber))
            {
                return finalOutcome;
            }

            logger.LogWarning(
                "AgentFramework run {ExecutionRunId} ended with unresolved execution work for process run {RunId}, step {StepRunId}. Missing tools: {MissingTools}. Critical failures: {CriticalFailures}. Retrying attempt {NextAttempt}/{MaxAttempts}.",
                executionRunId,
                candidate.Run.Id,
                candidate.StepRun.Id,
                missingRequiredTools.Count == 0
                    ? "none"
                    : string.Join(", ", missingRequiredTools),
                unresolvedCriticalToolFailures.Count == 0
                    ? "none"
                    : string.Join(
                        "; ",
                        unresolvedCriticalToolFailures
                            .Take(2)
                            .Select(item => $"{item.ToolName}: {item.ExitSummary}")),
                attemptNumber + 1,
                MaxExecutionAttempts);

            // Start recovery attempts on a fresh chat session so stale context or provider-side errors
            // from the previous attempt do not poison the next governed retry.
            automationChatSessionId = null;

            recoveryDirective = BuildRecoveryDirective(
                candidate,
                detail,
                responseText,
                missingRequiredTools,
                unresolvedCriticalToolFailures,
                attemptNumber);
        }

        return finalOutcome
               ?? throw new InvalidOperationException($"No AgentFramework execution outcome was captured for process step '{candidate.StepRun.Id:D}'.");
    }

    private async Task<ProviderRepairOutcome?> TryRepairAssignedAgentProvidersAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        int attemptNumber,
        CancellationToken cancellationToken)
    {
        if (attemptNumber >= MaxExecutionAttempts ||
            !TryResolveRecoverableProviderFailure(detail, responseText, out var failureSummary))
        {
            return null;
        }

        var agents = await workspaceService.ListAgentsAsync(includeTemplates: false, cancellationToken);
        var agentsById = agents.ToDictionary(item => item.Id);
        if (!agentsById.TryGetValue(candidate.TechnicalAgentId, out var currentAgent) ||
            !currentAgent.ProviderProfileId.HasValue)
        {
            return null;
        }

        var failedProviderId = currentAgent.ProviderProfileId.Value;
        var providers = await workspaceService.ListProvidersAsync(cancellationToken);
        var failedProviderName = providers.FirstOrDefault(item => item.Id == failedProviderId)?.Name;
        var fallbackResolution = await ResolveHealthyFallbackProviderAsync(
            providers,
            failedProviderId,
            cancellationToken);
        if (fallbackResolution is null)
        {
            logger.LogWarning(
                "Process run {RunId}, step {StepRunId} detected a recoverable provider failure, but no healthy fallback provider was available for technical agent {TechnicalAgentId}. Failure summary: {FailureSummary}",
                candidate.Run.Id,
                candidate.StepRun.Id,
                candidate.TechnicalAgentId,
                failureSummary);
            return null;
        }

        var assignedPartyIds = await LoadAssignedPartyIdsAsync(
            candidate.Run.Id,
            candidate.StepRun.CurrentExecutorPartyId,
            cancellationToken);
        var assignedSummaries = assignedPartyIds.Count == 0
            ? new Dictionary<Guid, AiTechnicalAgentDirectorySummary>()
            : await technicalAgentBridge.GetDirectorySummariesAsync(assignedPartyIds, cancellationToken);
        var technicalAgentIdsToRepair = assignedSummaries.Values
            .Where(summary => summary.TechnicalAgentId.HasValue)
            .Select(summary => summary.TechnicalAgentId!.Value)
            .Distinct()
            .Where(agentId =>
                agentsById.TryGetValue(agentId, out var assignedAgent) &&
                assignedAgent.ProviderProfileId == failedProviderId)
            .ToHashSet();
        technicalAgentIdsToRepair.Add(candidate.TechnicalAgentId);

        var affectedAgentCount = 0;
        foreach (var technicalAgentId in technicalAgentIdsToRepair)
        {
            try
            {
                var editor = await workspaceService.GetAgentEditorAsync(technicalAgentId, cancellationToken);
                if (editor.ProviderProfileId == fallbackResolution.Provider.Id &&
                    string.Equals(editor.Model, fallbackResolution.Model, StringComparison.Ordinal))
                {
                    affectedAgentCount++;
                    continue;
                }

                editor.ProviderProfileId = fallbackResolution.Provider.Id;
                editor.Model = fallbackResolution.Model;
                await workspaceService.SaveAgentAsync(editor, cancellationToken);
                affectedAgentCount++;
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Failed to switch technical agent {TechnicalAgentId} to fallback provider '{ProviderName}' while recovering process run {RunId}, step {StepRunId}.",
                    technicalAgentId,
                    fallbackResolution.Provider.Name,
                    candidate.Run.Id,
                    candidate.StepRun.Id);

                if (technicalAgentId == candidate.TechnicalAgentId)
                {
                    return null;
                }
            }
        }

        if (affectedAgentCount == 0)
        {
            return null;
        }

        return new ProviderRepairOutcome(
            failedProviderName ?? detail.Run.ProviderName,
            fallbackResolution.Provider.Name,
            fallbackResolution.Model,
            affectedAgentCount,
            failureSummary);
    }

    private async Task<ProviderFallbackResolution?> ResolveHealthyFallbackProviderAsync(
        IReadOnlyList<ProviderProfile> providers,
        Guid failedProviderId,
        CancellationToken cancellationToken)
    {
        foreach (var provider in OrderFallbackProviders(providers, failedProviderId))
        {
            ProviderHealthResult healthResult;
            try
            {
                healthResult = await workspaceService.TestProviderAsync(provider.Id, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogInformation(
                    exception,
                    "Fallback provider probe for '{ProviderName}' failed while evaluating process execution recovery.",
                    provider.Name);
                continue;
            }

            if (!healthResult.Success)
            {
                logger.LogInformation(
                    "Skipping fallback provider '{ProviderName}' because its health probe failed: {Summary}",
                    provider.Name,
                    healthResult.Summary);
                continue;
            }

            return new ProviderFallbackResolution(
                provider,
                ResolveFallbackProviderModel(provider, healthResult),
                healthResult.Summary);
        }

        return null;
    }

    private async Task<IReadOnlyList<Guid>> LoadAssignedPartyIdsAsync(
        Guid processRunId,
        Guid? currentExecutorPartyId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var partyIds = await dbContext.Set<ProcessRunAssignment>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == processRunId && item.PartyId.HasValue && !item.IsCapabilityGap)
            .Select(item => item.PartyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (currentExecutorPartyId.HasValue && !partyIds.Contains(currentExecutorPartyId.Value))
        {
            partyIds.Add(currentExecutorPartyId.Value);
        }

        return partyIds;
    }

    private async Task ProjectExecutionArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
    {
        var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
        foreach (var artifact in detail.Artifacts)
        {
            if (IsTransientExecutionArtifact(artifact))
            {
                logger.LogDebug(
                    "Skipping transient execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId}, path {RelativePath}.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id,
                    artifact.RelativePath);
                continue;
            }

            var matchedExpectation = ResolveArtifactExpectation(candidate, artifact);
            var externalReferenceKey = BuildExternalReferenceKey(artifact);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var fullPath = Path.GetFullPath(Path.Combine(workspaceRoot, artifact.RelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, fullPath) || !File.Exists(fullPath))
            {
                logger.LogDebug(
                    "Skipping execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId} because the file path is unavailable.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id);
                continue;
            }

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Execution artifact {ArtifactId} could not be read for process run {RunId}.",
                    artifact.Id,
                    candidate.Run.Id);
                continue;
            }

            var placement = await storagePlacementService.PlaceAsync(
                new StoragePlacementRequest(
                    Path.GetFileName(fullPath),
                    string.IsNullOrWhiteSpace(artifact.ContentType)
                        ? "application/octet-stream"
                        : artifact.ContentType,
                    content,
                    StorageUsagePurpose.Evidence,
                    ResolveStorageContentKind(artifact.ContentType, fullPath),
                    ProjectId: candidate.Run.ProjectId,
                    RelativePathHint: BuildStorageRelativePath(candidate, artifact)),
                cancellationToken);

            var recordResult = await RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    ArtifactExpectationId = matchedExpectation?.Id,
                    ArtifactKind = matchedExpectation?.ArtifactKind ?? ResolveProcessArtifactKind(candidate, artifact),
                    Title = matchedExpectation?.Title ?? BuildArtifactTitle(artifact),
                    TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                    SensitivityLevel = matchedExpectation?.SensitivityLevel ?? ProcessSensitivityLevel.Internal,
                    ProvenanceSummary = $"Projected from AgentFramework execution run {detail.Run.Id:D} artifact '{artifact.RelativePath}'.",
                    AllowedFutureUsageSummary = "Process evidence and audit review.",
                    ReviewSummary = string.IsNullOrWhiteSpace(artifact.Summary)
                        ? detail.Run.ResultSummary
                        : artifact.Summary,
                    ManagedStoragePath = placement.RelativePath,
                    ExternalReferenceKey = externalReferenceKey
                },
                cancellationToken);
            if (recordResult.IsSuccess)
            {
                candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            }
            else
            {
                logger.LogWarning(
                    "Process artifact projection failed for run {RunId}, step {StepRunId}, artifact {ArtifactId}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id,
                    string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
            }
        }

        await ProjectResponseTextArtifactsAsync(
            candidate,
            detail,
            responseText,
            workspaceRoot,
            completionStatus,
            cancellationToken);
        await ProjectProviderNativeBrowserArtifactsAsync(candidate, detail, workspaceRoot, cancellationToken);
        await EnsureDecisionArtifactsForCompletedStepAsync(
            candidate,
            detail,
            responseText,
            completionStatus,
            cancellationToken);
    }

    private async Task EnsureDecisionArtifactsForCompletedStepAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
    {
        if (completionStatus != ProcessStepRunStatus.Completed || candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        foreach (var expectedArtifact in candidate.ExpectedArtifacts.Where(ShouldAutoRecordCompletedDecisionArtifact))
        {
            var externalReferenceKey = BuildCompletedDecisionArtifactExternalReferenceKey(
                candidate.StepRun.Id,
                expectedArtifact.Id);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var recordResult = await RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = candidate.Run.Id,
                    StepRunId = candidate.StepRun.Id,
                    ArtifactExpectationId = expectedArtifact.Id,
                    ArtifactKind = expectedArtifact.ArtifactKind,
                    Title = expectedArtifact.Title,
                    TrustStatus = ResolveCompletedDecisionArtifactTrustStatus(expectedArtifact.TrustRequirement),
                    SensitivityLevel = expectedArtifact.SensitivityLevel,
                    ProvenanceSummary = BuildCompletedDecisionArtifactProvenanceSummary(candidate, detail),
                    AllowedFutureUsageSummary = string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                        ? "Reusable for audit, release replay, and governance tuning."
                        : expectedArtifact.AllowedFutureUsageSummary,
                    ReviewSummary = BuildCompletedDecisionArtifactReviewSummary(candidate, detail, responseText, expectedArtifact),
                    ExternalReferenceKey = externalReferenceKey
                },
                cancellationToken);
            if (recordResult.IsSuccess)
            {
                candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            }
            else
            {
                logger.LogWarning(
                    "Completed-step decision artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
            }
        }
    }

    private async Task ProjectResponseTextArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        string workspaceRoot,
        ProcessStepRunStatus completionStatus,
        CancellationToken cancellationToken)
    {
        if (!ShouldProjectResponseTextArtifacts(detail.Run, completionStatus) ||
            candidate.ExpectedArtifacts.Count == 0 ||
            string.IsNullOrWhiteSpace(responseText))
        {
            return;
        }

        var normalizedResponseText = responseText.Trim().ReplaceLineEndings(Environment.NewLine);
        if (string.IsNullOrWhiteSpace(normalizedResponseText))
        {
            return;
        }

        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));

        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (!TryResolveResponseTextArtifactRelativePath(
                    candidate,
                    workspaceScope,
                    expectedArtifact,
                    out var projectedRelativePath))
            {
                continue;
            }

            if (detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var externalReferenceKey = BuildResponseTextArtifactExternalReferenceKey(detail.Run.Id, projectedRelativePath);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                continue;
            }

            var targetFullPath = Path.GetFullPath(Path.Combine(
                workspaceRoot,
                projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, targetFullPath))
            {
                logger.LogWarning(
                    "Skipping response-text artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because target path '{ExpectedPath}' resolves outside the workspace root.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    projectedRelativePath);
                continue;
            }

            try
            {
                var targetDirectory = Path.GetDirectoryName(targetFullPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                var persistedResponseText = normalizedResponseText.EndsWith(Environment.NewLine, StringComparison.Ordinal)
                    ? normalizedResponseText
                    : normalizedResponseText + Environment.NewLine;
                await File.WriteAllTextAsync(targetFullPath, persistedResponseText, Encoding.UTF8, cancellationToken);

                var content = Encoding.UTF8.GetBytes(persistedResponseText);
                var syntheticArtifact = new ExecutionArtifactRecord(
                    Guid.NewGuid(),
                    detail.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    GuessContentTypeFromPath(targetFullPath),
                    "assistant-response",
                    "Projected the final assistant response into the required managed text artifact path.",
                    DateTimeOffset.UtcNow);

                var placement = await storagePlacementService.PlaceAsync(
                    new StoragePlacementRequest(
                        Path.GetFileName(targetFullPath),
                        syntheticArtifact.ContentType,
                        content,
                        StorageUsagePurpose.Evidence,
                        ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                        ProjectId: candidate.Run.ProjectId,
                        RelativePathHint: BuildStorageRelativePath(candidate, syntheticArtifact)),
                    cancellationToken);

                var recordResult = await RecordArtifactAsync(
                    new ProcessArtifactRecordRequest
                    {
                        ProcessRunId = candidate.Run.Id,
                        StepRunId = candidate.StepRun.Id,
                        ArtifactExpectationId = expectedArtifact.Id,
                        ArtifactKind = expectedArtifact.ArtifactKind,
                        Title = expectedArtifact.Title,
                        TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                        SensitivityLevel = expectedArtifact.SensitivityLevel,
                        ProvenanceSummary = $"Projected from the final assistant response for AgentFramework execution run {detail.Run.Id:D}.",
                        AllowedFutureUsageSummary = string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                            ? "Process evidence and audit review."
                            : expectedArtifact.AllowedFutureUsageSummary,
                        ReviewSummary = syntheticArtifact.Summary,
                        ManagedStoragePath = placement.RelativePath,
                        ExternalReferenceKey = externalReferenceKey
                    },
                    cancellationToken);
                if (recordResult.IsSuccess)
                {
                    candidate.ExternalReferenceKeys.Add(externalReferenceKey);
                }
                else
                {
                    logger.LogWarning(
                        "Response-text artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        expectedArtifact.Title,
                        string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Response-text artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title);
            }
        }
    }

    private async Task ProjectProviderNativeBrowserArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        if (candidate.ExpectedArtifacts.Count == 0 || string.IsNullOrWhiteSpace(detail.Run.SerializedSessionStateJson))
        {
            return;
        }

        var browserOutputsByToolName = ResolveSuccessfulSessionToolOutputFiles(detail.Run.SerializedSessionStateJson);
        if (browserOutputsByToolName.Count == 0)
        {
            return;
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail);
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return;
        }

        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));

        foreach (var expectedArtifact in candidate.ExpectedArtifacts)
        {
            if (!TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
            {
                continue;
            }

            if (detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, artifact) == expectedArtifact.Id))
            {
                continue;
            }

            var requiredToolName = ResolveProviderNativeBrowserToolName(expectedRelativePath);
            if (string.IsNullOrWhiteSpace(requiredToolName) ||
                !browserOutputsByToolName.TryGetValue(requiredToolName, out var outputFileNames))
            {
                continue;
            }

            var matchedOutputFileName = outputFileNames.FirstOrDefault(outputFileName =>
                MatchesExpectedBrowserOutputFile(expectedRelativePath, outputFileName));
            if (string.IsNullOrWhiteSpace(matchedOutputFileName))
            {
                continue;
            }

            var sourceFullPath = Path.GetFullPath(Path.Combine(
                browserWorkingDirectory,
                matchedOutputFileName.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, sourceFullPath) || !File.Exists(sourceFullPath))
            {
                logger.LogDebug(
                    "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because source file {SourcePath} is unavailable.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    sourceFullPath);
                continue;
            }

            var projectedRelativePath = ResolveScopedManagedRelativePath(workspaceScope, expectedRelativePath);
            var targetFullPath = Path.GetFullPath(Path.Combine(
                workspaceRoot,
                projectedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinWorkspace(workspaceRoot, targetFullPath))
            {
                logger.LogWarning(
                    "Skipping provider-native browser artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because target path '{ExpectedPath}' resolves outside the workspace root.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title,
                    projectedRelativePath);
                continue;
            }

            try
            {
                var targetDirectory = Path.GetDirectoryName(targetFullPath);
                if (!string.IsNullOrWhiteSpace(targetDirectory))
                {
                    Directory.CreateDirectory(targetDirectory);
                }

                if (!string.Equals(sourceFullPath, targetFullPath, StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourceFullPath, targetFullPath, overwrite: true);
                }

                var content = await File.ReadAllBytesAsync(targetFullPath, cancellationToken);
                var syntheticArtifact = new ExecutionArtifactRecord(
                    Guid.NewGuid(),
                    detail.Run.Id,
                    "generated-output",
                    expectedArtifact.Title,
                    projectedRelativePath,
                    GuessContentTypeFromPath(targetFullPath),
                    requiredToolName,
                    $"Projected provider-native browser output '{matchedOutputFileName}' into the required managed artifact path.",
                    DateTimeOffset.UtcNow);
                var externalReferenceKey = BuildProviderNativeBrowserArtifactExternalReferenceKey(
                    detail.Run.Id,
                    projectedRelativePath);
                if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
                {
                    continue;
                }

                var placement = await storagePlacementService.PlaceAsync(
                    new StoragePlacementRequest(
                        Path.GetFileName(targetFullPath),
                        syntheticArtifact.ContentType,
                        content,
                        StorageUsagePurpose.Evidence,
                        ResolveStorageContentKind(syntheticArtifact.ContentType, targetFullPath),
                        ProjectId: candidate.Run.ProjectId,
                        RelativePathHint: BuildStorageRelativePath(candidate, syntheticArtifact)),
                    cancellationToken);

                var recordResult = await RecordArtifactAsync(
                    new ProcessArtifactRecordRequest
                    {
                        ProcessRunId = candidate.Run.Id,
                        StepRunId = candidate.StepRun.Id,
                        ArtifactExpectationId = expectedArtifact.Id,
                        ArtifactKind = expectedArtifact.ArtifactKind,
                        Title = expectedArtifact.Title,
                        TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
                        SensitivityLevel = expectedArtifact.SensitivityLevel,
                        ProvenanceSummary = $"Projected from provider-native browser output '{matchedOutputFileName}' for AgentFramework execution run {detail.Run.Id:D}.",
                        AllowedFutureUsageSummary = "Process evidence and audit review.",
                        ReviewSummary = syntheticArtifact.Summary,
                        ManagedStoragePath = placement.RelativePath,
                        ExternalReferenceKey = externalReferenceKey
                    },
                    cancellationToken);
                if (recordResult.IsSuccess)
                {
                    candidate.ExternalReferenceKeys.Add(externalReferenceKey);
                }
                else
                {
                    logger.LogWarning(
                        "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}. Errors: {Errors}",
                        candidate.Run.Id,
                        candidate.StepRun.Id,
                        expectedArtifact.Title,
                        string.Join(" | ", recordResult.Errors.Select(error => error.Message)));
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(
                    exception,
                    "Provider-native browser artifact projection failed for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle}.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title);
            }
        }
    }

    private async Task<Result> TransitionStepAsync(
        ProcessStepTransitionRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.TransitionStepAsync(request, cancellationToken);
    }

    private async Task<StepRunTransitionSnapshot?> LoadStepRunTransitionSnapshotAsync(
        Guid stepRunId,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.Id == stepRunId)
            .Select(item => new StepRunTransitionSnapshot(item.Id, item.Status, item.ConcurrencyToken))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Result<Guid>> RecordArtifactAsync(
        ProcessArtifactRecordRequest request,
        CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var processesService = scope.ServiceProvider.GetRequiredService<ProcessesService>();
        return await processesService.RecordArtifactAsync(request, cancellationToken);
    }

    private static string BuildExecutionPrompt(DispatchCandidate candidate)
    {
        return BuildExecutionPromptCore(candidate, null, null, null);
    }

    private static string BuildExecutionPromptCore(
        DispatchCandidate candidate,
        string? recoveryDirective,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
    {
        var workBrief = candidate.WorkBrief;
        ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out var projectStructureContext);
        var summarizedTriggerReason = ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason);
        var builder = new StringBuilder();
        builder.AppendLine("You are executing a CanDoItAll process step.");
        builder.AppendLine();
        builder.AppendLine($"Process: {candidate.Definition.Name}");
        builder.AppendLine($"Run: {candidate.Run.Name}");
        builder.AppendLine($"Step: {candidate.StepRun.Title}");
        builder.AppendLine($"Executor: {candidate.StepRun.CurrentExecutorName}");
        builder.AppendLine();
        builder.AppendLine("Run objective:");
        builder.AppendLine(string.IsNullOrWhiteSpace(summarizedTriggerReason)
            ? string.IsNullOrWhiteSpace(candidate.Definition.Summary)
                ? candidate.Definition.ValueStatement
                : candidate.Definition.Summary
            : summarizedTriggerReason);
        builder.AppendLine();
        if (projectStructureContext is not null)
        {
            builder.AppendLine("Project structure context:");
            builder.AppendLine(ProcessProjectStructureContextFormatter.BuildPromptSummary(projectStructureContext));
            builder.AppendLine();
            builder.AppendLine("Project structure execution rules:");
            builder.AppendLine(string.IsNullOrWhiteSpace(projectStructureGroundingSummary)
                ? $"- Use `project_structure_read` early in this step for project `{projectStructureContext.ProjectId:D}` so you inspect the live project graph instead of relying only on the selected node label."
                : "- The dispatcher already fetched a live project-structure snapshot for this selected branch and included it below. Treat that grounding as current fact, and call `project_structure_read` yourself if you need a broader or narrower slice before you conclude.");
            builder.AppendLine("- Do not assume the selected task node contains every requirement. Carry forward concrete stack choices, output directories, examples, UI expectations, and acceptance notes that appear on related root or sibling project-structure nodes.");
            builder.AppendLine("- If the project structure names a concrete output directory outside the managed workspace, do not silently relocate the deliverable. Use a controlled local execution path when necessary, and record the exact external target in the artifacts you write.");
            builder.AppendLine("- Workspace file and dotnet tools cannot use a raw absolute external path like `C:\\target\\app` directly. Convert it to the mapped alias `external-target/C/target/app` when you call `workspace_create_directory`, `workspace_write_file`, `workspace_read_file`, `workspace_stat_path`, `workspace_dotnet_new`, or `workspace_dotnet_build`.");
            builder.AppendLine("- The mapped `external-target/<drive>/...` alias resolves to the real external target. Do not create a shadow copy in a different workspace folder.");
            builder.AppendLine("- Treat missing project-structure inspection as incomplete work for this step.");
            builder.AppendLine("- If project_structure_read reveals an exact external output directory for the selected work node, scaffold and implement in that exact location during this step instead of returning a note that the code does not exist yet.");
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(projectStructureGroundingSummary))
        {
            builder.AppendLine("Live project structure grounding:");
            builder.AppendLine(projectStructureGroundingSummary.Trim());
            builder.AppendLine();
        }

        builder.AppendLine("Work brief:");
        builder.AppendLine(workBrief?.WorkBriefText ?? "No work brief was captured for this step.");
        builder.AppendLine();
        builder.AppendLine("Handoff summary:");
        builder.AppendLine(workBrief?.HandoffSummary ?? "None");
        builder.AppendLine();
        builder.AppendLine("Expected outcome:");
        builder.AppendLine(workBrief?.ExpectedOutcome ?? "Complete the step and produce durable evidence artifacts.");
        builder.AppendLine();
        builder.AppendLine("Evidence expectation:");
        builder.AppendLine(workBrief?.EvidenceExpectationSummary ?? "Save any relevant evidence artifacts inside the workspace.");
        builder.AppendLine();
        builder.AppendLine("Required output artifacts:");
        builder.AppendLine(BuildExpectedArtifactSummary(candidate.ExpectedArtifacts));
        builder.AppendLine();
        builder.AppendLine("Upstream artifacts:");
        builder.AppendLine(BuildArtifactInputSummary(candidate.ArtifactInputs));
        builder.AppendLine();
        if (!string.IsNullOrWhiteSpace(artifactInspectionGroundingSummary))
        {
            builder.AppendLine("Prefetched governed artifact grounding:");
            builder.AppendLine(artifactInspectionGroundingSummary.Trim());
            builder.AppendLine();
        }

        if (candidate.BranchOutcomes.Count > 0)
        {
            builder.AppendLine("Available branch outcomes:");
            builder.AppendLine(BuildBranchOutcomePromptSummary(candidate.BranchOutcomes));
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(recoveryDirective))
        {
            builder.AppendLine("Recovery directive:");
            builder.AppendLine(recoveryDirective.Trim());
            builder.AppendLine();
        }

        var governedInspectionPaths = ResolveGovernedInspectionPaths(candidate.ExpectedArtifacts);
        var artifactInputInspectionPaths = ResolveArtifactInputInspectionPaths(candidate.ArtifactInputs);

        if (RequiresGovernedInspection(candidate.StepRun) || RequiresDurableTextArtifactWrite(candidate))
        {
            builder.AppendLine("Governed evidence rules:");
            if (RequiresGovernedInspection(candidate.StepRun))
            {
                if (!string.IsNullOrWhiteSpace(artifactInspectionGroundingSummary))
                {
                    builder.AppendLine("- The dispatcher already inspected upstream governed artifact files and included the verified paths and excerpts below. Treat that grounding as current evidence, and call workspace_stat_path or workspace_read_file again only when you need broader or fresher inspection before you conclude.");
                }

                builder.AppendLine("- Use workspace_stat_path and workspace_read_file on the concrete workspace files or durable artifacts you cite as evidence. Do not rely only on summaries, RAG snippets, or prior notes.");
                if (governedInspectionPaths.StatPaths.Count > 0)
                {
                    builder.AppendLine($"- Before you conclude, use workspace_stat_path on these governed output paths after they exist: {FormatPromptPathList(governedInspectionPaths.StatPaths)}.");
                }

                if (governedInspectionPaths.ReadPaths.Count > 0)
                {
                    builder.AppendLine($"- Before you conclude, use workspace_read_file on these text-based governed artifacts after they exist: {FormatPromptPathList(governedInspectionPaths.ReadPaths)}.");
                }

                if (governedInspectionPaths.StatPaths.Count > 0 && governedInspectionPaths.ReadPaths.Count == 0)
                {
                    builder.AppendLine("- If the governed artifacts are binary-only, stat the binary files and read the nearest durable markdown, log, JSON, YAML, or text artifact that explains or imports them before you conclude.");
                }

                if (artifactInputInspectionPaths.StatPaths.Count > 0)
                {
                    builder.AppendLine($"- Use workspace_stat_path on these upstream durable artifact paths while you review the inherited evidence: {FormatPromptPathList(artifactInputInspectionPaths.StatPaths)}.");
                }

                if (artifactInputInspectionPaths.ReadPaths.Count > 0)
                {
                    builder.AppendLine($"- Use workspace_read_file on these upstream durable text artifacts before you conclude: {FormatPromptPathList(artifactInputInspectionPaths.ReadPaths)}.");
                }
            }

            if (RequiresDurableTextArtifactWrite(candidate))
            {
                builder.AppendLine("- Use workspace_write_file to write required markdown or text artifacts at their governed managed paths instead of relying on response projection.");
            }

            builder.AppendLine();
        }

        builder.AppendLine("Execution rules:");
        builder.AppendLine("- Complete the actual work described in the work brief and expected outcome before writing summary artifacts.");
        builder.AppendLine("- Required output artifacts are evidence of completed work. They do not replace code changes, runnable outputs, tests, screenshots, or other concrete deliverables.");
        builder.AppendLine("- Do not execute helper scripts, app launches, browser proof, release rollout, or other side actions unless the current step contract or required artifacts explicitly call for them.");
        builder.AppendLine("- Paths under artifacts/, output/, integration-map/, and data/ are managed workspace aliases for the current scope. Use them directly, and create missing managed directories or files when the step contract requires them.");
        builder.AppendLine("- Treat run-level paths and planned solution targets as context unless the current step contract explicitly tells you to create, inspect, build, test, launch, or review them. Only then must that concrete output exist before you conclude.");
        builder.AppendLine("- If the current step contract describes greenfield implementation or gives you a bootstrap or init script, missing solution or project files are expected pre-bootstrap state, not a blocker. Run the bootstrap or init step first, then inspect the scaffolded files and continue.");
        builder.AppendLine("- Do not claim that planned scaffold targets are missing deliverables when the current step contract explicitly tells you to create, bootstrap, or scaffold them in this step.");
        builder.AppendLine("- If a required build, test, launch, browser check, or artifact import fails, inspect the real diagnostics, fix the underlying problem, and rerun the same required validation before you conclude. Do not treat the first failed validation as acceptable end-state evidence.");
        builder.AppendLine("- Do not stop after inspection, reconnaissance, bootstrap confirmation, or a next-steps summary if required tools, concrete deliverables, or required artifacts are still missing.");
        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("- Because this is an implementation step, create the real scaffold or code now. A markdown change set alone is not a completed implementation.");
            builder.AppendLine("- Before you conclude this implementation step, use `workspace_stat_path` on the concrete solution, project, and source paths you created or changed, and use `workspace_read_file` on at least one concrete project or source file from that implementation.");
            builder.AppendLine("- Run `workspace_dotnet_build` against the implemented solution or project before you claim the scaffold or code is build-ready.");
            if (artifactInputInspectionPaths.StatPaths.Count > 0 || artifactInputInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine("- Before you implement against inherited requirements or architecture notes, inspect the upstream durable artifacts directly instead of relying only on their summaries.");
                if (artifactInputInspectionPaths.StatPaths.Count > 0)
                {
                    builder.AppendLine($"- Use `workspace_stat_path` on these upstream durable artifact paths before you code against them: {FormatPromptPathList(artifactInputInspectionPaths.StatPaths)}.");
                }

                if (artifactInputInspectionPaths.ReadPaths.Count > 0)
                {
                    builder.AppendLine($"- Use `workspace_read_file` on these upstream durable text artifacts before you code or conclude: {FormatPromptPathList(artifactInputInspectionPaths.ReadPaths)}.");
                }
            }

            builder.AppendLine("- If the solution or project files do not exist yet, bootstrap them now with `workspace_dotnet_new` or an approved local helper path instead of hand-writing only loose source files.");
            builder.AppendLine("- Prefer `workspace_dotnet_new` over hand-written `.csproj` or `.sln` files when you are bootstrapping a greenfield .NET solution.");
            builder.AppendLine("- When you bootstrap with `workspace_dotnet_new`, explicitly request a supported target framework such as `net10.0` instead of accepting an older template default.");
            builder.AppendLine("- If you must write a new `.csproj` manually, choose a target framework supported by this workspace and repo baseline. For this repository, prefer `net10.0` unless the project structure or existing solution explicitly requires another target.");
            builder.AppendLine("- If you create browser-facing UI files such as `.razor`, `.cshtml`, or `wwwroot` assets, scaffold a runnable web host with the required startup entrypoint. Do not leave browser UI inside a plain class library or non-host project.");
            builder.AppendLine("- If the inherited requirements or project structure describe a browser-validated Blazor or web app, leave a runnable browser surface for downstream QA instead of concluding with service-only or library-only output.");
            builder.AppendLine("- If no concrete solution, project, or source files exist yet, do not return Completed.");
            if (projectStructureContext is not null)
            {
                builder.AppendLine("- If the project structure sends you to an external target directory, map that directory to `external-target/<drive>/...`, scaffold the real solution there, inspect those mapped paths, and run `workspace_dotnet_build` against that mapped solution or project.");
                builder.AppendLine("- Use `workspace_pwsh_run_script` only when you need a controlled helper command to bootstrap or verify the exact external target; otherwise stay on the mapped `external-target/...` path with the workspace tools.");
            }
        }

        if (RequiresConcreteImplementationReview(candidate))
        {
            builder.AppendLine("- Because this review step depends on real implementation, inspect actual solution, project, or source files in addition to managed artifacts before you conclude.");
            builder.AppendLine("- If the implementation artifacts describe a scaffold or build result that the workspace does not contain, return Blocked with the missing concrete paths instead of approving integration readiness.");
        }

        builder.AppendLine("- End your final response with exactly one HTML comment in this format: <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed|Blocked|Failed|WaitingApproval|Refused\",\"reason\":\"short concrete reason\"} -->.");
        if (candidate.BranchOutcomes.Count > 0)
        {
            builder.AppendLine("- If this step completes onto a specific downstream branch, include the exact branchOutcomeKey from the available branch outcomes, for example <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"short concrete reason\",\"branchOutcomeKey\":\"approved\"} -->.");
        }

        builder.AppendLine("- Use status Completed only when the actual work is done, the concrete deliverable exists, required validation passed, and the next step may proceed.");
        builder.AppendLine("- Use status Blocked when unresolved defects, missing proof, rejected approval, or required remediation mean the next step must not proceed yet.");
        builder.AppendLine("- Use status Failed only when tool, execution, or environment failure prevented you from producing a governed step result.");
        builder.Append("Before concluding, create one durable workspace artifact for every required output listed above. Do not ask for confirmation, permission, or a follow-up reply before writing required artifacts. If a required artifact is a text or markdown file you can produce now, write it yourself with workspace tools instead of drafting it in chat. If required upstream artifacts are missing or the concrete deliverable does not exist, stop and say so explicitly. Keep the response concise and mention what you completed.");
        return builder.ToString();
    }

    private static string BuildCorrelationId(Guid stepRunId)
    {
        return $"process-step:{stepRunId:D}";
    }

    private static string BuildExecutionMetadataJson(DispatchCandidate candidate, string trigger)
    {
        return System.Text.Json.JsonSerializer.Serialize(
            new
            {
                processDefinitionId = candidate.Definition.Id,
                processRunId = candidate.Run.Id,
                processStepRunId = candidate.StepRun.Id,
                processStepTitle = candidate.StepRun.Title,
                trigger = string.IsNullOrWhiteSpace(trigger) ? "process-runtime" : trigger.Trim()
            });
    }

    internal static bool HasBlockingAutomationExecutionRun(IReadOnlyList<ExecutionRunRecord> executionRuns)
        => HasBlockingAutomationExecutionRun(executionRuns, DateTimeOffset.UtcNow);

    internal static bool HasBlockingAutomationExecutionRun(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        return ResolveBlockingAutomationExecutionRunId(executionRuns, now).HasValue;
    }

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        IReadOnlyList<ExecutionRunRecord> executionRuns)
        => ResolveBlockingAutomationExecutionRunId(executionRuns, DateTimeOffset.UtcNow);

    internal static Guid? ResolveBlockingAutomationExecutionRunId(
        IReadOnlyList<ExecutionRunRecord> executionRuns,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        return executionRuns
            .Where(executionRun => IsBlockingAutomationExecutionRun(executionRun, now))
            .OrderByDescending(executionRun => executionRun.UpdatedAtUtc == default
                ? executionRun.CreatedAtUtc
                : executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
    }

    internal static Guid? ResolveRecoverableAutomationExecutionRunId(
        ProcessStepRun stepRun,
        IReadOnlyList<ExecutionRunRecord> executionRuns)
    {
        ArgumentNullException.ThrowIfNull(stepRun);

        if (stepRun.Status != ProcessStepRunStatus.InProgress)
        {
            return null;
        }

        return executionRuns
            .Where(executionRun =>
                string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase) &&
                executionRun.State is ExecutionState.Completed or ExecutionState.Failed &&
                executionRun.Outcome != RunOutcome.Cancelled &&
                IsRecoverableExecutionRunForCurrentAttempt(executionRun, stepRun.StartedAtUtc))
            .OrderByDescending(executionRun => executionRun.CompletedAtUtc ?? executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => (Guid?)executionRun.Id)
            .FirstOrDefault();
    }

    internal static Guid? ResolveReusableAutomationChatSessionId(
        IReadOnlyList<ExecutionRunRecord> executionRuns)
    {
        ArgumentNullException.ThrowIfNull(executionRuns);

        return executionRuns
            .Where(executionRun =>
                string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase) &&
                executionRun.ChatSessionId.HasValue)
            .OrderByDescending(executionRun => executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .Select(executionRun => executionRun.ChatSessionId)
            .FirstOrDefault();
    }

    private async Task<ConcurrentAutomationExecution?> TryAdoptConcurrentAutomationExecutionAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                ProcessRunId: candidate.Run.Id.ToString("D"),
                ProcessStepId: candidate.StepRun.Id.ToString("D"),
                Take: 20),
            cancellationToken);
        var blockingExecutionRunId = ResolveBlockingAutomationExecutionRunId(executionRuns, clock.GetUtcNow());
        if (!blockingExecutionRunId.HasValue)
        {
            return null;
        }

        var detail = await workspaceService.GetExecutionRunDetailAsync(blockingExecutionRunId.Value, cancellationToken);
        return new ConcurrentAutomationExecution(
            blockingExecutionRunId.Value,
            detail,
            ResolveRecoveredExecutionResponseText(detail));
    }

    internal static bool ShouldSkipAutomationCompletionTransition(
        ProcessStepRunStatus currentStatus,
        ProcessStepRunStatus requestedStatus)
    {
        if (currentStatus == requestedStatus)
        {
            return true;
        }

        return currentStatus is not ProcessStepRunStatus.InProgress and not ProcessStepRunStatus.WaitingApproval;
    }

    internal static bool ShouldSkipFreshAutomationDispatch(
        ProcessStepRunStatus currentStatus,
        Guid? recoverableExecutionRunId,
        DateTimeOffset? currentAttemptStartedAtUtc,
        DateTimeOffset now,
        string trigger)
    {
        if (currentStatus != ProcessStepRunStatus.InProgress)
        {
            return false;
        }

        if (recoverableExecutionRunId.HasValue)
        {
            return false;
        }

        if (!IsRecoveryTrigger(trigger))
        {
            return true;
        }

        if (!currentAttemptStartedAtUtc.HasValue)
        {
            return false;
        }

        return now - currentAttemptStartedAtUtc.Value < FreshInProgressRecoveryGracePeriod;
    }

    private static bool IsBlockingAutomationExecutionRun(
        ExecutionRunRecord executionRun,
        DateTimeOffset now)
    {
        return string.Equals(executionRun.RequestedBy, AutomationActor, StringComparison.OrdinalIgnoreCase)
               && executionRun.State is not ExecutionState.Completed
               and not ExecutionState.Failed
               && !IsStaleAutomationExecutionRun(executionRun, now);
    }

    private static bool IsStaleAutomationExecutionRun(
        ExecutionRunRecord executionRun,
        DateTimeOffset now)
    {
        if (executionRun.PendingApprovals.Count > 0)
        {
            return false;
        }

        var lastProgressAtUtc = executionRun.UpdatedAtUtc == default
            ? executionRun.CreatedAtUtc
            : executionRun.UpdatedAtUtc;
        return now - lastProgressAtUtc >= FreshInProgressRecoveryGracePeriod;
    }

    private static bool IsRecoveryTrigger(string trigger)
    {
        return string.Equals(
            trigger?.Trim(),
            "runtime-recovery-scan",
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableExecutionRunForCurrentAttempt(
        ExecutionRunRecord executionRun,
        DateTimeOffset? currentAttemptStartedAtUtc)
    {
        if (!currentAttemptStartedAtUtc.HasValue)
        {
            return true;
        }

        var executionAttemptStartedAtUtc = executionRun.StartedAtUtc ?? executionRun.CreatedAtUtc;
        return executionAttemptStartedAtUtc >= currentAttemptStartedAtUtc.Value;
    }

    private static string ResolveRecoveredExecutionResponseText(ExecutionRunDetail detail)
    {
        var assistantMessage = detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ChatMessageRole.Assistant);
        if (!string.IsNullOrWhiteSpace(assistantMessage?.Content))
        {
            return assistantMessage.Content;
        }

        var serializedResponseText = ResolveLatestAssistantResponseText(detail.Run.SerializedSessionStateJson);
        return string.IsNullOrWhiteSpace(serializedResponseText)
            ? detail.Run.ResultSummary
            : serializedResponseText;
    }

    private static string ResolvePreferredExecutionResponseText(
        DispatchCandidate candidate,
        string? responseText,
        ExecutionRunDetail detail)
    {
        var primaryResponse = string.IsNullOrWhiteSpace(responseText)
            ? string.Empty
            : responseText.Trim();
        var recoveredResponse = ResolveRecoveredExecutionResponseText(detail).Trim();
        if (string.IsNullOrWhiteSpace(primaryResponse))
        {
            return recoveredResponse;
        }

        if (!RequiresGovernedStepOutcome(candidate.StepRun))
        {
            return primaryResponse;
        }

        var primaryHasDeclaredOutcome = TryResolveDeclaredStepOutcome(primaryResponse, out _);
        var recoveredHasDeclaredOutcome = TryResolveDeclaredStepOutcome(recoveredResponse, out _);
        return !primaryHasDeclaredOutcome && recoveredHasDeclaredOutcome
            ? recoveredResponse
            : primaryResponse;
    }

    private static bool TryResolveRecoverableProviderFailure(
        ExecutionRunDetail detail,
        string? responseText,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        var candidateTexts = new[]
        {
            responseText,
            detail.ChatSession?.Messages.LastOrDefault(item => item.Role == ChatMessageRole.Assistant)?.Content,
            ResolveLatestAssistantErrorSummary(detail.Run.SerializedSessionStateJson),
            ResolveLatestAssistantResponseText(detail.Run.SerializedSessionStateJson),
            detail.Run.ResultSummary
        };

        foreach (var candidateText in candidateTexts)
        {
            if (TryMapRecoverableProviderFailureSummary(candidateText, out failureSummary))
            {
                return true;
            }
        }

        return false;
    }

    private static ProcessStepRunStatus ResolveCompletionStatus(DispatchCandidate candidate, ExecutionRunDetail detail)
    {
        return ResolveCompletionStatusWithCarryForward(candidate, detail, [], detail.Run.ResultSummary);
    }

    private static bool ShouldRetryIncompleteSuccessfulRun(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        int attemptNumber)
    {
        var run = detail.Run;
        var unresolvedCriticalToolFailures = ResolveUnresolvedCriticalToolFailures(detail);
        var recoverableImplementationPunt = IsRecoverableImplementationPunt(candidate, responseText);
        var recoverableGovernedOutcomeGap = IsRecoverableGovernedOutcomeGap(candidate, responseText);
        var recoverableProviderFailure = TryResolveRecoverableProviderFailure(detail, responseText, out _);
        return attemptNumber < MaxExecutionAttempts
               && run.State == ExecutionState.Completed
               && run.PendingApprovals.Count == 0
               && run.Outcome == RunOutcome.Succeeded
               && (missingRequiredTools.Count > 0 ||
                   unresolvedCriticalToolFailures.Count > 0 ||
                   recoverableImplementationPunt ||
                   recoverableGovernedOutcomeGap ||
                   recoverableProviderFailure);
    }

    private static string BuildCompletionReason(DispatchCandidate candidate, ExecutionRunDetail detail, string stepTitle)
    {
        return BuildCompletionReasonCore(
            candidate,
            detail,
            stepTitle,
            ResolveMissingRequiredToolExecutions(candidate, detail),
            detail.Run.ResultSummary);
    }

    private static string BuildCompletionReasonCore(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string stepTitle,
        IReadOnlyList<string> missingRequiredTools,
        string? responseText)
    {
        var run = detail.Run;
        if (run.State == ExecutionState.WaitingOnTool || run.PendingApprovals.Count > 0)
        {
            return $"AgentFramework run '{run.Title}' is waiting on approval before '{stepTitle}' can continue.";
        }

        if (run.Outcome != RunOutcome.Succeeded)
        {
            return string.IsNullOrWhiteSpace(run.ResultSummary)
                ? $"AgentFramework run '{run.Title}' failed."
                : $"AgentFramework run '{run.Title}' failed: {run.ResultSummary}";
        }

        var unresolvedFailures = ResolveUnresolvedCriticalToolFailures(detail);
        if (unresolvedFailures.Count > 0)
        {
            var summary = string.Join(
                "; ",
                unresolvedFailures
                    .Take(2)
                    .Select(item => $"{item.ToolName}: {item.ExitSummary}"));
            return $"AgentFramework run '{run.Title}' failed because critical tool executions did not recover: {summary}";
        }

        if (TryResolveRecoverableProviderFailure(detail, responseText, out var providerFailureSummary))
        {
            return $"AgentFramework run '{run.Title}' failed because the assigned provider could not produce a usable response: {providerFailureSummary}";
        }

        if (missingRequiredTools.Count > 0)
        {
            return $"AgentFramework run '{run.Title}' did not execute the required step tools successfully: {string.Join(", ", missingRequiredTools)}";
        }

        if (TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome))
        {
            var branchOutcomeSelectionFailure = ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome);
            if (!string.IsNullOrWhiteSpace(branchOutcomeSelectionFailure))
            {
                return branchOutcomeSelectionFailure;
            }

            return BuildDeclaredStepOutcomeReason(run.Title, stepTitle, declaredOutcome);
        }

        if (RequiresGovernedStepOutcome(candidate.StepRun))
        {
            return $"AgentFramework run '{run.Title}' did not declare a required PROCESS_STEP_OUTCOME for governed step '{stepTitle}'.";
        }

        return $"AgentFramework run '{run.Title}' completed successfully.";
    }

    private static string BuildRecoveryDirective(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        int attemptNumber)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Attempt {attemptNumber} ended before the step was actually complete.");

        if (missingRequiredTools.Count > 0)
        {
            builder.AppendLine($"Missing required step tools: {string.Join(", ", missingRequiredTools)}.");
        }

        if (unresolvedCriticalToolFailures.Count > 0)
        {
            builder.AppendLine(
                $"Unresolved critical tool failures: {string.Join("; ", unresolvedCriticalToolFailures.Take(2).Select(item => $"{item.ToolName}: {item.ExitSummary}"))}.");
        }

        builder.AppendLine("Do not stop after inspection, planning, bootstrap confirmation, or a next-steps summary on this retry.");
        builder.AppendLine("Finish the concrete work, rerun every failed or missing required validation successfully, and then write every required durable artifact.");

        var governedInspectionPaths = ResolveGovernedInspectionPaths(candidate.ExpectedArtifacts);
        var artifactInputInspectionPaths = ResolveArtifactInputInspectionPaths(candidate.ArtifactInputs);

        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("This retry is still the implementation step. Do not report that implementation or code artifacts are missing before you attempt the bootstrap or scaffold yourself.");
            builder.AppendLine("Bootstrap the runnable solution or project now, then validate the concrete files you created with workspace_stat_path, workspace_read_file, and workspace_dotnet_build before you conclude.");
            builder.AppendLine("If the scaffold is greenfield, create the actual solution and project files now with workspace_dotnet_new or a controlled helper path instead of writing only a source file set.");
            builder.AppendLine("If you retry a greenfield .NET bootstrap with workspace_dotnet_new, explicitly request a supported target framework such as `net10.0` instead of accepting an older template default.");
            builder.AppendLine("If this implementation produces browser-facing UI files such as `.razor`, `.cshtml`, or `wwwroot` assets, leave a runnable web host and startup entrypoint in place for downstream QA. Do not stop at a plain class library.");
            if (artifactInputInspectionPaths.StatPaths.Count > 0 || artifactInputInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine("Inspect the inherited durable artifacts directly on this retry instead of relying only on prior summaries or response text.");
                if (artifactInputInspectionPaths.StatPaths.Count > 0)
                {
                    builder.AppendLine($"Use workspace_stat_path on these upstream durable artifact paths now: {FormatPromptPathList(artifactInputInspectionPaths.StatPaths)}.");
                }

                if (artifactInputInspectionPaths.ReadPaths.Count > 0)
                {
                    builder.AppendLine($"Use workspace_read_file on these upstream durable text artifacts now: {FormatPromptPathList(artifactInputInspectionPaths.ReadPaths)}.");
                }
            }

            if (HasProjectStructureContext(candidate))
            {
                builder.AppendLine("Call project_structure_read now, resolve the exact target output directory from the project structure, and honor that path instead of improvising a different location.");
                builder.AppendLine("If the resolved target directory is outside the managed workspace, map it to the workspace alias format `external-target/<drive>/...` for workspace file and dotnet tools.");
                builder.AppendLine("Scaffold, inspect, and build the exact mapped external-target project now. Use workspace_pwsh_run_script only when you need a controlled helper command for the real external target.");
            }
        }

        if (unresolvedCriticalToolFailures.Any(item =>
                string.Equals(NormalizeToolToken(item.ToolName), "workspace_dotnet_build", StringComparison.Ordinal) ||
                string.Equals(NormalizeToolToken(item.ToolName), "workspace_pwsh_run_script", StringComparison.Ordinal)))
        {
            builder.AppendLine("If a prior runtime host, launch script, or locked output file is blocking the build or launch retry, stop the prior host before rerunning validation. Use any provided stop script or recorded PID file when the workspace includes one.");
        }

        var frameworkRecoveryGuidance = BuildDotnetFrameworkRecoveryGuidance(
            candidate,
            unresolvedCriticalToolFailures,
            responseText);
        if (!string.IsNullOrWhiteSpace(frameworkRecoveryGuidance))
        {
            builder.AppendLine(frameworkRecoveryGuidance);
        }

        if (missingRequiredTools.Contains("workspace_stat_path", StringComparer.Ordinal) &&
            governedInspectionPaths.StatPaths.Count > 0)
        {
            builder.AppendLine($"Use workspace_stat_path on these exact governed output paths after they exist: {FormatPromptPathList(governedInspectionPaths.StatPaths)}.");
        }
        else if (missingRequiredTools.Contains("workspace_stat_path", StringComparer.Ordinal) &&
                 artifactInputInspectionPaths.StatPaths.Count > 0)
        {
            builder.AppendLine($"Use workspace_stat_path on these exact upstream durable artifact paths now: {FormatPromptPathList(artifactInputInspectionPaths.StatPaths)}.");
        }

        if (missingRequiredTools.Contains("workspace_read_file", StringComparer.Ordinal))
        {
            if (governedInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine($"Use workspace_read_file on these exact governed text artifacts after they exist: {FormatPromptPathList(governedInspectionPaths.ReadPaths)}.");
            }
            else if (artifactInputInspectionPaths.ReadPaths.Count > 0)
            {
                builder.AppendLine($"Use workspace_read_file on these exact upstream durable text artifacts now: {FormatPromptPathList(artifactInputInspectionPaths.ReadPaths)}.");
            }
            else if (governedInspectionPaths.StatPaths.Count > 0)
            {
                builder.AppendLine("If the governed outputs are binary-only, read the nearest durable markdown, log, JSON, YAML, or text artifact that explains the governed outputs after you create it.");
            }
            else if (artifactInputInspectionPaths.StatPaths.Count > 0)
            {
                builder.AppendLine("If the upstream artifacts are binary-only, read the nearest durable markdown, log, JSON, YAML, or text artifact that explains them before you conclude this retry.");
            }
        }

        if (RequiresGovernedStepOutcome(candidate.StepRun))
        {
            builder.AppendLine("Do not conclude this governed retry without the PROCESS_STEP_OUTCOME comment.");
            builder.AppendLine("End the retry response with exactly one HTML comment in this format: <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed|Blocked|Failed|WaitingApproval|Refused\",\"reason\":\"short concrete reason\"} -->.");
            if (candidate.RequiresExplicitBranchOutcomeSelection)
            {
                builder.AppendLine("If this retry completes onto a specific downstream branch, include the exact branchOutcomeKey from the available branch outcomes, for example <!-- PROCESS_STEP_OUTCOME {\"status\":\"Completed\",\"reason\":\"short concrete reason\",\"branchOutcomeKey\":\"approved\"} -->.");
            }
        }

        var priorSummary = !string.IsNullOrWhiteSpace(detail.Run.ResultSummary)
            ? detail.Run.ResultSummary
            : responseText;
        if (!string.IsNullOrWhiteSpace(priorSummary))
        {
            builder.Append("Previous run summary: ");
            builder.AppendLine(TruncateForPrompt(priorSummary, 400));
        }

        return builder.ToString().Trim();
    }

    private async Task<PrefetchedProjectStructureGrounding> TryBuildProjectStructureGroundingAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (!ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out var projectStructureContext) ||
            projectStructureContext is null)
        {
            return PrefetchedProjectStructureGrounding.Empty;
        }

        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var projectWorkbenchServiceType = Type.GetType("CanDoItAll.Modules.Workbench.ProjectWorkbenchService, CanDoItAll.Modules.Workbench");
            if (projectWorkbenchServiceType is null)
            {
                return PrefetchedProjectStructureGrounding.Empty;
            }

            var projectWorkbenchService = scope.ServiceProvider.GetService(projectWorkbenchServiceType);
            if (projectWorkbenchService is null)
            {
                return PrefetchedProjectStructureGrounding.Empty;
            }

            var getStructureAsync = projectWorkbenchServiceType.GetMethod(
                "GetStructureAsync",
                [typeof(Guid), typeof(CancellationToken)]);
            if (getStructureAsync is null)
            {
                return PrefetchedProjectStructureGrounding.Empty;
            }

            var surfaceTask = getStructureAsync.Invoke(projectWorkbenchService, [projectStructureContext.ProjectId, cancellationToken]) as Task;
            if (surfaceTask is null)
            {
                return PrefetchedProjectStructureGrounding.Empty;
            }

            await surfaceTask;
            var surface = surfaceTask.GetType().GetProperty("Result")?.GetValue(surfaceTask);
            if (surface is null)
            {
                return PrefetchedProjectStructureGrounding.Empty;
            }

            var promptSummary = BuildProjectStructureGroundingSummary(surface, projectStructureContext);
            return string.IsNullOrWhiteSpace(promptSummary)
                ? PrefetchedProjectStructureGrounding.Empty
                : new PrefetchedProjectStructureGrounding(
                    promptSummary,
                    ["project_structure_read"]);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not prefetch project structure grounding for process run {RunId}, step {StepRunId}, project {ProjectId}.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                projectStructureContext.ProjectId);
            return PrefetchedProjectStructureGrounding.Empty;
        }
    }

    private async Task<PrefetchedArtifactInspectionGrounding> TryBuildArtifactInspectionGroundingAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        if (candidate.ArtifactInputs.Count == 0)
        {
            return PrefetchedArtifactInspectionGrounding.Empty;
        }

        var artifactEntries = candidate.ArtifactInputs
            .SelectMany(
                artifactInput => artifactInput.Artifacts.Select(artifact => new
                {
                    artifactInput.SourceStepTitle,
                    artifactInput.ExpectedArtifactTitle,
                    Artifact = artifact
                }))
            .Where(item => !string.IsNullOrWhiteSpace(item.Artifact.ManagedStoragePath))
            .GroupBy(
                item => WorkspaceScopeDescriptor.NormalizeRelativePath(item.Artifact.ManagedStoragePath),
                StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Take(4)
            .ToList();
        if (artifactEntries.Count == 0)
        {
            return PrefetchedArtifactInspectionGrounding.Empty;
        }

        try
        {
            var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
            var builder = new StringBuilder();
            var satisfiedToolNames = new HashSet<string>(StringComparer.Ordinal);
            var appendedArtifactCount = 0;

            builder.AppendLine("Dispatcher pre-inspected recorded upstream durable artifacts before this step started:");
            foreach (var artifactEntry in artifactEntries)
            {
                var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifactEntry.Artifact.ManagedStoragePath);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    continue;
                }

                var fullPath = Path.GetFullPath(Path.Combine(
                    workspaceRoot,
                    normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsWithinWorkspace(workspaceRoot, fullPath) || !File.Exists(fullPath))
                {
                    continue;
                }

                var fileInfo = new FileInfo(fullPath);
                satisfiedToolNames.Add("workspace_stat_path");
                builder.Append("- `");
                builder.Append(normalizedPath);
                builder.Append("` from ");
                builder.Append(artifactEntry.SourceStepTitle);
                builder.Append(" -> ");
                builder.Append(artifactEntry.Artifact.Title);
                builder.Append(" (");
                builder.Append(fileInfo.Length);
                builder.Append(" bytes");
                if (fileInfo.LastWriteTimeUtc != default)
                {
                    builder.Append(", updated ");
                    builder.Append(fileInfo.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
                }

                builder.AppendLine(")");

                if (!string.IsNullOrWhiteSpace(artifactEntry.Artifact.ReviewSummary))
                {
                    builder.Append("  Review summary: ");
                    builder.AppendLine(TrimForPrompt(artifactEntry.Artifact.ReviewSummary, 280));
                }

                if (!string.IsNullOrWhiteSpace(artifactEntry.Artifact.ProvenanceSummary))
                {
                    builder.Append("  Provenance: ");
                    builder.AppendLine(TrimForPrompt(artifactEntry.Artifact.ProvenanceSummary, 280));
                }

                if (IsTextReadableManagedArtifactPath(normalizedPath))
                {
                    var fileContents = await File.ReadAllTextAsync(fullPath, cancellationToken);
                    satisfiedToolNames.Add("workspace_read_file");
                    builder.Append("  Excerpt: ");
                    builder.AppendLine(string.IsNullOrWhiteSpace(fileContents)
                        ? "(file is empty)"
                        : TrimForPrompt(CollapsePromptWhitespace(fileContents), 420));
                }

                appendedArtifactCount++;
            }

            if (appendedArtifactCount == 0 || satisfiedToolNames.Count == 0)
            {
                return PrefetchedArtifactInspectionGrounding.Empty;
            }

            return new PrefetchedArtifactInspectionGrounding(
                builder.ToString().Trim(),
                satisfiedToolNames.ToList());
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not prefetch governed artifact inspection grounding for process run {RunId}, step {StepRunId}.",
                candidate.Run.Id,
                candidate.StepRun.Id);
            return PrefetchedArtifactInspectionGrounding.Empty;
        }
    }

    private static string BuildProjectStructureGroundingSummary(
        object surface,
        ProcessProjectStructureContext context)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(context);

        var projectName = GetProjectStructureGroundingString(surface, "ProjectName");
        var nodes = ExtractProjectStructureGroundingNodes(surface);
        if (nodes.Count == 0)
        {
            return string.Empty;
        }

        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var targetNodeId = NormalizeProjectStructureNodeId(context.ResolveTargetNodeId());
        var selectedProcessNodeId = NormalizeProjectStructureNodeId(context.NodeId);
        var builder = new StringBuilder();
        builder.AppendLine($"Dispatcher fetched the live project structure for `{projectName}` and focused this prompt on the selected work branch.");

        var ancestorPath = ResolveProjectStructureAncestorPath(targetNodeId, nodesById);
        if (ancestorPath.Count > 0)
        {
            builder.AppendLine("Ancestor path to the target work node:");
            AppendProjectStructureGroundingNodes(builder, ancestorPath);
        }

        if (!string.IsNullOrWhiteSpace(selectedProcessNodeId) &&
            nodesById.TryGetValue(selectedProcessNodeId, out var selectedProcessNode) &&
            !string.Equals(selectedProcessNodeId, targetNodeId, StringComparison.Ordinal))
        {
            builder.AppendLine("Selected process node:");
            AppendProjectStructureGroundingNodes(builder, [selectedProcessNode]);
        }

        if (!string.IsNullOrWhiteSpace(targetNodeId) &&
            nodesById.TryGetValue(targetNodeId, out var targetNode))
        {
            var siblingNodes = nodes
                .Where(node =>
                    !string.Equals(node.Id, targetNode.Id, StringComparison.Ordinal) &&
                    !string.Equals(node.Id, selectedProcessNodeId, StringComparison.Ordinal) &&
                    string.Equals(node.ParentId, targetNode.ParentId, StringComparison.Ordinal))
                .Where(HasProjectStructureGroundingSignal)
                .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                .Take(4)
                .ToList();

            if (siblingNodes.Count > 0)
            {
                builder.AppendLine("Sibling planning context under the same parent:");
                AppendProjectStructureGroundingNodes(builder, siblingNodes);
            }

            var childNodes = nodes
                .Where(node =>
                    string.Equals(node.ParentId, targetNode.Id, StringComparison.Ordinal) &&
                    !string.Equals(node.Id, selectedProcessNodeId, StringComparison.Ordinal))
                .OrderBy(node => node.Title, StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToList();

            if (childNodes.Count > 0)
            {
                builder.AppendLine("Immediate child nodes under the target work node:");
                AppendProjectStructureGroundingNodes(builder, childNodes);
            }
        }

        return builder.ToString().Trim();
    }

    private static string BuildProviderRepairRecoveryDirective(
        string recoveryDirective,
        ProviderRepairOutcome repairOutcome)
    {
        var builder = new StringBuilder();
        builder.Append("Infrastructure recovery: the previous attempt hit a provider failure. ");
        builder.Append("Assigned internal agents using provider '")
            .Append(repairOutcome.FailedProviderName)
            .Append("' were moved to '")
            .Append(repairOutcome.FallbackProviderName)
            .Append("' with model '")
            .Append(repairOutcome.FallbackModel)
            .Append("'. ");
        builder.AppendLine($"Failure summary: {repairOutcome.FailureSummary}");

        if (!string.IsNullOrWhiteSpace(recoveryDirective))
        {
            builder.AppendLine(recoveryDirective.Trim());
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyList<ProviderProfile> OrderFallbackProviders(
        IEnumerable<ProviderProfile> providers,
        Guid failedProviderId)
    {
        return providers
            .Where(item => item.IsEnabled && item.SupportsTools && item.Id != failedProviderId)
            .OrderBy(item => item.Kind == ProviderKind.Ollama ? 0 : 1)
            .ThenBy(item => item.Kind is ProviderKind.OpenAi or ProviderKind.AzureOpenAi ? 1 : 0)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ResolveFallbackProviderModel(
        ProviderProfile provider,
        ProviderHealthResult healthResult)
    {
        var suggestedModels = healthResult.SuggestedModels
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (!string.IsNullOrWhiteSpace(provider.DefaultModel) &&
            (suggestedModels.Count == 0 || suggestedModels.Contains(provider.DefaultModel, StringComparer.OrdinalIgnoreCase)))
        {
            return provider.DefaultModel;
        }

        return suggestedModels.FirstOrDefault()
               ?? provider.DefaultModel;
    }

    private static IReadOnlyList<ProjectStructureGroundingNodeData> ResolveProjectStructureAncestorPath(
        string? nodeId,
        IReadOnlyDictionary<string, ProjectStructureGroundingNodeData> nodesById)
    {
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return [];
        }

        var path = new List<ProjectStructureGroundingNodeData>();
        var cursor = NormalizeProjectStructureNodeId(nodeId);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        while (!string.IsNullOrWhiteSpace(cursor) &&
               visited.Add(cursor) &&
               nodesById.TryGetValue(cursor, out var node))
        {
            path.Add(node);
            cursor = NormalizeProjectStructureNodeId(node.ParentId);
        }

        path.Reverse();
        return path;
    }

    private static void AppendProjectStructureGroundingNodes(
        StringBuilder builder,
        IReadOnlyList<ProjectStructureGroundingNodeData> nodes)
    {
        foreach (var node in nodes)
        {
            builder.AppendLine($"- {BuildProjectStructureGroundingNodeSummary(node)}");
        }
    }

    private static string BuildProjectStructureGroundingNodeSummary(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var segments = new List<string>
        {
            $"{node.Title} ({node.Id})",
            $"type: {node.ObjectType}/{NormalizeProjectStructureNodeSubtype(node.ObjectSubtype)}"
        };

        if (!string.IsNullOrWhiteSpace(node.Status))
        {
            segments.Add($"status: {CollapsePromptWhitespace(node.Status)}");
        }

        if (!string.IsNullOrWhiteSpace(node.Subtitle))
        {
            segments.Add($"subtitle: {TrimProjectStructureGroundingText(node.Subtitle, 140)}");
        }

        if (!string.IsNullOrWhiteSpace(node.Notes))
        {
            segments.Add($"notes: {TrimProjectStructureGroundingText(node.Notes, 320)}");
        }

        var metadataSummary = NormalizeProjectStructureMetadataSummary(node.MetadataJson);
        if (!string.IsNullOrWhiteSpace(metadataSummary))
        {
            segments.Add($"metadata: {metadataSummary}");
        }

        return string.Join("; ", segments);
    }

    private static bool HasProjectStructureGroundingSignal(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return !string.IsNullOrWhiteSpace(node.Notes) ||
               !string.IsNullOrWhiteSpace(node.Subtitle) ||
               !string.IsNullOrWhiteSpace(NormalizeProjectStructureMetadataSummary(node.MetadataJson));
    }

    private static string NormalizeProjectStructureNodeId(string? nodeId)
        => string.IsNullOrWhiteSpace(nodeId) ? string.Empty : nodeId.Trim();

    private static string NormalizeProjectStructureNodeSubtype(string? objectSubtype)
        => string.IsNullOrWhiteSpace(objectSubtype) ? "default" : CollapsePromptWhitespace(objectSubtype);

    private static string TrimProjectStructureGroundingText(string? value, int maxLength)
    {
        var collapsed = CollapsePromptWhitespace(value);
        if (collapsed.Length <= maxLength)
        {
            return collapsed;
        }

        return $"{collapsed[..Math.Max(0, maxLength - 3)].TrimEnd()}...";
    }

    private static string CollapsePromptWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
    }

    private static string NormalizeProjectStructureMetadataSummary(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && !root.EnumerateObject().MoveNext())
            {
                return string.Empty;
            }

            if (root.ValueKind == JsonValueKind.Array && root.GetArrayLength() == 0)
            {
                return string.Empty;
            }

            return TrimProjectStructureGroundingText(JsonSerializer.Serialize(root), 320);
        }
        catch (JsonException)
        {
            return TrimProjectStructureGroundingText(metadataJson, 320);
        }
    }

    private static IReadOnlyList<ProjectStructureGroundingNodeData> ExtractProjectStructureGroundingNodes(object surface)
    {
        var nodesValue = surface.GetType().GetProperty("Nodes")?.GetValue(surface) as IEnumerable;
        if (nodesValue is null)
        {
            return [];
        }

        var nodes = new List<ProjectStructureGroundingNodeData>();
        foreach (var node in nodesValue.Cast<object>())
        {
            var id = GetProjectStructureGroundingString(node, "Id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            nodes.Add(new ProjectStructureGroundingNodeData(
                id,
                GetProjectStructureGroundingString(node, "ParentId"),
                GetProjectStructureGroundingString(node, "ObjectType"),
                GetProjectStructureGroundingString(node, "ObjectSubtype"),
                GetProjectStructureGroundingString(node, "Title"),
                GetProjectStructureGroundingString(node, "Subtitle"),
                GetProjectStructureGroundingString(node, "Status"),
                GetProjectStructureGroundingString(node, "Notes"),
                GetProjectStructureGroundingString(node, "MetadataJson")));
        }

        return nodes;
    }

    private static string GetProjectStructureGroundingString(object source, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var value = source.GetType().GetProperty(propertyName)?.GetValue(source);
        return value?.ToString()?.Trim() ?? string.Empty;
    }

    private static string BuildDotnetFrameworkRecoveryGuidance(
        DispatchCandidate candidate,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        string responseText)
    {
        var dotnetFailureSummary = string.Join(
            Environment.NewLine,
            unresolvedCriticalToolFailures
                .Where(IsFrameworkRecoverableDotnetToolFailure)
                .Select(item => item.ExitSummary));
        if (string.IsNullOrWhiteSpace(dotnetFailureSummary))
        {
            return string.Empty;
        }

        var combinedFailureText = string.Join(
            Environment.NewLine,
            new[] { dotnetFailureSummary, responseText }.Where(item => !string.IsNullOrWhiteSpace(item)));
        if (!MentionsMissingDotnetFramework(combinedFailureText))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("A previous dotnet validation failed because the generated project targeted a framework/runtime that is not available in this workspace.");
        builder.AppendLine("Inspect the generated `.csproj` files now and replace unsupported target frameworks such as `net7.0` with a supported target before rerunning the failed dotnet validation.");
        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("For new greenfield .NET projects in this repository, prefer `workspace_dotnet_new`; if you must author a project file manually, prefer `net10.0` unless the project structure or existing solution explicitly requires another target.");
        }
        else
        {
            builder.AppendLine("This retry must repair the concrete solution or project configuration, not just report the mismatch.");
            builder.AppendLine("Update the affected `.csproj` or solution files to a supported target, then rerun the originally required dotnet validation successfully before you conclude.");
            builder.AppendLine("If the project was bootstrapped during this process and no stricter runtime is required, prefer `net10.0` for the repaired target.");
        }
        return builder.ToString().Trim();
    }

    private static bool IsFrameworkRecoverableDotnetToolFailure(ToolExecutionReceiptRecord receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_build", StringComparison.Ordinal) ||
               string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_test", StringComparison.Ordinal) ||
               string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_run", StringComparison.Ordinal) ||
               string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_publish", StringComparison.Ordinal);
    }

    private static bool MentionsMissingDotnetFramework(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("You must install or update .NET", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Microsoft.NETCore.App", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("was not found", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("NETSDK1045", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("is not supported by this SDK", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("TargetFramework", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("net7.0", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ToolExecutionReceiptRecord> ResolveUnresolvedCriticalToolFailures(ExecutionRunDetail detail)
    {
        return detail.ToolReceipts
            .Where(IsCriticalToolReceipt)
            .GroupBy(
                item => string.Join(
                    "|",
                    NormalizeToolToken(item.ToolName),
                    item.RequestSummary.Trim(),
                    item.WorkingDirectory.Trim()),
                StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(item => item.CompletedAtUtc)
                .ThenByDescending(item => item.StartedAtUtc)
                .First())
            .Where(IsFailedToolReceipt)
            .ToList();
    }

    private static IReadOnlyList<string> ResolveMissingRequiredToolExecutions(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        return ResolveMissingRequiredToolExecutionsWithCarryForward(candidate, detail, []);
    }

    private static IReadOnlyList<string> ResolveMissingRequiredToolExecutionsWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts)
    {
        var requiredToolNames = ResolveRequiredToolNames(candidate);
        if (requiredToolNames.Count == 0)
        {
            return [];
        }

        var successfulToolNames = ResolveSuccessfulToolNames(detail);
        foreach (var toolName in successfulToolNamesFromPriorAttempts)
        {
            var normalizedToolName = NormalizeToolToken(toolName);
            if (!string.IsNullOrWhiteSpace(normalizedToolName))
            {
                successfulToolNames.Add(normalizedToolName);
            }
        }

        var missing = new List<string>();

        foreach (var requiredToolName in requiredToolNames)
        {
            if (!successfulToolNames.Contains(requiredToolName))
            {
                missing.Add(requiredToolName);
            }
        }

        return missing;
    }

    private static ProcessStepRunStatus ResolveCompletionStatusWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts)
    {
        return ResolveCompletionStatusWithCarryForward(
            candidate,
            detail,
            successfulToolNamesFromPriorAttempts,
            detail.Run.ResultSummary);
    }

    private static ProcessStepRunStatus ResolveCompletionStatusWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IEnumerable<string> successfulToolNamesFromPriorAttempts,
        string? responseText)
    {
        var run = detail.Run;
        if (run.State != ExecutionState.Completed)
        {
            return run.PendingApprovals.Count > 0
                ? ProcessStepRunStatus.WaitingApproval
                : run.State == ExecutionState.Failed
                    ? ProcessStepRunStatus.Failed
                    : candidate.StepRun.Status == ProcessStepRunStatus.WaitingApproval
                        ? ProcessStepRunStatus.WaitingApproval
                        : ProcessStepRunStatus.InProgress;
        }

        if (run.PendingApprovals.Count > 0)
        {
            return ProcessStepRunStatus.WaitingApproval;
        }

        if (run.Outcome != RunOutcome.Succeeded)
        {
            return ProcessStepRunStatus.Failed;
        }

        if (ResolveMissingRequiredToolExecutionsWithCarryForward(candidate, detail, successfulToolNamesFromPriorAttempts).Count > 0)
        {
            return ProcessStepRunStatus.Failed;
        }

        if (ResolveUnresolvedCriticalToolFailures(detail).Count > 0)
        {
            return ProcessStepRunStatus.Failed;
        }

        if (TryResolveRecoverableProviderFailure(detail, responseText, out _))
        {
            return ProcessStepRunStatus.Failed;
        }

        if (TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome))
        {
            if (!string.IsNullOrWhiteSpace(ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome)))
            {
                return ProcessStepRunStatus.Failed;
            }

            return declaredOutcome.Status;
        }

        if (RequiresGovernedStepOutcome(candidate.StepRun))
        {
            return ProcessStepRunStatus.Failed;
        }

        return ProcessStepRunStatus.Completed;
    }

    private static string BuildCompletionReasonWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string stepTitle,
        IEnumerable<string> successfulToolNamesFromPriorAttempts)
    {
        return BuildCompletionReasonWithCarryForward(
            candidate,
            detail,
            stepTitle,
            successfulToolNamesFromPriorAttempts,
            detail.Run.ResultSummary);
    }

    private static string BuildCompletionReasonWithCarryForward(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string stepTitle,
        IEnumerable<string> successfulToolNamesFromPriorAttempts,
        string? responseText)
    {
        return BuildCompletionReasonCore(
            candidate,
            detail,
            stepTitle,
            ResolveMissingRequiredToolExecutionsWithCarryForward(candidate, detail, successfulToolNamesFromPriorAttempts),
            responseText);
    }

    private static bool TryResolveDeclaredStepOutcome(string? responseText, out DeclaredStepOutcome declaredOutcome)
    {
        declaredOutcome = default;
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        var matches = DeclaredStepOutcomeRegex.Matches(responseText);
        if (matches.Count == 0)
        {
            return false;
        }

        var json = matches[^1].Groups["json"].Value;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("status", out var statusElement) ||
                statusElement.ValueKind != JsonValueKind.String ||
                !TryMapDeclaredStepStatus(statusElement.GetString(), out var status))
            {
                return false;
            }

            var reason = document.RootElement.TryGetProperty("reason", out var reasonElement) &&
                         reasonElement.ValueKind == JsonValueKind.String
                ? reasonElement.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var branchOutcomeKey = document.RootElement.TryGetProperty("branchOutcomeKey", out var branchOutcomeKeyElement) &&
                                   branchOutcomeKeyElement.ValueKind == JsonValueKind.String
                ? branchOutcomeKeyElement.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            var branchOutcomeTitle = document.RootElement.TryGetProperty("branchOutcomeTitle", out var branchOutcomeTitleElement) &&
                                     branchOutcomeTitleElement.ValueKind == JsonValueKind.String
                ? branchOutcomeTitleElement.GetString()?.Trim() ?? string.Empty
                : string.Empty;
            declaredOutcome = new DeclaredStepOutcome(status, reason, null, branchOutcomeKey, branchOutcomeTitle);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryMapDeclaredStepStatus(string? value, out ProcessStepRunStatus status)
    {
        status = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().Replace("-", string.Empty, StringComparison.Ordinal).Replace("_", string.Empty, StringComparison.Ordinal);
        if (normalized.Equals(nameof(ProcessStepRunStatus.Completed), StringComparison.OrdinalIgnoreCase))
        {
            status = ProcessStepRunStatus.Completed;
            return true;
        }

        if (normalized.Equals(nameof(ProcessStepRunStatus.Blocked), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("block", StringComparison.OrdinalIgnoreCase))
        {
            status = ProcessStepRunStatus.Blocked;
            return true;
        }

        if (normalized.Equals(nameof(ProcessStepRunStatus.Failed), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("error", StringComparison.OrdinalIgnoreCase))
        {
            status = ProcessStepRunStatus.Failed;
            return true;
        }

        if (normalized.Equals(nameof(ProcessStepRunStatus.WaitingApproval), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("approval", StringComparison.OrdinalIgnoreCase))
        {
            status = ProcessStepRunStatus.WaitingApproval;
            return true;
        }

        if (normalized.Equals(nameof(ProcessStepRunStatus.Refused), StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("reject", StringComparison.OrdinalIgnoreCase) ||
            normalized.Equals("rejected", StringComparison.OrdinalIgnoreCase))
        {
            status = ProcessStepRunStatus.Refused;
            return true;
        }

        return false;
    }

    private static string BuildDeclaredStepOutcomeReason(string runTitle, string stepTitle, DeclaredStepOutcome declaredOutcome)
    {
        var trimmedReason = declaredOutcome.Reason.Trim();
        return declaredOutcome.Status switch
        {
            ProcessStepRunStatus.Completed => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' completed step '{stepTitle}' with an explicit governed outcome."
                : $"AgentFramework run '{runTitle}' completed step '{stepTitle}': {trimmedReason}",
            ProcessStepRunStatus.Blocked => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' blocked step '{stepTitle}' pending remediation."
                : $"AgentFramework run '{runTitle}' blocked step '{stepTitle}': {trimmedReason}",
            ProcessStepRunStatus.WaitingApproval => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' is waiting on approval before '{stepTitle}' can continue."
                : $"AgentFramework run '{runTitle}' is waiting on approval before '{stepTitle}' can continue: {trimmedReason}",
            ProcessStepRunStatus.Refused => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' refused step '{stepTitle}'."
                : $"AgentFramework run '{runTitle}' refused step '{stepTitle}': {trimmedReason}",
            _ => string.IsNullOrWhiteSpace(trimmedReason)
                ? $"AgentFramework run '{runTitle}' failed step '{stepTitle}'."
                : $"AgentFramework run '{runTitle}' failed step '{stepTitle}': {trimmedReason}"
        };
    }

    private static ISet<string> ResolveSuccessfulToolNames(ExecutionRunDetail detail)
    {
        var successfulToolNames = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .Select(receipt => NormalizeToolToken(receipt.ToolName))
            .Where(toolName => !string.IsNullOrWhiteSpace(toolName))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var toolName in ResolveSuccessfulSessionToolNames(detail.Run.SerializedSessionStateJson))
        {
            successfulToolNames.Add(toolName);
        }

        return successfulToolNames;
    }

    private static IReadOnlyList<string> ResolveSuccessfulSessionToolNames(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var toolNamesByCallId = new Dictionary<string, string>(StringComparer.Ordinal);
            var successfulToolNames = new HashSet<string>(StringComparer.Ordinal);

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement))
                    {
                        continue;
                    }

                    var contentType = typeElement.GetString();
                    if (string.Equals(contentType, "functionCall", StringComparison.Ordinal))
                    {
                        var callId = content.TryGetProperty("callId", out var callIdElement)
                            ? callIdElement.GetString()
                            : null;
                        var toolName = content.TryGetProperty("name", out var nameElement)
                            ? NormalizeToolToken(nameElement.GetString() ?? string.Empty)
                            : string.Empty;
                        if (!string.IsNullOrWhiteSpace(callId) && !string.IsNullOrWhiteSpace(toolName))
                        {
                            toolNamesByCallId[callId] = toolName;
                        }

                        continue;
                    }

                    if (!string.Equals(contentType, "functionResult", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var resultCallId = content.TryGetProperty("callId", out var resultCallIdElement)
                        ? resultCallIdElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(resultCallId) ||
                        !toolNamesByCallId.TryGetValue(resultCallId, out var recordedToolName) ||
                        !content.TryGetProperty("result", out var resultElement) ||
                        !IsSuccessfulSessionFunctionResult(resultElement))
                    {
                        continue;
                    }

                    successfulToolNames.Add(recordedToolName);
                }
            }

            return successfulToolNames.ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ResolveLatestAssistantResponseText(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? latestAssistantText = null;

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("role", out var roleElement) ||
                    !string.Equals(roleElement.GetString(), "assistant", StringComparison.OrdinalIgnoreCase) ||
                    !message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var assistantParts = new List<string>();
                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement) ||
                        !string.Equals(typeElement.GetString(), "text", StringComparison.OrdinalIgnoreCase) ||
                        !content.TryGetProperty("text", out var textElement))
                    {
                        continue;
                    }

                    var text = textElement.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        assistantParts.Add(text.Trim());
                    }
                }

                if (assistantParts.Count > 0)
                {
                    latestAssistantText = string.Join(Environment.NewLine, assistantParts);
                }
            }

            return latestAssistantText;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ResolveLatestAssistantErrorSummary(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            string? latestAssistantError = null;
            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("role", out var roleElement) ||
                    !string.Equals(roleElement.GetString(), "assistant", StringComparison.OrdinalIgnoreCase) ||
                    !message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!TryResolveAssistantErrorSummary(content, out var assistantError))
                    {
                        continue;
                    }

                    latestAssistantError = assistantError;
                }
            }

            return latestAssistantError;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryResolveAssistantErrorSummary(
        JsonElement content,
        out string assistantError)
    {
        assistantError = string.Empty;
        var hasErrorCode = content.TryGetProperty("errorCode", out var errorCodeElement) &&
            errorCodeElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(errorCodeElement.GetString());
        var contentType = content.TryGetProperty("$type", out var typeElement)
            ? typeElement.GetString()
            : string.Empty;
        if (!hasErrorCode &&
            !string.Equals(contentType, "error", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var errorCode = hasErrorCode
            ? errorCodeElement.GetString()!.Trim()
            : string.Empty;
        var message = TryResolveStringProperty(content, "message")
            ?? TryResolveStringProperty(content, "errorMessage")
            ?? TryResolveStringProperty(content, "text")
            ?? TryResolveStringProperty(content, "content")
            ?? string.Empty;
        assistantError = string.IsNullOrWhiteSpace(errorCode)
            ? message.Trim()
            : string.IsNullOrWhiteSpace(message)
                ? errorCode
                : $"{errorCode}: {message.Trim()}";
        return !string.IsNullOrWhiteSpace(assistantError);
    }

    private static string? TryResolveStringProperty(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var propertyValue) &&
               propertyValue.ValueKind == JsonValueKind.String
            ? propertyValue.GetString()
            : null;
    }

    private static bool TryMapRecoverableProviderFailureSummary(
        string? candidateText,
        out string failureSummary)
    {
        failureSummary = string.Empty;
        if (string.IsNullOrWhiteSpace(candidateText))
        {
            return false;
        }

        var normalizedText = Regex.Replace(
                candidateText,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        if (normalizedText.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("exceeded your current quota", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "Provider quota was exhausted before the agent returned a usable response.";
            return true;
        }

        if (normalizedText.Contains("rate_limit", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "The assigned provider hit a rate limit before the agent returned a usable response.";
            return true;
        }

        if (normalizedText.Contains("The provider completed without returning text.", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("provider completed without returning text", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("provider returned an empty response", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "The assigned provider completed without returning text.";
            return true;
        }

        return false;
    }

    private static string TruncateForPrompt(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength].TrimEnd() + "...";
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ResolveSuccessfulSessionToolOutputFiles(string serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            if (!document.RootElement.TryGetProperty("stateBag", out var stateBag) ||
                !stateBag.TryGetProperty("InMemoryChatHistoryProvider", out var historyProvider) ||
                !historyProvider.TryGetProperty("messages", out var messages) ||
                messages.ValueKind != JsonValueKind.Array)
            {
                return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
            }

            var callsById = new Dictionary<string, SessionToolCall>(StringComparer.Ordinal);
            var outputFilesByToolName = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            foreach (var message in messages.EnumerateArray())
            {
                if (!message.TryGetProperty("contents", out var contents) ||
                    contents.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                foreach (var content in contents.EnumerateArray())
                {
                    if (!content.TryGetProperty("$type", out var typeElement))
                    {
                        continue;
                    }

                    var contentType = typeElement.GetString();
                    if (string.Equals(contentType, "functionCall", StringComparison.Ordinal))
                    {
                        var callId = content.TryGetProperty("callId", out var callIdElement)
                            ? callIdElement.GetString()
                            : null;
                        var toolName = content.TryGetProperty("name", out var nameElement)
                            ? NormalizeToolToken(nameElement.GetString() ?? string.Empty)
                            : string.Empty;
                        var outputFileName = TryResolveSessionToolOutputFileName(content);
                        if (!string.IsNullOrWhiteSpace(callId) &&
                            !string.IsNullOrWhiteSpace(toolName) &&
                            !string.IsNullOrWhiteSpace(outputFileName))
                        {
                            callsById[callId] = new SessionToolCall(toolName, outputFileName);
                        }

                        continue;
                    }

                    if (!string.Equals(contentType, "functionResult", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    var resultCallId = content.TryGetProperty("callId", out var resultCallIdElement)
                        ? resultCallIdElement.GetString()
                        : null;
                    if (string.IsNullOrWhiteSpace(resultCallId) ||
                        !callsById.TryGetValue(resultCallId, out var call) ||
                        !content.TryGetProperty("result", out var resultElement) ||
                        !IsSuccessfulSessionFunctionResult(resultElement))
                    {
                        continue;
                    }

                    if (!outputFilesByToolName.TryGetValue(call.ToolName, out var outputFiles))
                    {
                        outputFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        outputFilesByToolName[call.ToolName] = outputFiles;
                    }

                    outputFiles.Add(WorkspaceScopeDescriptor.NormalizeRelativePath(call.OutputFileName));
                }
            }

            return outputFilesByToolName.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList(),
                StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        }
    }

    private static bool IsSuccessfulSessionFunctionResult(JsonElement result)
    {
        switch (result.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
            {
                return false;
            }
            case JsonValueKind.False:
            {
                return false;
            }
            case JsonValueKind.True:
            case JsonValueKind.Number:
            {
                return true;
            }
            case JsonValueKind.String:
            {
                var text = result.GetString();
                return !string.IsNullOrWhiteSpace(text) &&
                       !text.TrimStart().StartsWith("Error", StringComparison.OrdinalIgnoreCase);
            }
            case JsonValueKind.Array:
            {
                return result.GetArrayLength() > 0;
            }
            case JsonValueKind.Object:
            {
                if (result.TryGetProperty("succeeded", out var succeededElement))
                {
                    return succeededElement.ValueKind switch
                    {
                        JsonValueKind.True => true,
                        JsonValueKind.False => false,
                        JsonValueKind.String when bool.TryParse(succeededElement.GetString(), out var succeeded) => succeeded,
                        _ => false
                    };
                }

                if (result.TryGetProperty("receipt", out var receiptElement) &&
                    receiptElement.ValueKind == JsonValueKind.Object &&
                    receiptElement.TryGetProperty("outcome", out var outcomeElement))
                {
                    var outcome = outcomeElement.GetString();
                    return !string.IsNullOrWhiteSpace(outcome) &&
                           !outcome.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) &&
                           !outcome.StartsWith("Denied", StringComparison.OrdinalIgnoreCase) &&
                           !outcome.StartsWith("TimedOut", StringComparison.OrdinalIgnoreCase);
                }

                if (result.TryGetProperty("$type", out _))
                {
                    return true;
                }

                return result.EnumerateObject().Any();
            }
            default:
            {
                return false;
            }
        }
    }

    private static string? TryResolveSessionToolOutputFileName(JsonElement functionCallContent)
    {
        if (!functionCallContent.TryGetProperty("arguments", out var argumentsElement) ||
            argumentsElement.ValueKind != JsonValueKind.Object ||
            !argumentsElement.TryGetProperty("filename", out var fileNameElement) ||
            fileNameElement.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var fileName = fileNameElement.GetString();
        return string.IsNullOrWhiteSpace(fileName)
            ? null
            : fileName.Trim();
    }

    private static IReadOnlyList<string> ResolveRequiredToolNames(DispatchCandidate candidate)
    {
        var requiredToolNames = new SortedSet<string>(StringComparer.Ordinal);
        var workBriefText = candidate.WorkBrief?.WorkBriefText;
        if (!string.IsNullOrWhiteSpace(workBriefText))
        {
            foreach (var toolName in RequiredToolNameRegex.Matches(workBriefText)
                         .Where(match => !IsNegatedRequiredToolReference(workBriefText, match))
                         .Select(match => NormalizeToolToken(match.Value))
                         .Where(IsHardRequiredProcessToolName))
            {
                requiredToolNames.Add(toolName);
            }
        }

        foreach (var toolName in ResolveImplicitRequiredToolNames(candidate))
        {
            requiredToolNames.Add(toolName);
        }

        return requiredToolNames.ToList();
    }

    private static IReadOnlyList<string> ResolveImplicitRequiredToolNames(DispatchCandidate candidate)
    {
        var requiredToolNames = new List<string>();
        if (HasProjectStructureContext(candidate))
        {
            requiredToolNames.Add("project_structure_read");
        }

        if (RequiresGovernedInspection(candidate.StepRun))
        {
            requiredToolNames.AddRange(GovernedInspectionToolNames);
        }

        if (RequiresConcreteImplementationProof(candidate))
        {
            requiredToolNames.AddRange(ImplementationProofToolNames);
        }

        if (RequiresDurableTextArtifactWrite(candidate))
        {
            requiredToolNames.Add("workspace_write_file");
        }

        return requiredToolNames;
    }

    private static bool RequiresGovernedStepOutcome(ProcessStepRun stepRun)
    {
        return stepRun.StepKind != ProcessStepKind.Start;
    }

    private static bool RequiresConcreteImplementationProof(DispatchCandidate candidate)
    {
        return candidate.StepRun.StepKind == ProcessStepKind.Work &&
               (candidate.StepRun.Title.Contains("implement", StringComparison.OrdinalIgnoreCase) ||
                candidate.ExpectedArtifacts.Any(item =>
                    item.ArtifactKind == ProcessArtifactKind.Deliverable &&
                    item.Title.Contains("change set", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool RequiresConcreteImplementationReview(DispatchCandidate candidate)
    {
        return candidate.StepRun.Title.Contains("peer review", StringComparison.OrdinalIgnoreCase) ||
               candidate.StepRun.Title.Contains("integration readiness", StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresGovernedInspection(ProcessStepRun stepRun)
    {
        return stepRun.StepKind is not ProcessStepKind.Start and not ProcessStepKind.Work;
    }

    private static bool RequiresDurableTextArtifactWrite(DispatchCandidate candidate)
    {
        return candidate.ExpectedArtifacts.Any(item =>
        {
            if (!item.IsRequired)
            {
                return false;
            }

            if (!TryExtractExpectedArtifactRelativePath(item.ValidationRequirementSummary, out var relativePath))
            {
                return false;
            }

            return IsResponseProjectableTextArtifact(relativePath);
        });
    }

    private static bool HasProjectStructureContext(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out _);
    }

    private static bool IsRecoverableImplementationPunt(
        DispatchCandidate candidate,
        string? responseText)
    {
        if (!RequiresConcreteImplementationProof(candidate) ||
            !TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome) ||
            declaredOutcome.Status != ProcessStepRunStatus.Blocked ||
            string.IsNullOrWhiteSpace(declaredOutcome.Reason))
        {
            return false;
        }

        var normalizedReason = Regex.Replace(
                declaredOutcome.Reason,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim()
            .ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            return false;
        }

        return normalizedReason.Contains("no implementation", StringComparison.Ordinal) ||
               normalizedReason.Contains("no code artifact", StringComparison.Ordinal) ||
               normalizedReason.Contains("bootstrap and implement", StringComparison.Ordinal) ||
               normalizedReason.Contains("proceed to bootstrap", StringComparison.Ordinal) ||
               normalizedReason.Contains("scaffold", StringComparison.Ordinal) ||
               normalizedReason.Contains("before marking as completed", StringComparison.Ordinal);
    }

    private static bool IsHardRequiredProcessToolName(string toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return false;
        }

        return !toolName.StartsWith("browser_", StringComparison.Ordinal) ||
               RequiredBrowserEvidenceToolNames.Contains(toolName);
    }

    private static bool IsNegatedRequiredToolReference(string workBriefText, Match match)
    {
        if (!match.Success)
        {
            return false;
        }

        var segmentStart = FindInstructionSegmentStart(workBriefText, match.Index);
        var contextLength = match.Index - segmentStart;
        if (contextLength <= 0)
        {
            return false;
        }

        var context = workBriefText.Substring(segmentStart, contextLength);
        if (string.IsNullOrWhiteSpace(context))
        {
            return false;
        }

        var normalizedContext = Regex.Replace(context, @"\s+", " ").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedContext))
        {
            return false;
        }

        foreach (var phrase in NegatedRequiredToolPhrases)
        {
            if (normalizedContext.Contains(phrase, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static int FindInstructionSegmentStart(string workBriefText, int matchIndex)
    {
        if (matchIndex <= 0)
        {
            return 0;
        }

        var segmentStart = 0;
        for (var index = matchIndex - 1; index >= 0; index--)
        {
            var current = workBriefText[index];
            if (current is '\r' or '\n' or '.' or '!' or '?' or ';')
            {
                segmentStart = index + 1;
                break;
            }
        }

        return segmentStart;
    }

    private static bool IsCriticalToolReceipt(ToolExecutionReceiptRecord receipt)
    {
        if (!string.Equals(receipt.ToolFamily, "workspace-process", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var toolName = NormalizeToolToken(receipt.ToolName);
        return !string.IsNullOrWhiteSpace(toolName) &&
               !NonCriticalWorkspaceProcessToolNames.Contains(toolName);
    }

    private static bool IsFailedToolReceipt(ToolExecutionReceiptRecord receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.ExitSummary))
        {
            return false;
        }

        return receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) ||
               receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase) ||
               receipt.ExitSummary.StartsWith("TimedOut", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeToolToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('-', '_').Trim().ToLowerInvariant();
    }

    private static string? ResolveProviderNativeBrowserWorkingDirectory(ExecutionRunDetail detail)
    {
        return detail.ToolReceipts
            .Where(receipt =>
                string.Equals(NormalizeToolToken(receipt.ToolName), "local_mcp_launch", StringComparison.Ordinal) &&
                receipt.RequestSummary.Contains("@playwright/mcp", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(receipt.WorkingDirectory) &&
                !IsFailedToolReceipt(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .Select(receipt => receipt.WorkingDirectory.Trim())
            .FirstOrDefault();
    }

    private static string ResolveProviderNativeBrowserToolName(string expectedRelativePath)
    {
        return Path.GetExtension(expectedRelativePath).ToLowerInvariant() switch
        {
            ".png" => "browser_take_screenshot",
            ".yml" or ".yaml" => "browser_snapshot",
            ".log" or ".txt" => "browser_console_messages",
            _ => string.Empty
        };
    }

    private static bool MatchesExpectedBrowserOutputFile(string expectedRelativePath, string outputFileName)
    {
        var normalizedExpectedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(expectedRelativePath);
        var normalizedOutputPath = WorkspaceScopeDescriptor.NormalizeRelativePath(outputFileName);
        if (string.Equals(normalizedExpectedPath, normalizedOutputPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedFileName = Path.GetFileName(normalizedExpectedPath);
        var outputFileNameOnly = Path.GetFileName(normalizedOutputPath);
        if (!string.Equals(expectedFileName, outputFileNameOnly, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedDirectoryName = Path.GetFileName(Path.GetDirectoryName(normalizedExpectedPath) ?? string.Empty);
        var outputDirectoryName = Path.GetFileName(Path.GetDirectoryName(normalizedOutputPath) ?? string.Empty);
        return string.Equals(expectedDirectoryName, outputDirectoryName, StringComparison.OrdinalIgnoreCase);
    }

    private static string GuessContentTypeFromPath(string fullPath)
    {
        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".svg" => "image/svg+xml",
            ".yml" or ".yaml" => "text/yaml",
            ".log" or ".txt" => "text/plain",
            ".md" => "text/markdown",
            ".json" => "application/json",
            _ => "application/octet-stream"
        };
    }

    private static string BuildArtifactTitle(ExecutionArtifactRecord artifact)
    {
        return string.IsNullOrWhiteSpace(artifact.DisplayName)
            ? Path.GetFileName(artifact.RelativePath)
            : artifact.DisplayName.Trim();
    }

    private static string BuildExternalReferenceKey(ExecutionArtifactRecord artifact)
    {
        return $"agentframework-artifact:{artifact.Id:D}";
    }

    private static string BuildCompletedDecisionArtifactExternalReferenceKey(Guid stepRunId, Guid artifactExpectationId)
    {
        return $"process-step-decision:{stepRunId:D}:{artifactExpectationId:D}";
    }

    private static string BuildProviderNativeBrowserArtifactExternalReferenceKey(Guid executionRunId, string relativePath)
    {
        return $"agentframework-browser-artifact:{executionRunId:D}:{WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath)}";
    }

    private static string BuildStorageRelativePath(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        var normalizedRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifact.RelativePath);
        if (!string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            return normalizedRelativePath;
        }

        return $"process-runs/{candidate.Run.Id:D}/{candidate.StepRun.Id:D}/{Path.GetFileName(artifact.RelativePath)}";
    }

    private static ProcessArtifactKind ResolveProcessArtifactKind(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        var matchedExpectation = ResolveArtifactExpectation(candidate, artifact);
        if (matchedExpectation is not null)
        {
            return matchedExpectation.ArtifactKind;
        }

        if (artifact.RelativePath.EndsWith("/response.md", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        var relativePath = artifact.RelativePath.Replace('\\', '/');
        var fileName = Path.GetFileName(relativePath);
        var extension = Path.GetExtension(fileName);

        if (artifact.ContentType.Contains("image", StringComparison.OrdinalIgnoreCase) ||
            IsImageExtension(extension))
        {
            return ProcessArtifactKind.Evidence;
        }

        if (ContainsArtifactHint(fileName, "checklist"))
        {
            return ProcessArtifactKind.Checklist;
        }

        if (ContainsArtifactHint(fileName, "decision"))
        {
            return ProcessArtifactKind.Decision;
        }

        if (ContainsArtifactHint(fileName, "brief"))
        {
            return ProcessArtifactKind.Brief;
        }

        if (ContainsArtifactHint(fileName, "prompt"))
        {
            return ProcessArtifactKind.Prompt;
        }

        if (ContainsArtifactHint(fileName, "dataset"))
        {
            return ProcessArtifactKind.Dataset;
        }

        if (ContainsArtifactHint(fileName, "log") ||
            ContainsArtifactHint(fileName, "transcript") ||
            ContainsArtifactHint(fileName, "stdout") ||
            ContainsArtifactHint(fileName, "stderr") ||
            extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".txt", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        return string.Equals(artifact.ArtifactKind, "generated-output", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               IsCodeOrProjectExtension(extension)
            ? ProcessArtifactKind.Deliverable
            : ProcessArtifactKind.Evidence;
    }

    private static StorageContentKind ResolveStorageContentKind(string contentType, string fullPath)
    {
        if (contentType.Contains("markdown", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Markdown;
        }

        if (contentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Json;
        }

        if (contentType.Contains("image", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Image;
        }

        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
        {
            return StorageContentKind.Pdf;
        }

        return Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".md" => StorageContentKind.Markdown,
            ".json" => StorageContentKind.Json,
            ".svg" => StorageContentKind.Image,
            ".png" => StorageContentKind.Image,
            ".jpg" or ".jpeg" => StorageContentKind.Image,
            ".pdf" => StorageContentKind.Pdf,
            ".txt" or ".log" => StorageContentKind.Log,
            _ => StorageContentKind.Unknown
        };
    }

    private static string NormalizeTrigger(string trigger, Guid? stepRunId)
    {
        if (!string.IsNullOrWhiteSpace(trigger))
        {
            return trigger.Trim();
        }

        return stepRunId.HasValue
            ? $"step:{stepRunId.Value:D}"
            : "process-runtime";
    }

    private static bool IsWithinWorkspace(string workspaceRoot, string fullPath)
    {
        return fullPath.StartsWith(workspaceRoot, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<DispatchArtifactInput> BuildResolvedArtifactInputs(
        IReadOnlyList<ProcessStepArtifactInputDefinition> configuredInputs,
        IReadOnlyDictionary<Guid, ProcessArtifactExpectation> artifactExpectationsById,
        IReadOnlyDictionary<Guid, ProcessStepDefinition> sourceStepsById,
        IReadOnlyDictionary<Guid, IReadOnlyList<ProcessStepRun>> stepRunsByDefinitionId,
        IReadOnlyList<ProcessArtifactRecord> existingArtifacts)
    {
        if (configuredInputs.Count == 0)
        {
            return [];
        }

        var resolvedInputs = new List<DispatchArtifactInput>(configuredInputs.Count);
        foreach (var configuredInput in configuredInputs)
        {
            if (!artifactExpectationsById.TryGetValue(configuredInput.ArtifactExpectationId, out var artifactExpectation))
            {
                continue;
            }

            sourceStepsById.TryGetValue(artifactExpectation.StepDefinitionId, out var sourceStepDefinition);
            stepRunsByDefinitionId.TryGetValue(artifactExpectation.StepDefinitionId, out var sourceStepRuns);
            var sourceStepRunIds = sourceStepRuns?
                .Select(item => item.Id)
                .ToHashSet()
                ?? [];
            var matchingArtifacts = existingArtifacts
                .Where(item =>
                    item.StepRunId.HasValue &&
                    sourceStepRunIds.Contains(item.StepRunId.Value) &&
                    SatisfiesExpectedArtifactInput(item, artifactExpectation))
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(3)
                .Select(item => new DispatchArtifactReference(
                    item.Title,
                    item.ArtifactKind.ToString(),
                    item.ManagedStoragePath,
                    item.ReviewSummary,
                    item.ProvenanceSummary))
                .ToList();

            resolvedInputs.Add(new DispatchArtifactInput(
                sourceStepDefinition?.Title ?? "Unknown upstream step",
                artifactExpectation.Title,
                matchingArtifacts));
        }

        return resolvedInputs;
    }

    private static string BuildArtifactInputSummary(IReadOnlyList<DispatchArtifactInput> artifactInputs)
    {
        if (artifactInputs.Count == 0)
        {
            return "No configured upstream artifact inputs for this step.";
        }

        var builder = new StringBuilder();
        foreach (var artifactInput in artifactInputs)
        {
            builder.Append("- Source step: ");
            builder.Append(artifactInput.SourceStepTitle);
            builder.Append(" | Expected artifact: ");
            builder.AppendLine(artifactInput.ExpectedArtifactTitle);

            if (artifactInput.Artifacts.Count == 0)
            {
                builder.AppendLine("  No recorded upstream artifacts are attached yet. If the contract cannot be fulfilled without them, stop and say so explicitly.");
                continue;
            }

            foreach (var artifact in artifactInput.Artifacts)
            {
                builder.Append("  - ");
                builder.Append(artifact.Title);
                builder.Append(" [");
                builder.Append(artifact.ArtifactKind);
                builder.Append(']');
                if (!string.IsNullOrWhiteSpace(artifact.ManagedStoragePath))
                {
                    builder.Append(" @ ");
                    builder.Append(artifact.ManagedStoragePath);
                }

                builder.AppendLine();
                if (!string.IsNullOrWhiteSpace(artifact.ReviewSummary))
                {
                    builder.Append("    Review: ");
                    builder.AppendLine(TrimForPrompt(artifact.ReviewSummary, 240));
                }

                if (!string.IsNullOrWhiteSpace(artifact.ProvenanceSummary))
                {
                    builder.Append("    Provenance: ");
                    builder.AppendLine(TrimForPrompt(artifact.ProvenanceSummary, 240));
                }
            }
        }

        return builder.ToString().TrimEnd();
    }

    private IReadOnlyList<DispatchArtifactInput> PrepareArtifactInputsForPrompt(
        IReadOnlyList<DispatchArtifactInput> artifactInputs,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope)
    {
        if (artifactInputs.Count == 0 || workspaceScope.IsDefaultSandbox)
        {
            return artifactInputs;
        }

        var preparedInputs = new List<DispatchArtifactInput>(artifactInputs.Count);
        foreach (var artifactInput in artifactInputs)
        {
            var preparedArtifacts = new List<DispatchArtifactReference>(artifactInput.Artifacts.Count);
            foreach (var artifact in artifactInput.Artifacts)
            {
                var preparedPath = PrepareManagedArtifactPathForPrompt(
                    artifact.ManagedStoragePath,
                    workspaceRoot,
                    workspaceScope);
                preparedArtifacts.Add(string.Equals(preparedPath, artifact.ManagedStoragePath, StringComparison.OrdinalIgnoreCase)
                    ? artifact
                    : artifact with
                    {
                        ManagedStoragePath = preparedPath
                    });
            }

            preparedInputs.Add(new DispatchArtifactInput(
                artifactInput.SourceStepTitle,
                artifactInput.ExpectedArtifactTitle,
                preparedArtifacts));
        }

        return preparedInputs;
    }

    private string PrepareManagedArtifactPathForPrompt(
        string managedStoragePath,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope)
    {
        if (string.IsNullOrWhiteSpace(managedStoragePath))
        {
            return string.Empty;
        }

        var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(managedStoragePath);
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return string.Empty;
        }

        var scopedPath = ResolveScopedManagedRelativePath(workspaceScope, normalizedPath);
        if (string.Equals(scopedPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath;
        }

        var sourceFullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            normalizedPath.Replace('/', Path.DirectorySeparatorChar)));
        var scopedFullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            scopedPath.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinWorkspace(workspaceRoot, sourceFullPath) ||
            !IsWithinWorkspace(workspaceRoot, scopedFullPath) ||
            !File.Exists(sourceFullPath))
        {
            return normalizedPath;
        }

        var scopedDirectory = Path.GetDirectoryName(scopedFullPath);
        if (!string.IsNullOrWhiteSpace(scopedDirectory))
        {
            Directory.CreateDirectory(scopedDirectory);
        }

        if (!string.Equals(sourceFullPath, scopedFullPath, StringComparison.OrdinalIgnoreCase))
        {
            File.Copy(sourceFullPath, scopedFullPath, overwrite: true);
        }

        return File.Exists(scopedFullPath)
            ? scopedPath
            : normalizedPath;
    }

    private static string TrimForPrompt(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength].TrimEnd() + "...";
    }

    private static bool SatisfiesExpectedArtifactInput(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return false;
        }

        if (artifact.ArtifactExpectationId.HasValue)
        {
            return artifact.ArtifactExpectationId.Value == expectation.Id;
        }

        return string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildExpectedArtifactSummary(IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        if (expectedArtifacts.Count == 0)
        {
            return "No explicit artifact outputs are configured for this step.";
        }

        var builder = new StringBuilder();
        foreach (var expectedArtifact in expectedArtifacts)
        {
            builder.Append("- ");
            builder.Append(expectedArtifact.Title);
            builder.Append(" [");
            builder.Append(expectedArtifact.ArtifactKind);
            builder.Append(']');
            if (expectedArtifact.IsRequired)
            {
                builder.Append(" required");
            }

            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(expectedArtifact.ValidationRequirementSummary))
            {
                builder.Append("  Validation: ");
                builder.AppendLine(TrimForPrompt(expectedArtifact.ValidationRequirementSummary, 240));
            }

            builder.Append("  Trust: ");
            builder.Append(expectedArtifact.TrustRequirement);
            builder.Append(" | Sensitivity: ");
            builder.AppendLine(expectedArtifact.SensitivityLevel.ToString());
        }

        return builder.ToString().TrimEnd();
    }

    private static Guid? ResolveArtifactExpectationId(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        return ResolveArtifactExpectation(candidate, artifact)?.Id;
    }

    private static DispatchArtifactExpectation? ResolveArtifactExpectation(
        DispatchCandidate candidate,
        ExecutionArtifactRecord artifact)
    {
        var matchedExpectationId = MatchExpectedArtifactId(candidate.ExpectedArtifacts, artifact);
        if (!matchedExpectationId.HasValue)
        {
            return null;
        }

        return candidate.ExpectedArtifacts.FirstOrDefault(item => item.Id == matchedExpectationId.Value);
    }

    internal static Guid? MatchExpectedArtifactId(
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts,
        ExecutionArtifactRecord artifact)
    {
        if (expectedArtifacts.Count == 0)
        {
            return null;
        }

        if (IsTransientExecutionArtifact(artifact))
        {
            return null;
        }

        var relativePath = artifact.RelativePath.Replace('\\', '/');
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(relativePath);
        var displayName = BuildArtifactTitle(artifact);
        var displaySlug = FileSafeSlugBuilder.Build(displayName);
        var fileSlug = FileSafeSlugBuilder.Build(fileNameWithoutExtension);
        var expectedKind = ResolveExpectedArtifactKind(artifact);
        var strongMatches = expectedArtifacts
            .Where(item => MatchesExpectedArtifact(item, relativePath, displayName, displaySlug, fileSlug))
            .ToList();
        if (strongMatches.Count == 1)
        {
            return strongMatches[0].Id;
        }

        if (strongMatches.Count > 1)
        {
            var kindMatches = strongMatches
                .Where(item => item.ArtifactKind == expectedKind)
                .ToList();
            if (kindMatches.Count == 1)
            {
                return kindMatches[0].Id;
            }
        }

        return null;
    }

    private static bool MatchesExpectedArtifact(
        DispatchArtifactExpectation expectedArtifact,
        string relativePath,
        string displayName,
        string displaySlug,
        string fileSlug)
    {
        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var expectedRelativePath))
        {
            return string.Equals(
                NormalizeManagedRelativePathForComparison(expectedRelativePath),
                NormalizeManagedRelativePathForComparison(relativePath),
                StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(expectedArtifact.Title, displayName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var expectedSlug = FileSafeSlugBuilder.Build(expectedArtifact.Title);
        return string.Equals(expectedSlug, displaySlug, StringComparison.Ordinal) ||
               string.Equals(expectedSlug, fileSlug, StringComparison.Ordinal) ||
               relativePath.Contains(expectedSlug, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryExtractExpectedArtifactRelativePath(string validationRequirementSummary, out string relativePath)
    {
        foreach (var marker in new[]
                 {
                     "Create this artifact at ",
                     "must exist at ",
                     "must be written at "
                 })
        {
            var markerIndex = validationRequirementSummary.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var startIndex = markerIndex + marker.Length;
            var remainder = validationRequirementSummary[startIndex..].TrimStart();
            if (string.IsNullOrWhiteSpace(remainder))
            {
                continue;
            }

            var endIndex = remainder.IndexOfAny([' ', '\r', '\n', '\t']);
            var token = endIndex >= 0
                ? remainder[..endIndex]
                : remainder;
            token = token.Trim().TrimEnd('.', ',', ';', ':').Replace('\\', '/');
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            relativePath = token;
            return true;
        }

        relativePath = string.Empty;
        return false;
    }

    private static GovernedInspectionPaths ResolveGovernedInspectionPaths(IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        var statPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var readPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var expectedArtifact in expectedArtifacts)
        {
            if (!TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var relativePath))
            {
                continue;
            }

            var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            statPaths.Add(normalizedPath);
            if (IsTextReadableManagedArtifactPath(normalizedPath))
            {
                readPaths.Add(normalizedPath);
            }
        }

        return new GovernedInspectionPaths(statPaths.ToList(), readPaths.ToList());
    }

    private static GovernedInspectionPaths ResolveArtifactInputInspectionPaths(IReadOnlyList<DispatchArtifactInput> artifactInputs)
    {
        var statPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        var readPaths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var artifactInput in artifactInputs)
        {
            foreach (var artifact in artifactInput.Artifacts)
            {
                var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifact.ManagedStoragePath);
                if (string.IsNullOrWhiteSpace(normalizedPath))
                {
                    continue;
                }

                statPaths.Add(normalizedPath);
                if (IsTextReadableManagedArtifactPath(normalizedPath))
                {
                    readPaths.Add(normalizedPath);
                }
            }
        }

        return new GovernedInspectionPaths(statPaths.ToList(), readPaths.ToList());
    }

    private static string FormatPromptPathList(IReadOnlyList<string> relativePaths)
    {
        return string.Join(", ", relativePaths.Select(relativePath => $"`{relativePath}`"));
    }

    private static string NormalizeManagedRelativePathForComparison(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length >= 5 &&
            IsManagedRootSegment(segments[0]) &&
            string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase))
        {
            return string.Join('/', [segments[0], .. segments.Skip(4)]);
        }

        return normalized;
    }

    private static bool IsResponseProjectableTextArtifact(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveResponseTextArtifactRelativePath(
        DispatchCandidate candidate,
        WorkspaceScopeDescriptor workspaceScope,
        DispatchArtifactExpectation expectedArtifact,
        out string relativePath)
    {
        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var declaredRelativePath))
        {
            if (!IsResponseProjectableTextArtifact(declaredRelativePath))
            {
                relativePath = string.Empty;
                return false;
            }

            relativePath = ResolveScopedManagedRelativePath(workspaceScope, declaredRelativePath);
            return !string.IsNullOrWhiteSpace(relativePath);
        }

        if (!CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact))
        {
            relativePath = string.Empty;
            return false;
        }

        relativePath = ResolveScopedManagedRelativePath(
            workspaceScope,
            BuildFallbackResponseTextArtifactRelativePath(candidate, expectedArtifact));
        return !string.IsNullOrWhiteSpace(relativePath);
    }

    private static bool CanProjectResponseTextArtifactWithoutDeclaredPath(DispatchArtifactExpectation expectedArtifact)
    {
        return expectedArtifact.ArtifactKind is ProcessArtifactKind.Brief
            or ProcessArtifactKind.Checklist
            or ProcessArtifactKind.Prompt
            or ProcessArtifactKind.Transcript;
    }

    private static string BuildFallbackResponseTextArtifactRelativePath(
        DispatchCandidate candidate,
        DispatchArtifactExpectation expectedArtifact)
    {
        var expectedSlug = FileSafeSlugBuilder.Build(expectedArtifact.Title);
        if (string.IsNullOrWhiteSpace(expectedSlug))
        {
            expectedSlug = "artifact";
        }

        return WorkspaceScopeDescriptor.NormalizeRelativePath(
            Path.Combine(
                "artifacts",
                "process-runs",
                candidate.Run.Id.ToString("D"),
                $"{candidate.StepRun.Sequence + 1:00}-{expectedSlug}.md"));
    }

    private static bool IsTextReadableManagedArtifactPath(string relativePath)
    {
        var extension = Path.GetExtension(relativePath);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".log", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".xml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
               IsCodeOrProjectExtension(extension);
    }

    private static bool ShouldProjectFinalAssistantResponse(ExecutionRunRecord run)
    {
        return run.State == ExecutionState.Completed &&
               run.Outcome == RunOutcome.Succeeded;
    }

    private static bool ShouldProjectResponseTextArtifacts(
        ExecutionRunRecord run,
        ProcessStepRunStatus completionStatus)
    {
        return completionStatus == ProcessStepRunStatus.Completed &&
               ShouldProjectFinalAssistantResponse(run);
    }

    private static string BuildResponseTextArtifactExternalReferenceKey(Guid executionRunId, string relativePath)
    {
        return $"assistant-response|{executionRunId:D}|{NormalizeManagedRelativePathForComparison(relativePath)}";
    }

    private static bool ShouldAutoRecordCompletedDecisionArtifact(DispatchArtifactExpectation expectedArtifact)
    {
        return expectedArtifact.IsRequired &&
               expectedArtifact.ArtifactKind == ProcessArtifactKind.Decision &&
               expectedArtifact.TrustRequirement is ProcessArtifactTrustRequirement.ReviewRequired or ProcessArtifactTrustRequirement.HumanApproved;
    }

    private static ProcessArtifactTrustStatus ResolveCompletedDecisionArtifactTrustStatus(
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.HumanApproved => ProcessArtifactTrustStatus.Approved,
            _ => ProcessArtifactTrustStatus.ReviewRequired
        };
    }

    private static string BuildCompletedDecisionArtifactProvenanceSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        var executorName = string.IsNullOrWhiteSpace(candidate.StepRun.CurrentExecutorName)
            ? "the assigned approver"
            : candidate.StepRun.CurrentExecutorName.Trim();
        return $"Recorded from the governed step outcome for AgentFramework execution run {detail.Run.Id:D} by {executorName}.";
    }

    private static string BuildCompletedDecisionArtifactReviewSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        DispatchArtifactExpectation expectedArtifact)
    {
        var executorName = string.IsNullOrWhiteSpace(candidate.StepRun.CurrentExecutorName)
            ? "The assigned approver"
            : candidate.StepRun.CurrentExecutorName.Trim();
        var summary = ResolveCompletedDecisionArtifactOutcomeSummary(candidate, detail, responseText);
        var builder = new StringBuilder();
        builder.Append(executorName);
        builder.Append(" completed step '");
        builder.Append(candidate.StepRun.Title);
        builder.Append("' and recorded decision artifact '");
        builder.Append(expectedArtifact.Title);
        builder.Append("'.");

        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.Append(' ');
            builder.Append(EnsureTerminalPunctuation(summary));
        }

        if (!string.IsNullOrWhiteSpace(expectedArtifact.ValidationRequirementSummary))
        {
            builder.Append(" Validation expectation: ");
            builder.Append(EnsureTerminalPunctuation(expectedArtifact.ValidationRequirementSummary.Trim()));
        }

        return builder.ToString();
    }

    private static string ResolveCompletedDecisionArtifactOutcomeSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText)
    {
        if (TryResolveDeclaredStepOutcome(responseText, out var declaredOutcome) &&
            !string.IsNullOrWhiteSpace(declaredOutcome.Reason))
        {
            return declaredOutcome.Reason.Trim();
        }

        if (!string.IsNullOrWhiteSpace(candidate.StepRun.DecisionSummary))
        {
            return candidate.StepRun.DecisionSummary.Trim();
        }

        if (!string.IsNullOrWhiteSpace(detail.Run.ResultSummary))
        {
            return detail.Run.ResultSummary.Trim();
        }

        return string.Empty;
    }

    private static string EnsureTerminalPunctuation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.EndsWith('.') || trimmed.EndsWith('!') || trimmed.EndsWith('?'))
        {
            return trimmed;
        }

        return $"{trimmed}.";
    }

    private static string ResolveScopedManagedRelativePath(WorkspaceScopeDescriptor workspaceScope, string relativePath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (workspaceScope.IsDefaultSandbox || string.IsNullOrWhiteSpace(normalized))
        {
            return normalized;
        }

        return TryResolveScopedManagedRelativePath(normalized, "artifacts", workspaceScope.ArtifactRootRelativePath)
            ?? TryResolveScopedManagedRelativePath(normalized, "output", workspaceScope.OutputRootRelativePath)
            ?? TryResolveScopedManagedRelativePath(normalized, "integration-map", workspaceScope.IntegrationMapRootRelativePath)
            ?? TryResolveScopedManagedRelativePath(normalized, "data", workspaceScope.DataRootRelativePath)
            ?? normalized;
    }

    private static string? TryResolveScopedManagedRelativePath(string relativePath, string rootName, string scopedRootRelativePath)
    {
        if (!IsManagedRootMatch(relativePath, rootName))
        {
            return null;
        }

        if (IsManagedRootMatch(relativePath, scopedRootRelativePath))
        {
            return relativePath;
        }

        var foreignScopedPrefix = $"{rootName}/scopes/";
        if (relativePath.StartsWith(foreignScopedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return relativePath;
        }

        var suffix = RemoveManagedRoot(relativePath, rootName);
        return string.IsNullOrWhiteSpace(suffix)
            ? scopedRootRelativePath
            : WorkspaceScopeDescriptor.NormalizeRelativePath(Path.Combine(scopedRootRelativePath, suffix));
    }

    private static bool IsManagedRootMatch(string relativePath, string rootRelativePath)
    {
        return string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase) ||
               relativePath.StartsWith(rootRelativePath + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveManagedRoot(string relativePath, string rootRelativePath)
    {
        if (string.Equals(relativePath, rootRelativePath, StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        return relativePath[(rootRelativePath.Length + 1)..];
    }

    private static bool IsManagedRootSegment(string segment)
    {
        return string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "integration-map", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(segment, "data", StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessArtifactKind ResolveExpectedArtifactKind(ExecutionArtifactRecord artifact)
    {
        if (artifact.RelativePath.EndsWith("/response.md", StringComparison.OrdinalIgnoreCase))
        {
            return ProcessArtifactKind.Transcript;
        }

        var fileName = Path.GetFileName(artifact.RelativePath.Replace('\\', '/'));
        var extension = Path.GetExtension(fileName);
        if (artifact.ContentType.Contains("image", StringComparison.OrdinalIgnoreCase) || IsImageExtension(extension))
        {
            return ProcessArtifactKind.Evidence;
        }

        if (ContainsArtifactHint(fileName, "checklist"))
        {
            return ProcessArtifactKind.Checklist;
        }

        if (ContainsArtifactHint(fileName, "log") ||
            ContainsArtifactHint(fileName, "transcript") ||
            ContainsArtifactHint(fileName, "stdout") ||
            ContainsArtifactHint(fileName, "stderr"))
        {
            return ProcessArtifactKind.Transcript;
        }

        return string.Equals(artifact.ArtifactKind, "generated-output", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               IsCodeOrProjectExtension(extension)
            ? ProcessArtifactKind.Deliverable
            : ProcessArtifactKind.Evidence;
    }

    private static bool ContainsArtifactHint(string fileName, string hint)
    {
        return fileName.Contains(hint, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCodeOrProjectExtension(string extension)
    {
        return extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".razor", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".csproj", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".sln", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsImageExtension(string extension)
    {
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsTransientExecutionArtifact(ExecutionArtifactRecord artifact)
    {
        var relativePath = artifact.RelativePath.Replace('\\', '/');
        return relativePath.StartsWith(".playwright-mcp/", StringComparison.OrdinalIgnoreCase) ||
               relativePath.Contains("/.playwright-mcp/", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IReadOnlyList<DispatchArtifactExpectation>> LoadExpectedArtifactsAsync(
        AppDbContext dbContext,
        Guid stepDefinitionId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<ProcessArtifactExpectation>()
            .AsNoTracking()
            .Where(item => item.StepDefinitionId == stepDefinitionId)
            .OrderBy(item => item.Title)
            .Select(item => new DispatchArtifactExpectation(
                item.Id,
                item.ArtifactKind,
                item.Title,
                item.IsRequired,
                item.TrustRequirement,
                item.SensitivityLevel,
                item.ValidationRequirementSummary,
                item.AllowedFutureUsageSummary))
            .ToListAsync(cancellationToken);
    }

    private sealed record DispatchCandidate(
        ProcessRun Run,
        ProcessDefinition Definition,
        ProcessStepRun StepRun,
        ProcessWorkBrief? WorkBrief,
        Guid TechnicalAgentId,
        IReadOnlyList<DispatchArtifactExpectation> ExpectedArtifacts,
        IReadOnlyList<DispatchArtifactInput> ArtifactInputs,
        HashSet<string> ExternalReferenceKeys,
        Guid? ChatSessionId,
        Guid? RecoveryExecutionRunId,
        IReadOnlyList<DispatchBranchOutcome> BranchOutcomes,
        bool RequiresExplicitBranchOutcomeSelection);

    private sealed record ConcurrentAutomationExecution(
        Guid ExecutionRunId,
        ExecutionRunDetail Detail,
        string ResponseText);

    private sealed record StepRunTransitionSnapshot(
        Guid Id,
        ProcessStepRunStatus Status,
        Guid ConcurrencyToken);

    internal sealed record DispatchArtifactExpectation(
        Guid Id,
        ProcessArtifactKind ArtifactKind,
        string Title,
        bool IsRequired,
        ProcessArtifactTrustRequirement TrustRequirement,
        ProcessSensitivityLevel SensitivityLevel,
        string ValidationRequirementSummary,
        string AllowedFutureUsageSummary);

    private sealed record DispatchArtifactInput(
        string SourceStepTitle,
        string ExpectedArtifactTitle,
        IReadOnlyList<DispatchArtifactReference> Artifacts);

    private sealed record DispatchArtifactReference(
        string Title,
        string ArtifactKind,
        string ManagedStoragePath,
        string ReviewSummary,
        string ProvenanceSummary);

    private sealed record GovernedInspectionPaths(
        IReadOnlyList<string> StatPaths,
        IReadOnlyList<string> ReadPaths);

    private sealed record DispatchExecutionOutcome(
        ExecutionRunDetail Detail,
        string ResponseText,
        ProcessStepRunStatus CompletionStatus,
        string CompletionReason,
        IReadOnlyList<string> MissingRequiredTools,
        int AttemptNumber,
        Guid? SelectedBranchOutcomeId);

    private sealed record ProviderFallbackResolution(
        ProviderProfile Provider,
        string Model,
        string HealthSummary);

    private sealed record ProviderRepairOutcome(
        string FailedProviderName,
        string FallbackProviderName,
        string FallbackModel,
        int AffectedAgentCount,
        string FailureSummary);

    private readonly record struct DeclaredStepOutcome(
        ProcessStepRunStatus Status,
        string Reason,
        Guid? SelectedBranchOutcomeId,
        string BranchOutcomeKey,
        string BranchOutcomeTitle);

    private sealed record DispatchBranchOutcome(
        Guid Id,
        string Key,
        string Title,
        string Description);

    private sealed record SessionToolCall(string ToolName, string OutputFileName);
}
