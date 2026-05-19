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
    private const int DefaultTake = 12;
    private const int MaximumTake = 50;

    public async ValueTask<CognitiveMemoryReviewUiSnapshot> GetSnapshotAsync(
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var summary = await LoadSummaryAsync(dbContext, query, cancellationToken);
        var paging = CreatePagingState(query, summary);
        var pageQuery = NormalizeQueryPages(query, paging);
        var memoryRecords = await LoadMemoryRecordsAsync(dbContext, pageQuery, cancellationToken);
        var reviewItems = await LoadReviewItemsAsync(dbContext, pageQuery, cancellationToken);
        var recallTraces = await LoadRecallTracesAsync(dbContext, pageQuery, cancellationToken);
        var consolidationRuns = await LoadConsolidationRunsAsync(dbContext, pageQuery, cancellationToken);
        var projectionHealth = await LoadProjectionHealthAsync(dbContext, pageQuery, cancellationToken);
        var procedureSkills = await LoadProcedureSkillsAsync(dbContext, pageQuery, cancellationToken);
        var replayJobs = await LoadReplayJobsAsync(dbContext, pageQuery, cancellationToken);
        var probeSessions = await LoadProbeSessionsAsync(dbContext, pageQuery, cancellationToken);
        var selfRegulationAssessments = await LoadSelfRegulationAssessmentsAsync(dbContext, pageQuery, cancellationToken);
        var answerGateDecisions = await LoadAnswerGateDecisionsAsync(dbContext, pageQuery, cancellationToken);
        var professorReviews = await LoadProfessorReviewsAsync(dbContext, pageQuery, cancellationToken);
        var learningProposals = await LoadLearningProposalsAsync(dbContext, pageQuery, cancellationToken);
        var crossProjectPromotions = await LoadCrossProjectPromotionsAsync(dbContext, pageQuery, cancellationToken);
        var distributedJobs = await LoadDistributedJobsAsync(dbContext, pageQuery, cancellationToken);
        var operatorAudit = await LoadOperatorAuditAsync(dbContext, pageQuery, cancellationToken);
        var qualityClusters = await LoadQualityClustersAsync(dbContext, pageQuery, cancellationToken);
        var dreamRuns = await LoadDreamRunsAsync(dbContext, pageQuery, cancellationToken);
        var aggregateCandidates = await LoadAggregateCandidatesAsync(dbContext, pageQuery, cancellationToken);
        var synthesizedRecalls = await LoadSynthesizedRecallsAsync(dbContext, pageQuery, cancellationToken);

        return new CognitiveMemoryReviewUiSnapshot(
            summary,
            paging,
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
            distributedJobs,
            operatorAudit,
            qualityClusters,
            dreamRuns,
            aggregateCandidates,
            synthesizedRecalls);
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

        if (query.PageRequests is null)
        {
            return;
        }

        foreach (var pageRequest in query.PageRequests)
        {
            if (!Enum.IsDefined(pageRequest.CollectionKind))
            {
                throw new ArgumentOutOfRangeException(nameof(query), pageRequest.CollectionKind, "Page request collection is not supported.");
            }

            if (pageRequest.PageIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(query), pageRequest.PageIndex, "Page index must be non-negative.");
            }

            if (pageRequest.PageSize is < 1 or > MaximumTake)
            {
                throw new ArgumentOutOfRangeException(nameof(query), pageRequest.PageSize, $"Page size must be between 1 and {MaximumTake}.");
            }
        }
    }

    private static CognitiveMemoryReviewUiPagingState CreatePagingState(
        CognitiveMemoryReviewUiQuery query,
        CognitiveMemoryReviewUiSummary summary)
        => new(Enum.GetValues<CognitiveMemoryReviewUiCollectionKind>()
            .Select(collectionKind =>
            {
                var window = ResolvePage(query, collectionKind);
                var totalCount = CountFor(query, summary, collectionKind);
                var maxPageIndex = totalCount == 0
                    ? 0
                    : Math.Max(0, (int)Math.Ceiling((double)totalCount / window.PageSize) - 1);
                return new CognitiveMemoryReviewUiPageInfo(
                    collectionKind,
                    Math.Min(window.PageIndex, maxPageIndex),
                    window.PageSize,
                    totalCount);
            })
            .ToArray());

    private static CognitiveMemoryReviewUiQuery NormalizeQueryPages(
        CognitiveMemoryReviewUiQuery query,
        CognitiveMemoryReviewUiPagingState paging)
        => query with
        {
            PageRequests = paging.Pages
                .Select(page => new CognitiveMemoryReviewUiPageRequest(
                    page.CollectionKind,
                    page.PageIndex,
                    page.PageSize))
                .ToArray()
        };

    private static CognitiveMemoryReviewUiPageWindow ResolvePage(
        CognitiveMemoryReviewUiQuery query,
        CognitiveMemoryReviewUiCollectionKind collectionKind)
    {
        var requested = query.PageRequests?
            .LastOrDefault(request => request.CollectionKind == collectionKind);
        var pageIndex = requested?.PageIndex ?? 0;
        var pageSize = requested?.PageSize ?? (query.Take <= 0 ? DefaultTake : query.Take);
        pageSize = Math.Clamp(pageSize, 1, MaximumTake);
        pageIndex = Math.Max(0, pageIndex);
        return new CognitiveMemoryReviewUiPageWindow(pageIndex, pageSize);
    }

    private static bool UsesSqlite(AppDbContext dbContext)
        => dbContext.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true;

    private static int CountFor(
        CognitiveMemoryReviewUiQuery query,
        CognitiveMemoryReviewUiSummary summary,
        CognitiveMemoryReviewUiCollectionKind collectionKind)
        => collectionKind switch
        {
            CognitiveMemoryReviewUiCollectionKind.MemoryRecords => summary.MemoryRecordCount,
            CognitiveMemoryReviewUiCollectionKind.ReviewItems => query.IncludeResolvedReviewItems
                ? summary.ReviewItemCount
                : summary.PendingReviewCount,
            CognitiveMemoryReviewUiCollectionKind.RecallTraces => summary.RecallTraceCount,
            CognitiveMemoryReviewUiCollectionKind.ConsolidationRuns => summary.ConsolidationRunCount,
            CognitiveMemoryReviewUiCollectionKind.ProjectionHealth => summary.ProjectionStateCount,
            CognitiveMemoryReviewUiCollectionKind.ProcedureSkills => summary.ProcedureSkillCount,
            CognitiveMemoryReviewUiCollectionKind.ReplayJobs => summary.ReplayJobCount,
            CognitiveMemoryReviewUiCollectionKind.ProbeSessions => summary.ProbeSessionCount,
            CognitiveMemoryReviewUiCollectionKind.SelfRegulationAssessments => summary.SelfRegulationAssessmentCount,
            CognitiveMemoryReviewUiCollectionKind.AnswerGateDecisions => summary.AnswerGateDecisionCount,
            CognitiveMemoryReviewUiCollectionKind.ProfessorReviews => summary.ProfessorReviewTotalCount,
            CognitiveMemoryReviewUiCollectionKind.LearningProposals => summary.LearningProposalTotalCount,
            CognitiveMemoryReviewUiCollectionKind.CrossProjectPromotions => summary.CrossProjectPromotionCount,
            CognitiveMemoryReviewUiCollectionKind.DistributedJobs => summary.DistributedJobCount,
            CognitiveMemoryReviewUiCollectionKind.OperatorAudit => summary.OperatorAuditCount,
            CognitiveMemoryReviewUiCollectionKind.QualityClusters => summary.QualityClusterCount,
            CognitiveMemoryReviewUiCollectionKind.DreamRuns => summary.DreamRunCount,
            CognitiveMemoryReviewUiCollectionKind.AggregateCandidates => summary.AggregateCandidateCount,
            CognitiveMemoryReviewUiCollectionKind.SynthesizedRecalls => summary.SynthesizedRecallCount,
            _ => 0
        };

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

    private readonly record struct CognitiveMemoryReviewUiPageWindow(
        int PageIndex,
        int PageSize)
    {
        public int Skip => PageIndex * PageSize;
    }

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
