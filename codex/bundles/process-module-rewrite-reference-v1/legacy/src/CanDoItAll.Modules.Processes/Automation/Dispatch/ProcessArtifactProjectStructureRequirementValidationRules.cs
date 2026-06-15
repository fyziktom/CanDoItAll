using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactProjectStructureRequirementValidationRules
{
    private static readonly HashSet<string> ProjectStructureRequirementNoiseTokens = new(StringComparer.Ordinal)
    {
        "acceptance",
        "constraint",
        "criteria",
        "from",
        "grounded",
        "project",
        "projectblock",
        "requirement",
        "requirements",
        "selected",
        "structure",
        "type"
    };

    public static string ResolveDowngradedProjectStructureRequirementSummary(
        string contractText,
        string? sourcePromptText,
        string? inspectionText)
    {
        if (!ExpectsProjectStructureRequirementPreservation(contractText) ||
            string.IsNullOrWhiteSpace(inspectionText))
        {
            return string.Empty;
        }

        var sourceLines = ResolveGroundedProjectStructureRequirementLines(sourcePromptText);
        if (sourceLines.Count == 0)
        {
            return string.Empty;
        }

        var weakeningStatements = SplitRequirementStatements(inspectionText)
            .Where(ContainsRequirementWeakeningPhrase)
            .ToList();
        if (weakeningStatements.Count == 0)
        {
            return string.Empty;
        }

        foreach (var sourceLine in sourceLines)
        {
            var sourceTokens = TokenizeProjectStructureRequirementText(sourceLine).ToHashSet(StringComparer.Ordinal);
            if (sourceTokens.Count < 2)
            {
                continue;
            }

            foreach (var weakeningStatement in weakeningStatements)
            {
                var weakenedTokens = TokenizeProjectStructureRequirementText(weakeningStatement).ToHashSet(StringComparer.Ordinal);
                var sharedTokenCount = sourceTokens.Count(weakenedTokens.Contains);
                if (sharedTokenCount < Math.Min(2, sourceTokens.Count))
                {
                    continue;
                }

                return $"the response downgrades a grounded project-structure requirement: {TrimForPrompt(sourceLine, 160)}";
            }
        }

        return string.Empty;
    }

    public static bool ExpectsProjectStructureRequirementPreservation(string contractText)
    {
        if (string.IsNullOrWhiteSpace(contractText))
        {
            return false;
        }

        var normalized = contractText.ToLowerInvariant();
        return normalized.Contains("project-structure", StringComparison.Ordinal) &&
               (normalized.Contains("downgrad", StringComparison.Ordinal) ||
                normalized.Contains("dropped", StringComparison.Ordinal) ||
                normalized.Contains("deferred", StringComparison.Ordinal) ||
                normalized.Contains("preserve", StringComparison.Ordinal) ||
                normalized.Contains("source of truth", StringComparison.Ordinal) ||
                normalized.Contains("source-of-truth", StringComparison.Ordinal));
    }

    public static IReadOnlyList<string> ResolveGroundedProjectStructureRequirementLines(string? promptText)
    {
        if (string.IsNullOrWhiteSpace(promptText))
        {
            return [];
        }

        var lines = promptText.Split(["\r\n", "\n"], StringSplitOptions.None);
        var result = new List<string>();
        var inGrounding = false;
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Equals("Live project structure grounding:", StringComparison.OrdinalIgnoreCase))
            {
                inGrounding = true;
                continue;
            }

            if (!inGrounding)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(trimmed))
            {
                break;
            }

            if (!trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                continue;
            }

            var requirementLine = trimmed.TrimStart('-', ' ');
            if (IsNonMandatoryProjectStructureSourceLine(requirementLine))
            {
                continue;
            }

            result.Add(requirementLine);
        }

        return result;
    }

    public static bool IsNonMandatoryProjectStructureSourceLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return true;
        }

        var normalized = CollapsePromptWhitespace(line).ToLowerInvariant();
        return normalized.Contains("optional", StringComparison.Ordinal) ||
               normalized.Contains("not required", StringComparison.Ordinal) ||
               normalized.Contains("not mandatory", StringComparison.Ordinal) ||
               normalized.Contains("nice to have", StringComparison.Ordinal) ||
               normalized.Contains("follow-up", StringComparison.Ordinal) ||
               normalized.Contains("follow up", StringComparison.Ordinal) ||
               normalized.Contains("later", StringComparison.Ordinal) ||
               normalized.Contains("future", StringComparison.Ordinal) ||
               normalized.Contains("out of scope", StringComparison.Ordinal) ||
               normalized.Contains("excluded", StringComparison.Ordinal) ||
               normalized.Contains("defer", StringComparison.Ordinal) ||
               normalized.Contains("maybe", StringComparison.Ordinal) ||
               normalized.Contains("if desired", StringComparison.Ordinal) ||
               normalized.Contains("if needed", StringComparison.Ordinal) ||
               normalized.Contains("as applicable", StringComparison.Ordinal) ||
               normalized.StartsWith("no ", StringComparison.Ordinal) ||
               normalized.Contains(" no backend", StringComparison.Ordinal) ||
               normalized.Contains("without backend", StringComparison.Ordinal) ||
               normalized.Contains("must not", StringComparison.Ordinal) ||
               normalized.Contains("do not", StringComparison.Ordinal) ||
               normalized.Contains("should not", StringComparison.Ordinal) ||
               normalized.Contains("never ", StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> SplitRequirementStatements(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return Regex
            .Split(text, @"(?<=[.!?])\s+|\r?\n+")
            .Select(statement => statement.Trim().TrimStart('-', '*', ' '))
            .Where(statement => !string.IsNullOrWhiteSpace(statement))
            .ToList();
    }

    public static bool ContainsRequirementWeakeningPhrase(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            return false;
        }

        var normalized = CollapsePromptWhitespace(statement).ToLowerInvariant();
        if (normalized.Contains("not optional", StringComparison.Ordinal) ||
            normalized.Contains("not deferred", StringComparison.Ordinal) ||
            normalized.Contains("not excluded", StringComparison.Ordinal) ||
            normalized.Contains("must not be optional", StringComparison.Ordinal) ||
            normalized.Contains("must not be deferred", StringComparison.Ordinal))
        {
            return false;
        }

        return normalized.Contains("optional", StringComparison.Ordinal) ||
               normalized.Contains("not required", StringComparison.Ordinal) ||
               normalized.Contains("not needed", StringComparison.Ordinal) ||
               normalized.Contains("not mandatory", StringComparison.Ordinal) ||
               normalized.Contains("out of scope", StringComparison.Ordinal) ||
               normalized.Contains("not in scope", StringComparison.Ordinal) ||
               normalized.Contains("excluded from acceptance", StringComparison.Ordinal) ||
               normalized.Contains("future enhancement", StringComparison.Ordinal) ||
               normalized.Contains("follow-up work", StringComparison.Ordinal) ||
               normalized.Contains("follow up work", StringComparison.Ordinal) ||
               normalized.Contains("can be deferred", StringComparison.Ordinal) ||
               normalized.Contains("may be deferred", StringComparison.Ordinal) ||
               normalized.Contains("deferred to", StringComparison.Ordinal) ||
               normalized.Contains("later phase", StringComparison.Ordinal) ||
               normalized.Contains("nice to have", StringComparison.Ordinal);
    }

    public static IReadOnlyList<string> TokenizeProjectStructureRequirementText(string value)
    {
        return ProcessArtifactTextMatchRules.TokenizeArtifactComparisonText(value)
            .Select(NormalizeProjectStructureRequirementToken)
            .Where(token => token.Length > 2)
            .Where(token => !ProcessArtifactTextMatchRules.IsArtifactTitleNoiseToken(token))
            .Where(token => !ProcessArtifactTextMatchRules.IsArtifactContentNoiseToken(token))
            .Where(token => !ProjectStructureRequirementNoiseTokens.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string NormalizeProjectStructureRequirementToken(string token)
    {
        return string.Equals(token, "locally", StringComparison.Ordinal)
            ? "local"
            : token;
    }

    private static string TrimForPrompt(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ReplaceLineEndings(" ").Trim();
        if (normalized.Length <= maxLength)
        {
            return normalized;
        }

        return normalized[..maxLength].TrimEnd() + "...";
    }

    private static string CollapsePromptWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
