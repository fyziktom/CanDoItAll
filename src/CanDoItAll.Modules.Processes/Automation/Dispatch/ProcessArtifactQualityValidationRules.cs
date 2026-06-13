using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessArtifactQualityValidationRules
{
    public static string ResolveMissingConcreteProofSummary(string? responseText)
    {
        var normalizedResponse = CollapsePromptWhitespace(responseText).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return string.Empty;
        }

        if (normalizedResponse.Contains("browser proof cannot proceed", StringComparison.Ordinal) ||
            normalizedResponse.Contains("browser proof not possible", StringComparison.Ordinal) ||
            normalizedResponse.Contains("browser proof deferred", StringComparison.Ordinal))
        {
            return "the response says browser proof could not proceed";
        }

        if (normalizedResponse.Contains("manual qa: not possible", StringComparison.Ordinal) ||
            normalizedResponse.Contains("manual qa not possible", StringComparison.Ordinal))
        {
            return "the response says manual QA was not possible";
        }

        if (normalizedResponse.Contains("no screenshots", StringComparison.Ordinal) ||
            normalizedResponse.Contains("screenshots: none possible", StringComparison.Ordinal) ||
            normalizedResponse.Contains("screenshots were not possible", StringComparison.Ordinal))
        {
            return "the response says screenshots were not captured";
        }

        if (normalizedResponse.Contains("application is not running", StringComparison.Ordinal) ||
            normalizedResponse.Contains("app is not running", StringComparison.Ordinal) ||
            normalizedResponse.Contains("no running app", StringComparison.Ordinal) ||
            normalizedResponse.Contains("no runnable output", StringComparison.Ordinal))
        {
            return "the response says the app was not running";
        }

        if (ContainsReportedBrowserRuntimeFailure(normalizedResponse))
        {
            return "the response says browser proof saw an application runtime error";
        }

        if (normalizedResponse.Contains("cannot validate ui", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ui validation can not be performed", StringComparison.Ordinal) ||
            normalizedResponse.Contains("ui validation cannot be performed", StringComparison.Ordinal))
        {
            return "the response says UI validation could not be performed";
        }

        return string.Empty;
    }

    public static string ResolveInvalidQualityValidationProofSummary(IReadOnlyList<string> evidenceTexts)
    {
        if (evidenceTexts.Count == 0)
        {
            return string.Empty;
        }

        if (evidenceTexts.Any(ContainsBuildWarningEvidence))
        {
            return "build validation output contains warnings; release-quality proof must be warning-free unless the process explicitly accepts the warning";
        }

        if (evidenceTexts.Any(ContainsZeroTestRunEvidence))
        {
            return "test validation output reports zero executed tests; a zero-test success is missing test proof";
        }

        return string.Empty;
    }

    public static bool ContainsQualityValidationContractSignal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsContractWord(text, "qa") ||
               ContainsContractWord(text, "quality") ||
               ContainsContractWord(text, "validation") ||
               ContainsContractWord(text, "validate") ||
               ContainsContractWord(text, "verification") ||
               ContainsContractWord(text, "verify") ||
               ContainsContractWord(text, "regression") ||
               ContainsContractWord(text, "release") ||
               ContainsContractWord(text, "build") ||
               ContainsExplicitImplementationTestRequest(text);
    }

    public static bool IsQualityValidationEvidenceToolName(
        string normalizedToolName,
        Func<string, bool> isImplementationValidationToolName)
    {
        return !string.IsNullOrWhiteSpace(normalizedToolName) &&
               (isImplementationValidationToolName(normalizedToolName) ||
                string.Equals(normalizedToolName, "workspace_pwsh_run_script", StringComparison.Ordinal));
    }

    public static bool ContainsConcreteBrowserProofSignal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = RemoveApplicabilityOnlyBrowserEvidencePhrases(CollapsePromptWhitespace(value));
        return normalized.Contains("browser proof", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("screenshot", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("screenshots", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("manual qa", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("ui validation", StringComparison.OrdinalIgnoreCase);
    }

    public static string RemoveApplicabilityOnlyBrowserEvidencePhrases(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = CollapsePromptWhitespace(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        var result = Regex.Replace(
            normalized,
            @"\bruntime\s*(?:/|\bor\b)\s*(?:api\s*(?:/|\bor\b)\s*)?browser\s+(?:validation|evidence|proof)\s+as\s+applicable\b",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        result = Regex.Replace(
            result,
            @"\bruntime\s+or\s+browser\s+proof\b",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        result = Regex.Replace(
            result,
            @"\bbrowser\s+(?:validation|evidence|proof)\s+as\s+applicable\b",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        result = Regex.Replace(
            result,
            @"\bscreenshots?\s+(?:only\s+)?for\s+ui\s+surfaces?\b",
            " ",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return CollapsePromptWhitespace(result);
    }

    public static bool IsPlaceholderCriticalToolRequestSummary(
        string normalizedToolName,
        string? requestSummary)
    {
        if (string.IsNullOrWhiteSpace(normalizedToolName))
        {
            return false;
        }

        var normalizedSummary = NormalizeToolToken(requestSummary ?? string.Empty);
        if (string.IsNullOrWhiteSpace(normalizedSummary))
        {
            return true;
        }

        if (string.Equals(normalizedSummary, normalizedToolName, StringComparison.Ordinal))
        {
            return true;
        }

        return normalizedToolName.StartsWith("workspace_", StringComparison.Ordinal) &&
               string.Equals(
                   normalizedSummary,
                   normalizedToolName["workspace_".Length..],
                   StringComparison.Ordinal);
    }

    public static bool ContainsBuildWarningEvidence(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = CollapsePromptWhitespace(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        return Regex.IsMatch(
                   normalized,
                   @"\bwarning\s+(?:CS|NU|MSB|CA|IL|NETSDK|ASP)\d+\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   normalized,
                   @"(?<!\d)[1-9]\d*\s+(?:warning(?:s|\(s\))?|upozorn\S*)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool ContainsZeroTestRunEvidence(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return Regex.IsMatch(
                   value,
                   @"(?im)^\s*#?\s*tests\s+0\s*$",
                   RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   value,
                   @"\btotal\s+tests\s*:\s*0\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   value,
                   @"\b(?:ran|run|executed|discovered|found|total)\s+0\s+tests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   value,
                   @"\b0\s+tests?\s+(?:ran|run|executed|discovered|found)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               value.Contains("no tests found", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("no test files found", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("no matching tests", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("no test is available", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("no tests are available", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("nejsou dostupn\u00e9 \u017e\u00e1dn\u00e9 testy", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsReportedBrowserRuntimeFailure(string normalizedResponse)
    {
        if (string.IsNullOrWhiteSpace(normalizedResponse))
        {
            return false;
        }

        return normalizedResponse.Contains("application error", StringComparison.Ordinal) ||
               normalizedResponse.Contains("app error", StringComparison.Ordinal) ||
               normalizedResponse.Contains("http 500", StringComparison.Ordinal) ||
               normalizedResponse.Contains("http error 500", StringComparison.Ordinal) ||
               normalizedResponse.Contains("unhandled exception", StringComparison.Ordinal) ||
               normalizedResponse.Contains("root route returned 500", StringComparison.Ordinal) ||
               normalizedResponse.Contains("root route shows an error", StringComparison.Ordinal) ||
               normalizedResponse.Contains("route shows an application error", StringComparison.Ordinal);
    }

    private static bool ContainsExplicitImplementationTestRequest(string text)
    {
        return ProcessImplementationStackRules.ContainsExplicitImplementationTestRequest(text);
    }

    private static bool ContainsContractWord(string text, string word)
    {
        return Regex.IsMatch(
            text,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(word)}(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string NormalizeToolToken(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace('-', '_').Trim().ToLowerInvariant();
    }

    private static string CollapsePromptWhitespace(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
