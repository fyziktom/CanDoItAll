using System.Text.RegularExpressions;

namespace CanDoItAll.Modules.Processes;

internal sealed record ProcessImplementationContractSnapshot(string Text, string TriggerText)
{
    internal static ProcessImplementationContractSnapshot Create(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        string? additionalContext = null)
    {
        var contractTextParts = new[]
            {
                candidate.StepRun.Title,
                candidate.WorkBrief?.Title,
                candidate.WorkBrief?.WorkBriefText,
                candidate.WorkBrief?.ExpectedOutcome,
                candidate.WorkBrief?.EvidenceExpectationSummary,
                additionalContext
            }
            .Concat(candidate.ExpectedArtifacts.Select(item => item.Title))
            .Concat(candidate.ExpectedArtifacts.Select(item => item.ValidationRequirementSummary));
        var triggerText = ProcessProjectStructureContextFormatter.RemoveSerializedContext(candidate.Run.TriggerReason);
        var triggerTextParts = new[]
        {
            triggerText,
            candidate.StepRun.Title,
            candidate.WorkBrief?.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary
        };

        return new ProcessImplementationContractSnapshot(
            CollapsePromptWhitespace(string.Join(' ', contractTextParts)),
            CollapsePromptWhitespace(string.Join(' ', triggerTextParts)));
    }

    internal static bool RequiresCurrentAttemptProductMutation(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        bool requiresConcreteImplementationProof)
    {
        if (!requiresConcreteImplementationProof)
        {
            return false;
        }

        var text = Create(candidate).Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ProcessImplementationStackRules.ContainsContractWord(text, "repair") ||
               ProcessImplementationStackRules.ContainsContractWord(text, "repaired") ||
               ProcessImplementationStackRules.ContainsContractWord(text, "rework") ||
               ProcessImplementationStackRules.ContainsContractWord(text, "remediation") ||
               ProcessImplementationStackRules.ContainsContractWord(text, "remediate") ||
               ProcessImplementationStackRules.ContainsContractWord(text, "fix") ||
               ProcessImplementationStackRules.ContainsContractWord(text, "fixes");
    }

    private static string CollapsePromptWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return Regex.Replace(
                value,
                @"\s+",
                " ",
                RegexOptions.CultureInvariant)
            .Trim();
    }
}

internal static class ProcessImplementationStackRules
{
    internal static string JavaScriptContractToken => "javascript";

    internal static bool IsDotNetWorkspaceToolName(string normalizedToolName)
    {
        return normalizedToolName.StartsWith("workspace_dotnet_", StringComparison.Ordinal);
    }

    internal static bool ContainsProjectFileSignal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains(".csproj", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".slnx", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".sln", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ContainsDevelopmentProfileSignal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsContractWord(text, "developer") ||
               ContainsContractWord(text, "engineer") ||
               ContainsContractWord(text, "implementation") ||
               ContainsContractWord(text, "implement") ||
               ContainsContractWord(text, "build") ||
               ContainsContractWord(text, "code") ||
               ContainsAffirmativeImplementationStackToken(text, "blazor") ||
               ContainsAffirmativeImplementationStackToken(text, ".net") ||
               ContainsAffirmativeImplementationStackToken(text, "dotnet") ||
               ContainsAffirmativeImplementationStackToken(text, "c#");
    }

    internal static bool ContainsRunnableApplicationContractSignal(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate)
    {
        var text = ProcessImplementationContractSnapshot.Create(candidate).Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsContractWord(text, "application") ||
               ContainsContractWord(text, "app") ||
               ContainsContractWord(text, "api") ||
               ContainsContractWord(text, "service") ||
               ContainsContractWord(text, "host") ||
               ContainsContractWord(text, "startup") ||
               ContainsContractWord(text, "runnable") ||
               ContainsContractWord(text, "browser") ||
               ContainsContractWord(text, "ui") ||
               text.Contains("asp.net", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".csproj", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool ImplementationContractMentionsTests(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        bool requiresConcreteImplementationProof)
    {
        if (!requiresConcreteImplementationProof ||
            !ContainsRunnableApplicationContractSignal(candidate))
        {
            return false;
        }

        var text = ProcessImplementationContractSnapshot.Create(candidate).TriggerText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsExplicitImplementationTestRequest(text);
    }

    internal static bool ImplementationContractMentionsDotNet(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        string? additionalContext = null)
    {
        var text = ProcessImplementationContractSnapshot.Create(candidate, additionalContext).Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsAffirmativeImplementationStackToken(text, ".net") ||
               ContainsAffirmativeImplementationStackToken(text, "asp.net") ||
               ContainsAffirmativeImplementationStackToken(text, "dotnet") ||
               ContainsAffirmativeImplementationStackToken(text, "c#") ||
               ContainsAffirmativeImplementationStackToken(text, "csharp") ||
               ContainsAffirmativeImplementationStackToken(text, "blazor") ||
               ContainsAffirmativeImplementationStackToken(text, "razor") ||
               ContainsAffirmativeImplementationStackToken(text, ".csproj") ||
               ContainsAffirmativeImplementationStackToken(text, ".sln") ||
               ContainsAffirmativeImplementationStackToken(text, ".slnx") ||
               ContainsAffirmativeImplementationStackToken(text, "nuget");
    }

    internal static bool ImplementationContractMentionsJavaScript(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        string? additionalContext = null)
    {
        var text = ProcessImplementationContractSnapshot.Create(candidate, additionalContext).Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsAffirmativeImplementationStackToken(text, "javascript") ||
               ContainsAffirmativeImplementationStackToken(text, "typescript") ||
               ContainsAffirmativeImplementationStackToken(text, "package.json") ||
               ContainsAffirmativeImplementationStackToken(text, ".mjs") ||
               ContainsAffirmativeImplementationStackToken(text, ".cjs") ||
               ContainsAffirmativeImplementationStackPattern(
                   text,
                   @"(?:^|[^a-z0-9])(?:js|node\.?js|npm|pnpm|yarn|vite|react|vue|svelte)(?:[^a-z0-9]|$)",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool ImplementationContractNegatesDotNet(
        ProcessRunAutomationDispatchService.DispatchCandidate candidate,
        string? additionalContext = null)
    {
        var text = ProcessImplementationContractSnapshot.Create(candidate, additionalContext).Text;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsNegatedImplementationStackToken(text, ".net") ||
               ContainsNegatedImplementationStackToken(text, "asp.net") ||
               ContainsNegatedImplementationStackToken(text, "dotnet") ||
               ContainsNegatedImplementationStackToken(text, "c#") ||
               ContainsNegatedImplementationStackToken(text, "csharp") ||
               ContainsNegatedImplementationStackToken(text, "blazor") ||
               ContainsNegatedImplementationStackToken(text, "razor") ||
               ContainsNegatedImplementationStackToken(text, ".csproj") ||
               ContainsNegatedImplementationStackToken(text, ".sln") ||
               ContainsNegatedImplementationStackToken(text, ".slnx") ||
               ContainsNegatedImplementationStackToken(text, "nuget");
    }

    internal static bool ContainsExplicitImplementationTestRequest(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains("dotnet test", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".Tests.csproj", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".Test.csproj", StringComparison.OrdinalIgnoreCase) ||
               ContainsContractWord(text, "MSTest") ||
               ContainsContractWord(text, "xUnit") ||
               ContainsContractWord(text, "NUnit") ||
               Regex.IsMatch(
                   text,
                   @"\b(?:add|create|write|include|implement|update|run|rerun|execute)\s+(?:the\s+)?(?:relevant\s+)?(?:automated\s+)?(?:unit\s+|integration\s+|regression\s+)?tests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\bwith\s+(?:automated\s+)?(?:unit\s+|integration\s+|regression\s+)?tests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\b(?:prove|verify|validate)\b.{0,80}\btests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\b(?:implement|deliver|build)\b.{0,80}\btests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\btests?\s+(?:pass|passes|passing|succeed|succeeds)\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\b(?:unit|integration|regression)\s+tests?\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
               Regex.IsMatch(
                   text,
                   @"\btest\s+project\b",
                   RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool ContainsNegatedImplementationStackToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(token)}(?![A-Za-z0-9_])";
        return Regex
            .Matches(text, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .Any(match => IsNegatedImplementationStackMention(text, match.Index));
    }

    internal static bool ContainsAffirmativeImplementationStackToken(string text, string token)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var pattern = $@"(?<![A-Za-z0-9_]){Regex.Escape(token)}(?![A-Za-z0-9_])";
        return ContainsAffirmativeImplementationStackPattern(
            text,
            pattern,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool ContainsAffirmativeImplementationStackPattern(
        string text,
        string pattern,
        RegexOptions options)
    {
        foreach (Match match in Regex.Matches(text, pattern, options))
        {
            if (!IsNegatedImplementationStackMention(text, match.Index))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsNegatedImplementationStackMention(string text, int matchIndex)
    {
        var prefixStart = Math.Max(0, matchIndex - 64);
        var prefix = text[prefixStart..matchIndex];
        return Regex.IsMatch(
            prefix,
            @"(?:\bnot\s+(?:a\s+)?$|\bno\s+$|\bnon[-\s]+$|\bnegated\s+$|\bwithout\s+$|\bnever\s+$|\bdo\s+not\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$|\bdon't\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$|\bmust\s+not\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    internal static bool ContainsContractWord(string text, string word)
    {
        return Regex.IsMatch(
            text,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(word)}(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
