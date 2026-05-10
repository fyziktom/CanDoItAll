using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Search;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService {
    public async Task<IReadOnlyList<ProcessRunListItem>> ListRunsAsync(
        Guid? definitionId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await runtimeReadQueryService.ListRunsAsync(dbContext, definitionId, projectId, cancellationToken);
    }

    public async Task<ProcessRunListItem?> GetRunAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await runtimeReadQueryService.GetRunAsync(dbContext, runId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessStepRunViewModel>> ListStepRunsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await runtimeReadQueryService.ListStepRunsAsync(dbContext, runId, cancellationToken);
    }

    public async Task<ProcessWorkspaceRunDetails> GetRunDetailsAsync(Guid runId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await runtimeReadQueryService.GetRunDetailsAsync(dbContext, runId, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, ProcessActiveRunHealthMetrics>> GetActiveRunHealthMetricsAsync(
        IReadOnlyCollection<Guid> runIds,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await runtimeReadQueryService.GetActiveRunHealthMetricsAsync(dbContext, runIds, cancellationToken);
    }

    public async Task<IReadOnlyList<ProcessDecisionViewModel>> ListDecisionRecordsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
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

    public async Task<IReadOnlyList<ProcessArtifactViewModel>> ListArtifactsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await dbContext.Set<ProcessArtifactRecord>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessArtifactViewModel(
                item.Id,
                item.StepRunId,
                item.ArtifactExpectationId,
                item.ArtifactKind,
                item.Title,
                item.TrustStatus,
                item.SensitivityLevel,
                item.ProvenanceSummary,
                item.AllowedFutureUsageSummary,
                item.ManagedStoragePath,
                item.ExternalReferenceKey,
                item.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessRunAssignmentViewModel>> ListAssignmentsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessRunAssignment>()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.DisplayName)
            .Select(item => new ProcessRunAssignmentViewModel(
                item.Id,
                item.RoleRequirementId,
                item.StepDefinitionId,
                item.PartyId,
                item.WorkflowDefinitionId,
                item.WorkflowVersionId,
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

    public async Task<IReadOnlyList<ProcessWorkBriefViewModel>> ListWorkBriefsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
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

    public async Task<IReadOnlyList<ProcessConformanceObservationViewModel>> ListConformanceObservationsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
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

    public async Task<IReadOnlyList<ProcessImprovementViewModel>> ListImprovementsAsync(
        Guid? definitionId = null,
        CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = dbContext.Set<ProcessImprovementCandidate>().AsQueryable();
        if (definitionId.HasValue) {
            query = query.Where(item => item.ProcessDefinitionId == definitionId.Value);
        }

        var items = await query
            .Select(item => new ProcessImprovementViewModel(
                item.Id,
                item.Title,
                item.Category,
                item.ProblemSummary,
                item.Status,
                item.IsTrainingOpportunity,
                item.RequiresGovernanceReview))
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<IReadOnlyList<ProcessImprovementViewModel>> ListRunImprovementsAsync(
        Guid runId,
        CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await dbContext.Set<ProcessImprovementCandidate>()
            .Where(item => item.ProcessRunId == runId)
            .Select(item => new ProcessImprovementViewModel(
                item.Id,
                item.Title,
                item.Category,
                item.ProblemSummary,
                item.Status,
                item.IsTrainingOpportunity,
                item.RequiresGovernanceReview))
            .ToListAsync(cancellationToken);
        return items;
    }

    public async Task<ProcessAnalyticsSummary> GetAnalyticsAsync(
        Guid? definitionId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await runtimeReadQueryService.GetAnalyticsAsync(dbContext, definitionId, projectId, cancellationToken);
    }

    public async Task<ProcessAnalyticsSummary> GetAnalyticsForDefinitionsAsync(
        IReadOnlyCollection<Guid> definitionIds,
        Guid? projectId = null,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await runtimeReadQueryService.GetAnalyticsForDefinitionsAsync(dbContext, definitionIds, projectId, cancellationToken);
    }

    public async Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(Guid projectId, CancellationToken cancellationToken = default) {
        return await projectPartyIntegrationBridge.ListPartyOptionsAsync(projectId, cancellationToken);
    }

    public async Task<ProcessLaunchPlanAccessSummary?> GetLaunchPlanAccessSummaryAsync(Guid launchPlanId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessLaunchPlan>()
            .AsNoTracking()
            .Where(item => item.Id == launchPlanId)
            .Select(item => new ProcessLaunchPlanAccessSummary(item.Id, item.ProcessDefinitionId, item.ProjectId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<ProcessStepRunAccessSummary?> GetStepRunAccessSummaryAsync(Guid stepRunId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessStepRun>()
            .AsNoTracking()
            .Where(item => item.Id == stepRunId)
            .Join(
                dbContext.Set<ProcessRun>().AsNoTracking(),
                stepRun => stepRun.ProcessRunId,
                run => run.Id,
                (stepRun, run) => new ProcessStepRunAccessSummary(
                    stepRun.Id,
                    run.Id,
                    run.ProcessDefinitionId,
                    run.ProjectId))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

