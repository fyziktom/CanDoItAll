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

        var keyEntries = new List<ClusterKeyEntry>();
        foreach (var record in records)
        {
            var recordSupport = support.ByRecordId.GetValueOrDefault(record.Id) ?? CognitiveMemoryRecordSupport.Empty(record.Id);
            foreach (var key in CreateKeys(record, recordSupport, relationKeysByRecordId.GetValueOrDefault(record.Id) ?? [], request.KeyFamilies))
            {
                keyEntries.Add(new ClusterKeyEntry(record, recordSupport, key));
            }
        }

        var warnings = new List<string>();
        var clusters = new List<CognitiveMemoryClusterPlan>();
        foreach (var group in keyEntries
            .Where(entry => IsStrongPrimaryKey(entry.Key))
            .GroupBy(entry => new { entry.Key.Family, entry.Key.Key })
            .OrderBy(group => group.Key.Family)
            .ThenBy(group => group.Key.Key, StringComparer.Ordinal))
        {
            var members = group
                .GroupBy(entry => entry.Record.Id)
                .Select(entryGroup => entryGroup.First())
                .ToArray();
            if (members.Length < request.MinMembers)
            {
                continue;
            }

            if (group.Key.Family == CognitiveMemoryQualityClusterKeyFamily.TaskIntent &&
                string.Equals(group.Key.Key, "intent:general", StringComparison.Ordinal))
            {
                continue;
            }

            var memberRecordIds = members.Select(member => member.Record.Id).ToHashSet();
            var clusterKeySignals = members
                .SelectMany(member => CreateKeys(member.Record, member.Support, relationKeysByRecordId.GetValueOrDefault(member.Record.Id) ?? [], request.KeyFamilies))
                .GroupBy(key => new { key.Family, key.Key })
                .Where(keyGroup => keyGroup.Count(key => memberRecordIds.Contains(key.RecordId)) >= Math.Min(2, members.Length))
                .Select(keyGroup => keyGroup.First() with { RecordId = Guid.Empty })
                .OrderBy(key => key.Family)
                .ThenBy(key => key.Key, StringComparer.Ordinal)
                .ToArray();
            var clusterKeys = clusterKeySignals
                .Select(key => new CognitiveMemoryClusterKey(key.Family, key.Key, key.DisplayText))
                .ToArray();
            var clusterMembers = members
                .SelectMany(member => ToClusterMembers(member.Record, member.Support))
                .ToArray();
            var qualityMetrics = ScoreCluster(group.Key.Family, group.Key.Key, members, clusterKeySignals, contradictionPairs);
            var readiness = ResolveReadiness(members, contradictionPairs, qualityMetrics);
            var clusterHash = CreateClusterHash(request.ProjectId, group.Key.Family, group.Key.Key, members.Select(member => member.Record.Id));
            var cluster = new CognitiveMemoryClusterPlan(
                CognitiveMemoryQualityClusterId.New(),
                request.ProjectId,
                clusterHash,
                group.Key.Family,
                readiness,
                clusterKeys.Length == 0
                    ? [new CognitiveMemoryClusterKey(group.Key.Family, group.Key.Key, group.First().Key.DisplayText)]
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
        foreach (var token in CognitiveMemoryQualityText.ExtractMeaningfulTokens($"{record.Title} {record.TopicKey} {string.Join(' ', support.Claims.Select(claim => claim.SubjectKey))}", maxTokens: 4))
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

    private sealed record CognitiveMemoryClusterKeyWithRecord(
        Guid RecordId,
        CognitiveMemoryQualityClusterKeyFamily Family,
        string Key,
        string DisplayText);
}
