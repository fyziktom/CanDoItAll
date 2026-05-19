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

    private static string RedactRestrictedContext(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : "[redacted by cognitive-memory professor-review access policy]";

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

