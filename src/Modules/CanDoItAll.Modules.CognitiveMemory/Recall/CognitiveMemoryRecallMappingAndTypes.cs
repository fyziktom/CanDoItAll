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

        return redactionState is CognitiveMemoryRedactionState.Redacted ||
            redactionState == CognitiveMemoryRedactionState.Restricted && !policyContext.AllowRestrictedContent
            ? CognitiveMemoryRecallExclusionReasonKind.RedactedSource
            : CognitiveMemoryRecallExclusionReasonKind.None;
    }

    private static bool CanIncludeSourceRef(
        CognitiveMemoryAccessLevel accessLevel,
        CognitiveMemoryRedactionState redactionState,
        CognitiveMemoryPolicyContext policyContext)
    {
        if (!PolicyCanRead(accessLevel, policyContext))
        {
            return false;
        }

        return redactionState switch
        {
            CognitiveMemoryRedactionState.Safe or CognitiveMemoryRedactionState.Unclassified => true,
            CognitiveMemoryRedactionState.Restricted => policyContext.AllowRestrictedContent,
            _ => false
        };
    }

    private static double ResolveRedactionPressure(
        MemoryRecordSnapshot record,
        CognitiveMemoryPolicyContext policyContext)
        => record.AccessLevel == CognitiveMemoryAccessLevel.Restricted && !policyContext.AllowRestrictedContent ? 0.7 : 0;

    private static string BuildSourceRefSummary(
        string sourceLinkSummary,
        SourceItemSnapshot? sourceItem)
    {
        if (sourceItem is not null && !string.IsNullOrWhiteSpace(sourceItem.ContentText))
        {
            var content = sourceItem.ContentText.Trim();
            return RedactRecallContextText(content.Length <= 2000 ? content : content[..2000]);
        }

        if (!string.IsNullOrWhiteSpace(sourceLinkSummary))
        {
            return RedactRecallContextText(sourceLinkSummary);
        }

        return RedactRecallContextText(sourceItem?.Title ?? string.Empty);
    }

    private static IReadOnlyList<string> NormalizeTerms(string query)
    {
        var terms = query
            .Split([' ', '\t', '\r', '\n', '.', ',', ';', ':', '?', '!', '/', '\\', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(term => term.Length >= 2)
            .Select(term => term.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var expandedTerms = terms
            .SelectMany(ExpandTermVariants)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var meaningfulTerms = expandedTerms
            .Where(term => !LexicalStopWords.Contains(term))
            .Take(LexicalTermLimit)
            .ToArray();
        return meaningfulTerms.Length == 0
            ? expandedTerms.Take(LexicalTermLimit).ToArray()
            : meaningfulTerms;
    }

    private static IEnumerable<string> ExpandTermVariants(string term)
    {
        yield return term;
        if (LexicalTermAliases.TryGetValue(term, out var aliases))
        {
            foreach (var alias in aliases)
            {
                yield return alias;
            }
        }

        if (term.Length > 4 && term.EndsWith("ies", StringComparison.Ordinal))
        {
            yield return $"{term[..^3]}y";
            yield break;
        }

        if (term.Length > 3 && term.EndsWith("s", StringComparison.Ordinal) && !term.EndsWith("ss", StringComparison.Ordinal))
        {
            yield return term[..^1];
        }
    }

    private static IReadOnlyList<CognitiveMemoryRecordKind> NormalizePreferredKinds(IReadOnlyList<CognitiveMemoryRecordKind>? preferredKinds)
        => preferredKinds?
            .Distinct()
            .ToArray() ?? [];

    private static double ComputeLexicalMatch(
        MemoryRecordSnapshot record,
        IReadOnlyList<string> queryTerms)
        => ComputeLexicalMatch($"{record.Title} {record.SummaryText} {record.CanonicalText} {record.TopicKey}", queryTerms);

    private static double ComputeLexicalMatch(
        string haystack,
        IReadOnlyList<string> queryTerms)
    {
        if (queryTerms.Count == 0)
        {
            return 0;
        }

        var normalizedHaystack = haystack.ToLowerInvariant();
        var totalWeight = 0d;
        var hitWeight = 0d;
        foreach (var term in queryTerms)
        {
            var weight = ResolveLexicalTermWeight(term);
            totalWeight += weight;
            if (normalizedHaystack.Contains(term, StringComparison.Ordinal))
            {
                hitWeight += weight;
            }
        }

        return totalWeight == 0
            ? 0
            : Math.Clamp(hitWeight / totalWeight, 0, 1);
    }

    private static double ResolveLexicalTermWeight(string term)
        => term.Length switch
        {
            <= 2 => 0.25,
            3 => 0.5,
            >= 10 => 1.5,
            >= 7 => 1.25,
            _ => 1
        };

    private static double ResolveLexicalMatch(
        MemoryRecordSnapshot record,
        IReadOnlyList<string> queryTerms,
        IReadOnlyDictionary<Guid, double> sourceLexicalScores)
        => Math.Max(
            ComputeLexicalMatch(record, queryTerms),
            sourceLexicalScores.GetValueOrDefault(record.Id));

    private static double ResolveSourceGraphProximity(int depth)
        => depth switch
        {
            <= 1 => 0.78,
            2 => 0.72,
            _ => 0.65
        };

    private static string ResolveDocumentLocator(string? locator)
    {
        if (string.IsNullOrWhiteSpace(locator))
        {
            return string.Empty;
        }

        var hashIndex = locator.IndexOf('#', StringComparison.Ordinal);
        return hashIndex < 0 ? locator.Trim() : locator[..hashIndex].Trim();
    }

    private static double ResolveFocusOrderingPriority(EvaluatedRecallCandidate candidate, string preferredScopeKey)
    {
        var lexical = GetScoreComponent(candidate, CognitiveMemoryScoreDimensionKind.LexicalMatch) ?? 0;
        var semantic = GetScoreComponent(candidate, CognitiveMemoryScoreDimensionKind.SemanticSimilarity) ?? 0;
        var graph = GetScoreComponent(candidate, CognitiveMemoryScoreDimensionKind.GraphProximity) ?? 0;
        var workspace = GetScoreComponent(candidate, CognitiveMemoryScoreDimensionKind.WorkspaceFocusFit) ?? 0;
        var memoryActivation = GetScoreComponent(candidate, CognitiveMemoryScoreDimensionKind.MemoryActivation) ?? 0;
        var directChannelBonus = ResolveDirectChannelOrderingBonus(candidate.ChannelKinds);
        var specificity = ResolveFocusSpecificity(candidate.Record);
        var sourceScopeFit = ResolveSourceScopeFit(candidate.SourceScopeKeys, preferredScopeKey);
        return directChannelBonus +
               lexical * 3 +
               semantic * 0.25 +
               graph * 0.35 +
               workspace * 0.6 +
               memoryActivation * 0.35 +
               specificity +
               sourceScopeFit;
    }

    private static double ResolveSourceScopeFit(IReadOnlyList<string> candidateScopeKeys, string preferredScopeKey)
    {
        if (string.IsNullOrWhiteSpace(preferredScopeKey) || candidateScopeKeys.Count == 0)
        {
            return 0;
        }

        return candidateScopeKeys.Contains(preferredScopeKey, StringComparer.Ordinal)
            ? 1.35
            : -0.75;
    }

    private static double ResolveFocusSpecificity(MemoryRecordSnapshot record)
    {
        var text = $"{record.Title} {record.SummaryText} {record.CanonicalText}";
        var score = 0d;
        if (text.Contains("Structural parent node derived from", StringComparison.OrdinalIgnoreCase))
        {
            score -= 0.55;
        }

        if (text.Contains("Object type: ProjectRoot", StringComparison.OrdinalIgnoreCase))
        {
            score -= 0.7;
        }

        if (text.Contains("Source truth S", StringComparison.OrdinalIgnoreCase) &&
            text.Contains("level 2", StringComparison.OrdinalIgnoreCase))
        {
            score -= 0.25;
        }

        if (record.Title.Contains(".md - ", StringComparison.OrdinalIgnoreCase) &&
            !IsStageHeaderTitle(record.Title))
        {
            score += 0.35;
        }

        if (text.Contains("\n-", StringComparison.Ordinal) ||
            text.Contains("\r\n-", StringComparison.Ordinal))
        {
            score += 0.2;
        }

        if (text.Any(char.IsDigit))
        {
            score += 0.12;
        }

        return score;
    }

    private static bool IsStageHeaderTitle(string title)
        => title.Contains(".md - S0", StringComparison.OrdinalIgnoreCase) ||
           title.StartsWith("S0", StringComparison.OrdinalIgnoreCase);

    private static string ResolvePreferredSourceScopeKey(CognitiveMemoryRecallRequest request)
    {
        if (request.Metadata is null ||
            !request.Metadata.TryGetValue("stageId", out var stageId))
        {
            return string.Empty;
        }

        return ExtractSourceScopeKeys(stageId).FirstOrDefault() ?? string.Empty;
    }

    private static IReadOnlyList<string> ExtractSourceScopeKeys(params string?[] values)
    {
        var scopeKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var normalized = value.ToLowerInvariant();
            for (var index = 0; index <= normalized.Length - 3; index++)
            {
                if (normalized[index] != 's' ||
                    !char.IsAsciiDigit(normalized[index + 1]) ||
                    !char.IsAsciiDigit(normalized[index + 2]) ||
                    !IsSourceScopeBoundary(normalized, index - 1) ||
                    !IsSourceScopeBoundary(normalized, index + 3))
                {
                    continue;
                }

                scopeKeys.Add(normalized.Substring(index, 3));
            }
        }

        return scopeKeys.ToArray();
    }

    private static bool IsSourceScopeBoundary(string value, int index)
        => index < 0 ||
           index >= value.Length ||
           !char.IsLetterOrDigit(value[index]);

    private static double ResolveDirectChannelOrderingBonus(IReadOnlyList<CognitiveMemoryRecallChannelKind> channelKinds)
    {
        var bonus = 0d;
        foreach (var channelKind in channelKinds)
        {
            bonus = Math.Max(
                bonus,
                channelKind switch
                {
                    CognitiveMemoryRecallChannelKind.VectorProjection => 0.45,
                    CognitiveMemoryRecallChannelKind.Workspace => 0.4,
                    CognitiveMemoryRecallChannelKind.SignalActivation => 0.35,
                    CognitiveMemoryRecallChannelKind.Lexical => 0.25,
                    _ => 0
                });
        }

        return bonus;
    }

    private static double? GetScoreComponent(
        EvaluatedRecallCandidate candidate,
        CognitiveMemoryScoreDimensionKind dimensionKind)
        => candidate.ScoreTrace.InputVectors
            .SelectMany(vector => vector.Components)
            .Where(component => component.DimensionKind == dimensionKind)
            .Select(component => (double?)component.NormalizedValue)
            .FirstOrDefault();

    private static bool IsSourceGraphExpansionSeed(RecallCandidateAccumulator candidate)
        => candidate.SemanticSimilarity is >= 0.55 ||
           candidate.LexicalMatch is >= 0.35 ||
           candidate.WorkspaceFocusFit is >= 0.55 ||
           candidate.MemoryActivation is >= 0.55;

    private static bool CanUseAsSourceGraphFrontier(SourceGraphItemSnapshot item)
    {
        if (item.SourceSystem == ExternalFileSourceSystem)
        {
            return !string.IsNullOrWhiteSpace(item.Locator);
        }

        if (item.SourceSystem != WorkbenchProjectStructureSourceSystem ||
            item.SourceItemType != ProjectNodeSourceItemType)
        {
            return true;
        }

        var node = TryReadProjectStructureNode(item.ProvenanceJson);
        return node is not null && !string.IsNullOrWhiteSpace(node.ParentId);
    }

    private static ProjectStructureNodeSourceSnapshot? TryReadProjectStructureNode(string provenanceJson)
    {
        if (string.IsNullOrWhiteSpace(provenanceJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(provenanceJson);
            var root = document.RootElement;
            var sourceEntityId = root.TryGetProperty("sourceEntityId", out var entityProperty)
                ? entityProperty.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(sourceEntityId))
            {
                return null;
            }

            var parentId = root.TryGetProperty("metadata", out var metadataProperty) &&
                           metadataProperty.ValueKind == JsonValueKind.Object &&
                           metadataProperty.TryGetProperty("parentId", out var parentProperty)
                ? parentProperty.GetString() ?? string.Empty
                : string.Empty;
            return new ProjectStructureNodeSourceSnapshot(sourceEntityId, parentId);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

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

}
