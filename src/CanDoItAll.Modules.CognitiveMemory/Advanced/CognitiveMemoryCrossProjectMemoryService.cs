using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryCrossProjectMemoryService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryCrossProjectMemoryService
{
    public async ValueTask<CognitiveMemoryCrossProjectPromotionCandidateRecord> CreateCandidateAsync(
        CognitiveMemoryCrossProjectPromotionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var sourceRecord = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.SourceMemoryRecordId, cancellationToken)
            ?? throw new InvalidOperationException($"Source memory record '{request.SourceMemoryRecordId:D}' was not found.");
        if (sourceRecord.AccessLevel == CognitiveMemoryAccessLevel.Restricted && !request.PolicyContext.AllowRestrictedContent)
        {
            throw new InvalidOperationException("Restricted project memory cannot be proposed for cross-project promotion without restricted-content policy.");
        }

        var now = clock.GetUtcNow();
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            request.SourceProjectId,
            CognitiveMemoryScoreOwnerKind.CrossProjectCandidate,
            request.SourceMemoryRecordId,
            CognitiveMemoryScoreSpaceKind.CrossProjectPromotion,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, request.SemanticSimilarity),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.EntityEquivalence, request.EntityEquivalence),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ContextSeparation, request.ContextSeparation),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceReusePermission, request.SourceReusePermission),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.PolicyCompatibility, request.PolicyCompatibility),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.PrivacyRisk, sourceRecord.AccessLevel == CognitiveMemoryAccessLevel.Public ? 0.1 : 0.45),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.GlobalReuseValue, 0.75)
            ],
            request.ContextSeparation >= 0.6 || request.PolicyCompatibility < 0.5
                ? CognitiveMemoryScoreProjectionBucket.Inhibit
                : CognitiveMemoryScoreProjectionBucket.NeedsReview,
            now,
            cancellationToken);
        var reviewItem = new CognitiveMemoryReviewItemRecord
        {
            ProjectId = request.SourceProjectId,
            ReviewKind = CognitiveMemoryReviewKind.GeneratedMemory,
            SubjectKind = CognitiveMemoryReviewSubjectKind.MemoryRecord,
            SubjectId = sourceRecord.Id,
            Status = CognitiveMemoryReviewStatus.Pending,
            RiskLevel = sourceRecord.RiskLevel,
            ReasonCode = "cross-project-promotion",
            ReasonText = request.Reason.Trim(),
            SourceEvidenceCount = sourceRecord.SourceEvidenceCount,
            CreatedAtUtc = now
        };
        dbContext.Add(reviewItem);
        var candidate = new CognitiveMemoryCrossProjectPromotionCandidateRecord
        {
            SourceProjectId = request.SourceProjectId,
            SourceMemoryRecordId = request.SourceMemoryRecordId,
            Status = CognitiveMemoryCrossProjectPromotionStatus.PendingReview,
            PromotionScoreEvaluationTraceId = trace.Id.Value,
            PromotionBucket = trace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            RequestedByActorId = request.RequestedByActorId.Trim(),
            Reason = request.Reason.Trim(),
            ReviewItemId = reviewItem.Id,
            CreatedAtUtc = now
        };
        dbContext.Add(candidate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return candidate;
    }

    public async ValueTask<CognitiveMemoryCrossProjectPromotionCandidateRecord> DecideAsync(
        Guid candidateId,
        CognitiveMemoryCrossProjectPromotionStatus decision,
        string actorId,
        string notes,
        CancellationToken cancellationToken = default)
    {
        if (decision is CognitiveMemoryCrossProjectPromotionStatus.Candidate or CognitiveMemoryCrossProjectPromotionStatus.PendingReview)
        {
            throw new ArgumentException("Cross-project promotion decision must be a terminal promotion state.", nameof(decision));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var candidate = await dbContext.Set<CognitiveMemoryCrossProjectPromotionCandidateRecord>()
            .SingleOrDefaultAsync(item => item.Id == candidateId, cancellationToken)
            ?? throw new InvalidOperationException($"Cross-project promotion candidate '{candidateId:D}' was not found.");
        candidate.Status = decision;
        candidate.DecidedByActorId = CognitiveMemoryGuard.EnsureText(actorId, nameof(actorId));
        candidate.DecisionNotes = notes.Trim();
        candidate.DecidedAtUtc = clock.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return candidate;
    }
}

