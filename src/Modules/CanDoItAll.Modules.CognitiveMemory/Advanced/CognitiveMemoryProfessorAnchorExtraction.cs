using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.CognitiveMemory;

public enum CognitiveMemoryProfessorAnchorCaptureKind
{
    TeachingAnswer = 0,
    Confirmation = 1,
    MisconceptionCorrection = 2,
    ScopeCorrection = 3,
    NewKnowledge = 4
}

public sealed record CognitiveMemoryProfessorAnchorClaim(
    string Text,
    CognitiveMemoryProfessorAnchorCaptureKind CaptureKind);

public sealed record CognitiveMemoryProfessorAnchorExtraction(
    CognitiveMemoryProfessorAnchorCaptureKind CaptureKind,
    IReadOnlyList<CognitiveMemoryProfessorAnchorClaim> Claims,
    string TargetScope,
    string MisconceptionCorrected,
    IReadOnlyList<string> SourceUtterances,
    double ConfidenceScore,
    string LanguageCode,
    IReadOnlyList<string> Examples,
    IReadOnlyList<string> Counterexamples);

public sealed record CognitiveMemoryProfessorTeachingExtractionRequest(
    string UserMessage,
    string CuratorResponse,
    IReadOnlyList<CognitiveMemoryCuratorTurnRecord> PreviousTurns,
    string? ExplicitCaptureScope,
    CognitiveMemoryCuratorCaptureKind? ExplicitCaptureKind = null);

public interface ICognitiveMemoryProfessorTeachingExtractor
{
    CognitiveMemoryProfessorAnchorExtraction? TryExtract(CognitiveMemoryProfessorTeachingExtractionRequest request);
}

public sealed record CognitiveMemoryProfessorTeachingSemanticClassification(
    bool IsProfessorTeaching,
    string LanguageCode,
    CognitiveMemoryProfessorAnchorCaptureKind? CaptureKind = null,
    double ConfidenceBoost = 0);

public interface ICognitiveMemoryProfessorTeachingSemanticClassifier
{
    CognitiveMemoryProfessorTeachingSemanticClassification? Classify(
        CognitiveMemoryProfessorTeachingExtractionRequest request);
}

internal sealed class CognitiveMemoryProfessorTeachingExtractor : ICognitiveMemoryProfessorTeachingExtractor
{
    public static CognitiveMemoryProfessorTeachingExtractor Instance { get; } = new();

    private readonly ICognitiveMemoryProfessorTeachingSemanticClassifier? semanticClassifier;

    private static readonly Regex SentenceSplitRegex = new(
        @"(?<=[.!?])\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex MisconceptionRegex = new(
        @"\bconfus(?:e|es|ed|ing)\s+(?<first>.+?)\s+with\s+(?<second>.+?)(?:[.;]|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly string[] TeachingSignals =
    [
        " in this project ",
        " for this project ",
        " because ",
        " therefore ",
        " distinction ",
        " confuse ",
        " must ",
        " only applies ",
        " applies only ",
        " source of truth ",
        " approval ",
        " example ",
        " counterexample ",
        " not ",
        " instead ",
        " plati ",
        " musi ",
        " vyzaduje ",
        " brana ",
        " schvaleni ",
        " vlastnik ",
        " vydani ",
        " priklad ",
        " protipriklad ",
        " nestaci ",
        " mylis ",
        " misto ",
        " pouze ",
        " docasne uceni profesora "
    ];

    private static readonly string[] QuestionLeadIns =
    [
        "what ",
        "why ",
        "how ",
        "when ",
        "where ",
        "can ",
        "could ",
        "should ",
        "do ",
        "does ",
        "is ",
        "are ",
        "co ",
        "proc ",
        "jak ",
        "kdy ",
        "kde ",
        "ma ",
        "musi ",
        "plati "
    ];

    public CognitiveMemoryProfessorTeachingExtractor(
        ICognitiveMemoryProfessorTeachingSemanticClassifier? semanticClassifier = null)
    {
        this.semanticClassifier = semanticClassifier;
    }

    public CognitiveMemoryProfessorAnchorExtraction? TryExtract(CognitiveMemoryProfessorTeachingExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userMessage = NormalizeText(request.UserMessage);
        var curatorResponse = NormalizeText(request.CuratorResponse);
        var userSearchText = NormalizeSearchText(userMessage);
        var curatorSearchText = NormalizeSearchText(curatorResponse);
        if (LooksLikeQuestionOnly(userSearchText) && !HasQuestionAnswerTeachingContext(request.PreviousTurns))
        {
            return null;
        }

        var semanticClassification = semanticClassifier?.Classify(request);
        var conversationText = string.Join(
            " ",
            request.PreviousTurns
                .SelectMany(turn => new[] { turn.UserMessage, turn.CuratorResponse })
                .Append(userMessage)
                .Append(curatorResponse)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(NormalizeText));
        var conversationSearchText = NormalizeSearchText(conversationText);
        var sourceUtterances = ResolveSourceUtterances(request.PreviousTurns, userMessage, curatorResponse);
        if (!HasProfessorTeachingIntent(request, conversationSearchText, sourceUtterances, semanticClassification))
        {
            return null;
        }

        var captureKind = semanticClassification?.CaptureKind ?? ResolveCaptureKind(conversationSearchText);
        var claims = ExtractClaims(userMessage, curatorResponse, sourceUtterances, captureKind);
        if (claims.Count == 0)
        {
            return null;
        }

        var targetScope = FirstNonEmpty(
            NormalizeText(request.ExplicitCaptureScope),
            ExtractTargetScope(userMessage),
            ExtractTargetScope(curatorResponse),
            ExtractTargetScope(sourceUtterances.LastOrDefault() ?? string.Empty));
        if (string.IsNullOrWhiteSpace(targetScope))
        {
            return null;
        }

        var misconception = ExtractMisconception(conversationText, conversationSearchText);
        var confidence = ResolveConfidence(userSearchText, curatorSearchText, misconception, semanticClassification?.ConfidenceBoost ?? 0);
        var examples = ExtractMarkedUtterances(sourceUtterances, counterexamples: false);
        var counterexamples = ExtractMarkedUtterances(sourceUtterances, counterexamples: true);
        var languageCode = FirstNonEmpty(semanticClassification?.LanguageCode, ResolveLanguageCode(conversationText, conversationSearchText));
        return new CognitiveMemoryProfessorAnchorExtraction(
            captureKind,
            claims,
            targetScope,
            misconception,
            sourceUtterances,
            confidence,
            languageCode,
            examples,
            counterexamples);
    }

    private static CognitiveMemoryProfessorAnchorCaptureKind ResolveCaptureKind(string conversationSearchText)
    {
        if (ContainsAny(conversationSearchText, [" wrong scope ", " only applies ", " applies only ", " different scope ", " spatny rozsah ", " pouze plati "]))
        {
            return CognitiveMemoryProfessorAnchorCaptureKind.ScopeCorrection;
        }

        if (ContainsAny(conversationSearchText, [" confuse ", " confused ", " confusing ", " distinction ", " not ", " instead ", " mylis ", " nestaci ", " misto ", " neni "]))
        {
            return CognitiveMemoryProfessorAnchorCaptureKind.MisconceptionCorrection;
        }

        if (ContainsAny(conversationSearchText, [" yes ", " correct ", " exactly ", " that is right ", " ano ", " spravne ", " presne "]))
        {
            return CognitiveMemoryProfessorAnchorCaptureKind.Confirmation;
        }

        return ContainsAny(conversationSearchText, [" must ", " requires ", " require ", " is a ", " is an ", " musi ", " vyzaduje ", " plati ", " je "])
            ? CognitiveMemoryProfessorAnchorCaptureKind.TeachingAnswer
            : CognitiveMemoryProfessorAnchorCaptureKind.NewKnowledge;
    }

    private static IReadOnlyList<CognitiveMemoryProfessorAnchorClaim> ExtractClaims(
        string userMessage,
        string curatorResponse,
        IReadOnlyList<string> sourceUtterances,
        CognitiveMemoryProfessorAnchorCaptureKind captureKind)
    {
        var claims = new List<CognitiveMemoryProfessorAnchorClaim>();
        foreach (var sentence in SplitSentences(string.Join(" ", sourceUtterances.Append(userMessage).Append(curatorResponse))))
        {
            var candidate = TrimTeachingPrefix(sentence);
            if (candidate.Length < 12 || LooksLikeQuestionOnly(candidate))
            {
                continue;
            }

            var candidateSearchText = $" {NormalizeSearchText(candidate)} ";
            if (!ContainsAny(candidateSearchText, [" must ", " need ", " needs ", " require ", " requires ", " is a ", " is an ", " are ", " means ", " gate ", " evidence ", " example ", " counterexample ", " not ", " musi ", " vyzaduje ", " plati ", " je ", " brana ", " dukaz ", " priklad ", " protipriklad ", " nestaci ", " podepise "]))
            {
                continue;
            }

            if (claims.Any(claim => claim.Text.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            claims.Add(new CognitiveMemoryProfessorAnchorClaim(candidate, captureKind));
            if (claims.Count == 3)
            {
                break;
            }
        }

        return claims;
    }

    private static IReadOnlyList<string> SplitSentences(string text)
        => SentenceSplitRegex.Split(text)
            .Select(NormalizeText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

    private static string ExtractTargetScope(string text)
    {
        var value = TrimTeachingPrefix(text);
        foreach (var prefix in new[] { "no: ", "no, ", "actually, ", "actually ", "ne: ", "ne, " })
        {
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = value[prefix.Length..].Trim();
                break;
            }
        }

        var becauseIndex = value.IndexOf(" because ", StringComparison.OrdinalIgnoreCase);
        if (becauseIndex > 0)
        {
            value = value[..becauseIndex].Trim();
        }

        var colonIndex = value.IndexOf(':', StringComparison.Ordinal);
        if (colonIndex > 0 && colonIndex <= 120)
        {
            return value[..colonIndex].Trim();
        }

        foreach (var separator in new[] { " is a ", " is an ", " requires ", " require ", " needs ", " need ", " must ", " platí ", " plati ", " vyžaduje ", " vyzaduje ", " musí ", " musi ", " je " })
        {
            var index = IndexOfSearch(value, separator);
            if (index > 0)
            {
                return value[..index].Trim();
            }
        }

        return value.Length <= 140
            ? value
            : value[..140].Trim();
    }

    private static string ExtractMisconception(string text, string searchText)
    {
        var match = MisconceptionRegex.Match(text);
        if (!match.Success)
        {
            return ContainsAny(searchText, [" distinction ", " instead ", " not ", " mylis ", " nestaci ", " misto ", " neni "])
                ? "The professor guidance corrected a distinction in the current conversation."
                : string.Empty;
        }

        return NormalizeText($"{match.Groups["first"].Value} with {match.Groups["second"].Value}");
    }

    private static double ResolveConfidence(
        string userSearchText,
        string curatorSearchText,
        string misconception,
        double semanticBoost)
    {
        var score = 0.62 + semanticBoost;
        if (ContainsAny(userSearchText, [" in this project ", " for this project ", " v tomto projektu ", " u nasazeni "]))
        {
            score += 0.1;
        }

        if (ContainsAny(userSearchText, [" because ", " requires ", " must ", " gate ", " vyzaduje ", " musi ", " brana ", " plati "]))
        {
            score += 0.1;
        }

        if (!string.IsNullOrWhiteSpace(curatorSearchText) && ContainsAny(curatorSearchText, [" distinction ", " matters ", " gate ", " evidence ", " uceni profesora ", " zachovam "]))
        {
            score += 0.07;
        }

        if (!string.IsNullOrWhiteSpace(misconception))
        {
            score += 0.04;
        }

        return Math.Clamp(score, 0.6, 0.92);
    }

    private static bool HasTeachingSignal(string text)
        => ContainsAny($" {NormalizeSearchText(text)} ", TeachingSignals);

    private static bool HasProfessorTeachingIntent(
        CognitiveMemoryProfessorTeachingExtractionRequest request,
        string conversationSearchText,
        IReadOnlyList<string> sourceUtterances,
        CognitiveMemoryProfessorTeachingSemanticClassification? semanticClassification)
    {
        if (semanticClassification?.IsProfessorTeaching == true)
        {
            return true;
        }

        if (HasQuestionAnswerTeachingContext(request.PreviousTurns) &&
            ContainsAny($" {NormalizeSearchText(request.UserMessage)} ", [" no:", " no,", " not ", " instead ", " gate ", " ne:", " ne,", " misto ", " brana "]))
        {
            return true;
        }

        if (request.ExplicitCaptureKind == CognitiveMemoryCuratorCaptureKind.NewKnowledge &&
            ContainsAny(conversationSearchText, ["approval", "gate", "source of truth", " only ", "before traffic", "before launch", "schvaleni", "brana", "pred navratem provozu", "pred provozem"]))
        {
            return true;
        }

        return HasTeachingSignal(conversationSearchText) &&
               (ContainsAny($" {conversationSearchText} ", [" confuse ", " distinction ", " because ", " counterexample ", " example ", " wrong scope ", " only ", " protipriklad ", " priklad ", " mylis ", " nestaci ", " plati ", " brana "]) ||
                sourceUtterances.Any(utterance => LooksLikeQuestionOnly(utterance)));
    }

    private static bool HasQuestionAnswerTeachingContext(IReadOnlyList<CognitiveMemoryCuratorTurnRecord> previousTurns)
        => previousTurns.Any(turn =>
            LooksLikeQuestionOnly(NormalizeSearchText(turn.UserMessage)) &&
            HasTeachingSignal(turn.CuratorResponse));

    private static IReadOnlyList<string> ResolveSourceUtterances(
        IReadOnlyList<CognitiveMemoryCuratorTurnRecord> previousTurns,
        string userMessage,
        string curatorResponse)
    {
        var values = previousTurns
            .Where(turn => LooksLikeQuestionOnly(NormalizeSearchText(turn.UserMessage)) || HasTeachingSignal(turn.CuratorResponse))
            .SelectMany(turn => new[] { NormalizeText(turn.UserMessage), NormalizeText(turn.CuratorResponse) })
            .Concat([userMessage, curatorResponse])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .TakeLast(6)
            .ToArray();
        return values.Length == 0 ? [userMessage, curatorResponse] : values;
    }

    private static bool LooksLikeQuestionOnly(string text)
    {
        var value = text.TrimStart();
        if (!value.EndsWith("?", StringComparison.Ordinal))
        {
            return false;
        }

        return QuestionLeadIns.Any(prefix => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string TrimTeachingPrefix(string value)
    {
        var result = NormalizeText(value);
        foreach (var prefix in new[]
        {
            "in this project, ",
            "for this project, ",
            "in our project, ",
            "for our project, ",
            "v tomto projektu, ",
            "pro tento projekt, "
        })
        {
            if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return result[prefix.Length..].Trim();
            }
        }

        return result;
    }

    private static IReadOnlyList<string> ExtractMarkedUtterances(
        IReadOnlyList<string> sourceUtterances,
        bool counterexamples)
    {
        return sourceUtterances
            .SelectMany(SplitSentences)
            .Where(sentence =>
            {
                var searchText = $" {NormalizeSearchText(sentence)} ";
                var isCounterexample = ContainsAny(searchText, [" counterexample ", " protipriklad "]);
                return counterexamples
                    ? isCounterexample
                    : !isCounterexample && ContainsAny(searchText, [" example ", " priklad "]);
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(4)
            .ToArray();
    }

    private static string ResolveLanguageCode(string text, string searchText)
    {
        if (ContainsAny(searchText, [" schvaleni ", " priklad ", " protipriklad ", " plati ", " vyzaduje ", " nestaci ", " mylis ", " vlastnik vydani "]) ||
            text.Any(character => character is 'á' or 'č' or 'ď' or 'é' or 'ě' or 'í' or 'ň' or 'ó' or 'ř' or 'š' or 'ť' or 'ú' or 'ů' or 'ý' or 'ž' or 'Á' or 'Č' or 'Ď' or 'É' or 'Ě' or 'Í' or 'Ň' or 'Ó' or 'Ř' or 'Š' or 'Ť' or 'Ú' or 'Ů' or 'Ý' or 'Ž'))
        {
            return "cs";
        }

        return ContainsAny(searchText, TeachingSignals)
            ? "en"
            : "und";
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> candidates)
    {
        return candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));
    }

    private static int IndexOfSearch(string value, string searchValue)
    {
        return value.IndexOf(searchValue, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), @"\s+", " ");

    private static string NormalizeSearchText(string? value)
    {
        var normalized = NormalizeText(value).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ");
    }

}
