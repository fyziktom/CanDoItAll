using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryReviewUiService
{
    private static async Task<CognitiveMemoryReviewUiSummary> LoadSummaryAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var memoryRecords = dbContext.Set<CognitiveMemoryRecord>().AsNoTracking();
        var reviewItems = dbContext.Set<CognitiveMemoryReviewItemRecord>().AsNoTracking();
        var recallTraces = dbContext.Set<CognitiveMemoryRecallTraceRecord>().AsNoTracking();
        var consolidationRuns = dbContext.Set<CognitiveMemoryConsolidationRunRecord>().AsNoTracking();
        var projectionStates = dbContext.Set<CognitiveMemoryProjectionStateRecord>().AsNoTracking();
        var procedureSkills = dbContext.Set<CognitiveMemoryProcedureSkillRecord>().AsNoTracking();
        var simulations = dbContext.Set<CognitiveMemoryProcedureSimulationRecord>().AsNoTracking();
        var probeSessions = dbContext.Set<CognitiveMemoryProbeSessionRecord>().AsNoTracking();
        var selfRegulationAssessments = dbContext.Set<CognitiveMemorySelfRegulationAssessmentRecord>().AsNoTracking();
        var answerGateDecisions = dbContext.Set<CognitiveMemoryAnswerGateDecisionRecord>().AsNoTracking();
        var professorReviews = dbContext.Set<CognitiveMemoryProfessorReviewRecord>().AsNoTracking();
        var learningProposals = dbContext.Set<CognitiveMemoryLearningProposalRecord>().AsNoTracking();
        var crossProjectPromotions = dbContext.Set<CognitiveMemoryCrossProjectPromotionCandidateRecord>().AsNoTracking();
        var distributedJobs = dbContext.Set<CognitiveMemoryDistributedJobRecord>().AsNoTracking();
        var distributedResults = dbContext.Set<CognitiveMemoryDistributedWorkerResultRecord>().AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            memoryRecords = memoryRecords.Where(record => record.ProjectId == projectId);
            reviewItems = reviewItems.Where(item => item.ProjectId == projectId);
            recallTraces = recallTraces.Where(trace => trace.ProjectId == projectId);
            consolidationRuns = consolidationRuns.Where(run => run.ProjectId == projectId);
            projectionStates = projectionStates.Where(state => state.ProjectId == projectId);
            procedureSkills = procedureSkills.Where(skill => skill.ProjectId == projectId);
            simulations = simulations.Where(simulation => simulation.ProjectId == projectId);
            probeSessions = probeSessions.Where(session => session.ProjectId == projectId);
            selfRegulationAssessments = selfRegulationAssessments.Where(assessment => assessment.ProjectId == projectId);
            answerGateDecisions = answerGateDecisions.Where(decision => decision.ProjectId == projectId);
            professorReviews = professorReviews.Where(review => review.ProjectId == projectId);
            learningProposals = learningProposals.Where(proposal => proposal.ProjectId == projectId);
            crossProjectPromotions = crossProjectPromotions.Where(candidate => candidate.SourceProjectId == projectId);
            distributedJobs = distributedJobs.Where(job => job.ProjectId == projectId);
            distributedResults = distributedResults.Where(result => result.ProjectId == projectId);
        }

        var memoryRecordCount = await memoryRecords.CountAsync(cancellationToken);
        var pendingReviewCount = await reviewItems
            .CountAsync(item => item.Status == CognitiveMemoryReviewStatus.Pending, cancellationToken);
        var highRiskReviewCount = await reviewItems
            .CountAsync(item => item.Status == CognitiveMemoryReviewStatus.Pending &&
                                item.RiskLevel == CognitiveMemoryRiskLevel.High, cancellationToken);
        var recallTraceCount = await recallTraces.CountAsync(cancellationToken);
        var consolidationIssueCount = await consolidationRuns
            .CountAsync(run => run.Status == CognitiveMemoryRunStatus.Failed ||
                               run.Status == CognitiveMemoryRunStatus.Blocked, cancellationToken);
        var projectionIssueCount = await projectionStates
            .CountAsync(state => state.RebuildRequired ||
                                 state.Status == CognitiveMemoryProjectionStatus.RebuildRequired ||
                                 state.Status == CognitiveMemoryProjectionStatus.Failed, cancellationToken);
        var procedureReviewCount = await procedureSkills
            .CountAsync(skill => skill.ValidationState == CognitiveMemoryValidationState.NeedsHumanReview ||
                                 skill.RiskLevel == CognitiveMemoryRiskLevel.High ||
                                 skill.Maturity == CognitiveMemoryProcedureSkillMaturity.Draft ||
                                 skill.Maturity == CognitiveMemoryProcedureSkillMaturity.Observed, cancellationToken);
        var simulationReviewCount = await simulations
            .CountAsync(simulation => simulation.Status == CognitiveMemoryProcedureSimulationStatus.NeedsReview, cancellationToken);
        var probeSessionCount = await probeSessions
            .CountAsync(session => session.Status == CognitiveMemoryProbeSessionStatus.Active, cancellationToken);
        var selfRegulationActionCount = await selfRegulationAssessments
            .CountAsync(assessment => assessment.State != CognitiveMemorySelfRegulationStateKind.Calibrated, cancellationToken);
        var answerGateInterventionCount = await answerGateDecisions
            .CountAsync(decision => decision.DecisionKind != CognitiveMemoryAnswerGateDecisionKind.Answer, cancellationToken);
        var professorReviewCount = await professorReviews
            .CountAsync(review => review.Status == CognitiveMemoryProfessorReviewStatus.Requested, cancellationToken);
        var learningProposalCount = await learningProposals
            .CountAsync(proposal => proposal.Status == CognitiveMemoryLearningProposalStatus.PendingApproval, cancellationToken);
        var crossProjectReviewCount = await crossProjectPromotions
            .CountAsync(candidate => candidate.Status == CognitiveMemoryCrossProjectPromotionStatus.PendingReview, cancellationToken);
        var distributedIssueCount = await distributedJobs
            .CountAsync(job => job.State == CognitiveMemoryDistributedJobState.Rejected ||
                               job.State == CognitiveMemoryDistributedJobState.Expired, cancellationToken);
        distributedIssueCount += await distributedResults
            .CountAsync(result => result.Status == CognitiveMemoryDistributedResultStatus.Rejected, cancellationToken);

        return new CognitiveMemoryReviewUiSummary(
            memoryRecordCount,
            pendingReviewCount,
            highRiskReviewCount,
            recallTraceCount,
            consolidationIssueCount,
            projectionIssueCount,
            procedureReviewCount,
            simulationReviewCount,
            probeSessionCount,
            selfRegulationActionCount,
            answerGateInterventionCount,
            professorReviewCount,
            learningProposalCount,
            crossProjectReviewCount,
            distributedIssueCount);
    }

    private static async Task<IReadOnlyList<CognitiveMemoryExplorerItem>> LoadMemoryRecordsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var recordsQuery = dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            recordsQuery = recordsQuery.Where(record => record.ProjectId == projectId);
        }

        var records = (await recordsQuery
            .ToListAsync(cancellationToken))
            .OrderBy(record => record.ValidationState == CognitiveMemoryValidationState.NeedsHumanReview ? 0 : 1)
            .ThenByDescending(record => record.RiskLevel)
            .ThenByDescending(record => record.UpdatedAtUtc)
            .Take(query.Take)
            .ToArray();
        var recordIds = records.Select(record => record.Id).ToArray();
        var sourceLinks = (await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => recordIds.Contains(link.MemoryRecordId))
            .ToListAsync(cancellationToken))
            .OrderBy(link => link.EvidenceRole)
            .ThenBy(link => link.CreatedAtUtc)
            .ToArray();
        var sourceLinksByRecord = sourceLinks
            .GroupBy(link => link.MemoryRecordId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Take(4)
                    .Select(link => new CognitiveMemorySourceLinkView(
                        link.SourceItemId,
                        link.EvidenceRole,
                        link.Locator ?? string.Empty,
                        FirstNonEmpty(link.Summary, link.Locator ?? string.Empty, link.SourceItemId.ToString("N"))))
                    .ToArray());

        return records
            .Select(record => new CognitiveMemoryExplorerItem(
                new CognitiveMemoryRecordId(record.Id),
                record.ProjectId,
                record.Kind,
                record.Origin,
                FirstNonEmpty(record.Title, FormatSubjectFallback(CognitiveMemoryReviewSubjectKind.MemoryRecord, record.Id)),
                FirstNonEmpty(record.SummaryText, record.CanonicalText),
                record.TopicKey,
                record.ValidationState,
                record.StabilityState,
                record.SourceEvidenceCount,
                record.EvidenceAnchorCount,
                record.ConfidenceBucket,
                record.ActivationBucket,
                record.AccessLevel,
                record.RiskLevel,
                record.UpdatedAtUtc,
                sourceLinksByRecord.TryGetValue(record.Id, out var links) ? links : []))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryReviewQueueItem>> LoadReviewItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var reviewItemsQuery = dbContext.Set<CognitiveMemoryReviewItemRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            reviewItemsQuery = reviewItemsQuery.Where(item => item.ProjectId == projectId);
        }

        if (!query.IncludeResolvedReviewItems)
        {
            reviewItemsQuery = reviewItemsQuery.Where(item => item.Status == CognitiveMemoryReviewStatus.Pending);
        }

        var reviewItems = dbContext.Database.IsSqlite()
            ? (await reviewItemsQuery
                .ToListAsync(cancellationToken))
                .OrderBy(item => item.Status == CognitiveMemoryReviewStatus.Pending ? 0 : 1)
                .ThenByDescending(item => item.RiskLevel)
                .ThenByDescending(item => item.CreatedAtUtc)
                .Take(query.Take)
                .ToArray()
            : await reviewItemsQuery
                .OrderBy(item => item.Status == CognitiveMemoryReviewStatus.Pending ? 0 : 1)
                .ThenByDescending(item => item.RiskLevel)
                .ThenByDescending(item => item.CreatedAtUtc)
                .Take(query.Take)
                .ToArrayAsync(cancellationToken);
        var subjectTitles = await ResolveSubjectTitlesAsync(dbContext, reviewItems, cancellationToken);
        var candidatePreviews = await LoadCandidatePreviewsAsync(dbContext, reviewItems, cancellationToken);
        return reviewItems
            .Select(item => MapReviewItem(item, subjectTitles, candidatePreviews))
            .ToArray();
    }

}
