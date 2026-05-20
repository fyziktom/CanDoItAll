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
    double ConfidenceScore);

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

internal sealed class CognitiveMemoryProfessorTeachingExtractor : ICognitiveMemoryProfessorTeachingExtractor
{
    public static CognitiveMemoryProfessorTeachingExtractor Instance { get; } = new();

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
        " instead "
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
        "are "
    ];

    public CognitiveMemoryProfessorAnchorExtraction? TryExtract(CognitiveMemoryProfessorTeachingExtractionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var userMessage = NormalizeText(request.UserMessage);
        var curatorResponse = NormalizeText(request.CuratorResponse);
        if (LooksLikeQuestionOnly(userMessage) && !HasQuestionAnswerTeachingContext(request.PreviousTurns))
        {
            return null;
        }

        var conversationText = string.Join(
            " ",
            request.PreviousTurns
                .SelectMany(turn => new[] { turn.UserMessage, turn.CuratorResponse })
                .Append(userMessage)
                .Append(curatorResponse)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(NormalizeText));
        var sourceUtterances = ResolveSourceUtterances(request.PreviousTurns, userMessage, curatorResponse);
        if (!HasProfessorTeachingIntent(request, conversationText, sourceUtterances))
        {
            return null;
        }

        var captureKind = ResolveCaptureKind(conversationText);
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

        var misconception = ExtractMisconception(conversationText);
        var confidence = ResolveConfidence(userMessage, curatorResponse, misconception);
        return new CognitiveMemoryProfessorAnchorExtraction(
            captureKind,
            claims,
            targetScope,
            misconception,
            sourceUtterances,
            confidence);
    }

    private static CognitiveMemoryProfessorAnchorCaptureKind ResolveCaptureKind(string conversationText)
    {
        if (ContainsAny(conversationText, [" wrong scope ", " only applies ", " applies only ", " different scope "]))
        {
            return CognitiveMemoryProfessorAnchorCaptureKind.ScopeCorrection;
        }

        if (ContainsAny(conversationText, [" confuse ", " confused ", " confusing ", " distinction ", " not ", " instead "]))
        {
            return CognitiveMemoryProfessorAnchorCaptureKind.MisconceptionCorrection;
        }

        if (ContainsAny(conversationText, [" yes ", " correct ", " exactly ", " that is right "]))
        {
            return CognitiveMemoryProfessorAnchorCaptureKind.Confirmation;
        }

        return ContainsAny(conversationText, [" must ", " requires ", " require ", " is a ", " is an "])
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

            if (!ContainsAny($" {candidate} ", [" must ", " need ", " needs ", " require ", " requires ", " is a ", " is an ", " are ", " means ", " gate ", " evidence ", " example ", " counterexample ", " not "]))
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
        foreach (var prefix in new[] { "no: ", "no, ", "actually, ", "actually " })
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

        foreach (var separator in new[] { " is a ", " is an ", " requires ", " require ", " needs ", " need ", " must " })
        {
            var index = value.IndexOf(separator, StringComparison.OrdinalIgnoreCase);
            if (index > 0)
            {
                return value[..index].Trim();
            }
        }

        return value.Length <= 140
            ? value
            : value[..140].Trim();
    }

    private static string ExtractMisconception(string text)
    {
        var match = MisconceptionRegex.Match(text);
        if (!match.Success)
        {
            return ContainsAny(text, [" distinction ", " instead ", " not "])
                ? "The professor guidance corrected a distinction in the current conversation."
                : string.Empty;
        }

        return NormalizeText($"{match.Groups["first"].Value} with {match.Groups["second"].Value}");
    }

    private static double ResolveConfidence(string userMessage, string curatorResponse, string misconception)
    {
        var score = 0.62;
        if (ContainsAny(userMessage, [" in this project ", " for this project "]))
        {
            score += 0.1;
        }

        if (ContainsAny(userMessage, [" because ", " requires ", " must ", " gate "]))
        {
            score += 0.1;
        }

        if (!string.IsNullOrWhiteSpace(curatorResponse) && ContainsAny(curatorResponse, [" distinction ", " matters ", " gate ", " evidence "]))
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
        => ContainsAny($" {text} ", TeachingSignals);

    private static bool HasProfessorTeachingIntent(
        CognitiveMemoryProfessorTeachingExtractionRequest request,
        string conversationText,
        IReadOnlyList<string> sourceUtterances)
    {
        if (HasQuestionAnswerTeachingContext(request.PreviousTurns) &&
            ContainsAny($" {request.UserMessage} ", [" no:", " no,", " not ", " instead ", " gate "]))
        {
            return true;
        }

        if (request.ExplicitCaptureKind == CognitiveMemoryCuratorCaptureKind.NewKnowledge &&
            ContainsAny(conversationText, ["approval", "gate", "source of truth", " only ", "before traffic", "before launch"]))
        {
            return true;
        }

        return HasTeachingSignal(conversationText) &&
               (ContainsAny($" {conversationText} ", [" confuse ", " distinction ", " because ", " counterexample ", " example ", " wrong scope ", " only "]) ||
                sourceUtterances.Any(utterance => LooksLikeQuestionOnly(utterance)));
    }

    private static bool HasQuestionAnswerTeachingContext(IReadOnlyList<CognitiveMemoryCuratorTurnRecord> previousTurns)
        => previousTurns.Any(turn =>
            LooksLikeQuestionOnly(NormalizeText(turn.UserMessage)) &&
            HasTeachingSignal(NormalizeText(turn.CuratorResponse)));

    private static IReadOnlyList<string> ResolveSourceUtterances(
        IReadOnlyList<CognitiveMemoryCuratorTurnRecord> previousTurns,
        string userMessage,
        string curatorResponse)
    {
        var values = previousTurns
            .Where(turn => LooksLikeQuestionOnly(NormalizeText(turn.UserMessage)) || HasTeachingSignal(NormalizeText(turn.CuratorResponse)))
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
            "for our project, "
        })
        {
            if (result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return result[prefix.Length..].Trim();
            }
        }

        return result;
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> candidates)
        => candidates.Any(candidate => value.Contains(candidate, StringComparison.OrdinalIgnoreCase));

    private static string FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;

    private static string NormalizeText(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Regex.Replace(value.Trim(), @"\s+", " ");
}
