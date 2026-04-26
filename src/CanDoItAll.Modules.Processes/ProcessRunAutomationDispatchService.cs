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

public interface IProcessRunAutomationDispatchService
{
    Task DispatchAsync(
        Guid processRunId,
        Guid? triggerStepRunId,
        string trigger,
        Func<CancellationToken, Task>? renewLeaseAsync = null,
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
    IOptions<ProcessRuntimeOptions> processRuntimeOptions,
    IClock clock,
    ILogger<ProcessRunAutomationDispatchService> logger) : IProcessRunAutomationDispatchService
{
    private const string AutomationActor = "process-automation-dispatch";
    private const string ExternalTargetAliasRoot = "external-target";
    private const int DefaultMaxExecutionAttempts = 3;
    private const int ConcreteImplementationMaxExecutionAttempts = 5;
    private const int MaxBrowserSnapshotInspectionCharacters = 262_144;
    private const string ProcessMockSessionFlagPropertyName = "processMockAgent";
    private const string ProcessMockRoleKeyPropertyName = "roleKey";
    private const string ProcessMockArtifactRootPropertyName = "artifactRoot";
    private const string ProcessMockBranchOutcomeKeyPropertyName = "branchOutcomeKey";
    private const string ProcessMockProductOwnerRoleKey = "product-owner";
    private const string ProcessMockArchitectRoleKey = "architect";
    private const string ProcessMockDeveloperRoleKey = "developer";
    private const string ProcessMockQaRoleKey = "qa";
    private const string ProcessMockRepairDeveloperRoleKey = "repair-developer";
    private const string ProcessMockReleaseManagerRoleKey = "release-manager";
    private const string ProcessMockBranchRepairsRequired = "repairs-required";
    private const string ProcessMockBranchApproved = "approved";
    private static readonly TimeSpan FreshInProgressRecoveryGracePeriod = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan StaleAutomationExecutionRunTimeout = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> StepDispatchGuards = [];
    private static readonly Regex RequiredToolNameRegex = new(
        @"\b(?:workspace|browser|project_structure)_[a-z0-9_]+\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex DeclaredStepOutcomeRegex = new(
        @"<!--\s*PROCESS_STEP_OUTCOME\s*(?<json>\{[^\r\n]*\})\s*-->",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ProjectPathInToolRequestRegex = new(
        @"(?<path>[A-Za-z]:\\[^`""'\r\n]+?\.csproj|external-target/[^\s`""']+?\.csproj)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex WorkspacePathInToolRequestRegex = new(
        @"(?<path>[A-Za-z]:\\[^`""'\r\n\s]+|external-target/[^\s`""']+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RazorPageDirectiveRegex = new(
        @"(?m)^\s*@page\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CalculatorEngineInjectDirectiveRegex = new(
        @"(?m)^\s*@inject\s+[^\r\n]*\bCalculatorEngine\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CalculatorEngineServiceRegistrationRegex = new(
        @"\bAdd(?:Scoped|Singleton|Transient)\s*<\s*[^>]*\bCalculatorEngine\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MalformedDoubleQuotedRazorStringCallbackRegex = new(
        @"@on\w+\s*=\s*""[^""\r\n]*=>[^""\r\n]*\(\s*""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RazorCharLiteralCallbackRegex = new(
        @"@on\w+\s*=\s*""[^""\r\n]*=>[^""\r\n]*\b(?<handler>[A-Za-z_][A-Za-z0-9_]*)\s*\(\s*'[^'\r\n]+'",
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
    private static readonly HashSet<string> ConcurrentAutomationSessionBusyMessages = new(StringComparer.OrdinalIgnoreCase)
    {
        "This session already has an active execution run. Wait for it to finish before sending a new prompt.",
        "This session has pending tool approvals. Approve or reject them before sending a new prompt."
    };
    private static readonly string[] ImplementationProofToolNames =
    [
        "workspace_stat_path",
        "workspace_read_file",
        "workspace_dotnet_build"
    ];
    private static readonly HashSet<string> CurrentAttemptOnlyImplementationProofToolNames =
    [
        "workspace_stat_path",
        "workspace_read_file",
        "workspace_dotnet_build",
        "workspace_dotnet_test"
    ];
    private static readonly HashSet<string> CurrentAttemptOnlyBrowserProofToolNames =
    [
        "browser_console_messages",
        "browser_snapshot",
        "browser_take_screenshot"
    ];
    private static readonly HashSet<string> ConcreteProductMutationToolNames =
    [
        "workspace_dotnet_new",
        "workspace_write_file",
        "workspace_append_file",
        "workspace_move_path",
        "workspace_delete_path",
        "workspace_create_directory"
    ];
    private static readonly HashSet<string> ConcreteProductSourceWriteToolNames =
    [
        "workspace_write_file",
        "workspace_append_file",
        "workspace_move_path"
    ];
    private static readonly string[] ImplicitBrowserProofToolNames =
    [
        "browser_console_messages",
        "browser_snapshot",
        "browser_take_screenshot"
    ];
    private static readonly HashSet<string> ArtifactTitleNoiseTokens = new(StringComparer.Ordinal)
    {
        "artifact",
        "artifacts",
        "brief",
        "briefs",
        "checklist",
        "checklists",
        "doc",
        "docs",
        "document",
        "documents",
        "evidence",
        "file",
        "files",
        "note",
        "notes",
        "output",
        "outputs",
        "packet",
        "packets",
        "record",
        "records",
        "report",
        "reports"
    };
    private static readonly HashSet<string> ArtifactContentNoiseTokens = new(StringComparer.Ordinal)
    {
        "and",
        "are",
        "capture",
        "captured",
        "create",
        "created",
        "form",
        "must",
        "required",
        "should",
        "the",
        "this",
        "with"
    };
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
            var reusableChatSessionId = ResolveReusableAutomationChatSessionId(executionRuns);
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
                manualRecoveryDirective,
                availableBranchOutcomes,
                requiresExplicitBranchOutcomeSelection);
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
        if (stepStartedAtUtc.HasValue)
        {
            query = query.Where(item => item.OccurredAtUtc >= stepStartedAtUtc.Value);
        }

        var journalEntries = await query.ToListAsync(cancellationToken);
        return journalEntries
            .OrderByDescending(item => item.OccurredAtUtc)
            .Select(item => item.Description)
            .FirstOrDefault() ?? string.Empty;
    }

    private async Task<DispatchExecutionOutcome> ExecuteUntilSettledAsync(
        DispatchCandidate candidate,
        string trigger,
        Func<CancellationToken, Task>? renewLeaseAsync,
        CancellationToken cancellationToken)
    {
        DispatchExecutionOutcome? finalOutcome = null;
        string? recoveryDirective = string.IsNullOrWhiteSpace(candidate.ManualRecoveryDirective)
            ? null
            : candidate.ManualRecoveryDirective.Trim();
        var recoverableExecutionRunId = candidate.RecoveryExecutionRunId;
        var automationChatSessionId = string.IsNullOrWhiteSpace(recoveryDirective)
            ? candidate.ChatSessionId
            : null;
        var prefetchedProjectStructureGrounding = await TryBuildProjectStructureGroundingAsync(candidate, cancellationToken);
        var prefetchedArtifactInspectionGrounding = await TryBuildArtifactInspectionGroundingAsync(candidate, cancellationToken);
        var successfulToolNamesAcrossAttempts = new HashSet<string>(
            prefetchedProjectStructureGrounding.SatisfiedToolNames,
            StringComparer.Ordinal);
        successfulToolNamesAcrossAttempts.UnionWith(prefetchedArtifactInspectionGrounding.SatisfiedToolNames);
        var maxExecutionAttempts = ResolveMaxExecutionAttempts(candidate);

        for (var attemptNumber = 1; attemptNumber <= maxExecutionAttempts; attemptNumber++)
        {
            if (renewLeaseAsync is not null)
            {
                await renewLeaseAsync(cancellationToken);
            }

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
                    ExecutionRunResult? executionResult = null;
                    ConcurrentAutomationExecution? adoptedConcurrentExecution = null;
                    ExecutionRunDetail? failedExecutionDetail = null;
                    Guid? failedExecutionRunId = null;
                    string? failedResponseText = null;

                    try
                    {
                        executionResult = await workspaceService.ExecuteRunAsync(
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
                                    MetadataJson: "{}",
                                    ProcessRunId: candidate.Run.Id.ToString("D"),
                                    ProcessStepId: candidate.StepRun.Id.ToString("D")),
                                AutoApprovePendingToolCalls: true),
                            cancellationToken);
                    }
                    catch (AgentChatRunFailedException exception)
                    {
                        failedExecutionRunId = exception.ExecutionRunId;
                        automationChatSessionId ??= exception.ChatSessionId;
                        failedExecutionDetail = await workspaceService.GetExecutionRunDetailAsync(
                            exception.ExecutionRunId,
                            cancellationToken);
                        failedResponseText = ResolvePreferredExecutionResponseText(
                            candidate,
                            exception.Message,
                            failedExecutionDetail);

                        logger.LogWarning(
                            exception,
                            "Continuing recovery inspection for failed AgentFramework execution run {ExecutionRunId} on process step {StepRunId} and run {RunId}.",
                            exception.ExecutionRunId,
                            candidate.StepRun.Id,
                            candidate.Run.Id);
                    }
                    catch (InvalidOperationException exception)
                    {
                        if (!IsConcurrentAutomationSessionBusyException(exception))
                        {
                            throw;
                        }

                        adoptedConcurrentExecution = await TryAdoptConcurrentAutomationExecutionAsync(candidate, cancellationToken);
                        if (adoptedConcurrentExecution is null)
                        {
                            throw;
                        }

                        logger.LogInformation(
                            "Adopting concurrently-started AgentFramework execution run {ExecutionRunId} for process step {StepRunId} on run {RunId} after chat-session start collision. Message: {Message}",
                            adoptedConcurrentExecution.ExecutionRunId,
                            candidate.StepRun.Id,
                            candidate.Run.Id,
                            exception.Message);
                    }

                    if (adoptedConcurrentExecution is not null)
                    {
                        executionRunId = adoptedConcurrentExecution.ExecutionRunId;
                        detail = adoptedConcurrentExecution.Detail;
                        responseText = adoptedConcurrentExecution.ResponseText;
                        automationChatSessionId ??= detail.Run.ChatSessionId;
                    }
                    else if (failedExecutionDetail is not null && failedExecutionRunId.HasValue)
                    {
                        executionRunId = failedExecutionRunId.Value;
                        detail = failedExecutionDetail;
                        responseText = failedResponseText ?? ResolveRecoveredExecutionResponseText(detail);
                    }
                    else
                    {
                        if (executionResult is null)
                        {
                            throw new InvalidOperationException(
                                $"AgentFramework execution start did not return a result for process step '{candidate.StepRun.Id:D}'.");
                        }

                        executionRunId = executionResult.ExecutionRunId;
                        automationChatSessionId ??= executionResult.ChatSessionId;
                        detail = await workspaceService.GetExecutionRunDetailAsync(executionRunId, cancellationToken);
                        responseText = ResolvePreferredExecutionResponseText(candidate, executionResult.ResponseText, detail);
                    }
                }
            }

            successfulToolNamesAcrossAttempts.UnionWith(ResolveSuccessfulToolNames(detail));
            if (renewLeaseAsync is not null)
            {
                await renewLeaseAsync(cancellationToken);
            }

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
                    ? $"{completionReason} Recovered on attempt {attemptNumber} of {maxExecutionAttempts}."
                    : $"{completionReason} Recovery attempt {attemptNumber} of {maxExecutionAttempts}.";
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
                maxExecutionAttempts,
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

            var shouldRetry =
                ShouldRetryIncompleteSuccessfulRun(
                    candidate,
                    detail,
                    responseText,
                    missingRequiredTools,
                    attemptNumber,
                    maxExecutionAttempts) ||
                ShouldRetryRecoverableFailedRun(
                    candidate,
                    detail,
                    responseText,
                    missingRequiredTools,
                    unresolvedCriticalToolFailures,
                    attemptNumber,
                    maxExecutionAttempts);

            if (!shouldRetry)
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
                maxExecutionAttempts);

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

    private static int ResolveMaxExecutionAttempts(DispatchCandidate candidate)
    {
        return RequiresConcreteImplementationProof(candidate)
            ? ConcreteImplementationMaxExecutionAttempts
            : DefaultMaxExecutionAttempts;
    }

    private async Task<ProviderRepairOutcome?> TryRepairAssignedAgentProvidersAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string responseText,
        int attemptNumber,
        int maxExecutionAttempts,
        CancellationToken cancellationToken)
    {
        if (attemptNumber >= maxExecutionAttempts ||
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
        var workspaceScope = WorkspaceScopeDescriptor.Organization(
            databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id.ToString("N"));
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

            if (!TryResolveArtifactFullPath(workspaceRoot, artifact.RelativePath, out var fullPath, out var pathResolutionFailure) ||
                !File.Exists(fullPath))
            {
                logger.LogDebug(
                    "Skipping execution artifact projection for run {RunId}, step {StepRunId}, artifact {ArtifactId} because the file path is unavailable. Reason: {Reason}",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    artifact.Id,
                    string.IsNullOrWhiteSpace(pathResolutionFailure) ? "File does not exist." : pathResolutionFailure);
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

        await ProjectProcessMockArtifactsAsync(
            candidate,
            detail,
            workspaceRoot,
            workspaceScope,
            cancellationToken);
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

    private async Task ProjectProcessMockArtifactsAsync(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string workspaceRoot,
        WorkspaceScopeDescriptor workspaceScope,
        CancellationToken cancellationToken)
    {
        if (candidate.ExpectedArtifacts.Count == 0)
        {
            return;
        }

        var projections = ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson);
        if (projections.Count == 0)
        {
            return;
        }

        var projectedExpectationIds = new HashSet<Guid>();
        foreach (var projection in projections)
        {
            var matchedExpectations = candidate.ExpectedArtifacts
                .Where(item => item.IsRequired && !projectedExpectationIds.Contains(item.Id))
                .Where(item => ProcessMockArtifactMatchesExpectation(item, projection))
                .ToList();
            if (matchedExpectations.Count == 0)
            {
                continue;
            }

            if (matchedExpectations.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Process mock artifact '{projection.RelativePath}' for role '{projection.RoleKey}' matched multiple required artifact expectations for step '{candidate.StepRun.Title}': {string.Join(", ", matchedExpectations.Select(item => item.Title))}.");
            }

            var expectedArtifact = matchedExpectations[0];
            var externalReferenceKey = BuildProcessMockArtifactExternalReferenceKey(
                candidate.StepRun.Id,
                expectedArtifact.Id,
                projection.RelativePath);
            if (candidate.ExternalReferenceKeys.Contains(externalReferenceKey))
            {
                projectedExpectationIds.Add(expectedArtifact.Id);
                continue;
            }

            var scopedRelativePath = ResolveScopedManagedRelativePath(workspaceScope, projection.RelativePath);
            if (!TryResolveArtifactFullPath(workspaceRoot, scopedRelativePath, out var fullPath, out var pathResolutionFailure) ||
                !File.Exists(fullPath))
            {
                throw new InvalidOperationException(
                    $"Process mock artifact '{projection.RelativePath}' for expected artifact '{expectedArtifact.Title}' was declared by execution run {detail.Run.Id:D}, but scoped path '{scopedRelativePath}' could not be found. {pathResolutionFailure}".Trim());
            }

            byte[] content;
            try
            {
                content = await File.ReadAllBytesAsync(fullPath, cancellationToken);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(
                    $"Process mock artifact '{projection.RelativePath}' for expected artifact '{expectedArtifact.Title}' at scoped path '{scopedRelativePath}' could not be read: {exception.Message}",
                    exception);
            }

            var contentType = GuessContentTypeFromPath(fullPath);
            var placement = await storagePlacementService.PlaceAsync(
                new StoragePlacementRequest(
                    Path.GetFileName(fullPath),
                    contentType,
                    content,
                    StorageUsagePurpose.Evidence,
                    ResolveStorageContentKind(contentType, fullPath),
                    ProjectId: candidate.Run.ProjectId,
                    RelativePathHint: scopedRelativePath),
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
                    ProvenanceSummary = $"Projected from deterministic process mock artifact '{projection.RelativePath}' at scoped workspace path '{scopedRelativePath}' for AgentFramework execution run {detail.Run.Id:D}.",
                    AllowedFutureUsageSummary = string.IsNullOrWhiteSpace(expectedArtifact.AllowedFutureUsageSummary)
                        ? "Process mock evidence and regression audit review."
                        : expectedArtifact.AllowedFutureUsageSummary,
                    ReviewSummary = $"Process mock role '{projection.RoleKey}' produced '{Path.GetFileName(projection.RelativePath)}'.",
                    ManagedStoragePath = placement.RelativePath,
                    ExternalReferenceKey = externalReferenceKey
                },
                cancellationToken);
            if (recordResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Process mock artifact projection failed for expected artifact '{expectedArtifact.Title}': {string.Join(" | ", recordResult.Errors.Select(error => error.Message))}");
            }

            candidate.ExternalReferenceKeys.Add(externalReferenceKey);
            projectedExpectationIds.Add(expectedArtifact.Id);
        }
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
            if (!IsUsableProjectedResponseArtifactContent(expectedArtifact, normalizedResponseText))
            {
                logger.LogInformation(
                    "Skipping response-text artifact projection for run {RunId}, step {StepRunId}, expected artifact {ArtifactTitle} because the assistant response is not usable artifact content.",
                    candidate.Run.Id,
                    candidate.StepRun.Id,
                    expectedArtifact.Title);
                continue;
            }

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
        var implementationMentionsTests = RequiresConcreteImplementationProof(candidate) &&
                                          (
                                              candidate.StepRun.Title.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                                              (workBrief?.WorkBriefText?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                              (workBrief?.ExpectedOutcome?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false) ||
                                              (workBrief?.EvidenceExpectationSummary?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false));
        ProcessProjectStructureContextFormatter.TryParse(candidate.Run.TriggerReason, out var projectStructureContext);
        var hasGroundedExternalTarget = TryResolveExternalTargetHintFromProjectStructureGrounding(
            projectStructureGroundingSummary,
            out var groundedExternalAbsolutePath,
            out var groundedExternalMappedAlias);
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
                : $"- The dispatcher already fetched a live project-structure snapshot for this selected branch and included it below. Treat that grounding as a starting point, not a substitute for tool execution. You must still call `project_structure_read` early in this step for project `{projectStructureContext.ProjectId:D}` before you conclude.");
            builder.AppendLine("- Do not assume the selected task node contains every requirement. Carry forward concrete stack choices, output directories, examples, UI expectations, and acceptance notes that appear on related root or sibling project-structure nodes.");
            builder.AppendLine("- If the project structure names a concrete output directory outside the managed workspace, do not silently relocate the deliverable. Use a controlled local execution path when necessary, and record the exact external target in the artifacts you write.");
            builder.AppendLine("- Workspace file and dotnet tools cannot use a raw absolute external path like `C:\\target\\app` directly. Convert it to the mapped alias `external-target/C/target/app` when you call `workspace_create_directory`, `workspace_write_file`, `workspace_read_file`, `workspace_stat_path`, `workspace_dotnet_new`, or `workspace_dotnet_build`.");
            builder.AppendLine("- `workspace_pwsh_run_script` executes a script file from the managed workspace. If that script invokes native tools against an external target, convert `external-target/<drive>/...` back to a native path such as `C:\\target\\app` inside the script before passing it to `dotnet`, `Start-Process`, `Test-Path`, or `Resolve-Path`.");
            builder.AppendLine("- The mapped `external-target/<drive>/...` alias resolves to the real external target. Do not create a shadow copy in a different workspace folder.");
            builder.AppendLine("- Treat missing project-structure inspection as incomplete work for this step.");
            builder.AppendLine("- If project_structure_read reveals an exact external output directory for the selected work node, scaffold and implement in that exact location during this step instead of returning a note that the code does not exist yet.");
            if (hasGroundedExternalTarget)
            {
                builder.AppendLine($"- The grounded project structure already identifies the external output root `{groundedExternalAbsolutePath}` mapped to `{groundedExternalMappedAlias}`. Treat that mapped alias as the product root for this run, not as an optional example.");
            }
            builder.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(projectStructureGroundingSummary))
        {
            builder.AppendLine("Live project structure grounding:");
            builder.AppendLine(projectStructureGroundingSummary.Trim());
            builder.AppendLine();
        }

        if (ShouldIncludeBlazorWebAppHostingContract(
                candidate,
                projectStructureGroundingSummary,
                artifactInspectionGroundingSummary))
        {
            builder.AppendLine("Blazor Web App hosting contract:");
            AppendBlazorWebAppHostingContract(builder);
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
        AppendRequiredArtifactResponseContract(builder, candidate.ExpectedArtifacts);
        builder.AppendLine("Upstream artifacts:");
        builder.AppendLine(BuildArtifactInputSummary(candidate.ArtifactInputs));
        builder.AppendLine();
        var missingUpstreamArtifactInputSummary = ResolveMissingUpstreamArtifactInputSummary(candidate);
        if (!string.IsNullOrWhiteSpace(missingUpstreamArtifactInputSummary))
        {
            builder.AppendLine("Upstream artifact gate:");
            builder.AppendLine(missingUpstreamArtifactInputSummary);
            builder.AppendLine("- Do not fabricate an upstream artifact in this step and do not spend validation/build attempts trying to compensate for it. Return `Blocked` and name the upstream step and artifact that must be rerun or supplied.");
            builder.AppendLine();
        }

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
        builder.AppendLine("- After a failed validation tool call, the next tool call must inspect the failing diagnostics or mutate files that directly address the failure. Repeating the same failed build/test/run command without an intervening cause-directed change is no-progress behavior.");
        builder.AppendLine("- Do not stop after inspection, reconnaissance, bootstrap confirmation, or a next-steps summary if required tools, concrete deliverables, or required artifacts are still missing.");
        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("- Because this is an implementation step, create the real scaffold or code now. A markdown change set alone is not a completed implementation.");
            builder.AppendLine("- Follow this implementation critical path: scaffold or inspect the runnable host, identify the generated project shape, create the real domain/application logic, wire the UI to that logic, create sibling automated tests with a ProjectReference to the host, replace stale template content, then build the host and run the tests.");
            builder.AppendLine("- Use `workspace_dotnet_new` only for the first bootstrap when the target project is missing. If a runnable host or test project already exists, inspect and repair it in place; do not rerun `workspace_dotnet_new --force` because it can overwrite the implemented route back to starter template content.");
            builder.AppendLine("- If a previous attempt already created a stock scaffold, treat that scaffold as the host to repair. Do not delete it or scaffold again; edit concrete source/project files such as `Components/Pages/Home.razor`, `Domain/CalculatorEngine.cs`, the sibling test `.csproj`, and test source.");
            builder.AppendLine("- When the project structure gives an exact product output root, treat that directory as the outer container. If the host project name is `Calculator`, scaffold with parentDirectory set to the exact output root and name `Calculator`, producing `<output-root>/Calculator/Calculator.csproj`; do not scaffold into the output root itself.");
            builder.AppendLine("- After `workspace_dotnet_new -n <Name>` under an output root, the canonical host is usually `<output-root>/<Name>/<Name>.csproj`. Do not create `<output-root>/<Name>.csproj`, `<output-root>/Program.cs`, or root `Pages/*.razor` files beside that nested host; target the actual scaffolded project path.");
            builder.AppendLine("- Do not use `workspace_delete_path` recursively on a directory that contains a `.csproj`, `.fsproj`, `.vbproj`, `.sln`, or `.slnx` just to make `workspace_dotnet_new` succeed. Repair the project in place.");
            builder.AppendLine("- Do not delete scaffold core files such as `.csproj`, `Program.cs`, `Components/App.razor`, `Components/Routes.razor`, `_Imports.razor`, `Components/Pages/Home.razor`, layout files, `appsettings*.json`, or `wwwroot/app.css`. Edit or overwrite those files instead.");
            builder.AppendLine("- After `workspace_dotnet_new`, do not guess framework-era file locations. Inspect the scaffolded `.csproj`, `Program.cs`, and routed page paths before writing UI or tests.");
            builder.AppendLine("- Do not write implementation change-set or rollout artifacts until after concrete source/project mutations and successful build/test validation in the same attempt.");
            builder.AppendLine("- For a calculator-like Blazor app, the minimum concrete implementation is a public domain/application type such as `CalculatorEngine`, a non-placeholder routed UI that calls it, and tests that instantiate that concrete type through a sibling test project.");
            builder.AppendLine("- For a calculator-like app, write and then read `Calculator/Domain/CalculatorEngine.cs`, include concrete Add/Subtract/Multiply/Divide operations there, wire the routed page to that engine, and test that engine directly.");
            builder.AppendLine("- Do not leave the generated empty `UnitTest1.cs` as the test evidence. Replace it or add meaningful test source that asserts CalculatorEngine addition, subtraction, multiplication, division, and division-by-zero behavior.");
            builder.AppendLine("- If tests fail with `CS0118` or text like `'Calculator' is a namespace but is used like a type`, do not rerun the same tests. Create or read the concrete engine type, add the test ProjectReference, update tests to `new CalculatorEngine()`, and rerun the host build plus test project.");
            builder.AppendLine("- Concrete feature and constraint nodes from the live project structure are required scope for this implementation step. Treat them as mandatory deliverables now, not as later backlog or rollout notes.");
            builder.AppendLine("- Do not defer grounded features, UI behavior, acceptance notes, or output constraints into `future steps`, follow-up work, or QA-only cleanup while still returning `Completed`.");
            builder.AppendLine("- Before you conclude this implementation step, use `workspace_stat_path` on the concrete solution, project, and source paths you created or changed, and use `workspace_read_file` on at least one concrete project or source file from that implementation.");
            builder.AppendLine("- Run `workspace_dotnet_build` against the implemented solution or project before you claim the scaffold or code is build-ready.");
            builder.AppendLine("- Build/read proof must happen after the last scaffold or source mutation in the same attempt. Previous attempt receipts do not prove the current mutated output.");
            builder.AppendLine("- Keep automated test projects as siblings of the runnable web host, not inside the host project directory. If a `*.Tests` folder or test source file is nested under the Blazor host, use `workspace_delete_path` with `recursive: true` on that stale nested test folder before building the host.");
            builder.AppendLine("- Never create or write test project files under the runnable web host. For a host at `external-target/.../Calculator/Calculator.csproj`, the sibling test project path is `external-target/.../Calculator.Tests/...`; `external-target/.../Calculator/Calculator.Tests/...` is invalid and must be deleted before the host build.");
            builder.AppendLine("- If `workspace_dotnet_test` is denied or fails because the sibling test project path does not exist, create or repair that sibling test project first, add a ProjectReference to the host project, and only then rerun `workspace_dotnet_test`. Repeating the same missing-path test command is not recovery.");
            builder.AppendLine("- `workspace_dotnet_test` targetPath must be a solution or test project file such as `Calculator.Tests/Calculator.Tests.csproj`. Never pass a `.cs` source file or a plain test directory as the target.");
            builder.AppendLine("- When repairing a scaffolded test project, clean stale template and duplicate test files before rerunning tests. Delete or replace files such as `UnitTest1.cs`, `<Project>.Tests.cs`, old `.bak` sources that are still compiled, or duplicate `CalculatorTests` classes instead of repeatedly rewriting only one new test file.");
            builder.AppendLine("- After creating, moving, or repairing tests, rerun `workspace_dotnet_build` against the runnable web host and `workspace_dotnet_test` against the test project. A successful test run does not recover an earlier failed host build unless the same host build is rerun successfully.");
            builder.AppendLine("- If `workspace_dotnet_build` reports missing xUnit, MSTest, or test attribute namespaces from the web project, inspect for misplaced test files under the host and remove or move them; do not fix that by adding test packages to the production web project.");
            builder.AppendLine("- Put business logic that needs automated coverage in a public domain or application class and test that class through a sibling test project with a ProjectReference to the host. For calculator-like tasks, use a concrete type such as `CalculatorEngine` under `<RootNamespace>.Domain`; do not instantiate the project namespace, root namespace, or a Razor component as if it were the calculator engine.");
            builder.AppendLine("- When tests use host-domain types, edit the sibling test `.csproj` to include a real `<ProjectReference Include=\"..\\<HostProject>\\<HostProject>.csproj\" />` before running tests; package references alone do not make the host code visible.");
            builder.AppendLine("- Avoid C# types whose simple name equals the Blazor project or root namespace, such as a `Calculator` class inside namespace `Calculator`. Use a name like `CalculatorEngine` under a non-conflicting namespace such as `<RootNamespace>.Domain`, and import that concrete namespace in `_Imports.razor`.");
            builder.AppendLine("- A `.razor` component file also generates a C# type. In a project/root namespace named `Calculator`, do not create `Components/Calculator.razor`; put the route in `Components/Pages/Home.razor` or name the component `CalculatorPage.razor` so `_Imports.razor` can still import namespaces.");
            builder.AppendLine("- For Blazor Web App scaffolds from `dotnet new blazor`, routed pages live under `Components/Pages`. If `Components/Pages/Home.razor` exists, it is the effective primary route. Put the primary `/` calculator surface there or another `Components/Pages/*.razor` route; do not create legacy root `Pages/*.razor` routes such as `Pages/Home.razor` or `Pages/Index.razor`.");
            builder.AppendLine("- Do not add `@page` directives to `Components/Routes.razor`. That file must stay the generated Router host; route directives belong in `Components/Pages/*.razor`.");
            builder.AppendLine("- If you find both `Components/Pages/Home.razor` and a legacy root `Pages/*.razor` file declaring `@page \"/\"`, delete or move the legacy root route with `workspace_delete_path` before build or runtime validation. Duplicate routes can build successfully but fail at app startup/browser proof.");
            builder.AppendLine("- Do not convert a `dotnet new blazor` Blazor Web App into older Blazor Server/Razor Pages hosting. Do not add `Pages/_Host.cshtml`, `Startup.cs`, `UseStartup<Startup>()`, `blazor.server.js`, or ASP.NET Core 7.x component package references to a net10 Blazor Web App scaffold.");
            builder.AppendLine("- If a repair attempt already added older Blazor Server hosting files or package references, delete `Pages/_Host.cshtml` and other legacy root `Pages/*.cshtml` files, remove obsolete `Microsoft.AspNetCore.Components*` package references, restore the generated minimal `Program.cs`/`Components/App.razor`/`Components/Routes.razor` shape, then rebuild.");
            builder.AppendLine("- Keep the generated `MainLayout` type/file unless you update every `@layout MainLayout`, `DefaultLayout=\"typeof(MainLayout)\"`, and `NotFound.razor` reference in the same change. For recovery, prefer editing `MainLayout` content/styles instead of renaming it.");
            builder.AppendLine("- Do not substitute repeated `workspace_stat_path` calls or checks for `bin/Debug/...` outputs for `workspace_dotnet_build`. The build tool creates and validates those outputs; stat polling does not.");
            builder.AppendLine("- If you scaffold from a starter template, replace placeholder output with the requested product surface before you conclude. Default starter content such as `Hello, world!`, untouched sample routes, or stock template pages is not a completed implementation.");
            builder.AppendLine("- Do not write implementation artifacts that say the requested UI, logic, tests, or rollout preparation will happen in a later step while this implementation step still returns `Completed`.");
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
            builder.AppendLine("- If `workspace_dotnet_new` reports overwrite conflicts or exits with code 73, immediately inspect the target directory before you declare a blocker. When a runnable scaffold already exists at the required path, repair and continue in place instead of retrying the scaffold into a deeper nested folder.");
            builder.AppendLine("- If you must write a new `.csproj` manually, choose a target framework supported by this workspace and repo baseline. For this repository, prefer `net10.0` unless the project structure or existing solution explicitly requires another target.");
            builder.AppendLine("- If you create browser-facing UI files such as `.razor`, `.cshtml`, or `wwwroot` assets, scaffold a runnable web host with the required startup entrypoint. Do not leave browser UI inside a plain class library or non-host project.");
            builder.AppendLine("- If the inherited requirements or project structure describe a browser-validated Blazor or web app, leave a runnable browser surface for downstream QA instead of concluding with service-only or library-only output.");
            builder.AppendLine("- If project-structure scope names Blazor SSR, do not replace it with MVC, Razor Pages, or controller/view placeholder scaffolding unless the project structure explicitly changed that architecture.");
            builder.AppendLine("- If no concrete solution, project, or source files exist yet, do not return Completed.");
            if (ContainsCalculatorContext(candidate))
            {
                AppendCalculatorImplementationContract(builder);
            }

            if (projectStructureContext is not null)
            {
                builder.AppendLine("- If the project structure sends you to an external target directory, map that directory to `external-target/<drive>/...`, scaffold the real solution there, inspect those mapped paths, and run `workspace_dotnet_build` against that mapped solution or project.");
                builder.AppendLine("- Use `workspace_pwsh_run_script` only when you need a controlled helper command to bootstrap or verify the exact external target; otherwise stay on the mapped `external-target/...` path with the workspace tools.");
            }

            if (hasGroundedExternalTarget)
            {
                builder.AppendLine($"- For this implementation, bootstrap and edit the runnable app under `{groundedExternalMappedAlias}`. Do not scaffold or repair the product in `artifacts/`, `output/`, `data/`, or other managed evidence folders when the grounded output root is external.");
                builder.AppendLine($"- If you use `workspace_dotnet_new` for this implementation, pass `{groundedExternalMappedAlias}` as the parent directory root instead of an `artifacts/...` evidence directory.");
            }

            if (implementationMentionsTests)
            {
                builder.AppendLine("- This implementation step explicitly includes tests. Add or update the relevant automated tests now and rerun the required validation before you conclude.");
                builder.AppendLine("- Do not defer implementation-owned tests to a later QA-only step when this step title, work brief, or expected outcome already says tests are part of the work.");
            }
        }

        if (RequiresConcreteImplementationReview(candidate))
        {
            builder.AppendLine("- Because this review step depends on real implementation, inspect actual solution, project, or source files in addition to managed artifacts before you conclude.");
            builder.AppendLine("- If the implementation artifacts describe concrete solution, project, source, or required durable evidence paths that the workspace does not contain, return Blocked with the missing concrete paths instead of approving integration readiness.");
            builder.AppendLine("- Successful upstream `workspace_dotnet_build` or `workspace_dotnet_test` receipts for the concrete implementation paths count as validation evidence for this review step. Do not require fresh `bin/`, `obj/`, or other transient build output folders unless the current step contract explicitly requires a rerun or those exact files.");
            builder.AppendLine("- Do not assume a `.sln`, `.slnx`, or specific `bin/Debug/<tfm>` folder must exist unless the work brief, expected outcome, or reviewed artifacts explicitly require that exact path.");
            builder.AppendLine("- If you inspect compiled output locations, derive them from the actual reviewed project files instead of assuming a target framework such as `net8.0`.");
            builder.AppendLine("- When the implementation lives under a grounded external target, review the concrete project and source files in that target instead of blocking only because managed artifact folders do not contain product binaries.");
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            builder.AppendLine("- This step requires runnable browser proof or screenshots, not build-only or file-only evidence.");
            builder.AppendLine("- Before browser proof, inspect the concrete host project, launch settings, or prior successful build/test receipts so you derive the actual launch target and reachable URL from the reviewed implementation.");
            builder.AppendLine("- If no reviewed app is already running, start the concrete host yourself before you open the browser. If `workspace_dotnet_run` is not available in your tool list, create or repair a short PowerShell helper with `workspace_write_file` and run it with `workspace_pwsh_run_script`; the helper should launch `dotnet run --no-build --project <reviewed .csproj> --urls http://127.0.0.1:<free-port>` in the background, wait until the URL returns a successful HTTP status, write a small JSON receipt containing `appProcessId`, URL, stdout log path, and stderr log path, then exit nonzero on 4xx/5xx or early process exit.");
            builder.AppendLine("- Use `external-target/<drive>/...` with workspace file and dotnet tools, but convert that alias to the native Windows path inside PowerShell helper content before passing it to `dotnet`, `Start-Process`, `Test-Path`, or `Resolve-Path`. For example, `external-target/C/programovani/app/App.csproj` must become `C:\\programovani\\app\\App.csproj` inside the helper. Do not pass a relative `external-target/...` string to `dotnet run` from a helper script.");
            builder.AppendLine("- In PowerShell helpers, never assign to `$PID`; it is a built-in read-only variable. Use names such as `$appProcess` and `$appProcessId`, and capture stdout/stderr so a runtime 500 includes actionable logs.");
            builder.AppendLine("- After a successful build/test receipt for the same unchanged project, do not repeat `workspace_dotnet_build` or `workspace_dotnet_test` just because browser proof is still missing. The next required action is app launch plus Playwright browser tools; repeated build/test receipts are not progress.");
            builder.AppendLine("- Do not assume the app must be reachable at `http://localhost:5000/`. Use the actual URL reported by the launch command, host logs, or `launchSettings.json`.");
            builder.AppendLine("- If a Blazor Web App returns HTTP 500 on the primary route after a successful build, inspect the app logs and route files before concluding. A common cause is duplicate `@page \"/\"` routes, especially legacy root `Pages/Index.razor` plus `Components/Pages/Home.razor`.");
            builder.AppendLine("- Do not treat an unstarted app, a missing published deployment, or an empty `bin/Debug/<tfm>` folder as an acceptable blocker when this QA step can launch the reviewed host itself. Launch it, confirm the reachable URL, and only return `Blocked` if launch or browser interaction still fails after you inspect the real diagnostics.");
            builder.AppendLine("- When the implementation lives under a grounded external target, run and inspect the reviewed host project from that target instead of expecting a separate published deployment.");
            builder.AppendLine("- Use the attached Playwright MCP tools after launch: `browser_navigate` to the launched URL, `browser_snapshot` for accessibility proof, `browser_take_screenshot` for visual proof, and `browser_console_messages` for console diagnostics.");
            builder.AppendLine("- After `browser_snapshot`, inspect the saved snapshot content. If it shows starter template text such as `Hello, world!` or `Welcome to your new app.`, return `Blocked` and repair the implementation instead of claiming proof.");
            builder.AppendLine("- For button-driven Blazor apps such as calculators, click a representative sequence and assert that the visible display or history changes to the expected result. If `@onclick` buttons do not mutate state in the browser, report a Blazor render-mode or static-SSR implementation defect instead of treating route reachability as proof.");
            builder.AppendLine("- If the app cannot be launched, the browser cannot be reached, screenshots cannot be captured, or the required UI flow is still missing, return `Blocked` instead of `Completed`.");
            builder.AppendLine("- Do not reframe missing browser proof as a residual risk, deferred next step, or artifact-only note while still marking the step complete.");
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

    private static bool ContainsCalculatorContext(DispatchCandidate candidate)
    {
        var contextText = string.Join(
            Environment.NewLine,
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.Name,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary);

        return contextText.Contains("Calculator", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendCalculatorImplementationContract(StringBuilder builder)
    {
        builder.AppendLine("- Calculator implementation contract: the exact product root is the outer directory `external-target/C/programovani/csharp/calculator`. Bootstrap with `workspace_create_directory` for that exact root, then `workspace_dotnet_new` with parentDirectory `external-target/C/programovani/csharp/calculator` and name `Calculator` so the host is `external-target/C/programovani/csharp/calculator/Calculator/Calculator.csproj`.");
        builder.AppendLine("- Calculator implementation contract: never call `workspace_dotnet_new` with parentDirectory `external-target/C/programovani/csharp` and name `Calculator` for this task. On Windows that targets the same lowercase output root by casing and creates the wrong top-level host shape.");
        builder.AppendLine("- Calculator implementation contract: after `workspace_dotnet_new` creates `external-target/.../calculator/Calculator/Calculator.csproj`, that nested host is the canonical app. Do not hand-write or repair `external-target/.../calculator/Calculator.csproj` or `external-target/.../calculator/Program.cs` at the output root.");
        builder.AppendLine("- Calculator implementation contract: preserve the generated Blazor Web App hosting shape. Do not replace `Calculator/Program.cs` with `WebAssemblyHostBuilder`, do not add `Microsoft.AspNetCore.Components.WebAssembly`, and do not add ASP.NET Core 7 component package references to a net10 host.");
        builder.AppendLine("- Calculator implementation contract: complete the concrete source sequence before any artifact writing or final answer: `Calculator/Domain/CalculatorEngine.cs`, `Calculator/Program.cs`, `Calculator/Components/Pages/Home.razor`, `Calculator.Tests/Calculator.Tests.csproj`, and one meaningful sibling test source.");
        builder.AppendLine("- Calculator implementation contract: `Calculator/Program.cs` must register `CalculatorEngine` in DI before `builder.Build()` if `Home.razor` injects it.");
        builder.AppendLine("- Calculator implementation contract: `Calculator/Components/Pages/Home.razor` must be the primary `/` route, call `CalculatorEngine`, and expose add, subtract, multiply, divide, equals/evaluate, numeric keypad, current display/result, divide-by-zero feedback, and calculation history behavior.");
        builder.AppendLine("- Calculator implementation contract: `Calculator/Components/Pages/Home.razor` must start with a valid route such as `@page \"/\"`; `@page \"\"` and `RZ9988` route-template failures mean the app is not buildable.");
        builder.AppendLine("- Calculator implementation contract: Razor keypad callbacks in `Home.razor` must be syntax-safe and type-consistent. Prefer char handlers such as `AppendDigit(char digit)` and `ChooseOperator(char op)` with callbacks like `@onclick=\"() => AppendDigit('1')\"` and `@onclick=\"() => ChooseOperator('+')\"`. If a handler accepts `string`, wrap the whole Razor attribute in single quotes, for example `@onclick='() => AppendDigit(\"1\")'`. Never pass char literals to string handlers, for example do not write `AppendToResult('1')` when the method is `AppendToResult(string value)`, and never write `@onclick=\"() => AppendDigit(\"1\")\"`.");
        builder.AppendLine("- Calculator implementation contract: create the sibling test project with `workspace_dotnet_new` using parentDirectory `external-target/C/programovani/csharp/calculator` and name `Calculator.Tests`, producing `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj`. Never set parentDirectory to a path already ending in `Calculator.Tests`, and never move `Calculator.Tests/Calculator.Tests` to `Calculator.Tests/Calculator.Tests.csproj`.");
        builder.AppendLine("- Calculator implementation contract: `Calculator.Tests/Calculator.Tests.csproj` must contain `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />`; package references alone do not make `Calculator.Domain.CalculatorEngine` visible to tests.");
        builder.AppendLine("- Calculator implementation contract: test source must use the host domain type, for example `using Calculator.Domain;` and `new CalculatorEngine()`, and must assert addition, subtraction, multiplication, division, and divide-by-zero behavior.");
        builder.AppendLine("- Calculator implementation contract: the template `Calculator.Tests/UnitTest1.cs` must not remain as an empty placeholder test; replace it with meaningful tests or delete it if another meaningful test source exists.");
        builder.AppendLine("- Calculator implementation contract: do not use a free-form text box with placeholder parsing or a `Calculate` handler that assigns a fixed result. The UI must invoke the concrete engine operations and update visible state from user-entered keypad/operator interactions.");
        builder.AppendLine("- Calculator implementation contract: repair product behavior before test-project polish. If `Home.razor` is still placeholder/free-form UI, the next concrete mutation must be `Calculator/Components/Pages/Home.razor`; repeatedly rewriting `Calculator.Tests/Calculator.Tests.csproj` is no-progress behavior.");
        builder.AppendLine("- Calculator implementation contract: a valid minimal recovery overwrites or repairs `Calculator/Program.cs`, `Calculator/Components/Pages/Home.razor`, `Calculator/Domain/CalculatorEngine.cs`, `Calculator.Tests/Calculator.Tests.csproj` when its ProjectReference is missing, and a meaningful sibling test source before build/test validation.");
    }

    private static void AppendCalculatorRecoveryChecklist(StringBuilder builder, string missingConcreteImplementationProofSummary)
    {
        builder.AppendLine("Calculator recovery checklist for this retry:");
        if (!string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
        {
            builder.AppendLine($"- Last concrete proof failure: {missingConcreteImplementationProofSummary}.");
        }

        builder.AppendLine("- Do not call `workspace_dotnet_new` again if either `external-target/C/programovani/csharp/calculator/Calculator/Calculator.csproj` or `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj` exists.");
        builder.AppendLine("- If `external-target/C/programovani/csharp/calculator/Calculator.csproj`, `external-target/C/programovani/csharp/calculator/Program.cs`, or `external-target/C/programovani/csharp/calculator/Components` exists at the output-root level, the host was scaffolded in the wrong place. Do not build that root host or create a second project under it in the same attempt; return Blocked/Failed so the next clean run can start from the correct outer-root shape.");
        builder.AppendLine("- If `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj` is a directory, do not write or delete it repeatedly. That path shape is corrupt; stop targeting it, report the path-shape failure, and continue only from a clean sibling test project path on a clean retry.");
        builder.AppendLine("- First read these exact files when present: `external-target/C/programovani/csharp/calculator/Calculator/Calculator.csproj`, `external-target/C/programovani/csharp/calculator/Calculator/Program.cs`, `external-target/C/programovani/csharp/calculator/Calculator/CalculatorEngine.cs`, `external-target/C/programovani/csharp/calculator/Calculator/Components/Routes.razor`, `external-target/C/programovani/csharp/calculator/Calculator/Components/Pages/Home.razor`, `external-target/C/programovani/csharp/calculator/Calculator/Domain/CalculatorEngine.cs`, `external-target/C/programovani/csharp/calculator/Calculator.Tests/Calculator.Tests.csproj`, `external-target/C/programovani/csharp/calculator/Calculator.Tests/UnitTest1.cs`, `external-target/C/programovani/csharp/calculator/Calculator.Tests/CalculatorTests.cs`, and `external-target/C/programovani/csharp/calculator/Calculator.Tests/CalculatorEngineTests.cs`.");
        builder.AppendLine("- Repair, in place, with `workspace_write_file`: keep `Calculator/Calculator.csproj` as a net10 Blazor Web App project without ASP.NET Core 7 component package references; keep `Calculator/Program.cs` on the generated `WebApplication`/`AddRazorComponents`/`MapRazorComponents<App>()` hosting path; add `using Calculator.Domain;` and `builder.Services.AddScoped<CalculatorEngine>();` before `builder.Build()` when the page injects the engine.");
        builder.AppendLine("- Repair `Calculator/Components/Pages/Home.razor` as the `/` route instead of editing `Components/Routes.razor`; `Routes.razor` must remain the Router host without `@page`.");
        builder.AppendLine("- If the host build reports `RZ9988`, `@page directive must specify a route template`, or `@page \"\"` in `Home.razor`, the next mutation must set `Home.razor` to `@page \"/\"` before any test-project repair or test rerun.");
        builder.AppendLine("- Replace placeholder UI in `Home.razor`; a free-form expression text box, TODO/parser comment, or `Calculate` method that sets a fixed/default result is not implementation. The route needs numeric keypad buttons, `+`, `-`, `*`, `/`, `=`, display/result state, divide-by-zero feedback, history, and calls to `CalculatorEngine` operations.");
        builder.AppendLine("- When writing `Home.razor` keypad buttons, use syntax-safe callbacks. Preferred pattern: handlers accept `char` and buttons use `@onclick=\"() => AppendDigit('1')\"` and `@onclick=\"() => ChooseOperator('+')\"`. Alternative pattern: handlers accept `string` and buttons use single-quoted Razor attributes such as `@onclick='() => AppendDigit(\"1\")'`. Do not write `@onclick=\"() => AppendDigit(\"1\")\"`, `@onclick=\"() => SetOperation(\"+\")\"`, `AppendToResult('1')` with a string parameter, or `SetOperation('+')` with a string parameter; these caused prior Razor/CS1503 failures.");
        builder.AppendLine("- If `Calculator.Tests/Calculator.Tests.csproj` already contains `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />`, do not rewrite that project file again until after the routed UI proof passes. The blocker is the effective UI, not the test project file.");
        builder.AppendLine("- If tests fail with `CS0234`, `CS0246`, `Calculator.Domain` missing, or `CalculatorEngine` missing from the sibling test project, the next mutation must repair `Calculator.Tests/Calculator.Tests.csproj` to include `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />` and confirm `Calculator/Domain/CalculatorEngine.cs` exists in namespace `Calculator.Domain`.");
        builder.AppendLine("- If the host build fails with `CS0101` or `CS0111` for `Calculator.Domain.CalculatorEngine`, inspect both `Calculator/CalculatorEngine.cs` and `Calculator/Domain/CalculatorEngine.cs`. Delete stale `Calculator/CalculatorEngine.cs` if both define `CalculatorEngine`; deleting and rewriting only `Domain/CalculatorEngine.cs` does not remove the duplicate type.");
        builder.AppendLine("- Repair `Calculator.Tests/Calculator.Tests.csproj` only when the ProjectReference or test packages are missing; replace or delete the generated empty `UnitTest1.cs`; keep concrete arithmetic tests in the sibling test project.");
        builder.AppendLine("- Replace duplicate add/divide-only tests with one meaningful test source that covers Add, Subtract, Multiply, Divide, and divide-by-zero behavior against `CalculatorEngine`.");
        builder.AppendLine("- After the last source or project-file mutation, read back at least `Calculator/Program.cs`, `Calculator/Components/Pages/Home.razor`, `Calculator/Domain/CalculatorEngine.cs`, and `Calculator.Tests/Calculator.Tests.csproj`, then run `workspace_dotnet_build` on `Calculator/Calculator.csproj` and `workspace_dotnet_test` on `Calculator.Tests/Calculator.Tests.csproj`.");
        builder.AppendLine("- Write required markdown artifacts only after those build and test commands succeed in this same retry.");
    }

    private static void AppendRequiredArtifactResponseContract(
        StringBuilder builder,
        IReadOnlyList<DispatchArtifactExpectation> expectedArtifacts)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(expectedArtifacts);

        var requiredArtifacts = expectedArtifacts
            .Where(item => item.IsRequired && !string.IsNullOrWhiteSpace(item.Title))
            .ToList();
        if (requiredArtifacts.Count == 0)
        {
            return;
        }

        builder.AppendLine("Required response structure:");
        builder.AppendLine("- Keep the response artifact-first. Use a dedicated markdown heading with the exact artifact title for every required output artifact.");
        builder.AppendLine("- Fill each required section with concrete content that satisfies its validation expectation. Do not leave headings empty, and do not replace the sections with a generic status summary.");

        foreach (var expectedArtifact in requiredArtifacts)
        {
            builder.Append("- `## ");
            builder.Append(expectedArtifact.Title.Trim());
            builder.Append('`');
            if (!string.IsNullOrWhiteSpace(expectedArtifact.ValidationRequirementSummary))
            {
                builder.Append(": ");
                builder.AppendLine(expectedArtifact.ValidationRequirementSummary.Trim());
            }
            else
            {
                builder.AppendLine();
            }
        }

        if (requiredArtifacts.Any(IsMigrationRolloutPreparationArtifact))
        {
            builder.AppendLine("- The migration/rollout checklist is required even when the implemented app has no database or persistent data. If no data migration is needed, say `No data migration required` and still name data changes, operational preconditions, validation evidence, and rollback steps.");
            builder.AppendLine("- A DB-free checklist is valid only when it explicitly says no schema migration, seed update, backfill, or data rollback is required, then lists rollout preconditions and code rollback steps.");
        }

        builder.AppendLine("- If you finish the step successfully, keep those exact section titles in the final response before the PROCESS_STEP_OUTCOME comment.");
        builder.AppendLine();
    }

    private static bool IsMigrationRolloutPreparationArtifact(DispatchArtifactExpectation expectedArtifact)
    {
        var text = string.Join(
            ' ',
            expectedArtifact.Title,
            expectedArtifact.ValidationRequirementSummary);
        return text.Contains("migration", StringComparison.OrdinalIgnoreCase) &&
               (text.Contains("rollout", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("rollback", StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildCorrelationId(Guid stepRunId)
    {
        return $"process-step:{stepRunId:D}";
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
                executionRun.ChatSessionId.HasValue &&
                executionRun.State is ExecutionState.Completed or ExecutionState.Failed)
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

    private async Task<ExecutionRunRecord?> ResolveCompetingActiveAutomationExecutionAsync(
        DispatchCandidate candidate,
        DispatchExecutionOutcome executionOutcome,
        CancellationToken cancellationToken)
    {
        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                ProcessRunId: candidate.Run.Id.ToString("D"),
                ProcessStepId: candidate.StepRun.Id.ToString("D"),
                Take: 20),
            cancellationToken);
        var now = clock.GetUtcNow();
        return executionRuns
            .Where(executionRun => executionRun.Id != executionOutcome.Detail.Run.Id)
            .Where(executionRun => IsBlockingAutomationExecutionRun(executionRun, now))
            .OrderByDescending(executionRun => executionRun.UpdatedAtUtc == default
                ? executionRun.CreatedAtUtc
                : executionRun.UpdatedAtUtc)
            .ThenByDescending(executionRun => executionRun.CreatedAtUtc)
            .FirstOrDefault();
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

    internal static bool IsConcurrentAutomationSessionBusyException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is InvalidOperationException &&
               ConcurrentAutomationSessionBusyMessages.Contains(exception.Message.Trim());
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

        if (!IsRecoveryTrigger(trigger))
        {
            return false;
        }

        if (recoverableExecutionRunId.HasValue)
        {
            return false;
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
        return now - lastProgressAtUtc >= StaleAutomationExecutionRunTimeout;
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
        int attemptNumber,
        int maxExecutionAttempts)
    {
        var run = detail.Run;
        var unresolvedCriticalToolFailures = ResolveUnresolvedCriticalToolFailures(detail);
        var recoverableImplementationPunt = IsRecoverableImplementationPunt(candidate, responseText);
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, responseText);
        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, responseText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);
        var missingRequiredArtifactSummary = ResolveMissingRequiredArtifactSummary(candidate, detail, responseText);
        var recoverableGovernedOutcomeGap = IsRecoverableGovernedOutcomeGap(candidate, responseText) &&
            !CanImplicitlyCompleteGovernedStep(candidate, detail, missingRequiredTools, responseText);
        var recoverableProviderFailure = TryResolveRecoverableProviderFailure(detail, responseText, out _);
        if (!string.IsNullOrWhiteSpace(ResolveMissingUpstreamArtifactInputSummary(candidate)) &&
            TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome) &&
            declaredOutcome.Status == ProcessStepRunStatus.Blocked)
        {
            return false;
        }

        return attemptNumber < maxExecutionAttempts
               && run.State == ExecutionState.Completed
               && run.PendingApprovals.Count == 0
               && run.Outcome == RunOutcome.Succeeded
                && (missingRequiredTools.Count > 0 ||
                    unresolvedCriticalToolFailures.Count > 0 ||
                    recoverableImplementationPunt ||
                    !string.IsNullOrWhiteSpace(incompleteImplementationSummary) ||
                    !string.IsNullOrWhiteSpace(missingConcreteProofSummary) ||
                    !string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary) ||
                    !string.IsNullOrWhiteSpace(invalidBrowserProofSummary) ||
                    !string.IsNullOrWhiteSpace(missingRequiredArtifactSummary) ||
                    recoverableGovernedOutcomeGap ||
                    recoverableProviderFailure);
    }

    private static bool ShouldRetryRecoverableFailedRun(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        int attemptNumber,
        int maxExecutionAttempts)
    {
        var run = detail.Run;
        if (attemptNumber >= maxExecutionAttempts ||
            run.State != ExecutionState.Failed ||
            run.PendingApprovals.Count > 0)
        {
            return false;
        }

        if (!RequiresConcreteImplementationProof(candidate) &&
            !RequiresConcreteBrowserProof(candidate))
        {
            return false;
        }

        return missingRequiredTools.Count > 0 ||
               unresolvedCriticalToolFailures.Any(IsFrameworkRecoverableDotnetToolFailure) ||
               TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
               MentionsRepeatedToolInvocation(responseText) ||
               MentionsRepeatedToolInvocation(run.ResultSummary);
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
            var missingImplementationProofForRequiredTools = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
            if (!string.IsNullOrWhiteSpace(missingImplementationProofForRequiredTools))
            {
                return $"AgentFramework run '{run.Title}' did not execute the required step tools successfully: {string.Join(", ", missingRequiredTools)}. Current-attempt implementation proof is also invalid: {missingImplementationProofForRequiredTools}";
            }

            return $"AgentFramework run '{run.Title}' did not execute the required step tools successfully: {string.Join(", ", missingRequiredTools)}";
        }

        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, responseText);
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, responseText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);
        var missingRequiredArtifactSummary = ResolveMissingRequiredArtifactSummary(candidate, detail, responseText);
        if (TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome))
        {
            var branchOutcomeSelectionFailure = ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome);
            if (!string.IsNullOrWhiteSpace(branchOutcomeSelectionFailure))
            {
                return branchOutcomeSelectionFailure;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingConcreteProofSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but the response still reported missing required browser proof: {missingConcreteProofSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(incompleteImplementationSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but the response still deferred required implementation work: {incompleteImplementationSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but current-attempt implementation proof is invalid: {missingConcreteImplementationProofSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(invalidBrowserProofSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but browser proof is invalid: {invalidBrowserProofSummary}";
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
            {
                return $"AgentFramework run '{run.Title}' claimed '{stepTitle}' completed, but required artifacts still could not be recorded automatically: {missingRequiredArtifactSummary}";
            }

            return BuildDeclaredStepOutcomeReason(run.Title, stepTitle, declaredOutcome);
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteProofSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because required browser proof is still missing: {missingConcreteProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(incompleteImplementationSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because the response still deferred required implementation work: {incompleteImplementationSummary}";
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because current-attempt implementation proof is invalid: {missingConcreteImplementationProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(invalidBrowserProofSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because browser proof is invalid: {invalidBrowserProofSummary}";
        }

        if (!string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
        {
            return $"AgentFramework run '{run.Title}' could not complete '{stepTitle}' because required artifacts still could not be recorded automatically: {missingRequiredArtifactSummary}";
        }

        if (CanImplicitlyCompleteGovernedStep(candidate, detail, missingRequiredTools, responseText))
        {
            return $"AgentFramework run '{run.Title}' completed step '{stepTitle}' from successful governed evidence, and the dispatcher inferred the governed completed outcome because PROCESS_STEP_OUTCOME was omitted.";
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
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, responseText);
        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, responseText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);

        if (missingRequiredTools.Count > 0)
        {
            builder.AppendLine($"Missing required step tools: {string.Join(", ", missingRequiredTools)}.");
        }

        if (missingRequiredTools.Contains("workspace_dotnet_build", StringComparer.Ordinal))
        {
            builder.AppendLine("The previous attempt failed because it never invoked `workspace_dotnet_build`.");
            builder.AppendLine("On this retry, after you know the concrete solution or project path, call `workspace_dotnet_build` directly against that path before any final answer.");
            builder.AppendLine("Do not poll `bin/`, `obj/`, DLL, PDB, or test-output paths as a replacement for `workspace_dotnet_build`; repeated successful stat results are not validation progress.");
        }

        if (unresolvedCriticalToolFailures.Count > 0)
        {
            builder.AppendLine(
                $"Unresolved critical tool failures: {string.Join("; ", unresolvedCriticalToolFailures.Take(2).Select(item => $"{item.ToolName}: {item.ExitSummary}"))}.");
        }

        if (!string.IsNullOrWhiteSpace(incompleteImplementationSummary))
        {
            builder.AppendLine($"Implementation remains incomplete: {incompleteImplementationSummary}.");
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteProofSummary))
        {
            builder.AppendLine($"Browser proof remains incomplete: {missingConcreteProofSummary}.");
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
        {
            builder.AppendLine($"Current-attempt implementation proof is invalid: {missingConcreteImplementationProofSummary}.");
        }

        if (!string.IsNullOrWhiteSpace(invalidBrowserProofSummary))
        {
            builder.AppendLine($"Browser proof is invalid: {invalidBrowserProofSummary}.");
        }

        var calculatorRecoveryFocusGuidance = BuildCalculatorRecoveryFocusGuidance(
            candidate,
            responseText,
            missingConcreteImplementationProofSummary,
            missingRequiredTools,
            unresolvedCriticalToolFailures);
        if (!string.IsNullOrWhiteSpace(calculatorRecoveryFocusGuidance))
        {
            builder.AppendLine(calculatorRecoveryFocusGuidance);
        }

        builder.AppendLine("Do not stop after inspection, planning, bootstrap confirmation, or a next-steps summary on this retry.");
        builder.AppendLine("Finish the concrete work, rerun every failed or missing required validation successfully, and then write every required durable artifact.");
        builder.AppendLine("Do not repeat the same failed validation command or rewrite the same file with the same content in a loop. Before rerunning validation, inspect the diagnostic source or change/delete files that directly address that diagnostic.");

        var governedInspectionPaths = ResolveGovernedInspectionPaths(candidate.ExpectedArtifacts);
        var artifactInputInspectionPaths = ResolveArtifactInputInspectionPaths(candidate.ArtifactInputs);

        if (RequiresConcreteImplementationProof(candidate))
        {
            builder.AppendLine("This retry is still the implementation step. Do not report that implementation or code artifacts are missing before you attempt the bootstrap or scaffold yourself.");
            builder.AppendLine("Bootstrap the runnable solution or project now, then validate the concrete files you created with workspace_stat_path, workspace_read_file, and workspace_dotnet_build before you conclude.");
            builder.AppendLine("If the scaffold is greenfield, create the actual solution and project files now with workspace_dotnet_new or a controlled helper path instead of writing only a source file set.");
            builder.AppendLine("If the host or sibling test project already exists from an earlier attempt, do not call workspace_dotnet_new again with --force. Inspect and repair the existing scaffold in place; a forced re-scaffold can erase the implemented Components/Pages route and reset the app to Hello, world.");
            builder.AppendLine("Do not recover by deleting scaffold core files one by one. Preserve and edit `.csproj`, `Program.cs`, `Components/App.razor`, `Components/Routes.razor`, `_Imports.razor`, `Components/Pages/Home.razor`, layout files, `appsettings*.json`, and `wwwroot/app.css`.");
            builder.AppendLine("If you retry a greenfield .NET bootstrap with workspace_dotnet_new, explicitly request a supported target framework such as `net10.0` instead of accepting an older template default.");
            builder.AppendLine("If a prior workspace_dotnet_new attempt failed because files already existed or the template wanted to overwrite content, inspect the target directory immediately. When the scaffold is already present at the required path, continue by repairing, reading, and building that existing project in place instead of declaring the retry blocked.");
            builder.AppendLine("If this implementation produces browser-facing UI files such as `.razor`, `.cshtml`, or `wwwroot` assets, leave a runnable web host and startup entrypoint in place for downstream QA. Do not stop at a plain class library.");
            builder.AppendLine("If the project structure names Blazor SSR, repair toward a runnable Blazor SSR app instead of MVC, Razor Pages, or controller/view placeholders.");
            builder.AppendLine("Keep test projects outside the Blazor host folder. If a previous attempt left `*.Tests` folders or test files nested under the host project, use `workspace_delete_path` with `recursive: true` on that stale nested test folder before rerunning the host build.");
            builder.AppendLine("Do not recreate nested test files under the host after deleting them. For a host at `external-target/.../Calculator/Calculator.csproj`, test files belong in the sibling `external-target/.../Calculator.Tests/...` project, not in `external-target/.../Calculator/Calculator.Tests/...`.");
            builder.AppendLine("If `workspace_dotnet_test` was denied because the sibling test project is missing, create or repair the sibling test project and ProjectReference before rerunning the identical test command.");
            builder.AppendLine("If the failed validation was `workspace_dotnet_build`, rerun `workspace_dotnet_build` against the exact failed host project after every repair. A later `workspace_dotnet_test` success does not recover that failed build by itself.");
            builder.AppendLine("Call `workspace_dotnet_test` only against a test `.csproj`, `.sln`, or `.slnx`. A `.cs` test source file or plain test directory is an invalid target; repair or create the sibling test project and use its `.csproj` path.");
            builder.AppendLine("If the build error mentions missing xUnit, MSTest, `Fact`, or test attribute namespaces in the host project, treat that as misplaced test code under the host and fix the file layout, not the production host dependencies.");
            builder.AppendLine("If tests fail with `CS0118` or because a project/root namespace is being used like a type, create or inspect the concrete domain/application type first, such as `<RootNamespace>.Domain.CalculatorEngine`, add the test ProjectReference, update the tests to target that type, and then rerun workspace_dotnet_build and workspace_dotnet_test.");
            builder.AppendLine("If tests compile against a host-domain type but cannot resolve it, edit the sibling test project file to add `<ProjectReference Include=\"..\\<HostProject>\\<HostProject>.csproj\" />`; do not try to solve that by adding packages or rewriting only the test source.");
            builder.AppendLine("For calculator-like apps, write and read `Calculator/Domain/CalculatorEngine.cs`, add a sibling test project ProjectReference to the host, and replace empty template tests with assertions against `CalculatorEngine` operations before rerunning validation.");
            builder.AppendLine("If the build error mentions `_Imports.razor` and `CS0138` because the root name is a type instead of a namespace, rename the conflicting domain type to a non-root name such as `CalculatorEngine` under a concrete namespace such as `<RootNamespace>.Domain`, then update `_Imports.razor` to import that namespace or remove the bad root import.");
            builder.AppendLine("If the conflicting type comes from a Razor component file such as `Components/Calculator.razor` in a project/root namespace named `Calculator`, rename that component to `CalculatorPage.razor` or move its routed content into `Components/Pages/Home.razor`; a `.razor` file name is also a generated type name.");
            builder.AppendLine("For Blazor Web App scaffolds, do not create legacy root `Pages/*.razor` routes. Use `Components/Pages/Home.razor` for `/`, and delete any stale root `Pages/Home.razor`, `Pages/Index.razor`, or other root `Pages/*.razor` route that duplicates or replaces `Components/Pages/Home.razor` before rerunning build or launch validation.");
            builder.AppendLine("Never put `@page` in `Components/Routes.razor`; if it is present there, remove it and keep `Routes.razor` as the Router-only host before rerunning build or tests.");
            builder.AppendLine("Do not repair `_Imports.razor` by repeatedly rebuilding. Change the conflicting file/type first, then rerun the exact failed host build.");
            builder.AppendLine("If you renamed or deleted `MainLayout`, either restore `MainLayout.razor` or update every `MainLayout` reference before building, including `Routes.razor`, `NotFound.razor`, and any `_Imports.razor` layout namespace.");
            builder.AppendLine("Do not stop at a starter template or say the app is merely ready for later feature implementation. Replace default template output with the requested product behavior before you conclude.");
            builder.AppendLine("On this retry, repair placeholder or incomplete product files before validating. A validation-only retry is acceptable only when read-back proves the current concrete source already satisfies the full implementation contract, then build and tests pass without any later mutation.");
            if (RequiresCalculatorLikeImplementationProof(candidate, detail))
            {
                AppendCalculatorRecoveryChecklist(builder, missingConcreteImplementationProofSummary);
            }

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

        var misplacedTestProjectRecoveryGuidance = BuildMisplacedTestProjectRecoveryGuidance(unresolvedCriticalToolFailures);
        if (!string.IsNullOrWhiteSpace(misplacedTestProjectRecoveryGuidance))
        {
            builder.AppendLine(misplacedTestProjectRecoveryGuidance);
        }

        var blazorBuildRecoveryGuidance = BuildBlazorBuildRecoveryGuidance(
            candidate,
            unresolvedCriticalToolFailures,
            responseText);
        if (!string.IsNullOrWhiteSpace(blazorBuildRecoveryGuidance))
        {
            builder.AppendLine(blazorBuildRecoveryGuidance);
        }

        var frameworkRecoveryGuidance = BuildDotnetFrameworkRecoveryGuidance(
            candidate,
            unresolvedCriticalToolFailures,
            responseText);
        if (!string.IsNullOrWhiteSpace(frameworkRecoveryGuidance))
        {
            builder.AppendLine(frameworkRecoveryGuidance);
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            builder.AppendLine("This retry is still the QA/browser-proof step. Inspect the reviewed host project, launch settings, and grounded implementation artifacts before you conclude.");
            if (HasProjectStructureContext(candidate))
            {
                builder.AppendLine("Call project_structure_read now, resolve the exact reviewed host under the grounded external-target path, and use that concrete app instead of assuming a separate published deployment.");
            }

            builder.AppendLine("Do not assume the app must be reachable at `http://localhost:5000/`. Derive the real launch URL from the reviewed host project, `launchSettings.json`, prior run diagnostics, or the URL reported by the launch command.");
            builder.AppendLine("If the app is not already running, start the reviewed host yourself before opening the browser. If `workspace_dotnet_run` is not available, write or repair a short PowerShell helper that starts `dotnet run --no-build --project <reviewed .csproj> --urls http://127.0.0.1:<free-port>` in the background, waits for a successful HTTP response, writes appProcessId/URL/stdout/stderr log-path evidence, and exits nonzero on 4xx/5xx or early process exit.");
            builder.AppendLine("When repairing a launch helper for an external target, keep `external-target/<drive>/...` for workspace tools, but convert it to the native Windows path inside the helper before invoking `dotnet`, `Start-Process`, `Test-Path`, or `Resolve-Path`. For example, `external-target/C/programovani/app/App.csproj` must become `C:\\programovani\\app\\App.csproj`; a relative `external-target/...` string can resolve under the managed workspace path alias and fail even after a successful build.");
            builder.AppendLine("Do not assign to `$PID` in the PowerShell helper; use `$appProcess` and `$appProcessId`. If a helper already exists, inspect and repair it instead of rewriting the same broken content.");
            builder.AppendLine("If the launched Blazor app returns HTTP 500, inspect the captured logs and route files. For Blazor Web App scaffolds, remove duplicate primary routes such as legacy root `Pages/Home.razor` or `Pages/Index.razor` when `Components/Pages/Home.razor` already declares `@page \"/\"`.");
            builder.AppendLine("Do not repeat a successful `workspace_dotnet_build` or `workspace_dotnet_test` receipt for the same unchanged project while browser proof is missing. Repeating build/test is not recovery; app launch plus Playwright evidence is the recovery path.");
            builder.AppendLine("Capture fresh browser evidence with `browser_take_screenshot`, `browser_snapshot`, and `browser_console_messages` before you conclude this retry.");
            builder.AppendLine("Inspect the saved `browser_snapshot` output before concluding. If it still contains starter template text such as `Hello, world!` or `Welcome to your new app.`, repair or block instead of returning Completed.");
            builder.AppendLine("For button-driven Blazor apps such as calculators, click a representative sequence and assert that the visible display or history changes to the expected result. If `@onclick` buttons do not mutate state in the browser, block with a Blazor render-mode or static-SSR implementation defect.");
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

        string? projectName = null;
        IReadOnlyList<ProjectStructureGroundingNodeData> surfaceNodes = [];
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var projectWorkbenchServiceType = Type.GetType("CanDoItAll.Modules.Workbench.ProjectWorkbenchService, CanDoItAll.Modules.Workbench");
            if (projectWorkbenchServiceType is null)
            {
                logger.LogDebug(
                    "Project workbench service type was unavailable while building project structure grounding for process run {RunId}, step {StepRunId}. Falling back to canonical workbench nodes only.",
                    candidate.Run.Id,
                    candidate.StepRun.Id);
            }
            else
            {
                var projectWorkbenchService = scope.ServiceProvider.GetService(projectWorkbenchServiceType);
                if (projectWorkbenchService is null)
                {
                    logger.LogDebug(
                        "Project workbench service was unavailable while building project structure grounding for process run {RunId}, step {StepRunId}. Falling back to canonical workbench nodes only.",
                        candidate.Run.Id,
                        candidate.StepRun.Id);
                }
                else
                {
                    var getStructureAsync = projectWorkbenchServiceType.GetMethod(
                        "GetStructureAsync",
                        [typeof(Guid), typeof(CancellationToken)]);
                    if (getStructureAsync is null)
                    {
                        logger.LogDebug(
                            "Project workbench service did not expose GetStructureAsync(Guid, CancellationToken) while building project structure grounding for process run {RunId}, step {StepRunId}. Falling back to canonical workbench nodes only.",
                            candidate.Run.Id,
                            candidate.StepRun.Id);
                    }
                    else
                    {
                        var surfaceTask = getStructureAsync.Invoke(projectWorkbenchService, [projectStructureContext.ProjectId, cancellationToken]) as Task;
                        if (surfaceTask is not null)
                        {
                            await surfaceTask;
                            var surface = surfaceTask.GetType().GetProperty("Result")?.GetValue(surfaceTask);
                            if (surface is not null)
                            {
                                projectName = GetProjectStructureGroundingString(surface, "ProjectName");
                                surfaceNodes = ExtractProjectStructureGroundingNodes(surface);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not prefetch projected project structure grounding for process run {RunId}, step {StepRunId}, project {ProjectId}. Falling back to canonical workbench nodes only.",
                candidate.Run.Id,
                candidate.StepRun.Id,
                projectStructureContext.ProjectId);
        }

        var canonicalNodes = await TryLoadCanonicalProjectStructureGroundingNodesAsync(projectStructureContext.ProjectId, cancellationToken);
        if (surfaceNodes.Count == 0 && canonicalNodes.Count == 0)
        {
            return PrefetchedProjectStructureGrounding.Empty;
        }

        if (string.IsNullOrWhiteSpace(projectName))
        {
            projectName = await TryResolveProjectStructureProjectNameAsync(projectStructureContext.ProjectId, cancellationToken);
        }

        var promptSummary = BuildProjectStructureGroundingSummary(
            string.IsNullOrWhiteSpace(projectName)
                ? projectStructureContext.ProjectId.ToString("D")
                : projectName,
            surfaceNodes,
            canonicalNodes,
            projectStructureContext);
        return string.IsNullOrWhiteSpace(promptSummary)
            ? PrefetchedProjectStructureGrounding.Empty
            : new PrefetchedProjectStructureGrounding(
                promptSummary,
                ["project_structure_read"]);
    }

    private async Task<PrefetchedArtifactInspectionGrounding> TryBuildArtifactInspectionGroundingAsync(
        DispatchCandidate candidate,
        CancellationToken cancellationToken)
    {
        var requiresUpstreamValidationReceiptGrounding = RequiresConcreteImplementationReview(candidate) ||
                                                        RequiresConcreteBrowserProof(candidate);
        if (candidate.ArtifactInputs.Count == 0 && !requiresUpstreamValidationReceiptGrounding)
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
        if (artifactEntries.Count == 0 && !requiresUpstreamValidationReceiptGrounding)
        {
            return PrefetchedArtifactInspectionGrounding.Empty;
        }

        try
        {
            var workspaceRoot = Path.GetFullPath(workspacePathResolver.ResolveWorkspaceRoot());
            var builder = new StringBuilder();
            var satisfiedToolNames = new HashSet<string>(StringComparer.Ordinal);
            var appendedArtifactCount = 0;

            if (artifactEntries.Count > 0)
            {
                builder.AppendLine("Dispatcher pre-inspected recorded upstream durable artifacts before this step started:");
                foreach (var artifactEntry in artifactEntries)
                {
                    var normalizedPath = WorkspaceScopeDescriptor.NormalizeRelativePath(artifactEntry.Artifact.ManagedStoragePath);
                    if (string.IsNullOrWhiteSpace(normalizedPath))
                    {
                        continue;
                    }

                    if (!TryResolveArtifactFullPath(workspaceRoot, normalizedPath, out var fullPath, out _) ||
                        !File.Exists(fullPath))
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
            }

            var appendedValidationReceiptCount = await AppendUpstreamValidationReceiptGroundingAsync(
                candidate,
                builder,
                satisfiedToolNames,
                cancellationToken);
            if (appendedArtifactCount == 0 && appendedValidationReceiptCount == 0)
            {
                return PrefetchedArtifactInspectionGrounding.Empty;
            }

            if (satisfiedToolNames.Count == 0)
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

    private async Task<int> AppendUpstreamValidationReceiptGroundingAsync(
        DispatchCandidate candidate,
        StringBuilder builder,
        ISet<string> satisfiedToolNames,
        CancellationToken cancellationToken)
    {
        if (!RequiresConcreteImplementationReview(candidate) &&
            !RequiresConcreteBrowserProof(candidate))
        {
            return 0;
        }

        var executionRuns = await workspaceService.ListExecutionRunsAsync(
            new ExecutionRunQuery(
                SourceKind: "process-step",
                ProcessRunId: candidate.Run.Id.ToString("D"),
                State: ExecutionState.Completed,
                Outcome: RunOutcome.Succeeded,
                Take: 24),
            cancellationToken);
        if (executionRuns.Count == 0)
        {
            return 0;
        }

        var appendedCount = 0;
        var wroteHeader = false;
        var seenReceiptKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var run in executionRuns
                     .Where(item => !string.Equals(item.ProcessStepId, candidate.StepRun.Id.ToString("D"), StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(item => item.CompletedAtUtc ?? item.UpdatedAtUtc))
        {
            var detail = await workspaceService.GetExecutionRunDetailAsync(run.Id, cancellationToken);
            foreach (var receipt in detail.ToolReceipts
                         .Where(IsSuccessfulUpstreamValidationReceipt)
                         .OrderByDescending(item => item.CompletedAtUtc)
                         .ThenByDescending(item => item.StartedAtUtc))
            {
                var receiptKey = string.Join(
                    "|",
                    NormalizeToolToken(receipt.ToolName),
                    receipt.RequestSummary.Trim(),
                    receipt.WorkingDirectory.Trim());
                if (!seenReceiptKeys.Add(receiptKey))
                {
                    continue;
                }

                if (!wroteHeader)
                {
                    if (builder.Length > 0)
                    {
                        builder.AppendLine();
                    }

                    builder.AppendLine("Dispatcher pre-inspected successful upstream build/test receipts before this step started:");
                    wroteHeader = true;
                }

                var normalizedToolName = NormalizeToolToken(receipt.ToolName);
                builder.Append("- `");
                builder.Append(normalizedToolName);
                builder.Append("` succeeded");

                if (!string.IsNullOrWhiteSpace(receipt.RequestSummary))
                {
                    builder.Append(" for `");
                    builder.Append(TrimForPrompt(receipt.RequestSummary.Trim(), 180));
                    builder.Append('`');
                }

                if (!string.IsNullOrWhiteSpace(receipt.WorkingDirectory))
                {
                    builder.Append(" in `");
                    builder.Append(TrimForPrompt(receipt.WorkingDirectory.Trim(), 180));
                    builder.Append('`');
                }

                builder.Append(" during upstream execution run `");
                builder.Append(run.Id.ToString("D"));
                builder.Append('`');

                var completedAtUtc = receipt.CompletedAtUtc == default
                    ? run.CompletedAtUtc ?? run.UpdatedAtUtc
                    : receipt.CompletedAtUtc;
                if (completedAtUtc != default)
                {
                    builder.Append(" at ");
                    builder.Append(completedAtUtc.ToString("yyyy-MM-dd HH:mm:ss 'UTC'"));
                }

                if (!string.IsNullOrWhiteSpace(receipt.ExitSummary))
                {
                    builder.Append(" (");
                    builder.Append(TrimForPrompt(receipt.ExitSummary.Trim(), 120));
                    builder.Append(')');
                }

                builder.AppendLine(".");

                satisfiedToolNames.Add(normalizedToolName);
                appendedCount++;
                if (appendedCount >= 4)
                {
                    return appendedCount;
                }
            }
        }

        return appendedCount;
    }

    private async Task<IReadOnlyList<ProjectStructureGroundingNodeData>> TryLoadCanonicalProjectStructureGroundingNodesAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var connection = dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = """
SELECT
    NodeKey,
    COALESCE(ParentNodeKey, ''),
    ObjectType,
    COALESCE(ObjectSubtype, ''),
    COALESCE(Title, ''),
    COALESCE(Subtitle, ''),
    COALESCE(Status, ''),
    COALESCE(Notes, ''),
    COALESCE(MetadataJson, '{}')
FROM Workbench_ProjectObjects
WHERE lower(ProjectId) = lower($projectId)
  AND IsSystemManaged = 0
ORDER BY CreatedAtUtc, Title;
""";

                var projectIdParameter = command.CreateParameter();
                projectIdParameter.ParameterName = "$projectId";
                projectIdParameter.Value = projectId.ToString("D");
                command.Parameters.Add(projectIdParameter);

                var nodes = new List<ProjectStructureGroundingNodeData>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var nodeId = ReadProjectStructureGroundingColumn(reader, 0);
                    if (string.IsNullOrWhiteSpace(nodeId))
                    {
                        continue;
                    }

                    nodes.Add(new ProjectStructureGroundingNodeData(
                        nodeId,
                        ReadProjectStructureGroundingColumn(reader, 1),
                        ResolveProjectStructureObjectTypeLabel(reader.GetValue(2)),
                        ReadProjectStructureGroundingColumn(reader, 3),
                        ReadProjectStructureGroundingColumn(reader, 4),
                        ReadProjectStructureGroundingColumn(reader, 5),
                        ReadProjectStructureGroundingColumn(reader, 6),
                        ReadProjectStructureGroundingColumn(reader, 7),
                        ReadProjectStructureGroundingColumn(reader, 8)));
                }

                return nodes;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not load canonical workbench nodes for project structure grounding on project {ProjectId}.",
                projectId);
            return [];
        }
    }

    private async Task<string> TryResolveProjectStructureProjectNameAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            return await dbContext.Set<Project>()
                .Where(item => item.Id == projectId)
                .Select(item => item.Name)
                .SingleOrDefaultAsync(cancellationToken)
                ?? string.Empty;
        }
        catch (Exception exception)
        {
            logger.LogDebug(
                exception,
                "Could not resolve project name while building project structure grounding for project {ProjectId}.",
                projectId);
            return string.Empty;
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
        return BuildProjectStructureGroundingSummary(projectName, nodes, [], context);
    }

    private static string BuildProjectStructureGroundingSummary(
        string projectName,
        IReadOnlyList<ProjectStructureGroundingNodeData> surfaceNodes,
        IReadOnlyList<ProjectStructureGroundingNodeData> supplementalNodes,
        ProcessProjectStructureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var nodes = MergeProjectStructureGroundingNodes(surfaceNodes, supplementalNodes);
        return BuildProjectStructureGroundingSummary(projectName, nodes, context);
    }

    private static string BuildProjectStructureGroundingSummary(
        string projectName,
        IReadOnlyList<ProjectStructureGroundingNodeData> nodes,
        ProcessProjectStructureContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (nodes.Count == 0)
        {
            return string.Empty;
        }

        var nodesById = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var nodesByParentId = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.ParentId))
            .GroupBy(node => NormalizeProjectStructureNodeId(node.ParentId), StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProjectStructureGroundingNodeData>)group.ToList(),
                StringComparer.Ordinal);
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
                .Select(node => new
                {
                    Node = node,
                    SignalScore = GetProjectStructureGroundingSignalScore(node)
                })
                .Where(item => item.SignalScore > 0 || !string.IsNullOrWhiteSpace(item.Node.Title))
                .OrderByDescending(item => item.SignalScore)
                .ThenBy(item => item.Node.Title, StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(item => item.Node)
                .ToList();

            if (siblingNodes.Count > 0)
            {
                builder.AppendLine("Sibling planning context under the same parent:");
                AppendProjectStructureGroundingNodes(builder, siblingNodes);
            }

            var siblingDescendantNodes = siblingNodes
                .SelectMany(node => ResolveProjectStructureDescendants(node.Id, nodesByParentId, maxDepth: 3))
                .Where(node =>
                    !string.Equals(node.Id, targetNode.Id, StringComparison.Ordinal) &&
                    !string.Equals(node.Id, selectedProcessNodeId, StringComparison.Ordinal) &&
                    !IsProjectStructureGroundingNoiseNode(node))
                .Select(node => new
                {
                    Node = node,
                    SignalScore = GetProjectStructureGroundingSignalScore(node)
                })
                .Where(item => item.SignalScore > 0 || !string.IsNullOrWhiteSpace(item.Node.Title))
                .OrderByDescending(item => item.SignalScore)
                .ThenBy(item => item.Node.Title, StringComparer.OrdinalIgnoreCase)
                .Take(12)
                .Select(item => item.Node)
                .ToList();

            if (siblingDescendantNodes.Count > 0)
            {
                builder.AppendLine("Descendant requirement context from sibling planning nodes:");
                AppendProjectStructureGroundingNodes(builder, siblingDescendantNodes);
            }

            var childNodes = nodes
                .Where(node =>
                    string.Equals(node.ParentId, targetNode.Id, StringComparison.Ordinal) &&
                    !string.Equals(node.Id, selectedProcessNodeId, StringComparison.Ordinal) &&
                    !IsProjectStructureGroundingNoiseNode(node))
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

    private static IReadOnlyList<ProjectStructureGroundingNodeData> MergeProjectStructureGroundingNodes(
        IReadOnlyList<ProjectStructureGroundingNodeData> primaryNodes,
        IReadOnlyList<ProjectStructureGroundingNodeData> supplementalNodes)
    {
        if (primaryNodes.Count == 0)
        {
            return supplementalNodes;
        }

        if (supplementalNodes.Count == 0)
        {
            return primaryNodes;
        }

        var merged = new Dictionary<string, ProjectStructureGroundingNodeData>(StringComparer.Ordinal);
        foreach (var node in primaryNodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id))
            {
                continue;
            }

            merged[node.Id] = node;
        }

        foreach (var node in supplementalNodes)
        {
            if (string.IsNullOrWhiteSpace(node.Id) || merged.ContainsKey(node.Id))
            {
                continue;
            }

            merged[node.Id] = node;
        }

        return merged.Values.ToList();
    }

    private static string ReadProjectStructureGroundingColumn(DbDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal)
            ? string.Empty
            : reader.GetValue(ordinal)?.ToString()?.Trim() ?? string.Empty;
    }

    private static string ResolveProjectStructureObjectTypeLabel(object? value)
    {
        if (value is null || value == DBNull.Value)
        {
            return string.Empty;
        }

        if (value is long longValue && Enum.IsDefined(typeof(ProjectObjectType), (int)longValue))
        {
            return ((ProjectObjectType)(int)longValue).ToString();
        }

        if (value is int intValue && Enum.IsDefined(typeof(ProjectObjectType), intValue))
        {
            return ((ProjectObjectType)intValue).ToString();
        }

        var text = value.ToString()?.Trim() ?? string.Empty;
        if (int.TryParse(text, out var parsedIntValue) &&
            Enum.IsDefined(typeof(ProjectObjectType), parsedIntValue))
        {
            return ((ProjectObjectType)parsedIntValue).ToString();
        }

        return text;
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

    private static IReadOnlyList<ProjectStructureGroundingNodeData> ResolveProjectStructureDescendants(
        string? nodeId,
        IReadOnlyDictionary<string, IReadOnlyList<ProjectStructureGroundingNodeData>> nodesByParentId,
        int maxDepth)
    {
        if (string.IsNullOrWhiteSpace(nodeId) || maxDepth <= 0)
        {
            return [];
        }

        var descendants = new List<ProjectStructureGroundingNodeData>();
        var queue = new Queue<(string NodeId, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue((NormalizeProjectStructureNodeId(nodeId), 0));

        while (queue.Count > 0)
        {
            var (currentNodeId, depth) = queue.Dequeue();
            if (depth >= maxDepth ||
                !nodesByParentId.TryGetValue(currentNodeId, out var children))
            {
                continue;
            }

            foreach (var child in children)
            {
                if (!visited.Add(child.Id))
                {
                    continue;
                }

                descendants.Add(child);
                queue.Enqueue((child.Id, depth + 1));
            }
        }

        return descendants;
    }

    private static bool TryResolveExternalTargetHintFromProjectStructureGrounding(
        string? groundingSummary,
        out string absolutePath,
        out string mappedAlias)
    {
        absolutePath = string.Empty;
        mappedAlias = string.Empty;

        if (string.IsNullOrWhiteSpace(groundingSummary))
        {
            return false;
        }

        var match = Regex.Match(
            groundingSummary,
            @"\b(?<path>[A-Za-z]:\\[A-Za-z0-9 _.\-\\]+)",
            RegexOptions.CultureInvariant);
        if (!match.Success)
        {
            return false;
        }

        var candidatePath = match.Groups["path"].Value.Trim().TrimEnd('\\');
        if (candidatePath.Length < 3 || candidatePath[1] != ':' || candidatePath[2] != '\\')
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(candidatePath[0]);
        var remainder = candidatePath.Length == 3
            ? string.Empty
            : candidatePath[3..].Replace('\\', '/');
        absolutePath = candidatePath;
        mappedAlias = string.IsNullOrWhiteSpace(remainder)
            ? $"external-target/{driveLetter}"
            : $"external-target/{driveLetter}/{remainder}";
        return true;
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

        return GetProjectStructureGroundingSignalScore(node) > 0;
    }

    private static int GetProjectStructureGroundingSignalScore(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        var score = 0;
        if (!string.IsNullOrWhiteSpace(node.Notes))
        {
            score += 4;
        }

        if (!string.IsNullOrWhiteSpace(node.Subtitle))
        {
            score += 3;
        }

        if (!string.IsNullOrWhiteSpace(NormalizeProjectStructureMetadataSummary(node.MetadataJson)))
        {
            score += 2;
        }

        if (LooksLikeProjectStructureConstraintTitle(node.Title))
        {
            score += 5;
        }

        if (LooksLikeProjectStructureFeatureTitle(node.Title))
        {
            score += 3;
        }

        return score;
    }

    private static bool LooksLikeProjectStructureConstraintTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return false;
        }

        return normalizedTitle.Contains("output", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("must", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("required", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("directory", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("path", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("place", StringComparison.OrdinalIgnoreCase) ||
               Regex.IsMatch(
                   normalizedTitle,
                   @"\b[a-zA-Z]:\\",
                RegexOptions.CultureInvariant) ||
               normalizedTitle.Contains("external-target/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeProjectStructureFeatureTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(title);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
        {
            return false;
        }

        return normalizedTitle.Contains("blazor", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("calculator", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("button", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("history", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("keypad", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("keyboard", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("screen", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("page", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("form", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("ui", StringComparison.OrdinalIgnoreCase) ||
               normalizedTitle.Contains("route", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsProjectStructureGroundingNoiseNode(ProjectStructureGroundingNodeData node)
    {
        ArgumentNullException.ThrowIfNull(node);

        return string.Equals(node.ObjectType, "ProcessRun", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(node.ObjectType, "File", StringComparison.OrdinalIgnoreCase);
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

    private static string BuildCalculatorRecoveryFocusGuidance(
        DispatchCandidate candidate,
        string? responseText,
        string missingConcreteImplementationProofSummary,
        IReadOnlyList<string> missingRequiredTools,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        if (!ContainsCalculatorContext(candidate))
        {
            return string.Empty;
        }

        var unresolvedFailureText = string.Join(
            Environment.NewLine,
            unresolvedCriticalToolFailures.Select(item => $"{item.ToolName} {item.RequestSummary} {item.ExitSummary}"));
        var recoveryDiagnosticText = string.Join(
            Environment.NewLine,
            responseText,
            missingConcreteImplementationProofSummary,
            unresolvedFailureText);
        var repeatedTestProjectWrite = MentionsRepeatedToolInvocation(responseText) &&
            responseText?.Contains("Calculator.Tests/Calculator.Tests.csproj", StringComparison.OrdinalIgnoreCase) == true;
        var repeatedHomeRazorWrite = MentionsRepeatedToolInvocation(responseText) &&
            responseText?.Contains("Calculator/Components/Pages/Home.razor", StringComparison.OrdinalIgnoreCase) == true;
        var homeRazorCharStringCompilerFailure = MentionsHomeRazorCharStringCompilerFailure(recoveryDiagnosticText);
        var homeRazorRouteTemplateFailure = MentionsHomeRazorRouteTemplateFailure(recoveryDiagnosticText);
        var calculatorEngineDuplicateCompilerFailure = MentionsCalculatorEngineDuplicateCompilerFailure(recoveryDiagnosticText);
        var testProjectReferenceFailure =
            MentionsCalculatorTestProjectReferenceFailure(responseText) ||
            MentionsCalculatorTestProjectReferenceFailure(missingConcreteImplementationProofSummary) ||
            MentionsCalculatorTestProjectReferenceFailure(unresolvedFailureText);
        var missingTestValidation = missingRequiredTools.Contains("workspace_dotnet_test", StringComparer.Ordinal);
        var routedUiProofMissing =
            missingConcreteImplementationProofSummary.Contains("routed UI", StringComparison.OrdinalIgnoreCase) ||
            missingConcreteImplementationProofSummary.Contains("Home.razor", StringComparison.OrdinalIgnoreCase) ||
            missingConcreteImplementationProofSummary.Contains("keypad", StringComparison.OrdinalIgnoreCase) ||
            missingConcreteImplementationProofSummary.Contains("history", StringComparison.OrdinalIgnoreCase);
        if (!repeatedTestProjectWrite &&
            !repeatedHomeRazorWrite &&
            !homeRazorCharStringCompilerFailure &&
            !homeRazorRouteTemplateFailure &&
            !calculatorEngineDuplicateCompilerFailure &&
            !testProjectReferenceFailure &&
            !missingTestValidation &&
            !routedUiProofMissing)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Immediate calculator recovery focus:");
        if (repeatedTestProjectWrite)
        {
            builder.AppendLine("- The previous attempt looped rewriting `Calculator.Tests/Calculator.Tests.csproj`. If that file already has the host ProjectReference, it is not the active blocker. Do not write it again until after the routed UI proof passes.");
        }

        if (routedUiProofMissing)
        {
            builder.AppendLine("- The next concrete mutation must repair `external-target/C/programovani/csharp/calculator/Calculator/Components/Pages/Home.razor`. Read it, then overwrite the placeholder/free-form textbox route with a `CalculatorEngine`-backed keypad/operator/equal/history UI before touching artifacts or rerunning tests.");
        }

        if (repeatedHomeRazorWrite)
        {
            builder.AppendLine("- The previous attempt looped rewriting `Calculator/Components/Pages/Home.razor`. Do not write the same page again unchanged; first inspect the latest build diagnostic and change the event handler signatures, button literal types, or calculation logic that directly addresses it.");
        }

        if (homeRazorCharStringCompilerFailure)
        {
            builder.AppendLine("- The host build is failing in `Calculator/Components/Pages/Home.razor` with `CS1503` char-to-string errors. Use one type-consistent Razor callback pattern: either handlers accept `char` and callbacks use `@onclick=\"() => AppendDigit('1')\"`, or handlers accept `string` and callbacks use single-quoted Razor attributes such as `@onclick='() => AppendDigit(\"1\")'`.");
            builder.AppendLine("- Do not leave `AppendToResult('1')` or `SetOperation('+')` calling methods that still accept `string`; that is the exact prior compiler failure. Also never write malformed double-quoted callbacks such as `@onclick=\"() => AppendDigit(\"1\")\"`.");
            builder.AppendLine("- If `Calculator.Tests/Calculator.Tests.csproj` already has the host ProjectReference, do not rewrite the test project again while the compiler error points at `Calculator/Components/Pages/Home.razor`; repair the routed UI first.");
            builder.AppendLine("- After the `Home.razor` compile fix, remove placeholder `CalculateResult` behavior and connect equals/evaluate, operators, display/result state, divide-by-zero feedback, and history to `CalculatorEngine`; then rerun `workspace_dotnet_build` on `Calculator/Calculator.csproj` before `workspace_dotnet_test`.");
        }

        if (homeRazorRouteTemplateFailure)
        {
            builder.AppendLine("- The host build is failing in `Calculator/Components/Pages/Home.razor` with `RZ9988` because the page route is empty. Change `@page \"\"` to `@page \"/\"` before any test-project repair or test rerun.");
        }

        if (calculatorEngineDuplicateCompilerFailure)
        {
            builder.AppendLine("- The host build is failing with duplicate `CalculatorEngine` definitions (`CS0101`/`CS0111`). Read both `Calculator/CalculatorEngine.cs` and `Calculator/Domain/CalculatorEngine.cs`; delete the stale top-level `Calculator/CalculatorEngine.cs` if both define the engine, then rebuild. Do not delete and recreate only `Domain/CalculatorEngine.cs` because that leaves the duplicate in place.");
        }

        if (testProjectReferenceFailure)
        {
            builder.AppendLine("- The previous test failure was a host visibility failure, not a package or assertion failure. Read `Calculator.Tests/Calculator.Tests.csproj` and `Calculator/Domain/CalculatorEngine.cs`, then repair the test project so it contains `<ProjectReference Include=\"..\\Calculator\\Calculator.csproj\" />` and the engine source is in namespace `Calculator.Domain`.");
        }

        if (missingTestValidation)
        {
            builder.AppendLine("- `workspace_dotnet_test` is still required. Do not rerun it until the host ProjectReference, `CalculatorEngine`, `Program.cs` DI registration, and routed UI have been read back after the latest mutations.");
        }

        builder.AppendLine("- Required repair order: fix `Calculator/Program.cs`, `Calculator/Components/Pages/Home.razor`, `Calculator/Domain/CalculatorEngine.cs`, `Calculator.Tests/Calculator.Tests.csproj`, and meaningful sibling tests; read those files back; then build the host and run the sibling test project.");
        return builder.ToString().Trim();
    }

    private static bool MentionsHomeRazorCharStringCompilerFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Home.razor", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("CS1503", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("char", StringComparison.OrdinalIgnoreCase) &&
               text.Contains("string", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MentionsHomeRazorRouteTemplateFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Home.razor", StringComparison.OrdinalIgnoreCase) &&
               (text.Contains("RZ9988", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("@page directive must specify a route template", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("@page \"\"", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MentionsCalculatorEngineDuplicateCompilerFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("CalculatorEngine", StringComparison.OrdinalIgnoreCase) &&
               (text.Contains("CS0101", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("CS0111", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("already contains a definition", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("already defines a member", StringComparison.OrdinalIgnoreCase));
    }

    private static bool MentionsCalculatorTestProjectReferenceFailure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var mentionsCalculatorTestOrValidation =
            text.Contains("Calculator.Tests", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("workspace_dotnet_test", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("ProjectReference", StringComparison.OrdinalIgnoreCase);
        var mentionsHostTypeVisibility =
            text.Contains("Calculator.Domain", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("CalculatorEngine", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("CS0234", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("CS0246", StringComparison.OrdinalIgnoreCase);

        return mentionsCalculatorTestOrValidation && mentionsHostTypeVisibility;
    }

    private static string BuildMisplacedTestProjectRecoveryGuidance(
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        var cleanupTargets = ResolveMisplacedTestProjectCleanupTargets(unresolvedCriticalToolFailures);
        if (cleanupTargets.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("A previous host build failed while a sibling test project build succeeded or was attempted. Treat stale nested test folders under the host as the first repair target before more scaffolding.");
        foreach (var target in cleanupTargets)
        {
            builder.AppendLine($"For failed host build `{target.HostProjectPath}`, remove the stale nested test directory `{target.NestedTestDirectoryPath}` with `workspace_delete_path` using `recursive: true`, then rerun `workspace_dotnet_build` against `{target.HostProjectPath}`.");
            builder.AppendLine($"Do not recreate test files under `{target.NestedTestDirectoryPath}`. If tests are still required, create or repair a sibling test project outside the host folder.");
        }

        builder.AppendLine("Do not add xUnit, MSTest, or test SDK packages to the production host to satisfy misplaced test files.");
        return builder.ToString().Trim();
    }

    private static string BuildBlazorBuildRecoveryGuidance(
        DispatchCandidate candidate,
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures,
        string responseText)
    {
        if (!RequiresConcreteImplementationProof(candidate) ||
            !unresolvedCriticalToolFailures.Any(IsFrameworkRecoverableDotnetToolFailure) &&
            !MentionsRepeatedToolInvocation(responseText))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Do not rerun the identical `workspace_dotnet_build` request until you have changed or deleted files that directly address the current compiler errors.");
        builder.AppendLine("Do not rerun the identical `workspace_dotnet_test` request after a denied or missing-path result until you have created or repaired the sibling test project and ProjectReference that the command targets.");
        builder.AppendLine("Do not recover from scaffold conflicts by recursively deleting the runnable host, sibling test project, or target root. If a directory contains a .NET project or solution file, repair it in place.");
        builder.AppendLine("Do not delete scaffold core files one by one to make re-scaffolding succeed. Preserve and edit `.csproj`, `Program.cs`, `Components/App.razor`, `Components/Routes.razor`, `_Imports.razor`, `Components/Pages/Home.razor`, layout files, `appsettings*.json`, and `wwwroot/app.css`.");
        builder.AppendLine("If the previous attempt only scaffolded or wrote markdown artifacts, the next recovery attempt must mutate concrete source/project files before writing any artifacts or running validations.");
        builder.AppendLine("If a `.csproj` exists anywhere under the target root, do not call `workspace_dotnet_new` for that same host again. Read the project shape and repair the existing scaffold.");
        builder.AppendLine("For Blazor host builds that mention nested `*.Tests` files, delete the nested host test folder and do not recreate it; use a sibling test project outside the host folder if tests are required.");
        builder.AppendLine("For test-project failures with duplicate test classes or methods (`CS0101`, `CS0111`), inspect the sibling test project files and remove stale template sources such as `UnitTest1.cs`, `<Project>.Tests.cs`, old `.bak` sources that are still compiled, or duplicate `CalculatorTests` files before rerunning `workspace_dotnet_test`.");
        builder.AppendLine("If a test retry keeps failing after rewriting the same test file, stop rewriting that file. Inspect the whole test project shape, add the missing `ProjectReference` or domain class, and remove the conflicting stale source files first.");
        builder.AppendLine("If `Calculator.Tests/Calculator.Tests.csproj` already has a host `ProjectReference` and the compiler error points at `Calculator/Components/Pages/Home.razor`, do not rewrite the test project again. Repair `Home.razor` first, especially `CS1503` char/string callback mismatches.");
        builder.AppendLine("For test failures such as `CS0118` or `'Calculator' is a namespace but is used like a type`, create a distinct concrete domain type such as `<RootNamespace>.Domain.CalculatorEngine`, update the sibling tests to instantiate that type, and add a ProjectReference to the host before rerunning validation.");
        builder.AppendLine("For Blazor Web App scaffolds, the primary route belongs under `Components/Pages`. Move any calculator UI from legacy root `Pages/*.razor` into `Components/Pages/Home.razor` and delete the stale root route before rerunning build/test/launch validation.");
        builder.AppendLine("For `Home.razor` build errors such as `CS1503` converting `char` to `string`, fix the Razor callback argument mismatch before rerunning tests. Either change the handler signatures to `char` (`AppendDigit(char digit)`, `ChooseOperator(char op)`) and keep callbacks such as `@onclick=\"() => AppendDigit('1')\"`, or keep `string` handlers and use single-quoted Razor attributes such as `@onclick='() => AppendDigit(\"1\")'`. Do not leave `AppendToResult('1')` or `SetOperation('+')` calling methods that still accept `string`.");
        builder.AppendLine("For `Home.razor` `RZ9988` or `@page \"\"` build errors, set the route directive to `@page \"/\"` before touching tests; do not rerun `workspace_dotnet_test` while the host build is red.");
        builder.AppendLine("For host build errors `CS0101` or `CS0111` involving `CalculatorEngine`, inspect for duplicate source files such as `Calculator/CalculatorEngine.cs` plus `Calculator/Domain/CalculatorEngine.cs`. Delete the stale top-level engine file and keep one concrete engine under `Calculator/Domain` before rerunning build/test.");
        builder.AppendLine("If the host build mentions `Pages/_Host.cshtml`, `typeof(App)`, `Startup.cs`, `UseStartup<Startup>()`, `blazor.server.js`, or ASP.NET Core 7.x component package warnings, a repair attempt polluted the Blazor Web App with old Blazor Server hosting. Delete `Pages/_Host.cshtml`, `Startup.cs`, legacy root `Pages/*.cshtml`, and stale root `Pages/*.razor` routes, remove obsolete `Microsoft.AspNetCore.Components*` package references, restore the generated minimal `Program.cs`/`Components/App.razor`/`Components/Routes.razor` shape, and put the UI in `Components/Pages/Home.razor` before rebuilding.");
        builder.AppendLine("For Blazor builds that mention `_Imports.razor` with `CS0138` or a type being used as a namespace, remove the bad root namespace import or rename the conflicting domain type to a distinct name such as `CalculatorEngine` under a concrete namespace such as `<RootNamespace>.Domain`.");
        builder.AppendLine("Remember that `Components/Calculator.razor` in a root namespace named `Calculator` generates a `Calculator` type too. Rename that component to `CalculatorPage.razor` or move the route into `Components/Pages/Home.razor` before rebuilding.");
        builder.AppendLine("If `MainLayout` was renamed, restore it or update all `MainLayout` references in the same repair before rerunning the build.");
        return builder.ToString().Trim();
    }

    private static IReadOnlyList<MisplacedTestProjectCleanupTarget> ResolveMisplacedTestProjectCleanupTargets(
        IReadOnlyList<ToolExecutionReceiptRecord> unresolvedCriticalToolFailures)
    {
        var targets = new Dictionary<string, MisplacedTestProjectCleanupTarget>(StringComparer.OrdinalIgnoreCase);
        foreach (var receipt in unresolvedCriticalToolFailures)
        {
            if (!string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_build", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (var projectPath in ResolveProjectPathsFromToolRequest(receipt.RequestSummary))
            {
                var projectName = Path.GetFileNameWithoutExtension(projectPath);
                if (string.IsNullOrWhiteSpace(projectName) || IsTestProjectName(projectName))
                {
                    continue;
                }

                var projectDirectory = ResolvePromptDirectory(projectPath);
                if (string.IsNullOrWhiteSpace(projectDirectory))
                {
                    continue;
                }

                var nestedTestDirectoryPath = $"{projectDirectory}/{projectName}.Tests";
                targets.TryAdd(
                    nestedTestDirectoryPath,
                    new MisplacedTestProjectCleanupTarget(projectPath, nestedTestDirectoryPath));
            }
        }

        return targets.Values.ToList();
    }

    private static IReadOnlyList<string> ResolveProjectPathsFromToolRequest(string requestSummary)
    {
        if (string.IsNullOrWhiteSpace(requestSummary))
        {
            return [];
        }

        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ProjectPathInToolRequestRegex.Matches(requestSummary))
        {
            var candidatePath = match.Groups["path"].Value;
            if (TryMapProjectPathForPrompt(candidatePath, out var promptPath))
            {
                paths.Add(promptPath);
            }
        }

        return paths.ToList();
    }

    private static bool TryMapProjectPathForPrompt(string projectPath, out string promptPath)
    {
        promptPath = string.Empty;
        var normalized = projectPath.Trim().TrimEnd(',', ';', '.', ')', ']').Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith($"{ExternalTargetAliasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            promptPath = normalized;
            return true;
        }

        if (normalized.Length < 3 || !char.IsLetter(normalized[0]) || normalized[1] != ':' || normalized[2] != '/')
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(normalized[0]);
        var remainder = normalized.Length == 3
            ? string.Empty
            : normalized[3..].Trim('/');
        promptPath = string.IsNullOrWhiteSpace(remainder)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{remainder}";
        return true;
    }

    private static string ResolvePromptDirectory(string promptPath)
    {
        var normalized = promptPath.Replace('\\', '/').TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash <= 0
            ? string.Empty
            : normalized[..lastSlash];
    }

    private static bool IsTestProjectName(string projectName)
    {
        return projectName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
               projectName.EndsWith("Tests", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsSuccessfulUpstreamValidationReceipt(ToolExecutionReceiptRecord receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        if (IsFailedToolReceipt(receipt))
        {
            return false;
        }

        return string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_build", StringComparison.Ordinal) ||
               string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_test", StringComparison.Ordinal);
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

    private static bool MentionsRepeatedToolInvocation(string? text)
    {
        return !string.IsNullOrWhiteSpace(text) &&
               text.Contains("repeated identical tool invocation", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<ToolExecutionReceiptRecord> ResolveUnresolvedCriticalToolFailures(ExecutionRunDetail detail)
    {
        var latestCriticalReceipts = detail.ToolReceipts
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
            .ToList();

        return latestCriticalReceipts
            .Where(IsFailedToolReceipt)
            .Where(item => !ShouldIgnoreSupersededCriticalToolFailure(detail, item))
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
            if (!string.IsNullOrWhiteSpace(normalizedToolName) &&
                ShouldCarryForwardSuccessfulToolName(candidate, normalizedToolName))
            {
                successfulToolNames.Add(normalizedToolName);
            }
        }

        foreach (var toolName in ResolveProcessMockSatisfiedToolNames(candidate, detail, requiredToolNames))
        {
            successfulToolNames.Add(toolName);
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

    private static IReadOnlyList<string> ResolveProcessMockSatisfiedToolNames(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyCollection<string> requiredToolNames)
    {
        var projections = ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson);
        if (projections.Count == 0 ||
            !projections.Any(projection => ProcessMockProjectionMatchesRequiredArtifact(candidate, projection)))
        {
            return [];
        }

        var satisfiedToolNames = new List<string>();
        if (RequiresGovernedInspection(candidate.StepRun))
        {
            satisfiedToolNames.AddRange(requiredToolNames
                .Where(toolName => GovernedInspectionToolNames.Contains(toolName, StringComparer.Ordinal)));
        }

        var hasProcessMockImplementationProof = projections.Any(projection =>
            CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection));
        if (hasProcessMockImplementationProof)
        {
            satisfiedToolNames.AddRange(requiredToolNames
                .Where(toolName => ImplementationProofToolNames.Contains(toolName, StringComparer.Ordinal)));
            if (RequiresConcreteTestProof(candidate) &&
                requiredToolNames.Contains("workspace_dotnet_test", StringComparer.Ordinal))
            {
                satisfiedToolNames.Add("workspace_dotnet_test");
            }
        }

        return satisfiedToolNames
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool ShouldCarryForwardSuccessfulToolName(DispatchCandidate candidate, string normalizedToolName)
    {
        if (string.IsNullOrWhiteSpace(normalizedToolName))
        {
            return false;
        }

        if (RequiresConcreteImplementationProof(candidate) &&
            CurrentAttemptOnlyImplementationProofToolNames.Contains(normalizedToolName))
        {
            return false;
        }

        if (RequiresConcreteBrowserProof(candidate) &&
            CurrentAttemptOnlyBrowserProofToolNames.Contains(normalizedToolName))
        {
            return false;
        }

        return true;
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
        var missingRequiredTools = ResolveMissingRequiredToolExecutionsWithCarryForward(
            candidate,
            detail,
            successfulToolNamesFromPriorAttempts);
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

        if (missingRequiredTools.Count > 0)
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

        var missingConcreteProofSummary = ResolveMissingConcreteProofSummary(candidate, responseText);
        var incompleteImplementationSummary = ResolveIncompleteImplementationSummary(candidate, responseText);
        var missingConcreteImplementationProofSummary = ResolveMissingConcreteImplementationProofSummary(candidate, detail);
        var invalidBrowserProofSummary = ResolveInvalidBrowserProofSummary(candidate, detail);
        var missingRequiredArtifactSummary = ResolveMissingRequiredArtifactSummary(candidate, detail, responseText);
        if (TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome))
        {
            if (!string.IsNullOrWhiteSpace(ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome)))
            {
                return ProcessStepRunStatus.Failed;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingConcreteProofSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(incompleteImplementationSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(invalidBrowserProofSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            if (declaredOutcome.Status == ProcessStepRunStatus.Completed &&
                !string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
            {
                return ProcessStepRunStatus.Blocked;
            }

            return declaredOutcome.Status;
        }

        if (!string.IsNullOrWhiteSpace(missingConcreteProofSummary) ||
            !string.IsNullOrWhiteSpace(incompleteImplementationSummary) ||
            !string.IsNullOrWhiteSpace(missingConcreteImplementationProofSummary) ||
            !string.IsNullOrWhiteSpace(invalidBrowserProofSummary) ||
            !string.IsNullOrWhiteSpace(missingRequiredArtifactSummary))
        {
            return ProcessStepRunStatus.Blocked;
        }

        if (CanImplicitlyCompleteGovernedStep(candidate, detail, missingRequiredTools, responseText))
        {
            return ProcessStepRunStatus.Completed;
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

    private static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileWrites(string? serializedSessionStateJson)
    {
        return ResolveSuccessfulSessionFileContents(
            serializedSessionStateJson,
            static toolName => string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
                               string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal),
            static callContent =>
            {
                if (!callContent.TryGetProperty("arguments", out var arguments) ||
                    arguments.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var path = TryResolveStringProperty(arguments, "path");
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                var content = TryResolveStringProperty(arguments, "content") ?? string.Empty;
                return new SessionFileContent(path.Trim(), content);
            },
            static _ => null);
    }

    private static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileReads(string? serializedSessionStateJson)
    {
        return ResolveSuccessfulSessionFileContents(
            serializedSessionStateJson,
            static toolName => string.Equals(toolName, "workspace_read_file", StringComparison.Ordinal),
            static callContent =>
            {
                if (!callContent.TryGetProperty("arguments", out var arguments) ||
                    arguments.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                var path = TryResolveStringProperty(arguments, "path");
                return string.IsNullOrWhiteSpace(path)
                    ? null
                    : new SessionFileContent(path.Trim(), string.Empty);
            },
            static resultContent =>
            {
                var path = TryResolveStringProperty(resultContent, "path");
                if (string.IsNullOrWhiteSpace(path))
                {
                    return null;
                }

                var content = TryResolveStringProperty(resultContent, "content") ?? string.Empty;
                return new SessionFileContent(path.Trim(), content);
            });
    }

    private static IReadOnlyList<SessionFileContent> ResolveSuccessfulSessionFileContents(
        string? serializedSessionStateJson,
        Func<string, bool> isTargetTool,
        Func<JsonElement, SessionFileContent?> resolveCallContent,
        Func<JsonElement, SessionFileContent?> resolveResultContent)
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

            var callsById = new Dictionary<string, SessionFileContent>(StringComparer.Ordinal);
            var successfulContents = new List<SessionFileContent>();

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
                        if (string.IsNullOrWhiteSpace(callId) ||
                            string.IsNullOrWhiteSpace(toolName) ||
                            !isTargetTool(toolName))
                        {
                            continue;
                        }

                        var fileContent = resolveCallContent(content);
                        if (fileContent is not null)
                        {
                            callsById[callId] = fileContent;
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
                        !callsById.TryGetValue(resultCallId, out var callFileContent) ||
                        !content.TryGetProperty("result", out var resultElement) ||
                        !IsSuccessfulSessionFunctionResult(resultElement))
                    {
                        continue;
                    }

                    var resultFileContent = resolveResultContent(resultElement);
                    successfulContents.Add(resultFileContent ?? callFileContent);
                }
            }

            return successfulContents;
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

        var missingProviderCredential =
            ((normalizedText.Contains("Environment variable '", StringComparison.OrdinalIgnoreCase) &&
              normalizedText.Contains("' is not set.", StringComparison.OrdinalIgnoreCase) &&
              !normalizedText.Contains("memory capability", StringComparison.OrdinalIgnoreCase)) ||
             normalizedText.Contains("No API key environment variable is configured for this provider", StringComparison.OrdinalIgnoreCase) ||
             normalizedText.Contains("No secret record or API key environment variable is configured for this provider", StringComparison.OrdinalIgnoreCase) ||
             (normalizedText.Contains("Secret record '", StringComparison.OrdinalIgnoreCase) &&
              (normalizedText.Contains("was not found.", StringComparison.OrdinalIgnoreCase) ||
               normalizedText.Contains("could not be decrypted", StringComparison.OrdinalIgnoreCase))));
        if (missingProviderCredential)
        {
            failureSummary = "The assigned provider did not have usable credentials in the current environment.";
            return true;
        }

        if (normalizedText.Contains("The provider completed without returning text.", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("provider completed without returning text", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("provider returned an empty response", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "The assigned provider completed without returning text.";
            return true;
        }

        if (Regex.IsMatch(
                normalizedText,
                @"Response status code does not indicate success:\s*5\d\d\b",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            normalizedText.Contains("Internal Server Error", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("Bad Gateway", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("Service Unavailable", StringComparison.OrdinalIgnoreCase) ||
            normalizedText.Contains("Gateway Timeout", StringComparison.OrdinalIgnoreCase))
        {
            failureSummary = "The assigned provider returned an upstream server error before the agent produced a usable response.";
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

        if (RequiresConcreteTestProof(candidate))
        {
            requiredToolNames.Add("workspace_dotnet_test");
        }

        if (RequiresConcreteBrowserProof(candidate))
        {
            requiredToolNames.AddRange(ImplicitBrowserProofToolNames);
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

    private static bool CanImplicitlyCompleteGovernedStep(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyCollection<string> missingRequiredTools,
        string? responseText)
    {
        return CanImplicitlyCompleteGovernedImplementationStep(
                   candidate,
                   detail,
                   missingRequiredTools,
                   responseText) ||
               CanImplicitlyCompleteGovernedArtifactResponseStep(
                   candidate,
                   detail,
                   missingRequiredTools,
                   responseText);
    }

    private static bool CanImplicitlyCompleteGovernedImplementationStep(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyCollection<string> missingRequiredTools,
        string? responseText)
    {
        if (!RequiresGovernedStepOutcome(candidate.StepRun) ||
            !RequiresConcreteImplementationProof(candidate) ||
            candidate.BranchOutcomes.Count > 0 ||
            candidate.RequiresExplicitBranchOutcomeSelection ||
            detail.Run.State != ExecutionState.Completed ||
            detail.Run.PendingApprovals.Count > 0 ||
            detail.Run.Outcome != RunOutcome.Succeeded ||
            missingRequiredTools.Count > 0)
        {
            return false;
        }

        if (ResolveUnresolvedCriticalToolFailures(detail).Count > 0 ||
            TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
            !string.IsNullOrWhiteSpace(ResolveMissingRequiredArtifactSummary(candidate, detail, responseText)) ||
            !string.IsNullOrWhiteSpace(ResolveIncompleteImplementationSummary(candidate, responseText)) ||
            !string.IsNullOrWhiteSpace(ResolveMissingConcreteImplementationProofSummary(candidate, detail)) ||
            TryResolveDeclaredStepOutcome(candidate, responseText, out _))
        {
            return false;
        }

        if (detail.Artifacts.Count == 0)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(detail.Run.ResultSummary) ||
               !string.IsNullOrWhiteSpace(ResolveRecoveredExecutionResponseText(detail));
    }

    private static bool CanImplicitlyCompleteGovernedArtifactResponseStep(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyCollection<string> missingRequiredTools,
        string? responseText)
    {
        if (!RequiresGovernedStepOutcome(candidate.StepRun) ||
            RequiresConcreteImplementationProof(candidate) ||
            candidate.ExpectedArtifacts.Count == 0 ||
            candidate.BranchOutcomes.Count > 0 ||
            candidate.RequiresExplicitBranchOutcomeSelection ||
            detail.Run.State != ExecutionState.Completed ||
            detail.Run.PendingApprovals.Count > 0 ||
            detail.Run.Outcome != RunOutcome.Succeeded ||
            missingRequiredTools.Count > 0)
        {
            return false;
        }

        if (ResolveUnresolvedCriticalToolFailures(detail).Count > 0 ||
            TryResolveRecoverableProviderFailure(detail, responseText, out _) ||
            !string.IsNullOrWhiteSpace(ResolveMissingConcreteProofSummary(candidate, responseText)) ||
            !string.IsNullOrWhiteSpace(ResolveIncompleteImplementationSummary(candidate, responseText)) ||
            !string.IsNullOrWhiteSpace(ResolveMissingRequiredArtifactSummary(candidate, detail, responseText)) ||
            TryResolveDeclaredStepOutcome(candidate, responseText, out _))
        {
            return false;
        }

        return HasRequiredArtifactResponseSections(candidate, responseText);
    }

    private static bool RequiresConcreteImplementationProof(DispatchCandidate candidate)
    {
        return candidate.StepRun.StepKind == ProcessStepKind.Work &&
               (candidate.StepRun.Title.Contains("implement", StringComparison.OrdinalIgnoreCase) ||
                candidate.ExpectedArtifacts.Any(item =>
                    item.ArtifactKind == ProcessArtifactKind.Deliverable &&
                    item.Title.Contains("change set", StringComparison.OrdinalIgnoreCase)));
    }

    private static bool RequiresConcreteTestProof(DispatchCandidate candidate)
    {
        return RequiresConcreteImplementationProof(candidate) &&
               (candidate.StepRun.Title.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                (candidate.WorkBrief?.WorkBriefText?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (candidate.WorkBrief?.ExpectedOutcome?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false) ||
                (candidate.WorkBrief?.EvidenceExpectationSummary?.Contains("test", StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static bool RequiresConcreteImplementationReview(DispatchCandidate candidate)
    {
        return candidate.StepRun.Title.Contains("peer review", StringComparison.OrdinalIgnoreCase) ||
               candidate.StepRun.Title.Contains("integration readiness", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasRequiredArtifactResponseSections(
        DispatchCandidate candidate,
        string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        var requiredArtifactTitles = candidate.ExpectedArtifacts
            .Where(item => item.IsRequired)
            .Select(item => item.Title?.Trim())
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (requiredArtifactTitles.Count == 0)
        {
            return false;
        }

        return requiredArtifactTitles.All(title => ContainsArtifactResponseSection(responseText, title!));
    }

    private static bool ContainsArtifactResponseSection(string responseText, string artifactTitle)
    {
        if (string.IsNullOrWhiteSpace(responseText) || string.IsNullOrWhiteSpace(artifactTitle))
        {
            return false;
        }

        var escapedTitle = Regex.Escape(artifactTitle.Trim());
        if (Regex.IsMatch(
                responseText,
                $@"(^|\r?\n)\s{{0,3}}(?:#+\s*)?{escapedTitle}\s*(?:\r?\n|:)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return true;
        }

        return false;
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

    private static bool RequiresConcreteBrowserProof(DispatchCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        return ContainsConcreteBrowserProofSignal(candidate.WorkBrief?.Title) ||
               ContainsConcreteBrowserProofSignal(candidate.WorkBrief?.WorkBriefText) ||
               ContainsConcreteBrowserProofSignal(candidate.WorkBrief?.ExpectedOutcome) ||
               ContainsConcreteBrowserProofSignal(candidate.WorkBrief?.EvidenceExpectationSummary) ||
               candidate.ExpectedArtifacts.Any(item =>
                   ContainsConcreteBrowserProofSignal(item.Title) ||
                   ContainsConcreteBrowserProofSignal(item.ValidationRequirementSummary));
    }

    private static string ResolveMissingConcreteImplementationProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return string.Empty;
        }

        if (ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
            .Any(projection => CanSatisfyConcreteImplementationProofWithProcessMock(candidate, projection)))
        {
            return string.Empty;
        }

        var successfulReceipts = detail.ToolReceipts
            .Where(receipt => !IsFailedToolReceipt(receipt))
            .ToList();
        var concreteReadReceipt = ResolveLatestReceipt(
            successfulReceipts,
            "workspace_read_file",
            requireConcreteProductPath: true,
            requireConcreteSourceOrProjectPath: true);
        if (concreteReadReceipt is null)
        {
            return "the current attempt did not read any concrete product source or project file";
        }

        var concreteMutationReceipts = successfulReceipts
            .Where(receipt => ConcreteProductMutationToolNames.Contains(NormalizeToolToken(receipt.ToolName)))
            .Where(IsConcreteProductMutationReceipt)
            .ToList();

        var latestMutationReceipt = concreteMutationReceipts
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();

        var blazorRouteProofSummary = ResolveMissingBlazorWebAppRouteProofSummary(candidate, detail, successfulReceipts);
        if (!string.IsNullOrWhiteSpace(blazorRouteProofSummary))
        {
            return blazorRouteProofSummary;
        }

        var blazorHostingShapeSummary = ResolveInvalidBlazorWebAppHostingShapeSummary(candidate, detail, successfulReceipts);
        if (!string.IsNullOrWhiteSpace(blazorHostingShapeSummary))
        {
            return blazorHostingShapeSummary;
        }

        var calculatorImplementationSummary = ResolveMissingCalculatorLikeImplementationProofSummary(candidate, detail, successfulReceipts);
        if (!string.IsNullOrWhiteSpace(calculatorImplementationSummary))
        {
            return calculatorImplementationSummary;
        }

        var successfulBuildReceipt = ResolveLatestReceipt(
            successfulReceipts,
            "workspace_dotnet_build",
            requireConcreteProductPath: false,
            requireConcreteSourceOrProjectPath: false);
        if (successfulBuildReceipt is null)
        {
            return "the current attempt did not run workspace_dotnet_build successfully";
        }

        var buildTargetPaths = ResolveWorkspacePathsFromToolRequest(successfulBuildReceipt.RequestSummary);
        if (buildTargetPaths.Count > 0 && !buildTargetPaths.Any(IsConcreteProductPath))
        {
            return "the current attempt built only managed artifact paths instead of the concrete product project";
        }

        if (latestMutationReceipt is not null)
        {
            if (IsReceiptAfter(latestMutationReceipt, successfulBuildReceipt))
            {
                return "workspace_dotnet_build ran before the latest concrete product mutation";
            }

            if (IsReceiptAfter(latestMutationReceipt, concreteReadReceipt))
            {
                return "workspace_read_file ran before the latest concrete product mutation";
            }

            var latestScaffoldReceipt = concreteMutationReceipts
                .Where(receipt => string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_new", StringComparison.Ordinal))
                .OrderByDescending(receipt => receipt.CompletedAtUtc)
                .ThenByDescending(receipt => receipt.StartedAtUtc)
                .FirstOrDefault();
            if (latestScaffoldReceipt is not null &&
                !successfulReceipts.Any(receipt =>
                    ConcreteProductSourceWriteToolNames.Contains(NormalizeToolToken(receipt.ToolName)) &&
                    IsReceiptAfter(receipt, latestScaffoldReceipt) &&
                    HasConcreteProductSourceOrProjectPath(receipt)))
            {
                return "the latest scaffold was not followed by a concrete product source or project file write";
            }
        }

        if (RequiresConcreteTestProof(candidate))
        {
            var successfulTestReceipt = ResolveLatestReceipt(
                successfulReceipts,
                "workspace_dotnet_test",
                requireConcreteProductPath: false,
                requireConcreteSourceOrProjectPath: false);
            if (successfulTestReceipt is null)
            {
                return "the current implementation attempt did not run workspace_dotnet_test successfully even though this step includes tests";
            }

            var testTargetPaths = ResolveWorkspacePathsFromToolRequest(successfulTestReceipt.RequestSummary);
            if (testTargetPaths.Count > 0 && !testTargetPaths.Any(IsConcreteProductPath))
            {
                return "the current implementation attempt tested only managed artifact paths instead of the concrete product test project";
            }

            if (latestMutationReceipt is not null &&
                IsReceiptAfter(latestMutationReceipt, successfulTestReceipt))
            {
                return "workspace_dotnet_test ran before the latest concrete product mutation";
            }
        }

        return string.Empty;
    }

    private static string ResolveMissingBlazorWebAppRouteProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        if (!RequiresBlazorWebAppRouteProof(candidate, detail, successfulReceipts))
        {
            return string.Empty;
        }

        var hasComponentsPagesMutation = successfulReceipts
            .Where(IsConcreteProductSourceMutationReceipt)
            .Any(HasConcreteBlazorComponentsPagePath);
        var componentPageReads = successfulReceipts
            .Where(receipt => string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_read_file", StringComparison.Ordinal))
            .Where(HasConcreteBlazorComponentsPagePath)
            .ToList();
        if (!hasComponentsPagesMutation &&
            componentPageReads.Count == 0)
        {
            var hasLegacyRootPageMutation = successfulReceipts
                .Where(IsConcreteProductSourceMutationReceipt)
                .Any(HasConcreteLegacyRootPagePath);
            return hasLegacyRootPageMutation
                ? "the current Blazor Web App attempt mutated a legacy root Pages/*.razor route instead of Components/Pages/*.razor; move that UI into Components/Pages/Home.razor and delete stale root Pages/Home.razor or Pages/Index.razor routes"
                : "the current Blazor Web App attempt did not read or mutate any routed page under Components/Pages";
        }

        var latestComponentsPagesMutation = successfulReceipts
            .Where(IsConcreteProductSourceMutationReceipt)
            .Where(HasConcreteBlazorComponentsPagePath)
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        var latestComponentsPagesRead = componentPageReads
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
        if (latestComponentsPagesMutation is not null &&
            (latestComponentsPagesRead is null || IsReceiptAfter(latestComponentsPagesMutation, latestComponentsPagesRead)))
        {
            return "workspace_read_file for the Components/Pages routed page ran before the latest routed page mutation";
        }

        return string.Empty;
    }

    private static string ResolveInvalidBlazorWebAppHostingShapeSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        if (!RequiresBlazorWebAppRouteProof(candidate, detail, successfulReceipts))
        {
            return string.Empty;
        }

        var routeFileWrites = ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson)
            .Where(item => IsBlazorRoutesPath(item.Path))
            .ToList();
        var routeFileReads = ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson)
            .Where(item => IsBlazorRoutesPath(item.Path))
            .ToList();

        if (routeFileWrites.Concat(routeFileReads).Any(item => ContainsRazorPageDirective(item.Content)))
        {
            return "the current Blazor Web App attempt left an @page directive in Components/Routes.razor; restore Routes.razor as the Router-only host and keep route directives in Components/Pages/Home.razor";
        }

        var sessionFileContents = routeFileWrites
            .Concat(routeFileReads)
            .Concat(ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson))
            .Concat(ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson))
            .ToList();
        if (sessionFileContents
            .Where(item => IsBlazorHostProgramPath(item.Path))
            .Any(item => ContainsBlazorWebAssemblyHostingContent(item.Content)))
        {
            return "the current Blazor Web App attempt replaced Program.cs with WebAssemblyHostBuilder hosting; restore the generated WebApplication/AddRazorComponents/MapRazorComponents<App>() server-side Blazor Web App shape";
        }

        if (sessionFileContents
            .Where(item => IsBlazorHostProjectFilePath(item.Path))
            .Any(item => ContainsLegacyBlazorComponentPackageReferences(item.Content)))
        {
            return "the current Blazor Web App attempt added obsolete ASP.NET Core 7 component package references to the net10 host project; remove those package references and rely on the shared framework";
        }

        return routeFileReads.Count == 0
            ? "the current Blazor Web App attempt did not read Components/Routes.razor to verify the generated Router hosting shape"
            : string.Empty;
    }

    private static string ResolveMissingCalculatorLikeImplementationProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        if (!RequiresCalculatorLikeImplementationProof(candidate, detail))
        {
            return string.Empty;
        }

        var fileWrites = ResolveSuccessfulSessionFileWrites(detail.Run.SerializedSessionStateJson);
        var fileReads = ResolveSuccessfulSessionFileReads(detail.Run.SerializedSessionStateJson);
        var fileContents = fileWrites
            .Concat(fileReads)
            .ToList();
        var engineWrites = fileWrites
            .Where(item => IsCalculatorEngineSourcePath(item.Path))
            .ToList();
        var engineContents = fileContents
            .Where(item => IsCalculatorEngineSourcePath(item.Path))
            .ToList();
        if (engineContents.Count == 0)
        {
            return "the current calculator implementation attempt did not write or read a concrete CalculatorEngine domain/application source file";
        }

        if (!engineContents.Any(item => ContainsCalculatorEngineImplementation(item.Content)))
        {
            return "the current calculator implementation wrote CalculatorEngine without concrete Add, Subtract, Multiply, and Divide operations";
        }

        if (engineWrites.Count > 0 &&
            !successfulReceipts.Any(receipt =>
                string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_read_file", StringComparison.Ordinal) &&
                ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary).Any(IsCalculatorEngineSourcePath)))
        {
            return "the current calculator implementation attempt did not read CalculatorEngine after writing it";
        }

        var routedPageContents = fileContents
            .Where(item => IsBlazorComponentsPagePath(item.Path))
            .ToList();
        if (routedPageContents.Any(item => ContainsMalformedDoubleQuotedRazorStringCallback(item.Content)))
        {
            return "the current calculator routed UI wrote a string literal inside a double-quoted Razor event attribute; either change the handlers to char signatures and use @onclick=\"() => AppendDigit('1')\", or keep string handlers and use single-quoted attributes such as @onclick='() => AppendDigit(\"1\")'";
        }

        if (routedPageContents.Any(item => ContainsCalculatorStringHandlerWithCharCallback(item.Content)))
        {
            return "the current calculator routed UI passes char literals to handlers that still accept string, causing CS1503; either change those handlers to char parameters or keep string handlers and use single-quoted attributes such as @onclick='() => AppendToResult(\"1\")'";
        }

        if (!routedPageContents.Any(item => ContainsCalculatorRoutedUiContent(item.Content)))
        {
            return "the current calculator implementation attempt did not leave a non-placeholder Components/Pages routed UI with CalculatorEngine-backed arithmetic controls, equals/evaluate behavior, keypad, and history";
        }

        if (routedPageContents.Any(item => ContainsInjectedCalculatorEngine(item.Content)))
        {
            var programContents = fileContents
                .Where(item => IsBlazorHostProgramPath(item.Path))
                .ToList();
            if (!programContents.Any(item => ContainsCalculatorEngineServiceRegistration(item.Content)))
            {
                return "the current calculator implementation injects CalculatorEngine in the routed UI but did not register CalculatorEngine in Program.cs before building the app";
            }
        }

        if (!RequiresConcreteTestProof(candidate))
        {
            return string.Empty;
        }

        var testProjectWrites = fileContents
            .Where(item => IsConcreteTestProjectFilePath(item.Path))
            .ToList();
        if (!testProjectWrites.Any(item => ContainsCalculatorHostProjectReference(item.Content)))
        {
            return "the current calculator implementation attempt did not write or read a sibling test project with a ProjectReference to the Calculator host project";
        }

        var testSourceWrites = fileContents
            .Where(item => IsConcreteTestSourcePath(item.Path))
            .ToList();
        if (testSourceWrites.Count == 0)
        {
            return "the current calculator implementation attempt did not write or read meaningful sibling test source";
        }

        return testSourceWrites.Any(item => ContainsCalculatorEngineTestContent(item.Content))
            ? string.Empty
            : "the current calculator implementation attempt wrote tests that do not exercise CalculatorEngine arithmetic behavior";
    }

    private static bool RequiresCalculatorLikeImplementationProof(DispatchCandidate candidate, ExecutionRunDetail detail)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return false;
        }

        var contextText = string.Join(
            Environment.NewLine,
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.Name,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            detail.Run.InputSummary,
            detail.Run.ResultSummary);

        return contextText.Contains("Calculator", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsRazorPageDirective(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               RazorPageDirectiveRegex.IsMatch(content);
    }

    private static bool ContainsMalformedDoubleQuotedRazorStringCallback(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               MalformedDoubleQuotedRazorStringCallbackRegex.IsMatch(content);
    }

    private static bool ContainsCalculatorStringHandlerWithCharCallback(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        return RazorCharLiteralCallbackRegex
            .Matches(content)
            .Cast<Match>()
            .Select(match => match.Groups["handler"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Any(handlerName => ContainsStringParameterHandler(content, handlerName));
    }

    private static bool ContainsStringParameterHandler(string content, string handlerName)
    {
        return Regex.IsMatch(
            content,
            $@"\b{Regex.Escape(handlerName)}\s*\(\s*string\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool ContainsBlazorWebAssemblyHostingContent(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               (content.Contains("WebAssemblyHostBuilder", StringComparison.Ordinal) ||
                content.Contains("Microsoft.AspNetCore.Components.WebAssembly.Hosting", StringComparison.Ordinal) ||
                content.Contains("RootComponents.Add<App>", StringComparison.Ordinal));
    }

    private static bool ContainsLegacyBlazorComponentPackageReferences(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               (content.Contains("Microsoft.AspNetCore.Components.WebAssembly", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Microsoft.AspNetCore.Components.Web\" Version=\"7.", StringComparison.OrdinalIgnoreCase) ||
                content.Contains("Microsoft.AspNetCore.Components\" Version=\"7.", StringComparison.OrdinalIgnoreCase));
    }

    private static bool ContainsCalculatorEngineImplementation(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            !content.Contains("CalculatorEngine", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsCalculatorOperation(content, "Add") &&
               ContainsCalculatorOperation(content, "Subtract") &&
               ContainsCalculatorOperation(content, "Multiply") &&
               ContainsCalculatorOperation(content, "Divide");
    }

    private static bool ContainsCalculatorRoutedUiContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            !ContainsRazorPageDirective(content) ||
            !content.Contains("CalculatorEngine", StringComparison.Ordinal))
        {
            return false;
        }

        return ContainsCalculatorUiOperation(content, "Add", "+") &&
               ContainsCalculatorUiOperation(content, "Subtract", "-") &&
               ContainsCalculatorUiOperation(content, "Multiply", "*") &&
               ContainsCalculatorUiOperation(content, "Divide", "/") &&
               ContainsEqualsOrEvaluateAction(content) &&
               ContainsCalculatorHistoryUi(content) &&
               ContainsCalculatorKeypadUi(content);
    }

    private static bool ContainsCalculatorUiOperation(string content, string operationName, string operationSymbol)
    {
        return content.Contains(operationName, StringComparison.OrdinalIgnoreCase) ||
               content.Contains(operationSymbol, StringComparison.Ordinal);
    }

    private static bool ContainsEqualsOrEvaluateAction(string content)
    {
        return content.Contains("Equals", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("Evaluate", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("Calculate", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("=", StringComparison.Ordinal);
    }

    private static bool ContainsCalculatorHistoryUi(string content)
    {
        return content.Contains("history", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCalculatorKeypadUi(string content)
    {
        if (content.Contains("keypad", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("AppendDigit", StringComparison.OrdinalIgnoreCase) ||
            content.Contains("InputDigit", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var digitButtonMatches = Regex.Matches(
            content,
            @"(?is)<button\b[^>]*>\s*[0-9]\s*</button>|['""]\s*[0-9]\s*['""]");
        return digitButtonMatches.Count >= 10;
    }

    private static bool ContainsInjectedCalculatorEngine(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               (CalculatorEngineInjectDirectiveRegex.IsMatch(content) ||
                content.Contains("[Inject]", StringComparison.Ordinal) &&
                content.Contains("CalculatorEngine", StringComparison.Ordinal));
    }

    private static bool ContainsCalculatorEngineServiceRegistration(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            !content.Contains("CalculatorEngine", StringComparison.Ordinal))
        {
            return false;
        }

        return CalculatorEngineServiceRegistrationRegex.IsMatch(content);
    }

    private static bool ContainsCalculatorHostProjectReference(string content)
    {
        return !string.IsNullOrWhiteSpace(content) &&
               content.Contains("<ProjectReference", StringComparison.OrdinalIgnoreCase) &&
               content.Contains("Calculator.csproj", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsCalculatorEngineTestContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content) ||
            !content.Contains("CalculatorEngine", StringComparison.Ordinal) ||
            !content.Contains("Assert.", StringComparison.Ordinal))
        {
            return false;
        }

        var hasTestAttribute =
            content.Contains("[Fact]", StringComparison.Ordinal) ||
            content.Contains("[Theory]", StringComparison.Ordinal) ||
            content.Contains("[TestMethod]", StringComparison.Ordinal) ||
            content.Contains("[Test]", StringComparison.Ordinal);
        return hasTestAttribute &&
               ContainsCalculatorOperation(content, "Add") &&
               ContainsCalculatorOperation(content, "Subtract") &&
               ContainsCalculatorOperation(content, "Multiply") &&
               ContainsCalculatorOperation(content, "Divide");
    }

    private static bool ContainsCalculatorOperation(string content, string operationName)
    {
        return content.Contains(operationName, StringComparison.OrdinalIgnoreCase);
    }

    private static bool RequiresBlazorWebAppRouteProof(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        IReadOnlyList<ToolExecutionReceiptRecord> successfulReceipts)
    {
        if (!RequiresConcreteImplementationProof(candidate))
        {
            return false;
        }

        return successfulReceipts.Any(IsBlazorWebAppScaffoldReceipt) ||
               successfulReceipts.Any(HasConcreteBlazorComponentsPagePath) ||
               ContainsStrongBlazorWebAppContext(candidate, detail);
    }

    private static bool ContainsStrongBlazorWebAppContext(DispatchCandidate candidate, ExecutionRunDetail detail)
    {
        var contextText = string.Join(
            Environment.NewLine,
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.Name,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.HandoffSummary,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            detail.Run.InputSummary,
            detail.Run.ResultSummary,
            detail.Run.SerializedSessionStateJson);

        return contextText.Contains("notes: Blazor", StringComparison.OrdinalIgnoreCase) ||
               contextText.Contains("Blazor Server-Side Rendering", StringComparison.OrdinalIgnoreCase) ||
               contextText.Contains("Blazor SSR (", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldIncludeBlazorWebAppHostingContract(
        DispatchCandidate candidate,
        string? projectStructureGroundingSummary,
        string? artifactInspectionGroundingSummary)
    {
        var contextText = string.Join(
            Environment.NewLine,
            candidate.Definition.Name,
            candidate.Definition.Summary,
            candidate.Definition.ValueStatement,
            candidate.Run.Name,
            candidate.Run.TriggerReason,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.HandoffSummary,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            projectStructureGroundingSummary,
            artifactInspectionGroundingSummary);

        return contextText.Contains("Blazor", StringComparison.OrdinalIgnoreCase) ||
               contextText.Contains("dotnet new blazor", StringComparison.OrdinalIgnoreCase) ||
               contextText.Contains("Components/Pages", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendBlazorWebAppHostingContract(StringBuilder builder)
    {
        builder.AppendLine("- On current .NET, `dotnet new blazor` creates a Blazor Web App: `Program.cs` maps Razor components, the app shell is `Components/App.razor`, routing is `Components/Routes.razor`, and routed UI belongs under `Components/Pages`.");
        builder.AppendLine("- Treat `Blazor SSR`, `Blazor Server-Side Rendering`, or `Blazor Web App` as this Blazor Web App hosting shape, not as legacy Blazor Server plus Razor Pages.");
        builder.AppendLine("- Do not recommend, create, or preserve `Pages/_Host.cshtml`, `Startup.cs`, `UseStartup<Startup>()`, root `Pages/*.razor` routes, `blazor.server.js`, or ASP.NET Core 7.x `Microsoft.AspNetCore.Components*` package references for a net10 Blazor Web App.");
        builder.AppendLine("- If an upstream artifact says `Blazor Server-Side`, `Blazor Server`, or `Razor Pages` while the live project structure or scaffold says Blazor SSR/Web App, treat that wording as stale shorthand and normalize the implementation plan back to Blazor Web App with routed pages under `Components/Pages`.");
        builder.AppendLine("- If legacy hosting files are present from a prior bad repair, delete those specific legacy files first, restore the generated minimal Blazor Web App shape, then build and test. Do not recursively delete the host project directory or build on top of both hosting models.");
    }

    private static bool IsBlazorWebAppScaffoldReceipt(ToolExecutionReceiptRecord receipt)
    {
        return string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_new", StringComparison.Ordinal) &&
               receipt.RequestSummary.Contains("blazor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConcreteProductSourceMutationReceipt(ToolExecutionReceiptRecord receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        return (string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
                string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal) ||
                string.Equals(toolName, "workspace_move_path", StringComparison.Ordinal)) &&
               HasConcreteProductSourceOrProjectPath(receipt);
    }

    private static bool HasConcreteBlazorComponentsPagePath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(path => IsConcreteProductPath(path) && IsBlazorComponentsPagePath(path));
    }

    private static bool HasConcreteLegacyRootPagePath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(path => IsConcreteProductPath(path) && IsLegacyRootRazorPagePath(path));
    }

    private static bool IsBlazorRoutesPath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return normalized.EndsWith("/Components/Routes.razor", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlazorComponentsPagePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return string.Equals(Path.GetExtension(normalized), ".razor", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains("/Components/Pages/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLegacyRootRazorPagePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return string.Equals(Path.GetExtension(normalized), ".razor", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains("/Pages/", StringComparison.OrdinalIgnoreCase) &&
               !normalized.Contains("/Components/Pages/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlazorHostProgramPath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return IsConcreteProductSourceOrProjectPath(normalized) &&
               !normalized.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetFileName(normalized), "Program.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBlazorHostProjectFilePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (!IsConcreteProductSourceOrProjectPath(normalized) ||
            !string.Equals(Path.GetExtension(normalized), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !IsTestProjectName(Path.GetFileNameWithoutExtension(normalized));
    }

    private static bool IsCalculatorEngineSourcePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return IsConcreteProductSourceOrProjectPath(normalized) &&
               !normalized.Contains(".Tests/", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(Path.GetFileName(normalized), "CalculatorEngine.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConcreteTestProjectFilePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (!IsConcreteProductSourceOrProjectPath(normalized) ||
            !string.Equals(Path.GetExtension(normalized), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var projectName = Path.GetFileNameWithoutExtension(normalized);
        return IsTestProjectName(projectName);
    }

    private static bool IsConcreteTestSourcePath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        return IsConcreteProductSourceOrProjectPath(normalized) &&
               string.Equals(Path.GetExtension(normalized), ".cs", StringComparison.OrdinalIgnoreCase) &&
               normalized.Contains(".Tests/", StringComparison.OrdinalIgnoreCase);
    }

    private static ToolExecutionReceiptRecord? ResolveLatestReceipt(
        IEnumerable<ToolExecutionReceiptRecord> receipts,
        string normalizedToolName,
        bool requireConcreteProductPath,
        bool requireConcreteSourceOrProjectPath)
    {
        return receipts
            .Where(receipt => string.Equals(NormalizeToolToken(receipt.ToolName), normalizedToolName, StringComparison.Ordinal))
            .Where(receipt => !requireConcreteProductPath || HasConcreteProductPath(receipt))
            .Where(receipt => !requireConcreteSourceOrProjectPath || HasConcreteProductSourceOrProjectPath(receipt))
            .OrderByDescending(receipt => receipt.CompletedAtUtc)
            .ThenByDescending(receipt => receipt.StartedAtUtc)
            .FirstOrDefault();
    }

    private static bool IsConcreteProductMutationReceipt(ToolExecutionReceiptRecord receipt)
    {
        var toolName = NormalizeToolToken(receipt.ToolName);
        if (string.Equals(toolName, "workspace_write_file", StringComparison.Ordinal) ||
            string.Equals(toolName, "workspace_append_file", StringComparison.Ordinal))
        {
            return HasConcreteProductSourceOrProjectPath(receipt);
        }

        return HasConcreteProductPath(receipt);
    }

    private static bool HasConcreteProductPath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(IsConcreteProductPath);
    }

    private static bool HasConcreteProductSourceOrProjectPath(ToolExecutionReceiptRecord receipt)
    {
        return ResolveWorkspacePathsFromToolRequest(receipt.RequestSummary)
            .Any(IsConcreteProductSourceOrProjectPath);
    }

    private static IReadOnlyList<string> ResolveWorkspacePathsFromToolRequest(string requestSummary)
    {
        if (string.IsNullOrWhiteSpace(requestSummary))
        {
            return [];
        }

        var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WorkspacePathInToolRequestRegex.Matches(requestSummary))
        {
            var candidatePath = match.Groups["path"].Value;
            if (TryMapWorkspacePathForPrompt(candidatePath, out var promptPath))
            {
                paths.Add(promptPath);
            }
        }

        return paths.ToList();
    }

    private static bool TryMapWorkspacePathForPrompt(string path, out string promptPath)
    {
        promptPath = string.Empty;
        var normalized = path.Trim().TrimEnd(',', ';', '.', ')', ']', '}').Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (normalized.StartsWith($"{ExternalTargetAliasRoot}/", StringComparison.OrdinalIgnoreCase))
        {
            promptPath = normalized;
            return true;
        }

        if (normalized.Length < 3 || !char.IsLetter(normalized[0]) || normalized[1] != ':' || normalized[2] != '/')
        {
            return false;
        }

        var driveLetter = char.ToUpperInvariant(normalized[0]);
        var remainder = normalized.Length == 3
            ? string.Empty
            : normalized[3..].Trim('/');
        promptPath = string.IsNullOrWhiteSpace(remainder)
            ? $"{ExternalTargetAliasRoot}/{driveLetter}"
            : $"{ExternalTargetAliasRoot}/{driveLetter}/{remainder}";
        return true;
    }

    private static bool IsConcreteProductSourceOrProjectPath(string promptPath)
    {
        if (!IsConcreteProductPath(promptPath))
        {
            return false;
        }

        var extension = Path.GetExtension(promptPath);
        return IsCodeOrProjectExtension(extension);
    }

    private static bool IsConcreteProductPath(string promptPath)
    {
        var normalized = WorkspaceScopeDescriptor.NormalizeRelativePath(promptPath);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length > 0 &&
               !IsManagedRootSegment(segments[0]) &&
               !segments.Any(IsNonProductPathSegment);
    }

    private static bool IsNonProductPathSegment(string segment)
    {
        return IsManagedRootSegment(segment) ||
               string.Equals(segment, ".playwright-mcp", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReceiptAfter(ToolExecutionReceiptRecord candidate, ToolExecutionReceiptRecord baseline)
    {
        return candidate.CompletedAtUtc > baseline.CompletedAtUtc ||
               candidate.CompletedAtUtc == baseline.CompletedAtUtc &&
               candidate.StartedAtUtc > baseline.StartedAtUtc;
    }

    private static string ResolveInvalidBrowserProofSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail)
    {
        if (!RequiresConcreteBrowserProof(candidate))
        {
            return string.Empty;
        }

        if (ContainsSerializedPowerShellErrorRecord(detail.Run.SerializedSessionStateJson))
        {
            return "the launch helper reported PowerShell errors on stderr despite a successful tool result";
        }

        var browserWorkingDirectory = ResolveProviderNativeBrowserWorkingDirectory(detail);
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory))
        {
            return string.Empty;
        }

        var outputsByToolName = ResolveSuccessfulSessionToolOutputFiles(detail.Run.SerializedSessionStateJson ?? string.Empty);
        if (!outputsByToolName.TryGetValue("browser_snapshot", out var snapshotFiles) ||
            snapshotFiles.Count == 0)
        {
            return string.Empty;
        }

        foreach (var snapshotFile in snapshotFiles)
        {
            if (!TryReadBrowserOutputText(browserWorkingDirectory, snapshotFile, out var snapshotText))
            {
                continue;
            }

            if (ContainsStarterTemplateBrowserProof(snapshotText))
            {
                return "browser proof captured the default Blazor starter page instead of the requested application";
            }
        }

        return string.Empty;
    }

    private static bool ContainsSerializedPowerShellErrorRecord(string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return false;
        }

        return serializedSessionStateJson.Contains("Cannot overwrite variable PID because it is read-only or constant", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("WriteError:", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("ParserError:", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("RuntimeException:", StringComparison.OrdinalIgnoreCase) ||
               serializedSessionStateJson.Contains("FullyQualifiedErrorId", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadBrowserOutputText(
        string browserWorkingDirectory,
        string relativeOutputPath,
        out string text)
    {
        text = string.Empty;
        if (!TryResolveSafeBrowserOutputPath(browserWorkingDirectory, relativeOutputPath, out var fullPath) ||
            !File.Exists(fullPath))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(fullPath);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[MaxBrowserSnapshotInspectionCharacters];
            var length = reader.ReadBlock(buffer, 0, buffer.Length);
            text = new string(buffer, 0, length);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool TryResolveSafeBrowserOutputPath(
        string browserWorkingDirectory,
        string relativeOutputPath,
        out string fullPath)
    {
        fullPath = string.Empty;
        if (string.IsNullOrWhiteSpace(browserWorkingDirectory) ||
            string.IsNullOrWhiteSpace(relativeOutputPath) ||
            Path.IsPathRooted(relativeOutputPath))
        {
            return false;
        }

        var root = Path.GetFullPath(browserWorkingDirectory);
        var candidate = Path.GetFullPath(Path.Combine(root, relativeOutputPath.Replace('/', Path.DirectorySeparatorChar)));
        var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        fullPath = candidate;
        return true;
    }

    private static bool ContainsStarterTemplateBrowserProof(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("Hello, world!", StringComparison.OrdinalIgnoreCase) ||
               text.Contains("Welcome to your new app.", StringComparison.OrdinalIgnoreCase);
    }

    private static string ResolveMissingConcreteProofSummary(
        DispatchCandidate candidate,
        string? responseText)
    {
        if (!RequiresConcreteBrowserProof(candidate))
        {
            return string.Empty;
        }

        var normalizedResponse = CollapsePromptWhitespace(responseText).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return string.Empty;
        }

        if (normalizedResponse.Contains("browser proof cannot proceed", StringComparison.Ordinal) ||
            normalizedResponse.Contains("browser proof not possible", StringComparison.Ordinal) ||
            normalizedResponse.Contains("browser proof deferred", StringComparison.Ordinal))
        {
            return "the response says browser proof could not proceed";
        }

        if (normalizedResponse.Contains("manual qa: not possible", StringComparison.Ordinal) ||
            normalizedResponse.Contains("manual qa not possible", StringComparison.Ordinal))
        {
            return "the response says manual QA was not possible";
        }

        if (normalizedResponse.Contains("no screenshots", StringComparison.Ordinal) ||
            normalizedResponse.Contains("screenshots: none possible", StringComparison.Ordinal) ||
            normalizedResponse.Contains("screenshots were not possible", StringComparison.Ordinal))
        {
            return "the response says screenshots were not captured";
        }

        if (normalizedResponse.Contains("application is not running", StringComparison.Ordinal) ||
            normalizedResponse.Contains("app is not running", StringComparison.Ordinal) ||
            normalizedResponse.Contains("no running app", StringComparison.Ordinal) ||
            normalizedResponse.Contains("no runnable output", StringComparison.Ordinal))
        {
            return "the response says the app was not running";
        }

        if (normalizedResponse.Contains("cannot validate ui", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ui validation can not be performed", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ui validation cannot be performed", StringComparison.Ordinal))
        {
            return "the response says UI validation could not be performed";
        }

        return string.Empty;
    }

    private static string ResolveIncompleteImplementationSummary(
        DispatchCandidate candidate,
        string? responseText)
    {
        if (!RequiresConcreteImplementationProof(candidate) || string.IsNullOrWhiteSpace(responseText))
        {
            return string.Empty;
        }

        var normalizedResponse = CollapsePromptWhitespace(responseText).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return string.Empty;
        }

        var defersFeatureImplementation =
            normalizedResponse.Contains("ready for feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ready for later feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ready for further feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("next steps for feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("future feature implementation", StringComparison.Ordinal) ||
            normalizedResponse.Contains("later feature implementation", StringComparison.Ordinal) ||
            (normalizedResponse.Contains("ready for", StringComparison.Ordinal) &&
             normalizedResponse.Contains("implementation", StringComparison.Ordinal) &&
             normalizedResponse.Contains("feature, tests, and migration notes", StringComparison.Ordinal)) ||
            (normalizedResponse.Contains("structured for further", StringComparison.Ordinal) &&
             normalizedResponse.Contains("implementation", StringComparison.Ordinal));

        if (!defersFeatureImplementation &&
            normalizedResponse.Contains("later step", StringComparison.Ordinal) &&
            normalizedResponse.Contains("feature implementation", StringComparison.Ordinal))
        {
            defersFeatureImplementation = true;
        }

        var reportsMissingRequestedBehavior =
            normalizedResponse.Contains("not yet implemented", StringComparison.Ordinal) ||
            normalizedResponse.Contains("still untouched template output", StringComparison.Ordinal) ||
            normalizedResponse.Contains("untouched template output", StringComparison.Ordinal) ||
            (normalizedResponse.Contains("hello, world!", StringComparison.Ordinal) &&
             (normalizedResponse.Contains("still", StringComparison.Ordinal) ||
              normalizedResponse.Contains("template", StringComparison.Ordinal))) ||
            (normalizedResponse.Contains("no required", StringComparison.Ordinal) &&
             normalizedResponse.Contains("present yet", StringComparison.Ordinal)) ||
            (normalizedResponse.Contains("required", StringComparison.Ordinal) &&
             normalizedResponse.Contains("is not present yet", StringComparison.Ordinal));

        var reportsDeferredExecution =
            normalizedResponse.Contains("next required actions", StringComparison.Ordinal) ||
            normalizedResponse.Contains("next implementation steps", StringComparison.Ordinal) ||
            normalizedResponse.Contains("for the next agent or step", StringComparison.Ordinal) ||
            normalizedResponse.Contains("proceeding to implement", StringComparison.Ordinal);

        return defersFeatureImplementation || reportsMissingRequestedBehavior || reportsDeferredExecution
            ? "the response says the step only scaffolded the app and left the requested feature implementation for later work"
            : string.Empty;
    }

    private static string ResolveMissingRequiredArtifactSummary(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        string? responseText)
    {
        if (candidate.ExpectedArtifacts.Count == 0)
        {
            return string.Empty;
        }

        var missingRequiredArtifacts = candidate.ExpectedArtifacts
            .Where(item => item.IsRequired)
            .Where(item => !HasRecordedExpectedArtifact(candidate, detail, item))
            .Where(item => !CanAutoSatisfyRequiredArtifact(candidate, detail, item, responseText))
            .Select(item => item.Title.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();

        return missingRequiredArtifacts.Count == 0
            ? string.Empty
            : string.Join(", ", missingRequiredArtifacts);
    }

    private static bool HasRecordedExpectedArtifact(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        return detail.Artifacts.Any(artifact => ResolveArtifactExpectationId(candidate, artifact) == expectedArtifact.Id);
    }

    private static bool CanAutoSatisfyRequiredArtifact(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact,
        string? responseText)
    {
        if (CanProjectProcessMockArtifact(candidate, detail, expectedArtifact))
        {
            return true;
        }

        if (ShouldAutoRecordCompletedDecisionArtifact(expectedArtifact))
        {
            return true;
        }

        if (TryExtractExpectedArtifactRelativePath(expectedArtifact.ValidationRequirementSummary, out var declaredRelativePath))
        {
            return !string.IsNullOrWhiteSpace(ResolveProviderNativeBrowserToolName(declaredRelativePath)) ||
                   (IsUsableProjectedResponseArtifactContent(expectedArtifact, responseText) &&
                    IsResponseProjectableTextArtifact(declaredRelativePath));
        }

        return IsUsableProjectedResponseArtifactContent(expectedArtifact, responseText) &&
               CanProjectResponseTextArtifactWithoutDeclaredPath(expectedArtifact);
    }

    private static bool CanProjectProcessMockArtifact(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        DispatchArtifactExpectation expectedArtifact)
    {
        return ResolveProcessMockArtifactProjections(detail.Run.SerializedSessionStateJson)
            .Any(projection => ProcessMockArtifactMatchesExpectation(expectedArtifact, projection));
    }

    private static bool IsUsableProjectedResponseArtifactContent(
        DispatchArtifactExpectation expectedArtifact,
        string? responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        var normalizedResponse = CollapsePromptWhitespace(responseText);
        if (normalizedResponse.Length < 160)
        {
            return false;
        }

        if (IsConversationalNonArtifactResponse(normalizedResponse))
        {
            return false;
        }

        return HasExpectedArtifactContentSignals(expectedArtifact, responseText, normalizedResponse);
    }

    private static bool HasExpectedArtifactContentSignals(
        DispatchArtifactExpectation expectedArtifact,
        string responseText,
        string normalizedResponse)
    {
        if (ContainsArtifactResponseSection(responseText, expectedArtifact.Title))
        {
            return HasExpectedArtifactValidationSignals(expectedArtifact, normalizedResponse);
        }

        var responseTokens = TokenizeArtifactContentSignalText(normalizedResponse)
            .ToHashSet(StringComparer.Ordinal);
        if (responseTokens.Count == 0)
        {
            return false;
        }

        var titleTokens = TokenizeArtifactContentSignalText(expectedArtifact.Title)
            .ToList();
        if (titleTokens.Count >= 2)
        {
            var requiredTitleMatches = Math.Min(2, titleTokens.Count);
            if (titleTokens.Count(responseTokens.Contains) < requiredTitleMatches)
            {
                return false;
            }
        }

        return HasExpectedArtifactValidationSignals(expectedArtifact, responseTokens);
    }

    private static bool HasExpectedArtifactValidationSignals(
        DispatchArtifactExpectation expectedArtifact,
        string normalizedResponse)
    {
        var responseTokens = TokenizeArtifactContentSignalText(normalizedResponse)
            .ToHashSet(StringComparer.Ordinal);
        return HasExpectedArtifactValidationSignals(expectedArtifact, responseTokens);
    }

    private static bool HasExpectedArtifactValidationSignals(
        DispatchArtifactExpectation expectedArtifact,
        IReadOnlySet<string> responseTokens)
    {
        var validationTokens = TokenizeArtifactContentSignalText(expectedArtifact.ValidationRequirementSummary)
            .ToList();
        if (validationTokens.Count < 3)
        {
            return true;
        }

        return validationTokens.Count(responseTokens.Contains) >= Math.Min(2, validationTokens.Count);
    }

    private static IReadOnlyList<string> TokenizeArtifactContentSignalText(string value)
    {
        return TokenizeArtifactComparisonText(value)
            .Where(token => token.Length > 2)
            .Where(token => !ArtifactTitleNoiseTokens.Contains(token))
            .Where(token => !ArtifactContentNoiseTokens.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static bool IsConversationalNonArtifactResponse(string normalizedResponse)
    {
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return true;
        }

        var value = normalizedResponse.ToLowerInvariant();
        return value.Contains("ready to help", StringComparison.Ordinal) ||
               value.Contains("please let me know", StringComparison.Ordinal) ||
               value.Contains("let me know what", StringComparison.Ordinal) ||
               value.Contains("what specific", StringComparison.Ordinal) ||
               value.Contains("specific area or step", StringComparison.Ordinal) ||
               value.Contains("how can i help", StringComparison.Ordinal) ||
               value.Contains("i can help with", StringComparison.Ordinal) ||
               value.Contains("provide more details", StringComparison.Ordinal) ||
               value.Contains("please provide", StringComparison.Ordinal) ||
               value.Contains("need more information", StringComparison.Ordinal) ||
               value.Contains("not enough information", StringComparison.Ordinal) ||
               value.Contains("cannot proceed without", StringComparison.Ordinal) ||
               value.Contains("unable to proceed without", StringComparison.Ordinal);
    }

    private static bool ContainsConcreteBrowserProofSignal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = CollapsePromptWhitespace(value);
        return normalized.Contains("browser proof", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("screenshots", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("manual qa", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("ui validation", StringComparison.OrdinalIgnoreCase);
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

    private static bool ShouldIgnoreSupersededCriticalToolFailure(
        ExecutionRunDetail detail,
        ToolExecutionReceiptRecord receipt)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(receipt);

        if (ShouldIgnoreRecoveredImplementationScaffoldFailure(detail, receipt))
        {
            return true;
        }

        if (!receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedToolName = NormalizeToolToken(receipt.ToolName);
        if (string.IsNullOrWhiteSpace(normalizedToolName) ||
            !IsPlaceholderCriticalToolRequestSummary(normalizedToolName, receipt.RequestSummary))
        {
            return false;
        }

        return detail.ToolReceipts.Any(item =>
            !ReferenceEquals(item, receipt) &&
            string.Equals(NormalizeToolToken(item.ToolName), normalizedToolName, StringComparison.Ordinal) &&
            !IsFailedToolReceipt(item) &&
            !IsPlaceholderCriticalToolRequestSummary(normalizedToolName, item.RequestSummary));
    }

    private static bool ShouldIgnoreRecoveredImplementationScaffoldFailure(
        ExecutionRunDetail detail,
        ToolExecutionReceiptRecord receipt)
    {
        ArgumentNullException.ThrowIfNull(detail);
        ArgumentNullException.ThrowIfNull(receipt);

        if ((!receipt.ExitSummary.StartsWith("Failed", StringComparison.OrdinalIgnoreCase) &&
             !receipt.ExitSummary.StartsWith("Denied", StringComparison.OrdinalIgnoreCase)) ||
            !string.Equals(NormalizeToolToken(receipt.ToolName), "workspace_dotnet_new", StringComparison.Ordinal))
        {
            return false;
        }

        if (detail.Run.State != ExecutionState.Completed ||
            detail.Run.Outcome != RunOutcome.Succeeded)
        {
            return false;
        }

        var responseText = ResolveRecoveredExecutionResponseText(detail);
        if (!TryResolveDeclaredStepOutcome(responseText, out var declaredOutcome) ||
            declaredOutcome.Status != ProcessStepRunStatus.Completed)
        {
            return false;
        }

        return detail.ToolReceipts.Any(item =>
        {
            if (ReferenceEquals(item, receipt) || IsFailedToolReceipt(item))
            {
                return false;
            }

            if (item.CompletedAtUtc < receipt.CompletedAtUtc ||
                item.CompletedAtUtc == receipt.CompletedAtUtc && item.StartedAtUtc < receipt.StartedAtUtc)
            {
                return false;
            }

            return ImplementationProofToolNames.Contains(NormalizeToolToken(item.ToolName));
        });
    }

    private static bool IsPlaceholderCriticalToolRequestSummary(
        string normalizedToolName,
        string? requestSummary)
    {
        if (string.IsNullOrWhiteSpace(normalizedToolName))
        {
            return false;
        }

        var normalizedSummary = NormalizeToolToken(requestSummary ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedSummary))
        {
            return true;
        }

        if (string.Equals(normalizedSummary, normalizedToolName, StringComparison.Ordinal))
        {
            return true;
        }

        return normalizedToolName.StartsWith("workspace_", StringComparison.Ordinal) &&
               string.Equals(
                   normalizedSummary,
                   normalizedToolName["workspace_".Length..],
                   StringComparison.Ordinal);
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

    private static string BuildProcessMockArtifactExternalReferenceKey(
        Guid stepRunId,
        Guid artifactExpectationId,
        string relativePath)
    {
        return $"process-mock-artifact:{stepRunId:D}:{artifactExpectationId:D}:{NormalizeManagedRelativePathForComparison(relativePath)}";
    }

    private static string BuildMissingTechnicalAgentBindingDiagnostic(
        Guid processRunId,
        Guid stepRunId,
        string stepTitle,
        Guid currentExecutorPartyId,
        AiResourceBindingStatus? bindingStatus,
        Guid? technicalAgentId)
    {
        var statusSummary = bindingStatus?.ToString() ?? "MissingDirectorySummary";
        var technicalAgentSummary = technicalAgentId.HasValue
            ? technicalAgentId.Value.ToString("D")
            : "none";
        return $"Process automation dispatch cannot run step '{stepTitle}' ({stepRunId:D}) for process run {processRunId:D} because executor party {currentExecutorPartyId:D} is not bound to an active technical agent. Binding status: {statusSummary}; technical agent ID: {technicalAgentSummary}.";
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
        var normalizedWorkspaceRoot = Path.GetFullPath(workspaceRoot);
        var normalizedFullPath = Path.GetFullPath(fullPath);
        return string.Equals(normalizedFullPath, normalizedWorkspaceRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedFullPath.StartsWith(EnsureTrailingDirectorySeparator(normalizedWorkspaceRoot), StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryResolveArtifactFullPath(
        string workspaceRoot,
        string relativePath,
        out string fullPath,
        out string failureReason)
    {
        fullPath = string.Empty;
        failureReason = string.Empty;

        var normalizedRelativePath = WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath);
        if (string.IsNullOrWhiteSpace(normalizedRelativePath))
        {
            failureReason = "Artifact relative path is empty.";
            return false;
        }

        if (IsExternalTargetAliasPath(normalizedRelativePath))
        {
            return TryResolveExternalTargetArtifactFullPath(normalizedRelativePath, out fullPath, out failureReason);
        }

        fullPath = Path.GetFullPath(Path.Combine(
            workspaceRoot,
            normalizedRelativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (IsWithinWorkspace(workspaceRoot, fullPath))
        {
            return true;
        }

        failureReason = $"Artifact path '{normalizedRelativePath}' resolves outside the workspace root.";
        fullPath = string.Empty;
        return false;
    }

    private static bool TryResolveExternalTargetArtifactFullPath(
        string normalizedRelativePath,
        out string fullPath,
        out string failureReason)
    {
        fullPath = string.Empty;
        failureReason = string.Empty;

        var suffix = normalizedRelativePath.Length == ExternalTargetAliasRoot.Length
            ? string.Empty
            : normalizedRelativePath[(ExternalTargetAliasRoot.Length + 1)..];
        var segments = suffix.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 ||
            segments[0].Length != 1 ||
            !char.IsLetter(segments[0][0]))
        {
            failureReason = $"Artifact path '{normalizedRelativePath}' uses invalid external-target syntax.";
            return false;
        }

        var driveRoot = $"{char.ToUpperInvariant(segments[0][0])}:{Path.DirectorySeparatorChar}";
        var remainingSegments = segments.Skip(1).ToArray();
        fullPath = Path.GetFullPath(
            remainingSegments.Length == 0
                ? driveRoot
                : Path.Combine(driveRoot, Path.Combine(remainingSegments)));
        return true;
    }

    private static bool IsExternalTargetAliasPath(string normalizedRelativePath)
    {
        return string.Equals(normalizedRelativePath, ExternalTargetAliasRoot, StringComparison.OrdinalIgnoreCase) ||
               normalizedRelativePath.StartsWith(ExternalTargetAliasRoot + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingDirectorySeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith(Path.AltDirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
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

    private static string ResolveMissingUpstreamArtifactInputSummary(DispatchCandidate candidate)
    {
        var missingInputs = candidate.ArtifactInputs
            .Where(item => item.Artifacts.Count == 0)
            .Select(item =>
                $"Upstream step '{item.SourceStepTitle}' must provide required artifact '{item.ExpectedArtifactTitle}'.")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
        return missingInputs.Count == 0
            ? string.Empty
            : string.Join(Environment.NewLine, missingInputs);
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
               relativePath.Contains(expectedSlug, StringComparison.OrdinalIgnoreCase) ||
               MatchesExpectedArtifactByTitleTokens(expectedArtifact.Title, relativePath, displayName);
    }

    private static bool MatchesExpectedArtifactByTitleTokens(
        string expectedTitle,
        string relativePath,
        string displayName)
    {
        var expectedTokens = TokenizeArtifactComparisonText(expectedTitle)
            .Where(token => !ArtifactTitleNoiseTokens.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (expectedTokens.Count < 2)
        {
            return false;
        }

        var observedTokens = TokenizeArtifactComparisonText(relativePath)
            .Concat(TokenizeArtifactComparisonText(displayName))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        if (observedTokens.Count == 0)
        {
            return false;
        }

        var matchedTokenCount = expectedTokens.Count(observedTokens.Contains);
        return matchedTokenCount >= 2;
    }

    private static IReadOnlyList<string> TokenizeArtifactComparisonText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var slug = FileSafeSlugBuilder.Build(value);
        return slug
            .Split(['-', '/', '.', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeArtifactComparisonToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }

    private static string NormalizeArtifactComparisonToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 3 &&
            normalized.EndsWith('s') &&
            !normalized.EndsWith("ss", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }

    private static bool TryResolveProcessMockArtifactProjection(
        string? serializedSessionStateJson,
        out ProcessMockArtifactProjection projection)
    {
        var projections = ResolveProcessMockArtifactProjections(serializedSessionStateJson);
        projection = projections.FirstOrDefault();
        return projections.Count > 0;
    }

    private static IReadOnlyList<ProcessMockArtifactProjection> ResolveProcessMockArtifactProjections(
        string? serializedSessionStateJson)
    {
        if (string.IsNullOrWhiteSpace(serializedSessionStateJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(serializedSessionStateJson);
            var root = document.RootElement;
            if (!root.TryGetProperty(ProcessMockSessionFlagPropertyName, out var processMockFlag) ||
                processMockFlag.ValueKind != JsonValueKind.True ||
                !TryGetStringProperty(root, ProcessMockRoleKeyPropertyName, out var roleKey) ||
                !TryGetStringProperty(root, ProcessMockArtifactRootPropertyName, out var artifactRoot))
            {
                return [];
            }

            var normalizedRoot = WorkspaceScopeDescriptor.NormalizeRelativePath(artifactRoot);
            if (string.IsNullOrWhiteSpace(normalizedRoot))
            {
                return [];
            }

            var branchOutcomeKey = TryGetStringProperty(root, ProcessMockBranchOutcomeKeyPropertyName, out var resolvedBranchOutcomeKey)
                ? resolvedBranchOutcomeKey
                : null;
            var projections = new List<ProcessMockArtifactProjection>();
            if (root.TryGetProperty("artifacts", out var artifactsElement) &&
                artifactsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var artifactElement in artifactsElement.EnumerateArray())
                {
                    if ((!TryGetStringProperty(artifactElement, "relativePath", out var relativePath) &&
                         !TryGetStringProperty(artifactElement, "RelativePath", out relativePath)) ||
                        (!TryGetStringProperty(artifactElement, "contentSignalText", out var contentSignalText) &&
                         !TryGetStringProperty(artifactElement, "ContentSignalText", out contentSignalText)))
                    {
                        continue;
                    }

                    projections.Add(new ProcessMockArtifactProjection(
                        roleKey.Trim(),
                        branchOutcomeKey,
                        WorkspaceScopeDescriptor.NormalizeRelativePath(relativePath),
                        contentSignalText));
                }
            }

            if (projections.Count > 0)
            {
                return projections;
            }

            if (!TryResolveProcessMockArtifactFile(roleKey, branchOutcomeKey, out var fileName, out var fallbackContentSignalText))
            {
                return [];
            }

            return
            [
                new ProcessMockArtifactProjection(
                    roleKey.Trim(),
                    branchOutcomeKey,
                    WorkspaceScopeDescriptor.NormalizeRelativePath($"{normalizedRoot.TrimEnd('/')}/{fileName}"),
                    fallbackContentSignalText)
            ];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool TryResolveProcessMockArtifactFile(
        string roleKey,
        string? branchOutcomeKey,
        out string fileName,
        out string contentSignalText)
    {
        var normalizedRoleKey = roleKey.Trim().ToLowerInvariant();
        var normalizedBranchOutcomeKey = branchOutcomeKey?.Trim().ToLowerInvariant() ?? string.Empty;
        (fileName, contentSignalText) = (normalizedRoleKey, normalizedBranchOutcomeKey) switch
        {
            (ProcessMockProductOwnerRoleKey, _) => ("01-scope.md", "calculator scope acceptance criteria arithmetic divide zero"),
            (ProcessMockArchitectRoleKey, _) => ("02-architecture.md", "calculator architecture boundary implementation qa expectations"),
            (ProcessMockDeveloperRoleKey, _) => ("03-implementation.md", "calculator first implementation deliverable deterministic defect"),
            (ProcessMockQaRoleKey, ProcessMockBranchRepairsRequired) => ("04-qa-finding.md", "calculator qa rejection finding repair branch reason"),
            (ProcessMockRepairDeveloperRoleKey, _) => ("05-repair.md", "calculator repair implementation divide zero fix"),
            (ProcessMockQaRoleKey, ProcessMockBranchApproved) => ("06-qa-approval.md", "calculator qa approval repaired implementation release"),
            (ProcessMockReleaseManagerRoleKey, _) => ("07-release-notes.md", "calculator release notes qa approval repair evidence"),
            _ => (string.Empty, string.Empty)
        };

        return !string.IsNullOrWhiteSpace(fileName);
    }

    private static bool ProcessMockArtifactMatchesExpectation(
        DispatchArtifactExpectation expectedArtifact,
        ProcessMockArtifactProjection projection)
    {
        var observedTokens = TokenizeArtifactContentSignalText($"{projection.RelativePath} {projection.ContentSignalText}")
            .ToHashSet(StringComparer.Ordinal);
        var titleTokens = TokenizeArtifactContentSignalText(expectedArtifact.Title)
            .ToList();
        if (observedTokens.Count == 0 || titleTokens.Count == 0)
        {
            return false;
        }

        return titleTokens.All(observedTokens.Contains);
    }

    private static bool CanSatisfyConcreteImplementationProofWithProcessMock(
        DispatchCandidate candidate,
        ProcessMockArtifactProjection projection)
    {
        return RequiresConcreteImplementationProof(candidate) &&
               IsProcessMockImplementationRole(projection.RoleKey) &&
               ProcessMockProjectionMatchesRequiredArtifact(candidate, projection);
    }

    private static bool IsProcessMockImplementationRole(string roleKey)
    {
        var normalizedRoleKey = roleKey.Trim().ToLowerInvariant();
        return normalizedRoleKey is ProcessMockDeveloperRoleKey or ProcessMockRepairDeveloperRoleKey;
    }

    private static bool ProcessMockProjectionMatchesRequiredArtifact(
        DispatchCandidate candidate,
        ProcessMockArtifactProjection projection)
    {
        return candidate.ExpectedArtifacts
            .Where(item => item.IsRequired)
            .Any(item => ProcessMockArtifactMatchesExpectation(item, projection));
    }

    private static bool TryGetStringProperty(
        JsonElement root,
        string propertyName,
        out string value)
    {
        if (root.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            value = property.GetString()?.Trim() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        value = string.Empty;
        return false;
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
            or ProcessArtifactKind.Transcript ||
               IsPathlessResponseProjectableDeliverable(expectedArtifact) ||
               IsPathlessResponseProjectableEvidence(expectedArtifact);
    }

    private static bool IsPathlessResponseProjectableDeliverable(DispatchArtifactExpectation expectedArtifact)
    {
        if (expectedArtifact.ArtifactKind != ProcessArtifactKind.Deliverable)
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(expectedArtifact.Title).ToLowerInvariant();
        var normalizedValidation = CollapsePromptWhitespace(expectedArtifact.ValidationRequirementSummary).ToLowerInvariant();
        return normalizedTitle.Contains("change set", StringComparison.Ordinal) ||
               normalizedValidation.Contains("change set", StringComparison.Ordinal);
    }

    private static bool IsPathlessResponseProjectableEvidence(DispatchArtifactExpectation expectedArtifact)
    {
        if (expectedArtifact.ArtifactKind != ProcessArtifactKind.Evidence)
        {
            return false;
        }

        var normalizedTitle = CollapsePromptWhitespace(expectedArtifact.Title).ToLowerInvariant();
        var normalizedValidation = CollapsePromptWhitespace(expectedArtifact.ValidationRequirementSummary).ToLowerInvariant();
        return normalizedTitle.Contains("note", StringComparison.Ordinal) ||
               normalizedTitle.Contains("review", StringComparison.Ordinal) ||
               normalizedValidation.Contains("accepted issues", StringComparison.Ordinal) ||
               normalizedValidation.Contains("rejected concerns", StringComparison.Ordinal) ||
               normalizedValidation.Contains("residual risk", StringComparison.Ordinal);
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

        var normalizedResponse = CollapsePromptWhitespace(responseText);
        if (!string.IsNullOrWhiteSpace(normalizedResponse) &&
            !string.Equals(
                normalizedResponse,
                "The provider completed without returning text.",
                StringComparison.OrdinalIgnoreCase))
        {
            return TrimForPrompt(normalizedResponse, 420);
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
               extension.Equals(".slnx", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".html", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".css", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".ts", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".props", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".targets", StringComparison.OrdinalIgnoreCase) ||
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
        string ManualRecoveryDirective,
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

    private sealed record MisplacedTestProjectCleanupTarget(
        string HostProjectPath,
        string NestedTestDirectoryPath);

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

    private sealed record ProcessAutomationDatabaseRequirementFailure(string Message);

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

    private readonly record struct ProcessMockArtifactProjection(
        string RoleKey,
        string? BranchOutcomeKey,
        string RelativePath,
        string ContentSignalText);

    private sealed record SessionToolCall(string ToolName, string OutputFileName);

    private sealed record SessionFileContent(string Path, string Content);
}
