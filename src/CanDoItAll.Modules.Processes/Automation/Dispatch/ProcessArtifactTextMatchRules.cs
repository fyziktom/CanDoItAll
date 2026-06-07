using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactTextMatchRules
{
    private static readonly HashSet<string> ArtifactTitleNoiseTokens = new(StringComparer.Ordinal)
    {
        "artifact",
        "artifacts",
        "brief",
        "briefs",
        "checklist",
        "checklists",
        "doc",
        "docs",
        "document",
        "documents",
        "evidence",
        "file",
        "files",
        "note",
        "notes",
        "output",
        "outputs",
        "packet",
        "packets",
        "record",
        "records",
        "report",
        "reports"
    };

    private static readonly HashSet<string> ArtifactContentNoiseTokens = new(StringComparer.Ordinal)
    {
        "and",
        "are",
        "capture",
        "captured",
        "create",
        "created",
        "form",
        "must",
        "required",
        "should",
        "the",
        "this",
        "with"
    };

    public static bool HasExpectedArtifactContentSignals(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string responseText,
        string normalizedResponse,
        bool containsArtifactResponseSection)
    {
        if (containsArtifactResponseSection)
        {
            return HasExpectedArtifactValidationSignals(expectedArtifact, normalizedResponse);
        }

        var responseTokens = TokenizeArtifactContentSignalText(normalizedResponse)
            .ToHashSet(StringComparer.Ordinal);
        if (responseTokens.Count == 0)
        {
            return false;
        }

        var titleTokens = TokenizeArtifactContentSignalText(expectedArtifact.Title)
            .ToList();
        if (titleTokens.Count >= 2)
        {
            var requiredTitleMatches = Math.Min(2, titleTokens.Count);
            if (titleTokens.Count(responseTokens.Contains) < requiredTitleMatches)
            {
                return false;
            }
        }

        return HasExpectedArtifactValidationSignals(expectedArtifact, responseTokens);
    }

    public static bool HasExpectedArtifactValidationSignals(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        string normalizedResponse)
    {
        var responseTokens = TokenizeArtifactContentSignalText(normalizedResponse)
            .ToHashSet(StringComparer.Ordinal);
        return HasExpectedArtifactValidationSignals(expectedArtifact, responseTokens);
    }

    public static bool HasExpectedArtifactValidationSignals(
        ProcessArtifactExpectationSnapshot expectedArtifact,
        IReadOnlySet<string> responseTokens)
    {
        var validationTokens = TokenizeArtifactContentSignalText(expectedArtifact.ValidationRequirementSummary)
            .ToList();
        if (validationTokens.Count < 3)
        {
            return true;
        }

        return validationTokens.Count(responseTokens.Contains) >= Math.Min(2, validationTokens.Count);
    }

    public static IReadOnlyList<string> TokenizeArtifactContentSignalText(string value)
    {
        return TokenizeArtifactComparisonText(value)
            .Where(token => token.Length > 2)
            .Where(token => !IsArtifactTitleNoiseToken(token))
            .Where(token => !IsArtifactContentNoiseToken(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static IReadOnlyList<string> TokenizeVisualArtifactMatchText(string value)
    {
        return TokenizeArtifactComparisonText(value)
            .Where(token => !IsArtifactTitleNoiseToken(token))
            .Where(token => !IsArtifactContentNoiseToken(token))
            .Where(token => !token.All(char.IsDigit))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    public static bool MatchesExpectedArtifactByTitleTokens(
        string expectedTitle,
        string relativePath,
        string displayName)
    {
        var expectedTokens = TokenizeArtifactComparisonText(expectedTitle)
            .Where(token => !IsArtifactTitleNoiseToken(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (expectedTokens.Count < 2)
        {
            return false;
        }

        var observedTokens = TokenizeArtifactComparisonText(relativePath)
            .Concat(TokenizeArtifactComparisonText(displayName))
            .Distinct(StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal);
        if (observedTokens.Count == 0)
        {
            return false;
        }

        var matchedTokenCount = expectedTokens.Count(observedTokens.Contains);
        return matchedTokenCount >= 2;
    }

    public static bool ContainsNarrativeArtifactSignal(string text)
    {
        return text.Contains("artifact", StringComparison.Ordinal) ||
               text.Contains("evidence", StringComparison.Ordinal) ||
               text.Contains("proof", StringComparison.Ordinal) ||
               text.Contains("report", StringComparison.Ordinal) ||
               text.Contains("review", StringComparison.Ordinal) ||
               text.Contains("validation", StringComparison.Ordinal) ||
               text.Contains("recheck", StringComparison.Ordinal) ||
               text.Contains("regression", StringComparison.Ordinal) ||
               text.Contains("change set", StringComparison.Ordinal);
    }

    public static bool SharesNarrativeArtifactPurpose(string expectedText, string observedText)
    {
        if (expectedText.Contains("evidence", StringComparison.Ordinal) ||
            expectedText.Contains("proof", StringComparison.Ordinal) ||
            expectedText.Contains("regression", StringComparison.Ordinal) ||
            expectedText.Contains("validation", StringComparison.Ordinal) ||
            expectedText.Contains("qa", StringComparison.Ordinal))
        {
            return observedText.Contains("evidence", StringComparison.Ordinal) ||
                   observedText.Contains("proof", StringComparison.Ordinal) ||
                   observedText.Contains("validation", StringComparison.Ordinal) ||
                   observedText.Contains("qa", StringComparison.Ordinal) ||
                   observedText.Contains("recheck", StringComparison.Ordinal) ||
                   observedText.Contains("regression", StringComparison.Ordinal) ||
                   observedText.Contains("test", StringComparison.Ordinal) ||
                   observedText.Contains("browser", StringComparison.Ordinal) ||
                   observedText.Contains("runtime", StringComparison.Ordinal);
        }

        if (expectedText.Contains("change set", StringComparison.Ordinal))
        {
            return observedText.Contains("change", StringComparison.Ordinal) ||
                   observedText.Contains("repair", StringComparison.Ordinal) ||
                   observedText.Contains("mutation", StringComparison.Ordinal);
        }

        return false;
    }

    public static IReadOnlyList<string> TokenizeArtifactComparisonText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var slug = FileSafeSlugBuilder.Build(value);
        return slug
            .Split(['-', '/', '.', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(NormalizeArtifactComparisonToken)
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .ToList();
    }

    public static bool IsArtifactTitleNoiseToken(string token)
        => ArtifactTitleNoiseTokens.Contains(token);

    public static bool IsArtifactContentNoiseToken(string token)
        => ArtifactContentNoiseTokens.Contains(token);

    private static string NormalizeArtifactComparisonToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 3 &&
            normalized.EndsWith('s') &&
            !normalized.EndsWith("ss", StringComparison.Ordinal))
        {
            normalized = normalized[..^1];
        }

        return normalized;
    }
}
