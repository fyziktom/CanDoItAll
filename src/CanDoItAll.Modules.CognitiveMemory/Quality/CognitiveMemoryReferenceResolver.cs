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
        }).ToArray();
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
