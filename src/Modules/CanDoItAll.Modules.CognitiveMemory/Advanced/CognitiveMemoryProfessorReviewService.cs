using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryProfessorReviewService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryProfessorReviewService
{
    public async ValueTask<CognitiveMemoryProfessorReviewRecord> RequestReviewAsync(
        CognitiveMemoryProfessorReviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var trace = await CognitiveMemoryAdvancedScoring.EvaluateAndPersistAsync(
            dbContext,
            scoreGeometryDriver,
            request.ProjectId,
            CognitiveMemoryScoreOwnerKind.ProfessorReview,
            null,
            CognitiveMemoryScoreSpaceKind.ProfessorReviewRouting,
            [
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ProfessorReviewValue, 0.85),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.ConsequenceRisk, request.PolicyContext.RiskLevel == CognitiveMemoryRiskLevel.High ? 0.9 : 0.45),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, string.IsNullOrWhiteSpace(request.ContextSummary) ? 0.85 : 0.35),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, request.PolicyContext.AllowRestrictedContent ? 0.45 : 0.1),
                CognitiveMemoryAdvancedScoring.Component(CognitiveMemoryScoreDimensionKind.RedactionPressure, request.PolicyContext.AllowRestrictedContent ? 0.35 : 0.1)
            ],
            CognitiveMemoryScoreProjectionBucket.NeedsReview,
            now,
            cancellationToken);
        var review = new CognitiveMemoryProfessorReviewRecord
        {
            ProjectId = request.ProjectId,
            ReviewMode = request.ReviewMode,
            Status = CognitiveMemoryProfessorReviewStatus.Requested,
            RequestedByActorId = CognitiveMemoryGuard.EnsureText(request.RequestedByActorId, nameof(request.RequestedByActorId)),
            ModelProfileId = request.ModelProfileId,
            PromptProfileVersion = CognitiveMemoryGuard.EnsureText(request.PromptProfileVersion, nameof(request.PromptProfileVersion)),
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            SelfRegulationAssessmentId = request.SelfRegulationAssessmentId,
            AnswerPostureDecisionId = request.AnswerPostureDecisionId,
            RoutingScoreEvaluationTraceId = trace.Id.Value,
            InputSummary = CognitiveMemoryGuard.EnsureText(request.InputSummary, nameof(request.InputSummary)),
            ContextSummary = request.PolicyContext.AllowRestrictedContent ? request.ContextSummary.Trim() : RedactRestrictedContext(request.ContextSummary),
            OutputHash = CognitiveMemoryHash.FromUtf8("requested").Value,
            CreatedAtUtc = now
        };
        dbContext.Add(review);
        foreach (var suggestionKind in request.RequestedSuggestionKinds.DefaultIfEmpty(CognitiveMemoryProfessorSuggestionKind.NoAction))
        {
            dbContext.Add(new CognitiveMemoryProfessorReviewActionRecord
            {
                ProfessorReviewId = review.Id,
                ProjectId = request.ProjectId,
                SuggestionKind = suggestionKind,
                Summary = "Requested professor review suggestion.",
                CreatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return review;
    }

    public async ValueTask<CognitiveMemoryProfessorReviewRecord> CompleteReviewAsync(
        Guid reviewId,
        string critique,
        string missingEvidence,
        CognitiveMemoryAnswerPostureKind recommendedPosture,
        IReadOnlyList<CognitiveMemoryProfessorSuggestionKind> suggestionKinds,
        CancellationToken cancellationToken = default)
    {
        if (reviewId == Guid.Empty)
        {
            throw new ArgumentException("Professor review id must not be empty.", nameof(reviewId));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var review = await dbContext.Set<CognitiveMemoryProfessorReviewRecord>()
            .SingleOrDefaultAsync(item => item.Id == reviewId, cancellationToken)
            ?? throw new InvalidOperationException($"Professor review '{reviewId:D}' was not found.");
        var now = clock.GetUtcNow();
        review.Status = CognitiveMemoryProfessorReviewStatus.Completed;
        review.Critique = CognitiveMemoryGuard.EnsureText(critique, nameof(critique));
        review.MissingEvidence = missingEvidence.Trim();
        review.RecommendedPosture = recommendedPosture;
        review.OutputHash = CognitiveMemoryHash.FromUtf8($"{review.Critique}\n{review.MissingEvidence}\n{recommendedPosture}").Value;
        review.CompletedAtUtc = now;

        foreach (var suggestionKind in suggestionKinds.DefaultIfEmpty(CognitiveMemoryProfessorSuggestionKind.NoAction).Distinct())
        {
            var action = new CognitiveMemoryProfessorReviewActionRecord
            {
                ProfessorReviewId = review.Id,
                ProjectId = review.ProjectId,
                SuggestionKind = suggestionKind,
                Summary = $"Professor review suggested {suggestionKind}.",
                CreatedAtUtc = now
            };
            if (suggestionKind == CognitiveMemoryProfessorSuggestionKind.ReviewItem)
            {
                var reviewItem = new CognitiveMemoryReviewItemRecord
                {
                    ProjectId = review.ProjectId,
                    ReviewKind = CognitiveMemoryReviewKind.GeneratedMemory,
                    SubjectKind = CognitiveMemoryReviewSubjectKind.Run,
                    SubjectId = review.Id,
                    Status = CognitiveMemoryReviewStatus.Pending,
                    RiskLevel = CognitiveMemoryRiskLevel.Medium,
                    ReasonCode = "professor-review",
                    ReasonText = "Professor review produced a governed review action.",
                    CreatedAtUtc = now
                };
                dbContext.Add(reviewItem);
                action.CreatedReviewItemId = reviewItem.Id;
            }

            if (suggestionKind == CognitiveMemoryProfessorSuggestionKind.LearningProposal && review.ProjectId is { } projectId)
            {
                var region = await EnsureKnowledgeRegionAsync(dbContext, projectId, "professor-review", now, cancellationToken);
                var gap = new CognitiveMemoryKnowledgeGapRecord
                {
                    ProjectId = projectId,
                    KnowledgeRegionId = region.Id,
                    GapKind = CognitiveMemoryKnowledgeGapKind.ProfessorSuggestedExpansion,
                    Summary = review.MissingEvidence,
                    EvidenceRefsJson = "[]",
                    CreatedAtUtc = now
                };
                dbContext.Add(gap);
                var proposal = new CognitiveMemoryLearningProposalRecord
                {
                    ProjectId = projectId,
                    KnowledgeGapId = gap.Id,
                    Status = CognitiveMemoryLearningProposalStatus.PendingApproval,
                    Title = "Professor review learning expansion",
                    Explanation = review.Critique,
                    EvidenceRefsJson = "[]",
                    Risks = new CognitiveMemoryRiskNotes("Professor review is challenge input, not source truth."),
                    AcceptanceCriteria = "Learning output must cite source refs and pass review.",
                    NeedScoreEvaluationTraceId = review.RoutingScoreEvaluationTraceId,
                    NeedBucket = CognitiveMemoryScoreProjectionBucket.NeedsReview,
                    CreatedAtUtc = now
                };
                dbContext.Add(proposal);
                action.CreatedLearningProposalId = proposal.Id;
            }

            dbContext.Add(action);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return review;
    }

    public async ValueTask<CognitiveMemoryProfessorComparisonReviewResolutionResult> ResolveComparisonAsync(
        CognitiveMemoryProfessorComparisonReviewResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.CaptureId == Guid.Empty)
        {
            throw new ArgumentException("Professor comparison resolution requires a capture id.", nameof(request));
        }

        var actorId = CognitiveMemoryGuard.EnsureText(request.ActorId, nameof(request.ActorId));
        var reason = CognitiveMemoryGuard.EnsureText(request.Reason, nameof(request.Reason));
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var capture = await dbContext.Set<CognitiveMemoryCuratorCapturedImprovementRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.CaptureId, cancellationToken)
            ?? throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' was not found.");
        if (capture.AnchorState != CognitiveMemoryProfessorAnchorState.Comparing)
        {
            throw new InvalidOperationException($"Professor anchor capture '{request.CaptureId:D}' is '{capture.AnchorState}' and cannot be resolved as a comparison review.");
        }

        var now = clock.GetUtcNow();
        var previousState = capture.AnchorState;
        Guid? derivedMemoryRecordId = request.Outcome == CognitiveMemoryProfessorComparisonReviewOutcome.AcceptAggregateMemory
            ? await ValidateAcceptedDerivedMemoryAsync(dbContext, capture, request.DerivedMemoryRecordId, cancellationToken)
            : null;
        capture.AnchorState = request.Outcome switch
        {
            CognitiveMemoryProfessorComparisonReviewOutcome.AcceptAggregateMemory => request.FadeAnchor
                ? CognitiveMemoryProfessorAnchorState.Faded
                : CognitiveMemoryProfessorAnchorState.Assimilated,
            CognitiveMemoryProfessorComparisonReviewOutcome.RejectComparisonReturnActive => CognitiveMemoryProfessorAnchorState.Active,
            CognitiveMemoryProfessorComparisonReviewOutcome.RejectAnchor => CognitiveMemoryProfessorAnchorState.Rejected,
            CognitiveMemoryProfessorComparisonReviewOutcome.RequestMoreEvidence => CognitiveMemoryProfessorAnchorState.Active,
            _ => throw new ArgumentOutOfRangeException(nameof(request), $"Unsupported professor comparison resolution outcome '{request.Outcome}'.")
        };
        capture.AssimilatedMemoryRecordId = request.Outcome == CognitiveMemoryProfessorComparisonReviewOutcome.AcceptAggregateMemory
            ? derivedMemoryRecordId
            : capture.AssimilatedMemoryRecordId;
        capture.AnchorRetiredAtUtc = capture.AnchorState is CognitiveMemoryProfessorAnchorState.Faded or CognitiveMemoryProfessorAnchorState.Rejected
            ? now
            : null;
        capture.ConcurrencyToken = Guid.NewGuid();

        CognitiveMemoryProfessorAnchorTransitionAudit.AddTransition(
            dbContext,
            capture,
            previousState,
            capture.AnchorState,
            now,
            $"ProfessorAnchorLifecycleTransition comparison review resolved by {actorId}: {request.Outcome}. {reason}",
            manualReviewConfirmed: true,
            derivedMemoryRecordId);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CognitiveMemoryProfessorComparisonReviewResolutionResult(
            capture.Id,
            capture.AnchorState,
            derivedMemoryRecordId is null ? null : new CognitiveMemoryRecordId(derivedMemoryRecordId.Value));
    }

    private static string RedactRestrictedContext(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : "[redacted by cognitive-memory professor-review access policy]";

    private static async Task<Guid> ValidateAcceptedDerivedMemoryAsync(
        AppDbContext dbContext,
        CognitiveMemoryCuratorCapturedImprovementRecord capture,
        CognitiveMemoryRecordId? derivedMemoryRecordId,
        CancellationToken cancellationToken)
    {
        if (derivedMemoryRecordId is null)
        {
            throw new InvalidOperationException("Accepting a professor comparison requires the accepted aggregate or derived memory id.");
        }

        if (capture.AppliedMemoryRecordId == derivedMemoryRecordId.Value.Value)
        {
            throw new InvalidOperationException("Professor comparison review cannot accept the direct capture memory as the derived aggregate.");
        }

        var memory = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                record => record.Id == derivedMemoryRecordId.Value.Value && record.ProjectId == capture.ProjectId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Derived memory '{derivedMemoryRecordId}' was not found in project '{capture.ProjectId:D}'.");
        if (memory.ValidationState != CognitiveMemoryValidationState.Approved ||
            memory.StabilityState is not (CognitiveMemoryStabilityState.Active or CognitiveMemoryStabilityState.Stable))
        {
            throw new InvalidOperationException($"Derived memory '{derivedMemoryRecordId}' must be approved and active before comparison review can accept it.");
        }

        return memory.Id;
    }

    private static async Task<CognitiveMemoryKnowledgeRegionRecord> EnsureKnowledgeRegionAsync(
        AppDbContext dbContext,
        Guid projectId,
        string regionKey,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var region = await dbContext.Set<CognitiveMemoryKnowledgeRegionRecord>()
            .SingleOrDefaultAsync(
                item => item.ProjectId == projectId &&
                        item.RegionKind == CognitiveMemoryKnowledgeRegionKind.Domain &&
                        item.RegionKey == regionKey,
                cancellationToken);
        if (region is not null)
        {
            return region;
        }

        region = new CognitiveMemoryKnowledgeRegionRecord
        {
            ProjectId = projectId,
            RegionKind = CognitiveMemoryKnowledgeRegionKind.Domain,
            RegionKey = regionKey,
            DisplayName = "Professor review",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Add(region);
        return region;
    }
}

