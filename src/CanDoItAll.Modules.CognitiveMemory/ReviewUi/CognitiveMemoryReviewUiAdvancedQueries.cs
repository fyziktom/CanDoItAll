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

        return (await sessionsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(session => session.Status == CognitiveMemoryProbeSessionStatus.Active)
            .ThenByDescending(session => session.UpdatedAtUtc)
            .Take(query.Take)
            .Select(session => new CognitiveMemoryProbeSessionView(
                session.Id,
                session.ProjectId,
                session.Status,
                session.RecallMode,
                session.Title,
                session.TurnCount,
                session.UpdatedAtUtc))
            .ToArray();
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

        return (await assessmentsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(assessment => assessment.State != CognitiveMemorySelfRegulationStateKind.Calibrated)
            .ThenByDescending(assessment => assessment.CreatedAtUtc)
            .Take(query.Take)
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
            .ToArray();
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

        return (await decisionsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(decision => decision.DecisionKind != CognitiveMemoryAnswerGateDecisionKind.Answer)
            .ThenByDescending(decision => decision.CreatedAtUtc)
            .Take(query.Take)
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
            .ToArray();
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

        return (await reviewsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(review => review.Status == CognitiveMemoryProfessorReviewStatus.Requested)
            .ThenByDescending(review => review.CreatedAtUtc)
            .Take(query.Take)
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
            .ToArray();
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

        return (await proposalsQuery
            .ToListAsync(cancellationToken))
            .OrderBy(proposal => proposal.Status == CognitiveMemoryLearningProposalStatus.PendingApproval ? 0 : 1)
            .ThenByDescending(proposal => proposal.DisplayPriorityProjection)
            .ThenByDescending(proposal => proposal.CreatedAtUtc)
            .Take(query.Take)
            .Select(proposal => new CognitiveMemoryLearningProposalView(
                proposal.Id,
                proposal.ProjectId,
                proposal.Status,
                proposal.Title,
                proposal.Explanation,
                proposal.NeedBucket,
                proposal.DisplayPriorityProjection,
                proposal.CreatedAtUtc))
            .ToArray();
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

        return (await candidatesQuery
            .ToListAsync(cancellationToken))
            .OrderBy(candidate => candidate.Status == CognitiveMemoryCrossProjectPromotionStatus.PendingReview ? 0 : 1)
            .ThenByDescending(candidate => candidate.CreatedAtUtc)
            .Take(query.Take)
            .Select(candidate => new CognitiveMemoryCrossProjectPromotionView(
                candidate.Id,
                candidate.SourceProjectId,
                candidate.SourceMemoryRecordId,
                candidate.Status,
                candidate.PromotionBucket,
                candidate.Reason,
                candidate.ReviewItemId,
                candidate.CreatedAtUtc))
            .ToArray();
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

        return (await jobsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(job => job.State == CognitiveMemoryDistributedJobState.Rejected ||
                                      job.State == CognitiveMemoryDistributedJobState.Expired)
            .ThenByDescending(job => job.UpdatedAtUtc)
            .Take(query.Take)
            .Select(job => new CognitiveMemoryDistributedJobView(
                job.Id,
                job.ProjectId,
                job.JobKind,
                job.State,
                job.SourceScopeKey,
                job.LeasedWorkerId,
                job.CreatedAtUtc,
                job.UpdatedAtUtc))
            .ToArray();
    }
}
