using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public interface IProcessRuntimeReadQueryService
{
    Task<IReadOnlyList<ProcessRunListItem>> ListRunsAsync(
        AppDbContext dbContext,
        Guid? definitionId,
        Guid? projectId,
        CancellationToken cancellationToken);

    Task<ProcessRunListItem?> GetRunAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessStepRunViewModel>> ListStepRunsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken);

    Task<ProcessWorkspaceRunDetails> GetRunDetailsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProcessRuntimeInvariantDiagnosticViewModel>> ListRuntimeInvariantDiagnosticsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, ProcessActiveRunHealthMetrics>> GetActiveRunHealthMetricsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken);

    Task<ProcessAnalyticsSummary> GetAnalyticsAsync(
        AppDbContext dbContext,
        Guid? definitionId,
        Guid? projectId,
        CancellationToken cancellationToken);

    Task<ProcessAnalyticsSummary> GetAnalyticsForDefinitionsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> definitionIds,
        Guid? projectId,
        CancellationToken cancellationToken);
}

public sealed partial class ProcessRuntimeReadQueryService(
    IWorkflowCatalogService? workflowCatalog = null,
    IWorkflowRunStore? workflowRuns = null) : IProcessRuntimeReadQueryService
{
    private readonly IWorkflowCatalogService? workflowCatalog = workflowCatalog;
    private readonly IWorkflowRunStore? workflowRuns = workflowRuns;

    public async Task<IReadOnlyList<ProcessRunListItem>> ListRunsAsync(
        AppDbContext dbContext,
        Guid? definitionId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var runsQuery = dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .AsQueryable();
        if (definitionId.HasValue)
        {
            runsQuery = runsQuery.Where(run => run.ProcessDefinitionId == definitionId.Value);
        }

        if (projectId.HasValue)
        {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId.Value);
        }

        return await LoadRunListAsync(dbContext, runsQuery, cancellationToken);
    }

    public async Task<ProcessRunListItem?> GetRunAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var runs = await LoadRunListAsync(
            dbContext,
            dbContext.Set<ProcessRun>()
                .AsNoTracking()
                .Where(run => run.Id == runId),
            cancellationToken);

        return runs.SingleOrDefault();
    }

    public async Task<IReadOnlyList<ProcessStepRunViewModel>> ListStepRunsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var stepRuns = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        if (stepRuns.Count == 0)
        {
            return [];
        }

        var stepRunIds = stepRuns
            .Select(item => item.Id)
            .ToList();
        var stepDefinitionIds = stepRuns
            .Select(item => item.StepDefinitionId)
            .Distinct()
            .ToList();
        var stepDefinitions = await dbContext.Set<ProcessStepDefinition>()
            .AsNoTracking()
            .Where(item => stepDefinitionIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var stepDependenciesByStepId = (await dbContext.Set<ProcessStepDependencyDefinition>()
                .AsNoTracking()
                .Where(item => stepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var stepRoleAssignmentsByStepId = (await dbContext.Set<ProcessStepRoleAssignmentRequirement>()
                .AsNoTracking()
                .Where(item => stepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.FallbackOrder)
                .ThenBy(item => item.ResponsibilityKind)
                .ThenBy(item => item.RoleRequirementId)
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.ToList());
        var artifactExpectations = await dbContext.Set<ProcessArtifactExpectation>()
                .AsNoTracking()
                .Where(item => stepDefinitionIds.Contains(item.StepDefinitionId) && !string.IsNullOrWhiteSpace(item.Title))
                .OrderBy(item => item.ArtifactKind)
                .ThenBy(item => item.Title)
                .ThenBy(item => item.Id)
                .ToListAsync(cancellationToken);
        var artifactOutputsByStepId = artifactExpectations
            .Select(
                item => new ProcessArtifactOutputProjection(
                    item.StepDefinitionId,
                    new ProcessStepRunArtifactPortViewModel(item.Id, item.Title, item.IsRequired)))
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepRunArtifactPortViewModel>)group
                    .Select(item => item.ArtifactOutput)
                    .ToList());
        var artifactExpectationsByStepId = artifactExpectations
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Title).ThenBy(item => item.Id).ToList());
        var artifactRecordsByStepRunId = (await dbContext.Set<ProcessArtifactRecord>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == runId && item.StepRunId.HasValue && stepRunIds.Contains(item.StepRunId.Value))
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.StepRunId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAtUtc).ToList());
        var manualRecoveryEvents = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessRunId == runId &&
                item.StepRunId.HasValue &&
                stepRunIds.Contains(item.StepRunId.Value) &&
                item.EventType == ProcessRuntimeEventTypes.ManualAgentStepRerun)
            .ToListAsync(cancellationToken);
        var manualRecoveryDirectivesByStepRunId = manualRecoveryEvents
            .OrderByDescending(item => item.OccurredAtUtc)
            .GroupBy(item => item.StepRunId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.First().Description);
        var artifactInputCountsByStepId = await dbContext.Set<ProcessStepArtifactInputDefinition>()
            .AsNoTracking()
            .Where(item => stepDefinitionIds.Contains(item.StepDefinitionId))
            .GroupBy(item => item.StepDefinitionId)
            .Select(group => new ProcessStepArtifactInputCountProjection(group.Key, group.Count()))
            .ToDictionaryAsync(item => item.StepDefinitionId, item => item.Count, cancellationToken);
        var branchOutcomesByStepId = (await dbContext.Set<ProcessStepBranchOutcomeDefinition>()
                .AsNoTracking()
                .Where(item => stepDefinitionIds.Contains(item.StepDefinitionId))
                .OrderBy(item => item.DisplayOrder)
                .Select(
                    item => new ProcessBranchOutcomeProjection(
                        item.StepDefinitionId,
                        new ProcessStepBranchOutcomeOptionViewModel(item.Id, item.Title, item.Description)))
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepBranchOutcomeOptionViewModel>)group
                    .Select(item => item.BranchOutcome)
                    .ToList());
        var roleRequirementIds = stepRoleAssignmentsByStepId.Values
            .SelectMany(item => item)
            .Select(item => item.RoleRequirementId)
            .Concat(stepDefinitions
                .Where(item => item.DecisionRoleRequirementId.HasValue)
                .Select(item => item.DecisionRoleRequirementId!.Value))
            .Distinct()
            .ToList();
        var roleTitlesById = roleRequirementIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<ProcessRoleRequirement>()
                .AsNoTracking()
                .Where(item => roleRequirementIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.DisplayName, cancellationToken);
        var stepDefinitionsById = stepDefinitions.ToDictionary(item => item.Id);
        var subprocessRuns = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(item => item.ParentStepRunId.HasValue && stepRunIds.Contains(item.ParentStepRunId.Value))
            .Select(
                item => new ProcessSubprocessRunProjection(
                    item.Id,
                    item.ProcessDefinitionId,
                    item.ProjectId,
                    item.ParentStepRunId!.Value,
                    item.Name,
                    item.Status,
                    item.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
        var subprocessRunIds = subprocessRuns
            .Select(item => item.Id)
            .ToList();
        var subprocessStepRunSummariesByRunId = subprocessRunIds.Count == 0
            ? new Dictionary<Guid, ProcessRunStepSummaryProjection>()
            : await dbContext.Set<ProcessStepRun>()
                .AsNoTracking()
                .Where(item => subprocessRunIds.Contains(item.ProcessRunId))
                .GroupBy(item => item.ProcessRunId)
                .Select(
                    group => new ProcessRunStepSummaryProjection(
                        group.Key,
                        group.Count(stepRun => stepRun.Status == ProcessStepRunStatus.Completed),
                        group.Count(),
                        group.Count(stepRun => stepRun.Status == ProcessStepRunStatus.Blocked),
                        group.Count(stepRun => stepRun.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None)))
                .ToDictionaryAsync(item => item.ProcessRunId, cancellationToken);
        var subprocessRunSummariesByStepRunId = subprocessRuns.ToDictionary(
            item => item.ParentStepRunId,
            item =>
            {
                var summary = subprocessStepRunSummariesByRunId.GetValueOrDefault(item.Id) ??
                    ProcessRunStepSummaryProjection.Empty(item.Id);

                return new ProcessSubprocessRunSummaryViewModel(
                    item.Id,
                    item.ProcessDefinitionId,
                    item.ProjectId,
                    item.Name,
                    item.Status,
                    summary.CompletedCount,
                    summary.TotalCount,
                    summary.BlockedCount,
                    item.UpdatedAtUtc);
            });

        return stepRuns
            .Select(
                item =>
                {
                    stepDefinitionsById.TryGetValue(item.StepDefinitionId, out var stepDefinition);
                    var dependencies = stepDefinition is null
                        ? []
                        : BuildRuntimeDependencies(stepDefinition, stepDependenciesByStepId);
                    var decisionRoleTitle = stepDefinition?.DecisionRoleRequirementId.HasValue == true &&
                                            roleTitlesById.TryGetValue(stepDefinition.DecisionRoleRequirementId.Value, out var resolvedDecisionRoleTitle)
                        ? resolvedDecisionRoleTitle
                        : string.Empty;

                    var artifactLedger = BuildArtifactLedger(
                        item,
                        artifactExpectationsByStepId,
                        artifactRecordsByStepRunId);

                    return new ProcessStepRunViewModel(
                        item.Id,
                        item.StepDefinitionId,
                        stepDefinition?.DecisionRoleRequirementId,
                        item.Sequence,
                        item.Title,
                        item.StepKind,
                        item.Status,
                        item.CurrentExecutorName,
                        item.DecisionSummary,
                        item.BlockedReason,
                        item.RefusalReason,
                        item.SelectedBranchOutcomeId,
                        item.SelectedBranchOutcomeTitle,
                        item.WaitMinutes,
                        item.TouchMinutes,
                        item.BlockedMinutes,
                        item.ReworkCount,
                        item.CapabilityGapSeverity,
                        branchOutcomesByStepId.GetValueOrDefault(item.StepDefinitionId) ?? [])
                    {
                        StepRunConcurrencyToken = item.ConcurrencyToken,
                        Dependencies = dependencies,
                        DecisionRoleTitle = decisionRoleTitle,
                        ResponsibilityPorts = stepDefinition is null
                            ? []
                            : BuildRuntimeResponsibilityPorts(stepDefinition.Id, stepRoleAssignmentsByStepId),
                        ArtifactInputCount = stepDefinition is null
                            ? 0
                            : artifactInputCountsByStepId.GetValueOrDefault(stepDefinition.Id),
                        ArtifactOutputs = stepDefinition is null
                            ? []
                            : artifactOutputsByStepId.GetValueOrDefault(stepDefinition.Id) ?? [],
                        ExceptionSummary = item.ExceptionSummary,
                        ArtifactExpectations = artifactLedger,
                        Health = BuildInitialStepHealth(
                            item,
                            artifactLedger,
                            manualRecoveryDirectivesByStepRunId.GetValueOrDefault(item.Id) ?? string.Empty),
                        SubprocessRun = subprocessRunSummariesByStepRunId.GetValueOrDefault(item.Id)
                    };
                })
            .ToList();
    }

    public async Task<ProcessWorkspaceRunDetails> GetRunDetailsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var stepRuns = await ListStepRunsAsync(dbContext, runId, cancellationToken);
        var decisions = await ListDecisionRecordsAsync(dbContext, runId, cancellationToken);
        var artifacts = await ListArtifactsAsync(dbContext, runId, cancellationToken);
        var outboxRecords = await ListOutboxRecordsAsync(dbContext, runId, cancellationToken);
        var assignments = await ListAssignmentsAsync(dbContext, runId, cancellationToken);
        var workBriefs = await ListWorkBriefsAsync(dbContext, runId, cancellationToken);
        var conformanceObservations = await ListConformanceObservationsAsync(dbContext, runId, cancellationToken);
        var directMessageThreads = await ListDirectMessageThreadsAsync(dbContext, runId, cancellationToken);
        var workflowRunLinks = await EnrichWorkflowRunsAsync(
            await ListWorkflowRunsAsync(dbContext, runId, cancellationToken),
            cancellationToken);
        var invariantDiagnostics = await ListRuntimeInvariantDiagnosticsAsync(dbContext, runId, cancellationToken);

        return new ProcessWorkspaceRunDetails(
            stepRuns,
            decisions,
            artifacts,
            outboxRecords,
            assignments,
            workBriefs,
            conformanceObservations,
            directMessageThreads)
        {
            WorkflowRuns = workflowRunLinks,
            InvariantDiagnostics = invariantDiagnostics
        };
    }

    public async Task<IReadOnlyList<ProcessRuntimeInvariantDiagnosticViewModel>> ListRuntimeInvariantDiagnosticsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var run = await dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null)
        {
            return [];
        }

        var stepRuns = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        var stepDefinitionIds = stepRuns
            .Select(item => item.StepDefinitionId)
            .Distinct()
            .ToList();
        IReadOnlyList<ProcessArtifactExpectation> artifactExpectations = stepDefinitionIds.Count == 0
            ? []
            : await dbContext.Set<ProcessArtifactExpectation>()
                .AsNoTracking()
                .Where(item => stepDefinitionIds.Contains(item.StepDefinitionId))
                .ToListAsync(cancellationToken);
        var artifactRecords = await dbContext.Set<ProcessArtifactRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId == runId)
            .ToListAsync(cancellationToken);
        var journalEntries = await dbContext.Set<ProcessJournalEntry>()
            .AsNoTracking()
            .Where(item =>
                item.ProcessRunId == runId &&
                (item.EventType == ProcessRuntimeEventTypes.RuntimeInvariantViolationRecorded ||
                    item.EventType == ProcessRuntimeEventTypes.ArtifactValidationDiagnostic))
            .ToListAsync(cancellationToken);

        return ProcessRuntimeInvariantAuditor.Audit(
            new ProcessRuntimeInvariantAuditInput(
                run,
                stepRuns,
                artifactExpectations,
                artifactRecords,
                journalEntries));
    }

    private async Task<IReadOnlyList<ProcessWorkflowRunViewModel>> EnrichWorkflowRunsAsync(
        IReadOnlyList<ProcessWorkflowRunViewModel> workflowRunLinks,
        CancellationToken cancellationToken)
    {
        if (workflowRunLinks.Count == 0 || workflowRuns is null)
        {
            return workflowRunLinks;
        }

        IReadOnlyDictionary<(Guid DefinitionId, Guid VersionId), string> workflowNamesByKey =
            workflowCatalog is null
                ? new Dictionary<(Guid DefinitionId, Guid VersionId), string>()
                : (await workflowCatalog.ListDefinitionsAsync(cancellationToken))
                    .ToDictionary(
                        item => (item.Id.Value, item.VersionId.Value),
                        item => item.Name);
        var enriched = new List<ProcessWorkflowRunViewModel>(workflowRunLinks.Count);
        foreach (var workflowRun in workflowRunLinks)
        {
            var runId = new WorkflowRunId(workflowRun.WorkflowRunId);
            var snapshot = await workflowRuns.GetRunAsync(runId, cancellationToken);
            var artifacts = await workflowRuns.ListArtifactsAsync(runId, cancellationToken);
            var pendingRequests = await workflowRuns.ListPendingExternalRequestsAsync(runId, cancellationToken);
            enriched.Add(workflowRun with
            {
                WorkflowName = workflowNamesByKey.GetValueOrDefault(
                    (workflowRun.WorkflowDefinitionId, workflowRun.WorkflowVersionId),
                    workflowRun.WorkflowName),
                State = snapshot?.State ?? workflowRun.State,
                Summary = string.IsNullOrWhiteSpace(snapshot?.Summary)
                    ? workflowRun.Summary
                    : snapshot.Summary,
                ArtifactCount = artifacts.Count,
                PendingRequestCount = pendingRequests.Count,
                UpdatedAtUtc = snapshot?.UpdatedAtUtc ?? workflowRun.UpdatedAtUtc
            });
        }

        return enriched
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, ProcessActiveRunHealthMetrics>> GetActiveRunHealthMetricsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(runIds);

        var normalizedRunIds = runIds
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToList();
        if (normalizedRunIds.Count == 0)
        {
            return new Dictionary<Guid, ProcessActiveRunHealthMetrics>();
        }

        var stepProjections = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => normalizedRunIds.Contains(item.ProcessRunId))
            .Select(item => new ProcessActiveRunStepHealthProjection(
                item.ProcessRunId,
                item.Id,
                item.Title,
                item.Status))
            .ToListAsync(cancellationToken);
        var stepTitlesByRunId = stepProjections
            .GroupBy(item => item.RunId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<Guid, string>)group.ToDictionary(item => item.StepRunId, item => item.Title));
        var blockedOrFailedStepCountsByRunId = stepProjections
            .GroupBy(item => item.RunId)
            .ToDictionary(
                group => group.Key,
                group => group.Count(item => item.Status is ProcessStepRunStatus.Blocked or ProcessStepRunStatus.Failed));

        var now = DateTimeOffset.UtcNow;
        var outboxProjections = await dbContext.Set<ProcessOutboxRecord>()
            .AsNoTracking()
            .Where(item => item.ProcessRunId.HasValue && normalizedRunIds.Contains(item.ProcessRunId.Value))
            .Select(item => new ProcessActiveRunOutboxHealthProjection(
                item.ProcessRunId!.Value,
                item.Status,
                item.LeaseExpiresAtUtc,
                item.NextAttemptAtUtc))
            .ToListAsync(cancellationToken);
        var outboxSummariesByRunId = outboxProjections
            .GroupBy(item => item.RunId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var healthStatuses = group
                        .Select(item => ResolveOutboxHealth(item.Status, item.LeaseExpiresAtUtc, item.NextAttemptAtUtc, now))
                        .ToList();
                    return new ProcessActiveRunOutboxSummaryProjection(
                        healthStatuses.Count(item => item is ProcessOutboxHealthStatus.Pending or ProcessOutboxHealthStatus.Leased or ProcessOutboxHealthStatus.WaitingToRetry),
                        healthStatuses.Count(item => item == ProcessOutboxHealthStatus.DeadLettered));
                });

        var result = new Dictionary<Guid, ProcessActiveRunHealthMetrics>(normalizedRunIds.Count);
        foreach (var runId in normalizedRunIds)
        {
            var outboxSummary = outboxSummariesByRunId.GetValueOrDefault(runId);
            result[runId] = new ProcessActiveRunHealthMetrics(
                runId,
                outboxSummary?.PendingCount ?? 0,
                outboxSummary?.DeadLetteredCount ?? 0,
                blockedOrFailedStepCountsByRunId.GetValueOrDefault(runId),
                stepTitlesByRunId.GetValueOrDefault(runId) ?? new Dictionary<Guid, string>());
        }

        return result;
    }

    public async Task<ProcessAnalyticsSummary> GetAnalyticsAsync(
        AppDbContext dbContext,
        Guid? definitionId,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        var runsQuery = dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .AsQueryable();
        if (definitionId.HasValue)
        {
            runsQuery = runsQuery.Where(run => run.ProcessDefinitionId == definitionId.Value);
        }

        if (projectId.HasValue)
        {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId.Value);
        }

        return await BuildAnalyticsAsync(
            dbContext,
            runsQuery,
            improvementQueryBuilder: improvementQuery =>
            {
                if (definitionId.HasValue)
                {
                    improvementQuery = improvementQuery.Where(item => item.ProcessDefinitionId == definitionId.Value);
                }

                return improvementQuery;
            },
            cancellationToken);
    }

    public async Task<ProcessAnalyticsSummary> GetAnalyticsForDefinitionsAsync(
        AppDbContext dbContext,
        IReadOnlyCollection<Guid> definitionIds,
        Guid? projectId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(definitionIds);

        var normalizedDefinitionIds = definitionIds
            .Where(item => item != Guid.Empty)
            .Distinct()
            .ToList();
        if (normalizedDefinitionIds.Count == 0)
        {
            return EmptyAnalytics;
        }

        var runsQuery = dbContext.Set<ProcessRun>()
            .AsNoTracking()
            .Where(run => normalizedDefinitionIds.Contains(run.ProcessDefinitionId));
        if (projectId.HasValue)
        {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId.Value);
        }

        return await BuildAnalyticsAsync(
            dbContext,
            runsQuery,
            improvementQuery => improvementQuery.Where(item => normalizedDefinitionIds.Contains(item.ProcessDefinitionId)),
            cancellationToken);
    }

    private static readonly ProcessAnalyticsSummary EmptyAnalytics = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    private static async Task<IReadOnlyList<ProcessRunListItem>> LoadRunListAsync(
        AppDbContext dbContext,
        IQueryable<ProcessRun> runsQuery,
        CancellationToken cancellationToken)
    {
        var orderedRunsQuery = runsQuery.OrderByDescending(run => run.UpdatedAtUtc);
        var projectedRunsQuery = orderedRunsQuery
            .Select(
                run => new ProcessRunListProjection(
                    run.Id,
                    run.ProcessDefinitionId,
                    run.ProcessDefinitionVersionId,
                    run.ParentRunId,
                    run.ParentStepRunId,
                    run.RootRunId ?? run.Id,
                    run.HierarchyDepth,
                    run.ProjectId,
                    run.Name,
                    run.Status,
                    run.OperatingMode,
                    run.ManagerAgentId,
                    run.ManagerAgentName,
                    run.EstimatedCost,
                    run.ActualCost,
                    run.UpdatedAtUtc));
        var runs = await projectedRunsQuery.ToListAsync(cancellationToken);
        if (runs.Count == 0)
        {
            return [];
        }

        var runIds = runs.Select(run => run.Id).ToList();
        var stepRunSummariesByRunId = await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(stepRun => runIds.Contains(stepRun.ProcessRunId))
            .GroupBy(stepRun => stepRun.ProcessRunId)
            .Select(
                group => new ProcessRunStepSummaryProjection(
                    group.Key,
                    group.Count(stepRun => stepRun.Status == ProcessStepRunStatus.Completed),
                    group.Count(),
                    group.Count(stepRun => stepRun.Status == ProcessStepRunStatus.Blocked),
                    group.Count(stepRun => stepRun.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None)))
            .ToDictionaryAsync(item => item.ProcessRunId, cancellationToken);

        return runs
            .Select(
                run =>
                {
                    var stepRunSummary = stepRunSummariesByRunId.TryGetValue(run.Id, out var resolvedStepRunSummary)
                        ? resolvedStepRunSummary
                        : ProcessRunStepSummaryProjection.Empty(run.Id);

                    return new ProcessRunListItem(
                        run.Id,
                        run.ProcessDefinitionId,
                        run.ProcessDefinitionVersionId,
                        run.ParentRunId,
                        run.ParentStepRunId,
                        run.RootRunId,
                        run.HierarchyDepth,
                        run.ProjectId,
                        run.Name,
                        run.Status,
                        run.OperatingMode,
                        run.ManagerAgentId,
                        run.ManagerAgentName,
                        stepRunSummary.CompletedCount,
                        stepRunSummary.TotalCount,
                        stepRunSummary.BlockedCount,
                        stepRunSummary.CapabilityGapCount,
                        run.EstimatedCost,
                        run.ActualCost,
                        run.UpdatedAtUtc);
                })
            .ToList();
    }

    private static async Task<ProcessAnalyticsSummary> BuildAnalyticsAsync(
        AppDbContext dbContext,
        IQueryable<ProcessRun> runsQuery,
        Func<IQueryable<ProcessImprovementCandidate>, IQueryable<ProcessImprovementCandidate>> improvementQueryBuilder,
        CancellationToken cancellationToken)
    {
        var runStats = await runsQuery
            .GroupBy(_ => 1)
            .Select(
                group => new ProcessAnalyticsRunStatsProjection(
                    group.Count(),
                    group.Count(run => run.Status == ProcessRunStatus.Active),
                    group.Count(run => run.Status == ProcessRunStatus.Completed),
                    group.Count(run => run.Status == ProcessRunStatus.Blocked),
                    group.Sum(run => run.EstimatedCost),
                    group.Sum(run => run.ActualCost)))
            .SingleOrDefaultAsync(cancellationToken);
        var scopedRunIds = runsQuery.Select(run => run.Id);
        var hasRuns = runStats?.TotalCount > 0;
        var stepStats = hasRuns
            ? await dbContext.Set<ProcessStepRun>()
                .AsNoTracking()
                .Where(stepRun => scopedRunIds.Contains(stepRun.ProcessRunId))
                .GroupBy(_ => 1)
                .Select(
                    group => new ProcessStepAnalyticsStatsProjection(
                        group.Count(),
                        group.Count(stepRun => stepRun.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None),
                        group.Sum(stepRun => stepRun.WaitMinutes + stepRun.TouchMinutes + stepRun.BlockedMinutes),
                        group.Sum(stepRun => stepRun.WaitMinutes),
                        group.Sum(stepRun => stepRun.BlockedMinutes)))
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var conformanceStats = hasRuns
            ? await dbContext.Set<ProcessConformanceObservation>()
                .AsNoTracking()
                .Where(item => scopedRunIds.Contains(item.ProcessRunId))
                .GroupBy(_ => 1)
                .Select(
                    group => new ProcessConformanceStatsProjection(
                        group.Count(),
                        group.Count(item => item.IsSafeNonAction)))
                .SingleOrDefaultAsync(cancellationToken)
            : null;
        var improvementCount = await improvementQueryBuilder(
                dbContext.Set<ProcessImprovementCandidate>()
                    .AsNoTracking()
                    .AsQueryable())
            .CountAsync(cancellationToken);

        return new ProcessAnalyticsSummary(
            runStats?.TotalCount ?? 0,
            runStats?.ActiveCount ?? 0,
            runStats?.CompletedCount ?? 0,
            runStats?.BlockedCount ?? 0,
            stepStats?.CapabilityGapCount ?? 0,
            improvementCount,
            conformanceStats?.TotalCount ?? 0,
            conformanceStats?.SafeNonActionCount ?? 0,
            Average(stepStats?.TotalCycleMinutes ?? 0, stepStats?.StepCount ?? 0),
            Average(stepStats?.TotalWaitMinutes ?? 0, stepStats?.StepCount ?? 0),
            Average(stepStats?.TotalBlockedMinutes ?? 0, stepStats?.StepCount ?? 0),
            runStats?.EstimatedCost ?? 0,
            runStats?.ActualCost ?? 0);
    }
}
