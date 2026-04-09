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
        CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runsQuery = dbContext.Set<ProcessRun>().AsQueryable();
        if (definitionId.HasValue) {
            runsQuery = runsQuery.Where(run => run.ProcessDefinitionId == definitionId.Value);
        }

        if (projectId.HasValue) {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId.Value);
        }

        var runs = await runsQuery.ToListAsync(cancellationToken);
        var stepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(stepRun => runs.Select(run => run.Id).Contains(stepRun.ProcessRunId))
            .ToListAsync(cancellationToken);

        return runs
            .OrderByDescending(run => run.UpdatedAtUtc)
            .Select(run => {
                var runStepRuns = stepRuns.Where(stepRun => stepRun.ProcessRunId == run.Id).ToList();
                return new ProcessRunListItem(
                    run.Id,
                    run.ProcessDefinitionId,
                    run.ProcessDefinitionVersionId,
                    run.ProjectId,
                    run.Name,
                    run.Status,
                    run.OperatingMode,
                    runStepRuns.Count(stepRun => stepRun.Status == ProcessStepRunStatus.Completed),
                    runStepRuns.Count,
                    runStepRuns.Count(stepRun => stepRun.Status == ProcessStepRunStatus.Blocked),
                    runStepRuns.Count(stepRun => stepRun.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None),
                    run.EstimatedCost,
                    run.ActualCost,
                    run.UpdatedAtUtc);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<ProcessStepRunViewModel>> ListStepRunsAsync(Guid runId, CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Set<ProcessStepRun>()
            .Where(item => item.ProcessRunId == runId)
            .OrderBy(item => item.Sequence)
            .Select(item => new ProcessStepRunViewModel(
                item.Id,
                item.StepDefinitionId,
                item.Sequence,
                item.Title,
                item.StepKind,
                item.Status,
                item.CurrentExecutorName,
                item.DecisionSummary,
                item.BlockedReason,
                item.RefusalReason,
                item.WaitMinutes,
                item.TouchMinutes,
                item.BlockedMinutes,
                item.ReworkCount,
                item.CapabilityGapSeverity))
            .ToListAsync(cancellationToken);
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
                item.DisplayName,
                item.ExecutorKind,
                item.BindingReason,
                item.SourceRegistryKey,
                item.SnapshotSummary,
                item.IsFallback,
                item.IsCapabilityGap))
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

    public async Task<ProcessAnalyticsSummary> GetAnalyticsAsync(
        Guid? definitionId = null,
        Guid? projectId = null,
        CancellationToken cancellationToken = default) {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var runsQuery = dbContext.Set<ProcessRun>().AsQueryable();
        if (definitionId.HasValue) {
            runsQuery = runsQuery.Where(run => run.ProcessDefinitionId == definitionId.Value);
        }

        if (projectId.HasValue) {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId.Value);
        }

        var runs = await runsQuery.ToListAsync(cancellationToken);
        var runIds = runs.Select(run => run.Id).ToHashSet();
        var stepRuns = await dbContext.Set<ProcessStepRun>()
            .Where(stepRun => runIds.Contains(stepRun.ProcessRunId))
            .ToListAsync(cancellationToken);
        var conformanceObservations = await dbContext.Set<ProcessConformanceObservation>()
            .Where(item => runIds.Contains(item.ProcessRunId))
            .ToListAsync(cancellationToken);
        var improvementCount = await dbContext.Set<ProcessImprovementCandidate>()
            .CountAsync(item => !definitionId.HasValue || item.ProcessDefinitionId == definitionId.Value, cancellationToken);

        return new ProcessAnalyticsSummary(
            runs.Count,
            runs.Count(run => run.Status == ProcessRunStatus.Active),
            runs.Count(run => run.Status == ProcessRunStatus.Completed),
            runs.Count(run => run.Status == ProcessRunStatus.Blocked),
            stepRuns.Count(stepRun => stepRun.CapabilityGapSeverity != ProcessCapabilityGapSeverity.None),
            improvementCount,
            conformanceObservations.Count,
            conformanceObservations.Count(item => item.IsSafeNonAction),
            Average(stepRuns.Select(item => item.WaitMinutes + item.TouchMinutes + item.BlockedMinutes)),
            Average(stepRuns.Select(item => item.WaitMinutes)),
            Average(stepRuns.Select(item => item.BlockedMinutes)),
            runs.Sum(run => run.EstimatedCost),
            runs.Sum(run => run.ActualCost));
    }

    public async Task<IReadOnlyList<ProjectPartyOption>> ListPartyOptionsAsync(Guid projectId, CancellationToken cancellationToken = default) {
        return await projectPartyIntegrationBridge.ListPartyOptionsAsync(projectId, cancellationToken);
    }
}

