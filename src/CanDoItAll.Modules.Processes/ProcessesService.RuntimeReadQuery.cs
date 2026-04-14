using CanDoItAll.Modules.Collaboration;
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

    Task<IReadOnlyList<ProcessStepRunViewModel>> ListStepRunsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken);

    Task<ProcessWorkspaceRunDetails> GetRunDetailsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken);

    Task<ProcessAnalyticsSummary> GetAnalyticsAsync(
        AppDbContext dbContext,
        Guid? definitionId,
        Guid? projectId,
        CancellationToken cancellationToken);
}

public sealed class ProcessRuntimeReadQueryService : IProcessRuntimeReadQueryService
{
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

        var runs = (await runsQuery
            .Select(
                run => new ProcessRunListProjection(
                    run.Id,
                    run.ProcessDefinitionId,
                    run.ProcessDefinitionVersionId,
                    run.ProjectId,
                    run.Name,
                    run.Status,
                    run.OperatingMode,
                    run.EstimatedCost,
                    run.ActualCost,
                    run.UpdatedAtUtc))
            .ToListAsync(cancellationToken))
            .OrderByDescending(run => run.UpdatedAtUtc)
            .ToList();
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
                        run.ProjectId,
                        run.Name,
                        run.Status,
                        run.OperatingMode,
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
        var artifactOutputsByStepId = (await dbContext.Set<ProcessArtifactExpectation>()
                .AsNoTracking()
                .Where(item => stepDefinitionIds.Contains(item.StepDefinitionId) && !string.IsNullOrWhiteSpace(item.Title))
                .OrderBy(item => item.ArtifactKind)
                .ThenBy(item => item.Title)
                .ThenBy(item => item.Id)
                .Select(
                    item => new ProcessArtifactOutputProjection(
                        item.StepDefinitionId,
                        new ProcessStepRunArtifactPortViewModel(item.Id, item.Title, item.IsRequired)))
                .ToListAsync(cancellationToken))
            .GroupBy(item => item.StepDefinitionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ProcessStepRunArtifactPortViewModel>)group
                    .Select(item => item.ArtifactOutput)
                    .ToList());
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
                            : artifactOutputsByStepId.GetValueOrDefault(stepDefinition.Id) ?? []
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
        var assignments = await ListAssignmentsAsync(dbContext, runId, cancellationToken);
        var workBriefs = await ListWorkBriefsAsync(dbContext, runId, cancellationToken);
        var conformanceObservations = await ListConformanceObservationsAsync(dbContext, runId, cancellationToken);
        var directMessageThreads = await ListDirectMessageThreadsAsync(dbContext, runId, cancellationToken);

        return new ProcessWorkspaceRunDetails(
            stepRuns,
            decisions,
            artifacts,
            assignments,
            workBriefs,
            conformanceObservations,
            directMessageThreads);
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

        var runs = await runsQuery
            .Select(
                run => new ProcessAnalyticsRunProjection(
                    run.Id,
                    run.Status,
                    run.EstimatedCost,
                    run.ActualCost))
            .ToListAsync(cancellationToken);
        var runIds = runs.Select(run => run.Id).ToList();
        var stepMetrics = runIds.Count == 0
            ? []
            : await dbContext.Set<ProcessStepRun>()
                .AsNoTracking()
                .Where(stepRun => runIds.Contains(stepRun.ProcessRunId))
                .Select(
                    stepRun => new ProcessStepAnalyticsProjection(
                        stepRun.WaitMinutes,
                        stepRun.TouchMinutes,
                        stepRun.BlockedMinutes,
                        stepRun.CapabilityGapSeverity))
                .ToListAsync(cancellationToken);
        var conformanceFlags = runIds.Count == 0
            ? []
            : await dbContext.Set<ProcessConformanceObservation>()
                .AsNoTracking()
                .Where(item => runIds.Contains(item.ProcessRunId))
                .Select(item => item.IsSafeNonAction)
                .ToListAsync(cancellationToken);
        var improvementQuery = dbContext.Set<ProcessImprovementCandidate>()
            .AsNoTracking()
            .AsQueryable();
        if (definitionId.HasValue)
        {
            improvementQuery = improvementQuery.Where(item => item.ProcessDefinitionId == definitionId.Value);
        }

        var improvementCount = await improvementQuery.CountAsync(cancellationToken);

        return new ProcessAnalyticsSummary(
            runs.Count,
            runs.Count(run => run.Status == ProcessRunStatus.Active),
            runs.Count(run => run.Status == ProcessRunStatus.Completed),
            runs.Count(run => run.Status == ProcessRunStatus.Blocked),
            stepMetrics.Count(item => item.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None),
            improvementCount,
            conformanceFlags.Count,
            conformanceFlags.Count(item => item),
            Average(stepMetrics.Select(item => item.WaitMinutes + item.TouchMinutes + item.BlockedMinutes)),
            Average(stepMetrics.Select(item => item.WaitMinutes)),
            Average(stepMetrics.Select(item => item.BlockedMinutes)),
            runs.Sum(run => run.EstimatedCost),
            runs.Sum(run => run.ActualCost));
    }

    private static IReadOnlyList<ProcessStepDependencyViewModel> BuildRuntimeDependencies(
        ProcessStepDefinition stepDefinition,
        IReadOnlyDictionary<Guid, List<ProcessStepDependencyDefinition>> dependenciesByStepId)
    {
        return ProcessStepDependencyCollection.BuildRuntimeDependencies(stepDefinition.Id, dependenciesByStepId);
    }

    private static IReadOnlyList<ProcessStepRunResponsibilityPortViewModel> BuildRuntimeResponsibilityPorts(
        Guid stepDefinitionId,
        IReadOnlyDictionary<Guid, List<ProcessStepRoleAssignmentRequirement>> assignmentsByStepId)
    {
        if (!assignmentsByStepId.TryGetValue(stepDefinitionId, out var assignments) || assignments.Count == 0)
        {
            return [];
        }

        var orderedKinds = new[]
        {
            ProcessResponsibilityKind.Responsible,
            ProcessResponsibilityKind.Reviewer,
            ProcessResponsibilityKind.Approver,
            ProcessResponsibilityKind.Backup
        };

        return orderedKinds
            .Select(
                responsibilityKind =>
                {
                    var matchingAssignments = assignments
                        .Where(item => item.ResponsibilityKind == responsibilityKind)
                        .ToList();
                    return new ProcessStepRunResponsibilityPortViewModel(
                        responsibilityKind,
                        matchingAssignments.Any(item => item.IsRequired),
                        matchingAssignments.Count);
                })
            .Where(item => item.AssignmentCount > 0)
            .ToList();
    }

    private static int Average(IEnumerable<int> values)
    {
        var materialized = values.ToList();
        return materialized.Count == 0
            ? 0
            : (int)Math.Round(materialized.Average(), MidpointRounding.AwayFromZero);
    }

    private static async Task<IReadOnlyList<ProcessDecisionViewModel>> ListDecisionRecordsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessDecisionRecord>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessDecisionViewModel(
                item.Id,
                item.DecisionKind,
                item.Outcome,
                item.Title,
                item.Reason,
                item.BranchOutcomeTitle,
                item.DecidedBy,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessArtifactViewModel>> ListArtifactsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessArtifactViewModel(
                item.Id,
                item.ArtifactKind,
                item.Title,
                item.TrustStatus,
                item.SensitivityLevel,
                item.ProvenanceSummary,
                item.AllowedFutureUsageSummary,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessRunAssignmentViewModel>> ListAssignmentsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<ProcessRunAssignment>()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.DisplayName)
            .Select(item => new ProcessRunAssignmentViewModel(
                item.Id,
                item.RoleRequirementId,
                item.StepDefinitionId,
                item.PartyId,
                item.DisplayName,
                item.ExecutorKind,
                item.BindingReason,
                item.SourceRegistryKey,
                item.SnapshotSummary,
                item.IsFallback,
                item.IsCapabilityGap,
                item.AllowsDirectMessaging))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<ProcessWorkBriefViewModel>> ListWorkBriefsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessWorkBrief>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessWorkBriefViewModel(
                item.Id,
                item.StepRunId,
                item.Title,
                item.WorkBriefText,
                item.HandoffSummary,
                item.AssignmentReason,
                item.ExpectedOutcome,
                item.EvidenceExpectationSummary,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessConformanceObservationViewModel>> ListConformanceObservationsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<ProcessConformanceObservation>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessConformanceObservationViewModel(
                item.Id,
                item.StepRunId,
                item.Severity,
                item.Category,
                item.Observation,
                item.DeviationReason,
                item.IsSafeNonAction,
                item.ContainsSensitiveAssessment,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    private static async Task<IReadOnlyList<ProcessDirectMessageThreadViewModel>> ListDirectMessageThreadsAsync(
        AppDbContext dbContext,
        Guid runId,
        CancellationToken cancellationToken)
    {
        var threads = await dbContext.Set<CollaborationThreadRecord>()
            .Where(item => item.ContextKind == CollaborationContextKind.ProcessRun && item.ContextId == runId)
            .Select(item => new ProcessDirectMessageThreadProjection(
                item.Id,
                item.Subject,
                item.LastActivityAtUtc))
            .ToListAsync(cancellationToken);
        if (threads.Count == 0)
        {
            return [];
        }

        var threadIds = threads
            .Select(item => item.ThreadId)
            .ToArray();
        var inboxItems = await dbContext.Set<CollaborationInboxItemRecord>()
            .Where(item => threadIds.Contains(item.ThreadId))
            .Select(item => new ProcessDirectMessageInboxProjection(
                item.ThreadId,
                item.Route,
                item.UnreadCount))
            .ToListAsync(cancellationToken);
        var participants = await dbContext.Set<CollaborationParticipantRecord>()
            .Where(item => threadIds.Contains(item.ThreadId) && item.ParticipantKind == CollaborationParticipantKind.Role)
            .Select(item => new ProcessDirectMessageParticipantProjection(
                item.ThreadId,
                item.DisplayName))
            .ToListAsync(cancellationToken);
        var messages = await dbContext.Set<CollaborationMessageRecord>()
            .Where(item => threadIds.Contains(item.ThreadId))
            .Select(item => new ProcessDirectMessageMessageProjection(
                item.ThreadId,
                item.Id,
                item.Kind,
                item.AuthorName,
                item.Body,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        var inboxByThreadId = inboxItems.ToDictionary(item => item.ThreadId);
        var participantsByThreadId = participants
            .GroupBy(item => item.ThreadId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => item.DisplayName)
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                    .ToList());
        var messagesByThreadId = messages
            .GroupBy(item => item.ThreadId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(item => new ProcessDirectMessageEntryViewModel(
                        item.MessageId,
                        item.MessageKind,
                        item.AuthorName,
                        item.Body,
                        item.CreatedAtUtc))
                    .ToList());

        return threads
            .OrderByDescending(item => item.LastActivityAtUtc)
            .Where(item => participantsByThreadId.ContainsKey(item.ThreadId))
            .Select(item =>
            {
                var roleLabels = participantsByThreadId[item.ThreadId];
                var threadMessages = (messagesByThreadId.GetValueOrDefault(item.ThreadId) ?? [])
                    .OrderBy(message => message.CreatedAtUtc)
                    .ToList();
                inboxByThreadId.TryGetValue(item.ThreadId, out var inbox);
                return new ProcessDirectMessageThreadViewModel(
                    item.ThreadId,
                    item.Subject,
                    inbox?.Route ?? string.Empty,
                    roleLabels.Count == 0 ? "Process roles" : string.Join(" / ", roleLabels),
                    threadMessages.Count,
                    inbox?.UnreadCount ?? 0,
                    item.LastActivityAtUtc,
                    threadMessages);
            })
            .ToList();
    }

    private sealed record ProcessRunListProjection(
        Guid Id,
        Guid ProcessDefinitionId,
        Guid ProcessDefinitionVersionId,
        Guid? ProjectId,
        string Name,
        ProcessRunStatus Status,
        ProcessOperatingMode OperatingMode,
        decimal EstimatedCost,
        decimal ActualCost,
        DateTimeOffset UpdatedAtUtc);

    private sealed record ProcessRunStepSummaryProjection(
        Guid ProcessRunId,
        int CompletedCount,
        int TotalCount,
        int BlockedCount,
        int CapabilityGapCount)
    {
        public static ProcessRunStepSummaryProjection Empty(Guid runId)
        {
            return new ProcessRunStepSummaryProjection(runId, 0, 0, 0, 0);
        }
    }

    private sealed record ProcessArtifactOutputProjection(
        Guid StepDefinitionId,
        ProcessStepRunArtifactPortViewModel ArtifactOutput);

    private sealed record ProcessStepArtifactInputCountProjection(Guid StepDefinitionId, int Count);

    private sealed record ProcessBranchOutcomeProjection(
        Guid StepDefinitionId,
        ProcessStepBranchOutcomeOptionViewModel BranchOutcome);

    private sealed record ProcessAnalyticsRunProjection(
        Guid Id,
        ProcessRunStatus Status,
        decimal EstimatedCost,
        decimal ActualCost);

    private sealed record ProcessStepAnalyticsProjection(
        int WaitMinutes,
        int TouchMinutes,
        int BlockedMinutes,
        ProcessCapabilityGapSeverity CapabilityGapSeverity);

    private sealed record ProcessDirectMessageThreadProjection(
        Guid ThreadId,
        string Subject,
        DateTimeOffset LastActivityAtUtc);

    private sealed record ProcessDirectMessageInboxProjection(
        Guid ThreadId,
        string Route,
        int UnreadCount);

    private sealed record ProcessDirectMessageParticipantProjection(
        Guid ThreadId,
        string DisplayName);

    private sealed record ProcessDirectMessageMessageProjection(
        Guid ThreadId,
        Guid MessageId,
        CollaborationMessageKind MessageKind,
        string AuthorName,
        string Body,
        DateTimeOffset CreatedAtUtc);
}
