using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryReviewUiService(
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

    private static CognitiveMemoryReviewQueueItem MapReviewItem(
        CognitiveMemoryReviewItemRecord item,
        IReadOnlyDictionary<Guid, string> subjectTitles,
        IReadOnlyDictionary<Guid, CognitiveMemoryReviewCandidatePreview> candidatePreviews)
        => new(
            new CognitiveMemoryReviewItemId(item.Id),
            item.ProjectId,
            item.ReviewKind,
            item.Status,
            item.SubjectKind,
            item.SubjectId,
            subjectTitles.TryGetValue(item.SubjectId, out var title) ? title : FormatSubjectFallback(item.SubjectKind, item.SubjectId),
            item.RiskLevel,
            item.ReasonCode,
            item.ReasonText,
            item.SourceEvidenceCount,
            item.CreatedAtUtc,
            item.DecidedAtUtc,
            item.DecidedByActorId,
            item.DecisionNotes,
            item.ConcurrencyToken,
            candidatePreviews.TryGetValue(item.Id, out var preview) ? preview : null);

    private static async Task<IReadOnlyDictionary<Guid, CognitiveMemoryReviewCandidatePreview>> LoadCandidatePreviewsAsync(
        AppDbContext dbContext,
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CancellationToken cancellationToken)
    {
        var reviewItemIds = reviewItems
            .Select(item => item.Id)
            .ToArray();
        if (reviewItemIds.Length == 0)
        {
            return new Dictionary<Guid, CognitiveMemoryReviewCandidatePreview>();
        }

        var candidates = await dbContext.Set<CognitiveMemoryConsolidationCandidateRecord>()
            .AsNoTracking()
            .Where(candidate => candidate.ReviewItemId != null && reviewItemIds.Contains(candidate.ReviewItemId.Value))
            .ToListAsync(cancellationToken);
        var sourceItemIds = candidates
            .Where(candidate => candidate.SourceItemId is not null)
            .Select(candidate => candidate.SourceItemId!.Value)
            .Distinct()
            .ToArray();
        var sourceItems = sourceItemIds.Length == 0
            ? new Dictionary<Guid, CognitiveMemorySourceItemRecord>()
            : await dbContext.Set<CognitiveMemorySourceItemRecord>()
                .AsNoTracking()
                .Where(sourceItem => sourceItemIds.Contains(sourceItem.Id))
                .ToDictionaryAsync(sourceItem => sourceItem.Id, cancellationToken);

        var previews = new Dictionary<Guid, CognitiveMemoryReviewCandidatePreview>();
        foreach (var candidate in candidates)
        {
            if (candidate.ReviewItemId is not { } reviewItemId)
            {
                continue;
            }

            var payload = DeserializeCandidatePayload(candidate);
            sourceItems.TryGetValue(candidate.SourceItemId ?? Guid.Empty, out var sourceItem);
            previews[reviewItemId] = new CognitiveMemoryReviewCandidatePreview(
                candidate.Id,
                candidate.CandidateKind,
                candidate.Status,
                candidate.SourceItemId,
                candidate.EvidenceAnchorId,
                candidate.MemoryRecordId,
                candidate.MutationCommandId,
                candidate.ScoreBucket,
                candidate.DisplayPriorityProjection,
                FirstNonEmpty(payload?.Title, sourceItem?.Title, FormatSubjectFallback(CognitiveMemoryReviewSubjectKind.Run, candidate.RunId)),
                FirstNonEmpty(payload?.Summary, sourceItem?.ContentText, string.Empty),
                FirstNonEmpty(payload?.Reason, candidate.ReasonText),
                FirstNonEmpty(payload?.SourceSystem, sourceItem?.SourceSystem, string.Empty),
                FirstNonEmpty(payload?.SourceItemType, sourceItem?.SourceItemType, string.Empty),
                FirstNonEmpty(sourceItem?.Title, payload?.Title, string.Empty),
                sourceItem?.Locator ?? string.Empty,
                BuildSourceExcerpt(sourceItem?.ContentText, payload?.Summary),
                FirstNonEmpty(candidate.SourceContentHash, sourceItem?.ContentHash, payload?.SourceContentHash, string.Empty));
        }

        return previews;
    }

    private static CognitiveMemoryConsolidationCandidatePayload? DeserializeCandidatePayload(
        CognitiveMemoryConsolidationCandidateRecord candidate)
    {
        try
        {
            return JsonSerializer.Deserialize(
                candidate.PayloadJson,
                CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryConsolidationCandidatePayload);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string BuildSourceExcerpt(string? sourceContent, string? fallback)
        => TruncateForReview(
            FirstNonEmpty(sourceContent, fallback, string.Empty),
            1800);

    private static string TruncateForReview(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : $"{trimmed[..maxLength]}...";
    }

    private static async Task<IReadOnlyDictionary<Guid, string>> ResolveSubjectTitlesAsync(
        AppDbContext dbContext,
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CancellationToken cancellationToken)
    {
        var titles = new Dictionary<Guid, string>();
        await AddMemoryTitlesAsync(dbContext, titles, reviewItems, cancellationToken);
        await AddSourceItemTitlesAsync(dbContext, titles, reviewItems, cancellationToken);
        await AddProjectionTitlesAsync(dbContext, titles, reviewItems, cancellationToken);
        await AddRecallTraceTitlesAsync(dbContext, titles, reviewItems, cancellationToken);
        await AddRunTitlesAsync(dbContext, titles, reviewItems, cancellationToken);
        await AddProcedureTitlesAsync(dbContext, titles, reviewItems, cancellationToken);
        await AddSimulationTitlesAsync(dbContext, titles, reviewItems, cancellationToken);
        return titles;
    }

    private static async Task AddMemoryTitlesAsync(
        AppDbContext dbContext,
        Dictionary<Guid, string> titles,
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CancellationToken cancellationToken)
    {
        var ids = GetSubjectIds(reviewItems, CognitiveMemoryReviewSubjectKind.MemoryRecord);
        if (ids.Count == 0)
        {
            return;
        }

        var records = await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record => ids.Contains(record.Id))
            .Select(record => new { record.Id, record.Title, record.TopicKey })
            .ToListAsync(cancellationToken);
        foreach (var record in records)
        {
            titles[record.Id] = FirstNonEmpty(record.Title, record.TopicKey, FormatSubjectFallback(CognitiveMemoryReviewSubjectKind.MemoryRecord, record.Id));
        }
    }

    private static async Task AddSourceItemTitlesAsync(
        AppDbContext dbContext,
        Dictionary<Guid, string> titles,
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CancellationToken cancellationToken)
    {
        var ids = GetSubjectIds(reviewItems, CognitiveMemoryReviewSubjectKind.SourceItem);
        if (ids.Count == 0)
        {
            return;
        }

        var records = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(record => ids.Contains(record.Id))
            .Select(record => new { record.Id, record.Title, record.SourceItemKey })
            .ToListAsync(cancellationToken);
        foreach (var record in records)
        {
            titles[record.Id] = FirstNonEmpty(record.Title, record.SourceItemKey, FormatSubjectFallback(CognitiveMemoryReviewSubjectKind.SourceItem, record.Id));
        }
    }

    private static async Task AddProjectionTitlesAsync(
        AppDbContext dbContext,
        Dictionary<Guid, string> titles,
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CancellationToken cancellationToken)
    {
        var ids = GetSubjectIds(reviewItems, CognitiveMemoryReviewSubjectKind.ProjectionState);
        if (ids.Count == 0)
        {
            return;
        }

        var records = await dbContext.Set<CognitiveMemoryProjectionStateRecord>()
            .AsNoTracking()
            .Where(record => ids.Contains(record.Id))
            .Select(record => new { record.Id, record.ProjectionKind, record.TargetProvider })
            .ToListAsync(cancellationToken);
        foreach (var record in records)
        {
            titles[record.Id] = $"{record.ProjectionKind} projection / {FirstNonEmpty(record.TargetProvider, "provider missing")}";
        }
    }

    private static async Task AddRecallTraceTitlesAsync(
        AppDbContext dbContext,
        Dictionary<Guid, string> titles,
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CancellationToken cancellationToken)
    {
        var ids = GetSubjectIds(reviewItems, CognitiveMemoryReviewSubjectKind.RecallTrace);
        if (ids.Count == 0)
        {
            return;
        }

        var records = await dbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .AsNoTracking()
            .Where(record => ids.Contains(record.Id))
            .Select(record => new { record.Id, record.RecallMode, record.StartedAtUtc })
            .ToListAsync(cancellationToken);
        foreach (var record in records)
        {
            titles[record.Id] = $"{record.RecallMode} recall / {record.StartedAtUtc:g}";
        }
    }

    private static async Task AddRunTitlesAsync(
        AppDbContext dbContext,
        Dictionary<Guid, string> titles,
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CancellationToken cancellationToken)
    {
        var ids = GetSubjectIds(reviewItems, CognitiveMemoryReviewSubjectKind.Run);
        if (ids.Count == 0)
        {
            return;
        }

        var records = await dbContext.Set<CognitiveMemoryRunRecord>()
            .AsNoTracking()
            .Where(record => ids.Contains(record.Id))
            .Select(record => new { record.Id, record.RunKind, record.Status })
            .ToListAsync(cancellationToken);
        foreach (var record in records)
        {
            titles[record.Id] = $"{record.RunKind} run / {record.Status}";
        }
    }

    private static async Task AddProcedureTitlesAsync(
        AppDbContext dbContext,
        Dictionary<Guid, string> titles,
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CancellationToken cancellationToken)
    {
        var ids = GetSubjectIds(reviewItems, CognitiveMemoryReviewSubjectKind.ProcedureSkill);
        if (ids.Count == 0)
        {
            return;
        }

        var records = await dbContext.Set<CognitiveMemoryProcedureSkillRecord>()
            .AsNoTracking()
            .Where(record => ids.Contains(record.Id))
            .Select(record => new { record.Id, record.Title })
            .ToListAsync(cancellationToken);
        foreach (var record in records)
        {
            titles[record.Id] = FirstNonEmpty(record.Title, FormatSubjectFallback(CognitiveMemoryReviewSubjectKind.ProcedureSkill, record.Id));
        }
    }

    private static async Task AddSimulationTitlesAsync(
        AppDbContext dbContext,
        Dictionary<Guid, string> titles,
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CancellationToken cancellationToken)
    {
        var ids = GetSubjectIds(reviewItems, CognitiveMemoryReviewSubjectKind.ProcedureSimulation);
        if (ids.Count == 0)
        {
            return;
        }

        var records = await dbContext.Set<CognitiveMemoryProcedureSimulationRecord>()
            .AsNoTracking()
            .Where(record => ids.Contains(record.Id))
            .Select(record => new { record.Id, record.OutputKind, record.Summary })
            .ToListAsync(cancellationToken);
        foreach (var record in records)
        {
            titles[record.Id] = FirstNonEmpty(record.Summary, $"{record.OutputKind} simulation");
        }
    }

    private static IReadOnlyList<Guid> GetSubjectIds(
        IReadOnlyList<CognitiveMemoryReviewItemRecord> reviewItems,
        CognitiveMemoryReviewSubjectKind subjectKind)
        => reviewItems
            .Where(item => item.SubjectKind == subjectKind)
            .Select(item => item.SubjectId)
            .Distinct()
            .ToArray();

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string FormatSubjectFallback(CognitiveMemoryReviewSubjectKind subjectKind, Guid subjectId)
        => $"{subjectKind} {subjectId:N}"[..Math.Min($"{subjectKind} {subjectId:N}".Length, subjectKind.ToString().Length + 10)];

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

    private static async Task<IReadOnlyList<CognitiveMemoryRecallTraceView>> LoadRecallTracesAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var tracesQuery = dbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            tracesQuery = tracesQuery.Where(trace => trace.ProjectId == projectId);
        }

        var traces = (await tracesQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(trace => trace.StartedAtUtc)
            .Take(query.Take)
            .ToArray();
        var traceIds = traces.Select(trace => trace.Id).ToArray();
        var stages = (await dbContext.Set<CognitiveMemoryRecallTraceStageRecord>()
            .AsNoTracking()
            .Where(stage => traceIds.Contains(stage.RecallTraceId))
            .ToListAsync(cancellationToken))
            .OrderBy(stage => stage.StartedAtUtc)
            .ToArray();
        var stagesByTrace = stages
            .GroupBy(stage => stage.RecallTraceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(stage => new CognitiveMemoryRecallStageView(
                        stage.StageKind,
                        stage.ChannelKind,
                        stage.Status,
                        stage.CandidateCount,
                        stage.SelectedCount,
                        stage.ExcludedCount,
                        stage.FailureCode,
                        stage.FailureMessage))
                    .ToArray());
        var candidates = await dbContext.Set<CognitiveMemoryRecallCandidateRecord>()
            .AsNoTracking()
            .Where(candidate => traceIds.Contains(candidate.RecallTraceId))
            .OrderByDescending(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected)
            .ThenByDescending(candidate => candidate.DisplayRankProjection)
            .ToListAsync(cancellationToken);
        var candidatesByTrace = candidates
            .GroupBy(candidate => candidate.RecallTraceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Take(6)
                    .Select(candidate => new CognitiveMemoryRecallCandidateView(
                        candidate.PrimaryChannelKind,
                        candidate.DecisionKind,
                        candidate.ExclusionReasonKind,
                        candidate.Title,
                        candidate.Summary,
                        candidate.Reason,
                        candidate.ScoreBucket,
                        candidate.DisplayRankProjection,
                        candidate.SourceRedacted))
                    .ToArray());
        var sourceRefs = await dbContext.Set<CognitiveMemoryRecallSourceRefRecord>()
            .AsNoTracking()
            .Where(sourceRef => traceIds.Contains(sourceRef.RecallTraceId))
            .OrderByDescending(sourceRef => sourceRef.IncludedInContext)
            .ThenBy(sourceRef => sourceRef.SourceSystem)
            .ToListAsync(cancellationToken);
        var sourceRefsByTrace = sourceRefs
            .GroupBy(sourceRef => sourceRef.RecallTraceId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Take(6)
                    .Select(sourceRef => new CognitiveMemoryRecallSourceReferenceView(
                        sourceRef.SourceSystem,
                        sourceRef.Locator,
                        sourceRef.Summary,
                        sourceRef.AccessLevel,
                        sourceRef.RedactionState,
                        sourceRef.IncludedInContext,
                        sourceRef.ExclusionReasonKind))
                    .ToArray());

        return traces
            .Select(trace => new CognitiveMemoryRecallTraceView(
                trace.Id,
                trace.ProjectId,
                trace.RecallMode,
                trace.Outcome,
                trace.IncludedRecordCount,
                trace.ExcludedRecordCount,
                trace.SelectedClaimCount,
                trace.SelectedEvidenceAnchorCount,
                trace.InhibitedCandidateCount,
                trace.LimitingBudget,
                trace.StartedAtUtc,
                trace.CompletedAtUtc,
                stagesByTrace.TryGetValue(trace.Id, out var stages) ? stages : [],
                candidatesByTrace.TryGetValue(trace.Id, out var candidates) ? candidates : [],
                sourceRefsByTrace.TryGetValue(trace.Id, out var sourceRefs) ? sourceRefs : []))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryConsolidationRunView>> LoadConsolidationRunsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var runsQuery = dbContext.Set<CognitiveMemoryConsolidationRunRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            runsQuery = runsQuery.Where(run => run.ProjectId == projectId);
        }

        return (await runsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(run => run.StartedAtUtc)
            .Take(query.Take)
            .Select(run => new CognitiveMemoryConsolidationRunView(
                run.Id,
                run.ProjectId,
                run.Mode,
                run.TriggerKind,
                run.Status,
                run.SourceItemsScanned,
                run.CandidatesCreated,
                run.MutationCommandsSubmitted,
                run.ReviewItemsCreated,
                run.ProjectionInvalidations,
                run.FailureCode,
                run.FailureMessage,
                run.StartedAtUtc,
                run.CompletedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryProjectionHealthView>> LoadProjectionHealthAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var projectionsQuery = dbContext.Set<CognitiveMemoryProjectionStateRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            projectionsQuery = projectionsQuery.Where(projection => projection.ProjectId == projectId);
        }

        return (await projectionsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(projection => projection.RebuildRequired)
            .ThenByDescending(projection => projection.Status == CognitiveMemoryProjectionStatus.Failed)
            .ThenByDescending(projection => projection.UpdatedAtUtc)
            .Take(query.Take)
            .Select(projection => new CognitiveMemoryProjectionHealthView(
                new CognitiveMemoryProjectionId(projection.Id),
                projection.ProjectId,
                projection.ProjectionKind,
                projection.Status,
                projection.TargetProvider,
                projection.RebuildRequired,
                projection.FailureCode,
                projection.FailureMessage,
                projection.UpdatedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryProcedureSkillView>> LoadProcedureSkillsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var skillsQuery = dbContext.Set<CognitiveMemoryProcedureSkillRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            skillsQuery = skillsQuery.Where(skill => skill.ProjectId == projectId);
        }

        return (await skillsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(skill => skill.RiskLevel)
            .ThenBy(skill => skill.Maturity)
            .ThenByDescending(skill => skill.UpdatedAtUtc)
            .Take(query.Take)
            .Select(skill => new CognitiveMemoryProcedureSkillView(
                new CognitiveMemoryProcedureSkillId(skill.Id),
                skill.ProjectId,
                skill.Title,
                skill.Maturity,
                skill.RiskLevel,
                skill.ValidationState,
                skill.AccessLevel,
                skill.MaturityBucket,
                skill.DisplayMaturityScore,
                skill.StepCount,
                skill.FailureModeCount,
                skill.ValidationEvidenceCount,
                skill.AutomationBindingCount,
                skill.UpdatedAtUtc))
            .ToArray();
    }

    private static async Task<IReadOnlyList<CognitiveMemoryReplayJobView>> LoadReplayJobsAsync(
        AppDbContext dbContext,
        CognitiveMemoryReviewUiQuery query,
        CancellationToken cancellationToken)
    {
        var jobsQuery = dbContext.Set<CognitiveMemoryReplayJobRecord>()
            .AsNoTracking();

        if (query.ProjectId is { } projectId)
        {
            jobsQuery = jobsQuery.Where(job => job.ProjectId == projectId);
        }

        return (await jobsQuery
            .ToListAsync(cancellationToken))
            .OrderByDescending(job => job.State == CognitiveMemoryReplayJobState.NeedsReview)
            .ThenByDescending(job => job.QueuePriority)
            .ThenByDescending(job => job.UpdatedAtUtc)
            .Take(query.Take)
            .Select(job => new CognitiveMemoryReplayJobView(
                job.Id,
                job.ProjectId,
                job.JobKind,
                job.State,
                job.PriorityBucket,
                job.DisplayPriorityProjection,
                job.QueuePriority,
                job.Reason,
                job.FailureCode,
                job.FailureMessage,
                job.UpdatedAtUtc))
            .ToArray();
    }
}
