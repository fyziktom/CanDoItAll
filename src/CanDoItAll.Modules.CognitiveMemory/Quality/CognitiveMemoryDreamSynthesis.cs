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
            .Select(NormalizeClaim)
            .Where(claim => !string.IsNullOrWhiteSpace(claim))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (claims.Length == 0)
        {
            return string.Empty;
        }

        if (claims.Length == 1)
        {
            return claims[0];
        }

        var commonPrefix = ResolveCommonPrefix(claims);
        if (!string.IsNullOrWhiteSpace(commonPrefix))
        {
            var tails = claims
                .Select(claim => RemovePrefix(claim, commonPrefix))
                .Where(tail => !string.IsNullOrWhiteSpace(tail))
                .ToArray();
            if (tails.Length >= 2)
            {
                return CognitiveMemoryQualityText.TrimText($"{commonPrefix} {JoinPhrases(tails)}", 1200);
            }
        }

        return CognitiveMemoryQualityText.TrimText(JoinPhrases(claims), 1200);
    }

    private static string NormalizeClaim(string claim)
    {
        var normalized = WhitespaceRegex().Replace(claim.Trim().TrimEnd('.'), " ");
        return CognitiveMemoryQualityText.TrimText(normalized, 1200);
    }

    private static string ResolveCommonPrefix(IReadOnlyList<string> claims)
    {
        var tokenized = claims
            .Select(claim => claim.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(tokens => tokens.Length > 0)
            .ToArray();
        if (tokenized.Length < 2)
        {
            return string.Empty;
        }

        var prefix = new List<string>();
        for (var index = 0; index < tokenized.Min(tokens => tokens.Length); index++)
        {
            var candidate = tokenized[0][index];
            if (tokenized.Any(tokens => !string.Equals(tokens[index], candidate, StringComparison.OrdinalIgnoreCase)))
            {
                break;
            }

            prefix.Add(candidate);
        }

        return prefix.Count < 2
            ? string.Empty
            : string.Join(' ', prefix);
    }

    private static string RemovePrefix(string claim, string prefix)
        => claim.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? claim[prefix.Length..].Trim()
            : claim;

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

    private static string LowercaseFirst(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : $"{char.ToLowerInvariant(value[0])}{value[1..]}";

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}

public sealed class CognitiveMemoryDreamEntailmentValidator : ICognitiveMemoryDreamEntailmentValidator
{
    public static readonly CognitiveMemoryDreamEntailmentValidator Instance = new();

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

        var sourceTokens = BuildTextSignature(sourceMaterial);
        var overlap = claimTokens.Count(sourceTokens.Contains);
        var requiredOverlap = claimTokens.Count <= 4
            ? Math.Min(2, claimTokens.Count)
            : Math.Min(5, Math.Max(3, claimTokens.Count / 3));
        return overlap >= requiredOverlap
            ? new CognitiveMemoryDreamEntailmentResult(true, $"Lexical entailment passed with {overlap} overlapping signal(s).")
            : new CognitiveMemoryDreamEntailmentResult(false, $"Only {overlap} of {requiredOverlap} required signal(s) were supported.");
    }

    public static HashSet<string> BuildTextSignature(string text)
        => CognitiveMemoryQualityText.ExtractMeaningfulTokens(text, 40).ToHashSet(StringComparer.Ordinal);

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
}
