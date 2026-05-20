using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed record CognitiveMemoryRecallBriefComposerRequest(
    string QueryText,
    IReadOnlyList<CognitiveMemoryRecallContextSection> SelectedSections,
    IReadOnlySet<Guid> AggregateClaimIds,
    CognitiveMemoryPolicyContext PolicyContext,
    int MaxStatements);

public sealed record CognitiveMemoryRecallBriefComposerResult(
    string Brief,
    IReadOnlyList<CognitiveMemorySynthesizedRecallStatement> Statements,
    IReadOnlyList<string> Warnings);

public interface ICognitiveMemoryRecallBriefComposer
{
    CognitiveMemoryRecallBriefComposerResult Compose(CognitiveMemoryRecallBriefComposerRequest request);
}

public sealed class CognitiveMemoryRecallBriefComposer(
    CognitiveMemoryQualityAlgorithmOptions? algorithmOptions = null) : ICognitiveMemoryRecallBriefComposer
{
    private readonly CognitiveMemoryQualityRecallAlgorithmOptions options = (algorithmOptions ?? CognitiveMemoryQualityAlgorithmOptions.Current).Recall;

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

    public CognitiveMemoryRecallBriefComposerResult Compose(CognitiveMemoryRecallBriefComposerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.MaxStatements <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Recall brief statement budget must be positive.");
        }

        var warnings = new List<string>();
        var queryTerms = ExtractQueryTerms(request.QueryText);
        var queryTopic = ResolveQueryTopic(request.QueryText, queryTerms);
        var sectionCandidates = request.SelectedSections
            .Select(section => CreateSectionCandidate(section, queryTerms, request.AggregateClaimIds))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Text))
            .ToArray();
        var statementGroups = CreateStatementGroups(sectionCandidates, queryTerms)
            .OrderByDescending(group => group.QueryOverlap)
            .ThenByDescending(group => group.IncludedSourceCount)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();
        var omittedDetailCount = statementGroups
            .Skip(request.MaxStatements)
            .Sum(group => group.Candidates.Count);
        if (omittedDetailCount > 0)
        {
            warnings.Add($"Omitted {omittedDetailCount} selected recall detail(s) because the statement budget is {request.MaxStatements}.");
        }

        var statements = statementGroups
            .Take(request.MaxStatements)
            .Select(group => new CognitiveMemorySynthesizedRecallStatement(
                CognitiveMemorySynthesizedStatementId.New(),
                ComposeStatementText(queryTopic, group.Candidates, group.IsConflictCaveat),
                group.Candidates
                    .SelectMany(candidate => candidate.AggregateClaimIds)
                    .Distinct()
                    .ToArray(),
                group
                    .Candidates
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
        return new CognitiveMemoryRecallBriefComposerResult(brief, statements, warnings);
    }

    private SectionCandidate CreateSectionCandidate(
        CognitiveMemoryRecallContextSection section,
        IReadOnlySet<string> queryTerms,
        IReadOnlySet<Guid> aggregateClaimIds)
    {
        var statementText = ExtractStatementText(section);
        var statementTokens = CognitiveMemoryQualityText.ExtractMeaningfulTokens($"{section.Title} {statementText}", 32).ToHashSet(StringComparer.Ordinal);
        var sectionAggregateClaimIds = section.ClaimIds
            .Where(claimId => aggregateClaimIds.Contains(claimId.Value))
            .Distinct()
            .ToArray();
        return new SectionCandidate(
            section,
            statementText,
            statementTokens.Count(queryTerms.Contains),
            HasCaveatSignal(section, statementText),
            HasActionSignal(statementText),
            ResolveConflictKey(section, statementText, queryTerms),
            ResolveConflictPolarity(section, statementText),
            sectionAggregateClaimIds);
    }

    private string ComposeStatementText(
        string queryTopic,
        IReadOnlyList<SectionCandidate> candidates,
        bool isConflictCaveat)
    {
        var orderedCandidates = candidates
            .OrderByDescending(candidate => candidate.QueryOverlap)
            .ThenBy(candidate => candidate.Text, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var fragments = orderedCandidates
            .Select(candidate => NormalizeStatementFragment(candidate.Text))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(options.MaxFragmentsPerStatement)
            .ToArray();
        if (fragments.Length == 0)
        {
            return string.Empty;
        }

        var prefix = string.IsNullOrWhiteSpace(queryTopic)
            ? string.Empty
            : $"{queryTopic}: ";
        var kind = isConflictCaveat
            ? "Conflict caveat - "
            : orderedCandidates.Any(candidate => candidate.HasActionSignal)
                ? "Action - "
                : "Answer - ";
        var caveat = orderedCandidates.Any(candidate => candidate.HasCaveatSignal)
            ? " Review caveat: recalled sources include stale, contradictory, or restricted context; inspect references before relying on this."
            : string.Empty;
        return CognitiveMemoryQualityText.TrimText($"{prefix}{kind}{JoinFragments(fragments)}.{caveat}", options.MaxStatementCharacters);
    }

    private static IReadOnlyList<StatementCandidateGroup> CreateStatementGroups(
        IReadOnlyList<SectionCandidate> candidates,
        IReadOnlySet<string> queryTerms)
    {
        var groups = new List<StatementCandidateGroup>();
        var conflictSectionKeys = candidates
            .Where(candidate => candidate.ConflictPolarity != StatementConflictPolarity.None)
            .GroupBy(candidate => candidate.ConflictKey, StringComparer.Ordinal)
            .Where(group => group.Select(candidate => candidate.ConflictPolarity).Distinct().Count() > 1)
            .SelectMany(group => group.Select(candidate => candidate.Section.SectionId.Value))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in candidates.Where(candidate => conflictSectionKeys.Contains(candidate.Section.SectionId.Value)))
        {
            groups.Add(new StatementCandidateGroup(
                $"conflict:{candidate.ConflictKey}:{candidate.ConflictPolarity}:{candidate.Section.SectionId.Value}",
                IsConflictCaveat: true,
                [candidate]));
        }

        groups.AddRange(candidates
            .Where(candidate => !conflictSectionKeys.Contains(candidate.Section.SectionId.Value))
            .GroupBy(candidate => ResolveStatementGroupKey(candidate, queryTerms), StringComparer.Ordinal)
            .Select(group => new StatementCandidateGroup(group.Key, IsConflictCaveat: false, group.ToArray())));

        return groups;
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

    private static bool HasActionSignal(string text)
        => ContainsAny(text, ["use ", "notify ", "requires ", "must ", "should ", "verify ", "restore ", "assign ", "run "]);

    private static string ResolveConflictKey(
        CognitiveMemoryRecallContextSection section,
        string text,
        IReadOnlySet<string> queryTerms)
    {
        var terms = queryTerms.Count > 0
            ? queryTerms
            : CognitiveMemoryQualityText.ExtractMeaningfulTokens($"{section.Title} {text}", 8).ToHashSet(StringComparer.Ordinal);
        var keyTerms = terms
            .Where(term => term is not ("requires" or "without" or "signed" or "during" or "before" or "after"))
            .Order(StringComparer.Ordinal)
            .Take(6)
            .ToArray();
        return keyTerms.Length == 0
            ? "selected-context"
            : string.Join('.', keyTerms);
    }

    private static StatementConflictPolarity ResolveConflictPolarity(
        CognitiveMemoryRecallContextSection section,
        string text)
    {
        var normalized = $"{section.Title} {text}".ToLowerInvariant();
        if (!ContainsAny(normalized, ["approval", "sign-off", "signoff", "authorize", "authorization", "permission"]))
        {
            return StatementConflictPolarity.None;
        }

        var exception = ContainsAny(normalized, ["without", "not required", "does not require", "may restore", "skip", "skipped", "optional"]);
        if (exception)
        {
            return StatementConflictPolarity.Exception;
        }

        return ContainsAny(normalized, ["requires", "must", "signed", "before", "cannot restore"])
            ? StatementConflictPolarity.Required
            : StatementConflictPolarity.None;
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> needles)
        => needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));

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
        bool HasCaveatSignal,
        bool HasActionSignal,
        string ConflictKey,
        StatementConflictPolarity ConflictPolarity,
        IReadOnlyList<CognitiveMemoryClaimId> AggregateClaimIds);

    private sealed record StatementCandidateGroup(
        string Key,
        bool IsConflictCaveat,
        IReadOnlyList<SectionCandidate> Candidates)
    {
        public int QueryOverlap => Candidates.Sum(candidate => candidate.QueryOverlap);

        public int IncludedSourceCount => Candidates
            .SelectMany(candidate => candidate.Section.SourceRefs)
            .Count(sourceRef => sourceRef.IncludedInContext);
    }

    private enum StatementConflictPolarity
    {
        None = 0,
        Required = 1,
        Exception = 2
    }
}
