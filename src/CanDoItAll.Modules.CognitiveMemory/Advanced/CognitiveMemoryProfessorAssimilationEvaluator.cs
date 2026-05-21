using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

internal sealed class CognitiveMemoryProfessorAssimilationEvaluator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    CognitiveMemoryQualityAlgorithmOptions? algorithmOptions = null) : ICognitiveMemoryProfessorAssimilationEvaluator
{
    private readonly CognitiveMemoryQualityProfessorLifecycleAlgorithmOptions options = (algorithmOptions ?? CognitiveMemoryQualityAlgorithmOptions.Current).ProfessorLifecycle;

    public async ValueTask<CognitiveMemoryProfessorAnchorAssimilationEvaluationResult> EvaluateAsync(
        CognitiveMemoryProfessorAnchorAssimilationEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaptureId == Guid.Empty)
        {
            throw new ArgumentException("Professor anchor assimilation evaluation requires a capture id.", nameof(request));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var capture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CaptureId, cancellationToken);
        if (capture is null)
        {
            return Reject("Professor anchor capture was not found.");
        }

        if (capture.AppliedMemoryRecordId == request.DerivedMemoryRecordId.Value)
        {
            return Reject("Professor anchor cannot use its direct capture memory as assimilation proof.");
        }

        var derivedMemory = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(record => record.Id == request.DerivedMemoryRecordId.Value, cancellationToken);
        if (derivedMemory is null ||
            derivedMemory.ProjectId != capture.ProjectId ||
            derivedMemory.ValidationState != CognitiveMemoryValidationState.Approved ||
            derivedMemory.StabilityState is not (CognitiveMemoryStabilityState.Active or CognitiveMemoryStabilityState.Stable))
        {
            return Reject("Professor anchor cannot be assimilated without an approved active derived memory.");
        }

        var sourceLinks = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => link.MemoryRecordId == derivedMemory.Id)
            .ToListAsync(cancellationToken);
        var evidenceLinks = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(link => link.MemoryRecordId == derivedMemory.Id)
            .ToListAsync(cancellationToken);
        if (!HasAnchorLineage(capture, sourceLinks, evidenceLinks))
        {
            return Reject("Professor anchor cannot be assimilated because the derived memory does not retain anchor lineage.");
        }

        var descendantMemoryIds = await BuildAnchorDescendantMemoryIdsAsync(dbContext, capture, cancellationToken);
        var independentSupportCount = await CountIndependentNonDescendantSupportAsync(
            dbContext,
            capture,
            derivedMemory.Id,
            sourceLinks,
            evidenceLinks,
            descendantMemoryIds,
            cancellationToken);
        if (independentSupportCount == 0)
        {
            return Reject("Professor anchor cannot be assimilated without independent non-descendant support.");
        }

        var repeatedUseCount = await CountAcceptedUseEventsAsync(dbContext, derivedMemory.Id, cancellationToken);
        if (repeatedUseCount == 0)
        {
            return Reject(
                "Professor anchor cannot be assimilated until the derived memory has durable accepted-use evidence.",
                independentSupportCount);
        }

        var hasIntegrationEvidence = await HasDreamOrClusterIntegrationAsync(dbContext, derivedMemory.Id, cancellationToken);
        if (request.RequireUsageAndIntegration && repeatedUseCount < options.RequiredRepeatedUseCount)
        {
            return Reject(
                $"Professor anchor automatic assimilation requires at least {options.RequiredRepeatedUseCount} accepted-use events.",
                independentSupportCount,
                repeatedUseCount,
                hasIntegrationEvidence);
        }

        if (request.RequireUsageAndIntegration && !hasIntegrationEvidence)
        {
            return Reject(
                "Professor anchor automatic assimilation requires dream or cluster integration evidence.",
                independentSupportCount,
                repeatedUseCount,
                hasIntegrationEvidence);
        }

        return new CognitiveMemoryProfessorAnchorAssimilationEvaluationResult(
            true,
            "Professor anchor has anchor lineage, independent non-descendant support, accepted-use events, and required integration.",
            independentSupportCount,
            repeatedUseCount,
            hasIntegrationEvidence);
    }

    private static CognitiveMemoryProfessorAnchorAssimilationEvaluationResult Reject(
        string reason,
        int independentSupportCount = 0,
        int repeatedUseCount = 0,
        bool hasIntegrationEvidence = false)
        => new(false, reason, independentSupportCount, repeatedUseCount, hasIntegrationEvidence);

    private static bool HasAnchorLineage(
        CognitiveMemoryCuratorCapturedImprovementRecord capture,
        IReadOnlyList<CognitiveMemorySourceLinkRecord> sourceLinks,
        IReadOnlyList<CognitiveMemoryRecordEvidenceAnchorRecord> evidenceLinks)
        => capture.SourceItemId is { } sourceItemId && sourceLinks.Any(link => link.SourceItemId == sourceItemId) ||
           capture.EvidenceAnchorId is { } evidenceAnchorId && evidenceLinks.Any(link => link.EvidenceAnchorId == evidenceAnchorId);

    private async Task<HashSet<Guid>> BuildAnchorDescendantMemoryIdsAsync(
        AppDbContext dbContext,
        CognitiveMemoryCuratorCapturedImprovementRecord capture,
        CancellationToken cancellationToken)
    {
        var descendants = new HashSet<Guid>();
        if (capture.AppliedMemoryRecordId is not { } appliedMemoryRecordId)
        {
            return descendants;
        }

        descendants.Add(appliedMemoryRecordId);
        var frontier = new HashSet<Guid> { appliedMemoryRecordId };
        for (var depth = 0; depth < options.DescendantTraversalDepth && frontier.Count > 0; depth++)
        {
            var frontierIds = frontier.ToArray();
            var candidateIds = await dbContext.Set<CognitiveMemoryDreamAggregateClaimSourceMapRecord>()
                .AsNoTracking()
                .Where(sourceMap => frontierIds.Contains(sourceMap.SourceMemoryRecordId))
                .Select(sourceMap => sourceMap.AggregateCandidateId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (candidateIds.Count == 0)
            {
                break;
            }

            var next = await dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
                .AsNoTracking()
                .Where(candidate => candidate.MemoryRecordId != null && candidateIds.Contains(candidate.Id))
                .Select(candidate => candidate.MemoryRecordId!.Value)
                .ToListAsync(cancellationToken);
            frontier.Clear();
            foreach (var memoryRecordId in next)
            {
                if (descendants.Add(memoryRecordId))
                {
                    frontier.Add(memoryRecordId);
                }
            }
        }

        return descendants;
    }

    private static async Task<int> CountIndependentNonDescendantSupportAsync(
        AppDbContext dbContext,
        CognitiveMemoryCuratorCapturedImprovementRecord capture,
        Guid derivedMemoryRecordId,
        IReadOnlyList<CognitiveMemorySourceLinkRecord> sourceLinks,
        IReadOnlyList<CognitiveMemoryRecordEvidenceAnchorRecord> evidenceLinks,
        IReadOnlySet<Guid> descendantMemoryIds,
        CancellationToken cancellationToken)
    {
        var aggregateCandidates = await dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .AsNoTracking()
            .Where(candidate => candidate.MemoryRecordId == derivedMemoryRecordId)
            .ToListAsync(cancellationToken);
        if (aggregateCandidates.Count > 0)
        {
            var candidateIds = aggregateCandidates.Select(candidate => candidate.Id).ToArray();
            var sourceMaps = await dbContext.Set<CognitiveMemoryDreamAggregateClaimSourceMapRecord>()
                .AsNoTracking()
                .Where(sourceMap => candidateIds.Contains(sourceMap.AggregateCandidateId))
                .ToListAsync(cancellationToken);
            return sourceMaps
                .Where(sourceMap =>
                    !descendantMemoryIds.Contains(sourceMap.SourceMemoryRecordId) &&
                    (capture.SourceItemId is null || sourceMap.SourceItemId != capture.SourceItemId.Value) &&
                    (capture.EvidenceAnchorId is null || sourceMap.EvidenceAnchorId != capture.EvidenceAnchorId.Value))
                .Select(sourceMap => sourceMap.SourceMemoryRecordId)
                .Distinct()
                .Count();
        }

        var sourceSupportCount = sourceLinks
            .Where(link => capture.SourceItemId is null || link.SourceItemId != capture.SourceItemId.Value)
            .Select(link => link.SourceItemId)
            .Where(sourceItemId => sourceItemId != Guid.Empty)
            .Distinct()
            .Count();
        var evidenceSupportCount = evidenceLinks
            .Where(link => capture.EvidenceAnchorId is null || link.EvidenceAnchorId != capture.EvidenceAnchorId.Value)
            .Select(link => link.EvidenceAnchorId)
            .Where(evidenceAnchorId => evidenceAnchorId != Guid.Empty)
            .Distinct()
            .Count();
        return Math.Max(sourceSupportCount, evidenceSupportCount);
    }

    private static async Task<int> CountAcceptedUseEventsAsync(
        AppDbContext dbContext,
        Guid derivedMemoryRecordId,
        CancellationToken cancellationToken)
        => await dbContext.Set<CognitiveMemorySignalRecord>()
            .AsNoTracking()
            .Where(signal =>
                signal.MemoryRecordId == derivedMemoryRecordId &&
                signal.SignalKind == CognitiveMemorySignalKind.ProfessorAnchorAcceptedUse &&
                !signal.RequiresReview)
            .Select(signal => signal.Id)
            .Distinct()
            .CountAsync(cancellationToken);

    private static async Task<bool> HasDreamOrClusterIntegrationAsync(
        AppDbContext dbContext,
        Guid derivedMemoryRecordId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .AsNoTracking()
            .AnyAsync(candidate =>
                candidate.MemoryRecordId == derivedMemoryRecordId &&
                candidate.SourceMapCount >= 2 &&
                (candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.Approved ||
                 candidate.Status == CognitiveMemoryDreamAggregateCandidateStatus.Applied),
                cancellationToken))
        {
            return true;
        }

        return await (
            from member in dbContext.Set<CognitiveMemoryQualityClusterMemberRecord>().AsNoTracking()
            join cluster in dbContext.Set<CognitiveMemoryQualityClusterRecord>().AsNoTracking()
                on member.ClusterId equals cluster.Id
            where member.MemberKind == CognitiveMemoryQualityClusterMemberKind.MemoryRecord &&
                  member.MemoryRecordId == derivedMemoryRecordId &&
                  cluster.AggregateEligible &&
                  cluster.Readiness == CognitiveMemoryQualityClusterReadiness.AggregateReady
            select member.Id)
            .AnyAsync(cancellationToken);
    }
}
