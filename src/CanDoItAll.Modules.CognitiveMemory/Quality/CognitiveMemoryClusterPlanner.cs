using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
public sealed class CognitiveMemoryClusterPlanner : ICognitiveMemoryClusterPlanner
{
    private static readonly CognitiveMemoryQualityClusterAlgorithmOptions AlgorithmOptions = CognitiveMemoryQualityAlgorithmOptions.Current.Cluster;
    private static string AlgorithmVersion => AlgorithmOptions.AlgorithmVersion.Value;
    private static int MaxAggregateReadyMemoryRecords => AlgorithmOptions.MaxAggregateReadyMemoryRecords;
    private static int MaxCandidatePairs => AlgorithmOptions.MaxCandidatePairs;
    private static double CompositeEdgeThreshold => AlgorithmOptions.CompositeEdgeThreshold;
    private readonly IDbContextFactory<AppDbContext> dbContextFactory;
    private readonly IClock clock;
    private readonly ICognitiveMemoryClusterKeyExtractor keyExtractor;
    private readonly ICognitiveMemoryCandidatePairSelector candidatePairSelector;

    public CognitiveMemoryClusterPlanner(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IClock clock)
        : this(
            dbContextFactory,
            clock,
            CognitiveMemoryClusterKeyExtractor.Instance,
            CognitiveMemoryCandidatePairSelector.Default)
    {
    }

    internal CognitiveMemoryClusterPlanner(
        IDbContextFactory<AppDbContext> dbContextFactory,
        IClock clock,
        ICognitiveMemoryClusterKeyExtractor keyExtractor,
        ICognitiveMemoryCandidatePairSelector candidatePairSelector)
    {
        this.dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.keyExtractor = keyExtractor ?? throw new ArgumentNullException(nameof(keyExtractor));
        this.candidatePairSelector = candidatePairSelector ?? throw new ArgumentNullException(nameof(candidatePairSelector));
    }

    private static readonly IReadOnlySet<CognitiveMemoryQualityClusterKeyFamily> StrongPrimaryFamilies = new HashSet<CognitiveMemoryQualityClusterKeyFamily>
    {
        CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
        CognitiveMemoryQualityClusterKeyFamily.Entity,
        CognitiveMemoryQualityClusterKeyFamily.TaskIntent,
        CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap,
        CognitiveMemoryQualityClusterKeyFamily.Relation
    };

    private static readonly IReadOnlySet<CognitiveMemoryQualityClusterKeyFamily> SupportingFamilies = new HashSet<CognitiveMemoryQualityClusterKeyFamily>
    {
        CognitiveMemoryQualityClusterKeyFamily.ProjectScope,
        CognitiveMemoryQualityClusterKeyFamily.SourceTopology,
        CognitiveMemoryQualityClusterKeyFamily.Temporal,
        CognitiveMemoryQualityClusterKeyFamily.AccessRisk
    };

    public async ValueTask<CognitiveMemoryClusterPlanningResult> PlanAsync(
        CognitiveMemoryClusterPlanningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        var nowUtc = clock.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var records = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record => request.ProjectId == null || record.ProjectId == request.ProjectId)
            .Where(record => record.ValidationState != CognitiveMemoryValidationState.Rejected)
            .Where(record => record.StabilityState != CognitiveMemoryStabilityState.Deprecated)
            .ToListAsync(cancellationToken);

        records = records
            .Where(record => CognitiveMemoryQualityText.PolicyCanRead(record.AccessLevel, request.PolicyContext))
            .OrderByDescending(record => record.UpdatedAtUtc)
            .ThenBy(record => record.Id)
            .Take(request.MaxRecords)
            .ToList();

        var recordIds = records.Select(record => record.Id).ToArray();
        var support = await CognitiveMemoryQualitySupportLoader.LoadAsync(dbContext, recordIds, cancellationToken);
        var relationRows = await dbContext.Set<CognitiveMemoryRelationRecord>()
            .AsNoTracking()
            .Where(relation => recordIds.Contains(relation.SourceMemoryRecordId) || recordIds.Contains(relation.TargetMemoryRecordId))
            .ToListAsync(cancellationToken);
        var relationKeysByRecordId = BuildRelationKeysByRecordId(relationRows);
        var contradictionPairs = relationRows
            .Where(relation => relation.RelationKind == CognitiveMemoryRelationKind.Contradicts)
            .Select(relation => NormalizePair(relation.SourceMemoryRecordId, relation.TargetMemoryRecordId))
            .ToHashSet(StringComparer.Ordinal);
        var sourceItemCount = support.SourceItemsById.Count;

        var recordEntries = new List<CognitiveMemoryClusterRecordEntry>(records.Count);
        var keyEntries = new List<ClusterKeyEntry>();
        foreach (var record in records)
        {
            var recordSupport = support.ByRecordId.GetValueOrDefault(record.Id) ?? CognitiveMemoryRecordSupport.Empty(record.Id);
            var recordKeys = keyExtractor.CreateKeys(record, recordSupport, relationKeysByRecordId.GetValueOrDefault(record.Id) ?? [], request.KeyFamilies);
            recordEntries.Add(new CognitiveMemoryClusterRecordEntry(record, recordSupport, recordKeys));
            foreach (var key in recordKeys)
            {
                keyEntries.Add(new ClusterKeyEntry(record, recordSupport, key));
            }
        }

        var warnings = new List<string>();
        var clusters = new List<CognitiveMemoryClusterPlan>();
        var compositeClusters = BuildCompositeClusters(
            recordEntries,
            request.MinMembers,
            contradictionPairs,
            candidatePairSelector,
            out var candidatePairCount,
            out var pairBudgetReached);
        if (pairBudgetReached)
        {
            warnings.Add($"Cluster candidate pair budget reached at {MaxCandidatePairs} pair(s); later records were not compared.");
        }

        foreach (var compositeCluster in compositeClusters)
        {
            var members = compositeCluster.Members;
            var primaryKey = SelectPrimaryClusterKey(compositeCluster.Keys);
            if (primaryKey is null)
            {
                continue;
            }

            var memberKeyEntries = members
                .Select(member => new ClusterKeyEntry(member.Record, member.Support, primaryKey with { RecordId = member.Record.Id }))
                .ToArray();
            var clusterKeys = compositeCluster.Keys
                .Select(key => new CognitiveMemoryClusterKey(key.Family, key.Key, key.DisplayText))
                .ToArray();
            var clusterMembers = members
                .SelectMany(member => ToClusterMembers(member.Record, member.Support))
                .ToArray();
            var qualityMetrics = ScoreCluster(primaryKey.Family, primaryKey.Key, memberKeyEntries, compositeCluster.Keys, compositeCluster.Edges, contradictionPairs);
            var readiness = ResolveReadiness(memberKeyEntries, contradictionPairs, qualityMetrics);
            var signalSummary = SummarizeEdgeSignals(compositeCluster.Edges);
            qualityMetrics = qualityMetrics with
            {
                EligibilityReason = string.IsNullOrWhiteSpace(signalSummary)
                    ? qualityMetrics.EligibilityReason
                    : $"{qualityMetrics.EligibilityReason} Edge signals: {signalSummary}"
            };
            var clusterHash = CreateClusterHash(request.ProjectId, primaryKey.Family, primaryKey.Key, members.Select(member => member.Record.Id));
            var cluster = new CognitiveMemoryClusterPlan(
                CognitiveMemoryQualityClusterId.New(),
                request.ProjectId,
                clusterHash,
                primaryKey.Family,
                readiness,
                clusterKeys.Length == 0
                    ? [new CognitiveMemoryClusterKey(primaryKey.Family, primaryKey.Key, primaryKey.DisplayText)]
                    : clusterKeys,
                clusterMembers,
                qualityMetrics,
                ResolveClusterWarnings(readiness, clusterMembers, qualityMetrics));
            clusters.Add(cluster);
        }

        IReadOnlyList<CognitiveMemoryClusterPlan> materializedClusters = clusters;
        if (request.PersistClusters && clusters.Count > 0)
        {
            materializedClusters = await PersistClustersAsync(dbContext, request, clusters, nowUtc, cancellationToken);
        }

        if (materializedClusters.Count == 0)
        {
            warnings.Add("Cluster planner did not find any multi-member clusters within the requested scope.");
        }

        var metrics = new CognitiveMemoryClusterPlannerMetrics(
            records.Count,
            sourceItemCount,
            keyEntries.Count,
            candidatePairCount,
            materializedClusters.Count,
            materializedClusters.Sum(cluster => cluster.Members.Count),
            relationRows.Count(relation => relation.RelationKind == CognitiveMemoryRelationKind.Contradicts),
            stopwatch.Elapsed);
        return new CognitiveMemoryClusterPlanningResult(materializedClusters, metrics, warnings);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryClusterPlan>> PersistClustersAsync(
        AppDbContext dbContext,
        CognitiveMemoryClusterPlanningRequest request,
        IReadOnlyList<CognitiveMemoryClusterPlan> clusters,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var hashes = clusters.Select(cluster => cluster.ClusterHash).ToArray();
        var existing = await dbContext.Set<CognitiveMemoryQualityClusterRecord>()
            .Where(cluster => cluster.ProjectId == request.ProjectId && hashes.Contains(cluster.ClusterHash))
            .ToDictionaryAsync(cluster => cluster.ClusterHash, cancellationToken);
        var existingClusterIds = existing.Values.Select(cluster => cluster.Id).ToArray();
        if (existingClusterIds.Length > 0)
        {
            var existingKeys = await dbContext.Set<CognitiveMemoryQualityClusterKeyRecord>()
                .Where(key => existingClusterIds.Contains(key.ClusterId))
                .ToListAsync(cancellationToken);
            var existingMembers = await dbContext.Set<CognitiveMemoryQualityClusterMemberRecord>()
                .Where(member => existingClusterIds.Contains(member.ClusterId))
                .ToListAsync(cancellationToken);
            dbContext.RemoveRange(existingKeys);
            dbContext.RemoveRange(existingMembers);
        }

        var persistedPlans = new List<CognitiveMemoryClusterPlan>(clusters.Count);
        foreach (var cluster in clusters)
        {
            var clusterRecord = existing.GetValueOrDefault(cluster.ClusterHash);
            if (clusterRecord is null)
            {
                clusterRecord = new CognitiveMemoryQualityClusterRecord
                {
                    Id = cluster.ClusterId.Value,
                    CreatedAtUtc = nowUtc
                };
                dbContext.Add(clusterRecord);
            }

            var persistedPlan = cluster with { ClusterId = new CognitiveMemoryQualityClusterId(clusterRecord.Id) };
            clusterRecord.ProjectId = persistedPlan.ProjectId;
            clusterRecord.ClusterHash = persistedPlan.ClusterHash;
            clusterRecord.PrimaryKeyFamily = persistedPlan.PrimaryKeyFamily;
            clusterRecord.Readiness = persistedPlan.Readiness;
            clusterRecord.AccessLevel = persistedPlan.Members.Select(member => member.AccessLevel).DefaultIfEmpty(CognitiveMemoryAccessLevel.Project).Max();
            clusterRecord.RiskLevel = persistedPlan.Members.Select(member => member.RiskLevel).DefaultIfEmpty(CognitiveMemoryRiskLevel.Low).Max();
            clusterRecord.PolicyProfileId = request.PolicyContext.PolicyProfileId.Value;
            clusterRecord.AlgorithmVersion = AlgorithmVersion;
            clusterRecord.KeyCount = persistedPlan.Keys.Count;
            clusterRecord.MemberCount = persistedPlan.Members.Count;
            clusterRecord.SourceEvidenceCount = persistedPlan.Members.Count(member => member.EvidenceAnchorId is not null);
            clusterRecord.ContradictionCount = persistedPlan.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory ? 1 : 0;
            clusterRecord.CohesionScore = persistedPlan.QualityMetrics.CohesionScore;
            clusterRecord.SourceIndependenceScore = persistedPlan.QualityMetrics.SourceIndependenceScore;
            clusterRecord.SourceDiversityScore = persistedPlan.QualityMetrics.SourceDiversityScore;
            clusterRecord.SemanticSignalScore = persistedPlan.QualityMetrics.SemanticSignalScore;
            clusterRecord.SupportingSignalScore = persistedPlan.QualityMetrics.SupportingSignalScore;
            clusterRecord.GuardPenaltyScore = persistedPlan.QualityMetrics.GuardPenaltyScore;
            clusterRecord.CompositeScore = persistedPlan.QualityMetrics.CompositeScore;
            clusterRecord.AggregateEligible = persistedPlan.QualityMetrics.AggregateEligible;
            clusterRecord.EligibilityReason = persistedPlan.QualityMetrics.EligibilityReason;
            clusterRecord.UpdatedAtUtc = nowUtc;
            clusterRecord.ConcurrencyToken = Guid.NewGuid();
            dbContext.AddRange(cluster.Keys.Select(key => new CognitiveMemoryQualityClusterKeyRecord
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterRecord.Id,
                ProjectId = persistedPlan.ProjectId,
                KeyFamily = key.Family,
                Key = key.Key,
                DisplayText = key.DisplayText,
                CreatedAtUtc = nowUtc
            }));
            dbContext.AddRange(persistedPlan.Members.Select(member => new CognitiveMemoryQualityClusterMemberRecord
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterRecord.Id,
                ProjectId = persistedPlan.ProjectId,
                MemberKind = member.MemberKind,
                MemoryRecordId = member.MemoryRecordId?.Value,
                SourceItemId = member.SourceItemId?.Value,
                EvidenceAnchorId = member.EvidenceAnchorId?.Value,
                AccessLevel = member.AccessLevel,
                RiskLevel = member.RiskLevel,
                ValidationState = member.ValidationState,
                StabilityState = member.StabilityState,
                CreatedAtUtc = nowUtc
            }));
            persistedPlans.Add(persistedPlan);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return persistedPlans;
    }

    private static IReadOnlyDictionary<Guid, IReadOnlyList<string>> BuildRelationKeysByRecordId(
        IReadOnlyList<CognitiveMemoryRelationRecord> relationRows)
    {
        var result = new Dictionary<Guid, List<string>>();
        foreach (var relation in relationRows)
        {
            var pairKey = NormalizePair(relation.SourceMemoryRecordId, relation.TargetMemoryRecordId);
            var key = $"relation:{relation.RelationKind}:{pairKey}";
            Add(relation.SourceMemoryRecordId, key);
            Add(relation.TargetMemoryRecordId, key);
        }

        return result.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<string>)pair.Value);

        void Add(Guid recordId, string key)
        {
            if (!result.TryGetValue(recordId, out var keys))
            {
                keys = [];
                result[recordId] = keys;
            }

            keys.Add(key);
        }
    }

    private static IReadOnlyList<CompositeClusterCandidate> BuildCompositeClusters(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        int minMembers,
        HashSet<string> contradictionPairs,
        ICognitiveMemoryCandidatePairSelector candidatePairSelector,
        out int candidatePairCount,
        out bool pairBudgetReached)
    {
        var candidatePairs = candidatePairSelector.SelectCandidatePairs(records, contradictionPairs);
        candidatePairCount = candidatePairs.Pairs.Count;
        pairBudgetReached = candidatePairs.PairBudgetReached;
        var edges = new List<CompositeEdgeSignal>();

        foreach (var pair in candidatePairs.Pairs.Values)
        {
            var edge = ScoreCompositeEdge(pair.Left, pair.Right, contradictionPairs);
            if (!edge.Connects)
            {
                continue;
            }

            edges.Add(edge);
        }

        return BuildCohesiveClusterCandidates(records, edges, minMembers)
            .Where(candidate => candidate.Keys.Any())
            .OrderBy(candidate => candidate.Keys.First().Family)
            .ThenBy(candidate => candidate.Keys.First().Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<CompositeClusterCandidate> BuildCohesiveClusterCandidates(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> records,
        IReadOnlyList<CompositeEdgeSignal> edges,
        int minMembers)
    {
        var recordsById = records.ToDictionary(record => record.Record.Id);
        var edgeByPair = edges.ToDictionary(edge => NormalizePair(edge.LeftRecordId, edge.RightRecordId), StringComparer.Ordinal);
        var clusterSignatures = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<CompositeClusterCandidate>();
        foreach (var seedEdge in edges
            .OrderByDescending(edge => edge.Score)
            .ThenBy(edge => NormalizePair(edge.LeftRecordId, edge.RightRecordId), StringComparer.Ordinal))
        {
            var memberIds = new SortedSet<Guid> { seedEdge.LeftRecordId, seedEdge.RightRecordId };
            foreach (var candidate in records.OrderBy(record => record.Record.Id))
            {
                if (memberIds.Contains(candidate.Record.Id))
                {
                    continue;
                }

                if (CanJoinCohesiveCandidate(candidate.Record.Id, memberIds, edgeByPair))
                {
                    memberIds.Add(candidate.Record.Id);
                }
            }

            if (memberIds.Count < minMembers)
            {
                continue;
            }

            var signature = string.Join('|', memberIds);
            if (!clusterSignatures.Add(signature))
            {
                continue;
            }

            var members = memberIds
                .Select(recordId => recordsById[recordId])
                .OrderBy(record => record.Record.Id)
                .ToArray();
            var memberIdSet = memberIds.ToHashSet();
            var candidateEdges = edges
                .Where(edge => memberIdSet.Contains(edge.LeftRecordId) && memberIdSet.Contains(edge.RightRecordId))
                .OrderByDescending(edge => edge.Score)
                .ToArray();
            candidates.Add(new CompositeClusterCandidate(
                members,
                BuildSharedClusterKeys(members),
                candidateEdges));
        }

        return candidates;
    }

    private static bool CanJoinCohesiveCandidate(
        Guid candidateRecordId,
        IReadOnlySet<Guid> memberIds,
        IReadOnlyDictionary<string, CompositeEdgeSignal> edgeByPair)
    {
        foreach (var memberId in memberIds)
        {
            if (!edgeByPair.TryGetValue(NormalizePair(candidateRecordId, memberId), out var edge) ||
                edge.IsContradiction ||
                edge.Score < CompositeEdgeThreshold)
            {
                return false;
            }
        }

        return true;
    }

    private static CompositeEdgeSignal ScoreCompositeEdge(
        CognitiveMemoryClusterRecordEntry left,
        CognitiveMemoryClusterRecordEntry right,
        HashSet<string> contradictionPairs)
    {
        var sharedKeys = left.Keys
            .Join(
                right.Keys,
                leftKey => new { leftKey.Family, leftKey.Key },
                rightKey => new { rightKey.Family, rightKey.Key },
                (leftKey, _) => leftKey)
            .DistinctBy(key => new { key.Family, key.Key })
            .ToArray();
        var positiveScore = 0d;
        var explanations = new List<string>();
        foreach (var keyGroup in sharedKeys.GroupBy(key => key.Family).OrderBy(group => group.Key))
        {
            var keys = keyGroup.Take(4).ToArray();
            var familyScore = keyGroup.Key switch
            {
                CognitiveMemoryQualityClusterKeyFamily.SemanticTopic => 0.48,
                CognitiveMemoryQualityClusterKeyFamily.Entity => Math.Min(keys.Length, 4) * 0.16,
                CognitiveMemoryQualityClusterKeyFamily.TaskIntent => Math.Min(keys.Length, 3) * 0.12,
                CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap => 0.34,
                CognitiveMemoryQualityClusterKeyFamily.Relation => 0.44,
                CognitiveMemoryQualityClusterKeyFamily.SourceTopology => 0.06,
                CognitiveMemoryQualityClusterKeyFamily.Temporal => 0.03,
                CognitiveMemoryQualityClusterKeyFamily.AccessRisk => 0.02,
                _ => 0
            };
            if (familyScore <= 0)
            {
                continue;
            }

            positiveScore += familyScore;
            explanations.Add($"{keyGroup.Key}:{string.Join(',', keys.Select(key => key.DisplayText).Distinct(StringComparer.OrdinalIgnoreCase))}");
        }

        var sharedContentTokens = SharedContentTokens(left.Record, right.Record);
        if (sharedContentTokens.Count >= 3)
        {
            positiveScore += 0.28;
            explanations.Add($"Content:{string.Join(',', sharedContentTokens.Take(5))}");
        }
        else if (sharedContentTokens.Count == 2)
        {
            positiveScore += 0.18;
            explanations.Add($"Content:{string.Join(',', sharedContentTokens)}");
        }

        var contradiction = contradictionPairs.Contains(NormalizePair(left.Record.Id, right.Record.Id));
        var penalty = contradiction ? 0.7 : 0d;
        if (left.Record.AccessLevel != right.Record.AccessLevel ||
            left.Support.HighestRedactionState != right.Support.HighestRedactionState)
        {
            penalty += 0.35;
            explanations.Add("Guard:access/redaction mismatch");
        }

        if (left.Record.StabilityState is CognitiveMemoryStabilityState.Stale or CognitiveMemoryStabilityState.Deprecated ||
            right.Record.StabilityState is CognitiveMemoryStabilityState.Stale or CognitiveMemoryStabilityState.Deprecated ||
            left.Record.ValidationState is CognitiveMemoryValidationState.NeedsHumanReview or CognitiveMemoryValidationState.Superseded ||
            right.Record.ValidationState is CognitiveMemoryValidationState.NeedsHumanReview or CognitiveMemoryValidationState.Superseded)
        {
            penalty += 0.25;
            explanations.Add("Guard:stale/review state");
        }

        if (contradiction)
        {
            explanations.Add("Guard:contradiction");
        }

        if (!HasSemanticFormationSignal(sharedKeys, sharedContentTokens))
        {
            positiveScore = Math.Min(positiveScore, 0.45);
        }

        var edgeScore = Math.Clamp(positiveScore - penalty, 0, 1);
        var contradictionOnly = contradiction &&
                                !sharedKeys.Any(key => key.Family is CognitiveMemoryQualityClusterKeyFamily.SemanticTopic
                                    or CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap);
        if (contradictionOnly)
        {
            explanations.Add("Relation:contradiction-only");
        }

        var connects = edgeScore >= CompositeEdgeThreshold || contradiction;
        return new CompositeEdgeSignal(
            left.Record.Id,
            right.Record.Id,
            RoundScore(edgeScore),
            connects,
            contradiction,
            contradictionOnly,
            string.Join("; ", explanations.Distinct(StringComparer.Ordinal)));
    }

    private static bool HasSemanticFormationSignal(
        IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> sharedKeys,
        IReadOnlyList<string> sharedContentTokens)
        => sharedContentTokens.Count >= 2 ||
           sharedKeys.Any(key => key.Family is CognitiveMemoryQualityClusterKeyFamily.SemanticTopic
               or CognitiveMemoryQualityClusterKeyFamily.Entity
               or CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap
               or CognitiveMemoryQualityClusterKeyFamily.Relation);

    private static IReadOnlyList<string> SharedContentTokens(CognitiveMemoryRecord left, CognitiveMemoryRecord right)
    {
        var leftTokens = CognitiveMemoryClusterTextSignals
            .ExtractSignals($"{left.Title} {left.TopicKey} {left.CanonicalText} {left.SummaryText}", maxSignals: 24)
            .ToHashSet(StringComparer.Ordinal);
        return CognitiveMemoryClusterTextSignals
            .ExtractSignals($"{right.Title} {right.TopicKey} {right.CanonicalText} {right.SummaryText}", maxSignals: 24)
            .Where(leftTokens.Contains)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> BuildSharedClusterKeys(IReadOnlyList<CognitiveMemoryClusterRecordEntry> members)
        => members
            .SelectMany(member => member.Keys)
            .GroupBy(key => new { key.Family, key.Key })
            .Where(group => group.Select(key => key.RecordId).Distinct().Count() >= Math.Min(2, members.Count))
            .Select(group => group.First() with { RecordId = Guid.Empty })
            .Where(key => key.Family != CognitiveMemoryQualityClusterKeyFamily.TaskIntent ||
                          !string.Equals(key.Key, "intent:general", StringComparison.Ordinal))
            .OrderBy(key => key.Family)
            .ThenBy(key => key.Key, StringComparer.Ordinal)
            .ToArray();

    private static CognitiveMemoryClusterKeyWithRecord? SelectPrimaryClusterKey(IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> keys)
        => keys
            .Where(key => IsStrongPrimaryKey(key))
            .OrderByDescending(key => key.Family switch
            {
                CognitiveMemoryQualityClusterKeyFamily.SemanticTopic => 6,
                CognitiveMemoryQualityClusterKeyFamily.Relation => 5,
                CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap => 4,
                CognitiveMemoryQualityClusterKeyFamily.Entity => 3,
                CognitiveMemoryQualityClusterKeyFamily.TaskIntent => 2,
                _ => 0
            })
            .ThenBy(key => key.Key, StringComparer.Ordinal)
            .FirstOrDefault();

    private static string SummarizeEdgeSignals(IReadOnlyList<CompositeEdgeSignal> edges)
        => string.Join(
            " | ",
            edges
                .OrderByDescending(edge => edge.Score)
                .Take(3)
                .Select(edge => $"{edge.Score:0.###}:{edge.Explanation}"));

    private static IReadOnlyList<CognitiveMemoryClusterMember> ToClusterMembers(
        CognitiveMemoryRecord record,
        CognitiveMemoryRecordSupport support)
    {
        var primarySourceItem = support.SourceItems.FirstOrDefault();
        var primaryEvidenceAnchor = support.EvidenceAnchors.FirstOrDefault();
        var members = new List<CognitiveMemoryClusterMember>
        {
            new(
            CognitiveMemoryQualityClusterMemberKind.MemoryRecord,
            new CognitiveMemoryRecordId(record.Id),
            primarySourceItem is null ? null : new CognitiveMemorySourceItemId(primarySourceItem.Id),
            primaryEvidenceAnchor is null ? null : new CognitiveMemoryEvidenceAnchorId(primaryEvidenceAnchor.Id),
            record.Title,
            record.AccessLevel,
            record.RiskLevel,
            record.ValidationState,
            record.StabilityState)
        };
        foreach (var sourceItem in support.SourceItems)
        {
            var evidenceAnchor = support.EvidenceAnchors.FirstOrDefault(anchor => anchor.SourceItemId == sourceItem.Id);
            members.Add(new CognitiveMemoryClusterMember(
                CognitiveMemoryQualityClusterMemberKind.SourceItem,
                null,
                new CognitiveMemorySourceItemId(sourceItem.Id),
                evidenceAnchor is null ? null : new CognitiveMemoryEvidenceAnchorId(evidenceAnchor.Id),
                sourceItem.Title,
                sourceItem.AccessLevel,
                ResolveSourceItemRiskLevel(sourceItem),
                CognitiveMemoryValidationState.Approved,
                CognitiveMemoryStabilityState.Active));
        }

        return members;
    }

    private static CognitiveMemoryRiskLevel ResolveSourceItemRiskLevel(CognitiveMemorySourceItemRecord sourceItem)
        => sourceItem.AccessLevel == CognitiveMemoryAccessLevel.Restricted ||
           sourceItem.RedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted
            ? CognitiveMemoryRiskLevel.High
            : CognitiveMemoryRiskLevel.Low;

    private static bool IsStrongPrimaryKey(CognitiveMemoryClusterKeyWithRecord key)
    {
        if (!StrongPrimaryFamilies.Contains(key.Family))
        {
            return false;
        }

        return key.Family != CognitiveMemoryQualityClusterKeyFamily.TaskIntent ||
               !string.Equals(key.Key, "intent:general", StringComparison.Ordinal);
    }

    private static CognitiveMemoryClusterQualityMetrics ScoreCluster(
        CognitiveMemoryQualityClusterKeyFamily primaryFamily,
        string primaryKey,
        IReadOnlyList<ClusterKeyEntry> members,
        IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> clusterKeys,
        IReadOnlyList<CompositeEdgeSignal> edges,
        HashSet<string> contradictionPairs)
    {
        var memoryRecordCount = members.Select(member => member.Record.Id).Distinct().Count();
        var distinctSourceItemCount = members
            .SelectMany(member => member.Support.SourceItems.Select(item => item.Id))
            .Distinct()
            .Count();
        var distinctSourceSystemCount = members
            .SelectMany(member => member.Support.SourceItems.Select(item => CognitiveMemoryQualityText.NormalizeKey(item.SourceSystem)))
            .Distinct(StringComparer.Ordinal)
            .Count();
        var strongKeyCount = clusterKeys
            .Where(IsStrongPrimaryKey)
            .Select(key => new { key.Family, key.Key })
            .Distinct()
            .Count();
        var supportingKeyCount = clusterKeys
            .Where(key => SupportingFamilies.Contains(key.Family))
            .Select(key => new { key.Family, key.Key })
            .Distinct()
            .Count();
        var semanticSignalScore = Math.Clamp(ScorePrimarySignal(primaryFamily, primaryKey) + Math.Min(strongKeyCount, 4) * 0.08, 0, 1);
        var sourceIndependenceScore = Math.Clamp(distinctSourceItemCount / 2d, 0, 1);
        var sourceDiversityScore = Math.Clamp(distinctSourceSystemCount / 2d, 0, 1);
        var supportingSignalScore = Math.Clamp(supportingKeyCount / 4d, 0, 1);
        var guardPenalty = ResolveGuardPenalty(members, contradictionPairs, memoryRecordCount);
        var expectedEdgeCount = memoryRecordCount < 2
            ? 1
            : memoryRecordCount * (memoryRecordCount - 1) / 2d;
        var edgeCoverageScore = Math.Clamp(edges.Count / expectedEdgeCount, 0, 1);
        var averageEdgeScore = edges.Count == 0
            ? 0
            : edges.Average(edge => edge.Score);
        var cohesionScore = Math.Clamp(
            semanticSignalScore * 0.55 +
            averageEdgeScore * 0.35 +
            edgeCoverageScore * 0.08 +
            Math.Min(memoryRecordCount, 5) * 0.02,
            0,
            1);
        var compositeScore = Math.Clamp(
            cohesionScore * 0.48 +
            sourceIndependenceScore * 0.22 +
            sourceDiversityScore * 0.12 +
            supportingSignalScore * 0.08 -
            guardPenalty,
            0,
            1);
        var aggregateEligible = compositeScore >= 0.62 &&
                                cohesionScore >= 0.55 &&
                                sourceIndependenceScore >= 1 &&
                                guardPenalty == 0 &&
                                memoryRecordCount <= MaxAggregateReadyMemoryRecords &&
                                edgeCoverageScore >= 1 &&
                                members.All(member => member.Support.EvidenceAnchors.Count > 0);
        var reason = aggregateEligible
            ? "Composite semantic, source independence, and guard metrics passed."
            : ResolveEligibilityReason(cohesionScore, sourceIndependenceScore, guardPenalty, memoryRecordCount, members);

        return new CognitiveMemoryClusterQualityMetrics(
            RoundScore(cohesionScore),
            RoundScore(sourceIndependenceScore),
            RoundScore(sourceDiversityScore),
            RoundScore(semanticSignalScore),
            RoundScore(supportingSignalScore),
            RoundScore(guardPenalty),
            RoundScore(compositeScore),
            aggregateEligible,
            reason);
    }

    private static double ScorePrimarySignal(CognitiveMemoryQualityClusterKeyFamily primaryFamily, string primaryKey)
        => primaryFamily switch
        {
            CognitiveMemoryQualityClusterKeyFamily.SemanticTopic => 0.58,
            CognitiveMemoryQualityClusterKeyFamily.Relation => 0.54,
            CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap => 0.52,
            CognitiveMemoryQualityClusterKeyFamily.Entity => 0.46,
            CognitiveMemoryQualityClusterKeyFamily.TaskIntent when !string.Equals(primaryKey, "intent:general", StringComparison.Ordinal) => 0.42,
            _ => 0.2
        };

    private static double ResolveGuardPenalty(
        IReadOnlyList<ClusterKeyEntry> members,
        HashSet<string> contradictionPairs,
        int memoryRecordCount)
    {
        var penalty = 0d;
        if (memoryRecordCount > MaxAggregateReadyMemoryRecords)
        {
            penalty += 0.35;
        }

        if (members.Any(member => member.Record.AccessLevel == CognitiveMemoryAccessLevel.Restricted ||
                                  member.Support.HighestRedactionState == CognitiveMemoryRedactionState.Restricted))
        {
            penalty += 0.45;
        }

        if (members.Any(member => member.Record.StabilityState is CognitiveMemoryStabilityState.Stale or CognitiveMemoryStabilityState.Deprecated ||
                                  member.Record.ValidationState is CognitiveMemoryValidationState.NeedsHumanReview or CognitiveMemoryValidationState.Superseded))
        {
            penalty += 0.25;
        }

        for (var left = 0; left < members.Count; left++)
        {
            for (var right = left + 1; right < members.Count; right++)
            {
                if (contradictionPairs.Contains(NormalizePair(members[left].Record.Id, members[right].Record.Id)))
                {
                    penalty += 0.55;
                    return Math.Clamp(penalty, 0, 1);
                }
            }
        }

        return Math.Clamp(penalty, 0, 1);
    }

    private static string ResolveEligibilityReason(
        double cohesionScore,
        double sourceIndependenceScore,
        double guardPenalty,
        int memoryRecordCount,
        IReadOnlyList<ClusterKeyEntry> members)
    {
        if (guardPenalty > 0)
        {
            return "Guard metrics require review before aggregate promotion.";
        }

        if (memoryRecordCount > MaxAggregateReadyMemoryRecords)
        {
            return "Cluster is too broad for automatic aggregate promotion.";
        }

        if (sourceIndependenceScore < 1)
        {
            return "Cluster lacks two independent source items.";
        }

        if (cohesionScore < 0.55)
        {
            return "Cluster semantic cohesion is below aggregate threshold.";
        }

        if (members.Any(member => member.Support.EvidenceAnchors.Count == 0))
        {
            return "Cluster contains memory records without evidence anchors.";
        }

        return "Composite score is below aggregate threshold.";
    }

    private static double RoundScore(double value)
        => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static CognitiveMemoryQualityClusterReadiness ResolveReadiness(
        IReadOnlyList<ClusterKeyEntry> members,
        HashSet<string> contradictionPairs,
        CognitiveMemoryClusterQualityMetrics qualityMetrics)
    {
        if (members.Any(member => member.Record.AccessLevel == CognitiveMemoryAccessLevel.Restricted ||
                                  member.Support.HighestRedactionState == CognitiveMemoryRedactionState.Restricted))
        {
            return CognitiveMemoryQualityClusterReadiness.Restricted;
        }

        for (var left = 0; left < members.Count; left++)
        {
            for (var right = left + 1; right < members.Count; right++)
            {
                if (contradictionPairs.Contains(NormalizePair(members[left].Record.Id, members[right].Record.Id)))
                {
                    return CognitiveMemoryQualityClusterReadiness.Contradictory;
                }
            }
        }

        if (members.Any(member => member.Record.StabilityState is CognitiveMemoryStabilityState.Stale or CognitiveMemoryStabilityState.Deprecated ||
                                  member.Record.ValidationState is CognitiveMemoryValidationState.NeedsHumanReview or CognitiveMemoryValidationState.Superseded))
        {
            return CognitiveMemoryQualityClusterReadiness.NeedsHumanReview;
        }

        if (!qualityMetrics.AggregateEligible)
        {
            return qualityMetrics.GuardPenaltyScore > 0 || members.Count > MaxAggregateReadyMemoryRecords
                ? CognitiveMemoryQualityClusterReadiness.NeedsHumanReview
                : CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence;
        }

        return members.All(member => member.Support.EvidenceAnchors.Count > 0)
            ? CognitiveMemoryQualityClusterReadiness.AggregateReady
            : CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence;
    }

    private static IReadOnlyList<string> ResolveClusterWarnings(
        CognitiveMemoryQualityClusterReadiness readiness,
        IReadOnlyList<CognitiveMemoryClusterMember> members,
        CognitiveMemoryClusterQualityMetrics qualityMetrics)
    {
        var warnings = new List<string>();
        if (readiness is CognitiveMemoryQualityClusterReadiness.Restricted or CognitiveMemoryQualityClusterReadiness.Contradictory)
        {
            warnings.Add($"Cluster requires validation review because readiness is {readiness}.");
        }

        if (members.Any(member => member.EvidenceAnchorId is null))
        {
            warnings.Add("Cluster contains one or more members without explicit evidence anchors.");
        }

        if (!qualityMetrics.AggregateEligible)
        {
            warnings.Add(qualityMetrics.EligibilityReason);
        }

        return warnings;
    }

    private static string CreateClusterHash(
        Guid? projectId,
        CognitiveMemoryQualityClusterKeyFamily family,
        string key,
        IEnumerable<Guid> memberIds)
    {
        var material = $"{projectId?.ToString("D") ?? "global"}|{family}|{key}|{string.Join('|', memberIds.OrderBy(id => id))}";
        return CognitiveMemoryHash.FromUtf8(material).Value;
    }

    private static string NormalizePair(Guid first, Guid second)
        => first.CompareTo(second) <= 0
            ? $"{first:D}:{second:D}"
            : $"{second:D}:{first:D}";

    private sealed record ClusterKeyEntry(
        CognitiveMemoryRecord Record,
        CognitiveMemoryRecordSupport Support,
        CognitiveMemoryClusterKeyWithRecord Key);

    private sealed record CompositeClusterCandidate(
        IReadOnlyList<CognitiveMemoryClusterRecordEntry> Members,
        IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> Keys,
        IReadOnlyList<CompositeEdgeSignal> Edges);

    private sealed record CompositeEdgeSignal(
        Guid LeftRecordId,
        Guid RightRecordId,
        double Score,
        bool Connects,
        bool IsContradiction,
        bool IsContradictionOnly,
        string Explanation);
}
