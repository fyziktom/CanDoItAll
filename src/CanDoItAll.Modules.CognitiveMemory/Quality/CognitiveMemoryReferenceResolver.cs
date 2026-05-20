using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
public sealed class CognitiveMemoryReferenceResolver(
    IDbContextFactory<AppDbContext> dbContextFactory) : ICognitiveMemoryReferenceResolver
{
    public async ValueTask<CognitiveMemoryReferenceResolverResult> ResolveAsync(
        CognitiveMemoryReferenceResolverRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var rows = await dbContext.Set<CognitiveMemorySynthesizedStatementSourceMapRecord>()
            .AsNoTracking()
            .Where(sourceMap => sourceMap.StatementId == request.StatementId.Value)
            .OrderBy(sourceMap => sourceMap.SourceSystem)
            .ThenBy(sourceMap => sourceMap.Locator)
            .ToListAsync(cancellationToken);
        var warnings = new List<string>();
        if (rows.Count == 0)
        {
            warnings.Add($"No reference source maps exist for synthesized statement '{request.StatementId}'.");
        }

        var references = rows.Select(row =>
        {
            var included = CanResolve(row, request);
            return new CognitiveMemoryResolvedReference(
                request.StatementId,
                new CognitiveMemoryRecordId(row.MemoryRecordId),
                row.SourceItemId is null ? null : new CognitiveMemorySourceItemId(row.SourceItemId.Value),
                row.EvidenceAnchorId is null ? null : new CognitiveMemoryEvidenceAnchorId(row.EvidenceAnchorId.Value),
                row.SourceSystem,
                included ? row.Locator : string.Empty,
                included ? CognitiveMemoryQualityText.Redact(row.Summary) : string.Empty,
                included,
                included ? CognitiveMemoryRecallExclusionReasonKind.None : ResolveExclusion(row, request.PolicyContext));
        }).ToList();
        var aggregateRecordIds = rows.Select(row => row.MemoryRecordId).Distinct().ToArray();
        var aggregateCandidates = await dbContext.Set<CognitiveMemoryDreamAggregateCandidateRecord>()
            .AsNoTracking()
            .Where(candidate => candidate.MemoryRecordId != null && aggregateRecordIds.Contains(candidate.MemoryRecordId.Value))
            .ToListAsync(cancellationToken);
        if (aggregateCandidates.Count > 0)
        {
            var candidateIds = aggregateCandidates.Select(candidate => candidate.Id).ToArray();
            var sourceMaps = await dbContext.Set<CognitiveMemoryDreamAggregateClaimSourceMapRecord>()
                .AsNoTracking()
                .Where(sourceMap => candidateIds.Contains(sourceMap.AggregateCandidateId))
                .ToListAsync(cancellationToken);
            var sourceItemIds = sourceMaps
                .Select(sourceMap => sourceMap.SourceItemId)
                .Where(id => id is not null)
                .Select(id => id!.Value)
                .Distinct()
                .ToArray();
            var sourceItems = await dbContext.Set<CognitiveMemorySourceItemRecord>()
                .AsNoTracking()
                .Where(item => sourceItemIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, cancellationToken);
            foreach (var sourceMap in sourceMaps)
            {
                var sourceItem = sourceMap.SourceItemId is { } sourceItemId
                    ? sourceItems.GetValueOrDefault(sourceItemId)
                    : null;
                var row = new CognitiveMemorySynthesizedStatementSourceMapRecord
                {
                    StatementId = request.StatementId.Value,
                    MemoryRecordId = sourceMap.SourceMemoryRecordId,
                    SourceItemId = sourceMap.SourceItemId,
                    EvidenceAnchorId = sourceMap.EvidenceAnchorId,
                    SourceSystem = sourceItem?.SourceSystem ?? "aggregate-source",
                    Locator = sourceItem?.Locator ?? string.Empty,
                    Summary = sourceMap.Summary,
                    AccessLevel = sourceMap.AccessLevel,
                    RedactionState = sourceMap.RedactionState
                };
                var included = CanResolve(row, request);
                references.Add(new CognitiveMemoryResolvedReference(
                    request.StatementId,
                    new CognitiveMemoryRecordId(sourceMap.SourceMemoryRecordId),
                    sourceMap.SourceItemId is null ? null : new CognitiveMemorySourceItemId(sourceMap.SourceItemId.Value),
                    sourceMap.EvidenceAnchorId is null ? null : new CognitiveMemoryEvidenceAnchorId(sourceMap.EvidenceAnchorId.Value),
                    row.SourceSystem,
                    included ? row.Locator : string.Empty,
                    included ? CognitiveMemoryQualityText.Redact(row.Summary) : string.Empty,
                    included,
                    included ? CognitiveMemoryRecallExclusionReasonKind.None : ResolveExclusion(row, request.PolicyContext)));
            }
        }

        references = references
            .GroupBy(reference => new { reference.MemoryRecordId, reference.SourceItemId, reference.EvidenceAnchorId })
            .Select(group => group.First())
            .OrderBy(reference => reference.SourceSystem, StringComparer.Ordinal)
            .ThenBy(reference => reference.Locator, StringComparer.Ordinal)
            .ToList();
        return new CognitiveMemoryReferenceResolverResult(references, warnings);
    }

    private static bool CanResolve(
        CognitiveMemorySynthesizedStatementSourceMapRecord row,
        CognitiveMemoryReferenceResolverRequest request)
    {
        if (!CognitiveMemoryQualityText.PolicyCanRead(row.AccessLevel, request.PolicyContext))
        {
            return false;
        }

        return row.RedactionState switch
        {
            CognitiveMemoryRedactionState.Safe or CognitiveMemoryRedactionState.Unclassified => true,
            CognitiveMemoryRedactionState.Restricted => request.IncludeRestrictedContent && request.PolicyContext.AllowRestrictedContent,
            _ => false
        };
    }

    private static CognitiveMemoryRecallExclusionReasonKind ResolveExclusion(
        CognitiveMemorySynthesizedStatementSourceMapRecord row,
        CognitiveMemoryPolicyContext policyContext)
    {
        if (!CognitiveMemoryQualityText.PolicyCanRead(row.AccessLevel, policyContext))
        {
            return CognitiveMemoryRecallExclusionReasonKind.AccessPolicy;
        }

        return CognitiveMemoryRecallExclusionReasonKind.RedactedSource;
    }
}
