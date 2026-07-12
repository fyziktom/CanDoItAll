using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.AgentFramework.Hosting;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Drivers.Standard;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;



namespace CanDoItAll.Modules.Processes;

internal static class ProcessCompletionText
{
    private static readonly Regex ManagedArtifactPathSegmentInvalidCharacters = new(
        "[^A-Za-z0-9._-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex NonTerminalStatusDeclaration = new(
        @"\bStatus\s*:\s*(?:in\s*progress|inprogress|progress|working|running|started)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex ExplicitBranchOutcomeKeyLine = new(
        @"^\s*(?:\*\*)?Branch\s+outcome\s+key(?:\*\*)?\s*:\s*`?(?<key>[A-Za-z0-9][A-Za-z0-9._-]*)`?\s*\.?\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex MissingRequiredReceipt = new(
        @"\bmissing\b[^\r\n]{0,80}\breceipts?\b|\breceipts?\b[^\r\n]{0,80}\b(?:missing|absent|unavailable)\b|\brequired\b[^\r\n]{0,80}\breceipts?\b[^\r\n]{0,80}\b(?:missing|absent|unavailable|not\s+(?:yet\s+)?(?:captured|recorded|produced|present))\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex OutcomePathReference = new(
        @"(?ix)
        (?:managed-files[/\\]project-media[/\\](?:files|images)[/\\][^\s`""'<>]+)
        |(?:artifacts[/\\](?:scopes[/\\][^\s`""'<>]+[/\\])?process-runs[/\\][0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(?:[/\\][^\s`""'<>]+)*)
        |(?:external-target[/\\][^\s`""'<>]+)
        |(?:(?<![a-z])[a-z]:[/\\][^\s`""'<>]+)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    internal static Regex ManagedArtifactPathSegmentInvalidCharactersRegex()
        => ManagedArtifactPathSegmentInvalidCharacters;

    internal static Regex NonTerminalStatusDeclarationRegex()
        => NonTerminalStatusDeclaration;

    internal static Regex ExplicitBranchOutcomeKeyLineRegex()
        => ExplicitBranchOutcomeKeyLine;

    internal static Regex MissingRequiredReceiptRegex()
        => MissingRequiredReceipt;

    internal static Regex OutcomePathReferenceRegex()
        => OutcomePathReference;

    internal static IEnumerable<string?> EnumerateOutcomeText(ProcessStepOutcomeResult output)
    {
        yield return output.Reason;
        yield return output.BranchOutcomeKey;
        yield return output.BranchOutcomeTitle;
        yield return output.HumanReadableSummaryMarkdown;

        foreach (var evidenceRef in output.EvidenceRefs)
        {
            yield return evidenceRef;
        }

        foreach (var nextAction in output.NextActions)
        {
            yield return nextAction;
        }
    }
}

internal sealed record ProductCompletionRequiredFileContentCheckResolution(
    IReadOnlyList<ProductCompletionRequiredFileContentCheck> Checks,
    string InvalidReason)
{
    public static ProductCompletionRequiredFileContentCheckResolution Empty { get; } = new([], string.Empty);

    public static ProductCompletionRequiredFileContentCheckResolution Invalid(string reason)
        => new([], reason);
}

internal sealed record ProductCompletionRequiredFileContentCheck(
    IReadOnlyList<string> PathCandidates,
    IReadOnlyList<IReadOnlyList<string>> RequiredTextAnyGroups,
    IReadOnlyList<IReadOnlyList<string>> ForbiddenTextAnyGroups,
    bool MustExist,
    IReadOnlyList<string> EnforceBranchOutcomeKeys,
    IReadOnlyList<string> EvidenceBranchOutcomeKeys);

internal sealed record ProductCompletionRequiredToolReceiptRule(
    string ToolReceipt,
    IReadOnlyList<string> ApplicableBranchOutcomeKeys,
    IReadOnlyList<string> SkippedBranchOutcomeKeys,
    string Purpose,
    string Key,
    string Reason,
    bool AllowFailedExecutionReceipt = false);

internal sealed record ProductCompletionRequiredToolReceiptRequirement(
    string ToolReceipt,
    bool AllowFailedExecutionReceipt);

internal sealed record ProcessCompletionIssueRoute(
    string IssueCode,
    IReadOnlyList<string> SourceBranchOutcomeKeys,
    string TargetBranchOutcomeKey,
    string TargetBranchOutcomeTitle,
    bool RequiresDefectEvidence,
    bool OnlyAfterAutomaticRetry);

internal sealed record ProductRootInspection(
    bool HasProductFiles,
    string Summary);
