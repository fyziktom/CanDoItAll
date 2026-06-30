using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;

namespace CanDoItAll.Modules.CognitiveMemory;

internal static class CognitiveMemoryConsolidationFactExtractor
{
    private const int MaxFacts = 8;
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex EmailRegex = new(
        @"[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PhoneRegex = new(
        @"(?:\+\d{1,3}\s*)?(?:\d[\s.-]?){7,}\d",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly IReadOnlyList<DimensionRule> DimensionRules =
    [
        new("business-plan", ["business plan", "podnikatelsk", "podnikatelsky plan", "executive summary", "swot", "business model", "obchodni model"]),
        new("product", ["product", "produkt", "service", "sluzba", "solution", "riesenie", "tlacitko", "modul", "kabel", "piezo", "firmware", "release", "m5stack"]),
        new("market-and-marketing", ["marketing", "market", "trh", "customer", "zakaznik", "sales", "predaj", "prodej", "campaign", "kampan", "nemocnic", "domov", "senior", "konkurenc", "ministerstvo"]),
        new("finance-and-expenses", ["finance", "financial", "cash flow", "expense", "cost", "naklad", "salary", "payroll", "mzda", "mzd", "plat", "rozpocet", "budget", "revenue", "obrat", "trz", "vynos", "prijem", "cena", "faktura"]),
        new("staffing", ["staff", "employee", "team", "hire", "recruit", "zamestnan", "pracovnik", "pracovnici", "personal", "tym", "nabor", "fte", "kapacit"]),
        new("operations-and-procurement", ["operation", "procurement", "supplier", "purchase", "install", "equipment", "license", "dodavatel", "nakup", "objednavka", "nabidka", "material", "komponent", "vyroba", "montaz", "instalac"]),
        new("risk-and-validation", ["risk", "rizik", "assumption", "pilot", "validation", "approval", "compliance", "certifik", "testov", "overen", "schvalen"]),
        new("milestones", ["stage", "phase", "milestone", "timeline", "harmonogram", "deadline", "launch", "faze", "etapa", "termin"])
    ];

    private static readonly IReadOnlyList<string> ProcedureTerms =
    [
        "procedure",
        "runbook",
        "step",
        "stage",
        "phase",
        "install",
        "configure",
        "prepare",
        "approve"
    ];

    private static readonly IReadOnlyList<string> DecisionTerms =
    [
        "decision",
        "approved",
        "selected",
        "chosen",
        "must",
        "shall"
    ];

    public static string CreateSummary(string title, string contentText, int maxCharacters)
    {
        var lines = ReadLines(contentText);
        var facts = ExtractFactLines(contentText);
        var dimensions = ResolvePlanningDimensions(contentText);
        if (facts.Count == 0 && IsSensitiveOrContactHeavy(lines))
        {
            return string.Empty;
        }

        if (facts.Count == 0 && dimensions.Count == 0)
        {
            return TrimForPayload(contentText.Trim(), maxCharacters);
        }

        var builder = new StringBuilder();
        builder.Append("Source title: ");
        builder.AppendLine(NormalizeLine(title));
        if (dimensions.Count > 0)
        {
            builder.Append("Detected planning dimensions: ");
            builder.AppendLine(string.Join(", ", dimensions));
        }

        if (facts.Count > 0)
        {
            builder.AppendLine("Extracted source-backed facts:");
            foreach (var fact in facts)
            {
                builder.Append("- ");
                builder.AppendLine(fact);
            }
        }

        return TrimForPayload(builder.ToString().Trim(), maxCharacters);
    }

    public static string CreateReason(
        string sourceItemKey,
        CognitiveMemoryConsolidationCandidateKind candidateKind,
        string contentText)
    {
        var dimensions = ResolvePlanningDimensions(contentText);
        var dimensionText = dimensions.Count == 0
            ? "no reusable planning dimensions detected"
            : $"detected dimensions: {string.Join(", ", dimensions)}";
        return $"Consolidation classified source item '{sourceItemKey}' as {candidateKind} from source-backed facts; {dimensionText}.";
    }

    public static bool LooksLikeProcedure(string contentText)
        => ProcedureTerms.Any(term => ContainsTerm(contentText, term)) ||
            ReadLines(contentText).Count(line => line.StartsWith("step ", StringComparison.OrdinalIgnoreCase) ||
                                                 line.StartsWith("phase ", StringComparison.OrdinalIgnoreCase)) >= 2;

    public static bool LooksLikeDecision(string contentText)
        => DecisionTerms.Any(term => ContainsTerm(contentText, term));

    public static IReadOnlyList<string> ResolvePlanningDimensions(string contentText)
    {
        var normalizedContent = NormalizeForMatching(contentText);
        return DimensionRules
            .Where(rule => rule.Terms.Any(term => normalizedContent.Contains(NormalizeForMatching(term), StringComparison.Ordinal)))
            .Select(rule => rule.Name)
            .ToArray();
    }

    private static IReadOnlyList<string> ExtractFactLines(string contentText)
    {
        var dimensions = ResolvePlanningDimensions(contentText);
        var selected = ReadLines(contentText)
            .Select(NormalizeLine)
            .Where(line => line.Length >= 16)
            .Where(line => !IsSensitiveContactLine(line))
            .Where(line => dimensions.Count == 0 || DimensionRules.Any(rule =>
                dimensions.Contains(rule.Name, StringComparer.Ordinal) &&
                rule.Terms.Any(term => ContainsTerm(line, term))))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(MaxFacts)
            .ToList();

        if (selected.Count > 0)
        {
            return selected;
        }

        return ReadLines(contentText)
            .Select(NormalizeLine)
            .Where(line => line.Length >= 16)
            .Where(line => !IsSensitiveContactLine(line))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(Math.Min(MaxFacts, 3))
            .ToArray();
    }

    private static IReadOnlyList<string> ReadLines(string contentText)
        => contentText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("•", "\n", StringComparison.Ordinal)
            .Replace("▪", "\n", StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizeLine(string value)
    {
        var text = value.Trim().TrimStart('-', '*', '#', ' ', '\t').Trim();
        return WhitespaceRegex.Replace(text, " ");
    }

    private static string TrimForPayload(string value, int maxCharacters)
        => value.Length <= maxCharacters ? value : value[..maxCharacters];

    private static bool IsSensitiveOrContactHeavy(IReadOnlyList<string> lines)
    {
        if (lines.Count == 0)
        {
            return false;
        }

        var sensitiveLineCount = lines.Count(line => IsSensitiveContactLine(NormalizeLine(line)));
        return sensitiveLineCount > 0 && sensitiveLineCount * 2 >= lines.Count;
    }

    private static bool IsSensitiveContactLine(string line)
        => EmailRegex.IsMatch(line) ||
           PhoneRegex.IsMatch(line) ||
           ContainsTerm(line, "tel:") ||
           ContainsTerm(line, "telefon") ||
           ContainsTerm(line, "kontakt") ||
           ContainsTerm(line, "kontaktní osoba") ||
           ContainsTerm(line, "kontaktni osoba") ||
           ContainsTerm(line, "dic") ||
           ContainsTerm(line, "ico") ||
           ContainsTerm(line, "bankovni ucet");

    private static bool ContainsTerm(string value, string term)
        => NormalizeForMatching(value).Contains(NormalizeForMatching(term), StringComparison.Ordinal);

    private static string NormalizeForMatching(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(character));
            }
        }

        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    private sealed record DimensionRule(string Name, IReadOnlyList<string> Terms);
}
