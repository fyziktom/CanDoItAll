using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryReviewUiService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock,
    ICognitiveMemoryConsolidationCandidateApplicator consolidationCandidateApplicator) : ICognitiveMemoryReviewUiService
{
    private const int MaximumTake = 50;

    public async ValueTask<CognitiveMemoryReviewUiSnapshot> GetSnapshotAsync(
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var summary = await LoadSummaryAsync(dbContext, query, cancellationToken);
        var memoryRecords = await LoadMemoryRecordsAsync(dbContext, query, cancellationToken);
        var reviewItems = await LoadReviewItemsAsync(dbContext, query, cancellationToken);
        var recallTraces = await LoadRecallTracesAsync(dbContext, query, cancellationToken);
        var consolidationRuns = await LoadConsolidationRunsAsync(dbContext, query, cancellationToken);
        var projectionHealth = await LoadProjectionHealthAsync(dbContext, query, cancellationToken);
        var procedureSkills = await LoadProcedureSkillsAsync(dbContext, query, cancellationToken);
        var replayJobs = await LoadReplayJobsAsync(dbContext, query, cancellationToken);
        var probeSessions = await LoadProbeSessionsAsync(dbContext, query, cancellationToken);
        var selfRegulationAssessments = await LoadSelfRegulationAssessmentsAsync(dbContext, query, cancellationToken);
        var answerGateDecisions = await LoadAnswerGateDecisionsAsync(dbContext, query, cancellationToken);
        var professorReviews = await LoadProfessorReviewsAsync(dbContext, query, cancellationToken);
        var learningProposals = await LoadLearningProposalsAsync(dbContext, query, cancellationToken);
        var crossProjectPromotions = await LoadCrossProjectPromotionsAsync(dbContext, query, cancellationToken);
        var distributedJobs = await LoadDistributedJobsAsync(dbContext, query, cancellationToken);

        return new CognitiveMemoryReviewUiSnapshot(
            summary,
            memoryRecords,
            reviewItems,
            recallTraces,
            consolidationRuns,
            projectionHealth,
            procedureSkills,
            replayJobs,
            probeSessions,
            selfRegulationAssessments,
            answerGateDecisions,
            professorReviews,
            learningProposals,
            crossProjectPromotions,
            distributedJobs);
    }

    public async ValueTask<CognitiveMemoryReviewQueueItem> DecideReviewItemAsync(
        CognitiveMemoryReviewDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateDecision(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var reviewItem = await dbContext.Set<CognitiveMemoryReviewItemRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.ReviewItemId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Review item '{request.ReviewItemId.Value:D}' was not found.");

        if (reviewItem.ConcurrencyToken != request.ExpectedConcurrencyToken)
        {
            throw new InvalidOperationException("Review item changed after it was loaded. Refresh before deciding.");
        }

        reviewItem.Status = ToReviewStatus(request.DecisionKind);
        reviewItem.DecidedAtUtc = clock.GetUtcNow();
        reviewItem.DecidedByActorId = request.ActorId.Trim();
        reviewItem.DecisionNotes = request.Notes.Trim();
        reviewItem.ConcurrencyToken = Guid.NewGuid();

        await ApplyConsolidationReviewDecisionAsync(dbContext, reviewItem, request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        var subjectTitles = await ResolveSubjectTitlesAsync(dbContext, [reviewItem], cancellationToken);
        var candidatePreviews = await LoadCandidatePreviewsAsync(dbContext, [reviewItem], cancellationToken);
        return MapReviewItem(reviewItem, subjectTitles, candidatePreviews);
    }

    private static void ValidateQuery(CognitiveMemoryReviewUiQuery query)
    {
        if (query.Take is < 1 or > MaximumTake)
        {
            throw new ArgumentOutOfRangeException(nameof(query), query.Take, $"Take must be between 1 and {MaximumTake}.");
        }
    }

    private static void ValidateDecision(CognitiveMemoryReviewDecisionRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ActorId);

        if (request.DecisionKind is not (
            CognitiveMemoryReviewDecisionKind.Approve or
            CognitiveMemoryReviewDecisionKind.Reject or
            CognitiveMemoryReviewDecisionKind.RequestChanges or
            CognitiveMemoryReviewDecisionKind.Defer))
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.DecisionKind, "Review decision is not supported.");
        }

        if (request.ExpectedConcurrencyToken == Guid.Empty)
        {
            throw new ArgumentException("Review decisions must include the expected concurrency token.", nameof(request));
        }

        if (request.DecisionKind != CognitiveMemoryReviewDecisionKind.Approve &&
            string.IsNullOrWhiteSpace(request.Notes))
        {
            throw new ArgumentException("Review decisions other than approval require decision notes.", nameof(request));
        }
    }

    private static CognitiveMemoryReviewStatus ToReviewStatus(CognitiveMemoryReviewDecisionKind decisionKind)
        => decisionKind switch
        {
            CognitiveMemoryReviewDecisionKind.Approve => CognitiveMemoryReviewStatus.Approved,
            CognitiveMemoryReviewDecisionKind.Reject => CognitiveMemoryReviewStatus.Rejected,
            CognitiveMemoryReviewDecisionKind.RequestChanges => CognitiveMemoryReviewStatus.NeedsChanges,
            CognitiveMemoryReviewDecisionKind.Defer => CognitiveMemoryReviewStatus.Deferred,
            _ => throw new ArgumentOutOfRangeException(nameof(decisionKind), decisionKind, "Review decision is not supported.")
        };

    private async Task ApplyConsolidationReviewDecisionAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewItemRecord reviewItem,
        CognitiveMemoryReviewDecisionRequest request,
        CancellationToken cancellationToken)
    {
        var candidate = await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>()
            .SingleOrDefaultAsync(item => item.ReviewItemId == reviewItem.Id, cancellationToken);
        if (candidate is null)
        {
            return;
        }

        if (request.DecisionKind == CognitiveMemoryReviewDecisionKind.Reject)
        {
            candidate.Status = CognitiveMemoryConsolidationCandidateStatus.Rejected;
            candidate.ConcurrencyToken = Guid.NewGuid();
            return;
        }

        if (request.DecisionKind != CognitiveMemoryReviewDecisionKind.Approve)
        {
            return;
        }

        var payload = JsonSerializer.Deserialize(
            candidate.PayloadJson,
            CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationCandidatePayload)
            ?? throw new JsonException($"Consolidation candidate '{candidate.Id:D}' payload was empty.");
        _ = await consolidationCandidateApplicator.ApplyAsync(
            dbContext,
            candidate,
            payload,
            CognitiveMemoryValidationState.Approved,
            CognitiveMemoryStabilityState.Active,
            request.ActorId,
            clock.GetUtcNow(),
            cancellationToken);
    }

}
