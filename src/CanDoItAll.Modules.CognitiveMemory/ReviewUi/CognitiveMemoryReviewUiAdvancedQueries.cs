using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryReviewUiService
{
    private static async Task<IReadOnlyList<CognitiveMemoryProbeSessionView>> LoadProbeSessionsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var sessionsQuery = dbContext.Set<CognitiveMemoryProbeSessionRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            sessionsQuery = sessionsQuery.Where(session => session.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.ProbeSessions);
        var orderedSessions = sessionsQuery
            .OrderByDescending(session => session.Status == CognitiveMemoryProbeSessionStatus.Active);
        return await (UsesSqlite(dbContext)
                ? orderedSessions.ThenBy(session => session.Id)
                : orderedSessions.ThenByDescending(session => session.UpdatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(session => new CognitiveMemoryProbeSessionView(
                session.Id,
                session.ProjectId,
                session.Status,
                session.RecallMode,
                session.Title,
                session.TurnCount,
                session.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemorySelfRegulationView>> LoadSelfRegulationAssessmentsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var assessmentsQuery = dbContext.Set<CognitiveMemorySelfRegulationAssessmentRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            assessmentsQuery = assessmentsQuery.Where(assessment => assessment.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.SelfRegulationAssessments);
        var orderedAssessments = assessmentsQuery
            .OrderByDescending(assessment => assessment.State != CognitiveMemorySelfRegulationStateKind.Calibrated);
        return await (UsesSqlite(dbContext)
                ? orderedAssessments.ThenBy(assessment => assessment.Id)
                : orderedAssessments.ThenByDescending(assessment => assessment.CreatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(assessment => new CognitiveMemorySelfRegulationView(
                assessment.Id,
                assessment.ProjectId,
                assessment.State,
                assessment.AssessmentBucket,
                assessment.DisplayAssessmentScore,
                assessment.DomainKey,
                assessment.TaskTypeKey,
                assessment.WarningsJson,
                assessment.RequiredOperationsJson,
                assessment.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryAnswerGateView>> LoadAnswerGateDecisionsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var decisionsQuery = dbContext.Set<CognitiveMemoryAnswerGateDecisionRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            decisionsQuery = decisionsQuery.Where(decision => decision.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.AnswerGateDecisions);
        var orderedDecisions = decisionsQuery
            .OrderByDescending(decision => decision.DecisionKind != CognitiveMemoryAnswerGateDecisionKind.Answer);
        return await (UsesSqlite(dbContext)
                ? orderedDecisions.ThenBy(decision => decision.Id)
                : orderedDecisions.ThenByDescending(decision => decision.CreatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(decision => new CognitiveMemoryAnswerGateView(
                decision.Id,
                decision.ProjectId,
                decision.DecisionKind,
                decision.DecisionBucket,
                decision.DisplayConfidenceProjection,
                decision.Reason,
                decision.WarningsJson,
                decision.RequiredOperationsJson,
                decision.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryProfessorReviewView>> LoadProfessorReviewsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var reviewsQuery = dbContext.Set<CognitiveMemoryProfessorReviewRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            reviewsQuery = reviewsQuery.Where(review => review.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.ProfessorReviews);
        var orderedReviews = reviewsQuery
            .OrderByDescending(review => review.Status == CognitiveMemoryProfessorReviewStatus.Requested);
        return await (UsesSqlite(dbContext)
                ? orderedReviews.ThenBy(review => review.Id)
                : orderedReviews.ThenByDescending(review => review.CreatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(review => new CognitiveMemoryProfessorReviewView(
                review.Id,
                review.ProjectId,
                review.ReviewMode,
                review.Status,
                review.RequestedByActorId,
                review.InputSummary,
                review.MissingEvidence,
                review.RequiresHumanReview,
                review.CreatedAtUtc,
                review.CompletedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryLearningProposalView>> LoadLearningProposalsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var proposalsQuery = dbContext.Set<CognitiveMemoryLearningProposalRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            proposalsQuery = proposalsQuery.Where(proposal => proposal.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.LearningProposals);
        var orderedProposals = proposalsQuery
            .OrderBy(proposal => proposal.Status == CognitiveMemoryLearningProposalStatus.PendingApproval ? 0 : 1)
            .ThenByDescending(proposal => proposal.DisplayPriorityProjection);
        return await (UsesSqlite(dbContext)
                ? orderedProposals.ThenBy(proposal => proposal.Id)
                : orderedProposals.ThenByDescending(proposal => proposal.CreatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(proposal => new CognitiveMemoryLearningProposalView(
                proposal.Id,
                proposal.ProjectId,
                proposal.Status,
                proposal.Title,
                proposal.Explanation,
                proposal.NeedBucket,
                proposal.DisplayPriorityProjection,
                proposal.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryCrossProjectPromotionView>> LoadCrossProjectPromotionsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var candidatesQuery = dbContext.Set<CognitiveMemoryCrossProjectPromotionCandidateRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            candidatesQuery = candidatesQuery.Where(candidate => candidate.SourceProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.CrossProjectPromotions);
        var orderedCandidates = candidatesQuery
            .OrderBy(candidate => candidate.Status == CognitiveMemoryCrossProjectPromotionStatus.PendingReview ? 0 : 1);
        return await (UsesSqlite(dbContext)
                ? orderedCandidates.ThenBy(candidate => candidate.Id)
                : orderedCandidates.ThenByDescending(candidate => candidate.CreatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(candidate => new CognitiveMemoryCrossProjectPromotionView(
                candidate.Id,
                candidate.SourceProjectId,
                candidate.SourceMemoryRecordId,
                candidate.Status,
                candidate.PromotionBucket,
                candidate.Reason,
                candidate.ReviewItemId,
                candidate.CreatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryDistributedJobView>> LoadDistributedJobsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var jobsQuery = dbContext.Set<CognitiveMemoryDistributedJobRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            jobsQuery = jobsQuery.Where(job => job.ProjectId == projectId);
        }

        var page = ResolvePage(query, CognitiveMemoryReviewUiCollectionKind.DistributedJobs);
        var orderedJobs = jobsQuery
            .OrderByDescending(job => job.State == CognitiveMemoryDistributedJobState.Rejected ||
                                      job.State == CognitiveMemoryDistributedJobState.Expired);
        return await (UsesSqlite(dbContext)
                ? orderedJobs.ThenBy(job => job.Id)
                : orderedJobs.ThenByDescending(job => job.UpdatedAtUtc))
            .Skip(page.Skip)
            .Take(page.PageSize)
            .Select(job => new CognitiveMemoryDistributedJobView(
                job.Id,
                job.ProjectId,
                job.JobKind,
                job.State,
                job.SourceScopeKey,
                job.LeasedWorkerId,
                job.CreatedAtUtc,
                job.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);
    }
}
