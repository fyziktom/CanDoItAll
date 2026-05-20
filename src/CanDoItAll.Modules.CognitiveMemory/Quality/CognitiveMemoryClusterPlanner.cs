using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
public sealed class CognitiveMemoryClusterPlanner(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryClusterPlanner
{
    private const string AlgorithmVersion = "quality-clustering-v2";
    private const int MaxAggregateReadyMemoryRecords = 20;
    private const int MaxCandidateKeyFanout = 80;
    private const int MaxCandidatePairs = 5000;
    private const double CompositeEdgeThreshold = 0.58;

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

        var recordEntries = new List<ClusterRecordEntry>(records.Count);
        var keyEntries = new List<ClusterKeyEntry>();
        foreach (var record in records)
        {
            var recordSupport = support.ByRecordId.GetValueOrDefault(record.Id) ?? CognitiveMemoryRecordSupport.Empty(record.Id);
            var recordKeys = CreateKeys(record, recordSupport, relationKeysByRecordId.GetValueOrDefault(record.Id) ?? [], request.KeyFamilies);
            recordEntries.Add(new ClusterRecordEntry(record, recordSupport, recordKeys));
            foreach (var key in recordKeys)
            {
                keyEntries.Add(new ClusterKeyEntry(record, recordSupport, key));
            }
        }

        var warnings = new List<string>();
        var clusters = new List<CognitiveMemoryClusterPlan>();
        var compositeClusters = BuildCompositeClusters(recordEntries, request.MinMembers, contradictionPairs, out var candidatePairCount);
        if (candidatePairCount >= MaxCandidatePairs)
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
            var qualityMetrics = ScoreCluster(primaryKey.Family, primaryKey.Key, memberKeyEntries, compositeCluster.Keys, contradictionPairs);
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

    private static IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> CreateKeys(
        CognitiveMemoryRecord record,
        CognitiveMemoryRecordSupport support,
        IReadOnlyList<string> relationKeys,
        IReadOnlyList<CognitiveMemoryQualityClusterKeyFamily> enabledFamilies)
    {
        var keys = new List<CognitiveMemoryClusterKeyWithRecord>();
        void Add(CognitiveMemoryQualityClusterKeyFamily family, string key, string displayText)
        {
            if (!enabledFamilies.Contains(family) || string.IsNullOrWhiteSpace(key))
            {
                return;
            }

            keys.Add(new CognitiveMemoryClusterKeyWithRecord(record.Id, family, key, displayText));
        }

        Add(CognitiveMemoryQualityClusterKeyFamily.ProjectScope, $"project:{record.ProjectId?.ToString("D") ?? "global"}", "Project scope");
        foreach (var sourceItem in support.SourceItems)
        {
            Add(
                CognitiveMemoryQualityClusterKeyFamily.SourceTopology,
                $"source:{CognitiveMemoryQualityText.NormalizeKey(sourceItem.SourceSystem)}:{CognitiveMemoryQualityText.NormalizeKey(sourceItem.SourceItemType)}",
                $"{sourceItem.SourceSystem}/{sourceItem.SourceItemType}");
        }

        Add(
            CognitiveMemoryQualityClusterKeyFamily.SemanticTopic,
            $"topic:{CognitiveMemoryQualityText.NormalizeKey(FirstNonEmpty(record.TopicKey, record.Title))}",
            FirstNonEmpty(record.TopicKey, record.Title));
        foreach (var token in CognitiveMemoryQualityText.ExtractMeaningfulTokens(
            $"{record.Title} {record.TopicKey} {record.CanonicalText} {record.SummaryText} {string.Join(' ', support.Claims.Select(claim => $"{claim.SubjectKey} {claim.ObjectKey}"))}",
            maxTokens: 10))
        {
            Add(CognitiveMemoryQualityClusterKeyFamily.Entity, $"entity:{token}", token);
        }

        foreach (var intent in CognitiveMemoryQualityText.ResolveTaskIntents($"{record.Title} {record.CanonicalText} {record.SummaryText}"))
        {
            Add(CognitiveMemoryQualityClusterKeyFamily.TaskIntent, $"intent:{intent}", intent);
        }

        Add(
            CognitiveMemoryQualityClusterKeyFamily.Temporal,
            $"updated:{record.UpdatedAtUtc:yyyy-MM}",
            $"Updated {record.UpdatedAtUtc:yyyy-MM}");
        foreach (var evidenceKey in support.EvidenceAnchors
            .SelectMany(anchor => new[] { anchor.SourceHash, anchor.QuoteHash })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Take(4))
        {
            Add(CognitiveMemoryQualityClusterKeyFamily.EvidenceOverlap, $"evidence:{evidenceKey}", "Evidence overlap");
        }

        foreach (var relationKey in relationKeys)
        {
            Add(CognitiveMemoryQualityClusterKeyFamily.Relation, relationKey, relationKey);
        }

        Add(
            CognitiveMemoryQualityClusterKeyFamily.AccessRisk,
            $"access:{record.AccessLevel}:risk:{record.RiskLevel}:redaction:{support.HighestRedactionState}",
            $"{record.AccessLevel}/{record.RiskLevel}/{support.HighestRedactionState}");
        return keys
            .GroupBy(key => new { key.Family, key.Key })
            .Select(group => group.First())
            .ToArray();
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
        IReadOnlyList<ClusterRecordEntry> records,
        int minMembers,
        HashSet<string> contradictionPairs,
        out int candidatePairCount)
    {
        var candidatePairs = BuildCandidatePairs(records);
        candidatePairCount = candidatePairs.Count;
        var parentByRecordId = records.ToDictionary(record => record.Record.Id, record => record.Record.Id);
        var edges = new List<CompositeEdgeSignal>();

        foreach (var pair in candidatePairs.Values)
        {
            var edge = ScoreCompositeEdge(pair.Left, pair.Right, contradictionPairs);
            if (!edge.Connects)
            {
                continue;
            }

            edges.Add(edge);
            Union(parentByRecordId, pair.Left.Record.Id, pair.Right.Record.Id);
        }

        var connectedRecordIds = edges
            .SelectMany(edge => new[] { edge.LeftRecordId, edge.RightRecordId })
            .ToHashSet();
        return records
            .Where(record => connectedRecordIds.Contains(record.Record.Id))
            .GroupBy(record => Find(parentByRecordId, record.Record.Id))
            .Select(group => group.OrderBy(record => record.Record.Id).ToArray())
            .Where(group => group.Length >= minMembers)
            .Select(group =>
            {
                var memberIds = group.Select(member => member.Record.Id).ToHashSet();
                var componentEdges = edges
                    .Where(edge => memberIds.Contains(edge.LeftRecordId) && memberIds.Contains(edge.RightRecordId))
                    .OrderByDescending(edge => edge.Score)
                    .ToArray();
                return new CompositeClusterCandidate(
                    group,
                    BuildSharedClusterKeys(group),
                    componentEdges);
            })
            .Where(candidate => candidate.Keys.Any())
            .OrderBy(candidate => candidate.Keys.First().Family)
            .ThenBy(candidate => candidate.Keys.First().Key, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, (ClusterRecordEntry Left, ClusterRecordEntry Right)> BuildCandidatePairs(
        IReadOnlyList<ClusterRecordEntry> records)
    {
        var pairs = new Dictionary<string, (ClusterRecordEntry Left, ClusterRecordEntry Right)>(StringComparer.Ordinal);
        var indexedRecords = records
            .SelectMany(record => record.Keys
                .Where(IsCandidatePreselectionKey)
                .Select(key => new { Key = $"{key.Family}:{key.Key}", Record = record }))
            .GroupBy(entry => entry.Key, StringComparer.Ordinal)
            .Where(group => group.Count() is >= 2 and <= MaxCandidateKeyFanout)
            .OrderBy(group => group.Key, StringComparer.Ordinal);

        foreach (var group in indexedRecords)
        {
            var groupRecords = group
                .Select(entry => entry.Record)
                .DistinctBy(entry => entry.Record.Id)
                .OrderBy(entry => entry.Record.Id)
                .ToArray();
            for (var leftIndex = 0; leftIndex < groupRecords.Length - 1; leftIndex++)
            {
                for (var rightIndex = leftIndex + 1; rightIndex < groupRecords.Length; rightIndex++)
                {
                    var left = groupRecords[leftIndex];
                    var right = groupRecords[rightIndex];
                    if (left.Record.ProjectId != right.Record.ProjectId)
                    {
                        continue;
                    }

                    pairs.TryAdd(NormalizePair(left.Record.Id, right.Record.Id), (left, right));
                    if (pairs.Count >= MaxCandidatePairs)
                    {
                        return pairs;
                    }
                }
            }
        }

        return pairs;
    }

    private static bool IsCandidatePreselectionKey(CognitiveMemoryClusterKeyWithRecord key)
        => IsStrongPrimaryKey(key) &&
           (key.Family != CognitiveMemoryQualityClusterKeyFamily.TaskIntent ||
            !string.Equals(key.Key, "intent:general", StringComparison.Ordinal));

    private static CompositeEdgeSignal ScoreCompositeEdge(
        ClusterRecordEntry left,
        ClusterRecordEntry right,
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
        var connects = edgeScore >= CompositeEdgeThreshold ||
                       contradiction && positiveScore >= 0.45;
        return new CompositeEdgeSignal(
            left.Record.Id,
            right.Record.Id,
            RoundScore(edgeScore),
            connects,
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
        var leftTokens = CognitiveMemoryQualityText
            .ExtractMeaningfulTokens($"{left.Title} {left.TopicKey} {left.CanonicalText} {left.SummaryText}", maxTokens: 16)
            .ToHashSet(StringComparer.Ordinal);
        return CognitiveMemoryQualityText
            .ExtractMeaningfulTokens($"{right.Title} {right.TopicKey} {right.CanonicalText} {right.SummaryText}", maxTokens: 16)
            .Where(leftTokens.Contains)
            .OrderBy(token => token, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> BuildSharedClusterKeys(IReadOnlyList<ClusterRecordEntry> members)
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

    private static void Union(Dictionary<Guid, Guid> parentByRecordId, Guid left, Guid right)
    {
        var leftRoot = Find(parentByRecordId, left);
        var rightRoot = Find(parentByRecordId, right);
        if (leftRoot == rightRoot)
        {
            return;
        }

        if (leftRoot.CompareTo(rightRoot) <= 0)
        {
            parentByRecordId[rightRoot] = leftRoot;
        }
        else
        {
            parentByRecordId[leftRoot] = rightRoot;
        }
    }

    private static Guid Find(Dictionary<Guid, Guid> parentByRecordId, Guid recordId)
    {
        var parent = parentByRecordId[recordId];
        if (parent == recordId)
        {
            return recordId;
        }

        var root = Find(parentByRecordId, parent);
        parentByRecordId[recordId] = root;
        return root;
    }

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
        var cohesionScore = Math.Clamp(semanticSignalScore + Math.Min(memoryRecordCount, 5) * 0.03, 0, 1);
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

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record ClusterKeyEntry(
        CognitiveMemoryRecord Record,
        CognitiveMemoryRecordSupport Support,
        CognitiveMemoryClusterKeyWithRecord Key);

    private sealed record ClusterRecordEntry(
        CognitiveMemoryRecord Record,
        CognitiveMemoryRecordSupport Support,
        IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> Keys);

    private sealed record CompositeClusterCandidate(
        IReadOnlyList<ClusterRecordEntry> Members,
        IReadOnlyList<CognitiveMemoryClusterKeyWithRecord> Keys,
        IReadOnlyList<CompositeEdgeSignal> Edges);

    private sealed record CompositeEdgeSignal(
        Guid LeftRecordId,
        Guid RightRecordId,
        double Score,
        bool Connects,
        string Explanation);

    private sealed record CognitiveMemoryClusterKeyWithRecord(
        Guid RecordId,
        CognitiveMemoryQualityClusterKeyFamily Family,
        string Key,
        string DisplayText);
}
