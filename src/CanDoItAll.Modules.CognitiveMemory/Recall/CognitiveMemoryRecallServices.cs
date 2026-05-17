using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed class CognitiveMemoryRecallOrchestrator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ICognitiveMemoryEmbeddingProvider embeddingProvider,
    ICognitiveMemoryProjectionAdapter projectionAdapter,
    ICognitiveMemoryScoreGeometryDriver scoreGeometryDriver,
    ICognitiveMemorySignalLedger signalLedger,
    ICognitiveMemoryWorkspaceService workspaceService,
    IClock clock,
    ILogger<CognitiveMemoryRecallOrchestrator> logger) : ICognitiveMemoryRecallOrchestrator
{
    public async ValueTask<CognitiveMemoryRecallResult> RecallAsync(
        CognitiveMemoryRecallRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);
        cancellationToken.ThrowIfCancellationRequested();

        var nowUtc = clock.GetUtcNow();
        var traceId = Guid.NewGuid();
        var queryTerms = NormalizeTerms(request.Query);
        var stages = new List<CognitiveMemoryRecallTraceStage>();
        var warnings = new List<string>();
        var workspaceFrameId = await ResolveWorkspaceFrameIdAsync(request, cancellationToken);
        var candidates = new Dictionary<Guid, RecallCandidateAccumulator>();

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.IntentAndScope,
            CognitiveMemoryRecallChannelKind.Unknown,
            CognitiveMemoryRecallStageStatus.Completed,
            candidateCount: 0,
            selectedCount: 0,
            excludedCount: 0,
            providerTrace: $"intent:{request.Intent}:mode:{request.Mode}",
            completedAtUtc: nowUtc));

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await AddLexicalCandidatesAsync(dbContext, request, queryTerms, candidates, stages, nowUtc, cancellationToken);
        await AddVectorCandidatesAsync(dbContext, request, candidates, stages, warnings, nowUtc, cancellationToken);
        await AddWorkspaceCandidatesAsync(dbContext, workspaceFrameId, candidates, stages, nowUtc, cancellationToken);
        await AddSignalActivationCandidatesAsync(request, candidates, stages, warnings, nowUtc, cancellationToken);
        await AddGraphExpansionCandidatesAsync(dbContext, request, candidates, stages, nowUtc, cancellationToken);

        var evaluatedCandidates = await EvaluateCandidatesAsync(
            dbContext,
            traceId,
            request,
            workspaceFrameId,
            queryTerms,
            candidates.Values.ToList(),
            nowUtc,
            cancellationToken);
        var focusedCandidates = SelectFocus(evaluatedCandidates, request.Budget, warnings);

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.FocusSelection,
            CognitiveMemoryRecallChannelKind.Unknown,
            CognitiveMemoryRecallStageStatus.Completed,
            evaluatedCandidates.Count,
            focusedCandidates.Count(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected),
            focusedCandidates.Count(candidate => candidate.DecisionKind is CognitiveMemoryRecallCandidateDecisionKind.Excluded or CognitiveMemoryRecallCandidateDecisionKind.Inhibited),
            "focus:score-geometry",
            limitingBudget: focusedCandidates.FirstOrDefault(candidate => candidate.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.BudgetLimit) is null ? null : CognitiveMemoryBudgetLimit.ItemCount,
            completedAtUtc: nowUtc));

        var contextPack = await BuildContextPackAsync(
            dbContext,
            traceId,
            request,
            workspaceFrameId,
            focusedCandidates,
            stages,
            warnings,
            nowUtc,
            cancellationToken);

        var selected = focusedCandidates.Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected).ToList();
        var excluded = focusedCandidates.Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Excluded).ToList();
        var inhibited = focusedCandidates.Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Inhibited).ToList();
        var selectedClaimCount = selected.SelectMany(candidate => candidate.SelectedClaimIds).Select(id => id.Value).Distinct().Count();
        var selectedEvidenceCount = contextPack.SourceRefs
            .Where(sourceRef => sourceRef.IncludedInContext && sourceRef.EvidenceAnchorId is not null)
            .Select(sourceRef => sourceRef.EvidenceAnchorId!.Value.Value)
            .Distinct()
            .Count();
        var limitingBudget = ResolveLimitingBudget(stages, focusedCandidates, contextPack.Warnings);
        var payload = new CognitiveMemoryRecallTracePayload(
            request.Mode,
            request.Intent,
            stages,
            warnings,
            request.Metadata ?? EmptyMetadata);
        var trace = new CognitiveMemoryRecallTraceRecord
        {
            Id = traceId,
            ProjectId = request.ProjectId,
            OperationMode = CognitiveMemoryOperationMode.Recall,
            RecallMode = request.Mode,
            RequestedByActorId = request.PolicyContext.ActorId,
            PolicyProfileId = request.PolicyContext.PolicyProfileId.Value,
            WorkspaceFrameId = workspaceFrameId?.Value,
            AttentionDecisionId = request.AttentionDecisionId?.Value,
            SelfRegulationAssessmentId = NormalizeOptional(request.SelfRegulationAssessmentId),
            AnswerPostureDecisionId = NormalizeOptional(request.AnswerPostureDecisionId),
            AnswerGateDecisionId = NormalizeOptional(request.AnswerGateDecisionId),
            ContextPackId = contextPack.Id.Value,
            RequestHashAlgorithm = CognitiveMemoryHashAlgorithm.Sha256,
            RequestHash = CognitiveMemoryHash.FromUtf8($"{request.ProjectId:D}:{request.Mode}:{request.Intent}:{request.Query}").Value,
            AlgorithmVersion = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion.Value,
            Outcome = CognitiveMemoryRunStatus.Succeeded,
            IncludedRecordCount = selected.Count,
            ExcludedRecordCount = excluded.Count,
            SelectedClaimCount = selectedClaimCount,
            SelectedEvidenceAnchorCount = selectedEvidenceCount,
            InhibitedCandidateCount = inhibited.Count,
            LimitingBudget = limitingBudget,
            TraceJson = JsonSerializer.Serialize(payload, CognitiveMemoryJsonSerializerContext.Default.CognitiveMemoryRecallTracePayload),
            StartedAtUtc = nowUtc,
            CompletedAtUtc = clock.GetUtcNow(),
            ConcurrencyToken = Guid.NewGuid()
        };

        dbContext.Add(trace);
        AddStageRecords(dbContext, traceId, request.ProjectId, stages, nowUtc);
        AddCandidateRecords(dbContext, traceId, workspaceFrameId, focusedCandidates, contextPack, nowUtc);
        AddContextPackRecords(dbContext, traceId, contextPack, request.Budget.ContextCharacterBudget, nowUtc);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CognitiveMemoryRecallResult(
            traceId,
            contextPack,
            focusedCandidates.Select(ToContract).ToList(),
            stages,
            warnings);
    }

    private async Task<CognitiveMemoryWorkspaceFrameId?> ResolveWorkspaceFrameIdAsync(
        CognitiveMemoryRecallRequest request,
        CancellationToken cancellationToken)
    {
        if (request.WorkspaceFrameId is { } workspaceFrameId)
        {
            return workspaceFrameId;
        }

        if (request.WorkspaceOpenRequest is null)
        {
            return null;
        }

        var workspace = await workspaceService.GetOrCreateAsync(request.WorkspaceOpenRequest, cancellationToken);
        return new CognitiveMemoryWorkspaceFrameId(workspace.Frame.Id);
    }

    private static void ValidateRequest(CognitiveMemoryRecallRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        CognitiveMemoryGuard.EnsureNonEmpty(request.ProjectId, nameof(request.ProjectId));
        CognitiveMemoryGuard.EnsureText(request.Query, nameof(request.Query));
        if (request.Intent == CognitiveMemoryRecallIntentKind.Unknown)
        {
            throw new ArgumentException("Recall intent must be explicit.", nameof(request.Intent));
        }

        if (request.Mode == CognitiveMemoryRecallMode.Unknown)
        {
            throw new ArgumentException("Recall mode must be explicit.", nameof(request.Mode));
        }

        ArgumentNullException.ThrowIfNull(request.PolicyContext);
        if (request.PolicyContext.ProjectId is { } policyProjectId && policyProjectId != request.ProjectId)
        {
            throw new InvalidOperationException($"Recall policy project '{policyProjectId:D}' does not match request project '{request.ProjectId:D}'.");
        }

        if (string.IsNullOrWhiteSpace(request.PolicyContext.ActorId))
        {
            throw new ArgumentException("Recall policy actor id is required.", nameof(request.PolicyContext.ActorId));
        }
    }

    private async Task AddLexicalCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<string> queryTerms,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (queryTerms.Count == 0)
        {
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.Lexical,
                CognitiveMemoryRecallStageStatus.Skipped,
                0,
                0,
                0,
                "lexical:empty-query",
                completedAtUtc: nowUtc));
            return;
        }

        var pattern = $"%{queryTerms[0]}%";
        var records = await BuildRecordQuery(dbContext, request)
            .Where(record =>
                EF.Functions.Like(record.Title, pattern) ||
                EF.Functions.Like(record.SummaryText, pattern) ||
                EF.Functions.Like(record.CanonicalText, pattern) ||
                EF.Functions.Like(record.TopicKey, pattern))
            .OrderBy(record => record.Title)
            .Take(request.Budget.CoarseCandidateLimit)
            .Select(record => new MemoryRecordSnapshot(
                record.Id,
                record.ProjectId,
                record.Kind,
                record.Title,
                record.SummaryText,
                record.CanonicalText,
                record.TopicKey,
                record.ValidationState,
                record.StabilityState,
                record.SourceEvidenceCount,
                record.EvidenceAnchorCount,
                record.PrimaryClaimId,
                record.PrimaryContextFrameId,
                record.AccessLevel,
                record.RiskLevel,
                record.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

        foreach (var record in records)
        {
            var candidate = GetCandidate(candidates, record);
            candidate.Channels.Add(CognitiveMemoryRecallChannelKind.Lexical);
            candidate.LexicalMatch = Math.Max(candidate.LexicalMatch ?? 0, ComputeLexicalMatch(record, queryTerms));
            candidate.Reasons.Add("Lexical channel matched durable memory text.");
        }

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
            CognitiveMemoryRecallChannelKind.Lexical,
            CognitiveMemoryRecallStageStatus.Completed,
            records.Count,
            records.Count,
            0,
            $"lexical:records:{records.Count}",
            limitingBudget: records.Count >= request.Budget.CoarseCandidateLimit ? CognitiveMemoryBudgetLimit.ItemCount : null,
            completedAtUtc: nowUtc));
    }

    private async Task AddVectorCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        List<string> warnings,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (request.ProjectionCollectionName is not { } collectionName ||
            request.ProjectionProfileId is not { } projectionProfileId ||
            request.EmbeddingProfileId is not { } embeddingProfileId)
        {
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.VectorProjection,
                CognitiveMemoryRecallStageStatus.Skipped,
                0,
                0,
                0,
                "vector:projection-options-missing",
                completedAtUtc: nowUtc));
            return;
        }

        if (!projectionAdapter.Capabilities.SupportsFilters)
        {
            warnings.Add($"Projection provider '{projectionAdapter.Capabilities.ProviderName}' does not support typed filters; vector recall was not used.");
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.VectorProjection,
                CognitiveMemoryRecallStageStatus.Unavailable,
                0,
                0,
                0,
                "vector:typed-filter-unavailable",
                failureCode: "ProjectionFiltersUnavailable",
                failureMessage: "Strict recall requires provider-side project/access filters.",
                completedAtUtc: nowUtc));
            return;
        }

        CognitiveMemoryProjectionSearchResult projectionResult;
        try
        {
            var embedding = await embeddingProvider.EmbedAsync(
                new CognitiveMemoryEmbeddingRequest(
                    embeddingProfileId,
                    request.Query,
                    new CognitiveMemoryProcessingBudget(1, request.Budget.MaxSourceBytes, TimeSpan.FromSeconds(10))),
                cancellationToken);

            projectionResult = await projectionAdapter.SearchAsync(
                new CognitiveMemoryProjectionSearchRequest(
                    collectionName,
                    projectionProfileId,
                    request.Query,
                    embedding.Vector,
                    new CognitiveMemoryPageRequest(take: request.Budget.VectorResultLimit),
                    new CognitiveMemoryProjectionFilter(
                        request.ProjectId,
                        NormalizePreferredKinds(request.PreferredRecordKinds),
                        [CognitiveMemoryProjectionKind.VectorCollection],
                        RecallReadableValidationStates,
                        GetProjectionMaximumAccessLevel(request.PolicyContext))),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Cognitive memory vector recall unavailable for ProjectId={ProjectId} Provider={Provider}.",
                request.ProjectId,
                projectionAdapter.Capabilities.ProviderName);

            warnings.Add($"Vector projection channel unavailable: {exception.GetType().Name}.");
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.VectorProjection,
                CognitiveMemoryRecallStageStatus.Unavailable,
                0,
                0,
                0,
                "vector:unavailable",
                failureCode: exception.GetType().Name,
                failureMessage: exception.Message,
                completedAtUtc: nowUtc));
            return;
        }

        var hitRecordIds = projectionResult.Hits.Select(hit => hit.MemoryRecordId.Value).Distinct().ToArray();
        var records = await LoadRecordsByIdAsync(dbContext, request, hitRecordIds, cancellationToken);
        var recordsById = records.ToDictionary(record => record.Id);
        foreach (var hit in projectionResult.Hits)
        {
            if (!recordsById.TryGetValue(hit.MemoryRecordId.Value, out var record))
            {
                continue;
            }

            var candidate = GetCandidate(candidates, record);
            candidate.Channels.Add(CognitiveMemoryRecallChannelKind.VectorProjection);
            candidate.SemanticSimilarity = Math.Max(candidate.SemanticSimilarity ?? 0, Math.Clamp(hit.ProviderScore, 0, 1));
            candidate.ProjectionPayloadHash = hit.PayloadHash.Value;
            candidate.Reasons.Add("Vector projection channel returned a provider-scoped hit.");
        }

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
            CognitiveMemoryRecallChannelKind.VectorProjection,
            CognitiveMemoryRecallStageStatus.Completed,
            projectionResult.Hits.Count,
            records.Count,
            projectionResult.Hits.Count - records.Count,
            projectionResult.ProviderTrace,
            limitingBudget: projectionResult.Hits.Count >= request.Budget.VectorResultLimit ? CognitiveMemoryBudgetLimit.ItemCount : null,
            completedAtUtc: nowUtc));
    }

    private async Task AddWorkspaceCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryWorkspaceFrameId? workspaceFrameId,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (workspaceFrameId is null)
        {
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.Workspace,
                CognitiveMemoryRecallStageStatus.Skipped,
                0,
                0,
                0,
                "workspace:not-provided",
                completedAtUtc: nowUtc));
            return;
        }

        var slots = await dbContext.Set<CognitiveMemoryWorkingMemorySlotRecord>()
            .AsNoTracking()
            .Where(slot => slot.WorkspaceFrameId == workspaceFrameId.Value.Value && slot.MemoryRecordId != null)
            .OrderBy(slot => slot.Id)
            .Take(50)
            .Select(slot => new
            {
                slot.MemoryRecordId,
                slot.SourceSufficiency,
                slot.DisplayAttentionScore,
                slot.InclusionReason
            })
            .ToListAsync(cancellationToken);
        var records = await LoadRecordsByIdAsync(
            dbContext,
            slots.Select(slot => slot.MemoryRecordId!.Value).Distinct().ToArray(),
            cancellationToken);
        var recordsById = records.ToDictionary(record => record.Id);
        foreach (var slot in slots)
        {
            if (!recordsById.TryGetValue(slot.MemoryRecordId!.Value, out var record))
            {
                continue;
            }

            var candidate = GetCandidate(candidates, record);
            candidate.Channels.Add(CognitiveMemoryRecallChannelKind.Workspace);
            candidate.WorkspaceFocusFit = Math.Max(candidate.WorkspaceFocusFit ?? 0, slot.DisplayAttentionScore ?? 0.85);
            candidate.Reasons.Add($"Workspace focus carried candidate forward: {slot.InclusionReason}");
        }

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
            CognitiveMemoryRecallChannelKind.Workspace,
            CognitiveMemoryRecallStageStatus.Completed,
            slots.Count,
            records.Count,
            slots.Count - records.Count,
            "workspace:focus-slots",
            completedAtUtc: nowUtc));
    }

    private async Task AddSignalActivationCandidatesAsync(
        CognitiveMemoryRecallRequest request,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        List<string> warnings,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var signalResult = await signalLedger.QueryAsync(
                new CognitiveMemorySignalQuery(
                    request.ProjectId,
                    request.PolicyContext,
                    new CognitiveMemoryPageRequest(take: Math.Min(50, CognitiveMemoryPageRequest.MaxTake)),
                    ConsumerKinds:
                    [
                        CognitiveMemorySignalConsumerKind.ActivationEngine,
                        CognitiveMemorySignalConsumerKind.RecallRanking
                    ]),
                cancellationToken);

            var linkedSignals = signalResult.Signals
                .Where(signal => signal.MemoryRecordId is not null)
                .ToList();
            foreach (var signal in linkedSignals)
            {
                if (!candidates.TryGetValue(signal.MemoryRecordId!.Value, out var candidate))
                {
                    continue;
                }

                candidate.Channels.Add(CognitiveMemoryRecallChannelKind.SignalActivation);
                candidate.MemoryActivation = Math.Max(candidate.MemoryActivation ?? 0, signal.DisplayMagnitudeProjection ?? 0.65);
                candidate.SignalIds.Add(signal.Id);
                candidate.Reasons.Add($"Signal activation channel contributed {signal.SignalKind} evidence.");
            }

            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.SignalActivation,
                CognitiveMemoryRecallStageStatus.Completed,
                signalResult.Signals.Count,
                linkedSignals.Count,
                signalResult.Signals.Count - linkedSignals.Count,
                "signals:recall-consumers",
                completedAtUtc: nowUtc));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(
                exception,
                "Cognitive memory signal activation recall channel unavailable for ProjectId={ProjectId}.",
                request.ProjectId);
            warnings.Add($"Signal activation channel unavailable: {exception.GetType().Name}.");
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
                CognitiveMemoryRecallChannelKind.SignalActivation,
                CognitiveMemoryRecallStageStatus.Unavailable,
                0,
                0,
                0,
                "signals:unavailable",
                failureCode: exception.GetType().Name,
                failureMessage: exception.Message,
                completedAtUtc: nowUtc));
        }
    }

    private async Task AddGraphExpansionCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        if (request.Budget.GraphExpansionDepth == 0 || candidates.Count == 0)
        {
            stages.Add(Stage(
                CognitiveMemoryRecallTraceStageKind.AssociationExpansion,
                CognitiveMemoryRecallChannelKind.Graph,
                CognitiveMemoryRecallStageStatus.Skipped,
                0,
                0,
                0,
                "graph:disabled-or-empty",
                completedAtUtc: nowUtc));
            return;
        }

        var frontier = candidates.Keys.ToArray();
        var relationLimit = Math.Max(request.Budget.CoarseCandidateLimit * Math.Max(1, request.Budget.GraphExpansionDepth), 1);
        var relations = await dbContext.Set<CognitiveMemoryRelationRecord>()
            .AsNoTracking()
            .Where(relation =>
                relation.ProjectId == request.ProjectId &&
                (frontier.Contains(relation.SourceMemoryRecordId) || frontier.Contains(relation.TargetMemoryRecordId)))
            .OrderBy(relation => relation.RelationKind)
            .Take(relationLimit)
            .Select(relation => new RelationSnapshot(
                relation.SourceMemoryRecordId,
                relation.TargetMemoryRecordId,
                relation.RelationKind,
                relation.DisplayStrengthProjection,
                relation.Reason))
            .ToListAsync(cancellationToken);
        var neighborIds = relations
            .Select(relation => frontier.Contains(relation.SourceMemoryRecordId) ? relation.TargetMemoryRecordId : relation.SourceMemoryRecordId)
            .Distinct()
            .Where(id => !candidates.ContainsKey(id))
            .ToArray();
        var records = await LoadRecordsByIdAsync(dbContext, request, neighborIds, cancellationToken);
        var recordsById = records.ToDictionary(record => record.Id);

        foreach (var relation in relations)
        {
            var neighborId = frontier.Contains(relation.SourceMemoryRecordId)
                ? relation.TargetMemoryRecordId
                : relation.SourceMemoryRecordId;
            if (!recordsById.TryGetValue(neighborId, out var record) && !candidates.TryGetValue(neighborId, out _))
            {
                continue;
            }

            var candidate = recordsById.TryGetValue(neighborId, out var loaded)
                ? GetCandidate(candidates, loaded)
                : candidates[neighborId];
            candidate.Channels.Add(CognitiveMemoryRecallChannelKind.Graph);
            candidate.GraphProximity = Math.Max(candidate.GraphProximity ?? 0, relation.DisplayStrengthProjection ?? 0.65);
            if (relation.RelationKind == CognitiveMemoryRelationKind.SemanticallyRelatedButContextSeparated)
            {
                candidate.ContextSeparation = Math.Max(candidate.ContextSeparation ?? 0, 0.95);
                candidate.ContextBoundaryReason = string.IsNullOrWhiteSpace(relation.Reason)
                    ? "Graph relation marks this memory as related but context separated."
                    : relation.Reason;
            }

            if (relation.RelationKind == CognitiveMemoryRelationKind.Contradicts)
            {
                candidate.ContradictionPressure = Math.Max(candidate.ContradictionPressure ?? 0, 0.8);
            }

            candidate.Reasons.Add($"Graph expansion followed relation {relation.RelationKind}.");
        }

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.AssociationExpansion,
            CognitiveMemoryRecallChannelKind.Graph,
            CognitiveMemoryRecallStageStatus.Completed,
            relations.Count,
            records.Count,
            0,
            $"graph:relations:{relations.Count}",
            limitingBudget: relations.Count >= relationLimit ? CognitiveMemoryBudgetLimit.ItemCount : null,
            completedAtUtc: nowUtc));
    }

    private async Task<List<EvaluatedRecallCandidate>> EvaluateCandidatesAsync(
        AppDbContext dbContext,
        Guid traceId,
        CognitiveMemoryRecallRequest request,
        CognitiveMemoryWorkspaceFrameId? workspaceFrameId,
        IReadOnlyList<string> queryTerms,
        IReadOnlyList<RecallCandidateAccumulator> candidates,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var candidateIds = candidates.Select(candidate => candidate.Record.Id).Distinct().ToArray();
        var claimsByRecordId = await LoadClaimsAsync(dbContext, candidateIds, cancellationToken);
        var evidenceByRecordId = await LoadEvidenceAnchorIdsAsync(dbContext, candidateIds, claimsByRecordId, cancellationToken);
        var evaluated = new List<EvaluatedRecallCandidate>(candidates.Count);

        foreach (var candidate in candidates)
        {
            var candidateId = CognitiveMemoryRecallCandidateId.New();
            var claims = claimsByRecordId.GetValueOrDefault(candidate.Record.Id) ?? [];
            var evidenceAnchorIds = evidenceByRecordId.GetValueOrDefault(candidate.Record.Id) ?? [];
            var vector = BuildCandidateVector(candidateId, traceId, request, candidate, claims, evidenceAnchorIds, queryTerms, nowUtc);
            var trace = await scoreGeometryDriver.EvaluateAsync(
                new CognitiveMemoryScoreEvaluationRequest(
                    request.ProjectId,
                    CognitiveMemoryScoreOwnerKind.RecallCandidate,
                    candidateId.Value,
                    CognitiveMemoryScoreSpaceKind.RecallCandidate,
                    CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
                    [vector],
                    BuildRecallCandidateShapes()),
                cancellationToken);
            await CognitiveMemoryScoreTracePersistence.AddIfMissingAsync(dbContext, trace, nowUtc, cancellationToken);

            var decision = DecideCandidate(candidate, trace, request);
            evaluated.Add(new EvaluatedRecallCandidate(
                candidateId,
                candidate.Record,
                workspaceFrameId,
                candidate.PrimaryChannelKind,
                decision.DecisionKind,
                decision.ExclusionReasonKind,
                trace,
                trace.ScalarProjection,
                claims.Select(claim => new CognitiveMemoryClaimId(claim.Id)).ToArray(),
                evidenceAnchorIds.Select(id => new CognitiveMemoryEvidenceAnchorId(id)).ToArray(),
                decision.Reason,
                candidate.Channels.ToArray(),
                candidate.ContextBoundaryReason));
        }

        return evaluated
            .OrderByDescending(candidate => candidate.ScoreTrace.ScalarProjection?.DisplayScore ?? 0)
            .ThenBy(candidate => candidate.Record.Title, StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<EvaluatedRecallCandidate> SelectFocus(
        IReadOnlyList<EvaluatedRecallCandidate> evaluatedCandidates,
        CognitiveMemoryRecallBudget budget,
        List<string> warnings)
    {
        var selectedCount = 0;
        var result = new List<EvaluatedRecallCandidate>(evaluatedCandidates.Count);
        foreach (var candidate in evaluatedCandidates)
        {
            if (candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Inhibited)
            {
                result.Add(candidate);
                continue;
            }

            if (selectedCount >= budget.FocusLimit)
            {
                warnings.Add($"Recall focus budget excluded '{candidate.Record.Title}'.");
                result.Add(candidate with
                {
                    DecisionKind = CognitiveMemoryRecallCandidateDecisionKind.Excluded,
                    ExclusionReasonKind = CognitiveMemoryRecallExclusionReasonKind.BudgetLimit,
                    Reason = "Candidate excluded by recall focus item budget."
                });
                continue;
            }

            selectedCount++;
            result.Add(candidate with
            {
                DecisionKind = CognitiveMemoryRecallCandidateDecisionKind.Selected
            });
        }

        return result;
    }

    private async Task<CognitiveMemoryRecallContextPack> BuildContextPackAsync(
        AppDbContext dbContext,
        Guid traceId,
        CognitiveMemoryRecallRequest request,
        CognitiveMemoryWorkspaceFrameId? workspaceFrameId,
        IReadOnlyList<EvaluatedRecallCandidate> candidates,
        List<CognitiveMemoryRecallTraceStage> stages,
        List<string> warnings,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken)
    {
        var selected = candidates
            .Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected)
            .Take(request.Budget.DetailItemLimit)
            .ToList();
        var limitedByDetail = candidates.Count(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Selected) > selected.Count;
        var sourceRefs = await LoadSourceRefsAsync(dbContext, request, selected, cancellationToken);
        var sourceBudget = request.Budget.MaxSourceBytes;
        var remainingCharacters = request.Budget.ContextCharacterBudget;
        var sections = new List<CognitiveMemoryRecallContextSection>();
        var sequence = 0;

        foreach (var candidate in selected)
        {
            var candidateRefs = sourceRefs
                .Where(sourceRef => sourceRef.MemoryRecordId.Value == candidate.Record.Id)
                .ToArray();
            var content = BuildSectionContent(candidate, candidateRefs, request.PolicyContext, ref sourceBudget, warnings);
            if (content.Length > remainingCharacters)
            {
                var trimmed = content[..Math.Max(0, remainingCharacters)];
                warnings.Add($"Context character budget truncated section for '{candidate.Record.Title}'.");
                content = trimmed;
            }

            if (content.Length == 0)
            {
                warnings.Add($"Context character budget excluded section for '{candidate.Record.Title}'.");
                continue;
            }

            remainingCharacters -= content.Length;
            sections.Add(new CognitiveMemoryRecallContextSection(
                new CognitiveMemorySectionId($"selected-{sequence}"),
                CognitiveMemoryRecallContextSectionKind.SelectedMemory,
                candidate.Record.Title,
                content,
                [new CognitiveMemoryRecordId(candidate.Record.Id)],
                candidate.SelectedClaimIds,
                candidateRefs));
            sequence++;

            if (remainingCharacters <= 0)
            {
                break;
            }
        }

        foreach (var inhibited in candidates.Where(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Inhibited))
        {
            if (remainingCharacters <= 0)
            {
                break;
            }

            var warning = $"Do not confuse with {inhibited.Record.Title}: {inhibited.Reason}";
            var content = warning.Length <= remainingCharacters
                ? warning
                : warning[..remainingCharacters];
            sections.Add(new CognitiveMemoryRecallContextSection(
                new CognitiveMemorySectionId($"inhibited-{sequence}"),
                CognitiveMemoryRecallContextSectionKind.DoNotConfuseWith,
                inhibited.Record.Title,
                content,
                [new CognitiveMemoryRecordId(inhibited.Record.Id)],
                inhibited.SelectedClaimIds,
                []));
            remainingCharacters -= content.Length;
            sequence++;
        }

        if (limitedByDetail)
        {
            warnings.Add("Recall detail item budget excluded one or more focused candidates from detailed source loading.");
        }

        var pack = new CognitiveMemoryRecallContextPack(
            CognitiveMemoryRecallContextPackId.New(),
            request.ProjectId,
            workspaceFrameId,
            $"Recall context for {request.Intent}",
            BuildPackSummary(selected, candidates),
            sections,
            sourceRefs,
            warnings.ToArray(),
            request.Metadata ?? EmptyMetadata);

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.DetailRetrieval,
            CognitiveMemoryRecallChannelKind.SourceDetail,
            CognitiveMemoryRecallStageStatus.Completed,
            selected.Count,
            sourceRefs.Count(sourceRef => sourceRef.IncludedInContext),
            sourceRefs.Count(sourceRef => !sourceRef.IncludedInContext),
            $"source-detail:refs:{sourceRefs.Count}",
            limitingBudget: limitedByDetail ? CognitiveMemoryBudgetLimit.DetailCount : null,
            completedAtUtc: nowUtc));
        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.ContextPackRendering,
            CognitiveMemoryRecallChannelKind.ContextPack,
            CognitiveMemoryRecallStageStatus.Completed,
            sections.Count,
            sections.Count,
            0,
            $"context-pack:chars:{request.Budget.ContextCharacterBudget - remainingCharacters}/{request.Budget.ContextCharacterBudget}",
            limitingBudget: remainingCharacters <= 0 ? CognitiveMemoryBudgetLimit.ByteCount : null,
            completedAtUtc: nowUtc));

        return pack;
    }

    private static string BuildSectionContent(
        EvaluatedRecallCandidate candidate,
        IReadOnlyList<CognitiveMemoryRecallSourceRef> sourceRefs,
        CognitiveMemoryPolicyContext policyContext,
        ref int remainingSourceBytes,
        List<string> warnings)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(candidate.Record.SummaryText))
        {
            builder.AppendLine(candidate.Record.SummaryText.Trim());
        }

        var canonical = candidate.Record.CanonicalText.Trim();
        if (canonical.Length > 0)
        {
            var bytes = Encoding.UTF8.GetByteCount(canonical);
            if (bytes <= remainingSourceBytes && PolicyCanRead(candidate.Record.AccessLevel, policyContext))
            {
                builder.AppendLine(canonical);
                remainingSourceBytes -= bytes;
            }
            else
            {
                warnings.Add($"Source byte or access budget prevented full canonical detail for '{candidate.Record.Title}'.");
            }
        }

        foreach (var sourceRef in sourceRefs.Where(sourceRef => sourceRef.IncludedInContext))
        {
            var sourceSummary = sourceRef.Summary.Trim();
            if (sourceSummary.Length == 0)
            {
                continue;
            }

            var bytes = Encoding.UTF8.GetByteCount(sourceSummary);
            if (bytes <= remainingSourceBytes)
            {
                builder.AppendLine($"Source detail: {sourceSummary}");
                remainingSourceBytes -= bytes;
                continue;
            }

            if (remainingSourceBytes > 0)
            {
                var snippet = sourceSummary[..Math.Min(sourceSummary.Length, remainingSourceBytes)];
                builder.AppendLine($"Source detail: {snippet}");
                warnings.Add($"Source byte budget truncated source detail for '{candidate.Record.Title}'.");
                remainingSourceBytes = 0;
            }
            else
            {
                warnings.Add($"Source byte budget excluded source detail for '{candidate.Record.Title}'.");
            }
        }

        foreach (var sourceRef in sourceRefs.Where(sourceRef => !sourceRef.IncludedInContext))
        {
            builder.AppendLine($"Source unavailable: {sourceRef.ExclusionReasonKind}.");
        }

        return builder.ToString().Trim();
    }

    private async Task<IReadOnlyList<CognitiveMemoryRecallSourceRef>> LoadSourceRefsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<EvaluatedRecallCandidate> selected,
        CancellationToken cancellationToken)
    {
        var memoryRecordIds = selected.Select(candidate => candidate.Record.Id).Distinct().ToArray();
        if (memoryRecordIds.Length == 0)
        {
            return [];
        }

        var sourceLinks = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => memoryRecordIds.Contains(link.MemoryRecordId))
            .Select(link => new SourceLinkSnapshot(
                link.MemoryRecordId,
                link.SourceItemId,
                link.Locator,
                link.QuoteHash,
                link.Summary))
            .ToListAsync(cancellationToken);
        var sourceItemIds = sourceLinks.Select(link => link.SourceItemId).Distinct().ToArray();
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => sourceItemIds.Contains(item.Id))
            .Select(item => new SourceItemSnapshot(
                item.Id,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemKey,
                item.Title,
                item.ContentText,
                item.Locator,
                item.RedactionState,
                item.AccessLevel))
            .ToListAsync(cancellationToken);
        var sourceItemsById = sourceItems.ToDictionary(item => item.Id);
        var evidenceLinks = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(link => memoryRecordIds.Contains(link.MemoryRecordId))
            .Select(link => new
            {
                link.MemoryRecordId,
                link.EvidenceAnchorId,
                link.Summary
            })
            .ToListAsync(cancellationToken);
        var evidenceAnchorIds = evidenceLinks.Select(link => link.EvidenceAnchorId).Distinct().ToArray();
        var anchors = await dbContext.Set<CognitiveMemoryEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(anchor => evidenceAnchorIds.Contains(anchor.Id))
            .Select(anchor => new EvidenceAnchorSnapshot(
                anchor.Id,
                anchor.SourceItemId,
                anchor.SourceSystem,
                anchor.Locator,
                anchor.QuoteHash,
                anchor.RedactionState))
            .ToListAsync(cancellationToken);
        var anchorsById = anchors.ToDictionary(anchor => anchor.Id);
        var sourceRefs = new List<CognitiveMemoryRecallSourceRef>();

        foreach (var link in sourceLinks)
        {
            sourceItemsById.TryGetValue(link.SourceItemId, out var item);
            var accessLevel = item?.AccessLevel ?? CognitiveMemoryAccessLevel.Project;
            var redactionState = item?.RedactionState ?? CognitiveMemoryRedactionState.Unclassified;
            var included = PolicyCanRead(accessLevel, request.PolicyContext) &&
                redactionState is CognitiveMemoryRedactionState.Safe or CognitiveMemoryRedactionState.Unclassified;
            sourceRefs.Add(new CognitiveMemoryRecallSourceRef(
                new CognitiveMemoryRecordId(link.MemoryRecordId),
                new CognitiveMemorySourceItemId(link.SourceItemId),
                null,
                item?.SourceSystem ?? string.Empty,
                item?.Locator ?? link.Locator ?? string.Empty,
                BuildSourceRefSummary(link.Summary, item),
                accessLevel,
                redactionState,
                included,
                included ? CognitiveMemoryRecallExclusionReasonKind.None : ResolveSourceRefExclusion(accessLevel, redactionState, request.PolicyContext)));
        }

        foreach (var evidenceLink in evidenceLinks)
        {
            if (!anchorsById.TryGetValue(evidenceLink.EvidenceAnchorId, out var anchor))
            {
                continue;
            }

            var included = anchor.RedactionState is CognitiveMemoryRedactionState.Safe or CognitiveMemoryRedactionState.Unclassified;
            sourceRefs.Add(new CognitiveMemoryRecallSourceRef(
                new CognitiveMemoryRecordId(evidenceLink.MemoryRecordId),
                anchor.SourceItemId is null ? null : new CognitiveMemorySourceItemId(anchor.SourceItemId.Value),
                new CognitiveMemoryEvidenceAnchorId(anchor.Id),
                anchor.SourceSystem,
                anchor.Locator,
                evidenceLink.Summary,
                CognitiveMemoryAccessLevel.Project,
                anchor.RedactionState,
                included,
                included ? CognitiveMemoryRecallExclusionReasonKind.None : CognitiveMemoryRecallExclusionReasonKind.RedactedSource));
        }

        return sourceRefs;
    }

    private IQueryable<CognitiveMemoryRecord> BuildRecordQuery(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request)
    {
        var query = dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record =>
                record.ProjectId == request.ProjectId &&
                record.ValidationState != CognitiveMemoryValidationState.Rejected &&
                record.ValidationState != CognitiveMemoryValidationState.Retired &&
                record.ValidationState != CognitiveMemoryValidationState.Superseded);
        var preferredKinds = NormalizePreferredKinds(request.PreferredRecordKinds);
        if (preferredKinds.Count > 0)
        {
            query = query.Where(record => preferredKinds.Contains(record.Kind));
        }

        if (!request.PolicyContext.AllowRestrictedContent)
        {
            query = query.Where(record => record.AccessLevel <= request.PolicyContext.AccessLevel);
        }

        return query;
    }

    private static IQueryable<CognitiveMemoryRecord> BuildRecordQuery(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<Guid> recordIds)
        => BuildRecordQueryStatic(dbContext, request, recordIds);

    private static IQueryable<CognitiveMemoryRecord> BuildRecordQueryStatic(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<Guid> recordIds)
    {
        var query = dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record =>
                recordIds.Contains(record.Id) &&
                record.ProjectId == request.ProjectId &&
                record.ValidationState != CognitiveMemoryValidationState.Rejected &&
                record.ValidationState != CognitiveMemoryValidationState.Retired &&
                record.ValidationState != CognitiveMemoryValidationState.Superseded);
        if (!request.PolicyContext.AllowRestrictedContent)
        {
            query = query.Where(record => record.AccessLevel <= request.PolicyContext.AccessLevel);
        }

        return query;
    }

    private async Task<IReadOnlyList<MemoryRecordSnapshot>> LoadRecordsByIdAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return [];
        }

        return await BuildRecordQuery(dbContext, request, recordIds)
            .Select(record => new MemoryRecordSnapshot(
                record.Id,
                record.ProjectId,
                record.Kind,
                record.Title,
                record.SummaryText,
                record.CanonicalText,
                record.TopicKey,
                record.ValidationState,
                record.StabilityState,
                record.SourceEvidenceCount,
                record.EvidenceAnchorCount,
                record.PrimaryClaimId,
                record.PrimaryContextFrameId,
                record.AccessLevel,
                record.RiskLevel,
                record.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<MemoryRecordSnapshot>> LoadRecordsByIdAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Set<CognitiveMemoryRecord>()
            .AsNoTracking()
            .Where(record => recordIds.Contains(record.Id))
            .Select(record => new MemoryRecordSnapshot(
                record.Id,
                record.ProjectId,
                record.Kind,
                record.Title,
                record.SummaryText,
                record.CanonicalText,
                record.TopicKey,
                record.ValidationState,
                record.StabilityState,
                record.SourceEvidenceCount,
                record.EvidenceAnchorCount,
                record.PrimaryClaimId,
                record.PrimaryContextFrameId,
                record.AccessLevel,
                record.RiskLevel,
                record.UpdatedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ClaimSnapshot>>> LoadClaimsAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ClaimSnapshot>>();
        }

        var claims = await dbContext.Set<CognitiveMemoryClaimRecord>()
            .AsNoTracking()
            .Where(claim => claim.MemoryRecordId != null && recordIds.Contains(claim.MemoryRecordId.Value))
            .OrderBy(claim => claim.ClaimKind)
            .Take(recordIds.Count * 4)
            .Select(claim => new ClaimSnapshot(
                claim.Id,
                claim.MemoryRecordId!.Value,
                claim.ClaimKind,
                claim.CurrentBeliefState,
                claim.ValidationState,
                claim.PrimaryContextFrameId))
            .ToListAsync(cancellationToken);
        return claims
            .GroupBy(claim => claim.MemoryRecordId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<ClaimSnapshot>)group.ToArray());
    }

    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<Guid>>> LoadEvidenceAnchorIdsAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> recordIds,
        IReadOnlyDictionary<Guid, IReadOnlyList<ClaimSnapshot>> claimsByRecordId,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<Guid>>();
        }

        var recordEvidence = await dbContext.Set<CognitiveMemoryRecordEvidenceAnchorRecord>()
            .AsNoTracking()
            .Where(link => recordIds.Contains(link.MemoryRecordId))
            .Select(link => new
            {
                link.MemoryRecordId,
                link.EvidenceAnchorId
            })
            .ToListAsync(cancellationToken);
        var claimIds = claimsByRecordId.Values.SelectMany(claims => claims.Select(claim => claim.Id)).Distinct().ToArray();
        var claimEvidence = claimIds.Length == 0
            ? []
            : await dbContext.Set<CognitiveMemoryClaimEvidenceLinkRecord>()
                .AsNoTracking()
                .Where(link => claimIds.Contains(link.ClaimId))
                .Select(link => new
                {
                    link.ClaimId,
                    link.EvidenceAnchorId
                })
                .ToListAsync(cancellationToken);
        var recordIdByClaimId = claimsByRecordId.Values
            .SelectMany(claims => claims)
            .ToDictionary(claim => claim.Id, claim => claim.MemoryRecordId);
        var map = new Dictionary<Guid, HashSet<Guid>>();
        foreach (var item in recordEvidence)
        {
            GetEvidenceSet(map, item.MemoryRecordId).Add(item.EvidenceAnchorId);
        }

        foreach (var item in claimEvidence)
        {
            if (recordIdByClaimId.TryGetValue(item.ClaimId, out var memoryRecordId))
            {
                GetEvidenceSet(map, memoryRecordId).Add(item.EvidenceAnchorId);
            }
        }

        return map.ToDictionary(pair => pair.Key, pair => (IReadOnlyList<Guid>)pair.Value.ToArray());
    }

    private static HashSet<Guid> GetEvidenceSet(Dictionary<Guid, HashSet<Guid>> map, Guid recordId)
    {
        if (map.TryGetValue(recordId, out var existing))
        {
            return existing;
        }

        var created = new HashSet<Guid>();
        map[recordId] = created;
        return created;
    }

    private static CognitiveMemoryScoreVectorSnapshot BuildCandidateVector(
        CognitiveMemoryRecallCandidateId candidateId,
        Guid traceId,
        CognitiveMemoryRecallRequest request,
        RecallCandidateAccumulator candidate,
        IReadOnlyList<ClaimSnapshot> claims,
        IReadOnlyList<Guid> evidenceAnchorIds,
        IReadOnlyList<string> queryTerms,
        DateTimeOffset nowUtc)
    {
        var record = candidate.Record;
        var evidenceRefs = BuildScoreEvidenceRefs(candidateId, traceId, candidate, evidenceAnchorIds, nowUtc);
        var components = new List<CognitiveMemoryScoreComponent>
        {
            Component(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, candidate.SemanticSimilarity ?? candidate.LexicalMatch ?? 0.35, candidate.SemanticSimilarity is null ? 0.35 : 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.ContextFit, ResolveContextFit(candidate), 1, evidenceRefs),
            Component(CognitiveMemoryScoreDimensionKind.SourceSufficiency, ResolveSourceSufficiency(record, evidenceAnchorIds), 1, evidenceRefs)
        };

        AddOptional(components, CognitiveMemoryScoreDimensionKind.LexicalMatch, candidate.LexicalMatch, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.GraphProximity, candidate.GraphProximity, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.WorkspaceFocusFit, candidate.WorkspaceFocusFit, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.MemoryActivation, candidate.MemoryActivation, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.ContextSeparation, candidate.ContextSeparation, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.ContradictionPressure, candidate.ContradictionPressure ?? ResolveContradictionPressure(claims), 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.StalenessPressure, ResolveStalenessPressure(record), 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, PolicyCanRead(record.AccessLevel, request.PolicyContext) ? 0 : 1, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.RedactionPressure, record.AccessLevel == CognitiveMemoryAccessLevel.Restricted ? 0.7 : 0, 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.MetadataFit, ResolveMetadataFit(record, request), 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.TemporalRecency, ResolveTemporalRecency(record, nowUtc), 0.5, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.EvidenceSupport, ResolveEvidenceSupport(claims, record), 1, evidenceRefs);
        AddOptional(components, CognitiveMemoryScoreDimensionKind.HumanValidation, ResolveHumanValidation(record, claims), 1, evidenceRefs);

        return new CognitiveMemoryScoreVectorSnapshot(
            CognitiveMemoryScoreSpaceKind.RecallCandidate,
            CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion,
            CognitiveMemoryScoreSpaceRegistry.CurrentNormalizationProfile,
            components,
            CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion,
            nowUtc,
            CognitiveMemoryHash.FromUtf8($"{candidateId}:{record.Id:D}:{request.Mode}:{request.Intent}:{string.Join('|', queryTerms)}"));
    }

    private static IReadOnlyList<CognitiveMemoryScoreEvidenceRef> BuildScoreEvidenceRefs(
        CognitiveMemoryRecallCandidateId candidateId,
        Guid traceId,
        RecallCandidateAccumulator candidate,
        IReadOnlyList<Guid> evidenceAnchorIds,
        DateTimeOffset nowUtc)
    {
        var refs = new List<CognitiveMemoryScoreEvidenceRef>
        {
            new(CognitiveMemoryScoreEvidenceKind.RecallTrace, traceId, 1, nowUtc),
            new(CognitiveMemoryScoreEvidenceKind.MemoryItem, candidate.Record.Id, 1, nowUtc)
        };
        refs.AddRange(evidenceAnchorIds.Select(id => new CognitiveMemoryScoreEvidenceRef(
            CognitiveMemoryScoreEvidenceKind.EvidenceAnchor,
            id,
            1,
            nowUtc)));
        refs.AddRange(candidate.SignalIds.Select(id => new CognitiveMemoryScoreEvidenceRef(
            CognitiveMemoryScoreEvidenceKind.CognitiveSignal,
            id,
            1,
            nowUtc)));
        refs.Add(new CognitiveMemoryScoreEvidenceRef(
            CognitiveMemoryScoreEvidenceKind.RecallTrace,
            candidateId.Value,
            1,
            nowUtc));
        return refs;
    }

    private static IReadOnlyList<CognitiveMemoryScoreShapeSnapshot> BuildRecallCandidateShapes()
    {
        var schema = CognitiveMemoryScoreSpaceRegistry.CurrentSchemaVersion;
        var algorithm = CognitiveMemoryScoreSpaceRegistry.CurrentAlgorithmVersion;
        return
        [
            Shape(CognitiveMemoryScoreProjectionBucket.Inhibit, "Recall candidate is inhibited because semantic similarity conflicts with context separation.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.SemanticSimilarity, 0.7),
                Higher(CognitiveMemoryScoreDimensionKind.ContextSeparation, 0.75)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.Inhibit, "Recall candidate is inhibited because policy or redaction pressure is too high.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.AccessPolicyRisk, 0.75)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.NeedsReview, "Recall candidate has weak source sufficiency and should not be treated as authoritative.",
            [
                Lower(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.35)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.StrongAccept, "Recall candidate has source-backed context fit.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.ContextFit, 0.65),
                Higher(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.55)
            ]),
            Shape(CognitiveMemoryScoreProjectionBucket.WeakAccept, "Recall candidate is usable as side context with enough source support.",
            [
                Higher(CognitiveMemoryScoreDimensionKind.ContextFit, 0.45),
                Higher(CognitiveMemoryScoreDimensionKind.SourceSufficiency, 0.35)
            ])
        ];

        CognitiveMemoryScoreShapeSnapshot Shape(
            CognitiveMemoryScoreProjectionBucket bucket,
            string explanation,
            IReadOnlyList<CognitiveMemoryScoreShapeComponent> components)
            => new(
                CognitiveMemoryScoreShapeKind.ThresholdEnvelope,
                CognitiveMemoryScoreSpaceKind.RecallCandidate,
                schema,
                components,
                radius: null,
                bucket,
                explanation,
                [],
                algorithm);
    }

    private static CognitiveMemoryScoreShapeComponent Higher(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double lowerBound)
        => new(dimensionKind, center: lowerBound, lowerBound, upperBound: null, weight: 1);

    private static CognitiveMemoryScoreShapeComponent Lower(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double upperBound)
        => new(dimensionKind, center: upperBound, lowerBound: null, upperBound, weight: 1);

    private static CognitiveMemoryScoreComponent Component(
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double value,
        double confidence,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef> evidenceRefs)
        => new(dimensionKind, Math.Clamp(value, 0, 1), Math.Clamp(confidence, 0, 1), evidenceRefs);

    private static void AddOptional(
        List<CognitiveMemoryScoreComponent> components,
        CognitiveMemoryScoreDimensionKind dimensionKind,
        double? value,
        double confidence,
        IReadOnlyList<CognitiveMemoryScoreEvidenceRef> evidenceRefs)
    {
        if (value is null)
        {
            return;
        }

        if (components.Any(component => component.DimensionKind == dimensionKind))
        {
            return;
        }

        components.Add(Component(dimensionKind, value.Value, confidence, evidenceRefs));
    }

    private static CandidateDecision DecideCandidate(
        RecallCandidateAccumulator candidate,
        CognitiveMemoryScoreEvaluationTrace trace,
        CognitiveMemoryRecallRequest request)
    {
        if (!PolicyCanRead(candidate.Record.AccessLevel, request.PolicyContext))
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.Inhibited,
                CognitiveMemoryRecallExclusionReasonKind.AccessPolicy,
                "Candidate inhibited by recall access policy.");
        }

        if (trace.MissingRequiredDimensions.Count > 0)
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.Excluded,
                CognitiveMemoryRecallExclusionReasonKind.ScoreGeometryRejected,
                $"Candidate excluded because score geometry is missing required dimensions: {string.Join(", ", trace.MissingRequiredDimensions.Select(dimension => dimension.DimensionKind))}.");
        }

        if (candidate.ContextSeparation is >= 0.75 && request.Mode != CognitiveMemoryRecallMode.CrossProjectAnalogy)
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.Inhibited,
                CognitiveMemoryRecallExclusionReasonKind.ContextBoundary,
                string.IsNullOrWhiteSpace(candidate.ContextBoundaryReason)
                    ? "Candidate is related but context separated from the active recall goal."
                    : candidate.ContextBoundaryReason);
        }

        if (trace.ScalarProjection?.Bucket is CognitiveMemoryScoreProjectionBucket.Inhibit or CognitiveMemoryScoreProjectionBucket.Reject or CognitiveMemoryScoreProjectionBucket.Abstain)
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.Inhibited,
                CognitiveMemoryRecallExclusionReasonKind.ScoreGeometryRejected,
                trace.DecisionExplanation);
        }

        if (trace.ScalarProjection?.Bucket == CognitiveMemoryScoreProjectionBucket.NeedsReview)
        {
            return new CandidateDecision(
                CognitiveMemoryRecallCandidateDecisionKind.SideContext,
                CognitiveMemoryRecallExclusionReasonKind.SourceInsufficient,
                "Candidate retained as side context because score geometry marked it review-worthy.");
        }

        return new CandidateDecision(
            CognitiveMemoryRecallCandidateDecisionKind.Selected,
            CognitiveMemoryRecallExclusionReasonKind.None,
            string.Join(" ", candidate.Reasons.Distinct(StringComparer.Ordinal)));
    }

    private static void AddStageRecords(
        AppDbContext dbContext,
        Guid traceId,
        Guid projectId,
        IReadOnlyList<CognitiveMemoryRecallTraceStage> stages,
        DateTimeOffset nowUtc)
    {
        foreach (var stage in stages)
        {
            dbContext.Add(new CognitiveMemoryRecallTraceStageRecord
            {
                RecallTraceId = traceId,
                ProjectId = projectId,
                StageKind = stage.StageKind,
                ChannelKind = stage.ChannelKind,
                Status = stage.Status,
                CandidateCount = stage.CandidateCount,
                SelectedCount = stage.SelectedCount,
                ExcludedCount = stage.ExcludedCount,
                LimitingBudget = stage.LimitingBudget,
                ProviderTrace = stage.ProviderTrace,
                FailureCode = stage.FailureCode,
                FailureMessage = stage.FailureMessage,
                StartedAtUtc = nowUtc,
                CompletedAtUtc = stage.CompletedAtUtc
            });
        }
    }

    private static void AddCandidateRecords(
        AppDbContext dbContext,
        Guid traceId,
        CognitiveMemoryWorkspaceFrameId? workspaceFrameId,
        IReadOnlyList<EvaluatedRecallCandidate> candidates,
        CognitiveMemoryRecallContextPack contextPack,
        DateTimeOffset nowUtc)
    {
        foreach (var candidate in candidates)
        {
            var refs = contextPack.SourceRefs
                .Where(sourceRef => sourceRef.MemoryRecordId.Value == candidate.Record.Id)
                .ToArray();
            dbContext.Add(new CognitiveMemoryRecallCandidateRecord
            {
                Id = candidate.Id.Value,
                RecallTraceId = traceId,
                ProjectId = candidate.Record.ProjectId,
                PrimaryChannelKind = candidate.PrimaryChannelKind,
                DecisionKind = candidate.DecisionKind,
                ExclusionReasonKind = candidate.ExclusionReasonKind,
                MemoryRecordId = candidate.Record.Id,
                MemoryKind = candidate.Record.Kind,
                ClaimId = candidate.SelectedClaimIds.FirstOrDefault().Value == Guid.Empty ? null : candidate.SelectedClaimIds.First().Value,
                SourceItemId = refs.FirstOrDefault(sourceRef => sourceRef.SourceItemId is not null)?.SourceItemId?.Value,
                EvidenceAnchorId = refs.FirstOrDefault(sourceRef => sourceRef.EvidenceAnchorId is not null)?.EvidenceAnchorId?.Value,
                WorkspaceFrameId = workspaceFrameId?.Value,
                ContextFrameId = candidate.Record.PrimaryContextFrameId,
                ScoreEvaluationTraceId = candidate.ScoreTrace.Id.Value,
                ScoreBucket = candidate.ScoreTrace.ScalarProjection?.Bucket ?? CognitiveMemoryScoreProjectionBucket.Unknown,
                DisplayRankProjection = candidate.DisplayRankProjection?.DisplayScore,
                HasSourceDetail = refs.Any(sourceRef => sourceRef.IncludedInContext),
                SourceRedacted = refs.Any(sourceRef => sourceRef.RedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted),
                EstimatedTokenCount = EstimateTokenCount(candidate.Record.SummaryText, candidate.Record.CanonicalText),
                SourceRefCount = refs.Length,
                Title = candidate.Record.Title,
                Summary = candidate.Record.SummaryText,
                Reason = candidate.Reason,
                ChannelTraceJson = JsonSerializer.Serialize(candidate.ChannelKinds.Select(kind => kind.ToString()).ToArray(), CognitiveMemoryJson.SerializerOptions),
                CreatedAtUtc = nowUtc
            });
        }
    }

    private static void AddContextPackRecords(
        AppDbContext dbContext,
        Guid traceId,
        CognitiveMemoryRecallContextPack contextPack,
        int characterBudget,
        DateTimeOffset nowUtc)
    {
        dbContext.Add(new CognitiveMemoryRecallContextPackRecord
        {
            Id = contextPack.Id.Value,
            RecallTraceId = traceId,
            ProjectId = contextPack.ProjectId,
            WorkspaceFrameId = contextPack.WorkspaceFrameId?.Value,
            Title = contextPack.Title,
            Summary = contextPack.Summary,
            CharacterBudget = characterBudget,
            RenderedCharacterCount = contextPack.Sections.Sum(section => section.Content.Length),
            SectionCount = contextPack.Sections.Count,
            SourceRefCount = contextPack.SourceRefs.Count,
            WarningCount = contextPack.Warnings.Count,
            MetadataJson = SerializeMetadata(contextPack.Metadata),
            CreatedAtUtc = nowUtc
        });

        for (var index = 0; index < contextPack.Sections.Count; index++)
        {
            var section = contextPack.Sections[index];
            dbContext.Add(new CognitiveMemoryRecallContextSectionRecord
            {
                ContextPackId = contextPack.Id.Value,
                RecallTraceId = traceId,
                ProjectId = contextPack.ProjectId,
                SectionKind = section.SectionKind,
                Sequence = index,
                SectionKey = section.SectionId.Value,
                Title = section.Title,
                Content = section.Content,
                MemoryRecordId = section.MemoryRecordIds.FirstOrDefault().Value == Guid.Empty ? null : section.MemoryRecordIds.First().Value,
                ClaimId = section.ClaimIds.FirstOrDefault().Value == Guid.Empty ? null : section.ClaimIds.First().Value,
                SourceItemId = section.SourceRefs.FirstOrDefault(sourceRef => sourceRef.SourceItemId is not null)?.SourceItemId?.Value,
                AccessLevel = section.SourceRefs.FirstOrDefault()?.AccessLevel ?? CognitiveMemoryAccessLevel.Project,
                RedactionState = section.SourceRefs.FirstOrDefault()?.RedactionState ?? CognitiveMemoryRedactionState.Safe,
                EstimatedTokenCount = EstimateTokenCount(section.Content),
                CreatedAtUtc = nowUtc
            });
        }

        foreach (var sourceRef in contextPack.SourceRefs)
        {
            dbContext.Add(new CognitiveMemoryRecallSourceRefRecord
            {
                RecallTraceId = traceId,
                ContextPackId = contextPack.Id.Value,
                ProjectId = contextPack.ProjectId,
                MemoryRecordId = sourceRef.MemoryRecordId.Value,
                SourceItemId = sourceRef.SourceItemId?.Value,
                EvidenceAnchorId = sourceRef.EvidenceAnchorId?.Value,
                SourceSystem = sourceRef.SourceSystem,
                Locator = sourceRef.Locator,
                QuoteHash = string.Empty,
                Summary = sourceRef.Summary,
                AccessLevel = sourceRef.AccessLevel,
                RedactionState = sourceRef.RedactionState,
                IncludedInContext = sourceRef.IncludedInContext,
                ExclusionReasonKind = sourceRef.ExclusionReasonKind,
                CreatedAtUtc = nowUtc
            });
        }
    }

    private static CognitiveMemoryRecallCandidate ToContract(EvaluatedRecallCandidate candidate)
        => new(
            candidate.Id,
            new CognitiveMemoryRecordId(candidate.Record.Id),
            candidate.Record.Kind,
            candidate.Record.Title,
            candidate.PrimaryChannelKind,
            candidate.DecisionKind,
            candidate.ExclusionReasonKind,
            candidate.ScoreTrace,
            candidate.DisplayRankProjection,
            candidate.SelectedClaimIds,
            candidate.EvidenceAnchorIds,
            candidate.Reason);

    private static RecallCandidateAccumulator GetCandidate(
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        MemoryRecordSnapshot record)
    {
        if (candidates.TryGetValue(record.Id, out var candidate))
        {
            return candidate;
        }

        candidate = new RecallCandidateAccumulator(record);
        candidates[record.Id] = candidate;
        return candidate;
    }

    private static CognitiveMemoryRecallTraceStage Stage(
        CognitiveMemoryRecallTraceStageKind stageKind,
        CognitiveMemoryRecallChannelKind channelKind,
        CognitiveMemoryRecallStageStatus status,
        int candidateCount,
        int selectedCount,
        int excludedCount,
        string providerTrace,
        CognitiveMemoryBudgetLimit? limitingBudget = null,
        string failureCode = "",
        string failureMessage = "",
        DateTimeOffset? completedAtUtc = null)
        => new(
            stageKind,
            channelKind,
            status,
            candidateCount,
            selectedCount,
            excludedCount,
            limitingBudget,
            providerTrace,
            failureCode,
            failureMessage,
            completedAtUtc ?? DateTimeOffset.UnixEpoch);

    private static CognitiveMemoryBudgetLimit? ResolveLimitingBudget(
        IReadOnlyList<CognitiveMemoryRecallTraceStage> stages,
        IReadOnlyList<EvaluatedRecallCandidate> candidates,
        IReadOnlyList<string> warnings)
        => stages.FirstOrDefault(stage => stage.LimitingBudget is not null)?.LimitingBudget ??
            (candidates.Any(candidate => candidate.ExclusionReasonKind == CognitiveMemoryRecallExclusionReasonKind.BudgetLimit)
                ? CognitiveMemoryBudgetLimit.ItemCount
                : warnings.Any(warning => warning.Contains("budget", StringComparison.OrdinalIgnoreCase))
                    ? CognitiveMemoryBudgetLimit.ByteCount
                    : null);

    private static CognitiveMemoryRecallExclusionReasonKind ResolveSourceRefExclusion(
        CognitiveMemoryAccessLevel accessLevel,
        CognitiveMemoryRedactionState redactionState,
        CognitiveMemoryPolicyContext policyContext)
    {
        if (!PolicyCanRead(accessLevel, policyContext))
        {
            return CognitiveMemoryRecallExclusionReasonKind.AccessPolicy;
        }

        return redactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted
            ? CognitiveMemoryRecallExclusionReasonKind.RedactedSource
            : CognitiveMemoryRecallExclusionReasonKind.None;
    }

    private static string BuildSourceRefSummary(
        string sourceLinkSummary,
        SourceItemSnapshot? sourceItem)
    {
        if (sourceItem is not null && !string.IsNullOrWhiteSpace(sourceItem.ContentText))
        {
            var content = sourceItem.ContentText.Trim();
            return content.Length <= 2000 ? content : content[..2000];
        }

        if (!string.IsNullOrWhiteSpace(sourceLinkSummary))
        {
            return sourceLinkSummary.Trim();
        }

        return sourceItem?.Title.Trim() ?? string.Empty;
    }

    private static IReadOnlyList<string> NormalizeTerms(string query)
        => query
            .Split([' ', '\t', '\r', '\n', '.', ',', ';', ':', '?', '!', '/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 2)
            .Select(term => term.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .Take(12)
            .ToArray();

    private static IReadOnlyList<CognitiveMemoryRecordKind> NormalizePreferredKinds(IReadOnlyList<CognitiveMemoryRecordKind>? preferredKinds)
        => preferredKinds?
            .Distinct()
            .ToArray() ?? [];

    private static double ComputeLexicalMatch(
        MemoryRecordSnapshot record,
        IReadOnlyList<string> queryTerms)
    {
        if (queryTerms.Count == 0)
        {
            return 0;
        }

        var haystack = $"{record.Title} {record.SummaryText} {record.CanonicalText} {record.TopicKey}".ToLowerInvariant();
        var hits = queryTerms.Count(term => haystack.Contains(term, StringComparison.Ordinal));
        return Math.Clamp((double)hits / queryTerms.Count, 0, 1);
    }

    private static double ResolveContextFit(RecallCandidateAccumulator candidate)
        => candidate.ContextSeparation is >= 0.75 ? 0.2 : 0.85;

    private static double ResolveSourceSufficiency(
        MemoryRecordSnapshot record,
        IReadOnlyList<Guid> evidenceAnchorIds)
    {
        if (record.SourceEvidenceCount > 0 && (record.EvidenceAnchorCount > 0 || evidenceAnchorIds.Count > 0))
        {
            return 0.9;
        }

        if (record.SourceEvidenceCount > 0 || evidenceAnchorIds.Count > 0)
        {
            return 0.65;
        }

        return 0.2;
    }

    private static double? ResolveContradictionPressure(IReadOnlyList<ClaimSnapshot> claims)
    {
        if (claims.Any(claim => claim.CurrentBeliefState is CognitiveMemoryBeliefStateKind.Contradicted or CognitiveMemoryBeliefStateKind.Contested))
        {
            return 0.85;
        }

        return null;
    }

    private static double ResolveStalenessPressure(MemoryRecordSnapshot record)
        => record.StabilityState switch
        {
            CognitiveMemoryStabilityState.Deprecated => 1,
            CognitiveMemoryStabilityState.Stale => 0.85,
            CognitiveMemoryStabilityState.Dormant => 0.45,
            _ => 0
        };

    private static double ResolveMetadataFit(
        MemoryRecordSnapshot record,
        CognitiveMemoryRecallRequest request)
    {
        var preferredKinds = NormalizePreferredKinds(request.PreferredRecordKinds);
        if (preferredKinds.Count == 0)
        {
            return request.Intent switch
            {
                CognitiveMemoryRecallIntentKind.Procedure => record.Kind == CognitiveMemoryRecordKind.Procedural ? 0.95 : 0.45,
                CognitiveMemoryRecallIntentKind.DecisionHistory => record.Kind == CognitiveMemoryRecordKind.Decision ? 0.95 : 0.45,
                _ => 0.65
            };
        }

        return preferredKinds.Contains(record.Kind) ? 0.95 : 0.25;
    }

    private static double ResolveTemporalRecency(
        MemoryRecordSnapshot record,
        DateTimeOffset nowUtc)
    {
        var age = nowUtc - record.UpdatedAtUtc;
        if (age <= TimeSpan.FromDays(30))
        {
            return 0.85;
        }

        if (age <= TimeSpan.FromDays(180))
        {
            return 0.55;
        }

        return 0.25;
    }

    private static double ResolveEvidenceSupport(
        IReadOnlyList<ClaimSnapshot> claims,
        MemoryRecordSnapshot record)
    {
        if (claims.Any(claim => claim.CurrentBeliefState is CognitiveMemoryBeliefStateKind.Supported or CognitiveMemoryBeliefStateKind.Validated))
        {
            return 0.9;
        }

        if (record.SourceEvidenceCount > 0)
        {
            return 0.65;
        }

        return 0.25;
    }

    private static double ResolveHumanValidation(
        MemoryRecordSnapshot record,
        IReadOnlyList<ClaimSnapshot> claims)
    {
        if (record.ValidationState == CognitiveMemoryValidationState.Approved ||
            record.ValidationState == CognitiveMemoryValidationState.HumanReviewed ||
            claims.Any(claim => claim.ValidationState is CognitiveMemoryValidationState.Approved or CognitiveMemoryValidationState.HumanReviewed))
        {
            return 1;
        }

        return 0.25;
    }

    private static CognitiveMemoryAccessLevel GetProjectionMaximumAccessLevel(CognitiveMemoryPolicyContext policyContext)
        => policyContext.AllowRestrictedContent
            ? CognitiveMemoryAccessLevel.Restricted
            : policyContext.AccessLevel;

    private static bool PolicyCanRead(
        CognitiveMemoryAccessLevel accessLevel,
        CognitiveMemoryPolicyContext policyContext)
        => accessLevel <= policyContext.AccessLevel ||
            accessLevel == CognitiveMemoryAccessLevel.Restricted && policyContext.AllowRestrictedContent;

    private static int EstimateTokenCount(string? value)
    {
        return Math.Max(1, (value?.Length ?? 0) / 4);
    }

    private static int EstimateTokenCount(string? first, string? second)
    {
        var characters = (first?.Length ?? 0) + (second?.Length ?? 0);
        return Math.Max(1, characters / 4);
    }

    private static string BuildPackSummary(
        IReadOnlyList<EvaluatedRecallCandidate> selected,
        IReadOnlyList<EvaluatedRecallCandidate> allCandidates)
    {
        if (selected.Count == 0)
        {
            return "No recall candidates were selected. Review warnings and unavailable channel traces before answering.";
        }

        var inhibitedCount = allCandidates.Count(candidate => candidate.DecisionKind == CognitiveMemoryRecallCandidateDecisionKind.Inhibited);
        return inhibitedCount == 0
            ? $"Selected {selected.Count} source-backed memory candidate(s)."
            : $"Selected {selected.Count} memory candidate(s) and inhibited {inhibitedCount} context-separated or unsafe candidate(s).";
    }

    private static string SerializeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return "{}";
        }

        var dictionary = metadata as Dictionary<string, string> ??
            metadata.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
        return JsonSerializer.Serialize(dictionary, CognitiveMemoryJson.SerializerOptions);
    }

    private static Guid? NormalizeOptional(Guid? value)
        => value is { } actual && actual != Guid.Empty ? actual : null;

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata = new Dictionary<string, string>(0, StringComparer.Ordinal);

    private static readonly IReadOnlyList<CognitiveMemoryValidationState> RecallReadableValidationStates =
    [
        CognitiveMemoryValidationState.MachineGenerated,
        CognitiveMemoryValidationState.NeedsHumanReview,
        CognitiveMemoryValidationState.HumanReviewed,
        CognitiveMemoryValidationState.Approved
    ];

    private sealed class RecallCandidateAccumulator(MemoryRecordSnapshot record)
    {
        public MemoryRecordSnapshot Record { get; } = record;

        public HashSet<CognitiveMemoryRecallChannelKind> Channels { get; } = [];

        public List<string> Reasons { get; } = [];

        public List<Guid> SignalIds { get; } = [];

        public double? SemanticSimilarity { get; set; }

        public double? LexicalMatch { get; set; }

        public double? GraphProximity { get; set; }

        public double? WorkspaceFocusFit { get; set; }

        public double? MemoryActivation { get; set; }

        public double? ContextSeparation { get; set; }

        public double? ContradictionPressure { get; set; }

        public string ProjectionPayloadHash { get; set; } = string.Empty;

        public string ContextBoundaryReason { get; set; } = string.Empty;

        public CognitiveMemoryRecallChannelKind PrimaryChannelKind
            => Channels.Contains(CognitiveMemoryRecallChannelKind.Workspace)
                ? CognitiveMemoryRecallChannelKind.Workspace
                : Channels.Contains(CognitiveMemoryRecallChannelKind.VectorProjection)
                    ? CognitiveMemoryRecallChannelKind.VectorProjection
                    : Channels.Contains(CognitiveMemoryRecallChannelKind.Lexical)
                        ? CognitiveMemoryRecallChannelKind.Lexical
                        : Channels.Contains(CognitiveMemoryRecallChannelKind.Graph)
                            ? CognitiveMemoryRecallChannelKind.Graph
                            : CognitiveMemoryRecallChannelKind.Unknown;
    }

    private sealed record MemoryRecordSnapshot(
        Guid Id,
        Guid? ProjectId,
        CognitiveMemoryRecordKind Kind,
        string Title,
        string SummaryText,
        string CanonicalText,
        string TopicKey,
        CognitiveMemoryValidationState ValidationState,
        CognitiveMemoryStabilityState StabilityState,
        int SourceEvidenceCount,
        int EvidenceAnchorCount,
        Guid? PrimaryClaimId,
        Guid? PrimaryContextFrameId,
        CognitiveMemoryAccessLevel AccessLevel,
        CognitiveMemoryRiskLevel RiskLevel,
        DateTimeOffset UpdatedAtUtc);

    private sealed record RelationSnapshot(
        Guid SourceMemoryRecordId,
        Guid TargetMemoryRecordId,
        CognitiveMemoryRelationKind RelationKind,
        double? DisplayStrengthProjection,
        string Reason);

    private sealed record ClaimSnapshot(
        Guid Id,
        Guid MemoryRecordId,
        CognitiveMemoryClaimKind ClaimKind,
        CognitiveMemoryBeliefStateKind CurrentBeliefState,
        CognitiveMemoryValidationState ValidationState,
        Guid? PrimaryContextFrameId);

    private sealed record CandidateDecision(
        CognitiveMemoryRecallCandidateDecisionKind DecisionKind,
        CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind,
        string Reason);

    private sealed record EvaluatedRecallCandidate(
        CognitiveMemoryRecallCandidateId Id,
        MemoryRecordSnapshot Record,
        CognitiveMemoryWorkspaceFrameId? WorkspaceFrameId,
        CognitiveMemoryRecallChannelKind PrimaryChannelKind,
        CognitiveMemoryRecallCandidateDecisionKind DecisionKind,
        CognitiveMemoryRecallExclusionReasonKind ExclusionReasonKind,
        CognitiveMemoryScoreEvaluationTrace ScoreTrace,
        CognitiveMemoryScoreScalarProjection? DisplayRankProjection,
        IReadOnlyList<CognitiveMemoryClaimId> SelectedClaimIds,
        IReadOnlyList<CognitiveMemoryEvidenceAnchorId> EvidenceAnchorIds,
        string Reason,
        IReadOnlyList<CognitiveMemoryRecallChannelKind> ChannelKinds,
        string ContextBoundaryReason);

    private sealed record SourceLinkSnapshot(
        Guid MemoryRecordId,
        Guid SourceItemId,
        string? Locator,
        string? QuoteHash,
        string Summary);

    private sealed record SourceItemSnapshot(
        Guid Id,
        Guid? ProjectId,
        string SourceSystem,
        string SourceItemKey,
        string Title,
        string ContentText,
        string? Locator,
        CognitiveMemoryRedactionState RedactionState,
        CognitiveMemoryAccessLevel AccessLevel);

    private sealed record EvidenceAnchorSnapshot(
        Guid Id,
        Guid? SourceItemId,
        string SourceSystem,
        string Locator,
        string QuoteHash,
        CognitiveMemoryRedactionState RedactionState);
}
