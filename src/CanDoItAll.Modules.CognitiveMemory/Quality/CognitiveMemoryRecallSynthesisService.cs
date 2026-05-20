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
    private static readonly IReadOnlySet<string> QueryStopWords = new HashSet<string>([
        "about",
        "backed",
        "candidate",
        "candidates",
        "context",
        "during",
        "happen",
        "happens",
        "memory",
        "recall",
        "selected",
        "should",
        "source",
        "tell",
        "what",
        "when",
        "where",
        "which"
    ], StringComparer.Ordinal);

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

        var queryText = $"{request.RecallResult.ContextPack.Title} {request.RecallResult.ContextPack.Summary}";
        var queryTerms = ExtractQueryTerms(queryText);
        var queryTopic = ResolveQueryTopic(queryText, queryTerms);
        var sectionCandidates = selectedSections
            .Select(section => CreateSectionCandidate(section, queryTerms))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Text))
            .ToArray();
        var statements = sectionCandidates
            .GroupBy(candidate => ResolveStatementGroupKey(candidate, queryTerms), StringComparer.Ordinal)
            .OrderByDescending(group => group.Sum(candidate => candidate.QueryOverlap))
            .ThenByDescending(group => group.SelectMany(candidate => candidate.Section.SourceRefs).Count(sourceRef => sourceRef.IncludedInContext))
            .Take(request.MaxStatements)
            .Select(group => new CognitiveMemorySynthesizedRecallStatement(
                CognitiveMemorySynthesizedStatementId.New(),
                ComposeStatementText(queryTopic, group.ToArray()),
                group
                    .SelectMany(candidate => candidate.Section.SourceRefs)
                    .Where(sourceRef => sourceRef.IncludedInContext && CognitiveMemoryQualityText.PolicyCanRead(sourceRef.AccessLevel, request.PolicyContext))
                    .GroupBy(sourceRef => new { sourceRef.MemoryRecordId, sourceRef.SourceItemId, sourceRef.EvidenceAnchorId })
                    .Select(group => group.First())
                    .ToArray()))
            .Where(statement => !string.IsNullOrWhiteSpace(statement.Text))
            .ToArray();
        var brief = statements.Length == 0
            ? "No source-backed recall statements were synthesized."
            : string.Join(Environment.NewLine, statements.Select(statement => statement.Text));
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

    private static SectionCandidate CreateSectionCandidate(
        CognitiveMemoryRecallContextSection section,
        IReadOnlySet<string> queryTerms)
    {
        var statementText = ExtractStatementText(section);
        var statementTokens = CognitiveMemoryQualityText.ExtractMeaningfulTokens($"{section.Title} {statementText}", 32).ToHashSet(StringComparer.Ordinal);
        return new SectionCandidate(
            section,
            statementText,
            statementTokens.Count(queryTerms.Contains),
            HasCaveatSignal(section, statementText));
    }

    private static string ComposeStatementText(
        string queryTopic,
        IReadOnlyList<SectionCandidate> candidates)
    {
        var orderedCandidates = candidates
            .OrderByDescending(candidate => candidate.QueryOverlap)
            .ThenBy(candidate => candidate.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fragments = orderedCandidates
            .Select(candidate => NormalizeStatementFragment(candidate.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
        if (fragments.Length == 0)
        {
            return string.Empty;
        }

        var prefix = string.IsNullOrWhiteSpace(queryTopic)
            ? string.Empty
            : $"{queryTopic}: ";
        var caveat = orderedCandidates.Any(candidate => candidate.HasCaveatSignal)
            ? " Review caveat: recalled sources include stale, contradictory, or restricted context; inspect references before relying on this."
            : string.Empty;
        return CognitiveMemoryQualityText.TrimText($"{prefix}{JoinFragments(fragments)}.{caveat}", 900);
    }

    private static string ResolveStatementGroupKey(
        SectionCandidate candidate,
        IReadOnlySet<string> queryTerms)
    {
        if (queryTerms.Count == 0)
        {
            return "selected-context";
        }

        if (candidate.QueryOverlap > 0)
        {
            return $"query:{string.Join('.', queryTerms.Order(StringComparer.Ordinal).Take(6))}";
        }

        var contentKey = CognitiveMemoryQualityText.ExtractMeaningfulTokens(candidate.Text, 6)
            .Order(StringComparer.Ordinal)
            .Take(4)
            .ToArray();
        return contentKey.Length == 0
            ? $"section:{candidate.Section.SectionId.Value}"
            : $"content:{string.Join('.', contentKey)}";
    }

    private static IReadOnlySet<string> ExtractQueryTerms(string requestText)
        => Regex.Split(requestText.ToLowerInvariant(), "[^\\p{L}\\p{Nd}]+")
            .Where(token => token.Length >= 4 && !QueryStopWords.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);

    private static string ResolveQueryTopic(
        string requestText,
        IReadOnlySet<string> queryTerms)
    {
        if (queryTerms.Count == 0)
        {
            return string.Empty;
        }

        var orderedTerms = Regex.Split(requestText.ToLowerInvariant(), "[^\\p{L}\\p{Nd}]+")
            .Where(token => queryTerms.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .Take(3)
            .Select(Capitalize)
            .ToArray();
        return string.Join(' ', orderedTerms);
    }

    private static string ExtractStatementText(CognitiveMemoryRecallContextSection section)
    {
        var content = section.Content.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (content.Length == 0)
        {
            return section.Title;
        }

        var usefulLines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(IsUsefulBriefLine)
            .Take(3)
            .Select(line => line.Trim().TrimStart('-', '*').Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var statement = string.Join(" ", usefulLines);
        return string.IsNullOrWhiteSpace(statement)
            ? section.Title
            : CognitiveMemoryQualityText.TrimText(statement, 900);
    }

    private static string NormalizeStatementFragment(string value)
    {
        var trimmed = value.Trim().TrimEnd('.');
        return string.IsNullOrWhiteSpace(trimmed)
            ? string.Empty
            : trimmed;
    }

    private static string JoinFragments(IReadOnlyList<string> fragments)
        => fragments.Count switch
        {
            0 => string.Empty,
            1 => fragments[0],
            _ => $"{string.Join("; ", fragments.Take(fragments.Count - 1))}; {fragments[^1]}"
        };

    private static bool HasCaveatSignal(
        CognitiveMemoryRecallContextSection section,
        string text)
    {
        var normalized = $"{section.Title} {text}".ToLowerInvariant();
        return normalized.Contains("contradict", StringComparison.Ordinal) ||
               normalized.Contains("conflict", StringComparison.Ordinal) ||
               normalized.Contains("stale", StringComparison.Ordinal) ||
               normalized.Contains("superseded", StringComparison.Ordinal) ||
               normalized.Contains("restricted", StringComparison.Ordinal) ||
               section.SourceRefs.Any(sourceRef => sourceRef.RedactionState is CognitiveMemoryRedactionState.Redacted or CognitiveMemoryRedactionState.Restricted ||
                                                   sourceRef.ExclusionReasonKind != CognitiveMemoryRecallExclusionReasonKind.None);
    }

    private static string Capitalize(string value)
        => value.Length == 0
            ? value
            : $"{char.ToUpperInvariant(value[0])}{value[1..]}";

    private static bool IsUsefulBriefLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        var normalized = line.Trim().ToLowerInvariant();
        return !normalized.StartsWith("source:", StringComparison.Ordinal) &&
               !normalized.StartsWith("sources:", StringComparison.Ordinal) &&
               !normalized.StartsWith("reference:", StringComparison.Ordinal) &&
               !normalized.StartsWith("references:", StringComparison.Ordinal) &&
               !normalized.Contains("displaybeliefscore", StringComparison.Ordinal) &&
               !normalized.Contains("internal score", StringComparison.Ordinal) &&
               !normalized.Contains("belief score", StringComparison.Ordinal);
    }

    private sealed record SectionCandidate(
        CognitiveMemoryRecallContextSection Section,
        string Text,
        int QueryOverlap,
        bool HasCaveatSignal);
}
