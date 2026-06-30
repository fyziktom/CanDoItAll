using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed partial class CognitiveMemoryReviewUiService
{
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
}
