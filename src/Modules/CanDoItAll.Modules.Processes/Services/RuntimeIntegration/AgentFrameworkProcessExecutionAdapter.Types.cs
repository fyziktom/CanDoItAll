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

internal sealed partial class AgentFrameworkProcessExecutionAdapter
{
    [GeneratedRegex(
        @"(?<![0-9a-fA-F])[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}(?![0-9a-fA-F])",
        RegexOptions.CultureInvariant)]
    private static partial Regex ProcessRunIdRegex();

    [GeneratedRegex("[^A-Za-z0-9._-]+", RegexOptions.CultureInvariant)]
    private static partial Regex ManagedArtifactPathSegmentInvalidCharactersRegex();

    [GeneratedRegex(@"\bStatus\s*:\s*(?:in\s*progress|inprogress|progress|working|running|started)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex NonTerminalStatusDeclarationRegex();

    [GeneratedRegex(@"^\s*-\s*(?<key>[A-Za-z0-9][A-Za-z0-9._-]*)\s*:\s*(?<rest>[^\r\n]*)$", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex BranchOutcomePromptLineRegex();

    [GeneratedRegex(@"^\s*(?:\*\*)?Branch\s+outcome\s+key(?:\*\*)?\s*:\s*`?(?<key>[A-Za-z0-9][A-Za-z0-9._-]*)`?\s*\.?\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitBranchOutcomeKeyLineRegex();

    [GeneratedRegex(@"\b(?:missing|required)\b[^\r\n]{0,80}\breceipt\b|\breceipt\b[^\r\n]{0,80}\bmissing\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MissingRequiredReceiptRegex();

    [GeneratedRegex(
        @"(?ix)
        (?:managed-files[/\\]project-media[/\\](?:files|images)[/\\][^\s`""'<>]+)
        |(?:artifacts[/\\](?:scopes[/\\][^\s`""'<>]+[/\\])?process-runs[/\\][0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}(?:[/\\][^\s`""'<>]+)*)
        |(?:external-target[/\\][^\s`""'<>]+)
        |(?:(?<![a-z])[a-z]:[/\\][^\s`""'<>]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OutcomePathReferenceRegex();

    private sealed record ProductCompletionRequiredFileContentCheckResolution(
        IReadOnlyList<ProductCompletionRequiredFileContentCheck> Checks,
        string InvalidReason)
    {
        public static ProductCompletionRequiredFileContentCheckResolution Empty { get; } = new([], string.Empty);

        public static ProductCompletionRequiredFileContentCheckResolution Invalid(string reason)
            => new([], reason);
    }

    private sealed record ProductCompletionRequiredFileContentCheck(
        IReadOnlyList<string> PathCandidates,
        IReadOnlyList<IReadOnlyList<string>> RequiredTextAnyGroups,
        IReadOnlyList<IReadOnlyList<string>> ForbiddenTextAnyGroups,
        bool MustExist,
        IReadOnlyList<string> EnforceBranchOutcomeKeys);

    private sealed record ProcessCompletionIssue(
        string Code,
        string Summary,
        string Evidence,
        IReadOnlyList<ArtifactSlotId> RequestedArtifactSlotIds,
        ProcessDiagnosticRetrySafety RetrySafety,
        ProcessDiagnosticIdempotencyClassification Idempotency);

    private sealed record ProductRootInspection(
        bool HasProductFiles,
        string Summary);
}
