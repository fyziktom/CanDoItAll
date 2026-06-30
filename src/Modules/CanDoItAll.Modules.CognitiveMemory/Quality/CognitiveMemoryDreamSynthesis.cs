using System.Globalization;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.CognitiveMemory;

public sealed record CognitiveMemoryDreamClaimSynthesisRequest(
    CognitiveMemoryConsolidationMode Mode,
    IReadOnlyList<string> SourceClaims);

public sealed record CognitiveMemoryDreamEntailmentRequest(
    string ClaimText,
    IReadOnlyList<string> SourceTexts);

public sealed record CognitiveMemoryDreamEntailmentResult(
    bool Supported,
    string Reason);

internal sealed record CognitiveMemoryDreamClaimSlots(
    CognitiveMemoryClaimKind ClaimKind,
    string SubjectKey,
    string PredicateKey,
    string ObjectKey,
    string ConditionKey,
    string ScopeKey,
    string ConditionText,
    string CaveatText);

public interface ICognitiveMemoryDreamClaimSynthesizer
{
    string Synthesize(CognitiveMemoryDreamClaimSynthesisRequest request);
}

public interface ICognitiveMemoryDreamEntailmentValidator
{
    CognitiveMemoryDreamEntailmentResult Validate(CognitiveMemoryDreamEntailmentRequest request);
}

public sealed partial class CognitiveMemoryDreamClaimSynthesizer : ICognitiveMemoryDreamClaimSynthesizer
{
    public static readonly CognitiveMemoryDreamClaimSynthesizer Instance = new();

    private CognitiveMemoryDreamClaimSynthesizer()
    {
    }

    public string Synthesize(CognitiveMemoryDreamClaimSynthesisRequest request)
    {
        var claims = request.SourceClaims
            .Select(NormalizeClaimSourceMapText)
            .Where(claim => !string.IsNullOrWhiteSpace(claim))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (claims.Length == 0)
        {
            return string.Empty;
        }

        var slots = claims
            .Select(claim => CognitiveMemoryDreamClaimSlotExtractor.Extract(claim, ResolveDefaultClaimKind(request.Mode)))
            .ToArray();
        var conclusion = BuildConclusion(slots, claims);
        var support = BuildSupport(claims);
        var condition = BuildCondition(slots);
        var caveat = BuildCaveat(slots);

        return CognitiveMemoryQualityText.TrimText(
            $"Claim: {conclusion}{Environment.NewLine}Evidence: {support}{Environment.NewLine}Condition: {condition}{Environment.NewLine}Caveat: {caveat}",
            1200);
    }

    private static string NormalizeClaim(string claim)
    {
        var normalized = WhitespaceRegex().Replace(claim.Trim().TrimEnd('.'), " ");
        return CognitiveMemoryQualityText.TrimText(normalized, 1200);
    }

    internal static string NormalizeClaimSourceMapText(string claim)
        => NormalizeClaim(claim);

    private static string BuildConclusion(
        IReadOnlyList<CognitiveMemoryDreamClaimSlots> slots,
        IReadOnlyList<string> claims)
    {
        var domainClaims = claims
            .Select(NormalizeClaim)
            .Where(claim => !string.IsNullOrWhiteSpace(claim))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (domainClaims.Length > 0)
        {
            return EnsureSentence(JoinPhrases(domainClaims));
        }

        var subjectLabels = slots
            .Select(slot => FormatKey(slot.SubjectKey))
            .Where(subject => !string.IsNullOrWhiteSpace(subject))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return EnsureSentence(JoinPhrases(subjectLabels));
    }

    private static string BuildSupport(IReadOnlyList<string> claims)
        => EnsureSentence(JoinPhrases(claims));

    private static string BuildCondition(IReadOnlyList<CognitiveMemoryDreamClaimSlots> slots)
    {
        var conditions = slots
            .Select(slot => slot.ConditionText)
            .Where(condition => !string.IsNullOrWhiteSpace(condition))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return conditions.Length == 0
            ? "No explicit condition was detected."
            : EnsureSentence(JoinPhrases(conditions));
    }

    private static string BuildCaveat(IReadOnlyList<CognitiveMemoryDreamClaimSlots> slots)
    {
        var caveats = slots
            .Select(slot => slot.CaveatText)
            .Where(caveat => !string.IsNullOrWhiteSpace(caveat))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return caveats.Length == 0
            ? "No explicit caveat was detected."
            : EnsureSentence(JoinPhrases(caveats));
    }

    private static string JoinPhrases(IReadOnlyList<string> phrases)
    {
        var distinct = phrases
            .Select(phrase => phrase.Trim().TrimEnd('.'))
            .Where(phrase => !string.IsNullOrWhiteSpace(phrase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return distinct.Length switch
        {
            0 => string.Empty,
            1 => distinct[0],
            2 => $"{distinct[0]} and {LowercaseFirst(distinct[1])}",
            _ => $"{string.Join(", ", distinct.Take(distinct.Length - 1))}, and {LowercaseFirst(distinct[^1])}"
        };
    }

    private static CognitiveMemoryClaimKind ResolveDefaultClaimKind(CognitiveMemoryConsolidationMode mode)
        => mode switch
        {
            CognitiveMemoryConsolidationMode.ProcedureMining => CognitiveMemoryClaimKind.ProcedureConstraint,
            CognitiveMemoryConsolidationMode.FailureLearning => CognitiveMemoryClaimKind.FailureMode,
            _ => CognitiveMemoryClaimKind.Fact
        };

    private static string FormatKey(string key)
        => key.Replace('.', ' ').Replace('-', ' ');

    private static string EnsureSentence(string value)
    {
        var trimmed = value.Trim();
        return trimmed.EndsWith(".", StringComparison.Ordinal) ||
               trimmed.EndsWith("?", StringComparison.Ordinal) ||
               trimmed.EndsWith("!", StringComparison.Ordinal)
            ? trimmed
            : $"{trimmed}.";
    }

    private static string LowercaseFirst(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"{char.ToLowerInvariant(value[0])}{value[1..]}";

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

internal static partial class CognitiveMemoryDreamClaimSlotExtractor
{
    private static readonly IReadOnlySet<string> Operators = new HashSet<string>([
        "applies",
        "assigns",
        "captures",
        "confirms",
        "includes",
        "logs",
        "records",
        "requires",
        "stages",
        "uses",
        "validates",
        "verifies",
        "waits"
    ], StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> ModalOperators = new HashSet<string>([
        "can",
        "may",
        "must",
        "should"
    ], StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlyList<string> ConditionMarkers = [
        " only after ",
        " only when ",
        " before ",
        " after ",
        " during ",
        " when ",
        " if "
    ];

    public static CognitiveMemoryDreamClaimSlots Extract(
        string claimText,
        CognitiveMemoryClaimKind claimKind,
        string? subjectKey = null,
        string? predicateKey = null,
        string? objectKey = null)
    {
        var normalizedClaim = NormalizeClaimText(claimText);
        var predicate = FirstNonEmpty(NormalizeExternalKey(predicateKey), InferPredicateKey(normalizedClaim));
        var subject = FirstNonEmpty(NormalizeExternalKey(subjectKey), InferSubjectKey(normalizedClaim, predicate));
        var obj = FirstNonEmpty(NormalizeExternalKey(objectKey), InferObjectKey(normalizedClaim, predicate));
        var conditionText = ExtractConditionText(normalizedClaim);
        var caveatText = ExtractCaveatText(normalizedClaim, conditionText);
        var conditionKey = NormalizeOptionalKey(conditionText, "none");
        return new CognitiveMemoryDreamClaimSlots(
            claimKind,
            subject,
            predicate,
            obj,
            conditionKey,
            conditionKey,
            conditionText,
            caveatText);
    }

    private static string NormalizeClaimText(string claimText)
        => WhitespaceRegex().Replace(claimText.Trim().TrimEnd('.'), " ");

    private static string InferSubjectKey(string claimText, string predicateKey)
    {
        var subjectMaterial = claimText;
        var commaIndex = subjectMaterial.IndexOf(',', StringComparison.Ordinal);
        if (commaIndex >= 0 && subjectMaterial.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
        {
            subjectMaterial = subjectMaterial[(commaIndex + 1)..].Trim();
        }

        var operatorIndex = IndexOfOperator(subjectMaterial);
        if (operatorIndex > 0)
        {
            subjectMaterial = subjectMaterial[..operatorIndex];
        }
        else if (!string.IsNullOrWhiteSpace(predicateKey))
        {
            var predicateIndex = subjectMaterial.IndexOf(predicateKey.Replace('.', ' '), StringComparison.OrdinalIgnoreCase);
            if (predicateIndex > 0)
            {
                subjectMaterial = subjectMaterial[..predicateIndex];
            }
        }

        return NormalizeOptionalKey(TakeMeaningfulTokens(subjectMaterial, 4), "unknown.subject");
    }

    private static string InferPredicateKey(string claimText)
    {
        var tokens = Tokenize(claimText);
        var predicate = tokens.FirstOrDefault(token => Operators.Contains(token)) ??
                        tokens.FirstOrDefault(token => ModalOperators.Contains(token));
        return NormalizeOptionalKey(predicate ?? "states", "states");
    }

    private static string InferObjectKey(string claimText, string predicateKey)
    {
        var subjectTrimmed = claimText;
        var operatorIndex = IndexOfOperator(subjectTrimmed);
        if (operatorIndex >= 0)
        {
            subjectTrimmed = subjectTrimmed[operatorIndex..];
            var firstSpace = subjectTrimmed.IndexOf(' ', StringComparison.Ordinal);
            subjectTrimmed = firstSpace >= 0 ? subjectTrimmed[(firstSpace + 1)..] : string.Empty;
        }

        foreach (var marker in ConditionMarkers)
        {
            var markerIndex = subjectTrimmed.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex >= 0)
            {
                subjectTrimmed = subjectTrimmed[..markerIndex];
                break;
            }
        }

        return NormalizeOptionalKey(TakeMeaningfulTokens(subjectTrimmed, 6), "object");
    }

    private static string ExtractConditionText(string claimText)
    {
        if (claimText.StartsWith("if ", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = claimText.IndexOf(',', StringComparison.Ordinal);
            if (commaIndex > 0)
            {
                return claimText[..commaIndex].Trim();
            }
        }

        foreach (var marker in ConditionMarkers)
        {
            var markerIndex = claimText.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (markerIndex < 0)
            {
                continue;
            }

            var condition = claimText[markerIndex..].Trim();
            return CognitiveMemoryQualityText.TrimText(condition, 180);
        }

        return string.Empty;
    }

    private static string ExtractCaveatText(string claimText, string conditionText)
    {
        var lowered = claimText.ToLowerInvariant();
        if (lowered.Contains(" fail", StringComparison.Ordinal) ||
            lowered.Contains("without", StringComparison.Ordinal) ||
            lowered.Contains("except", StringComparison.Ordinal) ||
            lowered.Contains("unless", StringComparison.Ordinal) ||
            lowered.Contains(" only ", StringComparison.Ordinal))
        {
            return CognitiveMemoryQualityText.TrimText(FirstNonEmpty(conditionText, claimText), 220);
        }

        return string.Empty;
    }

    private static int IndexOfOperator(string text)
    {
        var matches = OperatorRegex().Matches(text);
        return matches.Count == 0 ? -1 : matches[0].Index;
    }

    private static IReadOnlyList<string> Tokenize(string text)
        => Regex.Split(text.ToLowerInvariant(), "[^\\p{L}\\p{Nd}]+")
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToArray();

    private static string TakeMeaningfulTokens(string text, int maxTokens)
        => string.Join('.', CognitiveMemoryQualityText.ExtractMeaningfulTokens(text, maxTokens));

    private static string NormalizeExternalKey(string? key)
        => string.IsNullOrWhiteSpace(key) ? string.Empty : CognitiveMemoryQualityText.NormalizeKey(key);

    private static string NormalizeOptionalKey(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : CognitiveMemoryQualityText.NormalizeKey(value);

    private static string FirstNonEmpty(params string[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    [GeneratedRegex("\\b(applies|assigns|captures|can|confirms|includes|logs|may|must|records|requires|should|stages|uses|validates|verifies|waits)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OperatorRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

public sealed partial class CognitiveMemoryDreamEntailmentValidator : ICognitiveMemoryDreamEntailmentValidator
{
    public static readonly CognitiveMemoryDreamEntailmentValidator Instance = new();

    private static readonly IReadOnlyDictionary<string, string> PredicateAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["applies"] = "apply",
        ["apply"] = "apply",
        ["approve"] = "approve",
        ["approves"] = "approve",
        ["assign"] = "assign",
        ["assigns"] = "assign",
        ["capture"] = "capture",
        ["captures"] = "capture",
        ["confirm"] = "confirm",
        ["confirms"] = "confirm",
        ["deploy"] = "deploy",
        ["deploys"] = "deploy",
        ["include"] = "include",
        ["includes"] = "include",
        ["log"] = "log",
        ["logs"] = "log",
        ["record"] = "record",
        ["records"] = "record",
        ["require"] = "require",
        ["required"] = "require",
        ["requires"] = "require",
        ["review"] = "review",
        ["reviews"] = "review",
        ["run"] = "run",
        ["runs"] = "run",
        ["stage"] = "stage",
        ["stages"] = "stage",
        ["use"] = "use",
        ["uses"] = "use",
        ["validate"] = "validate",
        ["validates"] = "validate",
        ["verify"] = "verify",
        ["verifies"] = "verify",
        ["wait"] = "wait",
        ["waits"] = "wait"
    };

    private static readonly IReadOnlySet<string> IgnoredNumberUnits = new HashSet<string>([
        "claim",
        "claims",
        "memory",
        "memories",
        "observation",
        "observations",
        "source",
        "sources"
    ], StringComparer.OrdinalIgnoreCase);

    private static readonly IReadOnlySet<string> ScopeTokens = new HashSet<string>([
        "local",
        "production",
        "prod",
        "simulation",
        "test",
        "validation"
    ], StringComparer.OrdinalIgnoreCase);

    private CognitiveMemoryDreamEntailmentValidator()
    {
    }

    public CognitiveMemoryDreamEntailmentResult Validate(CognitiveMemoryDreamEntailmentRequest request)
    {
        var claimTokens = BuildTextSignature(request.ClaimText);
        if (claimTokens.Count == 0)
        {
            return new CognitiveMemoryDreamEntailmentResult(false, "Claim has no meaningful lexical content.");
        }

        var sourceMaterial = string.Join(' ', request.SourceTexts);
        if (HasBypassConflict(request.ClaimText, sourceMaterial))
        {
            return new CognitiveMemoryDreamEntailmentResult(false, "Claim reverses a source requirement or approval constraint.");
        }

        var claimProfile = CognitiveMemoryDreamEntailmentProfile.Create(request.ClaimText);
        var sourceProfile = CognitiveMemoryDreamEntailmentProfile.Create(sourceMaterial);
        var semanticBlocker = FindSemanticBlocker(claimProfile, sourceProfile);
        if (!string.IsNullOrWhiteSpace(semanticBlocker))
        {
            return new CognitiveMemoryDreamEntailmentResult(false, semanticBlocker);
        }

        var sourceTokens = BuildTextSignature(sourceMaterial);
        var overlap = claimTokens.Count(sourceTokens.Contains);
        var requiredOverlap = claimTokens.Count <= 4
            ? Math.Min(2, claimTokens.Count)
            : Math.Min(5, Math.Max(3, claimTokens.Count / 3));
        return overlap >= requiredOverlap
            ? new CognitiveMemoryDreamEntailmentResult(true, $"Lexical entailment passed with {overlap} overlapping signal(s).")
            : new CognitiveMemoryDreamEntailmentResult(false, $"Only {overlap} of {requiredOverlap} required signal(s) were supported.");
    }

    public static int CountSemanticOperatorSignals(string text)
        => CognitiveMemoryDreamEntailmentProfile.Create(text).RiskSignalCount;

    public static HashSet<string> BuildTextSignature(string text)
        => CognitiveMemoryQualityText.ExtractMeaningfulTokens(text, 40).ToHashSet(StringComparer.Ordinal);

    private static string FindSemanticBlocker(
        CognitiveMemoryDreamEntailmentProfile claim,
        CognitiveMemoryDreamEntailmentProfile source)
    {
        foreach (var measurement in claim.Measurements)
        {
            var sameUnit = source.Measurements.Where(sourceMeasurement => string.Equals(sourceMeasurement.UnitKey, measurement.UnitKey, StringComparison.Ordinal)).ToArray();
            if (sameUnit.Length > 0 && sameUnit.All(sourceMeasurement => sourceMeasurement.Value != measurement.Value))
            {
                return $"Numeric value '{measurement.DisplayText}' is contradicted by source value(s) {string.Join(", ", sameUnit.Select(value => value.DisplayText))}.";
            }

            if (sameUnit.Length == 0 && source.Measurements.Count > 0)
            {
                return $"Numeric value '{measurement.DisplayText}' is not supported by mapped source value(s).";
            }
        }

        foreach (var relation in claim.TemporalRelations)
        {
            var sameAnchor = source.TemporalRelations.Where(sourceRelation => sourceRelation.HasSameAnchor(relation)).ToArray();
            if (sameAnchor.Any(sourceRelation => sourceRelation.IsOppositeOf(relation)))
            {
                return $"Temporal relation '{relation.Marker} {relation.AnchorKey}' contradicts mapped source order.";
            }

            if (sameAnchor.Length > 0 && sameAnchor.All(sourceRelation => !sourceRelation.Matches(relation)))
            {
                return $"Temporal relation '{relation.Marker} {relation.AnchorKey}' is not supported by mapped source order.";
            }
        }

        if (claim.Modality != CognitiveMemoryDreamEntailmentModality.Neutral &&
            source.Modality != CognitiveMemoryDreamEntailmentModality.Neutral &&
            claim.Modality != source.Modality)
        {
            return $"Modality '{claim.Modality}' is contradicted by source modality '{source.Modality}'.";
        }

        if (claim.ConditionPolarity != CognitiveMemoryDreamConditionPolarity.Neutral &&
            source.ConditionPolarity != CognitiveMemoryDreamConditionPolarity.Neutral &&
            claim.ConditionPolarity != source.ConditionPolarity)
        {
            return $"Conditional polarity '{claim.ConditionPolarity}' is contradicted by source condition '{source.ConditionPolarity}'.";
        }

        if (source.OnlyScopes.Count > 0 && claim.ScopeTargets.Count > 0 && !claim.ScopeTargets.Any(source.OnlyScopes.Contains))
        {
            return $"Scope target '{string.Join(".", claim.ScopeTargets.Order(StringComparer.Ordinal))}' exceeds source-only scope '{string.Join(".", source.OnlyScopes.Order(StringComparer.Ordinal))}'.";
        }

        foreach (var claimRole in claim.Roles)
        {
            var sourceRoles = source.Roles.Where(sourceRole => string.Equals(sourceRole.PredicateKey, claimRole.PredicateKey, StringComparison.Ordinal)).ToArray();
            if (sourceRoles.Length == 0)
            {
                continue;
            }

            if (sourceRoles.Any(sourceRole => sourceRole.Matches(claimRole)))
            {
                continue;
            }

            if (sourceRoles.Any(sourceRole => sourceRole.HasMeaningfulRoleConflict(claimRole)))
            {
                return $"Actor/action role '{claimRole.SubjectKey}.{claimRole.PredicateKey}.{claimRole.ObjectKey}' is contradicted by mapped source role(s).";
            }
        }

        return string.Empty;
    }

    private static bool HasBypassConflict(string claimText, string sourceMaterial)
    {
        var claim = claimText.ToLowerInvariant();
        var source = sourceMaterial.ToLowerInvariant();
        var claimBypassesRequirement = ContainsAny(claim, [
            "can skip",
            "skip ",
            "without ",
            "bypass",
            "does not require",
            "not require",
            "no approval"
        ]);
        if (!claimBypassesRequirement)
        {
            return false;
        }

        return claim.Contains("approval", StringComparison.Ordinal) &&
               ContainsAny(source, [
                   "requires",
                   "must",
                   "approval before",
                   "approval during",
                   "records manager approval",
                   "requires manager approval"
               ]);
    }

    private static bool ContainsAny(string text, IReadOnlyList<string> values)
        => values.Any(value => text.Contains(value, StringComparison.Ordinal));

    private sealed record CognitiveMemoryDreamEntailmentProfile(
        IReadOnlyList<CognitiveMemoryDreamMeasurement> Measurements,
        IReadOnlyList<CognitiveMemoryDreamTemporalRelation> TemporalRelations,
        IReadOnlyList<CognitiveMemoryDreamSemanticRole> Roles,
        CognitiveMemoryDreamEntailmentModality Modality,
        CognitiveMemoryDreamConditionPolarity ConditionPolarity,
        IReadOnlySet<string> OnlyScopes,
        IReadOnlySet<string> ScopeTargets)
    {
        public int RiskSignalCount =>
            Measurements.Count +
            TemporalRelations.Count +
            Roles.Count +
            (Modality == CognitiveMemoryDreamEntailmentModality.Neutral ? 0 : 1) +
            (ConditionPolarity == CognitiveMemoryDreamConditionPolarity.Neutral ? 0 : 1) +
            OnlyScopes.Count +
            ScopeTargets.Count;

        public static CognitiveMemoryDreamEntailmentProfile Create(string text)
        {
            var normalized = NormalizeSemanticText(text);
            return new CognitiveMemoryDreamEntailmentProfile(
                ExtractMeasurements(normalized),
                ExtractTemporalRelations(normalized),
                ExtractRoles(normalized),
                ExtractModality(normalized),
                ExtractConditionPolarity(normalized),
                ExtractOnlyScopes(normalized),
                ExtractScopeTargets(normalized));
        }
    }

    private sealed record CognitiveMemoryDreamMeasurement(
        decimal Value,
        string UnitKey,
        string DisplayText);

    private sealed record CognitiveMemoryDreamTemporalRelation(
        string Marker,
        string AnchorKey)
    {
        public bool HasSameAnchor(CognitiveMemoryDreamTemporalRelation other)
            => string.Equals(AnchorKey, other.AnchorKey, StringComparison.Ordinal);

        public bool Matches(CognitiveMemoryDreamTemporalRelation other)
            => string.Equals(Marker, other.Marker, StringComparison.Ordinal) &&
               HasSameAnchor(other);

        public bool IsOppositeOf(CognitiveMemoryDreamTemporalRelation other)
            => HasSameAnchor(other) &&
               ((Marker == "before" && other.Marker == "after") ||
                (Marker == "after" && other.Marker == "before"));
    }

    private sealed record CognitiveMemoryDreamSemanticRole(
        string PredicateKey,
        string SubjectKey,
        string ObjectKey)
    {
        public bool Matches(CognitiveMemoryDreamSemanticRole other)
            => string.Equals(SubjectKey, other.SubjectKey, StringComparison.Ordinal) &&
               string.Equals(ObjectKey, other.ObjectKey, StringComparison.Ordinal);

        public bool HasMeaningfulRoleConflict(CognitiveMemoryDreamSemanticRole other)
        {
            if (string.IsNullOrWhiteSpace(SubjectKey) ||
                string.IsNullOrWhiteSpace(ObjectKey) ||
                string.IsNullOrWhiteSpace(other.SubjectKey) ||
                string.IsNullOrWhiteSpace(other.ObjectKey))
            {
                return false;
            }

            return !Matches(other);
        }
    }

    private enum CognitiveMemoryDreamEntailmentModality
    {
        Neutral,
        Required,
        Optional,
        Prohibited,
        Permissive
    }

    private enum CognitiveMemoryDreamConditionPolarity
    {
        Neutral,
        Pass,
        Fail
    }

    private static IReadOnlyList<CognitiveMemoryDreamMeasurement> ExtractMeasurements(string text)
        => NumberMeasurementRegex()
            .Matches(text)
            .Select(match =>
            {
                var unit = NormalizeKey(match.Groups["unit"].Value);
                return new CognitiveMemoryDreamMeasurement(
                    decimal.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture),
                    unit,
                    string.IsNullOrWhiteSpace(unit) ? match.Groups["value"].Value : $"{match.Groups["value"].Value} {unit}");
            })
            .Where(measurement => string.IsNullOrWhiteSpace(measurement.UnitKey) || !IgnoredNumberUnits.Contains(measurement.UnitKey))
            .Distinct()
            .ToArray();

    private static IReadOnlyList<CognitiveMemoryDreamTemporalRelation> ExtractTemporalRelations(string text)
        => TemporalRelationRegex()
            .Matches(text)
            .Select(match => new CognitiveMemoryDreamTemporalRelation(
                match.Groups["marker"].Value.ToLowerInvariant(),
                NormalizeKey(TakeMeaningfulTokens(match.Groups["anchor"].Value, 5))))
            .Where(relation => !string.IsNullOrWhiteSpace(relation.AnchorKey))
            .Distinct()
            .ToArray();

    private static IReadOnlyList<CognitiveMemoryDreamSemanticRole> ExtractRoles(string text)
    {
        var roles = new List<CognitiveMemoryDreamSemanticRole>();
        foreach (Match match in PredicateRegex().Matches(text))
        {
            var predicate = PredicateAliases.GetValueOrDefault(match.Groups["predicate"].Value, match.Groups["predicate"].Value);
            var subjectMaterial = text[..match.Index];
            var objectMaterial = text[(match.Index + match.Length)..];
            var punctuationBoundary = Math.Max(subjectMaterial.LastIndexOf(".", StringComparison.Ordinal), subjectMaterial.LastIndexOf(":", StringComparison.Ordinal));
            var conjunctionBoundary = subjectMaterial.LastIndexOf(" and ", StringComparison.OrdinalIgnoreCase);
            var subjectStartIndex = Math.Max(
                punctuationBoundary >= 0 ? punctuationBoundary + 1 : 0,
                conjunctionBoundary >= 0 ? conjunctionBoundary + " and ".Length : 0);
            if (subjectStartIndex > 0)
            {
                subjectMaterial = subjectMaterial[subjectStartIndex..];
            }

            var objectBoundary = objectMaterial.IndexOfAny(['.', ';', ':']);
            if (objectBoundary >= 0)
            {
                objectMaterial = objectMaterial[..objectBoundary];
            }

            foreach (var marker in new[] { " before ", " after ", " during ", " when ", " if ", " only when ", " only after ", " only before " })
            {
                var markerIndex = objectMaterial.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (markerIndex >= 0)
                {
                    objectMaterial = objectMaterial[..markerIndex];
                    break;
                }
            }

            var subject = NormalizeKey(TakeMeaningfulTokens(subjectMaterial, 4));
            var obj = NormalizeKey(TakeMeaningfulTokens(objectMaterial, 6));
            if (!string.IsNullOrWhiteSpace(subject) && !string.IsNullOrWhiteSpace(obj))
            {
                roles.Add(new CognitiveMemoryDreamSemanticRole(predicate, subject, obj));
            }
        }

        return roles
            .Distinct()
            .ToArray();
    }

    private static CognitiveMemoryDreamEntailmentModality ExtractModality(string text)
    {
        if (ContainsAny(text, ["must not", "cannot", "can not", "not allowed", "forbidden", "prohibited"]))
        {
            return CognitiveMemoryDreamEntailmentModality.Prohibited;
        }

        if (ContainsAny(text, ["required", "requires", "require ", "must ", "shall ", "only when", "only after"]))
        {
            return CognitiveMemoryDreamEntailmentModality.Required;
        }

        if (ContainsAny(text, ["optional", "may ", "can skip", "does not require", "not require"]))
        {
            return CognitiveMemoryDreamEntailmentModality.Optional;
        }

        if (ContainsAny(text, ["can ", "allowed", "permits", "permit "]))
        {
            return CognitiveMemoryDreamEntailmentModality.Permissive;
        }

        return CognitiveMemoryDreamEntailmentModality.Neutral;
    }

    private static CognitiveMemoryDreamConditionPolarity ExtractConditionPolarity(string text)
    {
        var conditionalMaterial = ExtractConditionalMaterial(text);
        if (string.IsNullOrWhiteSpace(conditionalMaterial))
        {
            return CognitiveMemoryDreamConditionPolarity.Neutral;
        }

        if (ContainsAny(conditionalMaterial, ["fail", "fails", "failed", "failure", "error", "broken"]))
        {
            return CognitiveMemoryDreamConditionPolarity.Fail;
        }

        if (ContainsAny(conditionalMaterial, ["pass", "passes", "passed", "success", "successful", "green"]))
        {
            return CognitiveMemoryDreamConditionPolarity.Pass;
        }

        return CognitiveMemoryDreamConditionPolarity.Neutral;
    }

    private static IReadOnlySet<string> ExtractOnlyScopes(string text)
        => ScopeOnlyRegex()
            .Matches(text)
            .SelectMany(match => CognitiveMemoryQualityText.ExtractMeaningfulTokens(match.Groups["scope"].Value, 5))
            .Where(ScopeTokens.Contains)
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> ExtractScopeTargets(string text)
        => CognitiveMemoryQualityText.ExtractMeaningfulTokens(text, 80)
            .Where(ScopeTokens.Contains)
            .ToHashSet(StringComparer.Ordinal);

    private static string ExtractConditionalMaterial(string text)
    {
        var match = ConditionRegex().Match(text);
        return match.Success ? match.Groups["condition"].Value : string.Empty;
    }

    private static string NormalizeSemanticText(string text)
        => WhitespaceRegex().Replace(text.Trim().TrimEnd('.').ToLowerInvariant(), " ");

    private static string TakeMeaningfulTokens(string text, int maxTokens)
        => string.Join('.', CognitiveMemoryQualityText.ExtractMeaningfulTokens(text, maxTokens));

    private static string NormalizeKey(string text)
        => CognitiveMemoryQualityText.NormalizeKey(text);

    [GeneratedRegex("\\b(?<value>\\d+(?:\\.\\d+)?)\\s*(?<unit>[a-z][a-z-]*)?", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NumberMeasurementRegex();

    [GeneratedRegex("\\b(?<marker>before|after|during)\\s+(?<anchor>[^.;,]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TemporalRelationRegex();

    [GeneratedRegex("\\b(?<predicate>applies|apply|approve|approves|assign|assigns|capture|captures|confirm|confirms|deploy|deploys|include|includes|log|logs|record|records|require|required|requires|review|reviews|run|runs|stage|stages|use|uses|validate|validates|verify|verifies|wait|waits)\\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PredicateRegex();

    [GeneratedRegex("\\b(?:if|when|only when|unless)\\s+(?<condition>[^.;,]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConditionRegex();

    [GeneratedRegex("\\bonly\\s+(?:to|for|in|within|during)\\s+(?<scope>[^.;,]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ScopeOnlyRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
