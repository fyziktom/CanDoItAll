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

public sealed partial class ProcessRuntimeReadQueryService : IProcessRuntimeReadQueryService
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
}
