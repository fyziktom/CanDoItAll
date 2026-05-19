using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryReviewUiService
{
    private const int ClusterSearchKeyPreviewLimit = 8;
    private const int ClusterSearchMemberPreviewLimit = 6;

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

    private static Task<int> CountClusterSearchResultsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
        => BuildClusterSearchClustersQuery(dbContext, query).CountAsync(cancellationToken);

    private static async Task<IReadOnlyList<CognitiveMemoryClusterSearchResultView>> LoadClusterSearchResultsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.ClusterSearchResults);
        var clustersQuery = BuildClusterSearchClustersQuery(dbContext, query)
            .OrderBy(cluster => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.NeedsHumanReview ? 0 : 1)
            .ThenBy(cluster => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory ? 0 : 1)
            .ThenByDescending(cluster => cluster.RiskLevel);
        var clusters = await (UsesSqlite(dbContext)
                ? clustersQuery.ThenBy(cluster => cluster.Id)
                : clustersQuery.ThenByDescending(cluster => cluster.UpdatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .ToArrayAsync(cancellationToken);
        var clusterIds = clusters
            .Select(cluster => cluster.Id)
            .ToArray();
        var keysByCluster = await LoadClusterSearchKeysAsync(dbContext, clusterIds, cancellationToken);
        var membersByCluster = await LoadClusterSearchMembersAsync(dbContext, clusterIds, cancellationToken);

        return clusters
            .Select(cluster => new CognitiveMemoryClusterSearchResultView(
                new CognitiveMemoryQualityClusterId(cluster.Id),
                cluster.ProjectId,
                cluster.ClusterHash,
                cluster.PrimaryKeyFamily,
                cluster.Readiness,
                cluster.AccessLevel,
                cluster.RiskLevel,
                cluster.KeyCount,
                cluster.MemberCount,
                cluster.SourceEvidenceCount,
                cluster.ContradictionCount,
                cluster.UpdatedAtUtc,
                keysByCluster.TryGetValue(cluster.Id, out var keys) ? keys : [],
                membersByCluster.TryGetValue(cluster.Id, out var members) ? members : []))
            .ToArray();
    }

    private static IQueryable<CognitiveMemoryQualityClusterRecord> BuildClusterSearchClustersQuery(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query)
    {
        var clustersQuery = dbContext.Set<CognitiveMemoryQualityClusterRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } projectId)
        {
            clustersQuery = clustersQuery.Where(cluster => cluster.ProjectId == projectId);
        }

        var filter = query.ClusterSearch;
        if (filter?.Readiness is { } readiness)
        {
            clustersQuery = clustersQuery.Where(cluster => cluster.Readiness == readiness);
        }

        if (filter?.RiskLevel is { } riskLevel)
        {
            clustersQuery = clustersQuery.Where(cluster => cluster.RiskLevel == riskLevel);
        }

        var keysQuery = dbContext.Set<CognitiveMemoryQualityClusterKeyRecord>()
            .AsNoTracking();
        if (query.ProjectId is { } keyProjectId)
        {
            keysQuery = keysQuery.Where(key => key.ProjectId == keyProjectId);
        }

        if (filter?.KeyFamily is { } keyFamily)
        {
            keysQuery = keysQuery.Where(key => key.KeyFamily == keyFamily);
            var familyClusterIds = keysQuery.Select(key => key.ClusterId);
            clustersQuery = clustersQuery.Where(cluster => familyClusterIds.Contains(cluster.Id));
        }

        var searchText = filter?.Text?.Trim();
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return clustersQuery;
        }

        var normalizedSearch = searchText.ToLowerInvariant();
        var matchingKeyClusterIds = keysQuery
            .Where(key => key.Key.ToLower().Contains(normalizedSearch) ||
                          key.DisplayText.ToLower().Contains(normalizedSearch))
            .Select(key => key.ClusterId);

        if (Guid.TryParse(searchText, out var clusterId))
        {
            clustersQuery = clustersQuery.Where(cluster =>
                cluster.Id == clusterId ||
                cluster.ClusterHash.ToLower().Contains(normalizedSearch) ||
                matchingKeyClusterIds.Contains(cluster.Id));
            return clustersQuery;
        }

        return clustersQuery.Where(cluster =>
            cluster.ClusterHash.ToLower().Contains(normalizedSearch) ||
            matchingKeyClusterIds.Contains(cluster.Id));
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CognitiveMemoryClusterSearchKeyView>>> LoadClusterSearchKeysAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> clusterIds,
        CancellationToken cancellationToken)
    {
        var keysByCluster = new Dictionary<Guid, IReadOnlyList<CognitiveMemoryClusterSearchKeyView>>();
        foreach (var clusterId in clusterIds)
        {
            keysByCluster[clusterId] = await dbContext.Set<CognitiveMemoryQualityClusterKeyRecord>()
                .AsNoTracking()
                .Where(key => key.ClusterId == clusterId)
                .OrderBy(key => key.KeyFamily)
                .ThenBy(key => key.Key)
                .Take(ClusterSearchKeyPreviewLimit)
                .Select(key => new CognitiveMemoryClusterSearchKeyView(
                    key.KeyFamily,
                    key.Key,
                    key.DisplayText))
                .ToArrayAsync(cancellationToken);
        }

        return keysByCluster;
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<CognitiveMemoryClusterSearchMemberPreviewView>>> LoadClusterSearchMembersAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> clusterIds,
        CancellationToken cancellationToken)
    {
        var membersByCluster = new Dictionary<Guid, IReadOnlyList<CognitiveMemoryClusterSearchMemberPreviewView>>();
        foreach (var clusterId in clusterIds)
        {
            membersByCluster[clusterId] = await dbContext.Set<CognitiveMemoryQualityClusterMemberRecord>()
                .AsNoTracking()
                .Where(member => member.ClusterId == clusterId)
                .OrderByDescending(member => member.RiskLevel)
                .ThenBy(member => member.MemberKind)
                .ThenBy(member => member.Id)
                .Take(ClusterSearchMemberPreviewLimit)
                .Select(member => new CognitiveMemoryClusterSearchMemberPreviewView(
                    member.MemberKind,
                    member.MemoryRecordId,
                    member.SourceItemId,
                    member.EvidenceAnchorId,
                    member.AccessLevel,
                    member.RiskLevel,
                    member.ValidationState,
                    member.StabilityState))
                .ToArrayAsync(cancellationToken);
        }

        return membersByCluster;
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
