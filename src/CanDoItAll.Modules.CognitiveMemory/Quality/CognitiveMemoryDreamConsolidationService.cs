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
    IClock clock,
    ICognitiveMemoryDreamClaimSynthesizer? claimSynthesizer = null,
    CognitiveMemoryQualityAlgorithmOptions? algorithmOptions = null,
    ICognitiveMemoryDreamModeClusterSelector? modeClusterSelector = null) : ICognitiveMemoryDreamConsolidationService
{
    private readonly ICognitiveMemoryDreamClaimSynthesizer claimSynthesizer = claimSynthesizer ?? CognitiveMemoryDreamClaimSynthesizer.Instance;
    private readonly ICognitiveMemoryDreamModeClusterSelector modeClusterSelector = modeClusterSelector ?? CognitiveMemoryDreamModeClusterSelector.Instance;
    private readonly CognitiveMemoryQualityDreamAlgorithmOptions options = (algorithmOptions ?? new CognitiveMemoryQualityAlgorithmOptions()).Dream;
    private string AlgorithmVersion => options.AlgorithmVersion.Value;
    private int MaxAggregateClaims => options.MaxAggregateClaims;
    private string AggregateClaimPredicateKey => options.AggregateClaimPredicateKey;

    public async ValueTask<CognitiveMemoryDreamRunResult> RunAsync(
        CognitiveMemoryDreamRunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var modePolicy = modeClusterSelector.ResolvePolicy(request.Mode);

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
                    persistClusters: false,
                    scope: modeClusterSelector.ResolvePlanningScope(request)),
                cancellationToken);
            var dryRunClusters = dryRunPlannerResult.Clusters
                .Where(cluster => modeClusterSelector.IsSelected(modePolicy.Mode, cluster))
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
                    persistClusters: request.PersistChanges,
                    scope: modeClusterSelector.ResolvePlanningScope(request)),
                cancellationToken);
            var selectedClusters = plannerResult.Clusters
                .Where(cluster => modeClusterSelector.IsSelected(modePolicy.Mode, cluster))
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
                    SelectionReasonCode = modeClusterSelector.ResolveSelectionReasonCode(modePolicy.Mode, cluster),
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

    private async Task<CognitiveMemoryDreamAggregateCandidate> CreateAggregateCandidateAsync(
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
        var title = ResolveAggregateTitle(request.Mode, cluster, records);
        var claimUnits = CreateClaimUnits(request.Mode, cluster, records, support.ByRecordId);
        var canonicalText = BuildAggregateCanonicalText(request.Mode, cluster, records, claimUnits);
        var candidateId = Guid.NewGuid();
        var aggregateClaims = new List<CognitiveMemoryDreamAggregateClaim>();
        var fallbackSubjectKey = ResolveAggregateSubjectKey(cluster, records);
        var claimSequence = 0;
        foreach (var claimGroup in BuildClaimGroups(claimUnits).Take(MaxAggregateClaims))
        {
            var aggregateClaimText = SynthesizeClaimGroupText(request.Mode, claimGroup);
            var aggregateSourceMaps = claimGroup.Units
                .SelectMany(unit => unit.SourceMaps)
                .GroupBy(sourceMap => new { sourceMap.SourceMemoryRecordId, sourceMap.SourceItemId, sourceMap.EvidenceAnchorId, sourceMap.Direction })
                .Select(group => group.First())
                .ToArray();
            if (string.IsNullOrWhiteSpace(aggregateClaimText) || aggregateSourceMaps.Length == 0)
            {
                continue;
            }

            var aggregateClaimId = Guid.NewGuid();
            var claimKind = claimGroup.RepresentativeSlots.ClaimKind;
            var subjectKey = FirstNonEmpty(claimGroup.RepresentativeSlots.SubjectKey, fallbackSubjectKey);
            var predicateKey = FirstNonEmpty(claimGroup.RepresentativeSlots.PredicateKey, AggregateClaimPredicateKey);
            var objectKey = FirstNonEmpty(claimGroup.RepresentativeSlots.ObjectKey, claimGroup.Signature);
            aggregateClaims.Add(new CognitiveMemoryDreamAggregateClaim(
                aggregateClaimId,
                claimKind,
                aggregateClaimText,
                subjectKey,
                CognitiveMemoryQualityText.TrimText(predicateKey, 160),
                CognitiveMemoryQualityText.TrimText(objectKey, 240),
                aggregateSourceMaps));
            if (persistChanges)
            {
                dbContext.Add(new CognitiveMemoryDreamAggregateClaimRecord
                {
                    Id = aggregateClaimId,
                    AggregateCandidateId = candidateId,
                    ProjectId = request.ProjectId,
                    Sequence = claimSequence,
                    ClaimKind = claimKind,
                    ClaimText = aggregateClaimText,
                    SubjectKey = subjectKey,
                    PredicateKey = CognitiveMemoryQualityText.TrimText(predicateKey, 160),
                    ObjectKey = CognitiveMemoryQualityText.TrimText(objectKey, 240),
                    CreatedAtUtc = nowUtc
                });
                foreach (var sourceMap in aggregateSourceMaps)
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

            claimSequence++;
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

    private static CognitiveMemoryClaimKind ResolveAggregateClaimKind(
        CognitiveMemoryConsolidationMode mode,
        CognitiveMemoryRecord? record)
        => mode switch
        {
            CognitiveMemoryConsolidationMode.ProcedureMining => CognitiveMemoryClaimKind.ProcedureConstraint,
            CognitiveMemoryConsolidationMode.FailureLearning => CognitiveMemoryClaimKind.FailureMode,
            _ => record?.Kind switch
            {
                CognitiveMemoryRecordKind.Decision => CognitiveMemoryClaimKind.Decision,
                CognitiveMemoryRecordKind.Procedural => CognitiveMemoryClaimKind.ProcedureConstraint,
                CognitiveMemoryRecordKind.Episodic => CognitiveMemoryClaimKind.Observation,
                _ => CognitiveMemoryClaimKind.Fact
            }
        };

    private string BuildAggregateCanonicalText(
        CognitiveMemoryConsolidationMode mode,
        CognitiveMemoryClusterPlan cluster,
        IReadOnlyList<CognitiveMemoryRecord> records,
        IReadOnlyList<DreamClaimUnit> claimUnits)
    {
        var claimTexts = BuildClaimGroups(claimUnits)
            .Select(group => SynthesizeClaimGroupText(mode, group))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxAggregateClaims)
            .ToArray();
        var builder = new StringBuilder();

        if (claimTexts.Length == 0)
        {
            builder.AppendLine($"{ResolvePrimaryClusterKey(cluster)?.DisplayText ?? records.FirstOrDefault()?.Title ?? "The cluster"} needs human review because no readable source-supported claim could be synthesized.");
        }

        AppendModeHeader(builder, mode);
        for (var index = 0; index < claimTexts.Length; index++)
        {
            var claimText = EnsureSentence(claimTexts[index]);
            if (mode == CognitiveMemoryConsolidationMode.ProcedureMining)
            {
                builder.AppendLine($"{index + 1}. {claimText}");
                continue;
            }

            builder.AppendLine(claimText);
        }

        if (cluster.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory)
        {
            builder.AppendLine("Review required because source memories in this cluster disagree; keep the conflicting claims separate until a curator resolves them.");
        }
        else if (!cluster.QualityMetrics.AggregateEligible)
        {
            builder.AppendLine("Review required before applying this aggregate because the clustered evidence is not yet strong enough for unattended memory promotion.");
        }

        return builder.ToString().Trim();
    }

    private static IReadOnlyList<DreamClaimUnit> CreateClaimUnits(
        CognitiveMemoryConsolidationMode mode,
        CognitiveMemoryClusterPlan cluster,
        IReadOnlyList<CognitiveMemoryRecord> records,
        IReadOnlyDictionary<Guid, CognitiveMemoryRecordSupport> supportByRecordId)
    {
        var units = new List<DreamClaimUnit>();
        foreach (var record in records)
        {
            var support = supportByRecordId.GetValueOrDefault(record.Id) ?? CognitiveMemoryRecordSupport.Empty(record.Id);
            var sourceMaps = CreateSourceMaps(record, support);
            if (sourceMaps.Count == 0)
            {
                continue;
            }

            var sourceClaims = support.Claims
                .Where(claim => claim.ValidationState != CognitiveMemoryValidationState.Rejected)
                .Select(claim => new SourceClaimDescriptor(
                    NormalizeConclusionFragment(claim.ClaimText),
                    claim.ClaimKind,
                    claim.SubjectKey,
                    claim.PredicateKey,
                    claim.ObjectKey))
                .Where(claim => !string.IsNullOrWhiteSpace(claim.ClaimText))
                .ToArray();
            if (sourceClaims.Length == 0)
            {
                sourceClaims =
                [
                    new SourceClaimDescriptor(
                        NormalizeConclusionFragment(CreateSafeClaimText(record, support)),
                        ResolveAggregateClaimKind(mode, record),
                        string.Empty,
                        string.Empty,
                        string.Empty)
                ];
            }

            foreach (var sourceClaim in sourceClaims.DistinctBy(claim => claim.ClaimText, StringComparer.OrdinalIgnoreCase))
            {
                var slots = CognitiveMemoryDreamClaimSlotExtractor.Extract(
                    sourceClaim.ClaimText,
                    sourceClaim.ClaimKind,
                    sourceClaim.SubjectKey,
                    sourceClaim.PredicateKey,
                    sourceClaim.ObjectKey);
                var signature = BuildClaimSignature(mode, slots);
                units.Add(new DreamClaimUnit(record, sourceClaim.ClaimText, signature, sourceMaps, slots));
            }
        }

        return units;
    }

    private static IReadOnlyList<DreamClaimGroup> BuildClaimGroups(IReadOnlyList<DreamClaimUnit> claimUnits)
        => claimUnits
            .GroupBy(unit => unit.Signature, StringComparer.Ordinal)
            .Select(group => new DreamClaimGroup(
                group.Key,
                group
                    .OrderBy(unit => unit.Record.Title, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(unit => unit.ClaimText, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                group
                    .OrderBy(unit => unit.Slots.SubjectKey, StringComparer.Ordinal)
                    .ThenBy(unit => unit.Slots.PredicateKey, StringComparer.Ordinal)
                    .ThenBy(unit => unit.Slots.ObjectKey, StringComparer.Ordinal)
                    .First()
                    .Slots))
            .OrderByDescending(group => group.Units.Select(unit => unit.Record.Id).Distinct().Count())
            .ThenBy(group => group.Units[0].ClaimText, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private string SynthesizeClaimGroupText(CognitiveMemoryConsolidationMode mode, DreamClaimGroup claimGroup)
        => claimSynthesizer.Synthesize(new CognitiveMemoryDreamClaimSynthesisRequest(
            mode,
            claimGroup.Units.Select(unit => unit.ClaimText).ToArray()));

    private static string BuildClaimSignature(
        CognitiveMemoryConsolidationMode mode,
        CognitiveMemoryDreamClaimSlots slots)
    {
        var modeKey = CognitiveMemoryQualityText.NormalizeKey(mode.ToString());
        var kindKey = CognitiveMemoryQualityText.NormalizeKey(slots.ClaimKind.ToString());
        var subjectKey = FirstNonEmpty(slots.SubjectKey, "unknown.subject");
        var complementarySubject = SubjectSupportsComplementaryClaims(slots.SubjectKey);
        var predicateScopeKey = complementarySubject
            ? "complementary"
            : ResolveClaimGroupingPredicateKey(slots);
        var scopeKey = complementarySubject
            ? "complementary"
            : FirstNonEmpty(slots.ScopeKey, "none");
        return CognitiveMemoryQualityText.TrimText($"{modeKey}.{kindKey}.{subjectKey}.{predicateScopeKey}.{scopeKey}", 240);
    }

    private static string ResolveClaimGroupingPredicateKey(CognitiveMemoryDreamClaimSlots slots)
    {
        if (SubjectSupportsComplementaryClaims(slots.SubjectKey))
        {
            return "complementary";
        }

        return CognitiveMemoryQualityText.TrimText(
            $"{FirstNonEmpty(slots.PredicateKey, "states")}.{FirstNonEmpty(slots.ObjectKey, "object")}",
            160);
    }

    private static bool SubjectSupportsComplementaryClaims(string subjectKey)
        => subjectKey.Contains("procedure", StringComparison.Ordinal) ||
           subjectKey.Contains("checklist", StringComparison.Ordinal) ||
           subjectKey.Contains("runbook", StringComparison.Ordinal) ||
           subjectKey.Contains("workflow", StringComparison.Ordinal) ||
           subjectKey.Contains("policy", StringComparison.Ordinal);

    private static void AppendModeHeader(StringBuilder builder, CognitiveMemoryConsolidationMode mode)
    {
        var header = mode switch
        {
            CognitiveMemoryConsolidationMode.ProcedureMining => "Procedure steps:",
            CognitiveMemoryConsolidationMode.FailureLearning => "Failure pattern:",
            CognitiveMemoryConsolidationMode.KnowledgeCoverageRefresh => "Coverage findings:",
            CognitiveMemoryConsolidationMode.EpistemicDriveScan => "Epistemic drive signals:",
            CognitiveMemoryConsolidationMode.LearningOpportunityReview => "Learning opportunities:",
            CognitiveMemoryConsolidationMode.CrossProjectWeekly => "Cross-project synthesis:",
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(header))
        {
            builder.AppendLine(header);
        }
    }

    private static string NormalizeConclusionFragment(string text)
    {
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? string.Empty;
        normalized = normalized.Trim().TrimEnd('.');
        foreach (var prefix in new[] { "Synthesis from ", "This candidate " })
        {
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                normalized = normalized[prefix.Length..].Trim();
            }
        }

        return CognitiveMemoryQualityText.TrimText(normalized, 240);
    }

    private static string EnsureSentence(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith(".", StringComparison.Ordinal) ||
               trimmed.EndsWith("?", StringComparison.Ordinal) ||
               trimmed.EndsWith("!", StringComparison.Ordinal)
            ? trimmed
            : $"{trimmed}.";
    }

    private static string ResolveAggregateTitle(
        CognitiveMemoryConsolidationMode mode,
        CognitiveMemoryClusterPlan cluster,
        IReadOnlyList<CognitiveMemoryRecord> records)
    {
        var titleKey = ResolvePrimaryClusterKey(cluster);
        return CognitiveMemoryQualityText.TrimText(
            $"{mode} synthesis: {titleKey?.DisplayText ?? records.FirstOrDefault()?.Title ?? "quality cluster"}",
            300);
    }

    private static string ResolveAggregateSubjectKey(
        CognitiveMemoryClusterPlan cluster,
        IReadOnlyList<CognitiveMemoryRecord> records)
        => CognitiveMemoryQualityText.TrimText(
            CognitiveMemoryQualityText.NormalizeKey(ResolvePrimaryClusterKey(cluster)?.DisplayText ?? records.FirstOrDefault()?.TopicKey ?? records.FirstOrDefault()?.Title ?? "aggregate"),
            240);

    private static CognitiveMemoryClusterKey? ResolvePrimaryClusterKey(CognitiveMemoryClusterPlan cluster)
        => cluster.Keys.FirstOrDefault(key => key.Family == cluster.PrimaryKeyFamily) ??
           cluster.Keys.FirstOrDefault(key => key.Family == CognitiveMemoryQualityClusterKeyFamily.SemanticTopic) ??
           cluster.Keys.FirstOrDefault(key => key.Family == CognitiveMemoryQualityClusterKeyFamily.Entity) ??
           cluster.Keys.FirstOrDefault();

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

    private sealed record DreamClaimUnit(
        CognitiveMemoryRecord Record,
        string ClaimText,
        string Signature,
        IReadOnlyList<CognitiveMemoryDreamAggregateSourceMap> SourceMaps,
        CognitiveMemoryDreamClaimSlots Slots);

    private sealed record DreamClaimGroup(
        string Signature,
        IReadOnlyList<DreamClaimUnit> Units,
        CognitiveMemoryDreamClaimSlots RepresentativeSlots);

    private sealed record SourceClaimDescriptor(
        string ClaimText,
        CognitiveMemoryClaimKind ClaimKind,
        string SubjectKey,
        string PredicateKey,
        string ObjectKey);

}
