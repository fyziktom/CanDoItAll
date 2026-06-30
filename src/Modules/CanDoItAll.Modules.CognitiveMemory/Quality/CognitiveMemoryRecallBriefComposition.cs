using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed record CognitiveMemoryRecallBriefComposerRequest(
    string QueryText,
    IReadOnlyList<CognitiveMemoryRecallContextSection> SelectedSections,
    IReadOnlySet<Guid> AggregateClaimIds,
    CognitiveMemoryPolicyContext PolicyContext,
    int MaxStatements,
    CognitiveMemoryRecallIntentKind Intent = CognitiveMemoryRecallIntentKind.Unknown);

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
        "handled",
        "memory",
        "please",
        "recall",
        "selected",
        "should",
        "show",
        "source",
        "sources",
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
            .SelectMany(section => CreateSectionCandidates(section, queryTerms, request.AggregateClaimIds, request.Intent))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Text))
            .ToArray();
        var statementPlans = CreateStatementPlans(sectionCandidates, queryTerms, request)
            .OrderBy(plan => ResolvePlanPriority(plan.PlanKind))
            .ThenByDescending(plan => plan.QueryOverlap)
            .ThenByDescending(plan => plan.IncludedSourceCount)
            .ThenBy(plan => plan.Key, StringComparer.Ordinal)
            .ToArray();
        var omittedPlans = statementPlans
            .Skip(request.MaxStatements)
            .ToArray();
        AddOmittedWarnings(omittedPlans, request.MaxStatements, warnings);

        var statements = statementPlans
            .Take(request.MaxStatements)
            .Select(plan => CreateStatement(plan, queryTopic, request.PolicyContext))
            .Where(statement => !string.IsNullOrWhiteSpace(statement.Text))
            .ToArray();
        var brief = statements.Length == 0
            ? "Missing evidence - no source-backed recall statements were synthesized."
            : string.Join(Environment.NewLine, statements.Select(statement => statement.Text));
        return new CognitiveMemoryRecallBriefComposerResult(brief, statements, warnings);
    }

    private SectionCandidate[] CreateSectionCandidates(
        CognitiveMemoryRecallContextSection section,
        IReadOnlySet<string> queryTerms,
        IReadOnlySet<Guid> aggregateClaimIds,
        CognitiveMemoryRecallIntentKind intent)
    {
        var statementText = ExtractStatementText(section);
        var statementTokens = CognitiveMemoryQualityText.ExtractMeaningfulTokens($"{section.Title} {statementText}", 32).ToHashSet(StringComparer.Ordinal);
        var sectionAggregateClaimIds = section.ClaimIds
            .Where(claimId => aggregateClaimIds.Contains(claimId.Value))
            .Distinct()
            .ToArray();
        if (sectionAggregateClaimIds.Length == 0)
        {
            return
            [
                CreateSectionCandidate(section, statementText, statementTokens, queryTerms, aggregateClaimId: null, intent)
            ];
        }

        return sectionAggregateClaimIds
            .Select(claimId => CreateSectionCandidate(section, statementText, statementTokens, queryTerms, claimId, intent))
            .ToArray();
    }

    private static SectionCandidate CreateSectionCandidate(
        CognitiveMemoryRecallContextSection section,
        string statementText,
        IReadOnlySet<string> statementTokens,
        IReadOnlySet<string> queryTerms,
        CognitiveMemoryClaimId? aggregateClaimId,
        CognitiveMemoryRecallIntentKind intent)
    {
        var hasSourceEvidence = section.SourceRefs.Count > 0;
        var hasCaveatSignal = HasCaveatSignal(section, statementText);
        var planKind = ResolveCandidatePlanKind(hasSourceEvidence, hasCaveatSignal, statementText, intent);
        return new SectionCandidate(
            section,
            statementText,
            statementTokens.Count(queryTerms.Contains),
            hasCaveatSignal,
            ResolveConflictKey(section, statementText, queryTerms),
            ResolveConflictPolarity(section, statementText),
            aggregateClaimId,
            planKind);
    }

    private static IReadOnlyList<StatementPlan> CreateStatementPlans(
        IReadOnlyList<SectionCandidate> candidates,
        IReadOnlySet<string> queryTerms,
        CognitiveMemoryRecallBriefComposerRequest request)
    {
        if (candidates.Count == 0)
        {
            return [CreateMissingEvidencePlan(request)];
        }

        candidates = FilterDominatedQueryCandidates(candidates);
        var plans = new List<StatementPlan>();
        var conflictSectionKeys = candidates
            .Where(candidate => candidate.ConflictPolarity != StatementConflictPolarity.None)
            .GroupBy(candidate => candidate.ConflictKey, StringComparer.Ordinal)
            .Where(group => group.Select(candidate => candidate.ConflictPolarity).Distinct().Count() > 1)
            .SelectMany(group => group.Select(candidate => candidate.Section.SectionId.Value))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var candidate in candidates.Where(candidate => conflictSectionKeys.Contains(candidate.Section.SectionId.Value)))
        {
            plans.Add(new StatementPlan(
                $"conflict:{candidate.ConflictKey}:{candidate.ConflictPolarity}:{candidate.Section.SectionId.Value}:{candidate.AggregateClaimId?.Value:D}",
                CognitiveMemoryRecallStatementPlanKind.Conflict,
                [candidate]));
        }

        plans.AddRange(candidates
            .Where(candidate => !conflictSectionKeys.Contains(candidate.Section.SectionId.Value))
            .GroupBy(candidate => ResolveStatementGroupKey(candidate, queryTerms), StringComparer.Ordinal)
            .Select(group => new StatementPlan(group.Key, ResolveGroupPlanKind(group), group.ToArray())));

        if (IsExplicitReferenceRequest(request.QueryText))
        {
            plans.Add(CreateReferenceHintPlan(candidates, request.PolicyContext));
        }

        return plans;
    }

    private static IReadOnlyList<SectionCandidate> FilterDominatedQueryCandidates(
        IReadOnlyList<SectionCandidate> candidates)
    {
        var maxOverlap = candidates.Max(candidate => candidate.QueryOverlap);
        if (maxOverlap <= 1)
        {
            return candidates;
        }

        return candidates
            .Where(candidate =>
                candidate.QueryOverlap == maxOverlap ||
                candidate.AggregateClaimId is not null ||
                candidate.ConflictPolarity != StatementConflictPolarity.None ||
                candidate.PlanKind is CognitiveMemoryRecallStatementPlanKind.Caveat
                    or CognitiveMemoryRecallStatementPlanKind.MissingEvidence)
            .ToArray();
    }

    private CognitiveMemorySynthesizedRecallStatement CreateStatement(
        StatementPlan plan,
        string queryTopic,
        CognitiveMemoryPolicyContext policyContext)
        => new(
            CognitiveMemorySynthesizedStatementId.New(),
            ComposeStatementText(queryTopic, plan),
            plan.PlanKind,
            ResolveAggregateClaimIds(plan),
            ResolveSourceRefs(plan, policyContext));

    private string ComposeStatementText(
        string queryTopic,
        StatementPlan plan)
    {
        if (!string.IsNullOrWhiteSpace(plan.ExplicitText))
        {
            return CognitiveMemoryQualityText.TrimText(plan.ExplicitText, options.MaxStatementCharacters);
        }

        var orderedCandidates = plan.Candidates
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

        var prefix = ShouldPrefixWithTopic(plan.PlanKind) && !string.IsNullOrWhiteSpace(queryTopic)
            ? $"{queryTopic}: "
            : string.Empty;
        var caveat = plan.PlanKind switch
        {
            CognitiveMemoryRecallStatementPlanKind.Conflict => " Resolve the conflict before relying on this.",
            CognitiveMemoryRecallStatementPlanKind.Caveat => " Inspect references before relying on this.",
            _ when orderedCandidates.Any(candidate => candidate.HasCaveatSignal) => " Review caveat: recalled sources include stale, contradictory, or restricted context; inspect references before relying on this.",
            _ => string.Empty
        };
        return CognitiveMemoryQualityText.TrimText($"{prefix}{ResolvePlanPrefix(plan.PlanKind)}{JoinFragments(fragments)}.{caveat}", options.MaxStatementCharacters);
    }

    private static IReadOnlyList<CognitiveMemoryClaimId> ResolveAggregateClaimIds(StatementPlan plan)
        => plan.AggregateClaimIdsOverride ??
           plan.Candidates
               .Select(candidate => candidate.AggregateClaimId)
               .Where(claimId => claimId is not null)
               .Select(claimId => claimId!.Value)
               .Distinct()
               .ToArray();

    private static IReadOnlyList<CognitiveMemoryRecallSourceRef> ResolveSourceRefs(
        StatementPlan plan,
        CognitiveMemoryPolicyContext policyContext)
        => (plan.SourceRefsOverride ??
            plan.Candidates
                .SelectMany(candidate => candidate.Section.SourceRefs))
            .Where(sourceRef => sourceRef.IncludedInContext && CognitiveMemoryQualityText.PolicyCanRead(sourceRef.AccessLevel, policyContext))
            .GroupBy(sourceRef => new { sourceRef.MemoryRecordId, sourceRef.SourceItemId, sourceRef.EvidenceAnchorId })
            .Select(group => group.First())
            .ToArray();

    private static StatementPlan CreateMissingEvidencePlan(CognitiveMemoryRecallBriefComposerRequest request)
    {
        var subject = string.IsNullOrWhiteSpace(request.QueryText)
            ? "the recall request"
            : CognitiveMemoryQualityText.TrimText(request.QueryText.Trim(), 160);
        return new StatementPlan(
            "missing-evidence:selected-context",
            CognitiveMemoryRecallStatementPlanKind.MissingEvidence,
            [],
            $"Missing evidence - no selected source-backed memory was available for {subject}.");
    }

    private static StatementPlan CreateReferenceHintPlan(
        IReadOnlyList<SectionCandidate> candidates,
        CognitiveMemoryPolicyContext policyContext)
    {
        var sourceRefs = candidates
            .SelectMany(candidate => candidate.Section.SourceRefs)
            .Where(sourceRef => sourceRef.IncludedInContext && CognitiveMemoryQualityText.PolicyCanRead(sourceRef.AccessLevel, policyContext))
            .GroupBy(sourceRef => new { sourceRef.MemoryRecordId, sourceRef.SourceItemId, sourceRef.EvidenceAnchorId })
            .Select(group => group.First())
            .ToArray();
        return new StatementPlan(
            "reference-hint:on-demand",
            CognitiveMemoryRecallStatementPlanKind.ReferenceHint,
            [],
            "Reference hint - source locators and summaries are available through statement reference resolution; they are hidden from the brief by default.",
            sourceRefs,
            []);
    }

    private static string ResolveStatementGroupKey(
        SectionCandidate candidate,
        IReadOnlySet<string> queryTerms)
    {
        if (candidate.AggregateClaimId is { } aggregateClaimId)
        {
            return $"claim:{aggregateClaimId.Value:D}";
        }

        var planPrefix = $"plan:{candidate.PlanKind}";
        if (queryTerms.Count == 0)
        {
            return $"{planPrefix}:selected-context";
        }

        if (candidate.QueryOverlap > 0)
        {
            return $"{planPrefix}:query:{string.Join('.', queryTerms.Order(StringComparer.Ordinal).Take(6))}";
        }

        var contentKey = CognitiveMemoryQualityText.ExtractMeaningfulTokens(candidate.Text, 6)
            .Order(StringComparer.Ordinal)
            .Take(4)
            .ToArray();
        return contentKey.Length == 0
            ? $"{planPrefix}:section:{candidate.Section.SectionId.Value}"
            : $"{planPrefix}:content:{string.Join('.', contentKey)}";
    }

    private static CognitiveMemoryRecallStatementPlanKind ResolveGroupPlanKind(
        IEnumerable<SectionCandidate> candidates)
    {
        var kinds = candidates.Select(candidate => candidate.PlanKind).ToArray();
        foreach (var planKind in new[]
        {
            CognitiveMemoryRecallStatementPlanKind.MissingEvidence,
            CognitiveMemoryRecallStatementPlanKind.Caveat,
            CognitiveMemoryRecallStatementPlanKind.Action,
            CognitiveMemoryRecallStatementPlanKind.Answer
        })
        {
            if (kinds.Contains(planKind))
            {
                return planKind;
            }
        }

        return CognitiveMemoryRecallStatementPlanKind.Answer;
    }

    private static CognitiveMemoryRecallStatementPlanKind ResolveCandidatePlanKind(
        bool hasSourceEvidence,
        bool hasCaveatSignal,
        string text,
        CognitiveMemoryRecallIntentKind intent)
    {
        if (!hasSourceEvidence)
        {
            return CognitiveMemoryRecallStatementPlanKind.MissingEvidence;
        }

        if (hasCaveatSignal)
        {
            return CognitiveMemoryRecallStatementPlanKind.Caveat;
        }

        return HasActionSignal(text, intent)
            ? CognitiveMemoryRecallStatementPlanKind.Action
            : CognitiveMemoryRecallStatementPlanKind.Answer;
    }

    private static int ResolvePlanPriority(CognitiveMemoryRecallStatementPlanKind planKind)
        => planKind switch
        {
            CognitiveMemoryRecallStatementPlanKind.Conflict => 0,
            CognitiveMemoryRecallStatementPlanKind.Caveat => 1,
            CognitiveMemoryRecallStatementPlanKind.MissingEvidence => 2,
            CognitiveMemoryRecallStatementPlanKind.Action => 3,
            CognitiveMemoryRecallStatementPlanKind.Answer => 4,
            CognitiveMemoryRecallStatementPlanKind.ReferenceHint => 5,
            _ => 6
        };

    private static void AddOmittedWarnings(
        IReadOnlyList<StatementPlan> omittedPlans,
        int maxStatements,
        List<string> warnings)
    {
        var omittedDetailCount = omittedPlans.Sum(plan => Math.Max(1, plan.Candidates.Count));
        if (omittedDetailCount == 0)
        {
            return;
        }

        warnings.Add($"Omitted {omittedDetailCount} selected recall detail(s) because the statement budget is {maxStatements}.");
        var importantPlanKinds = omittedPlans
            .Where(plan => plan.PlanKind is CognitiveMemoryRecallStatementPlanKind.Caveat
                or CognitiveMemoryRecallStatementPlanKind.Conflict
                or CognitiveMemoryRecallStatementPlanKind.MissingEvidence)
            .Select(plan => plan.PlanKind)
            .Distinct()
            .Order()
            .ToArray();
        if (importantPlanKinds.Length > 0)
        {
            warnings.Add($"Omitted important {string.Join('/', importantPlanKinds.Select(FormatPlanKind))} recall detail(s); request a larger statement budget before relying on the brief.");
        }
    }

    private static string ResolvePlanPrefix(CognitiveMemoryRecallStatementPlanKind planKind)
        => planKind switch
        {
            CognitiveMemoryRecallStatementPlanKind.Action => "Action - ",
            CognitiveMemoryRecallStatementPlanKind.Caveat => "Caveat - ",
            CognitiveMemoryRecallStatementPlanKind.Conflict => "Conflict - ",
            CognitiveMemoryRecallStatementPlanKind.MissingEvidence => "Missing evidence - ",
            CognitiveMemoryRecallStatementPlanKind.ReferenceHint => "Reference hint - ",
            _ => "Answer - "
        };

    private static bool ShouldPrefixWithTopic(CognitiveMemoryRecallStatementPlanKind planKind)
        => planKind is CognitiveMemoryRecallStatementPlanKind.Answer
            or CognitiveMemoryRecallStatementPlanKind.Action
            or CognitiveMemoryRecallStatementPlanKind.Caveat
            or CognitiveMemoryRecallStatementPlanKind.Conflict;

    private static string FormatPlanKind(CognitiveMemoryRecallStatementPlanKind planKind)
        => planKind.ToString().Replace("Evidence", "-evidence", StringComparison.Ordinal).ToLowerInvariant();

    private static bool IsExplicitReferenceRequest(string requestText)
    {
        var normalized = requestText.Trim().ToLowerInvariant();
        return ContainsAny(normalized, ["debug", "provenance", "lineage", "citation", "citations"]) ||
               Regex.IsMatch(normalized, "\\b(show|include|with|resolve|open|inspect)\\s+(the\\s+)?(source\\s+)?references?\\b", RegexOptions.CultureInvariant);
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

    private static bool HasActionSignal(
        string text,
        CognitiveMemoryRecallIntentKind intent)
        => IsActionIntent(intent) || ContainsAny(text, ["use ", "notify ", "requires ", "must ", "should ", "verify ", "restore ", "assign ", "run "]);

    private static bool IsActionIntent(CognitiveMemoryRecallIntentKind intent)
        => intent is CognitiveMemoryRecallIntentKind.Implementation
            or CognitiveMemoryRecallIntentKind.Procedure
            or CognitiveMemoryRecallIntentKind.Debugging
            or CognitiveMemoryRecallIntentKind.Testing
            or CognitiveMemoryRecallIntentKind.Deployment;

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
               !normalized.StartsWith("diagnostic:", StringComparison.Ordinal) &&
               !normalized.StartsWith("diagnostics:", StringComparison.Ordinal) &&
               !normalized.StartsWith("internal:", StringComparison.Ordinal) &&
               !normalized.StartsWith("score:", StringComparison.Ordinal) &&
               !normalized.Contains("displaybeliefscore", StringComparison.Ordinal) &&
               !normalized.Contains("display rank", StringComparison.Ordinal) &&
               !normalized.Contains("internal score", StringComparison.Ordinal) &&
               !normalized.Contains("belief score", StringComparison.Ordinal);
    }

    private sealed record SectionCandidate(
        CognitiveMemoryRecallContextSection Section,
        string Text,
        int QueryOverlap,
        bool HasCaveatSignal,
        string ConflictKey,
        StatementConflictPolarity ConflictPolarity,
        CognitiveMemoryClaimId? AggregateClaimId,
        CognitiveMemoryRecallStatementPlanKind PlanKind);

    private sealed record StatementPlan(
        string Key,
        CognitiveMemoryRecallStatementPlanKind PlanKind,
        IReadOnlyList<SectionCandidate> Candidates,
        string? ExplicitText = null,
        IReadOnlyList<CognitiveMemoryRecallSourceRef>? SourceRefsOverride = null,
        IReadOnlyList<CognitiveMemoryClaimId>? AggregateClaimIdsOverride = null)
    {
        public int QueryOverlap => Candidates.Sum(candidate => candidate.QueryOverlap);

        public int IncludedSourceCount => (SourceRefsOverride ?? Candidates.SelectMany(candidate => candidate.Section.SourceRefs))
            .Count(sourceRef => sourceRef.IncludedInContext);
    }

    private enum StatementConflictPolarity
    {
        None = 0,
        Required = 1,
        Exception = 2
    }
}
