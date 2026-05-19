using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.CognitiveMemory;
public sealed class CognitiveMemoryRecallSynthesisService(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IClock clock) : ICognitiveMemoryRecallSynthesisService
{
    public async ValueTask<CognitiveMemorySynthesizedRecallResult> SynthesizeAsync(
        CognitiveMemoryRecallSynthesisRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaxStatements <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Recall synthesis statement budget must be positive.");
        }

        var selectedSections = request.RecallResult.ContextPack.Sections
            .Where(section => section.SectionKind == CognitiveMemoryRecallContextSectionKind.SelectedMemory)
            .ToArray();
        var warnings = new List<string>();
        if (selectedSections.Length == 0)
        {
            warnings.Add("Recall synthesis received no selected memory sections.");
        }

        var statements = selectedSections
            .GroupBy(section => CognitiveMemoryQualityText.NormalizeKey(section.Title), StringComparer.Ordinal)
            .Take(request.MaxStatements)
            .Select(sectionGroup => new CognitiveMemorySynthesizedRecallStatement(
                CognitiveMemorySynthesizedStatementId.New(),
                CognitiveMemoryQualityText.TrimText(
                    string.Join(
                        " ",
                        sectionGroup
                            .Select(ExtractStatementText)
                            .Where(text => !string.IsNullOrWhiteSpace(text))
                            .Distinct(StringComparer.Ordinal)),
                    900),
                sectionGroup
                    .SelectMany(section => section.SourceRefs)
                    .Where(sourceRef => sourceRef.IncludedInContext && CognitiveMemoryQualityText.PolicyCanRead(sourceRef.AccessLevel, request.PolicyContext))
                    .GroupBy(sourceRef => new { sourceRef.MemoryRecordId, sourceRef.SourceItemId, sourceRef.EvidenceAnchorId })
                    .Select(group => group.First())
                    .ToArray()))
            .Where(statement => !string.IsNullOrWhiteSpace(statement.Text))
            .ToArray();
        var brief = statements.Length == 0
            ? "No source-backed recall statements were synthesized."
            : string.Join(Environment.NewLine, statements.Select(statement => $"- {statement.Text}"));
        var synthesisId = CognitiveMemorySynthesizedRecallId.New();

        if (request.PersistSynthesis)
        {
            await PersistAsync(request, synthesisId, brief, statements, cancellationToken);
        }

        return new CognitiveMemorySynthesizedRecallResult(
            synthesisId,
            request.RecallResult.ContextPack.ProjectId,
            request.RecallResult.TraceId,
            brief,
            statements,
            ReferencesShownByDefault: false,
            warnings);
    }

    private async Task PersistAsync(
        CognitiveMemoryRecallSynthesisRequest request,
        CognitiveMemorySynthesizedRecallId synthesisId,
        string brief,
        IReadOnlyList<CognitiveMemorySynthesizedRecallStatement> statements,
        CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var traceExists = await dbContext.Set<CognitiveMemoryRecallTraceRecord>()
            .AnyAsync(trace => trace.Id == request.RecallResult.TraceId, cancellationToken);
        if (!traceExists)
        {
            throw new InvalidOperationException($"Recall trace '{request.RecallResult.TraceId:D}' was not found for synthesis persistence.");
        }

        var nowUtc = clock.GetUtcNow();
        dbContext.Add(new CognitiveMemorySynthesizedRecallRecord
        {
            Id = synthesisId.Value,
            ProjectId = request.RecallResult.ContextPack.ProjectId,
            RecallTraceId = request.RecallResult.TraceId,
            Brief = brief,
            ReferencesShownByDefault = false,
            StatementCount = statements.Count,
            SourceMapCount = statements.Sum(statement => statement.SourceRefs.Count),
            CreatedAtUtc = nowUtc,
            ConcurrencyToken = Guid.NewGuid()
        });

        var sequence = 0;
        foreach (var statement in statements)
        {
            dbContext.Add(new CognitiveMemorySynthesizedStatementRecord
            {
                Id = statement.StatementId.Value,
                SynthesisId = synthesisId.Value,
                ProjectId = request.RecallResult.ContextPack.ProjectId,
                Sequence = sequence,
                Text = statement.Text,
                CreatedAtUtc = nowUtc
            });
            foreach (var sourceRef in statement.SourceRefs)
            {
                dbContext.Add(new CognitiveMemorySynthesizedStatementSourceMapRecord
                {
                    Id = Guid.NewGuid(),
                    SynthesisId = synthesisId.Value,
                    StatementId = statement.StatementId.Value,
                    ProjectId = request.RecallResult.ContextPack.ProjectId,
                    MemoryRecordId = sourceRef.MemoryRecordId.Value,
                    SourceItemId = sourceRef.SourceItemId?.Value,
                    EvidenceAnchorId = sourceRef.EvidenceAnchorId?.Value,
                    SourceSystem = sourceRef.SourceSystem,
                    Locator = sourceRef.Locator,
                    Summary = sourceRef.Summary,
                    AccessLevel = sourceRef.AccessLevel,
                    RedactionState = sourceRef.RedactionState,
                    CreatedAtUtc = nowUtc
                });
            }

            sequence++;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string ExtractStatementText(CognitiveMemoryRecallContextSection section)
    {
        var content = section.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (content.Length == 0)
        {
            return section.Title;
        }

        var firstLine = content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        return string.IsNullOrWhiteSpace(firstLine)
            ? section.Title
            : firstLine;
    }
}
