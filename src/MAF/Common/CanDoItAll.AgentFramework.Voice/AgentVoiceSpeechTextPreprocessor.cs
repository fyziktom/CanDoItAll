using System.Text.RegularExpressions;

namespace CanDoItAll.AgentFramework.Voice;

public sealed partial class AgentVoiceSpeechTextPreprocessor : IAgentVoiceSpeechTextPreprocessor
{
    public const string IdentifierOmissionNotice =
        "During speech I skipped saying exact IDs, but you can find them in my text response.";

    private const string IdentifierOnlyFallback =
        "The response contains exact IDs that are available in the text response.";

    public AgentVoiceSpeechTextPreparationResult Prepare(
        string text,
        bool suppressIdentifierOmissionNotice)
    {
        ArgumentNullException.ThrowIfNull(text);

        var removedIdentifierCount = 0;
        var spokenText = FullGuidPattern().Replace(
            text.Trim(),
            _ =>
            {
                removedIdentifierCount++;
                return string.Empty;
            });
        spokenText = TruncatedHexIdentifierPattern().Replace(
            spokenText,
            _ =>
            {
                removedIdentifierCount++;
                return string.Empty;
            });

        spokenText = CleanupIdentifierGaps(spokenText);
        var identifiersOmitted = removedIdentifierCount > 0;
        var noticeIncluded = identifiersOmitted && !suppressIdentifierOmissionNotice;

        if (noticeIncluded)
        {
            spokenText = string.IsNullOrWhiteSpace(spokenText)
                ? IdentifierOmissionNotice
                : $"{IdentifierOmissionNotice} {spokenText}";
        }
        else if (identifiersOmitted && string.IsNullOrWhiteSpace(spokenText))
        {
            spokenText = IdentifierOnlyFallback;
        }

        return new AgentVoiceSpeechTextPreparationResult(
            spokenText,
            identifiersOmitted,
            noticeIncluded,
            removedIdentifierCount);
    }

    private static string CleanupIdentifierGaps(string text)
    {
        var cleaned = EmptyBracketsPattern().Replace(text, string.Empty);
        cleaned = SpaceBeforePunctuationPattern().Replace(cleaned, "$1");
        cleaned = DuplicateSeparatorPattern().Replace(cleaned, "$1");
        cleaned = SeparatorBeforeClosingPattern().Replace(cleaned, "$1");
        cleaned = SeparatorAfterOpeningPattern().Replace(cleaned, "$1");
        cleaned = DanglingBulletSeparatorPattern().Replace(cleaned, "$1");
        cleaned = WhitespacePattern().Replace(cleaned, " ");
        return cleaned.Trim(' ', ',', ';', ':', '-', '\t', '\r', '\n');
    }

    [GeneratedRegex(@"\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b", RegexOptions.CultureInvariant)]
    private static partial Regex FullGuidPattern();

    [GeneratedRegex(@"\b[0-9a-fA-F]{7,32}(?:\.\.\.|\u2026)", RegexOptions.CultureInvariant)]
    private static partial Regex TruncatedHexIdentifierPattern();

    [GeneratedRegex(@"[\(\[]\s*[\)\]]", RegexOptions.CultureInvariant)]
    private static partial Regex EmptyBracketsPattern();

    [GeneratedRegex(@"\s+([,.;:)\]])", RegexOptions.CultureInvariant)]
    private static partial Regex SpaceBeforePunctuationPattern();

    [GeneratedRegex(@"[,;:](\s*[,;:])+", RegexOptions.CultureInvariant)]
    private static partial Regex DuplicateSeparatorPattern();

    [GeneratedRegex(@"[,;:]\s*([)\]])", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorBeforeClosingPattern();

    [GeneratedRegex(@"([(\[])\s*[,;:]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex SeparatorAfterOpeningPattern();

    [GeneratedRegex(@"(^|\s)(?:-|\u2013)\s*[,;:]\s*", RegexOptions.CultureInvariant)]
    private static partial Regex DanglingBulletSeparatorPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespacePattern();
}
