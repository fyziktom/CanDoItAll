using System.Text.RegularExpressions;

namespace CanDoItAll.Processes.Drivers.SoftwareDeliveryEvidence;

public static class SoftwareDeliveryContractRules
{
    public static string JavaScriptContractToken => "javascript";

    public static bool IsDotNetWorkspaceToolName(string normalizedToolName)
    {
        return normalizedToolName.StartsWith("workspace_dotnet_", StringComparison.Ordinal);
    }

    public static SoftwareDeliveryContractSignals ResolveSignals(
        SoftwareDeliveryImplementationContractSnapshot contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var mentionsDotNet = ImplementationContractMentionsDotNet(contract);
        var mentionsJavaScript = ImplementationContractMentionsJavaScript(contract);
        var negatesDotNet = ImplementationContractNegatesDotNet(contract);
        var containsRunnableApplicationSignal = ContainsRunnableApplicationContractSignal(contract);
        return new SoftwareDeliveryContractSignals(
            mentionsDotNet && mentionsJavaScript
                ? SoftwareDeliveryImplementationStack.Mixed
                : mentionsDotNet
                    ? SoftwareDeliveryImplementationStack.DotNet
                    : mentionsJavaScript
                        ? SoftwareDeliveryImplementationStack.JavaScript
                        : contract.RequiresConcreteImplementationProof
                            ? SoftwareDeliveryImplementationStack.Unknown
                            : SoftwareDeliveryImplementationStack.NonSoftware,
            mentionsDotNet,
            mentionsJavaScript,
            negatesDotNet,
            containsRunnableApplicationSignal,
            RequiresSourceOrProjectImplementationProof(containsRunnableApplicationSignal));
    }

    public static bool ContainsProjectFileSignal(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return text.Contains(".csproj", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".slnx", StringComparison.OrdinalIgnoreCase) ||
               text.Contains(".sln", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ContainsDevelopmentProfileSignal(string text)
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

    public static bool ContainsRunnableApplicationContractSignal(
        SoftwareDeliveryImplementationContractSnapshot contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var text = contract.ContractText;
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

    public static bool ImplementationContractMentionsTests(
        SoftwareDeliveryImplementationContractSnapshot contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (!contract.RequiresConcreteImplementationProof ||
            !ContainsRunnableApplicationContractSignal(contract))
        {
            return false;
        }

        return ContainsExplicitImplementationTestRequest(contract.TriggerText);
    }

    public static bool ImplementationContractMentionsDotNet(
        SoftwareDeliveryImplementationContractSnapshot contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var text = contract.ContractText;
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

    public static bool ImplementationContractMentionsJavaScript(
        SoftwareDeliveryImplementationContractSnapshot contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var text = contract.ContractText;
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

    public static bool ImplementationContractNegatesDotNet(
        SoftwareDeliveryImplementationContractSnapshot contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var text = contract.ContractText;
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

    public static bool RequiresCurrentAttemptProductMutation(
        SoftwareDeliveryImplementationContractSnapshot contract)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (!contract.RequiresConcreteImplementationProof)
        {
            return false;
        }

        var text = contract.ContractText;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return ContainsContractWord(text, "repair") ||
               ContainsContractWord(text, "repaired") ||
               ContainsContractWord(text, "rework") ||
               ContainsContractWord(text, "remediation") ||
               ContainsContractWord(text, "remediate") ||
               ContainsContractWord(text, "fix") ||
               ContainsContractWord(text, "fixes");
    }

    public static bool ContainsExplicitImplementationTestRequest(string text)
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

    public static bool ContainsNegatedImplementationStackToken(string text, string token)
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

    public static bool ContainsAffirmativeImplementationStackToken(string text, string token)
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

    public static bool ContainsAffirmativeImplementationStackPattern(
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

    public static bool IsNegatedImplementationStackMention(string text, int matchIndex)
    {
        var prefixStart = Math.Max(0, matchIndex - 64);
        var prefix = text[prefixStart..matchIndex];
        return Regex.IsMatch(
            prefix,
            @"(?:\bnot\s+(?:a\s+)?$|\bno\s+$|\bnon[-\s]+$|\bnegated\s+$|\bwithout\s+$|\bnever\s+$|\bdo\s+not\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$|\bdon't\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$|\bmust\s+not\s+(?:use\s+|call\s+|default\s+to\s+)?[^.;:\r\n]{0,48}$)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    public static bool ContainsContractWord(string text, string word)
    {
        return Regex.IsMatch(
            text,
            $@"(?<![A-Za-z0-9_]){Regex.Escape(word)}(?![A-Za-z0-9_])",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static bool RequiresSourceOrProjectImplementationProof(bool containsRunnableApplicationContractSignal)
    {
        return containsRunnableApplicationContractSignal;
    }
}

public sealed record SoftwareDeliveryContractSignals(
    SoftwareDeliveryImplementationStack Stack,
    bool MentionsDotNet,
    bool MentionsJavaScript,
    bool NegatesDotNet,
    bool ContainsRunnableApplicationSignal,
    bool RequiresSourceOrProjectImplementationProof);
