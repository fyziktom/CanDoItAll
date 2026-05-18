using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Voice;

public static class AgentVoiceConfirmationClassifier
{
    private static readonly string[] RejectPhrases =
    [
        "do not store",
        "dont store",
        "don't store",
        "do not save",
        "dont save",
        "don't save",
        "cancel",
        "stop",
        "no",
        "nope",
        "wrong"
    ];

    private static readonly string[] AffirmPhrases =
    [
        "this is good store it",
        "that is good store it",
        "store it",
        "save it",
        "confirm",
        "yes",
        "yep",
        "ok",
        "okay",
        "looks good",
        "this is good",
        "that is good"
    ];

    public static AgentVoiceConfirmationIntent Classify(string? transcript)
    {
        var normalized = Normalize(transcript);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return AgentVoiceConfirmationIntent.Unknown;
        }

        if (RejectPhrases.Any(phrase => ContainsPhrase(normalized, phrase)))
        {
            return AgentVoiceConfirmationIntent.Reject;
        }

        return AffirmPhrases.Any(phrase => ContainsPhrase(normalized, phrase))
            ? AgentVoiceConfirmationIntent.Affirm
            : AgentVoiceConfirmationIntent.Unknown;
    }

    private static bool ContainsPhrase(string normalized, string phrase)
    {
        var normalizedPhrase = Normalize(phrase);
        var padded = $" {normalized} ";
        return normalized == normalizedPhrase ||
               padded.Contains($" {normalizedPhrase} ", StringComparison.Ordinal);
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var characters = value
            .ToLowerInvariant()
            .Select(character => char.IsLetterOrDigit(character) ? character : ' ')
            .ToArray();

        return string.Join(' ', new string(characters).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}
