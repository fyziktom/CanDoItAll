using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryReviewUiService
{
    private static async Task<IReadOnlyList<CognitiveMemoryRecallTraceView>> LoadRecallTracesAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var tracesQuery = dbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            tracesQuery = tracesQuery.Where(trace => trace.ProjectId == projectId);
        }

        var traces = (await tracesQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(trace => trace.StartedAtUtc)
            .Take(query.Take)
            .ToArray();
        var traceIds = traces.Select(trace => trace.Id).ToArray();
        var stages = (await dbContext.Set<CognitiveMemoryRecallTraceStageRecord>()
            .AsNoTracking()
            .Where(stage => traceIds.Contains(stage.RecallTraceId))
            .ToListAsync(cancellationToken))
            .OrderBy(stage => stage.StartedAtUtc)
            .ToArray();
        var stagesByTrace = stages
            .GroupBy(stage => stage.RecallTraceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(stage => new CognitiveMemoryRecallStageView(
                        stage.StageKind,
                        stage.ChannelKind,
                        stage.Status,
                        stage.CandidateCount,
                        stage.SelectedCount,
                        stage.ExcludedCount,
                        stage.FailureCode,
                        stage.FailureMessage))
                    .ToArray());
        var candidates = await dbContext.Set<CognitiveMemoryRecallCandidateRecord>()
            .AsNoTracking()
            .Where(candidate => traceIds.Contains(candidate.RecallTraceId))
            .OrderByDescending(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected)
            .ThenByDescending(candidate => candidate.DisplayRankProjection)
            .ToListAsync(cancellationToken);
        var candidatesByTrace = candidates
            .GroupBy(candidate => candidate.RecallTraceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Take(6)
                    .Select(candidate => new CognitiveMemoryRecallCandidateView(
                        candidate.PrimaryChannelKind,
                        candidate.DecisionKind,
                        candidate.ExclusionReasonKind,
                        candidate.Title,
                        candidate.Summary,
                        candidate.Reason,
                        candidate.ScoreBucket,
                        candidate.DisplayRankProjection,
                        candidate.SourceRedacted))
                    .ToArray());
        var sourceRefs = await dbContext.Set<CognitiveMemoryRecallSourceRefRecord>()
            .AsNoTracking()
            .Where(sourceRef => traceIds.Contains(sourceRef.RecallTraceId))
            .OrderByDescending(sourceRef => sourceRef.IncludedInContext)
            .ThenBy(sourceRef => sourceRef.SourceSystem)
            .ToListAsync(cancellationToken);
        var sourceRefsByTrace = sourceRefs
            .GroupBy(sourceRef => sourceRef.RecallTraceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Take(6)
                    .Select(sourceRef => new CognitiveMemoryRecallSourceReferenceView(
                        sourceRef.SourceSystem,
                        sourceRef.Locator,
                        sourceRef.Summary,
                        sourceRef.AccessLevel,
                        sourceRef.RedactionState,
                        sourceRef.IncludedInContext,
                        sourceRef.ExclusionReasonKind))
                    .ToArray());

        return traces
            .Select(trace => new CognitiveMemoryRecallTraceView(
                trace.Id,
                trace.ProjectId,
                trace.RecallMode,
                trace.Outcome,
                trace.IncludedRecordCount,
                trace.ExcludedRecordCount,
                trace.SelectedClaimCount,
                trace.SelectedEvidenceAnchorCount,
                trace.InhibitedCandidateCount,
                trace.LimitingBudget,
                trace.StartedAtUtc,
                trace.CompletedAtUtc,
                stagesByTrace.TryGetValue(trace.Id, out var stages) ? stages : [],
                candidatesByTrace.TryGetValue(trace.Id, out var candidates) ? candidates : [],
                sourceRefsByTrace.TryGetValue(trace.Id, out var sourceRefs) ? sourceRefs : []))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryConsolidationRunView>> LoadConsolidationRunsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var runsQuery = dbContext.Set<CognitiveMemoryConsolidationRunRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId);
        }

        return (await runsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(run => run.StartedAtUtc)
            .Take(query.Take)
            .Select(run => new CognitiveMemoryConsolidationRunView(
                run.Id,
                run.ProjectId,
                run.Mode,
                run.TriggerKind,
                run.Status,
                run.SourceItemsScanned,
                run.CandidatesCreated,
                run.MutationCommandsSubmitted,
                run.ReviewItemsCreated,
                run.ProjectionInvalidations,
                run.FailureCode,
                run.FailureMessage,
                run.StartedAtUtc,
                run.CompletedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryProjectionHealthView>> LoadProjectionHealthAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var projectionsQuery = dbContext.Set<CognitiveMemoryProjectionStateRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            projectionsQuery = projectionsQuery.Where(projection => projection.ProjectId == projectId);
        }

        return (await projectionsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(projection => projection.RebuildRequired)
            .ThenByDescending(projection => projection.Status == CognitiveMemoryProjectionStatus.Failed)
            .ThenByDescending(projection => projection.UpdatedAtUtc)
            .Take(query.Take)
            .Select(projection => new CognitiveMemoryProjectionHealthView(
                new CognitiveMemoryProjectionId(projection.Id),
                projection.ProjectId,
                projection.ProjectionKind,
                projection.Status,
                projection.TargetProvider,
                projection.RebuildRequired,
                projection.FailureCode,
                projection.FailureMessage,
                projection.UpdatedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryProcedureSkillView>> LoadProcedureSkillsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var skillsQuery = dbContext.Set<CognitiveMemoryProcedureSkillRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            skillsQuery = skillsQuery.Where(skill => skill.ProjectId == projectId);
        }

        return (await skillsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(skill => skill.RiskLevel)
            .ThenBy(skill => skill.Maturity)
            .ThenByDescending(skill => skill.UpdatedAtUtc)
            .Take(query.Take)
            .Select(skill => new CognitiveMemoryProcedureSkillView(
                new CognitiveMemoryProcedureSkillId(skill.Id),
                skill.ProjectId,
                skill.Title,
                skill.Maturity,
                skill.RiskLevel,
                skill.ValidationState,
                skill.AccessLevel,
                skill.MaturityBucket,
                skill.DisplayMaturityScore,
                skill.StepCount,
                skill.FailureModeCount,
                skill.ValidationEvidenceCount,
                skill.AutomationBindingCount,
                skill.UpdatedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryReplayJobView>> LoadReplayJobsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var jobsQuery = dbContext.Set<CognitiveMemoryReplayJobRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            jobsQuery = jobsQuery.Where(job => job.ProjectId == projectId);
        }

        return (await jobsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(job => job.State == CognitiveMemoryReplayJobState.NeedsReview)
            .ThenByDescending(job => job.QueuePriority)
            .ThenByDescending(job => job.UpdatedAtUtc)
            .Take(query.Take)
            .Select(job => new CognitiveMemoryReplayJobView(
                job.Id,
                job.ProjectId,
                job.JobKind,
                job.State,
                job.PriorityBucket,
                job.DisplayPriorityProjection,
                job.QueuePriority,
                job.Reason,
                job.FailureCode,
                job.FailureMessage,
                job.UpdatedAtUtc))
            .ToArray();
    }
}
