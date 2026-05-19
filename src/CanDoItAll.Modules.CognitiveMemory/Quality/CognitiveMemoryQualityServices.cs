using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryQualityDiagnosticsService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryQualityDiagnosticsService
{
    public async ValueTask<CognitiveMemoryQualityDiagnosticsReport> CreateReportAsync(
        CognitiveMemoryQualityDiagnosticsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var started = clock.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sourceItems = await CountProjectAsync<CognitiveMemorySourceItemRecord>(dbContext, request.ProjectId, cancellationToken);
        var memoryRecords = await CountProjectAsync<CognitiveMemoryRecord>(dbContext, request.ProjectId, cancellationToken);
        var clusters = await CountProjectAsync<CognitiveMemoryQualityClusterRecord>(dbContext, request.ProjectId, cancellationToken);
        var clusterMembers = await CountProjectAsync<CognitiveMemoryQualityClusterMemberRecord>(dbContext, request.ProjectId, cancellationToken);
        var dreamRuns = await CountProjectAsync<CognitiveMemoryDreamRunRecord>(dbContext, request.ProjectId, cancellationToken);
        var dreamRunClusters = await CountProjectAsync<CognitiveMemoryDreamRunClusterRecord>(dbContext, request.ProjectId, cancellationToken);
        var aggregateCandidates = await CountProjectAsync<CognitiveMemoryDreamAggregateCandidateRecord>(dbContext, request.ProjectId, cancellationToken);
        var aggregateClaims = await CountProjectAsync<CognitiveMemoryDreamAggregateClaimRecord>(dbContext, request.ProjectId, cancellationToken);
        var aggregateSourceMaps = await CountProjectAsync<CognitiveMemoryDreamAggregateClaimSourceMapRecord>(dbContext, request.ProjectId, cancellationToken);
        var validations = await CountProjectAsync<CognitiveMemoryDreamValidationRecord>(dbContext, request.ProjectId, cancellationToken);
        var reviewItems = await CountProjectAsync<CognitiveMemoryReviewItemRecord>(dbContext, request.ProjectId, cancellationToken);
        var synthesizedRecalls = await CountProjectAsync<CognitiveMemorySynthesizedRecallRecord>(dbContext, request.ProjectId, cancellationToken);
        var synthesizedStatements = await CountProjectAsync<CognitiveMemorySynthesizedStatementRecord>(dbContext, request.ProjectId, cancellationToken);

        var warnings = new List<CognitiveMemoryQualityDiagnosticWarning>();
        if (sourceItems > 0 && memoryRecords > 0 && clusters == 0)
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.clusters.missing",
                "Source-backed memories exist, but no quality clusters have been planned.",
                CognitiveMemoryRiskLevel.Medium));
        }

        if (dreamRuns > 0 && (dreamRunClusters == 0 || aggregateCandidates == 0))
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.dream.shallow",
                "A dream run exists without linked clusters or aggregate candidates.",
                CognitiveMemoryRiskLevel.High));
        }

        if (aggregateClaims > 0 && aggregateSourceMaps == 0)
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.aggregate.provenance-missing",
                "Aggregate claims exist without claim-level source maps.",
                CognitiveMemoryRiskLevel.High));
        }

        if (aggregateCandidates > 0 && validations == 0)
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.validation.missing",
                "Aggregate candidates exist without validation gate records.",
                CognitiveMemoryRiskLevel.High));
        }

        if (synthesizedRecalls > 0 && synthesizedStatements == 0)
        {
            warnings.Add(new CognitiveMemoryQualityDiagnosticWarning(
                "quality.recall.synthesis-empty",
                "A synthesized recall exists without user-facing statements.",
                CognitiveMemoryRiskLevel.Medium));
        }

        return new CognitiveMemoryQualityDiagnosticsReport(
            request.ProjectId,
            sourceItems,
            memoryRecords,
            clusters,
            clusterMembers,
            dreamRuns,
            dreamRunClusters,
            aggregateCandidates,
            aggregateClaims,
            aggregateSourceMaps,
            validations,
            reviewItems,
            synthesizedRecalls,
            synthesizedStatements,
            clock.GetUtcNow() - started,
            warnings);
    }

    private static Task<int> CountProjectAsync<TEntity>(
        AppDbContext dbContext,
        Guid? projectId,
        CancellationToken cancellationToken)
        where TEntity : class
        => projectId is null
            ? dbContext.Set<TEntity>().CountAsync(cancellationToken)
            : dbContext.Set<TEntity>().CountAsync(entity => EF.Property<Guid?>(entity, "ProjectId") == projectId, cancellationToken);
}

public sealed class CognitiveMemoryClusterPlanner(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryClusterPlanner
{
    private const string AlgorithmVersion = "quality-clustering-v1";

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

            var memberRecordIds = members.Select(member => member.Record.Id).ToHashSet();
            var clusterKeys = members
                .SelectMany(member => CreateKeys(member.Record, member.Support, relationKeysByRecordId.GetValueOrDefault(member.Record.Id) ?? [], request.KeyFamilies))
                .GroupBy(key => new { key.Family, key.Key })
                .Where(keyGroup => keyGroup.Count(key => memberRecordIds.Contains(key.RecordId)) >= Math.Min(2, members.Length))
                .Select(keyGroup => keyGroup.First() with { RecordId = Guid.Empty })
                .OrderBy(key => key.Family)
                .ThenBy(key => key.Key, StringComparer.Ordinal)
                .Select(key => new CognitiveMemoryClusterKey(key.Family, key.Key, key.DisplayText))
                .ToArray();
            var clusterMembers = members
                .Select(member => ToClusterMember(member.Record, member.Support))
                .ToArray();
            var readiness = ResolveReadiness(members, contradictionPairs);
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
                ResolveClusterWarnings(readiness, clusterMembers));
            clusters.Add(cluster);
        }

        if (request.PersistClusters && clusters.Count > 0)
        {
            await PersistClustersAsync(dbContext, request, clusters, nowUtc, cancellationToken);
        }

        if (clusters.Count == 0)
        {
            warnings.Add("Cluster planner did not find any multi-member clusters within the requested scope.");
        }

        var metrics = new CognitiveMemoryClusterPlannerMetrics(
            records.Count,
            sourceItemCount,
            keyEntries.Count,
            clusters.Count,
            clusters.Sum(cluster => cluster.Members.Count),
            relationRows.Count(relation => relation.RelationKind == CognitiveMemoryRelationKind.Contradicts),
            stopwatch.Elapsed);
        return new CognitiveMemoryClusterPlanningResult(clusters, metrics, warnings);
    }

    private static async Task PersistClustersAsync(
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
        foreach (var cluster in clusters)
        {
            if (existing.ContainsKey(cluster.ClusterHash))
            {
                continue;
            }

            var clusterRecord = new CognitiveMemoryQualityClusterRecord
            {
                Id = cluster.ClusterId.Value,
                ProjectId = cluster.ProjectId,
                ClusterHash = cluster.ClusterHash,
                PrimaryKeyFamily = cluster.PrimaryKeyFamily,
                Readiness = cluster.Readiness,
                AccessLevel = cluster.Members.Select(member => member.AccessLevel).DefaultIfEmpty(CognitiveMemoryAccessLevel.Project).Max(),
                RiskLevel = cluster.Members.Select(member => member.RiskLevel).DefaultIfEmpty(CognitiveMemoryRiskLevel.Low).Max(),
                PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
                AlgorithmVersion = AlgorithmVersion,
                KeyCount = cluster.Keys.Count,
                MemberCount = cluster.Members.Count,
                SourceEvidenceCount = cluster.Members.Count(member => member.EvidenceAnchorId is not null),
                ContradictionCount = cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory ? 1 : 0,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(clusterRecord);
            dbContext.AddRange(cluster.Keys.Select(key => new CognitiveMemoryQualityClusterKeyRecord
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterRecord.Id,
                ProjectId = cluster.ProjectId,
                KeyFamily = key.Family,
                Key = key.Key,
                DisplayText = key.DisplayText,
                CreatedAtUtc = nowUtc
            }));
            dbContext.AddRange(cluster.Members.Select(member => new CognitiveMemoryQualityClusterMemberRecord
            {
                Id = Guid.NewGuid(),
                ClusterId = clusterRecord.Id,
                ProjectId = cluster.ProjectId,
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
        }

        await dbContext.SaveChangesAsync(cancellationToken);
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

    private static CognitiveMemoryClusterMember ToClusterMember(
        CognitiveMemoryRecord record,
        CognitiveMemoryRecordSupport support)
    {
        var primarySourceItem = support.SourceItems.FirstOrDefault();
        var primaryEvidenceAnchor = support.EvidenceAnchors.FirstOrDefault();
        return new CognitiveMemoryClusterMember(
            CognitiveMemoryQualityClusterMemberKind.MemoryRecord,
            new CognitiveMemoryRecordId(record.Id),
            primarySourceItem is null ? null : new CognitiveMemorySourceItemId(primarySourceItem.Id),
            primaryEvidenceAnchor is null ? null : new CognitiveMemoryEvidenceAnchorId(primaryEvidenceAnchor.Id),
            record.Title,
            record.AccessLevel,
            record.RiskLevel,
            record.ValidationState,
            record.StabilityState);
    }

    private static CognitiveMemoryQualityClusterReadiness ResolveReadiness(
        IReadOnlyList<ClusterKeyEntry> members,
        HashSet<string> contradictionPairs)
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

        return members.All(member => member.Support.EvidenceAnchors.Count > 0)
            ? CognitiveMemoryQualityClusterReadiness.AggregateReady
            : CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence;
    }

    private static IReadOnlyList<string> ResolveClusterWarnings(
        CognitiveMemoryQualityClusterReadiness readiness,
        IReadOnlyList<CognitiveMemoryClusterMember> members)
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

public sealed class CognitiveMemoryDreamConsolidationService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryClusterPlanner clusterPlanner,
    ICognitiveMemoryDreamValidator validator,
    IClock clock) : ICognitiveMemoryDreamConsolidationService
{
    private const string AlgorithmVersion = "quality-dream-v1";

    public async ValueTask<CognitiveMemoryDreamRunResult> RunAsync(
        CognitiveMemoryDreamRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var stopwatch = Stopwatch.StartNew();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var existingRun = await dbContext.Set<CognitiveMemoryDreamRunRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(run => run.ProjectId == request.ProjectId && run.IdempotencyKey == request.IdempotencyKey.Value, cancellationToken);
        if (existingRun is not null)
        {
            var existingCandidates = await LoadCandidateContractsAsync(dbContext, existingRun.Id, cancellationToken);
            var warnings = new[] { $"Idempotent replay for dream run '{existingRun.Id:D}'." };
            return new CognitiveMemoryDreamRunResult(
                new CognitiveMemoryDreamRunId(existingRun.Id),
                existingRun.Status,
                ToMetrics(existingRun, stopwatch.Elapsed),
                existingCandidates,
                warnings);
        }

        var nowUtc = clock.GetUtcNow();
        var runRecord = new CognitiveMemoryDreamRunRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = request.ProjectId,
            Mode = request.Mode,
            TriggerKind = request.TriggerKind,
            Status = CognitiveMemoryRunStatus.Running,
            IdempotencyKey = request.IdempotencyKey.Value,
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            AlgorithmVersion = AlgorithmVersion,
            StartedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(runRecord);
        await dbContext.SaveChangesAsync(cancellationToken);

        var plannerResult = await clusterPlanner.PlanAsync(
            new CognitiveMemoryClusterPlanningRequest(
                request.ProjectId,
                request.PolicyContext,
                minMembers: request.MinMembersPerCluster,
                maxRecords: 1000,
                persistClusters: request.PersistChanges),
            cancellationToken);
        var selectedClusters = plannerResult.Clusters
            .Where(cluster => IsClusterSelectedForMode(request.Mode, cluster))
            .Take(request.MaxClusters)
            .ToArray();
        var warningsList = plannerResult.Warnings.ToList();
        if (selectedClusters.Length == 0)
        {
            warningsList.Add($"Dream mode '{request.Mode}' found no eligible clusters.");
        }

        var totalClaims = 0;
        var totalSourceMaps = 0;
        foreach (var cluster in selectedClusters)
        {
            var contract = await CreateAggregateCandidateAsync(dbContext, request, runRecord.Id, cluster, nowUtc, cancellationToken);
            totalClaims += contract.Claims.Count;
            totalSourceMaps += contract.Claims.Sum(claim => claim.SourceMaps.Count);
            dbContext.Add(new CognitiveMemoryDreamRunClusterRecord
            {
                Id = Guid.NewGuid(),
                DreamRunId = runRecord.Id,
                ClusterId = cluster.ClusterId.Value,
                ProjectId = request.ProjectId,
                Readiness = cluster.Readiness,
                SelectionReasonCode = ResolveSelectionReasonCode(request.Mode, cluster),
                MemberCount = cluster.Members.Count,
                ClaimCount = contract.Claims.Count,
                CreatedAtUtc = nowUtc
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var candidates = await LoadCandidateContractsAsync(dbContext, runRecord.Id, cancellationToken);
        var validationResults = new List<CognitiveMemoryDreamValidationResult>();
        foreach (var candidate in candidates)
        {
            validationResults.Add(await validator.ValidateAsync(
                new CognitiveMemoryDreamValidationRequest(candidate.Id, request.PolicyContext),
                cancellationToken));
        }

        var completedAtUtc = clock.GetUtcNow();
        dbContext.ChangeTracker.Clear();
        var runToUpdate = await dbContext.Set<CognitiveMemoryDreamRunRecord>()
            .SingleAsync(run => run.Id == runRecord.Id, cancellationToken);
        runToUpdate.Status = CognitiveMemoryRunStatus.Succeeded;
        runToUpdate.CompletedAtUtc = completedAtUtc;
        runToUpdate.ClustersConsidered = plannerResult.Metrics.ClustersCreated;
        runToUpdate.ClusterMembersRead = selectedClusters.Sum(cluster => cluster.Members.Count);
        runToUpdate.ClaimsExtracted = totalClaims;
        runToUpdate.AggregateCandidatesCreated = candidates.Count;
        runToUpdate.AggregateClaimsCreated = totalClaims;
        runToUpdate.AggregateClaimSourceMapsCreated = totalSourceMaps;
        runToUpdate.ValidationRecordsCreated = validationResults.Count;
        runToUpdate.ReviewItemsCreated = validationResults.Count(result => result.ReviewItemId is not null);
        runToUpdate.ApprovedCandidates = validationResults.Count(result => result.Decision == CognitiveMemoryDreamValidationDecision.Approved);
        runToUpdate.RejectedCandidates = validationResults.Count(result => result.Decision == CognitiveMemoryDreamValidationDecision.Rejected);
        runToUpdate.NeedsReviewCandidates = validationResults.Count(result => result.Decision == CognitiveMemoryDreamValidationDecision.NeedsHumanReview);
        runToUpdate.EvidenceCoverageRatio = totalClaims == 0
            ? 0
            : Math.Clamp((double)totalSourceMaps / totalClaims, 0, 1);
        runToUpdate.WarningsJson = SerializeStringArray(warningsList);
        runToUpdate.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);

        var refreshedCandidates = await LoadCandidateContractsAsync(dbContext, runRecord.Id, cancellationToken);
        return new CognitiveMemoryDreamRunResult(
            new CognitiveMemoryDreamRunId(runRecord.Id),
            CognitiveMemoryRunStatus.Succeeded,
            ToMetrics(runToUpdate, stopwatch.Elapsed),
            refreshedCandidates,
            warningsList);
    }

    private static async Task<CognitiveMemoryDreamAggregateCandidate> CreateAggregateCandidateAsync(
        AppDbContext dbContext,
        CognitiveMemoryDreamRunRequest request,
        Guid dreamRunId,
        CognitiveMemoryClusterPlan cluster,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var memoryRecordIds = cluster.Members
            .Where(member => member.MemoryRecordId is not null)
            .Select(member => member.MemoryRecordId!.Value.Value)
            .Distinct()
            .ToArray();
        var records = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record => memoryRecordIds.Contains(record.Id))
            .OrderBy(record => record.Title)
            .ToListAsync(cancellationToken);
        var support = await CognitiveMemoryQualitySupportLoader.LoadAsync(dbContext, memoryRecordIds, cancellationToken);
        var title = CognitiveMemoryQualityText.TrimText(
            $"{request.Mode} synthesis: {cluster.Keys.FirstOrDefault()?.DisplayText ?? records.FirstOrDefault()?.Title ?? "quality cluster"}",
            300);
        var canonicalText = BuildAggregateCanonicalText(records);
        var candidateId = Guid.NewGuid();
        var aggregateClaims = new List<CognitiveMemoryDreamAggregateClaim>();
        var sequence = 0;
        foreach (var record in records)
        {
            var recordSupport = support.ByRecordId.GetValueOrDefault(record.Id) ?? CognitiveMemoryRecordSupport.Empty(record.Id);
            var claimText = CognitiveMemoryQualityText.TrimText(FirstNonEmpty(record.SummaryText, record.CanonicalText, record.Title), 1200);
            if (string.IsNullOrWhiteSpace(claimText))
            {
                continue;
            }

            var aggregateClaimId = Guid.NewGuid();
            var sourceMaps = CreateSourceMaps(record, recordSupport);
            aggregateClaims.Add(new CognitiveMemoryDreamAggregateClaim(
                aggregateClaimId,
                ResolveAggregateClaimKind(request.Mode, record),
                claimText,
                CognitiveMemoryQualityText.TrimText(CognitiveMemoryQualityText.NormalizeKey(FirstNonEmpty(record.TopicKey, record.Title)), 240),
                "is-supported-by-cluster",
                CognitiveMemoryQualityText.TrimText(cluster.ClusterHash, 240),
                sourceMaps));
            dbContext.Add(new CognitiveMemoryDreamAggregateClaimRecord
            {
                Id = aggregateClaimId,
                AggregateCandidateId = candidateId,
                ProjectId = request.ProjectId,
                Sequence = sequence,
                ClaimKind = ResolveAggregateClaimKind(request.Mode, record),
                ClaimText = claimText,
                SubjectKey = CognitiveMemoryQualityText.TrimText(CognitiveMemoryQualityText.NormalizeKey(FirstNonEmpty(record.TopicKey, record.Title)), 240),
                PredicateKey = "is-supported-by-cluster",
                ObjectKey = CognitiveMemoryQualityText.TrimText(cluster.ClusterHash, 240),
                CreatedAtUtc = nowUtc
            });
            foreach (var sourceMap in sourceMaps)
            {
                dbContext.Add(new CognitiveMemoryDreamAggregateClaimSourceMapRecord
                {
                    Id = Guid.NewGuid(),
                    AggregateCandidateId = candidateId,
                    AggregateClaimId = aggregateClaimId,
                    ProjectId = request.ProjectId,
                    SourceMemoryRecordId = sourceMap.SourceMemoryRecordId.Value,
                    SourceItemId = sourceMap.SourceItemId?.Value,
                    EvidenceAnchorId = sourceMap.EvidenceAnchorId?.Value,
                    Direction = sourceMap.Direction,
                    AccessLevel = sourceMap.AccessLevel,
                    RedactionState = sourceMap.RedactionState,
                    Summary = sourceMap.Summary,
                    CreatedAtUtc = nowUtc
                });
            }

            sequence++;
        }

        var sourceMapCount = aggregateClaims.Sum(claim => claim.SourceMaps.Count);
        var accessLevel = aggregateClaims
            .SelectMany(claim => claim.SourceMaps)
            .Select(sourceMap => sourceMap.AccessLevel)
            .DefaultIfEmpty(CognitiveMemoryAccessLevel.Project)
            .Max();
        var riskLevel = records.Select(record => record.RiskLevel).DefaultIfEmpty(CognitiveMemoryRiskLevel.Low).Max();
        var payloadHash = CognitiveMemoryHash.FromUtf8($"{cluster.ClusterHash}|{canonicalText}|{sourceMapCount}").Value;
        dbContext.Add(new CognitiveMemoryDreamAggregateCandidateRecord
        {
            Id = candidateId,
            DreamRunId = dreamRunId,
            ClusterId = cluster.ClusterId.Value,
            ProjectId = request.ProjectId,
            Mode = request.Mode,
            Status = CognitiveMemoryDreamAggregateCandidateStatus.Proposed,
            Title = title,
            SummaryText = CognitiveMemoryQualityText.TrimText(canonicalText, 1200),
            CanonicalText = canonicalText,
            AccessLevel = accessLevel,
            RiskLevel = riskLevel,
            AlgorithmVersion = AlgorithmVersion,
            PayloadHash = payloadHash,
            ClaimCount = aggregateClaims.Count,
            SourceMapCount = sourceMapCount,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        });

        return new CognitiveMemoryDreamAggregateCandidate(
            new CognitiveMemoryDreamAggregateCandidateId(candidateId),
            new CognitiveMemoryDreamRunId(dreamRunId),
            cluster.ClusterId,
            request.ProjectId,
            request.Mode,
            CognitiveMemoryDreamAggregateCandidateStatus.Proposed,
            title,
            CognitiveMemoryQualityText.TrimText(canonicalText, 1200),
            canonicalText,
            accessLevel,
            riskLevel,
            aggregateClaims);
    }

    private static IReadOnlyList<CognitiveMemoryDreamAggregateSourceMap> CreateSourceMaps(
        CognitiveMemoryRecord record,
        CognitiveMemoryRecordSupport support)
    {
        var maps = new List<CognitiveMemoryDreamAggregateSourceMap>();
        foreach (var sourceLink in support.SourceLinks)
        {
            var sourceItem = support.SourceItems.FirstOrDefault(item => item.Id == sourceLink.SourceItemId);
            var evidenceAnchor = support.EvidenceAnchors.FirstOrDefault(anchor => anchor.SourceItemId == sourceLink.SourceItemId);
            maps.Add(new CognitiveMemoryDreamAggregateSourceMap(
                new CognitiveMemoryRecordId(record.Id),
                new CognitiveMemorySourceItemId(sourceLink.SourceItemId),
                evidenceAnchor is null ? null : new CognitiveMemoryEvidenceAnchorId(evidenceAnchor.Id),
                CognitiveMemoryEvidenceDirection.Supports,
                sourceItem?.AccessLevel ?? record.AccessLevel,
                sourceItem?.RedactionState ?? CognitiveMemoryRedactionState.Unclassified,
                CognitiveMemoryQualityText.Redact(sourceLink.Summary)));
        }

        foreach (var evidenceAnchor in support.EvidenceAnchors.Where(anchor => maps.All(map => map.EvidenceAnchorId?.Value != anchor.Id)))
        {
            maps.Add(new CognitiveMemoryDreamAggregateSourceMap(
                new CognitiveMemoryRecordId(record.Id),
                evidenceAnchor.SourceItemId is null ? null : new CognitiveMemorySourceItemId(evidenceAnchor.SourceItemId.Value),
                new CognitiveMemoryEvidenceAnchorId(evidenceAnchor.Id),
                CognitiveMemoryEvidenceDirection.Supports,
                CognitiveMemoryAccessLevel.Project,
                evidenceAnchor.RedactionState,
                CognitiveMemoryQualityText.Redact(evidenceAnchor.Locator)));
        }

        return maps;
    }

    private static async Task<IReadOnlyList<CognitiveMemoryDreamAggregateCandidate>> LoadCandidateContractsAsync(
        AppDbContext dbContext,
        Guid dreamRunId,
        CancellationToken cancellationToken)
    {
        var candidates = await dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .AsNoTracking()
            .Where(candidate => candidate.DreamRunId == dreamRunId)
            .OrderBy(candidate => candidate.Id)
            .ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return [];
        }

        var candidateIds = candidates.Select(candidate => candidate.Id).ToArray();
        var claims = await dbContext.Set<CognitiveMemoryDreamAggregateClaimRecord>()
            .AsNoTracking()
            .Where(claim => candidateIds.Contains(claim.AggregateCandidateId))
            .OrderBy(claim => claim.Sequence)
            .ToListAsync(cancellationToken);
        var sourceMaps = await dbContext.Set<CognitiveMemoryDreamAggregateClaimSourceMapRecord>()
            .AsNoTracking()
            .Where(sourceMap => candidateIds.Contains(sourceMap.AggregateCandidateId))
            .ToListAsync(cancellationToken);
        var sourceMapsByClaimId = sourceMaps
            .GroupBy(sourceMap => sourceMap.AggregateClaimId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var claimsByCandidateId = claims
            .GroupBy(claim => claim.AggregateCandidateId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        return candidates
            .Select(candidate => new CognitiveMemoryDreamAggregateCandidate(
                new CognitiveMemoryDreamAggregateCandidateId(candidate.Id),
                new CognitiveMemoryDreamRunId(candidate.DreamRunId),
                new CognitiveMemoryQualityClusterId(candidate.ClusterId),
                candidate.ProjectId,
                candidate.Mode,
                candidate.Status,
                candidate.Title,
                candidate.SummaryText,
                candidate.CanonicalText,
                candidate.AccessLevel,
                candidate.RiskLevel,
                (claimsByCandidateId.GetValueOrDefault(candidate.Id) ?? [])
                    .Select(claim => new CognitiveMemoryDreamAggregateClaim(
                        claim.Id,
                        claim.ClaimKind,
                        claim.ClaimText,
                        claim.SubjectKey,
                        claim.PredicateKey,
                        claim.ObjectKey,
                        (sourceMapsByClaimId.GetValueOrDefault(claim.Id) ?? [])
                            .Select(sourceMap => new CognitiveMemoryDreamAggregateSourceMap(
                                new CognitiveMemoryRecordId(sourceMap.SourceMemoryRecordId),
                                sourceMap.SourceItemId is null ? null : new CognitiveMemorySourceItemId(sourceMap.SourceItemId.Value),
                                sourceMap.EvidenceAnchorId is null ? null : new CognitiveMemoryEvidenceAnchorId(sourceMap.EvidenceAnchorId.Value),
                                sourceMap.Direction,
                                sourceMap.AccessLevel,
                                sourceMap.RedactionState,
                                sourceMap.Summary))
                            .ToArray()))
                    .ToArray()))
            .ToArray();
    }

    private static CognitiveMemoryDreamConsolidationMetrics ToMetrics(
        CognitiveMemoryDreamRunRecord run,
        TimeSpan elapsed)
        => new(
            run.ClustersConsidered,
            run.ClusterMembersRead,
            run.ClaimsExtracted,
            run.AggregateCandidatesCreated,
            run.AggregateClaimsCreated,
            run.AggregateClaimSourceMapsCreated,
            run.ValidationRecordsCreated,
            run.ReviewItemsCreated,
            run.ApprovedCandidates,
            run.RejectedCandidates,
            run.NeedsReviewCandidates,
            run.EvidenceCoverageRatio,
            elapsed);

    private static bool IsClusterSelectedForMode(
        CognitiveMemoryConsolidationMode mode,
        CognitiveMemoryClusterPlan cluster)
        => mode switch
        {
            CognitiveMemoryConsolidationMode.ProjectNightly => cluster.Readiness != CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence,
            CognitiveMemoryConsolidationMode.ProcedureMining => HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "procedure") ||
                                                               HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "workflow"),
            CognitiveMemoryConsolidationMode.FailureLearning => HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "failure") ||
                                                               cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory,
            CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh => cluster.Readiness is CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence
                or CognitiveMemoryQualityClusterReadiness.NeedsHumanReview,
            _ => true
        };

    private static string ResolveSelectionReasonCode(
        CognitiveMemoryConsolidationMode mode,
        CognitiveMemoryClusterPlan cluster)
        => mode switch
        {
            CognitiveMemoryConsolidationMode.ProjectNightly => "dream.project-nightly.aggregate-ready",
            CognitiveMemoryConsolidationMode.ProcedureMining => "dream.procedure-mining.task-intent",
            CognitiveMemoryConsolidationMode.FailureLearning => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory
                ? "dream.failure-learning.contradiction"
                : "dream.failure-learning.incident",
            CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh => "dream.knowledge-coverage.refresh",
            _ => $"dream.{mode.ToString().ToLowerInvariant()}"
        };

    private static bool HasKey(
        CognitiveMemoryClusterPlan cluster,
        CognitiveMemoryQualityClusterKeyFamily family,
        string value)
        => cluster.Keys.Any(key => key.Family == family && key.Key.Contains(value, StringComparison.OrdinalIgnoreCase));

    private static CognitiveMemoryClaimKind ResolveAggregateClaimKind(
        CognitiveMemoryConsolidationMode mode,
        CognitiveMemoryRecord record)
        => mode switch
        {
            CognitiveMemoryConsolidationMode.ProcedureMining => CognitiveMemoryClaimKind.ProcedureConstraint,
            CognitiveMemoryConsolidationMode.FailureLearning => CognitiveMemoryClaimKind.FailureMode,
            _ => record.Kind switch
            {
                CognitiveMemoryRecordKind.Decision => CognitiveMemoryClaimKind.Decision,
                CognitiveMemoryRecordKind.Procedural => CognitiveMemoryClaimKind.ProcedureConstraint,
                CognitiveMemoryRecordKind.Episodic => CognitiveMemoryClaimKind.Observation,
                _ => CognitiveMemoryClaimKind.Fact
            }
        };

    private static string BuildAggregateCanonicalText(IReadOnlyList<CognitiveMemoryRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Synthesis from {records.Count} source-backed memory record(s).");
        foreach (var record in records.Take(8))
        {
            var text = CognitiveMemoryQualityText.TrimText(FirstNonEmpty(record.SummaryText, record.CanonicalText, record.Title), 300);
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine($"- {text}");
            }
        }

        return builder.ToString().Trim();
    }

    private static string SerializeStringArray(IReadOnlyList<string> values)
        => JsonSerializer.Serialize(values.ToArray(), CognitiveMemoryJsonSerializerContext.Default.StringArray);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}

public sealed class CognitiveMemoryDreamValidator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryDreamValidator
{
    public async ValueTask<CognitiveMemoryDreamValidationResult> ValidateAsync(
        CognitiveMemoryDreamValidationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.AggregateCandidateId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Dream aggregate candidate '{request.AggregateCandidateId}' was not found.");
        var claims = await dbContext.Set<CognitiveMemoryDreamAggregateClaimRecord>()
            .Where(claim => claim.AggregateCandidateId == candidate.Id)
            .OrderBy(claim => claim.Sequence)
            .ToListAsync(cancellationToken);
        var sourceMaps = await dbContext.Set<CognitiveMemoryDreamAggregateClaimSourceMapRecord>()
            .Where(sourceMap => sourceMap.AggregateCandidateId == candidate.Id)
            .ToListAsync(cancellationToken);
        var sourceRecordIds = sourceMaps.Select(sourceMap => sourceMap.SourceMemoryRecordId).Distinct().ToArray();
        var sourceRecords = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record => sourceRecordIds.Contains(record.Id))
            .ToListAsync(cancellationToken);
        var issues = ResolveIssues(candidate, claims, sourceMaps, sourceRecords, request.PolicyContext);
        var decision = ResolveDecision(issues);
        var nowUtc = clock.GetUtcNow();
        var validation = new CognitiveMemoryDreamValidationRecord
        {
            Id = Guid.NewGuid(),
            AggregateCandidateId = candidate.Id,
            ProjectId = candidate.ProjectId,
            Decision = decision,
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            IssueCount = issues.Count,
            ClaimsChecked = claims.Count,
            SourceMapsChecked = sourceMaps.Count,
            IssuesJson = JsonSerializer.Serialize(issues.ToArray(), CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryDreamValidationIssueArray),
            CreatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(validation);

        Guid? reviewItemId = null;
        if (decision == CognitiveMemoryDreamValidationDecision.NeedsHumanReview && request.CreateReviewItemWhenNeeded)
        {
            var reviewItem = new CognitiveMemoryReviewItemRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = candidate.ProjectId,
                ReviewKind = CognitiveMemoryReviewKind.GeneratedMemory,
                Status = CognitiveMemoryReviewStatus.Pending,
                SubjectKind = CognitiveMemoryReviewSubjectKind.Run,
                SubjectId = candidate.DreamRunId,
                RiskLevel = issues.Select(issue => issue.RiskLevel).DefaultIfEmpty(CognitiveMemoryRiskLevel.Medium).Max(),
                ReasonCode = "dream.aggregate.validation",
                ReasonText = string.Join("; ", issues.Select(issue => issue.Message).Distinct(StringComparer.Ordinal)),
                SourceEvidenceCount = sourceMaps.Count,
                CreatedAtUtc = nowUtc,
                DecidedByActorId = string.Empty,
                DecisionNotes = string.Empty,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(reviewItem);
            reviewItemId = reviewItem.Id;
            candidate.ReviewItemId = reviewItem.Id;
        }

        candidate.ValidationRecordId = validation.Id;
        candidate.Status = decision switch
        {
            CognitiveMemoryDreamValidationDecision.Approved => CognitiveMemoryDreamAggregateCandidateStatus.Approved,
            CognitiveMemoryDreamValidationDecision.Rejected => CognitiveMemoryDreamAggregateCandidateStatus.Rejected,
            _ => CognitiveMemoryDreamAggregateCandidateStatus.NeedsHumanReview
        };
        candidate.UpdatedAtUtc = nowUtc;
        candidate.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryDreamValidationResult(request.AggregateCandidateId, decision, issues, reviewItemId);
    }

    private static IReadOnlyList<CognitiveMemoryDreamValidationIssue> ResolveIssues(
        CognitiveMemoryDreamAggregateCandidateRecord candidate,
        IReadOnlyList<CognitiveMemoryDreamAggregateClaimRecord> claims,
        IReadOnlyList<CognitiveMemoryDreamAggregateClaimSourceMapRecord> sourceMaps,
        IReadOnlyList<CognitiveMemoryRecord> sourceRecords,
        CognitiveMemoryPolicyContext policyContext)
    {
        var issues = new List<CognitiveMemoryDreamValidationIssue>();
        foreach (var claim in claims)
        {
            if (sourceMaps.All(sourceMap => sourceMap.AggregateClaimId != claim.Id))
            {
                issues.Add(new CognitiveMemoryDreamValidationIssue(
                    CognitiveMemoryDreamValidationIssueKind.MissingSourceMap,
                    CognitiveMemoryRiskLevel.High,
                    $"Aggregate claim '{claim.Id:D}' has no claim-level source map."));
            }
        }

        if (sourceMaps.Select(sourceMap => sourceMap.SourceMemoryRecordId).Distinct().Count() < 2)
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.WeakEvidence,
                CognitiveMemoryRiskLevel.Medium,
                "Aggregate candidate has fewer than two independent source memories."));
        }

        if (sourceMaps.Any(sourceMap => sourceMap.Direction == CognitiveMemoryEvidenceDirection.Attacks))
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.Contradiction,
                CognitiveMemoryRiskLevel.High,
                "Aggregate candidate includes attacking or contradictory source evidence."));
        }

        if (sourceRecords.Any(record => record.ValidationState is CognitiveMemoryValidationState.Superseded or CognitiveMemoryValidationState.Rejected ||
                                        record.StabilityState is CognitiveMemoryStabilityState.Stale or CognitiveMemoryStabilityState.Deprecated))
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.StaleOrSuperseded,
                CognitiveMemoryRiskLevel.Medium,
                "Aggregate candidate depends on stale, superseded, or rejected source memory."));
        }

        if (sourceMaps.Any(sourceMap => sourceMap.AccessLevel == CognitiveMemoryAccessLevel.Restricted))
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.RestrictedContent,
                CognitiveMemoryRiskLevel.High,
                "Aggregate candidate includes restricted source mappings and requires explicit review."));
        }

        if (sourceMaps.Any(sourceMap => sourceMap.RedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted))
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.RedactedSource,
                CognitiveMemoryRiskLevel.High,
                "Aggregate candidate includes redacted or restricted source evidence."));
        }

        if (!CognitiveMemoryQualityText.PolicyCanRead(candidate.AccessLevel, policyContext))
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.AccessPolicy,
                CognitiveMemoryRiskLevel.High,
                "Policy context cannot read the aggregate candidate access level."));
        }

        if (sourceRecords.Count > 0 && sourceRecords.All(record => record.Origin == CognitiveMemoryRecordOrigin.MachineGenerated))
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.GeneratedTextLeakage,
                CognitiveMemoryRiskLevel.Medium,
                "Aggregate candidate is supported only by machine-generated memory records."));
        }

        return issues
            .GroupBy(issue => issue.IssueKind)
            .Select(group => group.First())
            .ToArray();
    }

    private static CognitiveMemoryDreamValidationDecision ResolveDecision(
        IReadOnlyList<CognitiveMemoryDreamValidationIssue> issues)
    {
        if (issues.Any(issue => issue.IssueKind == CognitiveMemoryDreamValidationIssueKind.MissingSourceMap))
        {
            return CognitiveMemoryDreamValidationDecision.Rejected;
        }

        return issues.Count == 0
            ? CognitiveMemoryDreamValidationDecision.Approved
            : CognitiveMemoryDreamValidationDecision.NeedsHumanReview;
    }
}

public sealed class CognitiveMemoryAggregateMemoryApplicator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryRecordValidator recordValidator,
    IClock clock) : ICognitiveMemoryAggregateMemoryApplicator
{
    private const string AlgorithmVersion = "quality-aggregate-apply-v1";

    public async ValueTask<CognitiveMemoryAggregateMemoryApplyResult> ApplyAsync(
        CognitiveMemoryAggregateMemoryApplyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorId);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .SingleOrDefaultAsync(candidate => candidate.Id == request.AggregateCandidateId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Dream aggregate candidate '{request.AggregateCandidateId}' was not found.");
        if (candidate.MemoryRecordId is { } existingMemoryId)
        {
            var existingClaims = await dbContext.Set<CognitiveMemoryClaimRecord>()
                .AsNoTracking()
                .Where(claim => claim.MemoryRecordId == existingMemoryId)
                .Select(claim => new CognitiveMemoryClaimId(claim.Id))
                .ToArrayAsync(cancellationToken);
            return new CognitiveMemoryAggregateMemoryApplyResult(new CognitiveMemoryRecordId(existingMemoryId), existingClaims, Created: false);
        }

        if (candidate.Status != CognitiveMemoryDreamAggregateCandidateStatus.Approved)
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' must be approved before it can be applied.");
        }

        var validation = (await dbContext.Set<CognitiveMemoryDreamValidationRecord>()
            .AsNoTracking()
            .Where(validation => validation.AggregateCandidateId == candidate.Id)
            .ToListAsync(cancellationToken))
            .OrderByDescending(validation => validation.CreatedAtUtc)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' has no validation record.");
        if (validation.Decision != CognitiveMemoryDreamValidationDecision.Approved)
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' validation decision is '{validation.Decision}'.");
        }

        var claims = await dbContext.Set<CognitiveMemoryDreamAggregateClaimRecord>()
            .Where(claim => claim.AggregateCandidateId == candidate.Id)
            .OrderBy(claim => claim.Sequence)
            .ToListAsync(cancellationToken);
        var sourceMaps = await dbContext.Set<CognitiveMemoryDreamAggregateClaimSourceMapRecord>()
            .Where(sourceMap => sourceMap.AggregateCandidateId == candidate.Id)
            .ToListAsync(cancellationToken);
        if (claims.Count == 0 || claims.Any(claim => sourceMaps.All(sourceMap => sourceMap.AggregateClaimId != claim.Id)))
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' cannot be applied because claim source maps are incomplete.");
        }

        var sourceItemIds = sourceMaps
            .Select(sourceMap => sourceMap.SourceItemId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        if (sourceItemIds.Length == 0)
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' cannot be applied without source item mappings.");
        }

        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => sourceItemIds.Contains(item.Id))
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (sourceItems.Count != sourceItemIds.Length)
        {
            throw new InvalidOperationException($"Dream aggregate candidate '{candidate.Id:D}' references missing source items.");
        }

        var nowUtc = clock.GetUtcNow();
        var contextFrame = new CognitiveMemoryContextFrameRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = candidate.ProjectId,
            FrameKind = CognitiveMemoryContextFrameKind.Composite,
            DisplayName = candidate.Title,
            ConfidenceBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        var memory = new CognitiveMemoryRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = candidate.ProjectId,
            Kind = ResolveRecordKind(candidate.Mode),
            Origin = CognitiveMemoryRecordOrigin.MachineGenerated,
            Title = candidate.Title,
            CanonicalText = candidate.CanonicalText,
            SummaryText = candidate.SummaryText,
            TopicKey = CognitiveMemoryQualityText.TrimText(CognitiveMemoryQualityText.NormalizeKey(candidate.Title), 240),
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Consolidate,
            AlgorithmVersion = AlgorithmVersion,
            ContentHash = CognitiveMemoryHash.FromUtf8($"{candidate.Id:D}|{candidate.PayloadHash}|{candidate.CanonicalText}").Value,
            SourceEvidenceCount = sourceItemIds.Length,
            EvidenceAnchorCount = sourceMaps.Select(sourceMap => sourceMap.EvidenceAnchorId).Where(id => id is not null).Distinct().Count(),
            GeneratedReason = CognitiveMemoryQualityText.TrimText($"Approved dream aggregate candidate {candidate.Id:D}.", 500),
            PrimaryContextFrameId = contextFrame.Id,
            ConfidenceBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            ActivationBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
            AccessLevel = candidate.AccessLevel,
            RiskLevel = candidate.RiskLevel,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        var validationResult = recordValidator.ValidateForPersistence(memory);
        if (validationResult.IsFailure)
        {
            throw new InvalidOperationException($"Generated aggregate memory record is invalid: {string.Join(", ", validationResult.Errors.Select(error => error.Code))}.");
        }

        dbContext.Add(contextFrame);
        dbContext.Add(memory);
        var createdClaimIds = new List<CognitiveMemoryClaimId>();
        foreach (var aggregateClaim in claims)
        {
            var claim = new CognitiveMemoryClaimRecord
            {
                Id = Guid.NewGuid(),
                ProjectId = candidate.ProjectId,
                MemoryRecordId = memory.Id,
                ClaimKind = aggregateClaim.ClaimKind,
                ClaimText = aggregateClaim.ClaimText,
                SubjectKey = aggregateClaim.SubjectKey,
                PredicateKey = aggregateClaim.PredicateKey,
                ObjectKey = aggregateClaim.ObjectKey,
                PrimaryContextFrameId = contextFrame.Id,
                CurrentBeliefState = CognitiveMemoryBeliefStateKind.Validated,
                CurrentBeliefBucket = CognitiveMemoryScoreProjectionBucket.StrongAccept,
                DisplayBeliefScore = 1,
                ValidationState = CognitiveMemoryValidationState.Approved,
                StabilityState = CognitiveMemoryStabilityState.Active,
                AlgorithmVersion = AlgorithmVersion,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                ConcurrencyToken = Guid.NewGuid()
            };
            dbContext.Add(claim);
            createdClaimIds.Add(new CognitiveMemoryClaimId(claim.Id));
            foreach (var sourceMap in sourceMaps.Where(sourceMap => sourceMap.AggregateClaimId == aggregateClaim.Id && sourceMap.EvidenceAnchorId is not null))
            {
                dbContext.Add(new CognitiveMemoryClaimEvidenceLinkRecord
                {
                    Id = Guid.NewGuid(),
                    ClaimId = claim.Id,
                    EvidenceAnchorId = sourceMap.EvidenceAnchorId!.Value,
                    Direction = sourceMap.Direction,
                    Explanation = sourceMap.Summary,
                    CreatedAtUtc = nowUtc
                });
            }
        }

        foreach (var sourceItemId in sourceItemIds)
        {
            var sourceItem = sourceItems[sourceItemId];
            var firstMap = sourceMaps.First(sourceMap => sourceMap.SourceItemId == sourceItemId);
            dbContext.Add(new CognitiveMemorySourceLinkRecord
            {
                Id = Guid.NewGuid(),
                MemoryRecordId = memory.Id,
                SourceManifestId = sourceItem.SourceManifestId,
                SourceItemId = sourceItem.Id,
                EvidenceRole = CognitiveMemoryEvidenceRole.SupportingSource,
                Locator = sourceItem.Locator,
                Summary = firstMap.Summary,
                CreatedAtUtc = nowUtc
            });
        }

        foreach (var sourceMap in sourceMaps.Where(sourceMap => sourceMap.EvidenceAnchorId is not null)
            .GroupBy(sourceMap => sourceMap.EvidenceAnchorId!.Value)
            .Select(group => group.First()))
        {
            dbContext.Add(new CognitiveMemoryRecordEvidenceAnchorRecord
            {
                Id = Guid.NewGuid(),
                MemoryRecordId = memory.Id,
                EvidenceAnchorId = sourceMap.EvidenceAnchorId!.Value,
                EvidenceRole = CognitiveMemoryEvidenceRole.SupportingSource,
                Summary = sourceMap.Summary,
                CreatedAtUtc = nowUtc
            });
        }

        dbContext.Add(new CognitiveMemoryMutationCommandRecord
        {
            Id = Guid.NewGuid(),
            ProjectId = candidate.ProjectId,
            CommandKind = CognitiveMemoryMutationCommandKind.ProposeClaim,
            Status = CognitiveMemoryMutationCommandStatus.Accepted,
            ActorKind = CognitiveMemoryActorKind.System,
            ActorId = request.ActorId.Trim(),
            IdempotencyKey = $"dream-aggregate-apply:{candidate.Id:D}",
            AffectedMemoryRecordIdsJson = JsonSerializer.Serialize(new[] { memory.Id }, CognitiveMemoryJsonSerializerContext.Default.GuidArray),
            AffectedClaimIdsJson = JsonSerializer.Serialize(createdClaimIds.Select(id => id.Value).ToArray(), CognitiveMemoryJsonSerializerContext.Default.GuidArray),
            EvidenceAnchorIdsJson = JsonSerializer.Serialize(sourceMaps.Select(sourceMap => sourceMap.EvidenceAnchorId).Where(id => id is not null).Select(id => id!.Value).Distinct().ToArray(), CognitiveMemoryJsonSerializerContext.Default.GuidArray),
            PayloadJson = "{}",
            ExpectedVersionToken = string.Empty,
            RequiresHumanReview = false,
            ReviewReason = string.Empty,
            ResultVersionToken = memory.ConcurrencyToken.ToString("D"),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        });

        candidate.Status = CognitiveMemoryDreamAggregateCandidateStatus.Applied;
        candidate.MemoryRecordId = memory.Id;
        candidate.UpdatedAtUtc = nowUtc;
        candidate.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryAggregateMemoryApplyResult(new CognitiveMemoryRecordId(memory.Id), createdClaimIds, Created: true);
    }

    private static CognitiveMemoryRecordKind ResolveRecordKind(CognitiveMemoryConsolidationMode mode)
        => mode switch
        {
            CognitiveMemoryConsolidationMode.ProcedureMining => CognitiveMemoryRecordKind.Procedural,
            CognitiveMemoryConsolidationMode.FailureLearning => CognitiveMemoryRecordKind.Reflection,
            _ => CognitiveMemoryRecordKind.Semantic
        };
}

public sealed class CognitiveMemoryRecallSynthesisService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryRecallSynthesisService
{
    public async ValueTask<CognitiveMemorySynthesizedRecallResult> SynthesizeAsync(
        CognitiveMemoryRecallSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaxStatements <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Recall synthesis statement budget must be positive.");
        }

        var selectedSections = request.RecallResult.ContextPack.Sections
            .Where(section => section.SectionKind == CognitiveMemoryRecallContextSectionKind.SelectedMemory)
            .Take(request.MaxStatements)
            .ToArray();
        var warnings = new List<string>();
        if (selectedSections.Length == 0)
        {
            warnings.Add("Recall synthesis received no selected memory sections.");
        }

        var statements = selectedSections
            .Select(section => new CognitiveMemorySynthesizedRecallStatement(
                CognitiveMemorySynthesizedStatementId.New(),
                CognitiveMemoryQualityText.TrimText(ExtractStatementText(section), 900),
                section.SourceRefs
                    .Where(sourceRef => sourceRef.IncludedInContext && CognitiveMemoryQualityText.PolicyCanRead(sourceRef.AccessLevel, request.PolicyContext))
                    .ToArray()))
            .Where(statement => !string.IsNullOrWhiteSpace(statement.Text))
            .ToArray();
        var brief = statements.Length == 0
            ? "No source-backed recall statements were synthesized."
            : string.Join(Environment.NewLine, statements.Select(statement => $"- {statement.Text}"));
        var synthesisId = CognitiveMemorySynthesizedRecallId.New();

        if (request.PersistSynthesis)
        {
            await PersistAsync(request, synthesisId, brief, statements, cancellationToken);
        }

        return new CognitiveMemorySynthesizedRecallResult(
            synthesisId,
            request.RecallResult.ContextPack.ProjectId,
            request.RecallResult.TraceId,
            brief,
            statements,
            ReferencesShownByDefault: false,
            warnings);
    }

    private async Task PersistAsync(
        CognitiveMemoryRecallSynthesisRequest request,
        CognitiveMemorySynthesizedRecallId synthesisId,
        string brief,
        IReadOnlyList<CognitiveMemorySynthesizedRecallStatement> statements,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var traceExists = await dbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .AnyAsync(trace => trace.Id == request.RecallResult.TraceId, cancellationToken);
        if (!traceExists)
        {
            throw new InvalidOperationException($"Recall trace '{request.RecallResult.TraceId:D}' was not found for synthesis persistence.");
        }

        var nowUtc = clock.GetUtcNow();
        dbContext.Add(new CognitiveMemorySynthesizedRecallRecord
        {
            Id = synthesisId.Value,
            ProjectId = request.RecallResult.ContextPack.ProjectId,
            RecallTraceId = request.RecallResult.TraceId,
            Brief = brief,
            ReferencesShownByDefault = false,
            StatementCount = statements.Count,
            SourceMapCount = statements.Sum(statement => statement.SourceRefs.Count),
            CreatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        });

        var sequence = 0;
        foreach (var statement in statements)
        {
            dbContext.Add(new CognitiveMemorySynthesizedStatementRecord
            {
                Id = statement.StatementId.Value,
                SynthesisId = synthesisId.Value,
                ProjectId = request.RecallResult.ContextPack.ProjectId,
                Sequence = sequence,
                Text = statement.Text,
                CreatedAtUtc = nowUtc
            });
            foreach (var sourceRef in statement.SourceRefs)
            {
                dbContext.Add(new CognitiveMemorySynthesizedStatementSourceMapRecord
                {
                    Id = Guid.NewGuid(),
                    SynthesisId = synthesisId.Value,
                    StatementId = statement.StatementId.Value,
                    ProjectId = request.RecallResult.ContextPack.ProjectId,
                    MemoryRecordId = sourceRef.MemoryRecordId.Value,
                    SourceItemId = sourceRef.SourceItemId?.Value,
                    EvidenceAnchorId = sourceRef.EvidenceAnchorId?.Value,
                    SourceSystem = sourceRef.SourceSystem,
                    Locator = sourceRef.Locator,
                    Summary = sourceRef.Summary,
                    AccessLevel = sourceRef.AccessLevel,
                    RedactionState = sourceRef.RedactionState,
                    CreatedAtUtc = nowUtc
                });
            }

            sequence++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ExtractStatementText(CognitiveMemoryRecallContextSection section)
    {
        var content = section.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (content.Length == 0)
        {
            return section.Title;
        }

        var firstLine = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstLine)
            ? section.Title
            : firstLine;
    }
}

public sealed class CognitiveMemoryReferenceResolver(
    IDbContextFactory<AppDbContext> dbContextFactory) : ICognitiveMemoryReferenceResolver
{
    public async ValueTask<CognitiveMemoryReferenceResolverResult> ResolveAsync(
        CognitiveMemoryReferenceResolverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.Set<CognitiveMemorySynthesizedStatementSourceMapRecord>()
            .AsNoTracking()
            .Where(sourceMap => sourceMap.StatementId == request.StatementId.Value)
            .OrderBy(sourceMap => sourceMap.SourceSystem)
            .ThenBy(sourceMap => sourceMap.Locator)
            .ToListAsync(cancellationToken);
        var warnings = new List<string>();
        if (rows.Count == 0)
        {
            warnings.Add($"No reference source maps exist for synthesized statement '{request.StatementId}'.");
        }

        var references = rows.Select(row =>
        {
            var included = CanResolve(row, request);
            return new CognitiveMemoryResolvedReference(
                request.StatementId,
                new CognitiveMemoryRecordId(row.MemoryRecordId),
                row.SourceItemId is null ? null : new CognitiveMemorySourceItemId(row.SourceItemId.Value),
                row.EvidenceAnchorId is null ? null : new CognitiveMemoryEvidenceAnchorId(row.EvidenceAnchorId.Value),
                row.SourceSystem,
                included ? row.Locator : string.Empty,
                included ? CognitiveMemoryQualityText.Redact(row.Summary) : string.Empty,
                included,
                included ? CognitiveMemoryRecallExclusionReasonKind.None : ResolveExclusion(row, request.PolicyContext));
        }).ToArray();
        return new CognitiveMemoryReferenceResolverResult(references, warnings);
    }

    private static bool CanResolve(
        CognitiveMemorySynthesizedStatementSourceMapRecord row,
        CognitiveMemoryReferenceResolverRequest request)
    {
        if (!CognitiveMemoryQualityText.PolicyCanRead(row.AccessLevel, request.PolicyContext))
        {
            return false;
        }

        return row.RedactionState switch
        {
            CognitiveMemoryRedactionState.Safe or CognitiveMemoryRedactionState.Unclassified => true,
            CognitiveMemoryRedactionState.Restricted => request.IncludeRestrictedContent && request.PolicyContext.AllowRestrictedContent,
            _ => false
        };
    }

    private static CognitiveMemoryRecallExclusionReasonKind ResolveExclusion(
        CognitiveMemorySynthesizedStatementSourceMapRecord row,
        CognitiveMemoryPolicyContext policyContext)
    {
        if (!CognitiveMemoryQualityText.PolicyCanRead(row.AccessLevel, policyContext))
        {
            return CognitiveMemoryRecallExclusionReasonKind.AccessPolicy;
        }

        return CognitiveMemoryRecallExclusionReasonKind.RedactedSource;
    }
}

internal sealed record CognitiveMemoryRecordSupport(
    Guid RecordId,
    IReadOnlyList<CognitiveMemorySourceLinkRecord> SourceLinks,
    IReadOnlyList<CognitiveMemorySourceItemRecord> SourceItems,
    IReadOnlyList<CognitiveMemoryRecordEvidenceAnchorRecord> EvidenceLinks,
    IReadOnlyList<CognitiveMemoryEvidenceAnchorRecord> EvidenceAnchors,
    IReadOnlyList<CognitiveMemoryClaimRecord> Claims)
{
    public CognitiveMemoryRedactionState HighestRedactionState
        => SourceItems
            .Select(item => item.RedactionState)
            .Concat(EvidenceAnchors.Select(anchor => anchor.RedactionState))
            .DefaultIfEmpty(CognitiveMemoryRedactionState.Unclassified)
            .Max();

    public static CognitiveMemoryRecordSupport Empty(Guid recordId)
        => new(recordId, [], [], [], [], []);
}

internal sealed record CognitiveMemorySupportSnapshot(
    IReadOnlyDictionary<Guid, CognitiveMemoryRecordSupport> ByRecordId,
    IReadOnlyDictionary<Guid, CognitiveMemorySourceItemRecord> SourceItemsById);

internal static class CognitiveMemoryQualitySupportLoader
{
    public static async Task<CognitiveMemorySupportSnapshot> LoadAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> memoryRecordIds,
        CancellationToken cancellationToken)
    {
        if (memoryRecordIds.Count == 0)
        {
            return new CognitiveMemorySupportSnapshot(
                new Dictionary<Guid, CognitiveMemoryRecordSupport>(),
                new Dictionary<Guid, CognitiveMemorySourceItemRecord>());
        }

        var sourceLinks = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => memoryRecordIds.Contains(link.MemoryRecordId))
            .ToListAsync(cancellationToken);
        var sourceItemIds = sourceLinks
            .Select(link => link.SourceItemId)
            .Distinct()
            .ToArray();
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => sourceItemIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var evidenceLinks = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(link => memoryRecordIds.Contains(link.MemoryRecordId))
            .ToListAsync(cancellationToken);
        var evidenceAnchorIds = evidenceLinks
            .Select(link => link.EvidenceAnchorId)
            .Distinct()
            .ToArray();
        var evidenceAnchors = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(anchor => evidenceAnchorIds.Contains(anchor.Id) || (anchor.SourceItemId != null && sourceItemIds.Contains(anchor.SourceItemId.Value)))
            .ToListAsync(cancellationToken);
        var claims = await dbContext.Set<CognitiveMemoryClaimRecord>()
            .AsNoTracking()
            .Where(claim => claim.MemoryRecordId != null && memoryRecordIds.Contains(claim.MemoryRecordId.Value))
            .ToListAsync(cancellationToken);
        var sourceItemsById = sourceItems.ToDictionary(item => item.Id);
        var evidenceAnchorsBySourceItemId = evidenceAnchors
            .Where(anchor => anchor.SourceItemId is not null)
            .GroupBy(anchor => anchor.SourceItemId!.Value)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var supportByRecordId = new Dictionary<Guid, CognitiveMemoryRecordSupport>();
        foreach (var memoryRecordId in memoryRecordIds)
        {
            var linksForRecord = sourceLinks.Where(link => link.MemoryRecordId == memoryRecordId).ToArray();
            var sourceItemsForRecord = linksForRecord
                .Select(link => sourceItemsById.GetValueOrDefault(link.SourceItemId))
                .OfType<CognitiveMemorySourceItemRecord>()
                .ToArray();
            var evidenceLinksForRecord = evidenceLinks.Where(link => link.MemoryRecordId == memoryRecordId).ToArray();
            var evidenceAnchorIdsForRecord = evidenceLinksForRecord.Select(link => link.EvidenceAnchorId).ToHashSet();
            foreach (var sourceItem in sourceItemsForRecord)
            {
                if (!evidenceAnchorsBySourceItemId.TryGetValue(sourceItem.Id, out var anchors))
                {
                    continue;
                }

                foreach (var anchor in anchors)
                {
                    evidenceAnchorIdsForRecord.Add(anchor.Id);
                }
            }

            var evidenceAnchorsForRecord = evidenceAnchors
                .Where(anchor => evidenceAnchorIdsForRecord.Contains(anchor.Id))
                .ToArray();
            supportByRecordId[memoryRecordId] = new CognitiveMemoryRecordSupport(
                memoryRecordId,
                linksForRecord,
                sourceItemsForRecord,
                evidenceLinksForRecord,
                evidenceAnchorsForRecord,
                claims.Where(claim => claim.MemoryRecordId == memoryRecordId).ToArray());
        }

        return new CognitiveMemorySupportSnapshot(supportByRecordId, sourceItemsById);
    }
}

internal static partial class CognitiveMemoryQualityText
{
    private static readonly Regex EmailRegex = CreateEmailRegex();
    private static readonly Regex PhoneRegex = CreatePhoneRegex();

    private static readonly IReadOnlySet<string> StopWords = new HashSet<string>([
        "about",
        "after",
        "and",
        "are",
        "for",
        "from",
        "has",
        "into",
        "must",
        "not",
        "the",
        "this",
        "use",
        "uses",
        "with"
    ], StringComparer.Ordinal);

    public static string NormalizeKey(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                continue;
            }

            if (builder.Length > 0 && builder[^1] != '.')
            {
                builder.Append('.');
            }
        }

        var normalized = builder.ToString().Trim('.');
        return string.IsNullOrWhiteSpace(normalized)
            ? "unknown"
            : normalized;
    }

    public static IReadOnlyList<string> ExtractMeaningfulTokens(string text, int maxTokens)
        => Regex.Split(text.ToLowerInvariant(), "[^\\p{L}\\p{Nd}]+")
            .Where(token => token.Length >= 4 && !StopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .Take(maxTokens)
            .ToArray();

    public static IReadOnlyList<string> ResolveTaskIntents(string text)
    {
        var normalized = text.ToLowerInvariant();
        var intents = new List<string>();
        AddIfAny("procedure", ["procedure", "runbook", "step", "checklist"]);
        AddIfAny("workflow", ["workflow", "process", "automation"]);
        AddIfAny("failure", ["failure", "error", "incident", "rollback", "bug"]);
        AddIfAny("decision", ["decision", "approved", "chosen", "tradeoff"]);
        AddIfAny("testing", ["test", "validation", "verify", "regression"]);
        AddIfAny("architecture", ["architecture", "design", "component", "module"]);
        AddIfAny("deployment", ["deploy", "release", "production", "docker"]);
        AddIfAny("coverage", ["coverage", "missing", "gap", "refresh"]);
        return intents.Count == 0 ? ["general"] : intents;

        void AddIfAny(string intent, IReadOnlyList<string> terms)
        {
            if (terms.Any(term => normalized.Contains(term, StringComparison.Ordinal)))
            {
                intents.Add(intent);
            }
        }
    }

    public static string Redact(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var builder = new StringBuilder(text.Length);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            builder.AppendLine(EmailRegex.IsMatch(trimmed) || PhoneRegex.IsMatch(trimmed)
                ? "[redacted-contact]"
                : trimmed);
        }

        return builder.ToString().Trim();
    }

    public static string TrimText(string? value, int maxLength)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        return normalized.Length <= maxLength
            ? normalized
            : normalized[..maxLength];
    }

    public static bool PolicyCanRead(
        CognitiveMemoryAccessLevel accessLevel,
        CognitiveMemoryPolicyContext policyContext)
        => accessLevel <= policyContext.AccessLevel ||
           accessLevel == CognitiveMemoryAccessLevel.Restricted && policyContext.AllowRestrictedContent;

    [GeneratedRegex("[A-Z0-9._%+-]+@[A-Z0-9.-]+\\.[A-Z]{2,}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CreateEmailRegex();

    [GeneratedRegex("\\+?\\d[\\d\\s().-]{7,}\\d", RegexOptions.CultureInvariant)]
    private static partial Regex CreatePhoneRegex();
}
