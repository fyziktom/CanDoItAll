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
        var clusterReadiness = await dbContext.Set<CognitiveMemoryQualityClusterRecord>()
            .AsNoTracking()
            .Where(cluster => cluster.Id == candidate.ClusterId)
            .Select(cluster => (CognitiveMemoryQualityClusterReadiness?)cluster.Readiness)
            .SingleOrDefaultAsync(cancellationToken);
        var issues = ResolveIssues(candidate, claims, sourceMaps, sourceRecords, clusterReadiness, request.PolicyContext);
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
        CognitiveMemoryQualityClusterReadiness? clusterReadiness,
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

        if (clusterReadiness == CognitiveMemoryQualityClusterReadiness.Contradictory ||
            sourceMaps.Any(sourceMap => sourceMap.Direction == CognitiveMemoryEvidenceDirection.Attacks))
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
