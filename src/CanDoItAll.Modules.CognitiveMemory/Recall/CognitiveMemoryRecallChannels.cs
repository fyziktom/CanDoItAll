using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.CognitiveMemory;


public sealed partial class CognitiveMemoryRecallOrchestrator
{
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

        var candidateRecords = new Dictionary<Guid, MemoryRecordSnapshot>();
        var sourceLexicalScores = new Dictionary<Guid, double>();
        var termScanLimit = Math.Max(request.Budget.CoarseCandidateLimit, 32);
        foreach (var term in queryTerms)
        {
            var pattern = $"%{term}%";
            var termQuery = BuildRecordQuery(dbContext, request)
                .Where(record =>
                    EF.Functions.Like(record.Title.ToLower(), pattern) ||
                    EF.Functions.Like(record.SummaryText.ToLower(), pattern) ||
                    EF.Functions.Like(record.CanonicalText.ToLower(), pattern) ||
                    EF.Functions.Like(record.TopicKey.ToLower(), pattern));
            var termRecords = dbContext.Database.IsSqlite()
                ? await termQuery
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
                    .ToListAsync(cancellationToken)
                : await termQuery
                    .OrderByDescending(record => record.UpdatedAtUtc)
                    .Take(termScanLimit)
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
            termRecords = termRecords
                .OrderByDescending(record => record.UpdatedAtUtc)
                .Take(termScanLimit)
                .ToList();

            foreach (var record in termRecords)
            {
                candidateRecords.TryAdd(record.Id, record);
            }
        }

        var sourceTextMatches = await LoadSourceTextLexicalMatchesAsync(
            dbContext,
            request,
            queryTerms,
            termScanLimit,
            cancellationToken);
        foreach (var match in sourceTextMatches)
        {
            candidateRecords.TryAdd(match.Record.Id, match.Record);
            sourceLexicalScores[match.Record.Id] = Math.Max(
                sourceLexicalScores.GetValueOrDefault(match.Record.Id),
                match.Score);
        }

        var fallbackCount = 0;
        if (candidateRecords.Count < request.Budget.CoarseCandidateLimit)
        {
            var existingRecordIds = candidateRecords.Keys.ToHashSet();
            var fallbackQuery = BuildRecordQuery(dbContext, request)
                .Where(record => !existingRecordIds.Contains(record.Id));
            var fallbackRecords = dbContext.Database.IsSqlite()
                ? await fallbackQuery
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
                    .ToListAsync(cancellationToken)
                : await fallbackQuery
                    .OrderByDescending(record => record.UpdatedAtUtc)
                    .Take(LexicalFallbackScanLimit)
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
            fallbackRecords = fallbackRecords
                .OrderByDescending(record => record.UpdatedAtUtc)
                .Take(LexicalFallbackScanLimit)
                .ToList();
            var fallbackMatches = fallbackRecords
                .Select(record => new
                {
                    Record = record,
                    Score = ResolveLexicalMatch(record, queryTerms, sourceLexicalScores)
                })
                .Where(match => match.Score > 0)
                .OrderByDescending(match => match.Score)
                .ThenByDescending(match => match.Record.UpdatedAtUtc)
                .Take(request.Budget.CoarseCandidateLimit - candidateRecords.Count)
                .Select(match => match.Record)
                .ToList();
            fallbackCount = fallbackMatches.Count;
            foreach (var record in fallbackMatches)
            {
                candidateRecords.TryAdd(record.Id, record);
            }
        }

        var records = candidateRecords.Values
            .Select(record => new
            {
                Record = record,
                Score = ResolveLexicalMatch(record, queryTerms, sourceLexicalScores)
            })
            .Where(match => match.Score > 0)
            .OrderByDescending(match => match.Score)
            .ThenByDescending(match => match.Record.UpdatedAtUtc)
            .Take(request.Budget.CoarseCandidateLimit)
            .ToList();

        foreach (var record in records)
        {
            var candidate = GetCandidate(candidates, record.Record);
            candidate.Channels.Add(CognitiveMemoryRecallChannelKind.Lexical);
            candidate.LexicalMatch = Math.Max(candidate.LexicalMatch ?? 0, record.Score);
            candidate.Reasons.Add("Lexical channel matched durable memory text.");
        }

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.CoarseCandidateActivation,
            CognitiveMemoryRecallChannelKind.Lexical,
            CognitiveMemoryRecallStageStatus.Completed,
            records.Count,
            records.Count,
            0,
            fallbackCount == 0
                ? $"lexical:terms:{queryTerms.Count}:records:{records.Count}"
                : $"lexical:terms:{queryTerms.Count}:records:{records.Count}:fallback:{fallbackCount}",
            limitingBudget: candidateRecords.Count >= request.Budget.CoarseCandidateLimit ? CognitiveMemoryBudgetLimit.ItemCount : null,
            completedAtUtc: nowUtc));
    }

    private async Task<IReadOnlyList<SourceTextLexicalMatch>> LoadSourceTextLexicalMatchesAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<string> queryTerms,
        int termScanLimit,
        CancellationToken cancellationToken)
    {
        var sourceItemsById = new Dictionary<Guid, SourceTextItemSnapshot>();
        foreach (var term in queryTerms)
        {
            var pattern = $"%{term}%";
            var sourceQuery = dbContext.Set<CognitiveMemorySourceItemRecord>()
                .AsNoTracking()
                .Where(item =>
                    item.ProjectId == request.ProjectId &&
                    item.RedactionState != CognitiveMemoryRedactionState.Redacted &&
                    (request.PolicyContext.AllowRestrictedContent || item.AccessLevel <= request.PolicyContext.AccessLevel) &&
                    (EF.Functions.Like(item.Title.ToLower(), pattern) ||
                     EF.Functions.Like(item.ContentText.ToLower(), pattern) ||
                     EF.Functions.Like(item.SourceItemKey.ToLower(), pattern) ||
                     item.Locator != null && EF.Functions.Like(item.Locator.ToLower(), pattern)));
            var matches = dbContext.Database.IsSqlite()
                ? await sourceQuery
                    .Select(item => new SourceTextItemSnapshot(
                        item.Id,
                        item.Title,
                        item.ContentText,
                        item.SourceItemKey,
                        item.Locator,
                        item.UpdatedAtUtc))
                    .ToListAsync(cancellationToken)
                : await sourceQuery
                    .OrderByDescending(item => item.UpdatedAtUtc)
                    .Take(termScanLimit)
                    .Select(item => new SourceTextItemSnapshot(
                    item.Id,
                    item.Title,
                    item.ContentText,
                    item.SourceItemKey,
                    item.Locator,
                    item.UpdatedAtUtc))
                    .ToListAsync(cancellationToken);
            matches = matches
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Take(termScanLimit)
                .ToList();

            foreach (var match in matches)
            {
                sourceItemsById.TryAdd(match.Id, match);
            }
        }

        if (sourceItemsById.Count == 0)
        {
            return [];
        }

        var sourceItemIds = sourceItemsById.Keys.ToArray();
        var sourceLinks = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => sourceItemIds.Contains(link.SourceItemId))
            .Select(link => new
            {
                link.MemoryRecordId,
                link.SourceItemId,
                link.Summary
            })
            .ToListAsync(cancellationToken);
        var recordIds = sourceLinks.Select(link => link.MemoryRecordId).Distinct().ToArray();
        var records = await LoadRecordsByIdAsync(dbContext, request, recordIds, cancellationToken);
        var recordsById = records.ToDictionary(record => record.Id);
        var scoresByRecordId = new Dictionary<Guid, double>();

        foreach (var link in sourceLinks)
        {
            if (!recordsById.ContainsKey(link.MemoryRecordId) ||
                !sourceItemsById.TryGetValue(link.SourceItemId, out var sourceItem))
            {
                continue;
            }

            var score = ComputeLexicalMatch(
                $"{sourceItem.Title} {sourceItem.ContentText} {sourceItem.SourceItemKey} {sourceItem.Locator} {link.Summary}",
                queryTerms);
            if (score <= 0)
            {
                continue;
            }

            scoresByRecordId[link.MemoryRecordId] = Math.Max(
                scoresByRecordId.GetValueOrDefault(link.MemoryRecordId),
                score);
        }

        return scoresByRecordId
            .Select(pair => new SourceTextLexicalMatch(recordsById[pair.Key], pair.Value))
            .ToList();
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

        var sourceGraphExpansion = await AddSourceGraphExpansionCandidatesAsync(
            dbContext,
            request,
            candidates,
            cancellationToken);

        stages.Add(Stage(
            CognitiveMemoryRecallTraceStageKind.AssociationExpansion,
            CognitiveMemoryRecallChannelKind.Graph,
            CognitiveMemoryRecallStageStatus.Completed,
            relations.Count + sourceGraphExpansion.EdgeCount,
            records.Count + sourceGraphExpansion.RecordCount,
            0,
            $"graph:relations:{relations.Count}:source-edges:{sourceGraphExpansion.EdgeCount}:source-records:{sourceGraphExpansion.RecordCount}",
            limitingBudget: relations.Count >= relationLimit || sourceGraphExpansion.Limited ? CognitiveMemoryBudgetLimit.ItemCount : null,
            completedAtUtc: nowUtc));
    }

    private async Task<SourceGraphExpansionResult> AddSourceGraphExpansionCandidatesAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        Dictionary<Guid, RecallCandidateAccumulator> candidates,
        CancellationToken cancellationToken)
    {
        var sourceExpansionSeedRecordIds = candidates.Values
            .Where(IsSourceGraphExpansionSeed)
            .Select(candidate => candidate.Record.Id)
            .Distinct()
            .ToArray();
        var frontierItems = (await LoadSourceGraphItemsForRecordsAsync(
                dbContext,
                sourceExpansionSeedRecordIds,
                cancellationToken))
            .Where(CanUseAsSourceGraphFrontier)
            .GroupBy(item => item.SourceItemKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToList();
        if (frontierItems.Count == 0)
        {
            return new SourceGraphExpansionResult(0, 0, Limited: false);
        }

        var visitedSourceItemKeys = frontierItems
            .Select(item => item.SourceItemKey)
            .ToHashSet(StringComparer.Ordinal);
        var edgeCount = 0;
        var recordCount = 0;
        var limited = false;
        var expansionLimit = Math.Max(request.Budget.CoarseCandidateLimit * Math.Max(1, request.Budget.GraphExpansionDepth), 1);

        for (var depth = 1; depth <= request.Budget.GraphExpansionDepth; depth++)
        {
            var nextItems = await LoadNeighborSourceGraphItemsAsync(
                dbContext,
                request,
                frontierItems,
                expansionLimit,
                cancellationToken);
            var unseenItems = nextItems
                .Where(item => visitedSourceItemKeys.Add(item.SourceItemKey))
                .Take(expansionLimit)
                .ToList();
            if (unseenItems.Count == 0)
            {
                break;
            }

            edgeCount += unseenItems.Count;
            limited |= nextItems.Count >= expansionLimit;
            var linkedRecordIds = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
                .AsNoTracking()
                .Where(link => unseenItems.Select(item => item.Id).Contains(link.SourceItemId))
                .Select(link => link.MemoryRecordId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var records = await LoadRecordsByIdAsync(dbContext, request, linkedRecordIds, cancellationToken);
            foreach (var record in records)
            {
                var candidate = GetCandidate(candidates, record);
                candidate.Channels.Add(CognitiveMemoryRecallChannelKind.Graph);
                candidate.GraphProximity = Math.Max(candidate.GraphProximity ?? 0, ResolveSourceGraphProximity(depth));
                candidate.Reasons.Add("Graph expansion followed source item structure.");
            }

            recordCount += records.Count;
            frontierItems = unseenItems;
        }

        return new SourceGraphExpansionResult(edgeCount, recordCount, limited);
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadSourceGraphItemsForRecordsAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> recordIds,
        CancellationToken cancellationToken)
    {
        if (recordIds.Count == 0)
        {
            return [];
        }

        var sourceItemIds = await dbContext.Set<CognitiveMemorySourceLinkRecord>()
            .AsNoTracking()
            .Where(link => recordIds.Contains(link.MemoryRecordId))
            .Select(link => link.SourceItemId)
            .Distinct()
            .ToListAsync(cancellationToken);
        return await LoadSourceGraphItemsByIdAsync(dbContext, sourceItemIds, cancellationToken);
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadSourceGraphItemsByIdAsync(
        AppDbContext dbContext,
        IReadOnlyList<Guid> sourceItemIds,
        CancellationToken cancellationToken)
    {
        if (sourceItemIds.Count == 0)
        {
            return [];
        }

        return await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item => sourceItemIds.Contains(item.Id))
            .Select(item => new SourceGraphItemSnapshot(
                item.Id,
                item.SourceManifestId,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemType,
                item.SourceItemKey,
                item.Title,
                item.Locator,
                item.ProvenanceJson))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadNeighborSourceGraphItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<SourceGraphItemSnapshot> frontierItems,
        int expansionLimit,
        CancellationToken cancellationToken)
    {
        var structuralItems = await LoadProjectStructureNeighborItemsAsync(
            dbContext,
            request,
            frontierItems,
            expansionLimit,
            cancellationToken);
        var externalFileItems = await LoadExternalFileNeighborItemsAsync(
            dbContext,
            request,
            frontierItems,
            expansionLimit,
            cancellationToken);
        return structuralItems
            .Concat(externalFileItems)
            .GroupBy(item => item.SourceItemKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(expansionLimit)
            .ToList();
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadExplicitSourceGraphNeighborItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<SourceGraphItemSnapshot> frontierItems,
        int expansionLimit,
        CancellationToken cancellationToken)
    {
        var sourceItemKeys = frontierItems.Select(item => item.SourceItemKey).Distinct(StringComparer.Ordinal).ToArray();
        var sourceManifestIds = frontierItems.Select(item => item.SourceManifestId).Distinct().ToArray();
        if (sourceItemKeys.Length == 0 || sourceManifestIds.Length == 0)
        {
            return [];
        }

        var links = await dbContext.Set<CognitiveMemorySourceItemGraphLinkRecord>()
            .AsNoTracking()
            .Where(link =>
                link.ProjectId == request.ProjectId &&
                sourceManifestIds.Contains(link.SourceManifestId) &&
                (sourceItemKeys.Contains(link.SourceItemKey) || sourceItemKeys.Contains(link.TargetSourceItemKey)))
            .Take(expansionLimit)
            .Select(link => new
            {
                link.SourceManifestId,
                link.SourceItemKey,
                link.TargetSourceItemKey
            })
            .ToListAsync(cancellationToken);
        var neighborKeys = links
            .Select(link => sourceItemKeys.Contains(link.SourceItemKey) ? link.TargetSourceItemKey : link.SourceItemKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (neighborKeys.Length == 0)
        {
            return [];
        }

        return await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == request.ProjectId &&
                sourceManifestIds.Contains(item.SourceManifestId) &&
                neighborKeys.Contains(item.SourceItemKey))
            .Take(expansionLimit)
            .Select(item => new SourceGraphItemSnapshot(
                item.Id,
                item.SourceManifestId,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemType,
                item.SourceItemKey,
                item.Title,
                item.Locator,
                item.ProvenanceJson))
            .ToListAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadProjectStructureNeighborItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<SourceGraphItemSnapshot> frontierItems,
        int expansionLimit,
        CancellationToken cancellationToken)
    {
        var projectStructureFrontier = frontierItems
            .Where(item => item.SourceSystem == WorkbenchProjectStructureSourceSystem &&
                           item.SourceItemType == ProjectNodeSourceItemType)
            .Select(item => new
            {
                Item = item,
                Node = TryReadProjectStructureNode(item.ProvenanceJson)
            })
            .Where(item => item.Node is not null)
            .ToList();
        if (projectStructureFrontier.Count == 0)
        {
            return [];
        }

        var manifestIds = projectStructureFrontier
            .Select(item => item.Item.SourceManifestId)
            .Distinct()
            .ToArray();
        var frontierEntityIds = projectStructureFrontier
            .Select(item => item.Node!.SourceEntityId)
            .ToHashSet(StringComparer.Ordinal);
        var frontierParentIds = projectStructureFrontier
            .Select(item => item.Node!.ParentId)
            .Where(parentId => !string.IsNullOrWhiteSpace(parentId))
            .ToHashSet(StringComparer.Ordinal);
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == request.ProjectId &&
                manifestIds.Contains(item.SourceManifestId) &&
                item.SourceSystem == WorkbenchProjectStructureSourceSystem &&
                item.SourceItemType == ProjectNodeSourceItemType)
            .Select(item => new SourceGraphItemSnapshot(
                item.Id,
                item.SourceManifestId,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemType,
                item.SourceItemKey,
                item.Title,
                item.Locator,
                item.ProvenanceJson))
            .ToListAsync(cancellationToken);

        return sourceItems
            .Select(item => new
            {
                Item = item,
                Node = TryReadProjectStructureNode(item.ProvenanceJson)
            })
            .Where(item => item.Node is not null &&
                           (frontierEntityIds.Contains(item.Node.ParentId) ||
                            frontierParentIds.Contains(item.Node.SourceEntityId) &&
                            !string.IsNullOrWhiteSpace(item.Node.ParentId)))
            .Select(item => item.Item)
            .GroupBy(item => item.SourceItemKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(expansionLimit)
            .ToList();
    }

    private static async Task<IReadOnlyList<SourceGraphItemSnapshot>> LoadExternalFileNeighborItemsAsync(
        AppDbContext dbContext,
        CognitiveMemoryRecallRequest request,
        IReadOnlyList<SourceGraphItemSnapshot> frontierItems,
        int expansionLimit,
        CancellationToken cancellationToken)
    {
        var externalFrontier = frontierItems
            .Where(item => item.SourceSystem == ExternalFileSourceSystem &&
                           !string.IsNullOrWhiteSpace(item.Locator))
            .Select(item => new
            {
                item.SourceManifestId,
                DocumentLocator = ResolveDocumentLocator(item.Locator)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.DocumentLocator))
            .Distinct()
            .ToList();
        if (externalFrontier.Count == 0)
        {
            return [];
        }

        var manifestIds = externalFrontier.Select(item => item.SourceManifestId).Distinct().ToArray();
        var documentLocators = externalFrontier
            .Select(item => item.DocumentLocator)
            .ToHashSet(StringComparer.Ordinal);
        var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
            .AsNoTracking()
            .Where(item =>
                item.ProjectId == request.ProjectId &&
                manifestIds.Contains(item.SourceManifestId) &&
                item.SourceSystem == ExternalFileSourceSystem &&
                item.Locator != null)
            .Select(item => new SourceGraphItemSnapshot(
                item.Id,
                item.SourceManifestId,
                item.ProjectId,
                item.SourceSystem,
                item.SourceItemType,
                item.SourceItemKey,
                item.Title,
                item.Locator,
                item.ProvenanceJson))
            .ToListAsync(cancellationToken);

        return sourceItems
            .Where(item => documentLocators.Contains(ResolveDocumentLocator(item.Locator)))
            .GroupBy(item => item.SourceItemKey, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(expansionLimit)
            .ToList();
    }
}