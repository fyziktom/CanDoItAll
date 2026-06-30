using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryWorkspaceService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryWorkspaceService
{
    public async ValueTask<CognitiveMemoryWorkspaceSnapshot> GetOrCreateAsync(
        CognitiveMemoryWorkspaceOpenRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var nowUtc = clock.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ExpireStaleFramesAsync(dbContext, nowUtc, cancellationToken);

        var existingFrame = await FindActiveFrameAsync(dbContext, request.Scope, nowUtc, cancellationToken);
        if (existingFrame is not null)
        {
            existingFrame.UpdatedAtUtc = nowUtc;
            existingFrame.ConcurrencyToken = Guid.NewGuid();
            await dbContext.SaveChangesAsync(cancellationToken);
            return await BuildSnapshotAsync(dbContext, existingFrame, cancellationToken);
        }

        var frame = new CognitiveMemoryWorkspaceFrameRecord
        {
            ProjectId = request.Scope.ProjectId,
            FrameKind = request.Scope.FrameKind,
            Status = CognitiveMemoryWorkspaceFrameStatus.Active,
            OwnerUserId = request.Scope.OwnerUserId ?? string.Empty,
            OwnerAgentId = request.Scope.OwnerAgentId ?? string.Empty,
            ProcessRunId = request.Scope.ProcessRunId,
            WorkflowRunId = request.Scope.WorkflowRunId,
            ProcessStepId = request.Scope.ProcessStepId,
            ProbeSessionId = request.Scope.ProbeSessionId,
            ReviewSessionId = request.Scope.ReviewSessionId,
            LearningTaskId = request.Scope.LearningTaskId,
            ContextBudgetTokenLimit = request.ContextBudget.TokenLimit,
            ContextBudgetSectionLimit = request.ContextBudget.SectionLimit,
            ContextBudgetDetailLimit = request.ContextBudget.DetailLimit,
            CognitiveLoadScoreEvaluationTraceId = NormalizeOptional(request.CognitiveLoadScoreEvaluationTraceId),
            CognitiveLoadBucket = request.CognitiveLoadBucket,
            DisplayCognitiveLoadScore = request.DisplayCognitiveLoadScore,
            MetadataJson = SerializeMetadata(request.Metadata),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc,
            ExpiresAtUtc = request.ExpiresAtUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(frame);
        AddGoals(dbContext, frame, request.GoalStack ?? [], nowUtc);
        AddOpenQuestions(dbContext, frame, request.OpenQuestions ?? [], nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);

        return await BuildSnapshotAsync(dbContext, frame, cancellationToken);
    }

    public async ValueTask<CognitiveMemoryWorkspaceSnapshot> UpdateAsync(
        CognitiveMemoryWorkspaceUpdateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var nowUtc = clock.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await ExpireStaleFramesAsync(dbContext, nowUtc, cancellationToken);

        var frame = await dbContext.Set<CognitiveMemoryWorkspaceFrameRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.WorkspaceFrameId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Cognitive workspace frame '{request.WorkspaceFrameId}' does not exist.");

        if (frame.Status != CognitiveMemoryWorkspaceFrameStatus.Active)
        {
            throw new InvalidOperationException($"Cognitive workspace frame '{request.WorkspaceFrameId}' is '{frame.Status}', not active.");
        }

        if (frame.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= nowUtc)
        {
            frame.Status = CognitiveMemoryWorkspaceFrameStatus.Expired;
            frame.UpdatedAtUtc = nowUtc;
            frame.ConcurrencyToken = Guid.NewGuid();
            await dbContext.SaveChangesAsync(cancellationToken);
            throw new InvalidOperationException($"Cognitive workspace frame '{request.WorkspaceFrameId}' expired at '{expiresAtUtc:O}'.");
        }

        await ReplaceChildStateAsync(dbContext, frame.Id, cancellationToken);
        if (request.ContextBudget is not null)
        {
            frame.ContextBudgetTokenLimit = request.ContextBudget.TokenLimit;
            frame.ContextBudgetSectionLimit = request.ContextBudget.SectionLimit;
            frame.ContextBudgetDetailLimit = request.ContextBudget.DetailLimit;
        }

        frame.CognitiveLoadScoreEvaluationTraceId = NormalizeOptional(request.CognitiveLoadScoreEvaluationTraceId);
        frame.CognitiveLoadBucket = request.CognitiveLoadBucket;
        frame.DisplayCognitiveLoadScore = request.DisplayCognitiveLoadScore;
        frame.LastSelfRegulationAssessmentId = NormalizeOptional(request.LastSelfRegulationAssessmentId);
        frame.LastAnswerPostureDecisionId = NormalizeOptional(request.LastAnswerPostureDecisionId);
        frame.UpdatedAtUtc = nowUtc;
        frame.ConcurrencyToken = Guid.NewGuid();

        AddGoals(dbContext, frame, request.GoalStack ?? [], nowUtc);
        AddOpenQuestions(dbContext, frame, request.OpenQuestions ?? [], nowUtc);
        var budgetResult = AddFocusAndInhibition(
            dbContext,
            frame,
            request.FocusSlots ?? [],
            request.InhibitedCandidates ?? [],
            nowUtc);
        ApplyBudgetResult(frame, budgetResult);

        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildSnapshotAsync(dbContext, frame, cancellationToken);
    }

    private static async Task ExpireStaleFramesAsync(
        AppDbContext dbContext,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var expirableFrames = await dbContext.Set<CognitiveMemoryWorkspaceFrameRecord>()
            .Where(frame =>
                frame.Status == CognitiveMemoryWorkspaceFrameStatus.Active &&
                frame.ExpiresAtUtc != null)
            .ToListAsync(cancellationToken);
        var staleFrames = expirableFrames
            .Where(frame => frame.ExpiresAtUtc <= nowUtc)
            .ToList();

        foreach (var frame in staleFrames)
        {
            frame.Status = CognitiveMemoryWorkspaceFrameStatus.Expired;
            frame.UpdatedAtUtc = nowUtc;
            frame.ConcurrencyToken = Guid.NewGuid();
        }

        if (staleFrames.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static async Task<CognitiveMemoryWorkspaceFrameRecord?> FindActiveFrameAsync(
        AppDbContext dbContext,
        CognitiveMemoryWorkspaceScope scope,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var matchingFrames = await dbContext.Set<CognitiveMemoryWorkspaceFrameRecord>()
            .Where(frame =>
                frame.ProjectId == scope.ProjectId &&
                frame.FrameKind == scope.FrameKind &&
                frame.Status == CognitiveMemoryWorkspaceFrameStatus.Active &&
                frame.OwnerUserId == (scope.OwnerUserId ?? string.Empty) &&
                frame.OwnerAgentId == (scope.OwnerAgentId ?? string.Empty) &&
                frame.ProcessRunId == scope.ProcessRunId &&
                frame.WorkflowRunId == scope.WorkflowRunId &&
                frame.ProcessStepId == scope.ProcessStepId &&
                frame.ProbeSessionId == scope.ProbeSessionId &&
                frame.ReviewSessionId == scope.ReviewSessionId &&
                frame.LearningTaskId == scope.LearningTaskId)
            .ToListAsync(cancellationToken);

        return matchingFrames
            .Where(frame => frame.ExpiresAtUtc is null || frame.ExpiresAtUtc > nowUtc)
            .OrderByDescending(frame => frame.UpdatedAtUtc)
            .FirstOrDefault();
    }

    private static async Task ReplaceChildStateAsync(
        AppDbContext dbContext,
        Guid workspaceFrameId,
        CancellationToken cancellationToken)
    {
        var goals = await dbContext.Set<CognitiveMemoryWorkspaceGoalRecord>()
            .Where(item => item.WorkspaceFrameId == workspaceFrameId)
            .ToListAsync(cancellationToken);
        var slotAnchors = await dbContext.Set<CognitiveMemoryWorkspaceSlotEvidenceAnchorRecord>()
            .Where(item => item.WorkspaceFrameId == workspaceFrameId)
            .ToListAsync(cancellationToken);
        var slots = await dbContext.Set<CognitiveMemoryWorkingMemorySlotRecord>()
            .Where(item => item.WorkspaceFrameId == workspaceFrameId)
            .ToListAsync(cancellationToken);
        var questions = await dbContext.Set<CognitiveMemoryWorkspaceOpenQuestionRecord>()
            .Where(item => item.WorkspaceFrameId == workspaceFrameId)
            .ToListAsync(cancellationToken);
        var inhibited = await dbContext.Set<CognitiveMemoryInhibitedCandidateRecord>()
            .Where(item => item.WorkspaceFrameId == workspaceFrameId)
            .ToListAsync(cancellationToken);

        dbContext.RemoveRange(slotAnchors);
        dbContext.RemoveRange(slots);
        dbContext.RemoveRange(goals);
        dbContext.RemoveRange(questions);
        dbContext.RemoveRange(inhibited);
    }

    private static void AddGoals(
        AppDbContext dbContext,
        CognitiveMemoryWorkspaceFrameRecord frame,
        IReadOnlyList<CognitiveMemoryWorkspaceGoalDraft> goals,
        DateTimeOffset nowUtc)
    {
        foreach (var goal in goals.OrderBy(goal => goal.Sequence))
        {
            dbContext.Add(new CognitiveMemoryWorkspaceGoalRecord
            {
                WorkspaceFrameId = frame.Id,
                ProjectId = frame.ProjectId,
                Sequence = goal.Sequence,
                ParentGoalId = NormalizeOptional(goal.ParentGoalId),
                GoalKey = goal.GoalKey.Value,
                Description = CognitiveMemoryGuard.EnsureText(goal.Description, nameof(goal.Description)),
                CreatedAtUtc = nowUtc
            });
        }
    }

    private static void AddOpenQuestions(
        AppDbContext dbContext,
        CognitiveMemoryWorkspaceFrameRecord frame,
        IReadOnlyList<CognitiveMemoryWorkspaceOpenQuestionDraft> questions,
        DateTimeOffset nowUtc)
    {
        foreach (var question in questions)
        {
            dbContext.Add(new CognitiveMemoryWorkspaceOpenQuestionRecord
            {
                WorkspaceFrameId = frame.Id,
                ProjectId = frame.ProjectId,
                QuestionText = CognitiveMemoryGuard.EnsureText(question.QuestionText, nameof(question.QuestionText)),
                Reason = CognitiveMemoryGuard.EnsureText(question.Reason, nameof(question.Reason)),
                Status = question.Status,
                CreatedAtUtc = nowUtc,
                ResolvedAtUtc = question.Status == CognitiveMemoryWorkspaceOpenQuestionStatus.Open ? null : nowUtc
            });
        }
    }

    private static CognitiveMemoryWorkspaceBudgetResult AddFocusAndInhibition(
        AppDbContext dbContext,
        CognitiveMemoryWorkspaceFrameRecord frame,
        IReadOnlyList<CognitiveMemoryWorkingMemorySlotDraft> focusSlots,
        IReadOnlyList<CognitiveMemoryInhibitedCandidateDraft> inhibitedCandidates,
        DateTimeOffset nowUtc)
    {
        var acceptedSlotCount = 0;
        var inhibitedByBudgetCount = 0;
        var tokenEstimate = 0;
        var sectionEstimate = 0;
        var detailEstimate = 0;
        CognitiveMemoryBudgetLimit? limitingBudget = null;

        foreach (var slot in focusSlots)
        {
            var nextLimit = GetBudgetLimit(frame, tokenEstimate, sectionEstimate, detailEstimate, slot);
            if (nextLimit is not null)
            {
                inhibitedByBudgetCount++;
                limitingBudget ??= nextLimit;
                AddInhibitedCandidate(dbContext, frame, CreateBudgetInhibition(slot, nextLimit.Value), nowUtc);
                continue;
            }

            var slotRecord = AddFocusSlot(dbContext, frame, slot, nowUtc);
            foreach (var evidenceAnchorId in slot.EvidenceAnchorIds)
            {
                dbContext.Add(new CognitiveMemoryWorkspaceSlotEvidenceAnchorRecord
                {
                    WorkspaceSlotId = slotRecord.Id,
                    WorkspaceFrameId = frame.Id,
                    ProjectId = frame.ProjectId,
                    EvidenceAnchorId = evidenceAnchorId.Value,
                    CreatedAtUtc = nowUtc
                });
            }

            acceptedSlotCount++;
            tokenEstimate += slot.EstimatedTokenCount;
            sectionEstimate += slot.EstimatedSectionCount;
            detailEstimate += slot.EstimatedDetailCount;
        }

        foreach (var candidate in inhibitedCandidates)
        {
            AddInhibitedCandidate(dbContext, frame, candidate, nowUtc);
        }

        return new CognitiveMemoryWorkspaceBudgetResult(
            acceptedSlotCount,
            inhibitedByBudgetCount,
            tokenEstimate,
            sectionEstimate,
            detailEstimate,
            limitingBudget);
    }

    private static CognitiveMemoryWorkingMemorySlotRecord AddFocusSlot(
        AppDbContext dbContext,
        CognitiveMemoryWorkspaceFrameRecord frame,
        CognitiveMemoryWorkingMemorySlotDraft slot,
        DateTimeOffset nowUtc)
    {
        var record = new CognitiveMemoryWorkingMemorySlotRecord
        {
            WorkspaceFrameId = frame.Id,
            ProjectId = frame.ProjectId,
            SlotKind = slot.SlotKind,
            MemoryRecordId = slot.MemoryRecordId?.Value,
            ClaimId = slot.ClaimId?.Value,
            SourceItemId = slot.SourceItemId?.Value,
            ProcedureSkillId = slot.ProcedureSkillId,
            RecallTraceId = slot.RecallTraceId,
            ProbeTurnId = slot.ProbeTurnId,
            WorkflowArtifactId = slot.WorkflowArtifactId,
            OpenQuestionId = slot.OpenQuestionId?.Value,
            ExternalPlaceholderKey = slot.ExternalPlaceholderKey?.Value ?? string.Empty,
            Title = slot.Title,
            Summary = slot.Summary,
            AttentionScoreEvaluationTraceId = slot.AttentionScoreEvaluationTraceId,
            AttentionBucket = slot.AttentionBucket,
            DisplayAttentionScore = slot.DisplayAttentionScore,
            SourceSufficiency = slot.SourceSufficiency,
            RiskLevel = slot.RiskLevel,
            ConfidenceBucket = slot.ConfidenceBucket,
            StalenessBucket = slot.StalenessBucket,
            InclusionReasonKind = slot.InclusionReasonKind,
            InclusionReason = slot.InclusionReason,
            RelationToActiveGoal = slot.RelationToActiveGoal,
            CompressionSummary = slot.CompressionSummary,
            EstimatedTokenCount = slot.EstimatedTokenCount,
            EstimatedSectionCount = slot.EstimatedSectionCount,
            EstimatedDetailCount = slot.EstimatedDetailCount,
            CreatedAtUtc = nowUtc
        };
        dbContext.Add(record);
        return record;
    }

    private static void AddInhibitedCandidate(
        AppDbContext dbContext,
        CognitiveMemoryWorkspaceFrameRecord frame,
        CognitiveMemoryInhibitedCandidateDraft candidate,
        DateTimeOffset nowUtc)
    {
        dbContext.Add(new CognitiveMemoryInhibitedCandidateRecord
        {
            WorkspaceFrameId = frame.Id,
            ProjectId = frame.ProjectId,
            CandidateKind = candidate.CandidateKind,
            MemoryRecordId = candidate.MemoryRecordId?.Value,
            ClaimId = candidate.ClaimId?.Value,
            SourceItemId = candidate.SourceItemId?.Value,
            ExternalCandidateKey = candidate.ExternalCandidateKey?.Value ?? string.Empty,
            ReasonKind = candidate.ReasonKind,
            Reason = candidate.Reason,
            InhibitionScoreEvaluationTraceId = candidate.InhibitionScoreEvaluationTraceId,
            InhibitionBucket = candidate.InhibitionBucket,
            DisplayRelevanceScore = candidate.DisplayRelevanceScore,
            DisplayInhibitionStrength = candidate.DisplayInhibitionStrength,
            CreatedAtUtc = nowUtc
        });
    }

    private static CognitiveMemoryInhibitedCandidateDraft CreateBudgetInhibition(
        CognitiveMemoryWorkingMemorySlotDraft slot,
        CognitiveMemoryBudgetLimit limit)
        => new(
            slot.SlotKind,
            CognitiveMemoryInhibitionReasonKind.BudgetLimit,
            $"Candidate exceeded workspace context budget limit '{limit}'.",
            slot.MemoryRecordId,
            slot.ClaimId,
            slot.SourceItemId,
            slot.ExternalPlaceholderKey,
            slot.AttentionScoreEvaluationTraceId,
            CognitiveMemoryScoreProjectionBucket.Inhibit,
            slot.DisplayAttentionScore,
            1);

    private static CognitiveMemoryBudgetLimit? GetBudgetLimit(
        CognitiveMemoryWorkspaceFrameRecord frame,
        int tokenEstimate,
        int sectionEstimate,
        int detailEstimate,
        CognitiveMemoryWorkingMemorySlotDraft slot)
    {
        if (tokenEstimate + slot.EstimatedTokenCount > frame.ContextBudgetTokenLimit)
        {
            return CognitiveMemoryBudgetLimit.TokenCount;
        }

        if (sectionEstimate + slot.EstimatedSectionCount > frame.ContextBudgetSectionLimit)
        {
            return CognitiveMemoryBudgetLimit.SectionCount;
        }

        if (detailEstimate + slot.EstimatedDetailCount > frame.ContextBudgetDetailLimit)
        {
            return CognitiveMemoryBudgetLimit.DetailCount;
        }

        return null;
    }

    private static void ApplyBudgetResult(
        CognitiveMemoryWorkspaceFrameRecord frame,
        CognitiveMemoryWorkspaceBudgetResult budgetResult)
    {
        frame.CurrentTokenEstimate = budgetResult.TokenEstimate;
        frame.CurrentSectionEstimate = budgetResult.SectionEstimate;
        frame.CurrentDetailEstimate = budgetResult.DetailEstimate;
        frame.BudgetExhausted = budgetResult.LimitingBudget is not null;
        frame.LimitingBudget = budgetResult.LimitingBudget;
    }

    private static async Task<CognitiveMemoryWorkspaceSnapshot> BuildSnapshotAsync(
        AppDbContext dbContext,
        CognitiveMemoryWorkspaceFrameRecord frame,
        CancellationToken cancellationToken)
    {
        var goals = await dbContext.Set<CognitiveMemoryWorkspaceGoalRecord>()
            .AsNoTracking()
            .Where(item => item.WorkspaceFrameId == frame.Id)
            .OrderBy(item => item.Sequence)
            .ToListAsync(cancellationToken);
        var slots = await dbContext.Set<CognitiveMemoryWorkingMemorySlotRecord>()
            .AsNoTracking()
            .Where(item => item.WorkspaceFrameId == frame.Id)
            .ToListAsync(cancellationToken);
        var questions = await dbContext.Set<CognitiveMemoryWorkspaceOpenQuestionRecord>()
            .AsNoTracking()
            .Where(item => item.WorkspaceFrameId == frame.Id)
            .ToListAsync(cancellationToken);
        var inhibited = await dbContext.Set<CognitiveMemoryInhibitedCandidateRecord>()
            .AsNoTracking()
            .Where(item => item.WorkspaceFrameId == frame.Id)
            .ToListAsync(cancellationToken);
        var detachedFrame = await dbContext.Set<CognitiveMemoryWorkspaceFrameRecord>()
            .AsNoTracking()
            .SingleAsync(item => item.Id == frame.Id, cancellationToken);

        return new CognitiveMemoryWorkspaceSnapshot(
            detachedFrame,
            goals,
            slots.OrderBy(item => item.CreatedAtUtc).ToList(),
            questions.OrderBy(item => item.CreatedAtUtc).ToList(),
            inhibited.OrderBy(item => item.CreatedAtUtc).ToList(),
            new CognitiveMemoryWorkspaceBudgetResult(
                slots.Count,
                inhibited.Count(item => item.ReasonKind == CognitiveMemoryInhibitionReasonKind.BudgetLimit),
                detachedFrame.CurrentTokenEstimate,
                detachedFrame.CurrentSectionEstimate,
                detachedFrame.CurrentDetailEstimate,
                detachedFrame.LimitingBudget));
    }

    private static string SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
        => metadata is null || metadata.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(new Dictionary<string, string>(metadata, StringComparer.Ordinal), CognitiveMemoryJson.SerializerOptions);

    private static Guid? NormalizeOptional(Guid? value)
        => value is { } actual && actual != Guid.Empty ? actual : null;
}

public sealed class CognitiveMemoryAttentionRouter(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    IClock clock) : ICognitiveMemoryAttentionRouter
{
    public async ValueTask<CognitiveMemoryAttentionRoutingDecision> RouteAsync(
        CognitiveMemoryAttentionRoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Signals);
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        var requestText = CognitiveMemoryGuard.EnsureText(request.RequestText, nameof(request.RequestText));
        cancellationToken.ThrowIfCancellationRequested();

        var nowUtc = clock.GetUtcNow();
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var workspace = await dbContext.Set<CognitiveMemoryWorkspaceFrameRecord>()
            .SingleOrDefaultAsync(item => item.Id == request.WorkspaceFrameId.Value, cancellationToken)
            ?? throw new InvalidOperationException($"Cognitive workspace frame '{request.WorkspaceFrameId}' does not exist.");

        if (workspace.ProjectId != request.ProjectId)
        {
            throw new InvalidOperationException($"Workspace frame '{request.WorkspaceFrameId}' belongs to project '{workspace.ProjectId:D}', not '{request.ProjectId:D}'.");
        }

        if (workspace.Status != CognitiveMemoryWorkspaceFrameStatus.Active ||
            workspace.ExpiresAtUtc is { } expiresAtUtc && expiresAtUtc <= nowUtc)
        {
            throw new InvalidOperationException($"Workspace frame '{request.WorkspaceFrameId}' is not active for attention routing.");
        }

        var decisionId = CognitiveMemoryAttentionDecisionId.New();
        var vector = BuildVector(request, decisionId.Value, nowUtc);
        var shapePairs = BuildOperationShapes();
        var trace = await scoreGeometryDriver.EvaluateAsync(
            new CognitiveMemoryScoreEvaluationRequest(
                request.ProjectId,
                CognitiveMemoryScoreOwnerKind.AttentionDecision,
                decisionId.Value,
                CognitiveMemoryScoreSpaceKind.AttentionRouting,
                CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                [vector],
                shapePairs.Select(pair => pair.Shape).ToList(),
                request.Metadata),
            cancellationToken);

        var selection = SelectDecision(request, trace, shapePairs);
        await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, trace, nowUtc, cancellationToken);

        var requiredActions = BuildRequiredActions(selection.DecisionKind, trace).ToArray();
        var decisionRecord = new CognitiveMemoryAttentionDecisionRecord
        {
            Id = decisionId.Value,
            ProjectId = request.ProjectId,
            WorkspaceFrameId = request.WorkspaceFrameId.Value,
            SelfRegulationAssessmentId = NormalizeOptional(request.SelfRegulationAssessmentId),
            AnswerPostureDecisionId = NormalizeOptional(request.AnswerPostureDecisionId),
            DecisionKind = selection.DecisionKind,
            ReasonKind = selection.ReasonKind,
            RequestHash = CognitiveMemoryHash.FromUtf8(requestText).Value,
            RequestPreview = Truncate(requestText, 500),
            RoutingScoreEvaluationTraceId = trace.Id.Value,
            RoutingBucket = trace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
            DisplayPriorityProjection = trace.ScalarProjection?.DisplayScore,
            MatchedShapeCount = trace.MatchedShapes.Count,
            MissingRequiredDimensionCount = trace.MissingRequiredDimensions.Count,
            Explanation = selection.Explanation,
            RequiredNextActionsJson = JsonSerializer.Serialize(requiredActions, CognitiveMemoryJson.SerializerOptions),
            MetadataJson = SerializeMetadata(request.Metadata),
            CreatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        };
        dbContext.Add(decisionRecord);
        workspace.LastAttentionDecisionId = decisionRecord.Id;
        workspace.LastSelfRegulationAssessmentId = NormalizeOptional(request.SelfRegulationAssessmentId);
        workspace.LastAnswerPostureDecisionId = NormalizeOptional(request.AnswerPostureDecisionId);
        workspace.UpdatedAtUtc = nowUtc;
        workspace.ConcurrencyToken = Guid.NewGuid();
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryAttentionRoutingDecision(
            decisionId,
            request.ProjectId,
            request.WorkspaceFrameId,
            selection.DecisionKind,
            selection.ReasonKind,
            selection.Explanation,
            requiredActions,
            trace,
            nowUtc);
    }

    private static CognitiveMemoryScoreVectorSnapshot BuildVector(
        CognitiveMemoryAttentionRoutingRequest request,
        Guid decisionId,
        DateTimeOffset nowUtc)
    {
        var components = new List<CognitiveMemoryScoreComponent>();
        AddComponent(components, CognitiveMemoryScoreDimensionKind.SourceSufficiency, request.Signals.SourceSufficiency, decisionId, nowUtc);
        AddComponent(components, CognitiveMemoryScoreDimensionKind.ContextAmbiguity, request.Signals.ContextAmbiguity, decisionId, nowUtc);
        AddComponent(components, CognitiveMemoryScoreDimensionKind.CognitiveLoad, request.Signals.CognitiveLoad, decisionId, nowUtc);
        AddComponent(components, CognitiveMemoryScoreDimensionKind.RiskImpact, request.Signals.RiskImpact, decisionId, nowUtc);
        AddComponent(components, CognitiveMemoryScoreDimensionKind.AvailableWorkspaceEvidence, request.Signals.AvailableWorkspaceEvidence, decisionId, nowUtc);
        AddComponent(components, CognitiveMemoryScoreDimensionKind.MissingKnowledgePressure, request.Signals.MissingKnowledgePressure, decisionId, nowUtc);
        AddComponent(components, CognitiveMemoryScoreDimensionKind.CalibrationRisk, request.Signals.CalibrationRisk, decisionId, nowUtc);
        AddComponent(components, CognitiveMemoryScoreDimensionKind.ActionCost, request.Signals.ActionCost, decisionId, nowUtc);
        AddComponent(components, CognitiveMemoryScoreDimensionKind.ExpectedValue, request.Signals.ExpectedValue, decisionId, nowUtc);

        if (components.Count == 0)
        {
            components.Add(new CognitiveMemoryScoreComponent(
                CognitiveMemoryScoreDimensionKind.SourceSufficiency,
                0,
                0,
                [Evidence(decisionId, nowUtc)]));
        }

        return new CognitiveMemoryScoreVectorSnapshot(
            CognitiveMemoryScoreSpaceKind.AttentionRouting,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            components,
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion,
            nowUtc,
            CognitiveMemoryHash.FromUtf8($"{request.WorkspaceFrameId}:{request.RequestText}:{string.Join("|", components.Select(component => $"{component.DimensionKind}:{component.NormalizedValue:0.###}"))}"));
    }

    private static void AddComponent(
        List<CognitiveMemoryScoreComponent> components,
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double? value,
        Guid decisionId,
        DateTimeOffset nowUtc)
    {
        if (value is null)
        {
            return;
        }

        CognitiveMemoryScoreGuard.EnsureUnitInterval(value.Value, dimensionKind.ToString());
        components.Add(new CognitiveMemoryScoreComponent(
            dimensionKind,
            value.Value,
            1,
            [Evidence(decisionId, nowUtc)]));
    }

    private static CognitiveMemoryScoreEvidenceRef Evidence(Guid decisionId, DateTimeOffset nowUtc)
        => new(
            CognitiveMemoryScoreEvidenceKind.AttentionDecision,
            decisionId,
            1,
            nowUtc);

    private static IReadOnlyList<AttentionShapePair> BuildOperationShapes()
    {
        var schema = CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion;
        var algorithm = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion;
        return
        [
            Shape(CognitiveMemoryAttentionDecisionKind.Abstain, CognitiveMemoryScoreProjectionBucket.Abstain, "Attention route abstains because source support is low and risk is high.",
            [
                Lower(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.2),
                Higher(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.7)
            ]),
            Shape(CognitiveMemoryAttentionDecisionKind.CreateReviewItem, CognitiveMemoryScoreProjectionBucket.NeedsReview, "Attention route creates review because high-risk or conflicting memory needs governed handling.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.75)
            ]),
            Shape(CognitiveMemoryAttentionDecisionKind.AskClarification, CognitiveMemoryScoreProjectionBucket.NeedsClarification, "Attention route asks clarification because context ambiguity is high.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.ContextAmbiguity, 0.65)
            ]),
            Shape(CognitiveMemoryAttentionDecisionKind.RunSourceAudit, CognitiveMemoryScoreProjectionBucket.NeedsReview, "Attention route runs source audit because source support is weak for the requested action.",
            [
                Lower(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.45)
            ]),
            Shape(CognitiveMemoryAttentionDecisionKind.StartProbe, CognitiveMemoryScoreProjectionBucket.NeedsClarification, "Attention route starts probing because the topic is weak and interrogation has value.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.MissingKnowledgePressure, 0.65),
                Higher(CognitiveMemoryScoreDimensionKind.ExpectedValue, 0.5)
            ]),
            Shape(CognitiveMemoryAttentionDecisionKind.RequestLearningProposal, CognitiveMemoryScoreProjectionBucket.NeedsReview, "Attention route requests a learning proposal because missing knowledge is relevant and source support is poor.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.MissingKnowledgePressure, 0.75),
                Lower(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.35),
                Higher(CognitiveMemoryScoreDimensionKind.ExpectedValue, 0.7)
            ]),
            Shape(CognitiveMemoryAttentionDecisionKind.RunReplay, CognitiveMemoryScoreProjectionBucket.NeedsReview, "Attention route runs replay because calibration risk and expected value justify rehearsal.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.CalibrationRisk, 0.65),
                Higher(CognitiveMemoryScoreDimensionKind.ExpectedValue, 0.5)
            ]),
            Shape(CognitiveMemoryAttentionDecisionKind.Recall, CognitiveMemoryScoreProjectionBucket.WeakAccept, "Attention route recalls because relevant memory likely exists but is not loaded in workspace.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.45),
                Lower(CognitiveMemoryScoreDimensionKind.ContextAmbiguity, 0.5),
                Lower(CognitiveMemoryScoreDimensionKind.AvailableWorkspaceEvidence, 0.55)
            ]),
            Shape(CognitiveMemoryAttentionDecisionKind.AnswerFromWorkspace, CognitiveMemoryScoreProjectionBucket.StrongAccept, "Attention route answers from workspace because source-backed focus is sufficient and unambiguous.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.7),
                Lower(CognitiveMemoryScoreDimensionKind.ContextAmbiguity, 0.35),
                Higher(CognitiveMemoryScoreDimensionKind.AvailableWorkspaceEvidence, 0.65),
                Lower(CognitiveMemoryScoreDimensionKind.RiskImpact, 0.5)
            ])
        ];

        AttentionShapePair Shape(
            CognitiveMemoryAttentionDecisionKind decisionKind,
            CognitiveMemoryScoreProjectionBucket bucket,
            string explanation,
            IReadOnlyList<CognitiveMemoryScoreShapeComponent> components)
            => new(
                decisionKind,
                new CognitiveMemoryScoreShapeSnapshot(
                    CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
                    CognitiveMemoryScoreSpaceKind.AttentionRouting,
                    schema,
                    components,
                    radius: null,
                    bucket,
                    explanation,
                    [],
                    algorithm));

        static CognitiveMemoryScoreShapeComponent Higher(
            CognitiveMemoryScoreDimensionKind dimensionKind,
            double lowerBound)
            => new(dimensionKind, center: lowerBound, lowerBound, upperBound: null, weight: 1);

        static CognitiveMemoryScoreShapeComponent Lower(
            CognitiveMemoryScoreDimensionKind dimensionKind,
            double upperBound)
            => new(dimensionKind, center: upperBound, lowerBound: null, upperBound, weight: 1);
    }

    private static AttentionSelection SelectDecision(
        CognitiveMemoryAttentionRoutingRequest request,
        CognitiveMemoryScoreEvaluationTrace trace,
        IReadOnlyList<AttentionShapePair> shapePairs)
    {
        if (trace.MissingRequiredDimensions.Count > 0)
        {
            return new AttentionSelection(
                CognitiveMemoryAttentionDecisionKind.Abstain,
                CognitiveMemoryAttentionReasonKind.MissingRequiredDimensions,
                $"Attention routing cannot continue because required dimensions are missing: {string.Join(", ", trace.MissingRequiredDimensions.Select(dimension => dimension.DimensionKind))}.");
        }

        var requiredDecision = request.RequiredDecisionKinds?
            .Where(kind => kind != CognitiveMemoryAttentionDecisionKind.Unknown)
            .OrderByDescending(GetDecisionPriority)
            .FirstOrDefault() ?? CognitiveMemoryAttentionDecisionKind.Unknown;
        if (requiredDecision != CognitiveMemoryAttentionDecisionKind.Unknown)
        {
            return new AttentionSelection(
                requiredDecision,
                CognitiveMemoryAttentionReasonKind.RequiredOperation,
                $"Attention routing selected '{requiredDecision}' because an upstream control assessment required that operation.");
        }

        var selected = trace.MatchedShapes
            .Select(shape => shapePairs.FirstOrDefault(pair => ReferenceEquals(pair.Shape, shape)))
            .Where(pair => pair is not null)
            .OrderByDescending(pair => GetDecisionPriority(pair!.DecisionKind))
            .FirstOrDefault();
        if (selected is not null)
        {
            return new AttentionSelection(
                selected.DecisionKind,
                CognitiveMemoryAttentionReasonKind.ScoreShapeMatched,
                selected.Shape.Explanation);
        }

        return new AttentionSelection(
            CognitiveMemoryAttentionDecisionKind.Abstain,
            CognitiveMemoryAttentionReasonKind.NoSafeOperation,
            "Attention routing found no operation shape that safely matched the current workspace and request.");
    }

    private static IReadOnlyList<string> BuildRequiredActions(
        CognitiveMemoryAttentionDecisionKind decisionKind,
        CognitiveMemoryScoreEvaluationTrace trace)
    {
        if (trace.MissingRequiredDimensions.Count > 0)
        {
            return trace.MissingRequiredDimensions
                .Select(dimension => $"provide:{dimension.DimensionKind}")
                .ToList();
        }

        return decisionKind switch
        {
            CognitiveMemoryAttentionDecisionKind.Recall => ["memory.recall"],
            CognitiveMemoryAttentionDecisionKind.AnswerFromWorkspace => ["answer.render.fromWorkspace"],
            CognitiveMemoryAttentionDecisionKind.AskClarification => ["user.clarify"],
            CognitiveMemoryAttentionDecisionKind.RunSourceAudit => ["memory.source.audit"],
            CognitiveMemoryAttentionDecisionKind.StartProbe => ["memory.probe.start"],
            CognitiveMemoryAttentionDecisionKind.CreateReviewItem => ["memory.review.enqueue"],
            CognitiveMemoryAttentionDecisionKind.RequestLearningProposal => ["memory.learning.propose"],
            CognitiveMemoryAttentionDecisionKind.RunReplay => ["memory.replay.enqueue"],
            CognitiveMemoryAttentionDecisionKind.Abstain => ["answer.abstain"],
            _ => ["answer.abstain"]
        };
    }

    private static int GetDecisionPriority(CognitiveMemoryAttentionDecisionKind decisionKind)
        => decisionKind switch
        {
            CognitiveMemoryAttentionDecisionKind.Abstain => 90,
            CognitiveMemoryAttentionDecisionKind.CreateReviewItem => 80,
            CognitiveMemoryAttentionDecisionKind.AskClarification => 70,
            CognitiveMemoryAttentionDecisionKind.RequestLearningProposal => 65,
            CognitiveMemoryAttentionDecisionKind.RunSourceAudit => 60,
            CognitiveMemoryAttentionDecisionKind.StartProbe => 50,
            CognitiveMemoryAttentionDecisionKind.RunReplay => 40,
            CognitiveMemoryAttentionDecisionKind.Recall => 30,
            CognitiveMemoryAttentionDecisionKind.AnswerFromWorkspace => 20,
            _ => 0
        };

    private static string SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
        => metadata is null || metadata.Count == 0
            ? "{}"
            : JsonSerializer.Serialize(new Dictionary<string, string>(metadata, StringComparer.Ordinal), CognitiveMemoryJson.SerializerOptions);

    private static Guid? NormalizeOptional(Guid? value)
        => value is { } actual && actual != Guid.Empty ? actual : null;

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength ? value : value[..maxLength];

    private sealed record AttentionShapePair(
        CognitiveMemoryAttentionDecisionKind DecisionKind,
        CognitiveMemoryScoreShapeSnapshot Shape);

    private sealed record AttentionSelection(
        CognitiveMemoryAttentionDecisionKind DecisionKind,
        CognitiveMemoryAttentionReasonKind ReasonKind,
        string Explanation);
}
