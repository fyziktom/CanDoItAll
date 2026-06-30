using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
public sealed class CognitiveMemoryDreamValidator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ICognitiveMemoryDreamEntailmentValidator? entailmentValidator = null) : ICognitiveMemoryDreamValidator
{
    private readonly ICognitiveMemoryDreamEntailmentValidator entailmentValidator = entailmentValidator ?? CognitiveMemoryDreamEntailmentValidator.Instance;

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
        var sourceItemIds = sourceMaps
            .Select(sourceMap => sourceMap.SourceItemId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var evidenceAnchorIds = sourceMaps
            .Select(sourceMap => sourceMap.EvidenceAnchorId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToArray();
        var sourceRecords = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record => sourceRecordIds.Contains(record.Id))
            .ToListAsync(cancellationToken);
        var sourceRecordSourceItemIds = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => sourceRecordIds.Contains(link.MemoryRecordId))
            .Select(link => link.SourceItemId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var allSourceItemIds = sourceItemIds
            .Concat(sourceRecordSourceItemIds)
            .Distinct()
            .ToArray();
        var sourceAnchors = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>()
            .Where(capture =>
                (capture.AppliedMemoryRecordId != null && sourceRecordIds.Contains(capture.AppliedMemoryRecordId.Value) ||
                 capture.SourceItemId != null && allSourceItemIds.Contains(capture.SourceItemId.Value) ||
                 capture.EvidenceAnchorId != null && evidenceAnchorIds.Contains(capture.EvidenceAnchorId.Value)) &&
                (capture.AnchorState == CognitiveMemoryProfessorAnchorState.Active ||
                 capture.AnchorState == CognitiveMemoryProfessorAnchorState.Comparing))
            .ToListAsync(cancellationToken);
        var activeSourceAnchors = sourceAnchors
            .Where(anchor => anchor.AnchorState == CognitiveMemoryProfessorAnchorState.Active)
            .ToArray();
        var unassimilatedProfessorAnchorSourceMemoryIds = sourceAnchors
            .Where(capture => capture.AppliedMemoryRecordId != null)
            .Select(capture => capture.AppliedMemoryRecordId!.Value)
            .Distinct()
            .ToHashSet();
        foreach (var sourceAnchor in activeSourceAnchors)
        {
            sourceAnchor.AnchorState = CognitiveMemoryProfessorAnchorState.Comparing;
            sourceAnchor.ConcurrencyToken = Guid.NewGuid();
        }

        var cluster = await dbContext.Set<CognitiveMemoryQualityClusterRecord>()
            .AsNoTracking()
            .Where(cluster => cluster.Id == candidate.ClusterId)
            .SingleOrDefaultAsync(cancellationToken);
        var existingGeneratedMemories = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record =>
                record.ProjectId == candidate.ProjectId &&
                record.Origin == CognitiveMemoryRecordOrigin.MachineGenerated &&
                record.StabilityState != CognitiveMemoryStabilityState.Deprecated &&
                record.Id != candidate.MemoryRecordId)
            .ToListAsync(cancellationToken);
        var existingGeneratedMemoryIds = existingGeneratedMemories.Select(record => record.Id).ToArray();
        var existingGeneratedClaims = existingGeneratedMemoryIds.Length == 0
            ? new List<CognitiveMemoryClaimRecord>()
            : await dbContext.Set<CognitiveMemoryClaimRecord>()
                .AsNoTracking()
                .Where(claim => claim.MemoryRecordId != null && existingGeneratedMemoryIds.Contains(claim.MemoryRecordId.Value))
                .ToListAsync(cancellationToken);
        var existingGeneratedSourceLinks = existingGeneratedMemoryIds.Length == 0
            ? new List<CognitiveMemorySourceLinkRecord>()
            : await dbContext.Set<CognitiveMemorySourceLinkRecord>()
                .AsNoTracking()
                .Where(link => existingGeneratedMemoryIds.Contains(link.MemoryRecordId))
                .ToListAsync(cancellationToken);
        var duplicateExists = HasDuplicateAggregate(
            candidate,
            claims,
            sourceMaps,
            existingGeneratedMemories,
            existingGeneratedClaims,
            existingGeneratedSourceLinks);
        var issues = ResolveIssues(
            candidate,
            claims,
            sourceMaps,
            sourceRecords,
            cluster,
            duplicateExists,
            unassimilatedProfessorAnchorSourceMemoryIds,
            request.PolicyContext);
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
        foreach (var sourceAnchor in activeSourceAnchors)
        {
            CognitiveMemoryProfessorAnchorTransitionAudit.AddTransition(
                dbContext,
                sourceAnchor,
                CognitiveMemoryProfessorAnchorState.Active,
                CognitiveMemoryProfessorAnchorState.Comparing,
                nowUtc,
                $"Dream aggregate candidate '{candidate.Id:D}' opened professor comparison review.",
                derivedMemoryRecordId: candidate.MemoryRecordId);
        }

        if (decision == CognitiveMemoryDreamValidationDecision.Rejected)
        {
            foreach (var sourceAnchor in activeSourceAnchors)
            {
                sourceAnchor.AnchorState = CognitiveMemoryProfessorAnchorState.Active;
                sourceAnchor.ConcurrencyToken = Guid.NewGuid();
                CognitiveMemoryProfessorAnchorTransitionAudit.AddTransition(
                    dbContext,
                    sourceAnchor,
                    CognitiveMemoryProfessorAnchorState.Comparing,
                    CognitiveMemoryProfessorAnchorState.Active,
                    nowUtc,
                    $"Dream aggregate candidate '{candidate.Id:D}' was rejected, so comparison returned to the active professor anchor.",
                    derivedMemoryRecordId: candidate.MemoryRecordId);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryDreamValidationResult(request.AggregateCandidateId, decision, issues, reviewItemId);
    }

    private IReadOnlyList<CognitiveMemoryDreamValidationIssue> ResolveIssues(
        CognitiveMemoryDreamAggregateCandidateRecord candidate,
        IReadOnlyList<CognitiveMemoryDreamAggregateClaimRecord> claims,
        IReadOnlyList<CognitiveMemoryDreamAggregateClaimSourceMapRecord> sourceMaps,
        IReadOnlyList<CognitiveMemoryRecord> sourceRecords,
        CognitiveMemoryQualityClusterRecord? cluster,
        bool duplicateExists,
        IReadOnlySet<Guid> unassimilatedProfessorAnchorSourceMemoryIds,
        CognitiveMemoryPolicyContext policyContext)
    {
        var issues = new List<CognitiveMemoryDreamValidationIssue>();
        var sourceRecordsById = sourceRecords.ToDictionary(record => record.Id);
        foreach (var claim in claims)
        {
            var claimSourceMaps = sourceMaps
                .Where(sourceMap => sourceMap.AggregateClaimId == claim.Id)
                .ToArray();
            if (claimSourceMaps.Length == 0)
            {
                issues.Add(new CognitiveMemoryDreamValidationIssue(
                    CognitiveMemoryDreamValidationIssueKind.MissingSourceMap,
                    CognitiveMemoryRiskLevel.High,
                    $"Aggregate claim '{claim.Id:D}' has no claim-level source map."));
                continue;
            }

            var supportResult = ValidateClaimSupport(claim, claimSourceMaps, sourceRecordsById);
            if (!supportResult.Supported)
            {
                issues.Add(new CognitiveMemoryDreamValidationIssue(
                    CognitiveMemoryDreamValidationIssueKind.UnsupportedClaim,
                    CognitiveMemoryRiskLevel.Medium,
                    $"Aggregate claim '{claim.Id:D}' is not entailed by its mapped source memories: {supportResult.Reason}"));
            }
        }

        if (sourceRecords.Count >= 2 && IsRepresentativeCopyAggregate(claims, sourceMaps, sourceRecords))
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.UnsupportedClaim,
                CognitiveMemoryRiskLevel.Medium,
                "Aggregate candidate copies representative source claims instead of synthesizing complementary evidence."));
        }

        if (sourceMaps.Select(sourceMap => sourceMap.SourceMemoryRecordId).Distinct().Count() < 2)
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.WeakSourceIndependence,
                CognitiveMemoryRiskLevel.Medium,
                "Aggregate candidate has fewer than two independent source memories."));
        }

        if (cluster?.Readiness == CognitiveMemoryQualityClusterReadiness.Contradictory ||
            sourceMaps.Any(sourceMap => sourceMap.Direction == CognitiveMemoryEvidenceDirection.Attacks))
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.Contradiction,
                CognitiveMemoryRiskLevel.High,
                "Aggregate candidate includes attacking or contradictory source evidence."));
        }

        if (cluster is not null && !cluster.AggregateEligible)
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                cluster.MemberCount > 20 ? CognitiveMemoryDreamValidationIssueKind.OverbroadCluster : CognitiveMemoryDreamValidationIssueKind.LowCohesion,
                CognitiveMemoryRiskLevel.Medium,
                $"Cluster quality is not aggregate-eligible: {cluster.EligibilityReason}"));
        }

        if (cluster is { SourceIndependenceScore: < 1 })
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.WeakSourceIndependence,
                CognitiveMemoryRiskLevel.Medium,
                "Aggregate candidate lacks independent source-item support."));
        }

        if (duplicateExists)
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.DuplicateAggregate,
                CognitiveMemoryRiskLevel.Medium,
                "An active generated aggregate with the same title or claim/source signature already exists."));
        }

        if (sourceRecords.Any(record => unassimilatedProfessorAnchorSourceMemoryIds.Contains(record.Id)))
        {
            issues.Add(new CognitiveMemoryDreamValidationIssue(
                CognitiveMemoryDreamValidationIssueKind.WeakEvidence,
                CognitiveMemoryRiskLevel.Medium,
                "Aggregate candidate depends on an unassimilated professor anchor memory and requires comparison review."));
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
            .GroupBy(issue => $"{issue.IssueKind}:{issue.Message}", StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static bool HasDuplicateAggregate(
        CognitiveMemoryDreamAggregateCandidateRecord candidate,
        IReadOnlyList<CognitiveMemoryDreamAggregateClaimRecord> claims,
        IReadOnlyList<CognitiveMemoryDreamAggregateClaimSourceMapRecord> sourceMaps,
        IReadOnlyList<CognitiveMemoryRecord> existingGeneratedMemories,
        IReadOnlyList<CognitiveMemoryClaimRecord> existingGeneratedClaims,
        IReadOnlyList<CognitiveMemorySourceLinkRecord> existingGeneratedSourceLinks)
    {
        if (existingGeneratedMemories.Count == 0)
        {
            return false;
        }

        var candidateClaimSignatures = claims
            .Select(claim => BuildTextSignature(claim.ClaimText))
            .Where(signature => signature.Count > 0)
            .ToArray();
        var candidateCanonicalSignature = BuildTextSignature(candidate.CanonicalText);
        var candidateSourceItemIds = sourceMaps
            .Select(sourceMap => sourceMap.SourceItemId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToHashSet();
        foreach (var existing in existingGeneratedMemories)
        {
            if (string.Equals(existing.Title, candidate.Title, StringComparison.Ordinal))
            {
                return true;
            }

            var existingSourceItemIds = existingGeneratedSourceLinks
                .Where(link => link.MemoryRecordId == existing.Id)
                .Select(link => link.SourceItemId)
                .Distinct()
                .ToHashSet();
            if (!HasMeaningfulSourceOverlap(candidateSourceItemIds, existingSourceItemIds))
            {
                continue;
            }

            var existingClaimSignatures = existingGeneratedClaims
                .Where(claim => claim.MemoryRecordId == existing.Id)
                .Select(claim => BuildTextSignature(claim.ClaimText))
                .Where(signature => signature.Count > 0)
                .DefaultIfEmpty(BuildTextSignature(FirstNonEmpty(existing.CanonicalText, existing.SummaryText, existing.Title)))
                .ToArray();
            if (candidateClaimSignatures.Any(candidateSignature => existingClaimSignatures.Any(existingSignature => IsNearDuplicateSignature(candidateSignature, existingSignature))) ||
                IsNearDuplicateSignature(candidateCanonicalSignature, BuildTextSignature(FirstNonEmpty(existing.CanonicalText, existing.SummaryText, existing.Title))))
            {
                return true;
            }
        }

        return false;
    }

    private CognitiveMemoryDreamEntailmentResult ValidateClaimSupport(
        CognitiveMemoryDreamAggregateClaimRecord claim,
        IReadOnlyList<CognitiveMemoryDreamAggregateClaimSourceMapRecord> sourceMaps,
        IReadOnlyDictionary<Guid, CognitiveMemoryRecord> sourceRecordsById)
    {
        if (sourceMaps.Any(sourceMap => sourceMap.Direction != CognitiveMemoryEvidenceDirection.Supports))
        {
            return new CognitiveMemoryDreamEntailmentResult(false, "At least one mapped source is not marked as supporting evidence.");
        }

        var sourceTexts = sourceMaps
            .Select(sourceMap =>
            {
                sourceRecordsById.TryGetValue(sourceMap.SourceMemoryRecordId, out var sourceRecord);
                return sourceRecord is null
                    ? sourceMap.Summary
                    : $"{sourceRecord.Title} {sourceRecord.CanonicalText} {sourceRecord.SummaryText} {sourceMap.Summary}";
            })
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToArray();
        return entailmentValidator.Validate(new CognitiveMemoryDreamEntailmentRequest(claim.ClaimText, sourceTexts));
    }

    private static bool IsRepresentativeCopyAggregate(
        IReadOnlyList<CognitiveMemoryDreamAggregateClaimRecord> claims,
        IReadOnlyList<CognitiveMemoryDreamAggregateClaimSourceMapRecord> sourceMaps,
        IReadOnlyList<CognitiveMemoryRecord> sourceRecords)
    {
        if (claims.Count == 0)
        {
            return false;
        }

        var sourceTexts = sourceRecords
            .SelectMany(record => new[] { record.CanonicalText, record.SummaryText })
            .Select(NormalizeCopyText)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var copiedClaimCount = claims.Count(claim => sourceTexts.Contains(NormalizeCopyText(claim.ClaimText)));
        return copiedClaimCount == claims.Count &&
               claims.All(claim => sourceMaps.Count(sourceMap => sourceMap.AggregateClaimId == claim.Id) <= 1);
    }

    private static string NormalizeCopyText(string text)
        => CognitiveMemoryQualityText.TrimText(text.Trim().TrimEnd('.'), 1200);

    private static bool HasMeaningfulSourceOverlap(
        IReadOnlySet<Guid> candidateSourceItemIds,
        IReadOnlySet<Guid> existingSourceItemIds)
    {
        if (candidateSourceItemIds.Count == 0 || existingSourceItemIds.Count == 0)
        {
            return false;
        }

        var overlap = candidateSourceItemIds.Count(existingSourceItemIds.Contains);
        return overlap >= Math.Min(2, Math.Min(candidateSourceItemIds.Count, existingSourceItemIds.Count));
    }

    private static bool IsNearDuplicateSignature(
        IReadOnlySet<string> left,
        IReadOnlySet<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
        {
            return false;
        }

        var overlap = left.Count(right.Contains);
        var union = left.Count + right.Count - overlap;
        return overlap >= 4 && (double)overlap / union >= 0.6;
    }

    private static HashSet<string> BuildTextSignature(string text)
        => CognitiveMemoryDreamEntailmentValidator.BuildTextSignature(text);

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

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
