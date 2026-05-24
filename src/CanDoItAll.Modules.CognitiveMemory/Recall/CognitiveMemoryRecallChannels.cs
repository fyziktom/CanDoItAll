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
            var termRecords = await termQuery
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
            var fallbackRecords = await fallbackQuery
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
            var matches = await sourceQuery
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
}
