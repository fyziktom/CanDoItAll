using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
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
        var modePolicy = CognitiveMemoryDreamModePolicy.Resolve(request.Mode);

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
        if (!request.PersistChanges)
        {
            var dryRunPlannerResult = await clusterPlanner.PlanAsync(
                new CognitiveMemoryClusterPlanningRequest(
                    request.ProjectId,
                    request.PolicyContext,
                    minMembers: request.MinMembersPerCluster,
                    maxRecords: 1000,
                    persistClusters: false),
                cancellationToken);
            var dryRunClusters = dryRunPlannerResult.Clusters
                .Where(cluster => IsClusterSelectedForMode(modePolicy, cluster))
                .Take(request.MaxClusters)
                .ToArray();
            var dryRunRunId = CognitiveMemoryDreamRunId.New();
            var dryRunCandidates = new List<CognitiveMemoryDreamAggregateCandidate>(dryRunClusters.Length);
            var dryRunClaims = 0;
            var dryRunSourceMaps = 0;
            foreach (var cluster in dryRunClusters)
            {
                var candidate = await CreateAggregateCandidateAsync(
                    dbContext,
                    request,
                    dryRunRunId.Value,
                    cluster,
                    nowUtc,
                    persistChanges: false,
                    cancellationToken);
                dryRunCandidates.Add(candidate);
                dryRunClaims += candidate.Claims.Count;
                dryRunSourceMaps += candidate.Claims.Sum(claim => claim.SourceMaps.Count);
            }

            var dryRunWarnings = dryRunPlannerResult.Warnings
                .Append("Dream run executed as a dry run; no quality records were persisted.")
                .ToArray();
            return new CognitiveMemoryDreamRunResult(
                dryRunRunId,
                CognitiveMemoryRunStatus.Succeeded,
                new CognitiveMemoryDreamConsolidationMetrics(
                    dryRunPlannerResult.Metrics.ClustersCreated,
                    dryRunClusters.Sum(cluster => cluster.Members.Count),
                    dryRunClaims,
                    dryRunCandidates.Count,
                    dryRunClaims,
                    dryRunSourceMaps,
                    0,
                    0,
                    0,
                    0,
                    0,
                    dryRunClaims == 0 ? 0 : Math.Clamp((double)dryRunSourceMaps / dryRunClaims, 0, 1),
                    stopwatch.Elapsed),
                dryRunCandidates,
                dryRunWarnings);
        }

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

        try
        {
            var plannerResult = await clusterPlanner.PlanAsync(
                new CognitiveMemoryClusterPlanningRequest(
                    request.ProjectId,
                    request.PolicyContext,
                    minMembers: request.MinMembersPerCluster,
                    maxRecords: 1000,
                    persistClusters: request.PersistChanges),
                cancellationToken);
            var selectedClusters = plannerResult.Clusters
                .Where(cluster => IsClusterSelectedForMode(modePolicy, cluster))
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
                var contract = await CreateAggregateCandidateAsync(
                    dbContext,
                    request,
                    runRecord.Id,
                    cluster,
                    nowUtc,
                    persistChanges: true,
                    cancellationToken);
                totalClaims += contract.Claims.Count;
                totalSourceMaps += contract.Claims.Sum(claim => claim.SourceMaps.Count);
                dbContext.Add(new CognitiveMemoryDreamRunClusterRecord
                {
                    Id = Guid.NewGuid(),
                    DreamRunId = runRecord.Id,
                    ClusterId = cluster.ClusterId.Value,
                    ProjectId = request.ProjectId,
                    Readiness = cluster.Readiness,
                    SelectionReasonCode = ResolveSelectionReasonCode(modePolicy, cluster),
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
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            dbContext.ChangeTracker.Clear();
            var failedRun = await dbContext.Set<CognitiveMemoryDreamRunRecord>()
                .SingleAsync(run => run.Id == runRecord.Id, cancellationToken);
            failedRun.Status = CognitiveMemoryRunStatus.Failed;
            failedRun.CompletedAtUtc = clock.GetUtcNow();
            failedRun.FailureCode = "quality.dream.run-failed";
            failedRun.FailureMessage = $"Dream run failed with {exception.GetType().Name}.";
            failedRun.WarningsJson = SerializeStringArray([$"Dream run failed with {exception.GetType().Name}."]);
            failedRun.ConcurrencyToken = Guid.NewGuid();
            await dbContext.SaveChangesAsync(cancellationToken);
            return new CognitiveMemoryDreamRunResult(
                new CognitiveMemoryDreamRunId(runRecord.Id),
                CognitiveMemoryRunStatus.Failed,
                ToMetrics(failedRun, stopwatch.Elapsed),
                [],
                [$"Dream run failed with {exception.GetType().Name}."]);
        }
    }

    private static async Task<CognitiveMemoryDreamAggregateCandidate> CreateAggregateCandidateAsync(
        AppDbContext dbContext,
        CognitiveMemoryDreamRunRequest request,
        Guid dreamRunId,
        CognitiveMemoryClusterPlan cluster,
        DateTimeOffset nowUtc,
        bool persistChanges,
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
        var primaryKeyTitle = cluster.Keys
            .Where(key => key.Family == cluster.PrimaryKeyFamily)
            .Concat(cluster.Keys)
            .Select(key => key.DisplayText)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        var title = CognitiveMemoryQualityText.TrimText(
            $"{request.Mode} synthesis: {FirstNonEmpty(primaryKeyTitle, records.FirstOrDefault()?.Title, "quality cluster")}",
            300);
        var canonicalText = BuildAggregateCanonicalText(records, support.ByRecordId);
        var candidateId = Guid.NewGuid();
        var aggregateClaims = new List<CognitiveMemoryDreamAggregateClaim>();
        var sequence = 0;
        foreach (var record in records)
        {
            var recordSupport = support.ByRecordId.GetValueOrDefault(record.Id) ?? CognitiveMemoryRecordSupport.Empty(record.Id);
            var claimText = CreateSafeClaimText(record, recordSupport);
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
            if (persistChanges)
            {
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
        if (persistChanges)
        {
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
        }

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
        CognitiveMemoryDreamModePolicy modePolicy,
        CognitiveMemoryClusterPlan cluster)
        => modePolicy.Mode switch
        {
            CognitiveMemoryConsolidationMode.ProjectNightly => cluster.Readiness != CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence,
            CognitiveMemoryConsolidationMode.CrossProjectWeekly => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.AggregateReady &&
                                                                   cluster.Keys.Any(key => key.Family == CognitiveMemoryQualityClusterKeyFamily.ProjectScope),
            CognitiveMemoryConsolidationMode.ProcedureMining => HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "procedure") ||
                                                                HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "workflow"),
            CognitiveMemoryConsolidationMode.FailureLearning => HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "failure") ||
                                                                cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory,
            CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh => cluster.Readiness is CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence
                or CognitiveMemoryQualityClusterReadiness.NeedsHumanReview,
            CognitiveMemoryConsolidationMode.EpistemicDriveScan => cluster.Readiness is CognitiveMemoryQualityClusterReadiness.NeedsMoreEvidence
                or CognitiveMemoryQualityClusterReadiness.Contradictory,
            CognitiveMemoryConsolidationMode.LearningOpportunityReview => HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "testing") ||
                                                                          HasKey(cluster, CognitiveMemoryQualityClusterKeyFamily.TaskIntent, "coverage") ||
                                                                          cluster.Readiness == CognitiveMemoryQualityClusterReadiness.NeedsHumanReview,
            _ => false
        };

    private static string ResolveSelectionReasonCode(
        CognitiveMemoryDreamModePolicy modePolicy,
        CognitiveMemoryClusterPlan cluster)
        => modePolicy.Mode switch
        {
            CognitiveMemoryConsolidationMode.ProjectNightly => "dream.project-nightly.aggregate-ready",
            CognitiveMemoryConsolidationMode.CrossProjectWeekly => "dream.cross-project-weekly.project-scope",
            CognitiveMemoryConsolidationMode.ProcedureMining => "dream.procedure-mining.task-intent",
            CognitiveMemoryConsolidationMode.FailureLearning => cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory
                ? "dream.failure-learning.contradiction"
                : "dream.failure-learning.incident",
            CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh => "dream.knowledge-coverage.refresh",
            CognitiveMemoryConsolidationMode.EpistemicDriveScan => "dream.epistemic-drive.scan",
            CognitiveMemoryConsolidationMode.LearningOpportunityReview => "dream.learning-opportunity.review",
            _ => modePolicy.ReasonCode
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

    private static string BuildAggregateCanonicalText(
        IReadOnlyList<CognitiveMemoryRecord> records,
        IReadOnlyDictionary<Guid, CognitiveMemoryRecordSupport> supportByRecordId)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Synthesis from {records.Count} source-backed memory record(s).");
        foreach (var record in records.Take(8))
        {
            var support = supportByRecordId.GetValueOrDefault(record.Id) ?? CognitiveMemoryRecordSupport.Empty(record.Id);
            var text = CognitiveMemoryQualityText.TrimText(CreateSafeClaimText(record, support), 300);
            if (!string.IsNullOrWhiteSpace(text))
            {
                builder.AppendLine($"- {text}");
            }
        }

        return builder.ToString().Trim();
    }

    private static string CreateSafeClaimText(
        CognitiveMemoryRecord record,
        CognitiveMemoryRecordSupport support)
    {
        if (support.HighestRedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted ||
            support.SourceItems.Any(item => item.AccessLevel == CognitiveMemoryAccessLevel.Restricted))
        {
            return $"{record.Title} is backed by restricted or redacted source evidence and requires review.";
        }

        return CognitiveMemoryQualityText.TrimText(
            CognitiveMemoryQualityText.Redact(FirstNonEmpty(record.SummaryText, record.CanonicalText, record.Title)),
            1200);
    }

    private static string SerializeStringArray(IReadOnlyList<string> values)
        => JsonSerializer.Serialize(values.ToArray(), CognitiveMemoryJsonSerializerContext.Default.StringArray);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private sealed record CognitiveMemoryDreamModePolicy(
        CognitiveMemoryConsolidationMode Mode,
        string ReasonCode)
    {
        public static CognitiveMemoryDreamModePolicy Resolve(CognitiveMemoryConsolidationMode mode)
            => mode switch
            {
                CognitiveMemoryConsolidationMode.ProjectNightly => new(mode, "dream.project-nightly"),
                CognitiveMemoryConsolidationMode.CrossProjectWeekly => new(mode, "dream.cross-project-weekly"),
                CognitiveMemoryConsolidationMode.ProcedureMining => new(mode, "dream.procedure-mining"),
                CognitiveMemoryConsolidationMode.FailureLearning => new(mode, "dream.failure-learning"),
                CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh => new(mode, "dream.knowledge-coverage-refresh"),
                CognitiveMemoryConsolidationMode.EpistemicDriveScan => new(mode, "dream.epistemic-drive-scan"),
                CognitiveMemoryConsolidationMode.LearningOpportunityReview => new(mode, "dream.learning-opportunity-review"),
                CognitiveMemoryConsolidationMode.IncrementalRecent => throw new ArgumentException("Dream consolidation must be explicit and must not run through the incremental profile.", nameof(mode)),
                _ => throw new NotSupportedException($"Consolidation mode '{mode}' is not supported by dream consolidation.")
            };
    }
}
