using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryReviewUiService
{
    private static async Task<IReadOnlyList<CognitiveMemoryQualityClusterView>> LoadQualityClustersAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var clustersQuery = dbContext.Set<CognitiveMemoryQualityClusterRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            clustersQuery = clustersQuery.Where(cluster => cluster.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.QualityClusters);
        var orderedClusters = clustersQuery
            .OrderBy(cluster => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.NeedsHumanReview ? 0 : 1)
            .ThenBy(cluster => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory ? 0 : 1)
            .ThenByDescending(cluster => cluster.RiskLevel);
        return await (UsesSqlite(dbContext)
                ? orderedClusters.ThenBy(cluster => cluster.Id)
                : orderedClusters.ThenByDescending(cluster => cluster.UpdatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(cluster => new CognitiveMemoryQualityClusterView(
                new CognitiveMemoryQualityClusterId(cluster.Id),
                cluster.ProjectId,
                cluster.PrimaryKeyFamily,
                cluster.Readiness,
                cluster.AccessLevel,
                cluster.RiskLevel,
                cluster.KeyCount,
                cluster.MemberCount,
                cluster.SourceEvidenceCount,
                cluster.ContradictionCount,
                cluster.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryDreamRunView>> LoadDreamRunsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var runsQuery = dbContext.Set<CognitiveMemoryDreamRunRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.DreamRuns);
        var orderedRuns = runsQuery
            .OrderBy(run => run.Status == CognitiveMemoryRunStatus.Failed ? 0 : 1)
            .ThenBy(run => run.Status == CognitiveMemoryRunStatus.Running ? 0 : 1);
        return await (UsesSqlite(dbContext)
                ? orderedRuns.ThenBy(run => run.Id)
                : orderedRuns.ThenByDescending(run => run.StartedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(run => new CognitiveMemoryDreamRunView(
                new CognitiveMemoryDreamRunId(run.Id),
                run.ProjectId,
                run.Mode,
                run.TriggerKind,
                run.Status,
                run.ClustersConsidered,
                run.AggregateCandidatesCreated,
                run.ApprovedCandidates,
                run.NeedsReviewCandidates,
                run.RejectedCandidates,
                run.EvidenceCoverageRatio,
                run.FailureCode,
                run.FailureMessage,
                run.StartedAtUtc,
                run.CompletedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryAggregateCandidateView>> LoadAggregateCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var candidatesQuery = dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            candidatesQuery = candidatesQuery.Where(candidate => candidate.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.AggregateCandidates);
        var orderedCandidates = candidatesQuery
            .OrderBy(candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview ? 0 : 1)
            .ThenBy(candidate => candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.Proposed ? 0 : 1)
            .ThenByDescending(candidate => candidate.RiskLevel);
        return await (UsesSqlite(dbContext)
                ? orderedCandidates.ThenBy(candidate => candidate.Id)
                : orderedCandidates.ThenByDescending(candidate => candidate.UpdatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(candidate => new CognitiveMemoryAggregateCandidateView(
                new CognitiveMemoryDreamAggregateCandidateId(candidate.Id),
                new CognitiveMemoryDreamRunId(candidate.DreamRunId),
                new CognitiveMemoryQualityClusterId(candidate.ClusterId),
                candidate.ProjectId,
                candidate.Mode,
                candidate.Status,
                candidate.Title,
                candidate.SummaryText,
                candidate.AccessLevel,
                candidate.RiskLevel,
                candidate.ClaimCount,
                candidate.SourceMapCount,
                candidate.ValidationRecordId,
                candidate.ReviewItemId,
                candidate.MemoryRecordId == null ? null : new CognitiveMemoryRecordId(candidate.MemoryRecordId.Value),
                candidate.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemorySynthesizedRecallView>> LoadSynthesizedRecallsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var recallsQuery = dbContext.Set<CognitiveMemorySynthesizedRecallRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            recallsQuery = recallsQuery.Where(recall => recall.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.SynthesizedRecalls);
        return await (UsesSqlite(dbContext)
                ? recallsQuery.OrderBy(recall => recall.Id)
                : recallsQuery.OrderByDescending(recall => recall.CreatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(recall => new CognitiveMemorySynthesizedRecallView(
                new CognitiveMemorySynthesizedRecallId(recall.Id),
                recall.ProjectId,
                recall.RecallTraceId,
                recall.Brief,
                recall.ReferencesShownByDefault,
                recall.StatementCount,
                recall.SourceMapCount,
                recall.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }
}
